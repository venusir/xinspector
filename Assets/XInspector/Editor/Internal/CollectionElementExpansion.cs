using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 集合元素节点化：安全阀（什么时候把元素变成真节点）、层的状态，以及边界告警。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么要有安全阀。</b> 元素节点化会把一个集合的外观从「整份交给 Unity」换成
    /// 「本包自绘的行 + 逐个子节点」——挨上它的集合外观就变了。判据因此收紧到
    /// 「**元素类型真的用到了本包** 且 **这个集合已经被本包接管**」：
    /// 没用到本包的集合外观逐字不变，与嵌套层的按需展开是同一条契约。
    /// </para>
    /// <para>
    /// <b>元素层是 <c>arraySize</c> 的同步投影。</b> 节点数与真实元素数**不允许**长期不一致：
    /// 每趟绘制之前由 <see cref="CollectionElementSync"/> 对账一次，对不上就整层丢弃重建。
    /// 推论：**元素节点不跨结构变更**——拿着一个元素节点等下一趟再读是错的（旧节点已作废），
    /// 这条写进了 <see cref="InspectorProperty.RawChildren"/> 的形状契约。
    /// </para>
    /// <para>
    /// <b>只做一层。</b> 元素**里面**的集合不节点化（见 <see cref="HasElementLayerAncestor"/>）：
    /// 内层跟着递归的话，节点数会随外层元素个数乘性放大（10×10×10 就是千级节点）——
    /// 那是**数据规模**的爆炸，不是类型深度的爆炸，<c>MaxDepth</c> 挡不住它。
    /// </para>
    /// <para>
    /// <b>判据看不见的展开是静默，看得见却不发生的展开也必须是响的。</b>
    /// 用到了本包却被挡住（没容器 / 表格形态 / 在元素层里面）时一律构建期告警一次；
    /// 真的没用本包的什么都不说（那是「外观不变」的正常路径）。
    /// </para>
    /// </remarks>
    internal static class CollectionElementExpansion
    {
        #region Public Types

        /// <summary>
        /// 一个集合该不该建元素层——结论连同理由，供构建期决定要不要告警。
        /// </summary>
        internal enum ElementLayerDecision
        {
            /// <summary>该建。</summary>
            Build,

            /// <summary>元素类型用不到本包：什么都不说（外观与从前逐字一致）。</summary>
            Inert,

            /// <summary>用到了，但这个字段没有被本包接管（没有容器）——元素里的特性不会生效。</summary>
            NoContainer,

            /// <summary>用到了，但它是表格形态——表格仍按单元格画。</summary>
            Table,

            /// <summary>用到了，但它在元素层里面——元素层只做一层。</summary>
            Nested,
        }

        #endregion

        #region 安全阀

        /// <summary>
        /// 判这个集合的元素层该不该建；被挡住时一并给出理由（供告警）。
        /// </summary>
        /// <param name="collection">成员节点。</param>
        /// <returns>判定结论。</returns>
        /// <remarks>
        /// <b>必须在第一趟处理器之后调用</b>：容器项（<c>[ListDrawerSettings]</c>）可能是处理器
        /// 注入的——<c>[TableList]</c> / <c>[Searchable]</c> / <c>[OnCollectionChanged]</c>
        /// 都会经 <c>EnsureListSettings</c> 补一份，判据要读注入**之后**的事实。
        /// </remarks>
        public static ElementLayerDecision Decide(InspectorProperty collection)
        {
            var array = collection?.ValueEntry?.SerializedProperty;

            // 不是被本包接管的数组 / List（字符串被 Unity 视作 isArray，单独排除）——
            // 与集合绘制器的降级判据同源，一份实现。
            if (!CollectionDrawerLayout.CanDraw(array))
            {
                return ElementLayerDecision.Inert;
            }

            var elementType = ElementTypeOf(collection);

            // 元素阀：元素类型（含深层）里有没有本包用得上的东西——**两条腿都算**
            // （可序列化字段上的特性，以及 [ShowInInspector] / [Button] 一族）。
            // 后者的消费者自 2026-10-06（元素层的读路径）起就在了：判据放开而消费者没到位，
            // 等于「展开了却什么都画不出来」，比不展开更糟。
            if (!UsesPackageInElement(elementType))
            {
                return ElementLayerDecision.Inert;
            }

            // 表格形态本轮不节点化：单元格画法逐字不变（那是另一种容器呈现，不是元素层）。
            if (collection.Attributes.Has<TableListAttribute>())
            {
                return ElementLayerDecision.Table;
            }

            // 深度：元素层里面不再建元素层。
            if (HasElementLayerAncestor(collection))
            {
                return ElementLayerDecision.Nested;
            }

            // 容器项：没有自绘容器就没有画元素行的落点（元素节点建了也没人画）。
            if (!collection.Attributes.Has<ListDrawerSettingsAttribute>())
            {
                return ElementLayerDecision.NoContainer;
            }

            return ElementLayerDecision.Build;
        }

        /// <summary>
        /// 这个类型（含深层，到 <c>MaxDepth</c>）用不用得到本包。
        /// </summary>
        /// <param name="elementType">元素类型。</param>
        /// <returns>用得到返回 <c>true</c>。</returns>
        /// <remarks>
        /// 排除原生标量、枚举、字符串与 <see cref="UnityEngine.Object"/> 派生（对象引用不是内联
        /// 序列化的，它的字段不在元素里）之后，交给展开判据的唯一实现处
        /// （<see cref="NestedMemberExpansion.ContainsSupportedFields"/>）——
        /// 「哪些特性算本包支持的」只有那一份答案。
        /// </remarks>
        public static bool UsesPackageInElement(Type elementType)
        {
            if (elementType == null || elementType.IsPrimitive || elementType.IsEnum ||
                elementType == typeof(string) || elementType == typeof(object) ||
                typeof(UnityEngine.Object).IsAssignableFrom(elementType))
            {
                return false;
            }

            return NestedMemberExpansion.ContainsSupportedFields(elementType);
        }

        /// <summary>这个节点是不是落在某个元素层**里面**（深度判据）。</summary>
        /// <param name="node">节点。</param>
        /// <returns>在里面返回 <c>true</c>。</returns>
        /// <remarks>
        /// 沿父链上溯找带层状态的节点。构建期顶层成员的 <c>Parent</c> 还是 <c>null</c>
        /// （分组装配之后才挂到根上），因此这条判据在构建期与绘制期都成立。
        /// </remarks>
        public static bool HasElementLayerAncestor(InspectorProperty node)
        {
            for (var current = node?.Parent; current != null; current = current.Parent)
            {
                if (current.State.Get<CollectionElementLayerState>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>这个节点是不是某个元素层的**元素节点**。</summary>
        /// <param name="node">节点。</param>
        /// <returns>是返回 <c>true</c>。</returns>
        /// <remarks>
        /// 判据是「父节点带层状态」——元素层的子节点**全是**元素节点，没有别的种类
        /// （元素身上标不了特性，分组装配也不会把它们搬走）。跨趟消费者要按这条判据
        /// 把元素子树排掉（搜索的节点过滤、重置的成员路径收集）。
        /// </remarks>
        public static bool IsElementNode(InspectorProperty node)
        {
            return node?.Parent != null && node.Parent.State.Get<CollectionElementLayerState>() != null;
        }

        /// <summary>取元素类型；不是数组 / <c>List&lt;T&gt;</c> 时返回 <c>null</c>。</summary>
        /// <param name="collection">集合节点。</param>
        /// <returns>元素类型。</returns>
        public static Type ElementTypeOf(InspectorProperty collection)
        {
            return collection?.Type == null ? null : CollectionElement.TypeOf(collection.Type);
        }

        #endregion

        #region 状态与告警

        /// <summary>
        /// 标记某个集合的元素层需要重建（结构刚被改过）。没有元素层时什么都不做。
        /// </summary>
        /// <param name="collection">集合节点。</param>
        /// <remarks>
        /// 由 <c>CollectionChangeInvoker</c> 在增删**真的落地**之后调用。见
        /// <see cref="CollectionElementLayerState.Dirty"/> 里对「为什么不能只看长度」的说明。
        /// </remarks>
        public static void MarkLayerDirty(InspectorProperty collection)
        {
            var layer = collection?.State.Get<CollectionElementLayerState>();
            if (layer != null)
            {
                layer.Dirty = true;
            }
        }

        /// <summary>
        /// 判定的结论是「用到了本包但不建」时，报一次告警。
        /// </summary>
        /// <param name="collection">集合节点（去重挂在它头上：一个集合只报一次）。</param>
        /// <param name="decision">判定结论。</param>
        public static void WarnBlocked(InspectorProperty collection, ElementLayerDecision decision)
        {
            var elementType = ElementTypeOf(collection);
            var name = elementType?.Name ?? "?";

            string tail;
            switch (decision)
            {
                case ElementLayerDecision.NoContainer:
                    tail = "这个字段没有被本包接管，没有画元素行的落点。给它加 [ListDrawerSettings] " +
                           "（[Searchable] / [OnCollectionChanged] 也会顺带补一份）即可";
                    break;
                case ElementLayerDecision.Table:
                    tail = "它是表格形态（[TableList]）——表格仍按单元格画，本轮不节点化";
                    break;
                case ElementLayerDecision.Nested:
                    tail = "它在元素层里面——元素层只做一层（内层跟着递归会让节点数随外层元素个数乘性放大）";
                    break;
                default:
                    return;
            }

            DrawerWarnings.Once(collection, nameof(CollectionElementExpansion) + "." + decision,
                $"[XInspector] 属性「{collection.Path}」的元素类型「{name}」用到了本包，" +
                $"但元素里的特性不会生效：{tail}。");
        }

        /// <summary>
        /// 元素类型（含深层）里有没有**用不了**的用法；有则返回一句人话，没有返回 <c>null</c>。
        /// </summary>
        /// <param name="elementType">元素类型。</param>
        /// <returns>边界的人话描述；没有返回 <c>null</c>。</returns>
        /// <remarks>
        /// <para>
        /// 只在**会建元素层的集合**上跑一遍（构建期、深度受
        /// <see cref="NestedMemberExpansion.MaxDepth"/> 约束），报在**集合**节点上——
        /// 按元素报会把十个元素的同一件事刷十遍。
        /// </para>
        /// <para>
        /// 另一类边界（元素里的集合）由构建期的树遍历顺带报：那些集合自己在树上，
        /// 走到它们时判据给出 <see cref="ElementLayerDecision.Nested"/>。
        /// </para>
        /// </remarks>
        public static string FindUnsupportedInElement(Type elementType)
        {
            return Scan(elementType, new HashSet<Type>(), 0);
        }

        #endregion

        #region Private Helpers

        /// <summary>递归扫描：类型上的**类级**分组特性（只有它仍不生效）。</summary>
        /// <param name="type">类型。</param>
        /// <param name="visited">已访问的类型（挡环形引用）。</param>
        /// <param name="depth">当前深度。</param>
        /// <returns>边界的人话描述；没有返回 <c>null</c>。</returns>
        private static string Scan(Type type, HashSet<Type> visited, int depth)
        {
            if (type == null || depth > NestedMemberExpansion.MaxDepth || !visited.Add(type))
            {
                return null;
            }

            foreach (var attribute in type.GetCustomAttributes(true))
            {
                if (attribute is PropertyGroupAttribute group)
                {
                    return $"类级分组特性（[{group.GetType().Name}(\"{group.GroupID}\")]）" +
                           "只在被检视的最外层类型上收集";
                }
            }

            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (var field in current.GetFields(Flags))
                {
                    if (!NestedMemberExpansion.IsSerializableField(field) || IsOpaque(field.FieldType))
                    {
                        continue;
                    }

                    var found = Scan(field.FieldType, visited, depth + 1);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        /// <summary>这个类型不用再往里扫：标量与引用类型（对象引用不是内联序列化的）。</summary>
        /// <param name="type">类型。</param>
        /// <returns>不用扫返回 <c>true</c>。</returns>
        private static bool IsOpaque(Type type)
        {
            return type == null || type.IsPrimitive || type.IsEnum || type == typeof(string) ||
                   typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        #endregion
    }

    /// <summary>
    /// 一个集合的元素层状态，挂在集合节点的 <see cref="PropertyState"/> 上。
    /// </summary>
    /// <remarks>
    /// <b>只在构建期与重建期写，绘制期只读。</b> 绘制器判「要不要走元素路径」的判据是
    /// <c>State.Get&lt;CollectionElementLayerState&gt;() != null</c>——**绝不能用
    /// <c>GetOrCreate</c>**：那会让每个集合都长出一份空层，`List&lt;int&gt;` 也会被当成
    /// 「0 个元素的层」而卷进对账。
    /// </remarks>
    internal sealed class CollectionElementLayerState
    {
        /// <summary>元素节点，下标与 <c>Array.data[i]</c> 一一对应。</summary>
        public readonly List<InspectorProperty> Nodes = new List<InspectorProperty>();

        /// <summary>
        /// 结构被改过（增删**真的落地**）的标记，是重建判据之一。
        /// </summary>
        /// <remarks>
        /// 只看「节点数 != arraySize」其实已经够——元素节点是**按位置**的投影，
        /// 长度不变时每个下标仍指向它该指的数据。这条标记防的是另一件事：
        /// **增删之后旧节点里的 <c>SerializedProperty</c> 句柄不再可信**
        /// （本仓在集合绘制器的趟末纪律里记过这条），哪怕净长度恰好没变
        /// （回调里又加了回去之类），也该换一批新句柄。
        /// </remarks>
        public bool Dirty;
    }
}
