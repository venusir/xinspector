using System;
using XInspector.Internal;

namespace XInspector
{
    /// <summary>
    /// 按数值重排成员的显示顺序：**越小越靠前**。
    /// <para>
    /// 它影响的是**成员顺序**而不是绘制——构建期在三段成员收集之后、分组装配之前，对成员列表做
    /// 一次**稳定排序**（键是本特性的 <see cref="Order"/>，未标注者视为 <c>0</c>）。
    /// 「稳定」是关键：未标注的成员之间保持声明先后，所以一个都不标时，一切与从前逐字一致。
    /// </para>
    /// <para>
    /// <b>与分组的分工：本特性排成员，分组特性的 <c>Order</c> 排分组</b>，两者互不干涉。
    /// 一处连带后果值得知道：分组节点落在**其首个成员出现的位置**，因此把某成员排到前面时，
    /// 它所属的分组会跟着一起移动。
    /// </para>
    /// <para>
    /// <b>可以标在方法上</b>（<c>[Button]</c> 一族）：方法与字段同在成员列表里，排序对它们
    /// 同样生效。于是「按钮一律排在字段之后」从此是一条**默认**而不再是铁律——给方法一个负的
    /// <see cref="Order"/>，它就能排到字段之间。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [PropertyOrder(-1)]              // 排到最前
    /// public int health = 100;
    ///
    /// public int mana = 50;            // 未标注 = 0，未标注者之间保持声明顺序
    ///
    /// [Button, PropertyOrder(1)]       // 排到字段之后
    /// private void Reset() { }
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class PropertyOrderAttribute : Attribute, ITreeOrderingAttribute
    {
        #region Public API

        /// <summary>
        /// 以默认顺序（<c>0</c>）构造。
        /// </summary>
        public PropertyOrderAttribute()
            : this(0f)
        {
        }

        /// <summary>
        /// 以指定顺序构造。
        /// </summary>
        /// <param name="order">排序权重，越小越靠前；未标注的成员视为 <c>0</c>。</param>
        public PropertyOrderAttribute(float order)
        {
            Order = order;
        }

        /// <summary>
        /// 排序权重，越小越靠前。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>0</c> 在这里是**合法排序值**（未标注者也是 0），不是「未指定」——
        /// 与分组特性的 <c>Order</c>（0 = 未指定、取最小非零值）刻意不同：
        /// 分组的规则是为了「不与声明顺序耦合」，而成员排序本就以声明顺序为基准。
        /// </para>
        /// <para>
        /// 可写是为了兼容官方的具名实参写法：<c>[PropertyOrder(Order = -1)]</c> 与
        /// <c>[PropertyOrder(-1)]</c> 都能编译（官方那边 <c>Order</c> 是个字段）。
        /// </para>
        /// </remarks>
        public float Order { get; set; }

        #endregion
    }
}
