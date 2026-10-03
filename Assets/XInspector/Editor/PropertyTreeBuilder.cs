using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using XInspector.Internal;

namespace XInspector.Editor
{
    /// <summary>
    /// 从 <see cref="SerializedObject"/> 构建属性树。
    /// <para>
    /// 这里是**值后端选择的落点**：成员遍历走 Unity 的序列化属性而非裸反射，
    /// 于是 Undo、预制体覆盖、场景标脏、多对象编辑、域重载后取值全部免费获得。
    /// 反射只用来补一件序列化系统不给的东西——成员的 <see cref="FieldInfo"/>，
    /// 以便读到它身上的特性。
    /// </para>
    /// <para>
    /// 构建分两步：**先按序列化顺序平铺出全部成员，再把它们搬进分组节点**。
    /// 分两步而非边走边插，是因为一个成员属于哪个分组要看它身上的特性，
    /// 而分组的祖先节点可能由后面的成员才首次声明——先有全集再装配，
    /// 就不必处理「边建边补祖先」的顺序依赖。
    /// </para>
    /// </summary>
    internal static class PropertyTreeBuilder
    {
        #region Private Fields

        // 末端绘制器无状态，可以安全共享；建一次即可，不必每个节点新建。
        private static readonly ChildrenDrawer ChildrenTerminal = new ChildrenDrawer();
        private static readonly UnityFallbackDrawer MemberTerminal = new UnityFallbackDrawer();

        #endregion

        #region Public API

        /// <summary>
        /// 构建属性树。
        /// </summary>
        /// <param name="serializedObject">目标序列化对象，其 <c>targetObject</c> 须非空。</param>
        /// <param name="memberFilter">
        /// 成员过滤器；返回 <c>false</c> 的成员不建节点。传 <c>null</c> 等价于全收。
        /// </param>
        /// <returns>构建好的树。</returns>
        public static PropertyTree Build(SerializedObject serializedObject, Func<FieldInfo, bool> memberFilter)
        {
            var targetType = serializedObject.targetObject.GetType();

            var root = new InspectorProperty(
                targetType.Name,
                string.Empty,
                targetType,
                InspectorPropertyKind.Root,
                new PropertyAttributes(CollectTypeAttributes(targetType)));

            var members = CollectMembers(serializedObject, targetType, memberFilter);

            // ---- 顺序是契约，动之前先读完这段 ----
            //
            // 处理器必须在**分组装配之前**跑：类级分组特性是处理器注入到成员上的，
            // 而分组装配必须看到它——顺序反过来，类级 [BoxGroup] 会静默地不生效。
            //
            // 处理器也必须在**挂链之前**跑：注入的特性会改变链条的构成
            // （例如类级 [Title] 被分发到成员身上，那个成员就该多一格标题绘制器）。
            RunProcessors(root, members);

            AttachChain(root, ChildrenTerminal);
            for (var i = 0; i < members.Count; i++)
            {
                AttachChain(members[i], MemberTerminal);
            }

            ApplyGrouping(root, members);

            return new PropertyTree(serializedObject, root);
        }

        #endregion

        #region 成员收集

        /// <summary>
        /// 按 Unity 的序列化顺序平铺出全部成员节点。
        /// </summary>
        /// <param name="serializedObject">目标序列化对象。</param>
        /// <param name="targetType">目标对象的运行时类型。</param>
        /// <param name="memberFilter">成员过滤器；<c>null</c> 表示全收。</param>
        /// <returns>成员节点列表，尚未挂到任何父节点上。</returns>
        private static List<InspectorProperty> CollectMembers(
            SerializedObject serializedObject,
            Type targetType,
            Func<FieldInfo, bool> memberFilter)
        {
            var members = new List<InspectorProperty>();
            var iterator = serializedObject.GetIterator();

            // 与 Editor.DrawDefaultInspector 的遍历方式保持一致：首帧进入子级，
            // 之后只在同层推进。这样字段集合与顺序都与原生 Inspector 逐一对齐。
            if (iterator.NextVisible(true))
            {
                do
                {
                    // 字段信息只解析一次，过滤与建节点共用——顺带避免了两处查找结果不一致。
                    var field = FindField(targetType, iterator.propertyPath);

                    // 被拒的成员**不建节点**，而不是建了再删：少一次分配，
                    // 也不会让链装配看到本不该存在的节点。
                    if (memberFilter != null && !memberFilter(field))
                    {
                        continue;
                    }

                    members.Add(CreateMember(serializedObject, iterator, field));
                }
                while (iterator.NextVisible(false));
            }

            return members;
        }

        /// <summary>
        /// 为一个可见的序列化属性建立成员节点。
        /// </summary>
        /// <param name="serializedObject">底层序列化对象，用于重新取得稳定的属性实例。</param>
        /// <param name="serializedProperty">遍历器当前指向的序列化属性，**仅用于读取名字与路径**。</param>
        /// <param name="field">该成员对应的字段；Unity 注入的成员（如 <c>m_Script</c>）为 <c>null</c>。</param>
        /// <returns>建好的成员节点，尚未挂到父节点上。</returns>
        /// <remarks>
        /// <b>不能把遍历器交出去。</b> <see cref="SerializedObject.GetIterator"/> 返回的是
        /// **同一个实例**，<c>NextVisible</c> 只是就地改写它。若把它存进节点，
        /// 所有节点会共享这一个对象，走完遍历后它们全都指向最后一个属性——
        /// 症状是「Inspector 里每个字段显示的都是同一个值」。
        /// 故这里只用它读名字与路径，随后用 <see cref="SerializedObject.FindProperty"/>
        /// 取一份**独立的**实例交给值入口。
        /// </remarks>
        private static InspectorProperty CreateMember(
            SerializedObject serializedObject,
            SerializedProperty serializedProperty,
            FieldInfo field)
        {
            var path = serializedProperty.propertyPath;
            var name = serializedProperty.name;
            var valueType = field != null ? field.FieldType : typeof(object);
            var stableProperty = serializedObject.FindProperty(path);

            var node = new InspectorProperty(
                name,
                path,
                valueType,
                InspectorPropertyKind.Member,
                new PropertyAttributes(CollectMemberAttributes(field)))
            {
                ValueEntry = new SerializedPropertyValueEntry(stableProperty, valueType),
                Member = field,
            };

            // 链条**不在这里挂**：处理器还没跑，特性尚未最终确定。
            return node;
        }

        #endregion

        #region 特性处理器

        /// <summary>
        /// 跑一遍所有特性处理器，让它们改写节点的特性列表或状态。
        /// </summary>
        /// <param name="root">根节点，类级特性都挂在它上面。</param>
        /// <param name="members">尚未挂到父节点上的成员列表。</param>
        /// <remarks>
        /// <para>
        /// 处理器按 <see cref="AttributeProcessor.ProcessorPriority"/> 升序跑（同优先级按类型名），
        /// 顺序由注册表保证确定——一个处理器注入的特性可能被后一个读到。
        /// </para>
        /// <para>
        /// <b>每个成员先跑「自身」再跑「父级注入」。</b> 这个顺序是契约：注入的特性不该影响
        /// 「这个成员自己有什么」的判断；而反过来，后跑的注入能被已经跑过的处理器看到，
        /// 那正是类级分组分发所需要的。
        /// </para>
        /// </remarks>
        private static void RunProcessors(InspectorProperty root, List<InspectorProperty> members)
        {
            var processors = AttributeProcessorRegistry.Processors;
            if (processors.Length == 0)
            {
                return;
            }

            for (var i = 0; i < processors.Length; i++)
            {
                if (processors[i].CanProcessSelfAttributes(root))
                {
                    processors[i].ProcessSelfAttributes(root, root.Attributes.Raw);
                }
            }

            for (var m = 0; m < members.Count; m++)
            {
                var member = members[m];

                for (var i = 0; i < processors.Length; i++)
                {
                    if (processors[i].CanProcessSelfAttributes(member))
                    {
                        processors[i].ProcessSelfAttributes(member, member.Attributes.Raw);
                    }
                }

                // 没有反射信息的成员不参与「父级注入」钩子。
                //
                // 这类成员是 Unity 注入的序列化属性（典型的是 m_Script）——它们**在
                // SerializedObject 里存在，却没有对应的托管字段**。让它们触发钩子有两个后果：
                // 处理器拿到 null 的 MemberInfo 后极容易 NRE（想读成员特性就会踩），
                // 以及「对每个成员各触发一次」这条契约会多算一条。守卫放在这里而不是
                // 各处理器里：这是唯一知道「哪些成员是真的成员」的地方。
                if (member.Member == null)
                {
                    continue;
                }

                for (var i = 0; i < processors.Length; i++)
                {
                    if (processors[i].CanProcessChildMemberAttributes(root, member.Member))
                    {
                        processors[i].ProcessChildMemberAttributes(root, member.Member, member.Attributes.Raw);
                    }
                }
            }
        }

        #endregion

        #region 分组装配

        /// <summary>
        /// 把成员搬进各自的分组节点，未声明分组的成员留在根下。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="members">按序列化顺序排列的成员节点。</param>
        /// <remarks>
        /// 分组节点**落在其首个成员出现的位置**：某个分组第一次被提及时就地插入父节点的
        /// 子列表。于是夹在分组字段之间的未分组字段会留在原地，而不是被挤到 Inspector 末尾
        /// ——后者一眼就能看出不对，却是「先摆所有分组、再摆散字段」那种朴素实现的必然结果。
        /// </remarks>
        private static void ApplyGrouping(InspectorProperty root, List<InspectorProperty> members)
        {
            var groups = new Dictionary<string, InspectorProperty>(StringComparer.Ordinal);

            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                var groupAttribute = FindDeepestGroupAttribute(member);

                if (groupAttribute == null)
                {
                    root.AddChild(member);
                    continue;
                }

                var groupNode = EnsureGroupChain(root, groups, groupAttribute.GroupID, groupAttribute);
                groupNode.AddChild(member);
            }

            SortGroupNodesAtLevel(root);
        }

        /// <summary>
        /// 取成员身上最深的那个分组特性。
        /// </summary>
        /// <param name="member">成员节点。</param>
        /// <returns>最深的 <see cref="PropertyGroupAttribute"/>；没有则返回 <c>null</c>。</returns>
        /// <remarks>
        /// 取最深而非第一个：<c>[BoxGroup("A")] [BoxGroup("A/B")]</c> 同时出现时，
        /// 成员应落在 <c>A/B</c> 里——<c>A/B</c> 本身已经蕴含了 <c>A</c>，
        /// 再让它当 A 的直接子节点就自相矛盾了。
        /// </remarks>
        private static PropertyGroupAttribute FindDeepestGroupAttribute(InspectorProperty member)
        {
            PropertyGroupAttribute deepest = null;
            var deepestDepth = -1;
            var attributes = member.Attributes;

            for (var i = 0; i < attributes.Count; i++)
            {
                if (!(attributes[i] is PropertyGroupAttribute group))
                {
                    continue;
                }

                var depth = DepthOf(group.GroupID);
                if (depth > deepestDepth)
                {
                    deepest = group;
                    deepestDepth = depth;
                }
            }

            return deepest;
        }

        /// <summary>
        /// 确保从根到指定路径的整条分组链都存在，返回最深的那一节。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="groups">已建分组节点的查找表。</param>
        /// <param name="path">目标路径，已规范化。</param>
        /// <param name="source">用于合成祖先节点的来源特性。</param>
        /// <returns>路径末段对应的分组节点。</returns>
        /// <remarks>
        /// 祖先节点由 <see cref="PropertyGroupAttribute.CloneForPath"/> 从来源特性复制而来，
        /// 因此 <c>ShowLabel</c> 这类子类字段会一并带到祖先上——语义是「祖先继承后代的呈现设定」，
        /// 比凭空造一个全默认的祖先更符合直觉。
        /// </remarks>
        private static InspectorProperty EnsureGroupChain(
            InspectorProperty root,
            Dictionary<string, InspectorProperty> groups,
            string path,
            PropertyGroupAttribute source)
        {
            var parent = root;
            string current = null;

            var segments = path.Split(PropertyGroupPath.Separator);
            for (var i = 0; i < segments.Length; i++)
            {
                current = current == null ? segments[i] : current + PropertyGroupPath.Separator + segments[i];

                if (groups.TryGetValue(current, out var node))
                {
                    // 同一个分组的又一次声明：按 Combine 规则并入既有特性，而不是丢弃。
                    // 不合并的话，「哪个字段先声明」就会悄悄决定分组的标题与排序。
                    var existing = node.Attributes.Get<PropertyGroupAttribute>();
                    if (existing != null)
                    {
                        existing.Combine(source.CloneForPath(current));
                    }
                }
                else
                {
                    var attribute = source.CloneForPath(current);
                    node = new InspectorProperty(
                        attribute.GroupName,
                        current,
                        null,
                        InspectorPropertyKind.Group,
                        new PropertyAttributes(new List<Attribute> { attribute }));

                    parent.AddChild(node);
                    groups[current] = node;
                    AttachChain(node, ChildrenTerminal);
                }

                parent = node;
            }

            return parent;
        }

        /// <summary>
        /// 同层的分组节点之间按 <see cref="PropertyGroupAttribute.Order"/> 重排，并递归到各分组内部。
        /// </summary>
        /// <param name="parent">要处理的父节点。</param>
        /// <remarks>
        /// <b>只重排分组节点彼此之间的先后，不动未分组成员的位置。</b>
        /// 做法是把分组占据的那些下标收集起来，把其中的分组按 Order 排好后再放回同一批下标。
        /// 这样「分组之间谁先谁后」由 Order 决定，而「分组与散字段的相对位置」仍由声明位置决定
        /// ——两个问题各自有单一答案，不必去调和「Order 与声明位置谁优先」这类无解的冲突。
        /// </remarks>
        private static void SortGroupNodesAtLevel(InspectorProperty parent)
        {
            var children = parent.RawChildren;
            var slots = new List<int>();

            for (var i = 0; i < children.Count; i++)
            {
                if (children[i].Kind == InspectorPropertyKind.Group)
                {
                    slots.Add(i);
                }
            }

            if (slots.Count > 1)
            {
                var ordered = new List<InspectorProperty>(slots.Count);
                for (var i = 0; i < slots.Count; i++)
                {
                    ordered.Add(children[slots[i]]);
                }

                // 插入排序：稳定，且同层分组数极少。稳定性在这里是必需而非偏好——
                // Order 相同的分组必须保持声明先后，否则每次编译的呈现都可能不同。
                for (var i = 1; i < ordered.Count; i++)
                {
                    var current = ordered[i];
                    var j = i - 1;

                    while (j >= 0 && OrderOf(ordered[j]) > OrderOf(current))
                    {
                        ordered[j + 1] = ordered[j];
                        j--;
                    }

                    ordered[j + 1] = current;
                }

                for (var i = 0; i < slots.Count; i++)
                {
                    children[slots[i]] = ordered[i];
                }
            }

            for (var i = 0; i < children.Count; i++)
            {
                if (children[i].Kind == InspectorPropertyKind.Group)
                {
                    SortGroupNodesAtLevel(children[i]);
                }
            }
        }

        /// <summary>取分组节点的排序权重。</summary>
        /// <param name="group">分组节点。</param>
        /// <returns>权重；节点上没有分组特性时返回 0。</returns>
        private static float OrderOf(InspectorProperty group)
        {
            var attribute = group.Attributes.Get<PropertyGroupAttribute>();
            return attribute != null ? attribute.Order : 0f;
        }

        /// <summary>数路径的层数。</summary>
        /// <param name="path">已规范化的路径。</param>
        /// <returns>层数，顶层为 1。</returns>
        /// <remarks>
        /// 按段数而非字符串长度比较深浅——<c>"A/B"</c> 比 <c>"LongName"</c> 短但更深。
        /// </remarks>
        private static int DepthOf(string path)
        {
            var depth = 1;
            for (var i = 0; i < path.Length; i++)
            {
                if (path[i] == PropertyGroupPath.Separator)
                {
                    depth++;
                }
            }

            return depth;
        }

        #endregion

        #region 辅助

        /// <summary>
        /// 装配并挂上绘制器链。
        /// </summary>
        /// <param name="property">目标节点。</param>
        /// <param name="terminal">该节点的末端绘制器。</param>
        private static void AttachChain(InspectorProperty property, XInspectorDrawer terminal)
        {
            property.Chain = DrawerChainBuilder.Build(property, terminal);
        }

        /// <summary>
        /// 沿继承链查找字段。
        /// </summary>
        /// <param name="type">起始类型。</param>
        /// <param name="name">字段名。</param>
        /// <returns>找到的字段；未找到返回 <c>null</c>。</returns>
        /// <remarks>
        /// 必须逐层 <c>DeclaredOnly</c> 上溯：<see cref="Type.GetField(string, BindingFlags)"/>
        /// 不返回基类的私有字段，而 <c>[SerializeField]</c> 的私有字段恰恰都在各自类里声明。
        /// 找不到不是错误——<c>m_Script</c> 这类由 Unity 注入的属性就没有对应的托管字段，
        /// 此时节点仍需存在（否则渲染结果会与原生 Inspector 不一致），只是拿不到特性。
        /// </remarks>
        private static FieldInfo FindField(Type type, string name)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, flags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        /// <summary>
        /// 收集成员字段上的特性。
        /// </summary>
        /// <param name="field">成员字段，可为 <c>null</c>。</param>
        /// <returns>特性列表；无字段时为空列表。</returns>
        /// <remarks>
        /// 反射每次调用都返回**新实例**，故这里天然满足「每个属性一份独立特性」——
        /// 不会出现多个属性共享同一个特性实例、改一个串一片的问题。
        /// 类级特性的分发才需要显式克隆。
        /// </remarks>
        private static List<Attribute> CollectMemberAttributes(FieldInfo field)
        {
            var result = new List<Attribute>();
            if (field == null)
            {
                return result;
            }

            foreach (var attribute in field.GetCustomAttributes(true))
            {
                if (attribute is Attribute typed)
                {
                    result.Add(typed);
                }
            }

            return result;
        }

        /// <summary>
        /// 收集类型上的特性。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <returns>特性列表。</returns>
        private static List<Attribute> CollectTypeAttributes(Type type)
        {
            var result = new List<Attribute>();

            foreach (var attribute in type.GetCustomAttributes(true))
            {
                if (attribute is Attribute typed)
                {
                    result.Add(typed);
                }
            }

            return result;
        }

        #endregion
    }
}
