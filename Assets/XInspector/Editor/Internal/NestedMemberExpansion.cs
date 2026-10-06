using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 嵌套 `[Serializable]` 类型的**按需展开**：什么时候把它的成员变成真节点，
    /// 以及按点分路径解析嵌套字段。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>按需展开是这块能力的安全阀。</b> 只给「用到了本包」的嵌套类型展开——判据是
    /// <see cref="ShouldExpand"/>：嵌套成员里有任何一个带**本包支持的特性**。其余情形整份交回
    /// <c>UnityFallbackDrawer</c>——「没用到本包的类型外观不变」这条契约不能破。
    /// </para>
    /// <para>
    /// <b>只做固定形状的那一半。</b> 数组与列表不展开（元素个数随时可变，与「树的形状在构建
    /// 结束后冻结」正面冲突，那是元素节点化的领域）；多态引用（<c>[SerializeReference]</c>）
    /// 也不展开——那是 L7 那条产品线。
    /// </para>
    /// <para>
    /// <b>成员级的分组特性自 2026-10-06 起生效</b>（构建期装配出分组节点，见
    /// <c>PropertyTreeBuilder.AssembleNestedLevel</c>）；**类级**特性仍不生效，见
    /// <see cref="WarnAboutInertTypeGroups"/>。
    /// </para>
    /// </remarks>
    internal static class NestedMemberExpansion
    {
        #region Private Fields

        /// <summary>递归扫描的深度上限——挡住自引用类型（<c>class Node { Node next; }</c>）。</summary>
        /// <remarks>
        /// 与 <c>[InlineEditor]</c> 的递归上限同档取 <c>4</c>：本包自定值，写进文档。
        /// 序列化属性那条路 Unity 自己会截断，纯反射这条路不会，故必须自己挡。
        /// <c>ReflectedAccessor</c> 的路径访问器也用它——同一条路径，同一个上限。
        /// </remarks>
        internal const int MaxDepth = 4;

        #endregion

        #region Public API

        /// <summary>
        /// 展开过的嵌套类型若在**类型上**带分组特性，报一次告警。
        /// </summary>
        /// <param name="parent">刚展开过的复合成员节点。</param>
        /// <remarks>
        /// <para>
        /// 嵌套层里的**成员级**分组特性自 2026-10-06 起正常生效（构建期会装配出分组节点）。
        /// 仍然不生效的是**类级**特性：本包只在被检视类型上收集类级特性
        /// （<c>CollectTypeAttributes</c> 只对根调用），嵌套类型的类级特性没有任何收集通道，
        /// 判据也照此收窄（类级特性不触发展开）。
        /// </para>
        /// <para>
        /// 这条告警只在**已经展开**的类型上说话：没展开就整份交给 Unity，那是文档写明的边界；
        /// 一旦展开，这份静默就是本包的。收件人明确、不会误报。
        /// </para>
        /// </remarks>
        public static void WarnAboutInertTypeGroups(InspectorProperty parent)
        {
            var type = parent.Type;
            if (type == null)
            {
                return;
            }

            foreach (var attribute in type.GetCustomAttributes(true))
            {
                if (attribute is PropertyGroupAttribute group)
                {
                    Debug.LogWarning(
                        $"[XInspector] 嵌套类型「{type.Name}」的**类级**分组特性" +
                        $"（[{group.GetType().Name}(\"{group.GroupID}\")]）不生效：" +
                        "类级特性只在被检视的最外层类型上收集，嵌套类型上的没有收集通道。" +
                        "把分组标到**成员**上即可生效。");
                    return;
                }
            }
        }

        /// <summary>
        /// 这个类型（作为字段的**声明类型**）会不会被按需展开——**自动接管的判据**用它。
        /// </summary>
        /// <param name="declaredType">字段的声明类型。</param>
        /// <returns>会被展开返回 <c>true</c>。</returns>
        /// <remarks>
        /// 与 <see cref="ShouldExpand"/> 的差别：这条**只看类型**（没有节点、没有序列化属性），
        /// 供 <c>XInspectorUsageDetection</c> 在「这个类型用没用到本插件」那一问里使用。
        /// 两条判据必须指向同一批类型——判据看不见的展开就是静默失效。
        /// </remarks>
        public static bool WouldExpand(Type declaredType)
        {
            if (declaredType == null || declaredType.IsArray || declaredType.IsPrimitive || declaredType.IsEnum)
            {
                return false;
            }

            if (declaredType == typeof(string) || declaredType == typeof(object))
            {
                return false;
            }

            return HasSupportedField(declaredType, new HashSet<Type>(), 0);
        }

        /// <summary>
        /// 这个类型（含更深层）的**可序列化字段**里有没有本包支持的特性——判据的唯一实现处。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// 两条腿都算：可序列化字段上的特性，**以及**迭代器看不见、但会产生节点的成员
        /// （<c>[ShowInInspector]</c> 的字段与属性、会生成方法节点的 <c>[Button]</c>）。
        /// 后者自 2026-10-06（元素层的读路径）起在嵌套层与元素层**都有消费者**——
        /// 在那之前元素侧刻意只数前一半，因为判据放开而消费者没到位等于「展开了却什么都画不出来」。
        /// </para>
        /// <para>
        /// 只按**声明类型**走，不解包数组 / <c>List</c>：要不要为某个集合建元素层，
        /// 判据问的是「**元素类型**里有没有用得上的东西」（见
        /// <see cref="CollectionElementExpansion.UsesPackageInElement"/>），
        /// 由调用方把元素类型递进来。数组字段本身永远不算——它要么被本包接管（另判），
        /// 要么整份交给 Unity。
        /// </para>
        /// </remarks>
        internal static bool ContainsSupportedFields(Type type)
        {
            return HasSupportedField(type, new HashSet<Type>(), 0);
        }

        /// <summary>
        /// 这个成员该不该展开成子节点。
        /// </summary>
        /// <param name="member">成员节点（建树期刚建好、还没挂链）。</param>
        /// <param name="property">它的序列化属性。</param>
        /// <returns>该展开返回 <c>true</c>。</returns>
        public static bool ShouldExpand(InspectorProperty member, SerializedProperty property)
        {
            if (!IsCompositeCandidate(member, property))
            {
                return false;
            }

            // 字段自己标了内联。
            if (member.Attributes.Has<InlinePropertyAttribute>())
            {
                return true;
            }

            // 声明类型上标了内联（官方的类级形态；Inherited = false，只看声明类型自身）。
            if (IsClassLevelInlineMarked(member.Type))
            {
                return true;
            }

            // 字段自己标了搜索：**它的子成员就是被过滤的对象**，不展开就无从过滤。
            // 这是继 [InlineProperty] 之后第二条「字段自己的特性也参与展开判据」的口子，
            // 代价同样是外观改变（从「整份交给 Unity」变成本包的折叠头 + 缩进）——
            // 但它是用户显式写下的，写进文档即可。
            //
            // **类型侧不跟着变**：WouldExpand（只给 IsUsedBy 用）仍返回 false。这是刻意的，
            // 判据必须与展开看同一批类型才对——这里没有类型级特性在起作用，
            // 是**字段上的**特性让类型进管线，而字段自己会被 IsUsedBy 扫到。
            if (member.Attributes.Has<SearchableAttribute>())
            {
                return true;
            }

            // 嵌套成员里有本包支持的特性——递归判一遍（孙辈也带特性时，得连展开两层）。
            // **判据只看成员级特性，不看嵌套类型的类级特性**（[InlineProperty] 除外，它是
            // 官方的类级形态）：其它类级特性本轮不生效，算进来等于「为了一个不画东西的特性而展开」。
            return HasSupportedMember(property, member.Type, 0);
        }

        /// <summary>
        /// 该成员够不够格当「复合成员」：真的复合类型、非数组、非多态引用、有可见子字段。
        /// </summary>
        /// <param name="member">成员节点。</param>
        /// <param name="property">它的序列化属性。</param>
        /// <returns>够格返回 <c>true</c>。</returns>
        private static bool IsCompositeCandidate(InspectorProperty member, SerializedProperty property)
        {
            if (member == null || property == null || member.Type == null)
            {
                return false;
            }

            // 要求 Generic：向量之类也有可见子级，但它们由原生控件整块画（本包不拆）。
            if (property.propertyType != SerializedPropertyType.Generic)
            {
                return false;
            }

            // 数组/列表是元素节点化的领域（动态形状），不在这里展开。
            if (property.isArray)
            {
                return false;
            }

            // 没有可见的序列化子字段时，只有**声明类型里还有非序列化的本包成员**才值得展开
            // （典型的是「只放了一个 [ShowInInspector] 属性」的嵌套类型）。
            // 不加这条：那种类型整份交回 Unity，里面的特性永远没机会生效。
            if (!property.hasVisibleChildren && !HasNonSerializedNodeMember(member.Type))
            {
                return false;
            }

            // [SerializeReference] 的多态引用是 L7 那条产品线——别在这里开半扇门。
            // 判据落在**字段**上（见 IsPolymorphicReference）：今天这类字段在序列化属性上是
            // ManagedReference，本来就过不了上面「要求 Generic」那一关，但两道闸说的是同一件事——
            // 哪道闸先变（Unity 改了 propertyType、或者判据被挪了位置）都不该由我们赌。
            return !IsPolymorphicReference(member.Member as FieldInfo);
        }

        /// <summary>
        /// 这个类型（作为字段的**声明类型**）上有没有类级 <see cref="InlinePropertyAttribute"/>。
        /// </summary>
        /// <param name="declaredType">字段的声明类型。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// 官方的类级形态：标在类上 → 该类型的字段一律内联（<c>Inherited = false</c>，
        /// 只看声明类型自身）。消费者是 <c>ClassLevelInlinePropertyProcessor</c>——它把类型上那份
        /// **注入**到字段身上；判据因此必须在**每一个看字段的地方**都问这一句。
        /// </para>
        /// <para>
        /// <b>判据只此一份</b>：展开判据（<see cref="ShouldExpand"/>）、两条递归判据
        /// （<see cref="HasSupportedMember"/> / <see cref="HasSupportedField"/>）与自动接管判据
        /// （<c>XInspectorUsageDetection</c>）四处共用。
        /// </para>
        /// </remarks>
        public static bool IsClassLevelInlineMarked(Type declaredType)
        {
            return declaredType != null && declaredType.IsDefined(typeof(InlinePropertyAttribute), false);
        }

        /// <summary>
        /// 这个字段是不是多态引用（<c>[SerializeReference]</c>）。
        /// </summary>
        /// <param name="field">字段；传 <c>null</c> 时返回 <c>false</c>。</param>
        /// <returns>是返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>要看的是字段，不是它的声明类型。</b> <c>[SerializeReference]</c> 的用法声明就是
        /// <c>AttributeTargets.Field</c>，写成「类型上有没有它」是一句**恒为假**的判据
        /// （2026-10-06 核出来并改正：那句话在展开判据里挂了很久，靠另一道闸才没出事）。
        /// </para>
        /// <para>
        /// 今天真正挡住这类字段的是「序列化属性必须是 Generic」那一关
        /// （它们在 Unity 里报 <c>ManagedReference</c>）。这条判据仍然留着：
        /// 展开判据、搜索的构建期告警、路径访问器三处问的是同一个问题，
        /// 答案只该有一份——三处各写一遍的话，迟早有一处先漂。
        /// </para>
        /// </remarks>
        public static bool IsPolymorphicReference(FieldInfo field)
        {
            return field != null && field.IsDefined(typeof(SerializeReference), true);
        }

        /// <summary>
        /// 这个类型（含继承链）上有没有**序列化迭代器看不见**、但会变成节点的成员。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// 展开判据的两条腿：序列化那一半由调用方沿 <c>SerializedProperty</c> 迭代器扫，
        /// 这里管的是迭代器**看不见**的那一半——<c>[ShowInInspector]</c> 的字段与属性、
        /// 以及会生成**方法节点**的方法。
        /// 只看序列化那一半的话，「只放了一个 <c>[ShowInInspector]</c> 属性」或
        /// 「只放了一个 <c>[Button]</c> 方法」的嵌套类型永远不会展开，
        /// 那些特性永远没机会生效——而这是**没有告警**的。
        /// </para>
        /// <para>
        /// 序列化字段跳过：它们由迭代器那条腿扫，两边都算会让判据的语义含混。
        /// </para>
        /// </remarks>
        public static bool HasNonSerializedNodeMember(Type type)
        {
            // 含 Static：收集通道（CollectMethodMembers）与自动接管判据都收静态成员，
            // 这里漏掉就会造出「只放静态按钮的嵌套类型」判据与收集不一致——判据看不见的展开，
            // 症状同样是零告警。
            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (var field in current.GetFields(Flags))
                {
                    if (!IsSerializableField(field) && MemberNodeCriteria.CarriesShowInInspector(field))
                    {
                        return true;
                    }
                }

                foreach (var property in current.GetProperties(Flags))
                {
                    // 索引器没有「一个目标对应一个值」的语义，收集通道也不收它。
                    if (property.GetIndexParameters().Length == 0 &&
                        MemberNodeCriteria.CarriesShowInInspector(property))
                    {
                        return true;
                    }
                }

                foreach (var method in current.GetMethods(Flags))
                {
                    if (MemberNodeCriteria.CreatesMethodNode(method))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 按**点分路径**逐层解析字段（嵌套成员用；现成的那条只认顶层名）。
        /// </summary>
        /// <param name="rootType">起点类型（被检视对象的类型）。</param>
        /// <param name="path">序列化路径，如 <c>nested.field</c>。</param>
        /// <returns>字段；解析不到时返回 <c>null</c>。</returns>
        /// <remarks>
        /// 逐段下钻：每段在该层类型上按 <c>DeclaredOnly</c> 逐级上溯找同名字段，再进它的
        /// <see cref="FieldInfo.FieldType"/>。解析不到不抛——子节点照建，只是拿不到特性
        /// （与 <c>m_Script</c> 那类 Unity 注入成员的处置一致）。
        /// </remarks>
        public static FieldInfo ResolveField(Type rootType, string path)
        {
            if (rootType == null || string.IsNullOrEmpty(path))
            {
                return null;
            }

            var current = rootType;
            var field = (FieldInfo)null;
            var start = 0;

            while (start < path.Length)
            {
                var separator = path.IndexOf('.', start);
                var name = separator < 0 ? path.Substring(start) : path.Substring(start, separator - start);

                field = FindDeclaredField(current, name);
                if (field == null)
                {
                    return null;
                }

                current = field.FieldType;

                if (separator < 0)
                {
                    break;
                }

                start = separator + 1;
            }

            return field;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 序列化属性这一层（含更深层）里有没有**本包支持的特性**。
        /// </summary>
        /// <param name="property">复合成员的序列化属性。</param>
        /// <param name="declaringType">它的声明类型。</param>
        /// <param name="depth">当前深度（挡自引用类型）。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        /// <remarks>
        /// 逐个子字段用路径解析器拿 <see cref="FieldInfo"/>（拿不到就跳过——那条子路径不是
        /// 普通字段），再用 <see cref="XInspectorUsageDetection.HasSupportedAttribute"/> 判——
        /// 「本包支持的特性」只有那一个定义处。
        /// </remarks>
        private static bool HasSupportedMember(SerializedProperty property, Type declaringType, int depth)
        {
            if (declaringType == null || depth > MaxDepth)
            {
                return false;
            }

            // 先看**迭代器看不见**的那一半（[ShowInInspector] 的字段与属性）。
            if (HasNonSerializedNodeMember(declaringType))
            {
                return true;
            }

            var child = property.Copy();
            var childDepth = property.depth + 1;
            var next = child.NextVisible(true) && child.depth == childDepth;

            while (next)
            {
                var field = FindDeclaredField(declaringType, child.name);

                if (field != null)
                {
                    if (XInspectorUsageDetection.HasSupportedAttribute(field.GetCustomAttributes(true)))
                    {
                        return true;
                    }

                    // 声明类型上的类级内联：注入（ClassLevelInlinePropertyProcessor）真的会发生，
                    // 判据看不见它就成了「消费者在等、判据没放开」——内联静默失效。
                    if (IsClassLevelInlineMarked(field.FieldType))
                    {
                        return true;
                    }

                    // 再往下一层看：孙辈带特性时，这一层也得展开，否则它进不了树。
                    if (child.hasVisibleChildren && HasSupportedMember(child, field.FieldType, depth + 1))
                    {
                        return true;
                    }
                }

                next = child.NextVisible(false) && child.depth == childDepth;
            }

            return false;
        }

        /// <summary>
        /// 纯反射版：这个类型（含更深层）的**可序列化字段**里有没有本包支持的特性。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <param name="visited">已访问的类型（挡环形引用）。</param>
        /// <param name="depth">当前深度。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        private static bool HasSupportedField(Type type, HashSet<Type> visited, int depth)
        {
            if (type == null || depth > MaxDepth || !visited.Add(type))
            {
                return false;
            }

            // 与 HasSupportedMember 同一条腿：迭代器看不见的那些成员。
            if (HasNonSerializedNodeMember(type))
            {
                return true;
            }

            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (var field in current.GetFields(Flags))
                {
                    if (!IsSerializableField(field))
                    {
                        continue;
                    }

                    if (XInspectorUsageDetection.HasSupportedAttribute(field.GetCustomAttributes(true)))
                    {
                        return true;
                    }

                    // 与 HasSupportedMember 同一条腿：声明类型上的类级内联也算数。
                    if (IsClassLevelInlineMarked(field.FieldType))
                    {
                        return true;
                    }

                    if (HasSupportedField(field.FieldType, visited, depth + 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>该字段算不算「会被 Unity 序列化」（展开只看这些字段的特性）。</summary>
        /// <param name="field">字段。</param>
        /// <returns>算返回 <c>true</c>。</returns>
        /// <remarks>
        /// 元素层的边界扫描（<c>CollectionElementExpansion</c>）也用它——
        /// 「哪些字段会进树」这件事只该有一个答案。
        /// </remarks>
        internal static bool IsSerializableField(FieldInfo field)
        {
            if (field.IsStatic || field.IsNotSerialized || field.IsDefined(typeof(HideInInspector), true))
            {
                return false;
            }

            return field.IsPublic || field.IsDefined(typeof(SerializeField), true);
        }

        /// <summary>在该类型及其基类上找同名**实例**字段（<c>DeclaredOnly</c> 逐级上溯）。</summary>
        /// <param name="type">起始类型。</param>
        /// <param name="name">字段名。</param>
        /// <returns>字段；找不到返回 <c>null</c>。</returns>
        /// <remarks>
        /// <b>只看实例字段</b>（<see cref="BindingFlags"/> 里没有 <c>Static</c>）：路径表达的是
        /// 「从这个实例往下走」，静态成员不在实例里，够不着也不该够着。
        /// <see cref="ReflectedAccessor.TryCreatePath"/> 也用本方法，两条路对「哪些段认得」必须一致。
        /// </remarks>
        internal static FieldInfo FindDeclaredField(Type type, string name)
        {
            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                var field = current.GetField(name, Flags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        #endregion
    }
}
