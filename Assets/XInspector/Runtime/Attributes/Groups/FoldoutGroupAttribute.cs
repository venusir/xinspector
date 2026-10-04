using System;

namespace XInspector
{
    /// <summary>
    /// 把成员归入一个**可折叠**的分组：收起时不画组内内容。
    /// <para>
    /// 展开状态属于**每个属性树**（存在对应节点的 <c>PropertyState</c> 上），
    /// 因此域重载、重开 Inspector 都回到 <see cref="Expanded"/> 的初值——
    /// **不跨会话持久化**是本包一贯的边界（见包 README 的「已知限制」）。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [FoldoutGroup("高级", true)]
    /// public int advanced;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class FoldoutGroupAttribute : PropertyGroupAttribute
    {
        #region Public API

        /// <summary>
        /// 以组名与初始展开状态构造。
        /// </summary>
        /// <param name="groupName">分组路径。</param>
        /// <param name="expanded">初始是否展开。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="groupName"/> 为 null、空白或不含有效段。</exception>
        public FoldoutGroupAttribute(string groupName, bool expanded, float order = 0f)
            : base(groupName, order)
        {
            Expanded = expanded;
            HasDefinedExpanded = true;
        }

        /// <summary>
        /// 以组名构造，初始**收起**。
        /// </summary>
        /// <param name="groupName">分组路径。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="groupName"/> 为 null、空白或不含有效段。</exception>
        public FoldoutGroupAttribute(string groupName, float order = 0f)
            : base(groupName, order)
        {
        }

        /// <summary>
        /// 初始是否展开，默认 <c>false</c>（收起）。
        /// </summary>
        public bool Expanded { get; set; }

        /// <summary>
        /// <see cref="Expanded"/> 是否被**显式设过**。
        /// </summary>
        /// <remarks>
        /// 布尔量无法区分「未设置」与「显式设为默认值」，合并两个声明时需要一个额外信号
        /// 才能实现「显式者优先」——这就是它存在的理由（照官方的同名属性）。
        /// </remarks>
        public bool HasDefinedExpanded { get; private set; }

        #endregion

        #region Internal

        /// <summary>
        /// 并入另一个同 ID 的声明：<see cref="Expanded"/> 取**先出现的显式值**。
        /// </summary>
        /// <param name="other">要并入的特性。</param>
        protected internal override void Combine(PropertyGroupAttribute other)
        {
            base.Combine(other);

            if (other is FoldoutGroupAttribute foldout && !HasDefinedExpanded && foldout.HasDefinedExpanded)
            {
                Expanded = foldout.Expanded;
                HasDefinedExpanded = true;
            }
        }

        #endregion
    }
}
