using System;
using System.Reflection;
using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 多态引用（<c>[SerializeReference]</c>）进管线的支线：解析它的**具体类型**，
    /// 并给出「能不能再往里展开」的判据。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是本包**第一次按运行时类型解析**：在那之前，成员一律按**声明类型**（字段的
    /// <c>FieldType</c>、容器的 <c>Type</c>）解析。多态引用的声明类型常常是接口或抽象类，
    /// 它自己可能一个字段都没有——真正决定「里面有什么」的是**装在槽位里的那个实例**。
    /// </para>
    /// <para>
    /// <b>为什么不用 <c>managedReferenceFullTypename</c> 反解类型。</b> 那个字符串是 Unity 的
    /// 内部拼法（形如 <c>程序集名 命名空间.类型名</c>，见 2026-10-07 的测量用例，且**刻意不钉格式**），
    /// 拿它去 <c>Type.GetType</c> 得自己拼限定名——那是一个无谓的脆弱点。字符串只适合用来
    /// **比变化**，不适合用来**取类型**；取类型走 <c>managedReferenceValue</c>（它给的是活实例）。
    /// </para>
    /// </remarks>
    internal static class PolymorphicReference
    {
        #region Constants

        /// <summary>
        /// 多态引用的嵌套层数上限（本包自定值）。
        /// </summary>
        /// <remarks>
        /// 数值与 <see cref="NestedMemberExpansion.MaxDepth"/> 相同，但**刻度不同**：
        /// 那个数的是「类型下钻的层数」，这个数的是「多态层祖先的个数」。两者刻意分开写，
        /// 免得日后调了其中一个、另一个跟着悄悄变（元素层也有一条同样独立的上限）。
        /// </remarks>
        internal const int MaxDepth = 4;

        #endregion

        #region Concrete Type

        /// <summary>
        /// 取多态字段当前实例的**具体类型**。
        /// </summary>
        /// <param name="property">这个多态字段的序列化属性。</param>
        /// <returns>具体类型；不是多态字段、没有实例、或**多选混合态**时返回 <c>null</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>混合态返回 <c>null</c> 是刻意的。</b> 2026-10-07 的测量用例钉住了两件事：
        /// <c>hasMultipleDifferentValues</c> 在「同类型、只是两个实例」时就已经为真，
        /// 故它区分不出「各目标的类型是否一致」；而 <c>managedReferenceValue</c> 在混合态下
        /// 给的是**主目标那个实例**。拿它建树就是「拿一个目标冒充全体」——本包在
        /// 「多选值不一致显示『—』」那条纪律上已经拒绝过一次这个选择，这里同理。
        /// </para>
        /// <para>
        /// 返回 <c>null</c> 的后果是**不展开**（整份交回 Unity 原生绘制），而不是报错：
        /// 多选是常规操作，与「多选下不增删元素」同款——保守处理、不打扰。
        /// </para>
        /// </remarks>
        public static Type ResolveConcreteType(SerializedProperty property)
        {
            if (property == null ||
                property.propertyType != SerializedPropertyType.ManagedReference ||
                property.hasMultipleDifferentValues)
            {
                return null;
            }

            return property.managedReferenceValue?.GetType();
        }

        #endregion

        #region Guard

        /// <summary>
        /// 往下展开会撞上的两道守卫：**类型链重复**与**深度超限**。
        /// </summary>
        /// <param name="node">待判定的多态成员节点（用它的父链）。</param>
        /// <param name="concreteType">它的具体类型。</param>
        /// <returns>撞上哪一道；都不撞返回 <c>null</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>这道刹车是必须的，不是保险。</b> 2026-10-07 的测量用例证明了：自引用的多态子树
        /// 在 <c>NextVisible</c> 下**是无限的**——环没有被 Unity 切断，回边照样有可见子级，
        /// 枚举会一直走下去。没有这道闸，展开就是构建期的无限递归。
        /// </para>
        /// <para>
        /// 两道各管一头，与元素层那两条同款：**类型链重复**挡「无限」（自己套自己、
        /// 两个类型互相套），**深度预算**挡「合法但过大」的类型链。
        /// </para>
        /// <para>
        /// 只数**多态祖先**（<see cref="InspectorPropertyKind.Member"/> 且成员是
        /// <c>[SerializeReference]</c> 字段）：中间的复合层级、分组节点都不进这条链，
        /// 与元素层「深度只数带层状态的祖先」是同一条口径。
        /// </para>
        /// </remarks>
        public static PolymorphicBlock? NestingBlock(InspectorProperty node, Type concreteType)
        {
            var depth = 0;

            for (var current = node?.Parent; current != null; current = current.Parent)
            {
                if (current.Kind != InspectorPropertyKind.Member)
                {
                    continue;
                }

                if (!NestedMemberExpansion.IsPolymorphicReference(current.Member as FieldInfo))
                {
                    continue;
                }

                depth++;

                // 祖先的 Type 就是它当时的具体类型（见 InspectorProperty.Type 的契约）。
                if (concreteType != null && current.Type == concreteType)
                {
                    return PolymorphicBlock.RepeatedConcreteType;
                }
            }

            return depth >= MaxDepth ? PolymorphicBlock.TooDeep : (PolymorphicBlock?)null;
        }

        #endregion

        #region Warning Text

        /// <summary>
        /// 被守卫挡下时的告警文案。
        /// </summary>
        /// <param name="property">被挡的成员节点。</param>
        /// <param name="block">撞上的那一道。</param>
        /// <returns>完整的告警文本（含 <c>[XInspector]</c> 前缀）。</returns>
        public static string BlockedWarning(InspectorProperty property, PolymorphicBlock block)
        {
            var path = property?.Path ?? "?";
            var typeName = property?.Type?.Name ?? "?";

            switch (block)
            {
                case PolymorphicBlock.RepeatedConcreteType:
                    return $"[XInspector] 属性「{path}」的具体类型 {typeName} 已经在祖先的多态引用链上" +
                           "出现过（自己套自己，或两个类型互相套）——再往里展开就是无限递归，这里不再展开。";

                case PolymorphicBlock.TooDeep:
                    return $"[XInspector] 属性「{path}」所在的多态引用深度已达上限 {MaxDepth} 层" +
                           "（本包自定值）——不再往里展开。";

                default:
                    throw new ArgumentOutOfRangeException(nameof(block), block, "未知的守卫结论。");
            }
        }

        #endregion
    }

    /// <summary>
    /// 一个**展开过的**多态引用容器的状态：它建树时用的具体类型，以及对账用的脏标记。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与 <see cref="CollectionElementLayerState"/> 同款：**有这个状态**就等于「这个节点是个
    /// 已展开的多态容器」，末端选型与对账登记都按它判。元素层那边还多一个 <c>Nodes</c> 列表
    /// （行下标 ↔ 元素），这里不需要——多态容器的子节点就是它的 <c>RawChildren</c>。
    /// </para>
    /// <para>
    /// <c>Dirty</c> 是留给「我们自己知道结构变了」的施加点，本批还没有生产者（换类型由
    /// 对账自己发现）；留着与元素层对称，免得日后有人以为漏了一条。
    /// </para>
    /// </remarks>
    internal sealed class PolymorphicLayerState
    {
        /// <summary>建树（或上次重建）时用的具体类型——对账键。</summary>
        public Type ConcreteType;

        /// <summary>结构已被我们自己改动，下次对账无条件重建。</summary>
        public bool Dirty;
    }

    /// <summary>
    /// 一个**尚未展开**的多态槽位的状态：只记「上次看到的具体类型」。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>与 <see cref="PolymorphicLayerState"/> 分开两个类，是刻意的。</b> 仓内有明文不变量
    /// 「**有层状态**就等于『这个节点是个已展开的多态容器』」——末端选型、注销、重建与用例
    /// 都吃它；给层状态加一个「已展开」标志位要动那四处，另立一个类则既有规则一条都不用改，
    /// 两个类各表达一个事实。
    /// </para>
    /// <para>
    /// 它把「类型变了没」这条每帧判据做成**一次引用比较**（<c>Type</c> 对象比较，零分配）：
    /// 槽位被赋值（本包的选择器、Unity 原生 UI、代码、撤销都算）之后，对账按**构建期同一道闸**
    /// 决定要不要立刻展开——「选了类型就出现子字段」与 Unity 原生一致。
    /// </para>
    /// </remarks>
    internal sealed class PolymorphicWatchState
    {
        /// <summary>上次看到的具体类型（登记时先填一次；对账时与当前值比）。</summary>
        public Type ObservedType;
    }

    /// <summary>多态展开被守卫挡下的两种理由。</summary>
    internal enum PolymorphicBlock
    {
        /// <summary>具体类型已经在祖先的多态引用链上出现过。</summary>
        RepeatedConcreteType,

        /// <summary>多态引用的嵌套深度达到上限。</summary>
        TooDeep,
    }
}
