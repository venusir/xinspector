using System;

namespace XInspector
{
    /// <summary>
    /// 把成员排成**一行**：每个成员按自己的 <see cref="Width"/> 分数分到一格的宽度。
    /// <para>
    /// 分数语义：显式分数之和 ≤ 1 时，未指定的成员**均分剩余**；之和大于 1 时按总和
    /// 等比缩放（确定性，不溢出）。未指定、非正或 NaN 都视为「未指定」。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 可用宽度取自当前布局组的实际内宽（不是窗口宽度），因此嵌套在框、缩进里也准。
    /// 但它是**近似**：单元格宽度按分数算好后交给 Unity 的布局，极端窄的窗口下仍可能挤压。
    /// </para>
    /// <para>
    /// 只有**直接成员**拿得到分数宽度；水平组里再套的分组节点按自然宽度排。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [HorizontalGroup("一行", 0.7f)]
    /// public int wide;
    ///
    /// [HorizontalGroup("一行", 0.3f)]
    /// public int narrow;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class HorizontalGroupAttribute : PropertyGroupAttribute
    {
        #region Public API

        /// <summary>
        /// 不指定分组路径时使用的默认组名（照 <see cref="VerticalGroupAttribute.DEFAULT_NAME"/>
        /// 的同款命名；官方对 HorizontalGroup 的默认名未从文档核实）。
        /// </summary>
        public const string DEFAULT_NAME = "_DefaultHorizontalGroup";

        /// <summary>
        /// 默认分组构造：所有无参声明的成员排进同一行。
        /// </summary>
        /// <param name="width">本成员占一行的宽度分数（0–1）；非正值表示「未指定」。</param>
        /// <param name="marginLeft">行左侧外边距（像素）。</param>
        /// <param name="marginRight">行右侧外边距（像素）。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        public HorizontalGroupAttribute(float width = 0f, int marginLeft = 0, int marginRight = 0, float order = 0f)
            : base(DEFAULT_NAME, order)
        {
            Initialize(width, marginLeft, marginRight);
        }

        /// <summary>
        /// 以分组路径构造。
        /// </summary>
        /// <param name="group">分组路径。</param>
        /// <param name="width">本成员占一行的宽度分数（0–1）；非正值表示「未指定」。</param>
        /// <param name="marginLeft">行左侧外边距（像素）。</param>
        /// <param name="marginRight">行右侧外边距（像素）。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="group"/> 为 null、空白或不含有效段。</exception>
        public HorizontalGroupAttribute(string group, float width = 0f, int marginLeft = 0, int marginRight = 0, float order = 0f)
            : base(group, order)
        {
            Initialize(width, marginLeft, marginRight);
        }

        /// <summary>本成员占一行的宽度分数（0–1）；非正值表示「未指定」（均分剩余）。</summary>
        public float Width { get; set; }

        /// <summary>行上格子之间的间距（像素）。默认 4。</summary>
        /// <remarks>官方默认值未核实，4 是本包自定的——不留间距时相邻字段会贴在一起。</remarks>
        public float Gap { get; set; } = 4f;

        /// <summary>行左侧外边距（像素）。非正值不画。</summary>
        public float MarginLeft { get; set; }

        /// <summary>行右侧外边距（像素）。非正值不画。</summary>
        public float MarginRight { get; set; }

        /// <summary>行内左侧内边距（像素）。非正值不画。</summary>
        public float PaddingLeft { get; set; }

        /// <summary>行内右侧内边距（像素）。非正值不画。</summary>
        public float PaddingRight { get; set; }

        /// <summary>格子的最小宽度（像素）。非正值不限制。</summary>
        public float MinWidth { get; set; }

        /// <summary>格子的最大宽度（像素）。非正值不限制。</summary>
        public float MaxWidth { get; set; }

        /// <summary>画在整行上方的标题。为 null 或空白时不画。</summary>
        public string Title { get; set; }

        /// <summary>格内标签宽度（像素）。非正值表示按格子宽度自动折算。</summary>
        public float LabelWidth { get; set; }

        /// <summary>
        /// 关掉「按格子宽度自动折算标签宽度」。默认 <c>false</c>。
        /// </summary>
        /// <remarks>
        /// 自动折算是为了让窄格子里的标签别吃掉整格（Inspector 宿主按**整页宽**设的
        /// <c>labelWidth</c> 放进 1/3 宽的格子会把值控件挤成一条缝）。关掉它则沿用当前值。
        /// </remarks>
        public bool DisableAutomaticLabelWidth { get; set; }

        #endregion

        #region Internal

        /// <summary>
        /// 并入另一个同 ID 的声明：呈现设定取**先出现的非零/非空值**，与基类 Order 同一规则。
        /// </summary>
        /// <param name="other">要并入的特性。</param>
        protected internal override void Combine(PropertyGroupAttribute other)
        {
            base.Combine(other);

            if (!(other is HorizontalGroupAttribute horizontal))
            {
                return;
            }

            if (Width == 0f)
            {
                Width = horizontal.Width;
            }

            if (Gap == 0f)
            {
                Gap = horizontal.Gap;
            }

            if (MarginLeft == 0f)
            {
                MarginLeft = horizontal.MarginLeft;
            }

            if (MarginRight == 0f)
            {
                MarginRight = horizontal.MarginRight;
            }

            if (PaddingLeft == 0f)
            {
                PaddingLeft = horizontal.PaddingLeft;
            }

            if (PaddingRight == 0f)
            {
                PaddingRight = horizontal.PaddingRight;
            }

            if (MinWidth == 0f)
            {
                MinWidth = horizontal.MinWidth;
            }

            if (MaxWidth == 0f)
            {
                MaxWidth = horizontal.MaxWidth;
            }

            if (LabelWidth == 0f)
            {
                LabelWidth = horizontal.LabelWidth;
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                Title = horizontal.Title;
            }

            if (!DisableAutomaticLabelWidth)
            {
                DisableAutomaticLabelWidth = horizontal.DisableAutomaticLabelWidth;
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>把两个构造共用的赋值收在一处。</summary>
        /// <param name="width">宽度分数。</param>
        /// <param name="marginLeft">左外边距。</param>
        /// <param name="marginRight">右外边距。</param>
        private void Initialize(float width, int marginLeft, int marginRight)
        {
            Width = width;
            MarginLeft = marginLeft;
            MarginRight = marginRight;
        }

        #endregion
    }
}
