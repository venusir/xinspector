using System;
using System.Collections.Generic;
using System.Text;
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
    /// <b>元素层可以递归（深度 &gt; 1），但有**两道守卫**。</b> 元素**里面**的集合也按需
    /// 节点化，两道守卫各挡一类：
    /// </para>
    /// <list type="bullet">
    /// <item><b>类型链去重</b>：候选集合的元素类型已出现在祖先元素层的元素类型链上
    /// （自己套自己、或两个类型互相套）→ 不建层——再展开就是无限递归；</item>
    /// <item><b>层数预算</b>（<see cref="MaxElementLayerDepth"/>，本包自定值）：带层状态的祖先数
    /// 达到上限 → 不建层。</item>
    /// </list>
    /// <para>
    /// 两道都是**响亮拒绝**（构建期告警），不是静默截断。注意预算挡的是**类型链**，
    /// **不挡数据规模**：每层元素个数不受限，4 层 × 每层 N 个仍是乘性放大——
    /// 那是「用户在每一层都显式写了容器与特性」的 opt-in 代价。
    /// </para>
    /// <para>
    /// <b>判据看不见的展开是静默，看得见却不发生的展开也必须是响的。</b>
    /// 用到了本包却被挡住（没容器 / 表格形态 / 两道守卫）时一律构建期告警一次；
    /// 真的没用本包的什么都不说（那是「外观不变」的正常路径）。
    /// </para>
    /// </remarks>
    internal static class CollectionElementExpansion
    {
        #region Private Fields

        /// <summary>元素层的层数上限（本包自定值，与三处 <c>MaxDepth</c> 同档）。</summary>
        /// <remarks>
        /// 它挡的是「**合法但过大**」的类型链；自引用由类型链去重挡
        /// （<see cref="ElementLayerDecision.RepeatedElementType"/>）。它**不挡数据规模**：
        /// 每层元素个数不受限，4 层 × 每层 N 个仍是乘性放大。
        /// </remarks>
        internal const int MaxElementLayerDepth = 4;

        #endregion

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

            /// <summary>用到了，但元素类型已在祖先元素层的类型链上出现过——再展开就是无限递归。</summary>
            RepeatedElementType,

            /// <summary>用到了，但元素层祖先数已达上限（<see cref="MaxElementLayerDepth"/>）。</summary>
            TooDeep,
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

            // 表格形态不节点化：单元格画法逐字不变（那是另一种容器呈现，不是元素层）。
            if (collection.Attributes.Has<TableListAttribute>())
            {
                return ElementLayerDecision.Table;
            }

            // 容器项：没有自绘容器就没有画元素行的落点（元素节点建了也没人画）。
            // **排在两道守卫之前**：内层集合「既在元素层里、又没写容器」是常态，
            // 报「加 [ListDrawerSettings]」才是可行动的（两守卫同时成立时先报它，
            // 用户照做后会看到第二条——两步揭示，接受）。
            if (!collection.Attributes.Has<ListDrawerSettingsAttribute>())
            {
                return ElementLayerDecision.NoContainer;
            }

            // 两道守卫（自引用 / 超预算）：沿父链一次走完，详见 LayerNestingBlock。
            var block = LayerNestingBlock(collection, elementType);
            if (block.HasValue)
            {
                return block.Value;
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

        /// <summary>
        /// 元素层的两道守卫：沿父链**一次**走完——数出带层状态的祖先（元素层深度），
        /// 并逐个比对元素类型（类型链去重）。
        /// </summary>
        /// <param name="collection">集合节点。</param>
        /// <param name="elementType">它的元素类型。</param>
        /// <returns>该被挡时返回对应结论；可以通过时返回 <c>null</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>类型链 = 祖先<em>元素层</em>的元素类型</b>，不是「所有祖先节点的类型」——
        /// 中间那些复合层级（<c>A</c> 里嵌着 <c>B</c>）不进链，链上只有**真的建了元素层**的
        /// 那些集合的元素类型。自引用（<c>Node{List&lt;Node&gt;}</c>）与互递归
        /// （<c>A{B} B{List&lt;A&gt;}</c>）都在类型重现的那一层被挡——再展开就是无限递归。
        /// </para>
        /// <para>
        /// <b>深度数的是带层状态的祖先数</b>，与 <c>NestedMemberExpansion.MaxDepth</c>
        /// （类型下钻）不是一个刻度；索引段也不在这里计数。
        /// </para>
        /// </remarks>
        private static ElementLayerDecision? LayerNestingBlock(InspectorProperty collection, Type elementType)
        {
            var depth = 0;

            for (var current = collection?.Parent; current != null; current = current.Parent)
            {
                if (current.State.Get<CollectionElementLayerState>() == null)
                {
                    continue;
                }

                depth++;

                if (elementType != null && ElementTypeOf(current) == elementType)
                {
                    return ElementLayerDecision.RepeatedElementType;
                }
            }

            return depth >= MaxElementLayerDepth ? ElementLayerDecision.TooDeep : (ElementLayerDecision?)null;
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
        /// <param name="collection">集合节点。</param>
        /// <param name="decision">判定结论。</param>
        /// <remarks>
        /// <para>
        /// <b>收件人上溯到最外层的元素层。</b> 被挡的内层集合在 N 个外层元素里各有一个节点，
        /// 报在自己头上就是 N 条只差路径的同一句话；锚到**最外层**带层状态的祖先
        /// （「这一族被挡集合」的共同宿主，且它在外层自己的重建中存活），账本才会只有一份。
        /// 没有这样的祖先（深度 0 的被挡集合）时锚是自己——行为与从前逐字一致。
        /// </para>
        /// <para>
        /// <b>键里带归一化路径</b>（<c>data[3]</c> → <c>data[*]</c>）：同一字段的 N 个元素实例
        /// 落同一个键、报一条；同一外层下的不同字段仍各报一条。文案里仍报**真实路径**。
        /// </para>
        /// </remarks>
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
                case ElementLayerDecision.RepeatedElementType:
                    tail = "它的元素类型已经在祖先元素层的类型链上出现过（自己套自己，或两个类型" +
                           "互相套）——再往里展开就是无限递归，这里不再建层";
                    break;
                case ElementLayerDecision.TooDeep:
                    tail = $"它所在的元素层深度已达上限 {MaxElementLayerDepth} 层（本包自定值，" +
                           "与 MaxDepth 同档）——再展开会让节点数随元素个数乘性膨胀";
                    break;
                case ElementLayerDecision.Build:
                case ElementLayerDecision.Inert:
                    return;

                // **不要改成 `default: return;`**——那会把将来新增的枚举成员静默吞掉
                //（本仓最忌讳的形态）。这条 default 永远不该被走到；抛在这里，新成员
                // 第一趟建树就会响亮撞上，用例也钉得住。
                //
                // （曾经想用「不加 default → 变量定值分析报 CS0165」当编译期陷阱：
                // 不成立——C# 把没有 default 的 switch 一律当作**非穷尽**，于是 switch 之后
                // 使用该变量**恒**报错，代码根本编译不过。）
                default:
                    throw new ArgumentOutOfRangeException(nameof(decision), decision, "未处理的元素层判定结论。");
            }

            var anchor = OutermostElementLayer(collection) ?? collection;
            var key = nameof(CollectionElementExpansion) + "." + decision + "|" +
                      NormalizeElementIndexes(collection.Path);

            DrawerWarnings.Once(anchor, key,
                $"[XInspector] 属性「{collection.Path}」的元素类型「{name}」用到了本包，" +
                $"但元素里的特性不会生效：{tail}。");
        }

        /// <summary>沿父链找**最外层**带元素层状态的祖先；没有返回 <c>null</c>。</summary>
        /// <param name="node">起点节点。</param>
        /// <returns>最外层的那个集合节点；没有返回 <c>null</c>。</returns>
        private static InspectorProperty OutermostElementLayer(InspectorProperty node)
        {
            var outermost = (InspectorProperty)null;

            for (var current = node?.Parent; current != null; current = current.Parent)
            {
                if (current.State.Get<CollectionElementLayerState>() != null)
                {
                    outermost = current;
                }
            }

            return outermost;
        }

        /// <summary>把路径里的元素下标归一化：<c>data[3]</c> → <c>data[*]</c>。</summary>
        /// <param name="path">路径。</param>
        /// <returns>归一化后的路径。</returns>
        /// <remarks>
        /// 只用于**告警去重的键**（文案里仍报真实路径）。构建 / 重建期跑，
        /// 不在绘制路径上；<c>data[</c> 形态之外的一律原样保留。
        /// </remarks>
        private static string NormalizeElementIndexes(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(path.Length);
            var start = 0;

            while (start < path.Length)
            {
                var index = path.IndexOf("data[", start, StringComparison.Ordinal);
                if (index < 0)
                {
                    builder.Append(path, start, path.Length - start);
                    break;
                }

                var close = path.IndexOf(']', index + 5);
                if (close < 0)
                {
                    builder.Append(path, start, path.Length - start);
                    break;
                }

                builder.Append(path, start, index - start);
                builder.Append("data[*]");
                start = close + 1;
            }

            return builder.ToString();
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
