using System;

namespace XInspector
{
    /// <summary>
    /// 给成员及其内侧的一切染上一层颜色。
    /// <para>
    /// 它在链上位于**最外层**（权重 <c>-900</c>），所以染的不只是值控件——分组框、
    /// 信息框、替换型值绘制器都在它里面。用在类上则整页着色：类级特性落在根节点，
    /// 根节点的链照样是「包住子节点」，于是「整页染色」不需要任何特例代码。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [GUIColor(1f, 0.6f, 0.2f)]
    /// public float dangerMeter;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class GUIColorAttribute : Attribute
    {
        /// <summary>
        /// 以 RGBA 构造，各分量取值 0–1。
        /// </summary>
        /// <param name="r">红色分量。</param>
        /// <param name="g">绿色分量。</param>
        /// <param name="b">蓝色分量。</param>
        /// <param name="a">不透明度，默认 <c>1</c>（不透明）。</param>
        public GUIColorAttribute(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>红色分量。</summary>
        public float R { get; }

        /// <summary>绿色分量。</summary>
        public float G { get; }

        /// <summary>蓝色分量。</summary>
        public float B { get; }

        /// <summary>不透明度。</summary>
        public float A { get; }
    }

    /// <summary>
    /// 覆盖本成员标签的宽度（像素）。
    /// <para>
    /// 一个字段的标签宽度由全局设置决定，个别字段需要例外时用它，
    /// 而不必去改整个 Inspector 的全局值。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 传负值等价于「交还给 Unity 的默认行为」（<c>EditorGUIUtility.labelWidth</c> 的既有语义），
    /// 本包不额外校验——那是个合法用法，不是笔误。
    /// </remarks>
    /// <example>
    /// <code>
    /// [LabelWidth(200f)]
    /// public string longDescription;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class LabelWidthAttribute : Attribute
    {
        /// <summary>
        /// 以标签宽度构造。
        /// </summary>
        /// <param name="width">标签宽度（像素）。</param>
        public LabelWidthAttribute(float width)
        {
            Width = width;
        }

        /// <summary>标签宽度（像素）。</summary>
        public float Width { get; }
    }

    /// <summary>
    /// 撤掉成员的标签，只留值控件。
    /// <para>
    /// 与把标签文本换成空串不同：它是把**整个标签区域**让给值控件，因此
    /// <c>[HideLabel]</c> 的字段值会占满整行。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [HideLabel]
    /// public string rawJson;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class HideLabelAttribute : Attribute
    {
    }

    /// <summary>
    /// 在成员前后各留一段间距（像素）。
    /// <para>
    /// 三个构造对应官方的三种写法：无参是前后各 8 像素，单参只给 <see cref="SpaceBefore"/>，
    /// 双参给全。也可以用具名字段直接赋值：<c>[PropertySpace(SpaceBefore = 30, SpaceAfter = 60)]</c>。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>负值不生效</b>：Unity 的垂直布局不接受负间距，本包在绘制时跳过非正值。
    /// 想要更紧凑的排布请调整分组结构，别指望负间距。
    /// </remarks>
    /// <example>
    /// <code>
    /// [PropertySpace(20f)]
    /// public int separated;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class PropertySpaceAttribute : Attribute
    {
        /// <summary>以默认间距（前后各 8 像素）构造。</summary>
        public PropertySpaceAttribute()
        {
            SpaceBefore = 8f;
            SpaceAfter = 8f;
        }

        /// <summary>
        /// 只指定**前置**间距；后置保持 0。
        /// </summary>
        /// <param name="spaceBefore">前置间距（像素）。</param>
        public PropertySpaceAttribute(float spaceBefore)
        {
            SpaceBefore = spaceBefore;
        }

        /// <summary>
        /// 指定前置与后置间距。
        /// </summary>
        /// <param name="spaceBefore">前置间距（像素）。</param>
        /// <param name="spaceAfter">后置间距（像素）。</param>
        public PropertySpaceAttribute(float spaceBefore, float spaceAfter)
        {
            SpaceBefore = spaceBefore;
            SpaceAfter = spaceAfter;
        }

        /// <summary>前置间距（像素）。</summary>
        public float SpaceBefore { get; set; }

        /// <summary>后置间距（像素）。</summary>
        public float SpaceAfter { get; set; }
    }

    /// <summary>
    /// 把成员及其内侧的一切缩进若干级。
    /// <para>
    /// 与 <see cref="PropertySpaceAttribute"/> 一样是「包住内侧」的修饰：它把缩进加上去、
    /// 调用下一个绘制器、再把缩进**原样还原**，因此嵌套多个 <c>[Indent]</c> 会逐层加深，
    /// 也不会污染同一 Inspector 里的其它字段。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 官方允许重复标注（多个 <c>[Indent]</c> 叠加）；本包同样允许——链按特性实例配对，
    /// 两个实例就是两格，各自加一级。负值表示反向缩进。
    /// </remarks>
    /// <example>
    /// <code>
    /// [Indent]
    /// public int child;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class IndentAttribute : Attribute
    {
        /// <summary>
        /// 以缩进级数构造。
        /// </summary>
        /// <param name="indentLevel">缩进级数，默认 1；负值为反向缩进。</param>
        public IndentAttribute(int indentLevel = 1)
        {
            IndentLevel = indentLevel;
        }

        /// <summary>缩进级数。</summary>
        public int IndentLevel { get; }
    }

    /// <summary>
    /// 在值控件右侧（或叠在其上）画一段后缀文字，用来补单位之类的说明。
    /// <para>
    /// 后缀是**只读装饰**：它不进值、不可编辑，也不改变值的布局语义。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>与官方的一处差异：</b>Odin 允许重复标注（多个后缀叠着画），本包只允许一个——
    /// 多个后缀在右侧争同一块宽度没有明确语义，与其发明一条规则不如让它编译不过。
    /// </remarks>
    /// <example>
    /// <code>
    /// [SuffixLabel("秒")]
    /// public float duration = 1.5f;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class SuffixLabelAttribute : Attribute
    {
        /// <summary>
        /// 以后缀文本构造。
        /// </summary>
        /// <param name="label">后缀文本，不得为空白。</param>
        /// <param name="overlay">是否把后缀叠在值控件之上（而非占右侧一列）。</param>
        /// <exception cref="ArgumentException"><paramref name="label"/> 为 null、空串或仅含空白。</exception>
        public SuffixLabelAttribute(string label, bool overlay = false)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("后缀文本不能为空。", nameof(label));
            }

            Label = label;
            Overlay = overlay;
        }

        /// <summary>后缀文本。</summary>
        public string Label { get; }

        /// <summary>是否叠在值控件之上，默认 <c>false</c>（占右侧一列）。</summary>
        public bool Overlay { get; }
    }
}
