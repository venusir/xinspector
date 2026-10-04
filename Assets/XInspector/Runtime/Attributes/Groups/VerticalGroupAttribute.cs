using System;

namespace XInspector
{
    /// <summary>
    /// 把成员归入一个**竖直分组**：只有排布容器与内边距，没有边框、没有标题。
    /// <para>
    /// 与 <see cref="BoxGroupAttribute"/> 的差别是视觉：盒子画框，它不画。
    /// 主要用途是配合水平分组——「一行里的一列」需要一个不带框的容器。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [VerticalGroup("左列")]
    /// public int a;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class VerticalGroupAttribute : PropertyGroupAttribute
    {
        #region Public API

        /// <summary>
        /// 不指定分组路径时使用的默认组名（照 Odin 的取值）。
        /// </summary>
        /// <remarks>
        /// 让 <c>[VerticalGroup]</c> 这种无参写法落进同一个分组，而不是各自长出节点。
        /// </remarks>
        public const string DEFAULT_NAME = "_DefaultVerticalGroup";

        /// <summary>
        /// 默认分组构造：所有无参声明的成员归入同一个分组。
        /// </summary>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        public VerticalGroupAttribute(float order = 0f)
            : base(DEFAULT_NAME, order)
        {
        }

        /// <summary>
        /// 以分组路径构造。
        /// </summary>
        /// <param name="groupId">分组路径，如 <c>"左列"</c>。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="groupId"/> 为 null、空白或不含有效段。</exception>
        public VerticalGroupAttribute(string groupId, float order = 0f)
            : base(groupId, order)
        {
        }

        /// <summary>分组上方的内边距（像素）。非正值不画。</summary>
        public float PaddingTop { get; set; }

        /// <summary>分组下方的内边距（像素）。非正值不画。</summary>
        public float PaddingBottom { get; set; }

        #endregion

        #region Internal

        /// <summary>
        /// 并入另一个同 ID 的声明：内边距取**先出现的非零值**（与基类 Order 同一规则）。
        /// </summary>
        /// <param name="other">要并入的特性。</param>
        protected internal override void Combine(PropertyGroupAttribute other)
        {
            base.Combine(other);

            if (other is VerticalGroupAttribute vertical)
            {
                if (PaddingTop == 0f)
                {
                    PaddingTop = vertical.PaddingTop;
                }

                if (PaddingBottom == 0f)
                {
                    PaddingBottom = vertical.PaddingBottom;
                }
            }
        }

        #endregion
    }
}
