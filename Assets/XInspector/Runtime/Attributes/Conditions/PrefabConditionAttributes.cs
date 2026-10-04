using System;

namespace XInspector
{
    // 预制体上下文条件族：按「被检视对象处在哪种预制体上下文」决定成员的可见性与可编辑性。
    //
    // 形态与 ModeAttributes（[HideInEditorMode] 那一组）**逐字同构**：Runtime 侧是薄数据类，
    // 判据在编辑器侧的处理器里（Runtime 零 Unity 依赖，碰不到 PrefabUtility）。
    // 四个都允许标在方法上：与 [Button] 配合时「只在预制体资产上显示这个按钮」是最常见的用法。
    // 判据与 ConditionalAttributes 那四个相同——方法会产生属性树节点，普通属性不会。

    /// <summary>
    /// 只在指定的**预制体上下文**里显示本成员。
    /// <para>
    /// <see cref="PrefabKind"/> 是位标志，多个上下文可以用 <c>|</c> 组合；
    /// 匹配规则是求交集，多选时**所有目标都要匹配**才显示（见 <see cref="PrefabKind"/> 的说明）。
    /// </para>
    /// <para>
    /// <b>「非预制体」也占一位</b>：<see cref="PrefabKind.NonPrefabInstance"/> 指场景里不属于任何
    /// 预制体的对象——那是最常见的情形，别把它当成「没有上下文」。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [ShowIn(PrefabKind.PrefabAsset)]
    /// public string onlyOnPrefabAssets;
    ///
    /// [ShowIn(PrefabKind.InstanceInScene | PrefabKind.InstanceInPrefab)]
    /// public int onlyOnInstances;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class ShowInAttribute : Attribute
    {
        /// <summary>
        /// 以预制体上下文构造。
        /// </summary>
        /// <param name="prefabKind">要求目标所处的上下文。传 <see cref="PrefabKind.None"/> 合法，
        /// 但恒不匹配（即恒隐藏）。</param>
        public ShowInAttribute(PrefabKind prefabKind)
        {
            PrefabKind = prefabKind;
        }

        /// <summary>
        /// 要求目标所处的上下文。
        /// </summary>
        public PrefabKind PrefabKind { get; }
    }

    /// <summary>
    /// 在指定的**预制体上下文**里隐藏本成员。语义是 <see cref="ShowInAttribute"/> 的取反。
    /// </summary>
    /// <example>
    /// <code>
    /// [HideIn(PrefabKind.PrefabAsset)]
    /// public string notOnPrefabAssets;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class HideInAttribute : Attribute
    {
        /// <summary>
        /// 以预制体上下文构造。
        /// </summary>
        /// <param name="prefabKind">要求隐藏的上下文。传 <see cref="PrefabKind.None"/> 合法，
        /// 但恒不匹配（即什么都不隐藏）。</param>
        public HideInAttribute(PrefabKind prefabKind)
        {
            PrefabKind = prefabKind;
        }

        /// <summary>
        /// 要求隐藏的上下文。
        /// </summary>
        public PrefabKind PrefabKind { get; }
    }

    /// <summary>
    /// 只在指定的**预制体上下文**里可编辑（其余上下文里变灰但仍可见）。
    /// </summary>
    /// <remarks>
    /// 与隐藏的差别是既有的那条：禁用保留了「这个字段存在、只是现在不能改」的信息。
    /// 想表达「预制体资产上是模板、实例上才调」时，用禁用比用隐藏更好读。
    /// </remarks>
    /// <example>
    /// <code>
    /// [EnableIn(PrefabKind.InstanceInScene)]
    /// public float tuning;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class EnableInAttribute : Attribute
    {
        /// <summary>
        /// 以预制体上下文构造。
        /// </summary>
        /// <param name="prefabKind">要求可编辑的上下文。传 <see cref="PrefabKind.None"/> 合法，
        /// 但恒不匹配（即恒不可编辑）。</param>
        public EnableInAttribute(PrefabKind prefabKind)
        {
            PrefabKind = prefabKind;
        }

        /// <summary>
        /// 要求可编辑的上下文。
        /// </summary>
        public PrefabKind PrefabKind { get; }
    }

    /// <summary>
    /// 在指定的**预制体上下文**里禁用本成员（变灰但仍可见）。语义是
    /// <see cref="EnableInAttribute"/> 的取反。
    /// </summary>
    /// <example>
    /// <code>
    /// [DisableIn(PrefabKind.PrefabAsset)]
    /// public float tuning;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class DisableInAttribute : Attribute
    {
        /// <summary>
        /// 以预制体上下文构造。
        /// </summary>
        /// <param name="prefabKind">要求禁用的上下文。传 <see cref="PrefabKind.None"/> 合法，
        /// 但恒不匹配（即什么都不禁用）。</param>
        public DisableInAttribute(PrefabKind prefabKind)
        {
            PrefabKind = prefabKind;
        }

        /// <summary>
        /// 要求禁用的上下文。
        /// </summary>
        public PrefabKind PrefabKind { get; }
    }
}
