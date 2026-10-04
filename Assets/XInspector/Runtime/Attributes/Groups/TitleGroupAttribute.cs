using System;

namespace XInspector
{
    /// <summary>
    /// 标题组的对齐方式。
    /// </summary>
    /// <remarks>
    /// 官方文档站按字母序列出这四个成员，**数值未核实**——本包按该顺序从 0 起排。
    /// </remarks>
    public enum TitleAlignments
    {
        /// <summary>标题与副标题居中。</summary>
        Centered,

        /// <summary>标题与副标题左对齐。</summary>
        Left,

        /// <summary>标题与副标题右对齐。</summary>
        Right,

        /// <summary>标题在左、副标题在右，分列两端。</summary>
        Split,
    }

    /// <summary>
    /// 把成员归入一个**带标题的分组**：标题加一条分隔线，可选副标题。
    /// <para>
    /// <see cref="PropertyGroupAttribute.GroupID"/> 就是标题文本——不必再给分组起名，
    /// 写 <c>[TitleGroup("战斗属性")]</c> 即可，路径与标题是同一样东西。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [TitleGroup("战斗属性", "只影响命中率")]
    /// public int accuracy;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class TitleGroupAttribute : PropertyGroupAttribute
    {
        #region Public API

        /// <summary>
        /// 以标题构造。
        /// </summary>
        /// <param name="title">标题，同时用作分组路径（可写 <c>"外层/内层"</c> 嵌套）。</param>
        /// <param name="subtitle">副标题；为 null 或空白时不画。</param>
        /// <param name="alignment">标题与副标题的对齐方式。</param>
        /// <param name="horizontalLine">是否在标题下方画分隔线。</param>
        /// <param name="boldTitle">标题是否加粗。</param>
        /// <param name="indent">组内内容是否缩进一级。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="title"/> 为 null、空白或不含有效段。</exception>
        public TitleGroupAttribute(
            string title,
            string subtitle = null,
            TitleAlignments alignment = TitleAlignments.Left,
            bool horizontalLine = true,
            bool boldTitle = true,
            bool indent = false,
            float order = 0f)
            : base(title, order)
        {
            if (subtitle != null && string.IsNullOrWhiteSpace(subtitle))
            {
                throw new ArgumentException("副标题要么不写，要么不能是空白串。", nameof(subtitle));
            }

            Subtitle = subtitle;
            Alignment = alignment;
            HorizontalLine = horizontalLine;
            BoldTitle = boldTitle;
            Indent = indent;
        }

        /// <summary>副标题；为 <c>null</c> 时不画。</summary>
        public string Subtitle { get; private set; }

        /// <summary>标题与副标题的对齐方式。</summary>
        public TitleAlignments Alignment { get; private set; }

        /// <summary>是否在标题下方画分隔线。</summary>
        public bool HorizontalLine { get; private set; }

        /// <summary>标题是否加粗。</summary>
        public bool BoldTitle { get; private set; }

        /// <summary>组内内容是否缩进一级。</summary>
        public bool Indent { get; private set; }

        #endregion

        #region Internal

        /// <summary>
        /// 并入另一个同 ID 的声明：呈现设定取**先声明者**的值，不累加、不覆盖。
        /// </summary>
        /// <param name="other">要并入的特性。</param>
        /// <remarks>
        /// 与 <see cref="BoxGroupAttribute.Combine"/> 同一条规则——「给分组多加一个字段」
        /// 不该意外改变分组的标题与外观。
        /// </remarks>
        protected internal override void Combine(PropertyGroupAttribute other)
        {
            base.Combine(other);

            if (!(other is TitleGroupAttribute title))
            {
                return;
            }

            if (string.IsNullOrEmpty(Subtitle))
            {
                Subtitle = title.Subtitle;
            }
        }

        #endregion
    }
}
