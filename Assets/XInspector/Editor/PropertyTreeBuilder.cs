using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
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
        private static readonly CompositeMemberTerminalDrawer CompositeMemberTerminal =
            new CompositeMemberTerminalDrawer();
        private static readonly MethodTerminalDrawer MethodTerminal = new MethodTerminalDrawer();

        /// <summary>展开过的多态引用容器：原生那一行照画，子节点走本包的树。</summary>
        private static readonly ManagedReferenceTerminalDrawer ManagedReferenceTerminal =
            new ManagedReferenceTerminalDrawer();
        private static readonly ReflectedMemberTerminalDrawer ReflectedTerminal =
            new ReflectedMemberTerminalDrawer();

        /// <summary>方法节点路径的后缀。C# 标识符不含圆括号，故它与任何字段路径都不可能相撞。</summary>
        private const string MethodPathSuffix = "()";

        /// <summary>按元数据令牌比较两个方法，用于把声明顺序稳定下来。</summary>
        /// <remarks>
        /// <c>GetMethods</c> 的返回顺序在 .NET 上**不保证**是声明顺序，不排的话按钮的先后
        /// 会随编译变化，测试也会莫名其妙地 flaky。存成静态字段是为了不在每次建树时新建比较器。
        /// </remarks>
        private static readonly Comparison<MethodInfo> CompareByMetadataToken =
            (left, right) => left.MetadataToken.CompareTo(right.MetadataToken);

        /// <summary>按元数据令牌比较两个字段。</summary>
        /// <remarks>
        /// <b>字段与属性必须各排各的。</b> 两者分属元数据的不同表（字段 0x04、属性 0x17），
        /// 表内编号、跨表不可比——这与「方法插不回字段之间」是同一条实测结论。
        /// 故反射成员的分组顺序是「同一层里先字段后属性」，而不是把两者混起来排。
        /// </remarks>
        private static readonly Comparison<FieldInfo> CompareFieldByMetadataToken =
            (left, right) => left.MetadataToken.CompareTo(right.MetadataToken);

        /// <summary>按元数据令牌比较两个属性。</summary>
        private static readonly Comparison<PropertyInfo> ComparePropertyByMetadataToken =
            (left, right) => left.MetadataToken.CompareTo(right.MetadataToken);

        #endregion

        #region Public API

        /// <summary>
        /// 构建属性树。
        /// </summary>
        /// <param name="serializedObject">
        /// 目标序列化对象；**反射树没有它**（目标是 POCO 之类），那时传 <c>null</c>。
        /// </param>
        /// <param name="targets">目标对象列表，至少一个。</param>
        /// <param name="memberFilter">
        /// 字段过滤器；返回 <c>false</c> 的成员不建节点。传 <c>null</c> 等价于全收。
        /// </param>
        /// <returns>构建好的树。</returns>
        /// <remarks>
        /// <paramref name="serializedObject"/> 为 <c>null</c> 时序列化通道整条不跑：
        /// 没有 <see cref="SerializedObject"/> 就没有「Unity 会序列化什么」这回事，
        /// 剩下的只有 <c>[ShowInInspector]</c> 那条反射通道与方法节点。
        /// </remarks>
        public static PropertyTree Build(
            SerializedObject serializedObject,
            object[] targets,
            Func<FieldInfo, bool> memberFilter)
        {
            var targetType = targets != null && targets.Length > 0 && targets[0] != null
                ? targets[0].GetType()
                : typeof(object);

            var root = new InspectorProperty(
                targetType.Name,
                string.Empty,
                targetType,
                InspectorPropertyKind.Root,
                new PropertyAttributes(CollectTypeAttributes(targetType)));

            // 类级 [HideMonoScript]：脚本槽位直接不建节点——它是「Inspector 路径刻意保留
            // m_Script 以与原生一致」那条默认行为的显式退出，两个行为各有用途。
            // 反射树没有序列化通道，这条自然用不上。
            var hideMonoScript = root.Attributes.Has<HideMonoScriptAttribute>();
            var members = serializedObject != null
                ? CollectMembers(serializedObject, targetType, memberFilter, hideMonoScript)
                : new List<InspectorProperty>();

            // 反射成员接在序列化成员之后、方法节点之前：它们是「字段性质」的东西，
            // 紧跟着字段比夹在按钮之间合理。
            //
            // **为什么不与序列化成员按声明顺序交错**：做不到。序列化成员的名字与顺序来自
            // SerializedObject，反射成员来自元数据表，两者之间没有共同的可比次序——
            // 字段在 0x04 表、属性在 0x17 表，而序列化顺序本身就是 Unity 说了算的。
            // 与其猜一个，不如定一条确定的规矩。
            members.AddRange(CollectReflectedMembers(targets, targetType, members));

            // 方法节点一律接在字段之后。**不是没试过按声明顺序交错**——实测拿不到那个信息：
            // 字段令牌与方法令牌分属元数据的两张表（0x04 与 0x06）各自编号，跨表没有可比性；
            // GetMembers 也只按种类分组返回。三条测量都有用例钉着（MethodNodeTests），
            // 哪天 Unity 换了行为，那几条会先红。
            members.AddRange(CollectMethodMembers(targetType));

            // [PropertyOrder]：三段都收完之后、**分组装配之前**做一次稳定排序。
            // 位置由 OdinGap 记着的落点决定（「成员收集之后、分组装配之前」）——
            // 装配是唯一消费这份顺序的地方：它决定根下散字段的次序、组内成员的次序，
            // 以及分组节点落在哪里，所以排序必须在它之前完成。
            SortMembersByPropertyOrder(members);

            // ---- 顺序是契约，动之前先读完这段 ----
            //
            // 树必须在**处理器之前**构造好：需要目标对象的处理器（按钮族按名解析方法、
            // 条件族定位序列化对象）只能经 node.Owner 拿到树。
            var tree = new PropertyTree(serializedObject, targets, root);

            // 成员此刻还不在树上（要等分组装配才挂上去），故构造期那次递归回填够不着它们。
            // 这一步补上，**且要递归**——嵌套子节点是收集期挂到父节点上的，那时父节点的
            // Owner 还是 null，AddChild 传播不过去；漏了的话嵌套层的条件与解析器全部落空。
            for (var i = 0; i < members.Count; i++)
            {
                PropertyTree.AssignOwner(members[i], tree);
            }

            // 生命周期钩子不产生节点（它们不在某个位置上画东西），故走独立通道收集。
            tree.Lifecycle = TreeLifecycle.Collect(targetType, tree.Targets);

            // 处理器的**第一趟**必须在**分组装配之前**跑：类级分组特性是处理器注入到成员上的，
            // 而分组装配必须看到它——顺序反过来，类级 [BoxGroup] 会静默地不生效。
            //
            // 第一趟也必须在**挂链之前**跑：注入的特性会改变链条的构成
            // （例如类级 [Title] 被分发到成员身上，那个成员就该多一格标题绘制器）。
            //
            // 处理**分组特性**的处理器不在第一趟里（见 AttributeProcessorRegistry.FirstPassProcessors），
            // 它们走下面的第二趟。
            RunProcessors(root, members);

            // 元素层必须在**第一趟处理器之后**：判据要看处理器注入的 [ListDrawerSettings]
            // （TableList / Searchable / OnCollectionChanged 三个处理器都会补一份），
            // 读注入之前的事实会漏掉那三种接管方式。
            //
            // 位置也在**挂链之前**（新子树的链由 AttachChainRecursive 的递归一并挂上）、
            // **分组装配之前**（元素内部那一层由 ApplyNestedGrouping 的递归一并装）。
            // 见 CollectionElementExpansion 的取舍。
            ExpandCollectionElements(tree, members);

            // 展开过的多态容器登记进对账名单。位置两处都要：在**第一趟处理器之后**
            // （判据要看注入的事实，与元素层同款）、在**挂链之前**（末端按状态选，
            // 见 TerminalFor——状态必须先于挂链就位）。
            for (var i = 0; i < members.Count; i++)
            {
                RegisterPolymorphicLayersIn(tree, members[i]);
            }

            AttachChain(root, ChildrenTerminal);
            for (var i = 0; i < members.Count; i++)
            {
                // 末端按种类选：值从哪来决定了由谁收尾。接错了只会画出一句
                // 「没有 XX 后端」——那句话本身没错，但答非所问。
                // **递归**：嵌套子节点不在 members 列表里，漏了它们就没链
                // （症状是绘制时直接抛「链为空」）。
                AttachChainRecursive(members[i]);
            }

            ApplyGrouping(root, members);

            // 处理器的**第二趟**：分组装配之后跑，只对分组节点、只跑「处理的特性派生自
            // PropertyGroupAttribute」的那些。分组节点到第一趟时还不存在（它们由
            // ApplyGrouping 创建），而分组特性只可能出现在分组节点上——两条合起来使
            // 「判据挂在分组节点上」这类需求第一次有了构建期的落点，且判据不需要任何开关。
            //
            // 本趟只有**自身**钩子：往成员身上注入分组特性在装配之后已经太晚，装配看不见它，
            // 症状是「特性像没写一样」。本趟也只许改 PropertyState——分组与链都已冻结，
            // 增删特性不会反映到它们上面。
            RunGroupProcessors(root);

            // [OnInspectorInit] 在**整棵树建好之后**才跑：它多半要读字段、甚至读别的节点的状态，
            // 提前到构造点等于让它在半成品上工作。
            TreeLifecycle.InvokeAll(tree.Lifecycle?.Init, tree.Targets, "OnInspectorInit");

            return tree;
        }

        #endregion

        #region 成员收集

        /// <summary>
        /// 按 Unity 的序列化顺序平铺出全部成员节点。
        /// </summary>
        /// <param name="serializedObject">目标序列化对象。</param>
        /// <param name="targetType">目标对象的运行时类型。</param>
        /// <param name="memberFilter">成员过滤器；<c>null</c> 表示全收。</param>
        /// <param name="hideMonoScript">是否因类级 <c>[HideMonoScript]</c> 跳过脚本槽位。</param>
        /// <returns>成员节点列表，尚未挂到任何父节点上。</returns>
        private static List<InspectorProperty> CollectMembers(
            SerializedObject serializedObject,
            Type targetType,
            Func<FieldInfo, bool> memberFilter,
            bool hideMonoScript)
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

                    // [HideMonoScript]：脚本槽位由 Unity 注入（没有 MemberInfo），路径固定是
                    // "m_Script"。同样「不建节点」而不是「建了再删」，与 memberFilter 的处置一致。
                    if (hideMonoScript && field == null &&
                        string.Equals(iterator.propertyPath, "m_Script", StringComparison.Ordinal))
                    {
                        continue;
                    }

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
            var stableProperty = serializedObject.FindProperty(path);
            var valueType = ResolveMemberType(field, stableProperty);

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

            // 嵌套复合成员按需展开（见 NestedMemberExpansion 的取舍）。
            if (NestedMemberExpansion.ShouldExpand(node, stableProperty))
            {
                ExpandChildren(serializedObject, node);
            }

            // 链条**不在这里挂**：处理器还没跑，特性尚未最终确定。
            return node;
        }

        /// <summary>
        /// 把复合成员的直接子级展开成真节点（递归下去）。
        /// </summary>
        /// <param name="serializedObject">底层序列化对象。</param>
        /// <param name="parent">复合成员节点。</param>
        /// <remarks>
        /// <para>
        /// 子节点的值入口一律用 <see cref="SerializedObject.FindProperty"/> 按各自路径取
        /// **独立**实例——迭代器是共享的（老坑：存进节点会让所有节点指向最后一个属性）。
        /// </para>
        /// <para>
        /// <b>子节点不过 <c>memberFilter</c>。</b> 那个过滤器的存在理由只是排除
        /// EditorWindow 自己的那几个内部字段（判据是 <c>field == null</c> 即拒、
        /// 声明类型必须可赋给窗口基类），套到嵌套层会把整层**静默滤掉**。
        /// </para>
        /// </remarks>
        /// <param name="serializedObject">底层序列化对象。</param>
        /// <param name="parent">复合父节点。</param>
        /// <remarks>
        /// 反射成员与方法节点**一律收**（嵌套层与元素层同）：元素路径
        /// （<c>items.Array.data[i]</c>）自 2026-10-06 起被
        /// <see cref="ReflectedAccessor.TryCreatePath"/> 认，取实例的那条链照样成立。
        /// </remarks>
        /// <summary>
        /// 成员节点该用哪个类型：多态字段取**具体类型**，其余取声明类型。
        /// </summary>
        /// <param name="field">成员的字段；Unity 注入的成员为 <c>null</c>。</param>
        /// <param name="property">该成员的序列化属性（独立实例）。</param>
        /// <returns>节点类型。</returns>
        /// <remarks>
        /// <para>
        /// <b>这是本包第一次按运行时类型解析。</b> 在此之前，节点类型一律是声明类型
        /// （字段的 <c>FieldType</c>）；多态引用的声明类型常常是接口或抽象类，按它解析的后果是
        /// 子成员拿不到 <see cref="FieldInfo"/>（<c>ResolveField(parent.Type, …)</c> 返回 null），
        /// 于是**特性、分组、告警一起静默消失**——而不是报错。
        /// </para>
        /// <para>
        /// 解析不出来（空引用、多选混合态）就回落声明类型：那种情况下本包**不展开**，
        /// 类型只用来显示与兜底，退回声明类型与从前的行为逐字一致。
        /// </para>
        /// </remarks>
        private static Type ResolveMemberType(FieldInfo field, SerializedProperty property)
        {
            if (field != null && NestedMemberExpansion.IsPolymorphicReference(field))
            {
                var concrete = PolymorphicReference.ResolveConcreteType(property);
                if (concrete != null)
                {
                    return concrete;
                }
            }

            return field != null ? field.FieldType : typeof(object);
        }

        private static void ExpandChildren(SerializedObject serializedObject, InspectorProperty parent)
        {
            var property = parent.ValueEntry?.SerializedProperty;
            if (property == null)
            {
                return;
            }

            var child = property.Copy();
            var depth = property.depth + 1;
            var next = child.NextVisible(true) && child.depth == depth;

            while (next)
            {
                var path = child.propertyPath;
                var field = NestedMemberExpansion.ResolveField(parent.Type, child.name);
                var stableProperty = serializedObject.FindProperty(path);
                var valueType = ResolveMemberType(field, stableProperty);

                var node = new InspectorProperty(
                    child.name,
                    path,
                    valueType,
                    InspectorPropertyKind.Member,
                    new PropertyAttributes(CollectMemberAttributes(field)))
                {
                    ValueEntry = new SerializedPropertyValueEntry(stableProperty, valueType),
                    Member = field,
                };

                parent.AddChild(node);

                if (node.Member != null && NestedMemberExpansion.ShouldExpand(node, stableProperty))
                {
                    ExpandChildren(serializedObject, node);
                }

                next = child.NextVisible(false) && child.depth == depth;
            }

            // 嵌套层与元素层里的 [ShowInInspector]：用**同一个实例**当取值对象，路径带父前缀
            // （元素层是 `items.Array.data[0].`，同一套机制）。方法节点同理。两段都排在序列化
            // 子节点之后——与顶层的三段顺序（序列化 → 反射 → 方法）一致；也都排在下面的排序
            // 之前，嵌套层/元素层的 [PropertyOrder] 因此同样能排到按钮。
            AppendNestedReflectedMembers(serializedObject, parent);
            AppendNestedMethodMembers(parent);

            // [PropertyOrder] 在嵌套层同样生效——每个复合父节点各排一次自己那一层。
            // 顶层那一次仍在 Build 里（两处都只对一层成员调同一个稳定排序）。
            SortMembersByPropertyOrder(parent.RawChildren);
        }

        #endregion

        #region 元素层

        /// <summary>
        /// 给「用到本包 + 已被接管」的集合建元素层；被挡住但用到了本包的，各告警一次。
        /// </summary>
        /// <param name="tree">属性树（登记对账名单用）。</param>
        /// <param name="members">顶层成员（此刻还没挂到根上，与处理器那一趟同款）。</param>
        private static void ExpandCollectionElements(PropertyTree tree, List<InspectorProperty> members)
        {
            for (var m = 0; m < members.Count; m++)
            {
                ExpandCollectionElementsIn(tree, members[m]);
            }
        }

        /// <summary>
        /// 深度优先地走一层子树：集合节点按判据建层，其余节点继续下探。
        /// </summary>
        /// <param name="tree">属性树。</param>
        /// <param name="node">当前节点。</param>
        /// <remarks>
        /// 递归发生在**建层之后**：元素节点是刚挂上来的，深度判据（元素里的集合不节点化）
        /// 要走到它们才报得出「Nested」那条边界。
        /// </remarks>
        private static void ExpandCollectionElementsIn(PropertyTree tree, InspectorProperty node)
        {
            if (node.Kind == InspectorPropertyKind.Member && node.ValueEntry?.SerializedProperty != null)
            {
                var decision = CollectionElementExpansion.Decide(node);

                if (decision == CollectionElementExpansion.ElementLayerDecision.Build)
                {
                    CreateElementLayer(tree.SerializedObject, tree, node);
                }
                else if (decision != CollectionElementExpansion.ElementLayerDecision.Inert)
                {
                    CollectionElementExpansion.WarnBlocked(node, decision);
                }
            }

            var children = node.RawChildren;
            for (var i = 0; i < children.Count; i++)
            {
                ExpandCollectionElementsIn(tree, children[i]);
            }
        }

        /// <summary>
        /// 建一个集合的元素层：逐元素建节点、递归展开，并登记进树的对账名单。
        /// </summary>
        /// <param name="serializedObject">底层序列化对象。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="collection">集合节点。</param>
        private static void CreateElementLayer(
            SerializedObject serializedObject, PropertyTree tree, InspectorProperty collection)
        {
            var layer = collection.State.GetOrCreate<CollectionElementLayerState>();
            layer.Nodes.Clear();
            layer.Dirty = false;

            var count = collection.ValueEntry.SerializedProperty.arraySize;
            for (var i = 0; i < count; i++)
            {
                layer.Nodes.Add(CreateElementNode(serializedObject, collection, i));
            }

            // 第一趟处理器——**只对新子树**（元素节点是刚挂上来的，顶层那一趟跑在它们出生之前）。
            // 与构建顺序契约同款：在挂链之前（注入的特性会改变链的构成）、在分组装配之前
            // （注入的分组特性要被装配看见）。父 / 根钩子不重跑——它们对集合节点自己已经跑过。
            RunNestedProcessors(AttributeProcessorRegistry.FirstPassProcessors, collection);

            // 登记进对账名单：此后每趟绘制之前 CollectionElementSync 都会看它一眼。
            tree.AddElementCollection(collection);
        }

        /// <summary>
        /// 建一个元素节点：路径 <c>items.Array.data[i]</c>、种类仍是 <c>Member</c>。
        /// </summary>
        /// <param name="serializedObject">底层序列化对象。</param>
        /// <param name="collection">集合节点。</param>
        /// <param name="index">元素下标。</param>
        /// <returns>建好的元素节点。</returns>
        /// <remarks>
        /// <para>
        /// <b>种类仍是 <see cref="InspectorPropertyKind.Member"/>。</b> 它的路径对
        /// <c>SerializedObject.FindProperty</c> 合法（<c>Kind.Member</c> 的不变量照旧成立），
        /// 且 <c>SerializedMemberResolver.FindNestedScopeNode</c> 靠这条认容器——
        /// 单列一个新 Kind 会让元素成员里的条件**静默看错对象**。它是 <c>Member == null</c>
        /// 的成员：元素上标不了任何特性，也就没有「成员自己的特性」可读。
        /// </para>
        /// <para>
        /// <b>值是独立实例。</b> 按路径重新 <c>FindProperty</c> 取一份（与
        /// <see cref="CreateMember"/> 同款）——<c>GetArrayElementAtIndex</c> 的句柄
        /// 会随结构变更作废，路径不会。
        /// </para>
        /// <para>
        /// <b>元素子树里也收反射成员与方法节点</b>（自 2026-10-06）：取值/调用的实例由
        /// <c>ReflectedAccessor.TryCreatePath</c> 按 <c>items.Array.data[i]</c> 这条路径
        /// 每帧现读——索引段是它认得的路径转义，取不到（越界、空集合、元素为 null）时
        /// 消费者落到既有的「取不到实例」语义。
        /// </para>
        /// </remarks>
        private static InspectorProperty CreateElementNode(
            SerializedObject serializedObject, InspectorProperty collection, int index)
        {
            var element = collection.ValueEntry.SerializedProperty.GetArrayElementAtIndex(index);
            var path = element.propertyPath;
            var elementType = CollectionElementExpansion.ElementTypeOf(collection) ?? typeof(object);
            var stableProperty = serializedObject.FindProperty(path) ?? element;

            var node = new InspectorProperty(
                element.displayName,
                path,
                elementType,
                InspectorPropertyKind.Member,
                new PropertyAttributes(new List<Attribute>()))
            {
                ValueEntry = new SerializedPropertyValueEntry(stableProperty, elementType),
                Member = null,
            };

            collection.AddChild(node);

            ExpandChildren(serializedObject, node);

            return node;
        }

        /// <summary>
        /// 重建一个集合的元素层——对账发现长度对不上（或结构刚被改过）时走这里。
        /// </summary>
        /// <param name="collection">集合节点。</param>
        /// <remarks>
        /// <para>
        /// <b>流水线与构建期逐条对应</b>（顺序是契约）：建节点 → 第一趟处理器 → 挂链 →
        /// 分组装配 → 第二趟分组处理器。与构建期的差别只有一处：第一趟**只对新子树**
        /// 跑（<c>RunNestedProcessors</c> 的既有语义），根与集合节点自己的钩子不重跑——
        /// 它们的特性没变，重跑等于把「注入只发生一次」的契约破掉。
        /// </para>
        /// <para>
        /// <b>元素层可以递归（深度 &gt; 1），所以这里比构建期多跑两件事</b>：释放旧子树
        /// **之前**先注销它里面登记过的内层集合（之后状态袋已清、认不出），以及在第一趟
        /// 处理器**之后**对**新**子树重走一遍元素层展开（否则外层改一次长度，内层元素层
        /// 就静默消失）。
        /// </para>
        /// <para>
        /// <b>旧子树整体释放。</b> 状态袋里的 <c>IDisposable</c>（内嵌编辑器之类）不释放
        /// 就是重建一次泄漏一次；旧节点对象同时作废——不得跨同步点持有（见
        /// <see cref="CollectionElementExpansion"/> 的有效窗口）。
        /// </para>
        /// <para>
        /// <b>这是一次性动作，不是每帧动作。</b> 反射与处理器都发生在这里，而这里只在
        /// 长度对不上时被调到——「反射仅限构建期」这条规则按此豁免（见
        /// <see cref="CollectionElementSync"/>）。
        /// </para>
        /// </remarks>
        /// <summary>
        /// 把一个**已展开的**多态容器登记进对账名单（递归覆盖整棵子树）。
        /// </summary>
        /// <param name="tree">属性树。</param>
        /// <param name="node">子树根（含它自己）。</param>
        /// <remarks>
        /// 「展开过的多态容器」的判据是**有子节点**：判据不给它展开时 <c>ExpandChildren</c>
        /// 根本没被调过，子节点数是 0。于是这里不需要再问一遍那道闸（问两遍的迟早有一处先漂）。
        /// </remarks>
        private static void RegisterPolymorphicLayersIn(PropertyTree tree, InspectorProperty node)
        {
            if (node.Kind == InspectorPropertyKind.Member &&
                NestedMemberExpansion.IsPolymorphicReference(node.Member as FieldInfo) &&
                node.RawChildren.Count > 0)
            {
                var layer = node.State.GetOrCreate<PolymorphicLayerState>();
                layer.ConcreteType = node.Type;
                layer.Dirty = false;

                tree.AddPolymorphicContainer(node);
            }

            var children = node.RawChildren;
            for (var i = 0; i < children.Count; i++)
            {
                RegisterPolymorphicLayersIn(tree, children[i]);
            }
        }

        /// <summary>
        /// 重建一个多态容器的子树：**换了具体类型**之后被对账调到。
        /// </summary>
        /// <param name="container">展开过的多态成员节点。</param>
        /// <remarks>
        /// <para>
        /// 与 <see cref="RebuildElementLayer"/> 是同一套七步，只有第 2 步不同：那边是「按
        /// <c>arraySize</c> 重建元素节点」，这边是「**换掉容器自己的类型**再重新展开」——
        /// 故它多一处 <c>container.Type = …</c>（<see cref="InspectorProperty.Type"/> 唯一的
        /// 改写入孔，别处仍当它是稳定的）。
        /// </para>
        /// <para>
        /// 槽位被清空（或变成多选混合态）时 <c>concrete</c> 为 <c>null</c>：旧子树照常释放、
        /// 新的不建——用户看到的就是原生那一行，与从没展开过一样。
        /// </para>
        /// <para>
        /// <b>「反射仅限构建期」这条规则在此豁免</b>——与元素层重建同款（见
        /// <see cref="PolymorphicReferenceSync"/>）：换类型是绘制期才发现的，而解析一次类型
        /// 不贵、也不进每帧路径。
        /// </para>
        /// </remarks>
        internal static void RebuildPolymorphicLayer(InspectorProperty container)
        {
            var layer = container.State.Get<PolymorphicLayerState>();
            var tree = container.Owner;
            var serializedObject = container.Owner?.SerializedObject;

            if (layer == null || serializedObject == null || tree == null)
            {
                return;
            }

            var children = container.RawChildren;

            // 1. 旧子树整体释放。**注销走两张表一起的那个入口**：多态层里可以嵌元素层
            //    （成员里放列表），反过来也行——只注销自己那张表，另一张就会留下强引用
            //    作废子树的僵尸。注销必须在释放**之前**（DisposeNode 会把状态袋 Reset 清空）。
            for (var i = 0; i < children.Count; i++)
            {
                tree.UnregisterLayersIn(children[i]);
                PropertyTree.DisposeNode(children[i]);
            }

            children.Clear();

            // 2. 换类型，再按新类型重新展开。
            var property = container.ValueEntry?.SerializedProperty;
            var concrete = PolymorphicReference.ResolveConcreteType(property);

            layer.ConcreteType = concrete;
            layer.Dirty = false;

            // 类型无条件改写：槽位清空（或变成混合态）时它要**退回声明类型**，
            // 与构建期 `ResolveMemberType` 的回落逐字一致——否则清空之后容器还挂着旧的具体类型。
            container.Type = ResolveMemberType(container.Member as FieldInfo, property);

            if (concrete != null)
            {
                ExpandChildren(serializedObject, container);
            }

            // 2.5 新子树里的多态容器也要登记（换成的类型里还可以有别的多态引用）。
            //     必须在挂链之前——末端按状态选。
            for (var i = 0; i < children.Count; i++)
            {
                RegisterPolymorphicLayersIn(tree, children[i]);
            }

            // 3. 第一趟处理器：只跑新子树（父 / 根钩子不重跑）。
            RunNestedProcessors(AttributeProcessorRegistry.FirstPassProcessors, container);

            // 3.5 新子树里的集合按判据建层/告警/登记。
            for (var i = 0; i < children.Count; i++)
            {
                ExpandCollectionElementsIn(tree, children[i]);
            }

            // 4. 挂链（递归覆盖新子树）。
            for (var i = 0; i < children.Count; i++)
            {
                AttachChainRecursive(children[i]);
            }

            // 5. 分组装配。**从容器自己开始**——它的那一层正是要重新装配的那一层
            //    （多态段里的分组路径前缀 = 容器路径），递归会一并覆盖更深处。
            //    这里与元素层重建刻意不同：那边装配的是**每个元素内部**那一层，
            //    集合自己那一层没动过，故从子节点开始。
            ApplyNestedGrouping(container);

            // 6. 第二趟：只对分组节点、只跑处理分组特性的那些。
            RunGroupProcessors(container);
        }

        internal static void RebuildElementLayer(InspectorProperty collection)
        {
            var layer = collection.State.Get<CollectionElementLayerState>();
            var tree = collection.Owner;
            var serializedObject = collection.Owner?.SerializedObject;

            if (layer == null || serializedObject == null || tree == null)
            {
                return;
            }

            var children = collection.RawChildren;

            // 1. 旧子树整体释放（状态随节点走）。**注销必须紧挨在释放之前**：DisposeNode 会把
            //    状态袋 Reset 清空，之后再走这棵树就认不出谁带过元素层，条目会变成
            //    强引用作废子树的僵尸。外层集合自己不在被摘之列（我们从元素节点开始走）。
            for (var i = 0; i < children.Count; i++)
            {
                tree.UnregisterLayersIn(children[i]);
                PropertyTree.DisposeNode(children[i]);
            }

            children.Clear();
            layer.Nodes.Clear();

            // 2. 建新节点（按路径取独立实例）。
            var count = collection.ValueEntry?.SerializedProperty?.arraySize ?? 0;
            for (var i = 0; i < count; i++)
            {
                layer.Nodes.Add(CreateElementNode(serializedObject, collection, i));
            }

            // 2.5 新子树里的多态容器登记（元素类型里的 `[SerializeReference]` 字段）。
            for (var i = 0; i < children.Count; i++)
            {
                RegisterPolymorphicLayersIn(tree, children[i]);
            }

            // 3. 第一趟处理器：只跑新子树（父 / 根钩子不重跑）。
            RunNestedProcessors(AttributeProcessorRegistry.FirstPassProcessors, collection);

            // 3.5 重走元素层展开：新子树里的集合（元素类型里的 `List<T>`）按判据建层/告警/
            //     登记——与构建期逐条对应（构建期这一步在 PropertyTreeBuilder.Build 的
            //     ExpandCollectionElements 里）。落位：在容器项注入（第一趟处理器）**之后**、
            //     在挂链与分组装配**之前**（那两步的递归会一并覆盖新内层节点）。
            //     对元素节点自身的判定是无害冗余：CanDraw 对 Generic 必然 Inert（构建期
            //     本来就对它们判过一次）。
            for (var i = 0; i < children.Count; i++)
            {
                ExpandCollectionElementsIn(tree, children[i]);
            }

            // 4. 挂链（递归覆盖新子树；带 [InlineProperty] 的折叠抑制也在这一步定案）。
            for (var i = 0; i < children.Count; i++)
            {
                AttachChainRecursive(children[i]);
            }

            // 5. 分组装配：元素内部那一层（含更深处），分组路径前缀 = 元素路径。
            for (var i = 0; i < children.Count; i++)
            {
                ApplyNestedGrouping(children[i]);
            }

            SortGroupNodesAtLevel(collection);

            // 6. 第二趟：只对分组节点、只跑处理分组特性的那些（新装配出来的分组）。
            RunGroupProcessors(collection);

            layer.Dirty = false;
        }

        #endregion

        #region 反射成员收集

        /// <summary>
        /// 为带 <c>[ShowInInspector]</c> 的成员建节点——那些 Unity 不会序列化的成员。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>判重看的是「树里有没有它」，不是「Unity 会不会序列化它」。</b> 两者不等价，
        /// 而差别正是想要的：<c>[SerializeField] private int x</c> 在树里，于是
        /// <c>[ShowInInspector]</c> 不会让它出现第二遍；而 <c>[HideInInspector] public int x</c>
        /// **被 Unity 序列化却不在树里**，于是它会被收进来——这时它是唯一的那一份，不是重复的。
        /// </para>
        /// <para>
        /// <b>不需要「跳过 UnityEngine 框架成员」的规则。</b> 这里只收带
        /// <c>[ShowInInspector]</c> 的成员，框架自己的成员不可能带本包的标记，
        /// 于是 <c>EditorWindow</c> 那几十个属性一个都进不来——这是构造上就成立的，不靠名单。
        /// </para>
        /// <para>
        /// <b>排序与去重照抄方法节点那套两段式</b>：先按最派生到最基类逐层收集、按名字去重保最派生，
        /// 再整体翻转成基类在前。层内字段按令牌排完再排属性（两张表不可比）。
        /// </para>
        /// <para>
        /// 顶层与嵌套层共用这一段：<paramref name="scopes"/> 为 <c>null</c> 时取值对象就是目标自身、
        /// <paramref name="pathPrefix"/> 为空时路径就是成员名——**顶层逐字不变**。
        /// </para>
        /// </remarks>
        /// <param name="targets">目标对象数组。</param>
        /// <param name="targetType">在哪个类型上收（顶层是目标类型，嵌套层是复合字段的声明类型）。</param>
        /// <param name="serializedMembers">已经进树的兄弟节点，用来判重。</param>
        /// <param name="scopes">逐目标的嵌套实例来源；顶层为 <c>null</c>。</param>
        /// <param name="pathPrefix">路径前缀（以 <c>.</c> 结尾）；顶层为空。</param>
        /// <returns>成员节点列表，尚未挂到任何父节点上。</returns>
        private static List<InspectorProperty> CollectReflectedMembers(
            object[] targets,
            Type targetType,
            List<InspectorProperty> serializedMembers,
            ReflectedAccessor[] scopes = null,
            string pathPrefix = null)
        {
            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            // 判重用**名字**而不是路径：顶层两者相同，嵌套层只有名字才是「重复」的判据
            // （同层的兄弟节点共享父前缀）。
            var taken = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < serializedMembers.Count; i++)
            {
                taken.Add(serializedMembers[i].Name);
            }

            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            var perLevel = new List<List<InspectorProperty>>();

            for (var type = targetType; type != null; type = type.BaseType)
            {
                var level = new List<InspectorProperty>();

                var fields = type.GetFields(Flags);
                Array.Sort(fields, CompareFieldByMetadataToken);

                for (var i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    if (!CarriesShowInInspector(field) || taken.Contains(field.Name) ||
                        !seenNames.Add(field.Name))
                    {
                        continue;
                    }

                    var node = CreateReflectedMember(targets, field, scopes, pathPrefix);
                    if (node != null)
                    {
                        level.Add(node);
                    }
                }

                var properties = type.GetProperties(Flags);
                Array.Sort(properties, ComparePropertyByMetadataToken);

                for (var i = 0; i < properties.Length; i++)
                {
                    var property = properties[i];

                    // 索引器没有「一个目标对应一个值」的语义，取值访问器也编译不出来。
                    if (property.GetIndexParameters().Length > 0 ||
                        !CarriesShowInInspector(property) || taken.Contains(property.Name) ||
                        !seenNames.Add(property.Name))
                    {
                        continue;
                    }

                    var node = CreateReflectedMember(targets, property, scopes, pathPrefix);
                    if (node != null)
                    {
                        level.Add(node);
                    }
                }

                perLevel.Add(level);
            }

            var members = new List<InspectorProperty>();
            for (var i = perLevel.Count - 1; i >= 0; i--)
            {
                members.AddRange(perLevel[i]);
            }

            return members;
        }

        /// <summary>成员身上有没有 <c>[ShowInInspector]</c>。</summary>
        /// <param name="member">成员。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        private static bool CarriesShowInInspector(MemberInfo member)
        {
            return MemberNodeCriteria.CarriesShowInInspector(member);
        }

        /// <summary>
        /// 为一个反射成员建节点，并装好它的值入口。
        /// </summary>
        /// <param name="targets">目标对象数组。</param>
        /// <param name="member">字段或属性。</param>
        /// <param name="scopes">逐目标的嵌套实例来源；顶层为 <c>null</c>。</param>
        /// <param name="pathPrefix">路径前缀（以 <c>.</c> 结尾）；顶层为空。</param>
        /// <returns>建好的节点；取值访问器编译不出来时返回 <c>null</c>（已告警）。</returns>
        /// <remarks>
        /// <b>值入口在这里就装好，不等树构造。</b> 处理器在挂链之前跑，而条件族之外还有
        /// <c>[Toggle]</c> 一族要经 <c>ValueEntry</c> 找「另一个成员」——晚一步装，
        /// 拿到这些成员的那些处理器就会静默落空。
        /// </remarks>
        private static InspectorProperty CreateReflectedMember(
            object[] targets,
            MemberInfo member,
            ReflectedAccessor[] scopes = null,
            string pathPrefix = null)
        {
            var accessors = ResolveAccessors(targets, member, scopes, out var reason);
            if (accessors == null)
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{member.Name}」上的 [ShowInInspector] 无法生效：{reason}。该属性已跳过。");
                return null;
            }

            var valueType = member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;

            // 嵌套层的路径带父前缀（`stats.Total`）——它是**合成路径**，不是序列化路径，
            // 因此节点种类必须是 ReflectedMember（那条「Member 的路径可交给序列化系统」的
            // 不变量只在 Kind.Member 上成立；按路径重置也只收 Kind.Member）。
            var path = string.IsNullOrEmpty(pathPrefix) ? member.Name : pathPrefix + member.Name;

            return new InspectorProperty(
                member.Name,
                path,
                valueType,
                InspectorPropertyKind.ReflectedMember,
                new PropertyAttributes(CollectMemberAttributes(member)))
            {
                ValueEntry = new ReflectedValueEntry(targets, accessors, valueType, scopes),
                Member = member,
            };
        }

        /// <summary>
        /// 逐目标解析取值访问器。
        /// </summary>
        /// <param name="targets">目标对象数组。</param>
        /// <param name="primary">主目标上解析出的成员。</param>
        /// <param name="scopes">逐目标的嵌套实例来源；顶层为 <c>null</c>。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>逐目标的访问器；主目标上都编译不出来时返回 <c>null</c>。</returns>
        /// <remarks>
        /// 多选下的目标未必是同一个类型，故**逐个目标按名字重解析**，而不是共用主目标那个。
        /// 某个目标上解析不出来就是 <c>null</c>，读取时算「不一致」——我们确实不知道它的值。
        /// </remarks>
        private static ReflectedAccessor[] ResolveAccessors(
            object[] targets,
            MemberInfo primary,
            ReflectedAccessor[] scopes,
            out string reason)
        {
            if (!ReflectedAccessor.TryCreate(primary, out var primaryAccessor, out reason))
            {
                return null;
            }

            var accessors = new ReflectedAccessor[targets.Length];
            if (accessors.Length > 0)
            {
                accessors[0] = primaryAccessor;
            }

            for (var i = 1; i < targets.Length; i++)
            {
                // 顶层按**目标自身**的类型重解析；嵌套层按**实例**的类型
                // （那条字段链的末端类型，多目标可以各不相同）。
                var type = scopes == null
                    ? targets[i]?.GetType()
                    : (i < scopes.Length ? scopes[i]?.ValueType : null);

                if (type == null)
                {
                    continue;
                }

                var member = FindReflectedMember(type, primary.Name);
                if (member != null && ReflectedAccessor.TryCreate(member, out var accessor, out _))
                {
                    accessors[i] = accessor;
                }
            }

            return accessors;
        }

        /// <summary>按名字逐层上溯找一个字段或属性。</summary>
        /// <param name="type">起始类型。</param>
        /// <param name="name">成员名。</param>
        /// <returns>成员；找不到返回 <c>null</c>。</returns>
        private static MemberInfo FindReflectedMember(Type type, string name)
        {
            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            for (var current = type; current != null; current = current.BaseType)
            {
                // 用 GetMember 而不是 GetProperty：同名索引器会让 GetProperty 抛
                // AmbiguousMatchException，而这里要的恰恰是「挑一个能读的」。
                var candidates = current.GetMember(name, Flags);

                for (var i = 0; i < candidates.Length; i++)
                {
                    if (candidates[i] is FieldInfo field)
                    {
                        return field;
                    }

                    if (candidates[i] is PropertyInfo property && property.GetIndexParameters().Length == 0)
                    {
                        return property;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 为节点及其**全部后代**装链（末端按节点形状选）。
        /// </summary>
        /// <param name="node">起始节点。</param>
        /// <remarks>
        /// 嵌套子节点不在顶层 <c>members</c> 列表里（那列表只服务顶层装配），
        /// 故挂链必须自己递归下去——漏了它们的链，绘制时会直接抛。
        /// </remarks>
        private static void AttachChainRecursive(InspectorProperty node)
        {
            var terminal = TerminalFor(node);
            AttachChain(node, terminal);

            if (ReferenceEquals(terminal, CompositeMemberTerminal))
            {
                // 复合成员的状态在**构建期**定案（绘制期只读）：带 [InlineProperty] 的节点
                // 不画折叠头——标签与「内联」由那只绘制器说了算，末端只负责把子节点画出来。
                node.State.GetOrCreate<CompositeMemberState>().FoldoutSuppressed =
                    node.Attributes.Has<InlinePropertyAttribute>();
            }

            var children = node.RawChildren;
            for (var i = 0; i < children.Count; i++)
            {
                AttachChainRecursive(children[i]);
            }
        }

        /// <summary>按节点挑链条末端。</summary>
        /// <param name="node">节点。</param>
        /// <returns>该节点的末端绘制器。</returns>
        /// <remarks>
        /// 成员节点分两种：**展开过的复合成员**（有子节点）末端画「标签 + 折叠三角 + 子节点」，
        /// 其余照旧交给 <c>UnityFallbackDrawer</c>。链在挂链期装配，而子节点在收集期就位，
        /// 故此刻 <c>Children.Count</c> 可信。
        /// </remarks>
        private static XInspectorDrawer TerminalFor(InspectorProperty node)
        {
            switch (node.Kind)
            {
                case InspectorPropertyKind.Method:
                    return MethodTerminal;
                case InspectorPropertyKind.ReflectedMember:
                    return ReflectedTerminal;
                default:
                    // 元素层容器：链上的集合绘制器一旦放行（CanDraw 为假），整份要交回 Unity
                    // 的**原生数组画法**——元素节点是集合绘制器自己画的（见 ListDrawerSettingsDrawer），
                    // 不是给复合末端当折叠头逐个画的。接错末端的症状是「列表回退时每个元素
                    // 变成一行折叠头 + 缩进」。
                    if (node.State.Get<CollectionElementLayerState>() != null)
                    {
                        return MemberTerminal;
                    }

                    // 多态引用容器：末端必须**画原生那一行**，否则值（那份引用）就没人画了——
                    // 用户看不见也换不了具体类型。故它既不能用复合末端（只画折叠头），
                    // 也不能按子节点数选（重建到零子节点时终端会变，而链是冻结的）。
                    if (node.State.Get<PolymorphicLayerState>() != null)
                    {
                        return ManagedReferenceTerminal;
                    }

                    return node.Children.Count > 0 ? CompositeMemberTerminal : MemberTerminal;
            }
        }

        #endregion

        #region 方法节点收集

        /// <summary>
        /// 收集带 <c>[Button]</c> 一类的**方法**，为它们建方法节点。
        /// </summary>
        /// <param name="targetType">在哪个类型上收（顶层是目标类型，嵌套层是复合字段的声明类型）。</param>
        /// <param name="pathPrefix">路径前缀（以 <c>.</c> 结尾）；顶层为空。</param>
        /// <returns>方法节点列表，尚未挂到任何父节点上。</returns>
        /// <remarks>
        /// <para>
        /// <b>只认检视的那个对象自己（含继承链）上的方法。</b> 成员遍历走的是
        /// <c>SerializedObject</c>，而方法根本不在序列化系统里，所以这一段只能靠反射。
        /// 嵌套 <c>[Serializable]</c> 类里的方法同样收不到——拿到嵌套实例需要一条
        /// 本包还没有的「只读反射路径解析」，那时才能把方法挂到对应的嵌套节点上。
        /// </para>
        /// <para>
        /// <b>先按最派生到最基类的顺序走，再按方法名去重。</b> 覆写链上只保留最派生的一份：
        /// <c>[Button]</c> 是 <c>Inherited = false</c>，覆写方不带特性就不会被收进来，
        /// 而带了特性时两份都会命中，去重保最派生的那个才对。同名重载无法两全——
        /// 按钮名就是方法名，两个同名按钮谁也分不清谁，故保留第一个并告警。
        /// </para>
        /// </remarks>
        private static List<InspectorProperty> CollectMethodMembers(Type targetType, string pathPrefix = null)
        {
            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            var seenNames = new HashSet<string>(StringComparer.Ordinal);

            // 逐层收集的结果先各存一份：**去重要按最派生优先**，而**输出要按基类在前**
            // （与 Unity 排字段的顺序一致），两件事的顺序要求相反，故不能一趟做完。
            var perLevel = new List<List<InspectorProperty>>();

            for (var type = targetType; type != null; type = type.BaseType)
            {
                var level = new List<InspectorProperty>();
                var declared = type.GetMethods(Flags);

                Array.Sort(declared, CompareByMetadataToken);

                for (var i = 0; i < declared.Length; i++)
                {
                    var method = declared[i];
                    if (!CreatesMethodNode(method))
                    {
                        continue;
                    }

                    if (!seenNames.Add(method.Name))
                    {
                        Debug.LogWarning(
                            $"[XInspector] 方法「{method.Name}」有多个重载带 [Button]，只保留最派生、"
                            + "最先声明的那一个——按钮文本默认就是方法名，同名无法区分。"
                            + "要都画出来，请给它们起不同的方法名。");
                        continue;
                    }

                    level.Add(CreateMethodMember(method, pathPrefix));
                }

                perLevel.Add(level);
            }

            var members = new List<InspectorProperty>();
            for (var i = perLevel.Count - 1; i >= 0; i--)
            {
                members.AddRange(perLevel[i]);
            }

            return members;
        }

        /// <summary>
        /// 判断一个方法是否该生成方法节点——即它身上有没有「会生成节点」的特性。
        /// </summary>
        /// <param name="method">候选方法。</param>
        /// <returns>该建节点返回 <c>true</c>。</returns>
        /// <remarks>
        /// 判据本身在 <see cref="MemberNodeCriteria.CreatesMethodNode"/>（收集通道与展开判据
        /// 共用那一份）——新增标在方法上的特性时改那里，收集、交错、分组装配、处理器都会自动覆盖。
        /// </remarks>
        private static bool CreatesMethodNode(MethodInfo method)
        {
            return MemberNodeCriteria.CreatesMethodNode(method);
        }

        /// <summary>
        /// 为一个方法建立节点。
        /// </summary>
        /// <param name="method">方法。</param>
        /// <param name="pathPrefix">路径前缀（以 <c>.</c> 结尾）；顶层为空。</param>
        /// <returns>建好的方法节点，尚未挂到父节点上。</returns>
        /// <remarks>
        /// <b>路径是「方法名 + <c>()</c>」。</b> 路径树内唯一是节点的身份契约，而方法名与字段名
        /// 可以跨继承重名（基类字段 <c>Foo</c> + 派生类方法 <c>Foo()</c>），加了后缀就不可能相撞
        /// ——C# 标识符里不允许出现圆括号。
        /// </remarks>
        private static InspectorProperty CreateMethodMember(MethodInfo method, string pathPrefix = null)
        {
            // 嵌套层的路径带父前缀（`stats.Heal()`）——与嵌套反射成员同款，是**合成路径**。
            // 前缀以 `.` 结尾，故顶层传 null 时与从前逐字相同。
            var path = string.IsNullOrEmpty(pathPrefix)
                ? method.Name + MethodPathSuffix
                : pathPrefix + method.Name + MethodPathSuffix;

            return new InspectorProperty(
                method.Name,
                path,
                method.ReturnType,
                InspectorPropertyKind.Method,
                new PropertyAttributes(CollectMemberAttributes(method)))
            {
                // 值入口留空：方法没有值。绘制器要调它时走 Owner 拿到目标对象，不经过值后端。
                Member = method,
            };
        }

        /// <summary>
        /// 按 <see cref="PropertyOrderAttribute"/> 稳定排序成员：数值升序，未标注者视为 <c>0</c>。
        /// </summary>
        /// <param name="members">成员列表，就地排序。</param>
        /// <remarks>
        /// <para>
        /// <b>稳定是必需的。</b> 未标注的成员之间必须保持声明先后——否则字段的呈现次序会随
        /// 列表规模变化（<c>List.Sort</c> 是不稳定排序，这正是分组那边手写插入排序的原因）。
        /// 一个都不标时，本方法不改变任何次序，既有顺序守卫因此照常成立。
        /// </para>
        /// <para>
        /// 三段成员（序列化 / 反射 / 方法）**一起参与**：无标注时原序保持，有标注时跨段排序
        /// 成为可能——这正是「<c>[Button]</c> 方法能排到字段之间」的实现途径。
        /// </para>
        /// <para>
        /// <c>0</c> 在这里是合法值而非「未指定」——与 <see cref="SortGroupNodesAtLevel"/> 的
        /// 「最小非零」刻意不同，理由写在 <see cref="PropertyOrderAttribute.Order"/> 上。
        /// </para>
        /// <para>
        /// <b>本方法排在处理器之前</b>（三段收集之后、<c>new PropertyTree</c> 之前），
        /// 更贴近 OdinGap 记的落点。今天的处理器没有任何一个注入或删除
        /// <see cref="PropertyOrderAttribute"/>，故与「处理器之后」等价；若将来支持**类级**
        /// <c>[PropertyOrder]</c>（把类型上的顺序分发到成员），注入发生在处理器阶段、
        /// 排在这一步之后——那时本方法要挪到处理器之后，否则排序看不到注入的特性。
        /// </para>
        /// </remarks>
        private static void SortMembersByPropertyOrder(List<InspectorProperty> members)
        {
            // 插入排序：稳定，且成员数不大——与 SortGroupNodesAtLevel 同一套手法。
            for (var i = 1; i < members.Count; i++)
            {
                var current = members[i];
                var order = OrderOfMember(current);
                var j = i - 1;

                while (j >= 0 && OrderOfMember(members[j]) > order)
                {
                    members[j + 1] = members[j];
                    j--;
                }

                members[j + 1] = current;
            }
        }

        /// <summary>
        /// 取成员的排序权重：标了 <see cref="PropertyOrderAttribute"/> 用它的值，否则 <c>0</c>。
        /// </summary>
        /// <param name="member">成员节点。</param>
        /// <returns>排序权重。</returns>
        private static float OrderOfMember(InspectorProperty member)
        {
            var attribute = member.Attributes.Get<PropertyOrderAttribute>();
            return attribute?.Order ?? 0f;
        }

        #endregion

        #region 特性处理器

        /// <summary>
        /// 跑处理器的**第一趟**（分组装配之前）：让它们改写节点的特性列表或状态。
        /// </summary>
        /// <param name="root">根节点，类级特性都挂在它上面。</param>
        /// <param name="members">尚未挂到父节点上的成员列表。</param>
        /// <remarks>
        /// <para>
        /// 处理器按 <see cref="AttributeProcessor.ProcessorPriority"/> 升序跑（同优先级按类型名），
        /// 顺序由注册表保证确定——一个处理器注入的特性可能被后一个读到。
        /// </para>
        /// <para>
        /// <b>处理分组特性的处理器不在这里</b>（注册表已按这条拆开）：分组节点本趟还不存在。
        /// 它们走 <see cref="RunGroupProcessors"/>。
        /// </para>
        /// <para>
        /// <b>每个成员先跑「自身」再跑「父级注入」。</b> 这个顺序是契约：注入的特性不该影响
        /// 「这个成员自己有什么」的判断；而反过来，后跑的注入能被已经跑过的处理器看到，
        /// 那正是类级分组分发所需要的。
        /// </para>
        /// </remarks>
        private static void RunProcessors(InspectorProperty root, List<InspectorProperty> members)
        {
            var processors = AttributeProcessorRegistry.FirstPassProcessors;
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

            // 嵌套子节点单独递归一趟：它们不在 members 列表里（那列表只服务顶层装配），
            // 但「先自身、后父级注入」这两步与顶层同款，只是父节点换成了各自的复合父节点。
            // 只**加一趟**，顶层那趟一字未动——顺序契约因此不受影响。
            for (var m = 0; m < members.Count; m++)
            {
                RunNestedProcessors(processors, members[m]);
            }
        }

        /// <summary>
        /// 对复合成员下的子节点跑处理器的两个钩子（递归到自己那一层）。
        /// </summary>
        /// <param name="processors">第一趟处理器（有序）。</param>
        /// <param name="parent">复合父节点。</param>
        private static void RunNestedProcessors(AttributeProcessor[] processors, InspectorProperty parent)
        {
            var children = parent.RawChildren;

            for (var c = 0; c < children.Count; c++)
            {
                var child = children[c];

                for (var i = 0; i < processors.Length; i++)
                {
                    if (processors[i].CanProcessSelfAttributes(child))
                    {
                        processors[i].ProcessSelfAttributes(child, child.Attributes.Raw);
                    }
                }

                // 与顶层同一条守卫：没有反射信息的成员不参与「父级注入」钩子。
                if (child.Member != null)
                {
                    for (var i = 0; i < processors.Length; i++)
                    {
                        if (processors[i].CanProcessChildMemberAttributes(parent, child.Member))
                        {
                            processors[i].ProcessChildMemberAttributes(parent, child.Member, child.Attributes.Raw);
                        }
                    }
                }

                RunNestedProcessors(processors, child);
            }
        }

        /// <summary>
        /// 跑处理器的**第二趟**（分组装配之后）：对**分组节点**跑「处理分组特性」的处理器。
        /// </summary>
        /// <param name="node">当前节点。根与成员会被跳过——它们的自身钩子已在第一趟跑过。</param>
        /// <remarks>
        /// <para>
        /// 深度优先遍历整棵（此时已装配完的）树，只对 <see cref="InspectorPropertyKind.Group"/>
        /// 的节点跑**自身**钩子。「父级注入」钩子留在第一趟：分组之后再往成员身上注入分组特性
        /// 已经太晚，分组装配看不见它，症状是「特性像没写一样」。
        /// </para>
        /// <para>
        /// 本趟只许改 <see cref="PropertyState"/>（如装一个可见性求值器）。此时分组与链都已
        /// 装配完毕，增删特性不会反映到它们上面。
        /// </para>
        /// </remarks>
        private static void RunGroupProcessors(InspectorProperty node)
        {
            var processors = AttributeProcessorRegistry.GroupProcessors;

            if (node.Kind == InspectorPropertyKind.Group && processors.Length > 0)
            {
                for (var i = 0; i < processors.Length; i++)
                {
                    if (processors[i].CanProcessSelfAttributes(node))
                    {
                        processors[i].ProcessSelfAttributes(node, node.Attributes.Raw);
                    }
                }
            }

            var children = node.RawChildren;
            for (var i = 0; i < children.Count; i++)
            {
                RunGroupProcessors(children[i]);
            }
        }

        #endregion

        #region 分组装配

        /// <summary>
        /// 分组装配的总入口：先装顶层，再逐层装嵌套层，最后全树排一次同层分组的先后。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="members">按序列化顺序排列的顶层成员节点。</param>
        /// <remarks>
        /// <para>
        /// 分组节点**落在其首个成员出现的位置**：某个分组第一次被提及时就地插入父节点的
        /// 子列表。于是夹在分组字段之间的未分组字段会留在原地，而不是被挤到 Inspector 末尾
        /// ——后者一眼就能看出不对，却是「先摆所有分组、再摆散字段」那种朴素实现的必然结果。
        /// </para>
        /// <para>
        /// 装配分两段走：顶层这次是「成员还没挂上去、装配负责挂」；嵌套层那次是「成员早在
        /// 收集期就挂在复合父节点下、装配负责把它们搬走」（见 <see cref="AssembleNestedLevel"/>）。
        /// 两段共用同一个 <see cref="AssembleLevel"/>。
        /// </para>
        /// </remarks>
        private static void ApplyGrouping(InspectorProperty root, List<InspectorProperty> members)
        {
            AssembleLevel(root, members, 0);

            ApplyNestedGrouping(root);

            // 全树排一次就够：本方法现在会进所有子节点（含成员节点），
            // 各层分组的 `Order` 因此都被吃到。
            SortGroupNodesAtLevel(root);
        }

        /// <summary>
        /// 把成员搬进各自的分组节点，未声明分组的成员直接挂到 <paramref name="container"/> 下。
        /// </summary>
        /// <param name="container">这一层的父节点（顶层是根，嵌套层是复合成员节点）。</param>
        /// <param name="members">按序列化顺序排列的成员节点，**尚未**挂在 <paramref name="container"/> 下。</param>
        /// <param name="prefixSegments">
        /// 路径前缀的段数：容器路径在分组路径里占几段。顶层为 <c>0</c>；嵌套层为
        /// <c>DepthOf(container.Path)</c>（成员序列化路径不含 <c>/</c>，故通常是 1，
        /// 但按段数表达就不必假设这一点）。
        /// </param>
        private static void AssembleLevel(
            InspectorProperty container,
            List<InspectorProperty> members,
            int prefixSegments)
        {
            var groups = new Dictionary<string, InspectorProperty>(StringComparer.Ordinal);

            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                var groupAttribute = FindDeepestGroupAttribute(member);

                if (groupAttribute == null)
                {
                    container.AddChild(member);
                    continue;
                }

                EnsureMemberGroupAttributes(
                    container, groups, member, groupAttribute.GroupID, prefixSegments);
                groups[groupAttribute.GroupID].AddChild(member);
            }
        }

        /// <summary>
        /// 把嵌套类型（或集合元素类型）里带 <c>[ShowInInspector]</c> 的成员收进**复合父节点**之下。
        /// </summary>
        /// <param name="serializedObject">底层序列化对象（目标列表由它给出）。</param>
        /// <param name="parent">复合成员节点（嵌套层的复合成员，或元素节点）。</param>
        /// <remarks>
        /// <para>
        /// 取值对象是**同一个嵌套实例**（元素层是**那个元素**），由一条构建期编译的字段链
        /// 每帧现读（见 <see cref="ReflectedAccessor.TryCreatePath"/>）——不是绑死的实例，
        /// 因此父字段被重新赋值之后取值跟着走。逐目标的访问器编译走
        /// <see cref="NestedInstanceScope.Compile"/>：与条件族、按名回调族**同一份实现**
        /// （此前这里自有一份逐目标循环，是天然的漂移点）。
        /// </para>
        /// <para>
        /// 路径带父前缀（<c>stats.Total</c>、元素层是 <c>items.Array.data[0].Tag</c>），
        /// 节点种类是 <see cref="InspectorPropertyKind.ReflectedMember"/>——这个路径是**合成的**，
        /// 不是序列化路径，两者绝不能混（见 <c>Kind.Member</c> 的不变量）。
        /// </para>
        /// <para>
        /// <b>一格都编译不出来时不静默</b>：这个类型上确实有会变成节点的成员就说一句
        /// （条件族那条路会各自告警，但反射成员这条没有别的出口——症状会是「写了
        /// <c>[ShowInInspector]</c> 却什么都不出现」）。
        /// </para>
        /// </remarks>
        private static void AppendNestedReflectedMembers(SerializedObject serializedObject, InspectorProperty parent)
        {
            if (parent.Type == null)
            {
                return;
            }

            var targets = serializedObject.targetObjects;
            var scopes = NestedInstanceScope.Compile(targets, parent.Path);
            var usable = 0;

            for (var i = 0; scopes != null && i < scopes.Length; i++)
            {
                if (scopes[i] != null)
                {
                    usable++;
                }
            }

            // 一个目标的实例都取不到：这一层不加反射成员——但**不许静默**（见 remarks）。
            if (usable == 0)
            {
                if (NestedMemberExpansion.HasNonSerializedNodeMember(parent.Type))
                {
                    DrawerWarnings.Once(parent, nameof(AppendNestedReflectedMembers) + ".取不到实例",
                        $"[XInspector] 属性「{parent.Path}」取不到取值实例，"
                        + $"「{parent.Type.Name}」里的 [ShowInInspector] 一族已跳过。");
                }

                return;
            }

            var members = CollectReflectedMembers(
                targets, parent.Type, parent.RawChildren, scopes, parent.Path + ".");

            for (var i = 0; i < members.Count; i++)
            {
                parent.AddChild(members[i]);
            }
        }

        /// <summary>
        /// 把嵌套类型里会生成方法节点的那些方法收进**复合父节点**之下。
        /// </summary>
        /// <param name="parent">复合成员节点。</param>
        /// <remarks>
        /// 与顶层同一条判据（<see cref="MemberNodeCriteria.CreatesMethodNode"/>）、同一套去重规则，
        /// 只是作用在字段的声明类型上、路径带父前缀。调用目标由处理器在构建期解析、
        /// 绘制期现读嵌套实例——那一段在 <see cref="NestedInstanceScope"/>。
        /// </remarks>
        private static void AppendNestedMethodMembers(InspectorProperty parent)
        {
            if (parent.Type == null)
            {
                return;
            }

            var members = CollectMethodMembers(parent.Type, parent.Path + ".");

            for (var i = 0; i < members.Count; i++)
            {
                parent.AddChild(members[i]);
            }
        }

        /// <summary>
        /// 逐层装配嵌套层：每个「有子节点的成员节点」都代表一层。
        /// </summary>
        /// <param name="node">当前节点。</param>
        /// <remarks>
        /// <para>
        /// 用整树深度优先而不是只下探一层：复合成员可能落在分组节点**里面**
        /// （顶层 <c>[BoxGroup("A")]</c> + 复合字段），所以「哪一层要装」得在整棵树上找。
        /// 判据只看 <see cref="InspectorPropertyKind.Member"/>——分组节点的子节点是成员，
        /// 它们自己那一层由各自作为复合父节点时再装，故不会重复装配。
        /// </para>
        /// <para>
        /// <b>装配必须留到这里，不能提前到收集期</b>：第一趟处理器会给成员注入特性
        /// （类级分组就是这么分发到成员上的），装配看不见它们就等于那个特性像没写一样。
        /// </para>
        /// </remarks>
        private static void ApplyNestedGrouping(InspectorProperty node)
        {
            if (node.Kind == InspectorPropertyKind.Member && node.RawChildren.Count > 0)
            {
                AssembleNestedLevel(node);
            }

            // 装配**之后**再取子节点：装配会新建分组节点，它们的子节点同样可能是复合成员。
            var children = node.RawChildren;
            for (var i = 0; i < children.Count; i++)
            {
                ApplyNestedGrouping(children[i]);
            }
        }

        /// <summary>
        /// 装复合成员节点下的那一层：子节点早已挂好，装配负责把它们搬进分组（或留在原地）。
        /// </summary>
        /// <param name="container">复合成员节点。</param>
        /// <remarks>
        /// <para>
        /// <b>前缀是父成员的序列化路径。</b> 同一个嵌套类型用在两处时（<c>stats</c> 与
        /// <c>other</c>），分组路径必须各不相同，否则两处的组会并成一个、节点 Path 也不再唯一。
        /// 前缀因此被写进 <c>GroupID</c>（走 <see cref="PropertyGroupAttribute.CloneForPath"/>，
        /// 与类级分组的分发同一套写法），于是「分组节点恒有 <c>GroupID == node.Path</c>」
        /// 这条不变量在嵌套层照旧成立。
        /// </para>
        /// <para>
        /// <b>前缀不是分组段。</b> 成员序列化路径里不含 <c>/</c>（字段名不可能含），
        /// 所以它整条是**一个不透明段**——<see cref="EnsureGroupChain"/> 靠
        /// <c>prefixSegments</c> 跳过它，绝不会造出一个以成员名命名的假分组节点。
        /// </para>
        /// <para>
        /// <b>重排的做法是「快照 → 清空 → 按原顺序重挂」</b>：<c>AddChild</c> 只追加、
        /// 不从旧父节点摘除，不清空就会留下重复。重挂会重设 <c>Parent</c> 与 <c>Owner</c>
        /// （<c>Owner</c> 此时已回填，是幂等的）。于是「分组落在其首个成员出现的位置」这条
        /// 语义在嵌套层**逐字继承**，不必重新定义；<c>[PropertyOrder]</c> 排好的次序也保住了
        /// （重挂的依据正是排好序的那份快照）。
        /// </para>
        /// </remarks>
        private static void AssembleNestedLevel(InspectorProperty container)
        {
            var children = container.RawChildren;

            // 早退：这一层没有任何分组特性时**原样不动**（不清空、不重挂）。
            // 「没用到本包的类型外观不变」这条契约因此不只靠推理，还靠这条分支。
            if (!HasGroupAttribute(children))
            {
                return;
            }

            for (var i = 0; i < children.Count; i++)
            {
                PrefixGroupAttributes(children[i], container.Path);
            }

            var members = new List<InspectorProperty>(children);
            children.Clear();

            var prefixSegments = DepthOf(container.Path);
            AssembleLevel(container, members, prefixSegments);
        }

        /// <summary>这一层的成员里有没有人带分组特性。</summary>
        /// <param name="members">成员节点。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        private static bool HasGroupAttribute(List<InspectorProperty> members)
        {
            for (var i = 0; i < members.Count; i++)
            {
                if (members[i].Attributes.Has<PropertyGroupAttribute>())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>把成员身上每个分组特性的路径改写成 <c>「前缀/原路径」</c>。</summary>
        /// <param name="member">成员节点。</param>
        /// <param name="prefix">前缀，即它所属复合容器的序列化路径。</param>
        /// <remarks>
        /// 克隆而不是就地改：特性实例来自反射，但同一个实例可能被别处引用着
        /// （「每个属性一份独立实例」那条纪律的同一个理由）。
        /// </remarks>
        private static void PrefixGroupAttributes(InspectorProperty member, string prefix)
        {
            var attributes = member.Attributes.Raw;

            for (var i = 0; i < attributes.Count; i++)
            {
                if (attributes[i] is PropertyGroupAttribute group)
                {
                    attributes[i] = group.CloneForPath(
                        prefix + PropertyGroupPath.Separator + group.GroupID);
                }
            }
        }

        /// <summary>
        /// 让成员身上「属于目标路径链」的每个分组特性各贡献自己那一段路径。
        /// </summary>
        /// <param name="container">这一层的父节点（顶层是根，嵌套层是复合成员节点）。</param>
        /// <param name="groups">已建分组节点的查找表。</param>
        /// <param name="member">成员节点。</param>
        /// <param name="targetPath">成员最终要落的路径（最深那条，含前缀）。</param>
        /// <param name="prefixSegments">路径前几段是容器路径（不造节点），顶层为 0。</param>
        /// <remarks>
        /// <para>
        /// 只处理**是目标路径前缀（或相等）**的那些特性：成员只归属最深的一条链，
        /// 不相关的分组仍被忽略（既有规则不变）。不这么做的话
        /// <c>[MarkerGroup("H")] [BoxGroup("H/Box")]</c> 里 H 拿不到 Marker 那份特性——
        /// 行静默不开、分数静默失效，正是本仓最想避免的一类现象。
        /// </para>
        /// <para>
        /// 按路径**由浅到深**处理：每个祖先节点优先由它自己那份声明创建，
        /// 「呈现设定先声明者优先」才落在正确的来源上。
        /// </para>
        /// </remarks>
        private static void EnsureMemberGroupAttributes(
            InspectorProperty container,
            Dictionary<string, InspectorProperty> groups,
            InspectorProperty member,
            string targetPath,
            int prefixSegments)
        {
            var attributes = member.Attributes;
            var deepestDepth = DepthOf(targetPath);

            // 从**前缀之后**的第一段起：前 prefixSegments 段是容器路径，不是分组。
            for (var depth = prefixSegments + 1; depth <= deepestDepth; depth++)
            {
                for (var i = 0; i < attributes.Count; i++)
                {
                    if (attributes[i] is PropertyGroupAttribute group &&
                        DepthOf(group.GroupID) == depth &&
                        IsPrefixOrEqual(group.GroupID, targetPath))
                    {
                        EnsureGroupChain(container, groups, group.GroupID, group, prefixSegments);
                    }
                }
            }
        }

        /// <summary>
        /// <paramref name="prefix"/> 是否为 <paramref name="path"/> 的路径前缀（含相等）。
        /// </summary>
        /// <param name="prefix">候选前缀。</param>
        /// <param name="path">完整路径，两者都已规范化。</param>
        /// <returns>是前缀返回 <c>true</c>。</returns>
        /// <remarks>
        /// 按**段**判断而不是字符串前缀：<c>"Ab"</c> 是 <c>"Abc"</c> 的字符串前缀，
        /// 却是两个不相干的分组。
        /// </remarks>
        private static bool IsPrefixOrEqual(string prefix, string path)
        {
            if (string.Equals(prefix, path, StringComparison.Ordinal))
            {
                return true;
            }

            return path.Length > prefix.Length &&
                   path[prefix.Length] == PropertyGroupPath.Separator &&
                   string.CompareOrdinal(path, 0, prefix, 0, prefix.Length) == 0;
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
        /// 确保从容器到指定路径的整条分组链都存在，返回最深的那一节。
        /// </summary>
        /// <param name="container">这一层的父节点（顶层是根，嵌套层是复合成员节点）。</param>
        /// <param name="groups">已建分组节点的查找表。</param>
        /// <param name="path">目标路径（含前缀），已规范化。</param>
        /// <param name="source">用于合成祖先节点的来源特性。</param>
        /// <param name="prefixSegments">路径前几段是容器路径（不造节点），顶层为 0。</param>
        /// <returns>路径末段对应的分组节点。</returns>
        /// <remarks>
        /// <para>
        /// 祖先节点由 <see cref="PropertyGroupAttribute.CloneForPath"/> 从来源特性复制而来，
        /// 因此 <c>ShowLabel</c> 这类子类字段会一并带到祖先上——语义是「祖先继承后代的呈现设定」，
        /// 比凭空造一个全默认的祖先更符合直觉。
        /// </para>
        /// <para>
        /// <b>前 <paramref name="prefixSegments"/> 段只累积路径、不造节点。</b> 嵌套层里前缀是
        /// 父成员的序列化路径（如 <c>stats</c>），它不是分组段——给它造节点会得到一个名为
        /// <c>stats</c> 的**分组**节点，挂在同样叫 <c>stats</c> 的**成员**节点下：
        /// 既是多余的一层框，又让 <c>Path</c> 不再唯一。节点身份仍是完整路径，故
        /// <c>current</c> 照常累积所有段。
        /// </para>
        /// </remarks>
        private static InspectorProperty EnsureGroupChain(
            InspectorProperty container,
            Dictionary<string, InspectorProperty> groups,
            string path,
            PropertyGroupAttribute source,
            int prefixSegments)
        {
            var parent = container;
            string current = null;

            var segments = path.Split(PropertyGroupPath.Separator);
            for (var i = 0; i < segments.Length; i++)
            {
                current = current == null ? segments[i] : current + PropertyGroupPath.Separator + segments[i];

                if (i < prefixSegments)
                {
                    continue;
                }

                if (groups.TryGetValue(current, out var node))
                {
                    MergeGroupAttribute(node, source.CloneForPath(current));
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
        /// 把一份分组特性并入既有分组节点：**同类型**走 <see cref="PropertyGroupAttribute.Combine"/>，
        /// **不同类型**并存。
        /// </summary>
        /// <param name="node">分组节点。</param>
        /// <param name="incoming">要并入的克隆，路径已改写为节点路径。</param>
        /// <remarks>
        /// <para>
        /// 同类型合并的理由是「同一个分组被多个字段各声明一次」——不合并的话，
        /// 「哪个字段先声明」就会悄悄决定分组的标题与排序。
        /// </para>
        /// <para>
        /// 不同类型并存的理由：两种分组特性落同一路径时**都该有自己的一格绘制器**
        /// （如「标题在外、框在内」）。此前只留先创建者那份，第二种被静默丢弃——
        /// 而且祖先节点带哪种类型取决于声明顺序，是典型的静默失效。
        /// </para>
        /// <para>
        /// 新增类型后要**重挂链**：链在装配时按当时的特性列表构建过。此时尚未绘制，
        /// 重挂是安全的。
        /// </para>
        /// </remarks>
        private static void MergeGroupAttribute(InspectorProperty node, PropertyGroupAttribute incoming)
        {
            var attributes = node.Attributes;

            for (var i = 0; i < attributes.Count; i++)
            {
                if (attributes[i].GetType() == incoming.GetType())
                {
                    ((PropertyGroupAttribute)attributes[i]).Combine(incoming);
                    return;
                }
            }

            attributes.Raw.Add(incoming);
            AttachChain(node, ChildrenTerminal);
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

            // 无条件进所有子节点：嵌套层的分组挂在**成员节点**之下，只认 Kind == Group
            // 会让那些分组永远轮不到 Order 排。判据少一条，代价只是构建期多走一遍树
            // （每一层各自重排，没有分组的层是空操作）。
            for (var i = 0; i < children.Count; i++)
            {
                SortGroupNodesAtLevel(children[i]);
            }
        }

        /// <summary>取分组节点的排序权重。</summary>
        /// <param name="group">分组节点。</param>
        /// <returns>权重；节点上没有分组特性（或其 Order 全是 0）时返回 0。</returns>
        /// <remarks>
        /// 节点上可能有多种分组特性，规则取**最小的非零值**：0 表示「未指定」
        /// （与 <see cref="PropertyGroupAttribute.Combine"/> 的「取先出现的非零值」同一精神），
        /// 冲突时更靠前的意愿赢；且**与声明顺序无关**——把顺序依赖请回来正是多类型改动要避免的。
        /// </remarks>
        private static float OrderOf(InspectorProperty group)
        {
            var attributes = group.Attributes;
            var order = 0f;

            for (var i = 0; i < attributes.Count; i++)
            {
                if (!(attributes[i] is PropertyGroupAttribute attribute) || attribute.Order == 0f)
                {
                    continue;
                }

                if (order == 0f || attribute.Order < order)
                {
                    order = attribute.Order;
                }
            }

            return order;
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
        /// 收集成员（字段或方法）上的特性。
        /// </summary>
        /// <param name="member">成员，可为 <c>null</c>。</param>
        /// <returns>特性列表；无成员时为空列表。</returns>
        /// <remarks>
        /// 反射每次调用都返回**新实例**，故这里天然满足「每个属性一份独立特性」——
        /// 不会出现多个属性共享同一个特性实例、改一个串一片的问题。
        /// 类级特性的分发才需要显式克隆。
        /// </remarks>
        private static List<Attribute> CollectMemberAttributes(MemberInfo member)
        {
            var result = new List<Attribute>();
            if (member == null)
            {
                return result;
            }

            foreach (var attribute in member.GetCustomAttributes(true))
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
