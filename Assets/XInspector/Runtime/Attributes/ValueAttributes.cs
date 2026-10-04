using System;

namespace XInspector
{
    /// <summary>
    /// 把成员的值画成**只读文本**，不带任何可编辑控件。
    /// <para>
    /// 它与其余值特性的性质不同：不「包住」值控件，而是**替换**它——
    /// 绘制器画完自己就结束，不再调用链上的下一个。这是绘制器链有意支持的能力
    /// （「不调用下一个就等于把内侧藏起来」），<see cref="ToggleLeftAttribute"/> 等亦然。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只支持**简单类型**（数值、字符串、bool、枚举、<c>UnityEngine.Object</c>）。
    /// 数组与嵌套结构会**退回普通绘制**——显示成什么形状没有显然的答案，
    /// 与其发明一种，不如不做。
    /// </para>
    /// <para>
    /// 与 Odin 的差异：官方另有 14 个重载，其中 8 个带 <c>TextAlignment</c>（Unity 类型，
    /// 本包 Runtime 零 Unity 依赖，**永久不做**）；<c>fontSize</c> / <c>enableRichText</c>
    /// 的重载推迟。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [DisplayAsString]
    /// public int computedId = 42;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class DisplayAsStringAttribute : Attribute
    {
        /// <summary>
        /// 以默认设置构造：文本裁到一行，不溢出。
        /// </summary>
        public DisplayAsStringAttribute()
        {
        }

        /// <summary>
        /// 以「是否允许溢出」构造。
        /// </summary>
        /// <param name="overflow">
        /// 为 <c>true</c> 时文本可以折行溢出（多行显示），
        /// 为 <c>false</c> 时裁到一行。
        /// </param>
        public DisplayAsStringAttribute(bool overflow)
        {
            Overflow = overflow;
        }

        /// <summary>
        /// 文本是否允许溢出到多行，默认 <c>false</c>（裁到一行）。
        /// </summary>
        public bool Overflow { get; }
    }

    /// <summary>
    /// 把 bool 画成**开关在左、标签在右**的复选框。
    /// <para>
    /// Unity 默认的 bool 是「标签在左、开关在右」，本特性把两者对调——
    /// 在一列复选框里，开关对齐比标签对齐更易读。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [ToggleLeft]
    /// public bool enableTracing;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ToggleLeftAttribute : Attribute
    {
    }

    /// <summary>
    /// 把数值画成进度条（可拖动改值）。
    /// <para>
    /// 只支持数值类型（<c>int</c>/<c>long</c>/<c>float</c>/<c>double</c>）；
    /// 其余类型告警并退回普通绘制。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>不钳制值。</b> 值超出 <see cref="Min"/>–<see cref="Max"/> 时进度条画到端点为止，
    /// 但不改动数据——想连数据一起钳制请配 <c>[MinValue]</c>/<c>[MaxValue]</c>。
    /// </para>
    /// <para>
    /// 与 Odin 的差异：官方另有三个 getter 形重载（<c>$</c> 表达式族，推迟）；
    /// 具名 <c>Color</c> 与 <c>ValueLabelAlignment</c> 是 Unity 类型，**永久不做**，
    /// 颜色改用构造参数 r/g/b 给。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [ProgressBar(0, 100)]
    /// public float health = 75f;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ProgressBarAttribute : Attribute
    {
        /// <summary>
        /// 以范围与填充色构造。
        /// </summary>
        /// <param name="min">进度条左端对应的值。</param>
        /// <param name="max">进度条右端对应的值，必须大于 <paramref name="min"/>。</param>
        /// <param name="r">填充色红分量（0–1），默认取自 Unity 的进度条蓝。</param>
        /// <param name="g">填充色绿分量（0–1）。</param>
        /// <param name="b">填充色蓝分量（0–1）。</param>
        /// <exception cref="ArgumentException"><paramref name="max"/> 不大于 <paramref name="min"/>。</exception>
        /// <remarks>
        /// 范围反过来几乎必然是笔误（本包不支持反向进度条），且它的表现是
        /// 「条子画得莫名其妙」——故在构造期明确报错，而不是画出一个说不清的东西。
        /// </remarks>
        public ProgressBarAttribute(double min, double max, float r = 0.15f, float g = 0.47f, float b = 0.74f)
        {
            if (max <= min)
            {
                throw new ArgumentException($"进度条的上限必须大于下限（收到 min={min}、max={max}）。", nameof(max));
            }

            Min = min;
            Max = max;
            R = r;
            G = g;
            B = b;
        }

        /// <summary>进度条左端对应的值。</summary>
        public double Min { get; }

        /// <summary>进度条右端对应的值。</summary>
        public double Max { get; }

        /// <summary>填充色红分量（0–1）。</summary>
        public float R { get; }

        /// <summary>填充色绿分量（0–1）。</summary>
        public float G { get; }

        /// <summary>填充色蓝分量（0–1）。</summary>
        public float B { get; }

        /// <summary>
        /// 进度条高度（像素）。非正值表示用默认高度（<c>EditorGUIUtility.singleLineHeight</c>）。
        /// </summary>
        public float Height { get; set; }

        /// <summary>
        /// 是否按 25% 分段显示（四段深浅交替）。默认 <c>false</c>。
        /// </summary>
        public bool Segmented { get; set; }

        /// <summary>
        /// 是否在条上显示当前值文本。默认 <c>true</c>。
        /// </summary>
        /// <remarks>
        /// 官方签名的默认值未从文档确认，<c>true</c> 是本包自定的——进度条不显示数值时，
        /// 不拖动就不知道它到底是多少。
        /// </remarks>
        public bool DrawValueLabel { get; set; } = true;
    }

    /// <summary>
    /// 把枚举画成一排按钮（工具栏），而不是下拉框。
    /// <para>
    /// 普通枚举单选；带 <c>[Flags]</c> 的枚举**逐位多选**（每按一次翻转一位）。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 只对枚举成员生效；标在其它类型上会告警并退回普通绘制。
    /// 官方对 <c>[Flags]</c> 的行为只确认「两种枚举都支持」，「多选」是本包自定的语义。
    /// </remarks>
    /// <example>
    /// <code>
    /// [EnumToggleButtons]
    /// public DamageType damageType;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EnumToggleButtonsAttribute : Attribute
    {
    }
}
