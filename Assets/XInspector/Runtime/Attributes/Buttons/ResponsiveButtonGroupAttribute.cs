using System;

namespace XInspector
{
    /// <summary>
    /// 把若干 <see cref="ButtonAttribute"/> 方法排成**随可用宽度折行的按钮带**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与 <see cref="ButtonGroupAttribute"/> 的差别只有排布：后者是一行等分，本类按每个按钮的
    /// 标签宽度贪心装行，宽度不够就换行——窄的 Inspector 面板里不会把按钮挤成一条缝。
    /// </para>
    /// <para>
    /// <see cref="UniformLayout"/> 为 <c>true</c> 时所有按钮取**同一个宽度**（其中最宽的那个决定），
    /// 于是行内边界整齐；为 <c>false</c>（默认）时各按自己的标签宽度。
    /// </para>
    /// <para>
    /// 不写组名时归入默认组 <c>"_DefaultResponsiveButtonGroup"</c>——照抄 Odin，本包没有自造。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [ResponsiveButtonGroup("常用")]
    /// private void Foo() { }
    ///
    /// [ResponsiveButtonGroup("常用")]
    /// private void Bar() { }
    ///
    /// [ResponsiveButtonGroup("等宽", UniformLayout = true)]
    /// private void Baz() { }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class ResponsiveButtonGroupAttribute : PropertyGroupAttribute
    {
        #region Constants

        /// <summary>不写组名时的默认组名。取自 Odin，勿改——改了就是破坏性变更。</summary>
        internal const string DefaultGroupName = "_DefaultResponsiveButtonGroup";

        #endregion

        #region Public API

        /// <summary>
        /// 构造响应式按钮分组。
        /// </summary>
        /// <param name="group">分组路径，默认 <c>"_DefaultResponsiveButtonGroup"</c>。</param>
        /// <exception cref="ArgumentException"><paramref name="group"/> 为 null、空白或不含有效段。</exception>
        public ResponsiveButtonGroupAttribute(string group = DefaultGroupName)
            : base(group)
        {
        }

        /// <summary>
        /// 组内按钮的默认高度档位，默认 <see cref="ButtonSizes.Medium"/>。
        /// </summary>
        /// <remarks>
        /// 按钮自己带了 <see cref="ButtonAttribute.Size"/> 时以它为准。
        /// </remarks>
        public ButtonSizes DefaultButtonSize { get; set; }

        /// <summary>
        /// 是否让组内所有按钮取同一宽度（最宽者决定）。默认 <c>false</c>。
        /// </summary>
        public bool UniformLayout { get; set; }

        #endregion

        #region Internal

        /// <summary>
        /// 并入另一个同 ID 的声明：<see cref="UniformLayout"/> 取逻辑或，
        /// <see cref="DefaultButtonSize"/> 取**先出现的非默认值**。
        /// </summary>
        /// <param name="other">要并入的特性。</param>
        /// <remarks>
        /// 布尔量取或而不是「先声明者优先」：这个开关的语义就是「有人要求等宽就等宽」，
        /// 组里任一成员提出即成立，不依赖声明顺序。高度则与其它分组字段同规则。
        /// </remarks>
        protected internal override void Combine(PropertyGroupAttribute other)
        {
            base.Combine(other);

            if (!(other is ResponsiveButtonGroupAttribute group))
            {
                return;
            }

            UniformLayout |= group.UniformLayout;

            if (DefaultButtonSize == ButtonSizes.Medium && group.DefaultButtonSize != ButtonSizes.Medium)
            {
                DefaultButtonSize = group.DefaultButtonSize;
            }
        }

        #endregion
    }
}
