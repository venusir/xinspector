using System;

namespace XInspector
{
    /// <summary>
    /// 把若干 <see cref="ButtonAttribute"/> 方法并排成**一行按钮**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 它是分组特性：同名（<see cref="PropertyGroupAttribute.GroupID"/>）的按钮归入同一个分组节点，
    /// 由分组绘制器排成一行、每格等宽伸展。不写组名时归入默认组
    /// <c>"_DefaultGroup"</c>——这个名字照抄 Odin，本包没有自造。
    /// </para>
    /// <para>
    /// 与其它分组一样支持路径嵌套（<c>"Outer/Inner"</c>），故按钮组可以嵌进
    /// <c>[BoxGroup]</c>／<c>[TabGroup]</c> 等分组里。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异：</b>不声明 <c>ButtonAlignment</c>／<c>Stretch</c>／<c>IconAlignment</c>
    /// ——前两个是逐按钮的对齐微调（本包等分排布），后者随图标一族一并否决。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [ButtonGroup]
    /// private void A() { }
    ///
    /// [ButtonGroup]
    /// private void B() { }
    ///
    /// [ButtonGroup("危险操作", ButtonHeight = 30)]
    /// private void DeleteAll() { }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class ButtonGroupAttribute : PropertyGroupAttribute
    {
        #region Constants

        /// <summary>不写组名时的默认组名。取自 Odin，勿改——改了就是破坏性变更。</summary>
        internal const string DefaultGroupName = "_DefaultGroup";

        #endregion

        #region Public API

        /// <summary>
        /// 构造按钮分组。
        /// </summary>
        /// <param name="group">分组路径，默认 <c>"_DefaultGroup"</c>。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="group"/> 为 null、空白或不含有效段。</exception>
        public ButtonGroupAttribute(string group = DefaultGroupName, float order = 0f)
            : base(group, order)
        {
        }

        /// <summary>
        /// 本组按钮的高度（像素）。非正值表示**用各按钮自己的档位**（默认）。
        /// </summary>
        /// <remarks>
        /// 组级值只是个默认：按钮自己带了 <see cref="ButtonAttribute.Size"/> 时以它为准。
        /// </remarks>
        public int ButtonHeight { get; set; }

        #endregion

        #region Internal

        /// <summary>
        /// 并入另一个同 ID 的声明：<see cref="ButtonHeight"/> 取**先出现的非零值**。
        /// </summary>
        /// <param name="other">要并入的特性。</param>
        /// <remarks>
        /// 与基类对 <c>Order</c> 的处理一致：不做累加也不做「后者覆盖」——
        /// 那会让「给分组多加一个按钮」意外改掉整组的高度。
        /// </remarks>
        protected internal override void Combine(PropertyGroupAttribute other)
        {
            base.Combine(other);

            if (ButtonHeight <= 0 && other is ButtonGroupAttribute group)
            {
                ButtonHeight = group.ButtonHeight;
            }
        }

        #endregion
    }
}
