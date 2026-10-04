using System;

namespace XInspector
{
    /// <summary>
    /// 把字符串画成**多行文本域**（而不是单行输入框），行数可调。
    /// </summary>
    /// <remarks>
    /// 只对字符串生效；其它类型告警并退回普通绘制。
    /// 与 Unity 原生 <c>[TextArea]</c> 的差别：行数由参数给定，且它是**替换型**
    /// （不调用下一个绘制器），可与 <c>[GUIColor]</c> <c>[Indent]</c> 等照常叠加。
    /// </remarks>
    /// <example>
    /// <code>
    /// [MultiLineProperty(5)]
    /// public string notes;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class MultiLinePropertyAttribute : Attribute
    {
        /// <summary>
        /// 以行数构造。
        /// </summary>
        /// <param name="lines">文本域的行数，默认 3。非正值按 1 行处理。</param>
        public MultiLinePropertyAttribute(int lines = 3)
        {
            Lines = lines;
        }

        /// <summary>文本域的行数。</summary>
        public int Lines { get; }
    }

    /// <summary>
    /// 把值画成**延迟提交**的控件：拖动或输入后不立即写回，
    /// 直到按下回车或焦点离开才提交。
    /// <para>
    /// 适合「每改一次都会触发重算」的字段——普通控件每敲一个字符就写一次，
    /// 延迟控件只写最终值。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 支持 <c>int</c>、<c>float</c>、<c>double</c>、<c>string</c>。
    /// <c>long</c> 字段在值超出 <c>int</c> 范围时**告警并退回普通绘制**——
    /// 延迟控件只有 <c>int</c> 版本，硬用会把超出部分悄悄截掉。
    /// </remarks>
    /// <example>
    /// <code>
    /// [DelayedProperty]
    /// public string searchFilter;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class DelayedPropertyAttribute : Attribute
    {
    }

    /// <summary>
    /// 给枚举加上「上一项 / 下一项」翻页按钮。
    /// <para>
    /// 枚举项很多时，下拉框要展开再找；翻页按钮适合「按顺序试」的调参场景
    /// （在末尾会自动绕回开头）。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 只对枚举生效；<c>[Flags]</c> 位标志枚举**没有顺序语义**，会告警并退回普通绘制。
    /// </remarks>
    /// <example>
    /// <code>
    /// [EnumPaging]
    /// public DamageType damageType;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EnumPagingAttribute : Attribute
    {
    }

    /// <summary>
    /// 把数值画成**滑块**，取值被限制在给定范围内。
    /// <para>
    /// 可以理解成 Unity 原生 <c>[Range]</c> 的属性化版本；与它并存的
    /// <c>[MinValue]</c>/<c>[MaxValue]</c> 是另一件事（那两个在绘制后钳制数据，
    /// 本特性只换控件）。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只支持数值类型。多对象编辑且各目标值不一致时**退回普通绘制**——
    /// 滑块没有「混合值」这个显示形态，硬画会用第一个目标的值冒充（Unity 自己会显示「—」）。
    /// </para>
    /// <para>
    /// 与 Odin 的差异：官方另有三个 getter 形重载（<c>$</c> 表达式族，推迟）；
    /// <see cref="MinGetter"/> / <see cref="MaxGetter"/> 照官方保留，未使用时为 <c>null</c>。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [PropertyRange(0, 100)]
    /// public float healthPercent;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class PropertyRangeAttribute : Attribute
    {
        /// <summary>
        /// 以范围构造。
        /// </summary>
        /// <param name="min">滑块左端的值。</param>
        /// <param name="max">滑块右端的值，必须大于 <paramref name="min"/>。</param>
        /// <exception cref="ArgumentException"><paramref name="max"/> 不大于 <paramref name="min"/>。</exception>
        public PropertyRangeAttribute(double min, double max)
        {
            if (max <= min)
            {
                throw new ArgumentException($"滑块的上限必须大于下限（收到 min={min}、max={max}）。", nameof(max));
            }

            Min = min;
            Max = max;
        }

        /// <summary>滑块左端的值。</summary>
        public double Min { get; }

        /// <summary>滑块右端的值。</summary>
        public double Max { get; }

        /// <summary>
        /// 下限的取法表达式。**本包不做 <c>$</c> 表达式族，恒为 <c>null</c>**——
        /// 保留它只是为了让照着 Odin 写的调用代码编译得过。
        /// </summary>
        public string MinGetter { get; }

        /// <summary>
        /// 上限的取法表达式。**本包不做 <c>$</c> 表达式族，恒为 <c>null</c>**。
        /// </summary>
        public string MaxGetter { get; }
    }

    /// <summary>
    /// 把 <c>Vector2</c> 画成**双滑块**：<c>x</c> 是下限、<c>y</c> 是上限。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只对 <c>Vector2</c> 生效。<c>Vector2Int</c> **不做**——值后端
    /// （<c>SerializedPropertyValueEntry</c>）支持的类型里没有它，绕开值入口直读
    /// <c>SP.vector2IntValue</c> 会让本特性成为唯一一个不走值后端的值绘制器，
    /// 为半个类型破一条架构缝不值得。
    /// </para>
    /// <para>
    /// <b>边界可以是字面量，也可以是序列化成员名。</b> Odin 那边的字符串参数是
    /// resolved string（支持 <c>@</c> 表达式与方法调用），本包只认**序列化成员名**——
    /// 与条件族、<c>[InfoBox].visibleIf</c> 同一条边界。
    /// </para>
    /// <para>
    /// 五组构造与官方一致。其中 <see cref="MinMaxValueGetter"/> **优先于**其余四个
    /// （官方原文：non-null 时覆盖它们的行为）。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [MinMaxSlider(0f, 100f)]
    /// public Vector2 hpRange;
    ///
    /// [MinMaxSlider("dynamicRange", true)]
    /// public Vector2 dynamic;          // dynamicRange 是序列化 Vector2 字段
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class MinMaxSliderAttribute : Attribute
    {
        /// <summary>
        /// 以两个字面量边界构造。
        /// </summary>
        /// <param name="minValue">滑块左端。</param>
        /// <param name="maxValue">滑块右端，必须大于 <paramref name="minValue"/>。</param>
        /// <param name="showFields">是否在滑块旁再画两个可输入的数值框。</param>
        /// <exception cref="ArgumentException"><paramref name="maxValue"/> 不大于 <paramref name="minValue"/>。</exception>
        public MinMaxSliderAttribute(float minValue, float maxValue, bool showFields = false)
        {
            if (maxValue <= minValue)
            {
                throw new ArgumentException(
                    $"滑块的上限必须大于下限（收到 minValue={minValue}、maxValue={maxValue}）。", nameof(maxValue));
            }

            MinValue = minValue;
            MaxValue = maxValue;
            ShowFields = showFields;
        }

        /// <summary>
        /// 以一个字面量下界 + 一个成员提供的上界构造。
        /// </summary>
        /// <param name="minValue">滑块左端。</param>
        /// <param name="maxValueGetter">上界所在的**序列化成员名**（<c>float</c> 字段）。</param>
        /// <param name="showFields">是否再画两个数值框。</param>
        public MinMaxSliderAttribute(float minValue, string maxValueGetter, bool showFields = false)
        {
            MinValue = minValue;
            MaxValueGetter = maxValueGetter;
            ShowFields = showFields;
        }

        /// <summary>
        /// 以一个成员同时提供上下界构造（该成员是 <c>Vector2</c>，<c>x</c> 为下界、<c>y</c> 为上界）。
        /// </summary>
        /// <param name="minMaxValueGetter">上下界所在的**序列化成员名**（<c>Vector2</c> 字段）。</param>
        /// <param name="showFields">是否再画两个数值框。</param>
        public MinMaxSliderAttribute(string minMaxValueGetter, bool showFields = false)
        {
            MinMaxValueGetter = minMaxValueGetter;
            ShowFields = showFields;
        }

        /// <summary>
        /// 以一个成员提供的下界 + 一个字面量上界构造。
        /// </summary>
        /// <param name="minValueGetter">下界所在的**序列化成员名**（<c>float</c> 字段）。</param>
        /// <param name="maxValue">滑块右端。</param>
        /// <param name="showFields">是否再画两个数值框。</param>
        public MinMaxSliderAttribute(string minValueGetter, float maxValue, bool showFields = false)
        {
            MinValueGetter = minValueGetter;
            MaxValue = maxValue;
            ShowFields = showFields;
        }

        /// <summary>
        /// 以两个成员分别提供上下界构造（都是 <c>float</c> 字段）。
        /// </summary>
        /// <param name="minValueGetter">下界所在的**序列化成员名**。</param>
        /// <param name="maxValueGetter">上界所在的**序列化成员名**。</param>
        /// <param name="showFields">是否再画两个数值框。</param>
        public MinMaxSliderAttribute(string minValueGetter, string maxValueGetter, bool showFields = false)
        {
            MinValueGetter = minValueGetter;
            MaxValueGetter = maxValueGetter;
            ShowFields = showFields;
        }

        /// <summary>字面量下界；未使用该形态时为 0。</summary>
        public float MinValue { get; }

        /// <summary>字面量上界；未使用该形态时为 0。</summary>
        public float MaxValue { get; }

        /// <summary>
        /// 下界所在的序列化成员名；未使用该形态时为 <c>null</c>。
        /// </summary>
        public string MinValueGetter { get; }

        /// <summary>
        /// 上界所在的序列化成员名；未使用该形态时为 <c>null</c>。
        /// </summary>
        public string MaxValueGetter { get; }

        /// <summary>
        /// 同时提供上下界的序列化成员名（<c>Vector2</c>）；未使用该形态时为 <c>null</c>。
        /// <para>非 <c>null</c> 时**覆盖**上面四个。</para>
        /// </summary>
        public string MinMaxValueGetter { get; }

        /// <summary>是否在滑块旁再画两个可输入的数值框。</summary>
        public bool ShowFields { get; }
    }

    /// <summary>
    /// 把数值**回绕**到给定范围内：超出上限就从下限重新开始（角度、时间这类周期值的常用语义）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 回绕发生在**每次绘制之后**（与 <c>[MinValue]</c>/<c>[MaxValue]</c> 的钳制同一时机），
    /// 且同样在三种情形下跳过：多对象值不一致、字段当前只读、非数值类型（告警一次）。
    /// </para>
    /// <para>
    /// 区间按**半开**处理：值恰好等于上限时回绕到下限。<c>[Wrap(0, 360)]</c> 下 360 → 0，
    /// 这正是角度想要的行为。官方注明不支持无符号原始类型。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Wrap(0f, 360f)]
    /// public float angle;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class WrapAttribute : Attribute
    {
        /// <summary>
        /// 以回绕范围构造。
        /// </summary>
        /// <param name="min">范围下端（含）。</param>
        /// <param name="max">范围上端（不含），必须大于 <paramref name="min"/>。</param>
        /// <exception cref="ArgumentException"><paramref name="max"/> 不大于 <paramref name="min"/>。</exception>
        public WrapAttribute(double min, double max)
        {
            if (max <= min)
            {
                throw new ArgumentException($"回绕范围的上限必须大于下限（收到 min={min}、max={max}）。", nameof(max));
            }

            Min = min;
            Max = max;
        }

        /// <summary>范围下端（含）。</summary>
        public double Min { get; }

        /// <summary>范围上端（不含）。</summary>
        public double Max { get; }
    }
}
