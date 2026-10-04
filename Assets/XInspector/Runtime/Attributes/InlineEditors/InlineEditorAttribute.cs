using System;

namespace XInspector
{
    /// <summary>
    /// 内嵌编辑器画什么。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是本包自建的枚举（Odin 也有一个同名的）。文档站按**字母序**排成员，
    /// **数值未核实**——故这里按自己的顺序编号：<b>默认成员排 0，其余按「画得越来越多」排</b>，
    /// 于是 <c>default(InlineEditorModes)</c> 恰好就是默认行为。
    /// </para>
    /// <para>不要写数值转换（如 <c>(InlineEditorModes)3</c>）——顺序将来可能变。</para>
    /// </remarks>
    public enum InlineEditorModes
    {
        /// <summary>只画编辑器本身的界面（默认）。</summary>
        GUIOnly = 0,

        /// <summary>画编辑器头 + 界面。</summary>
        GUIAndHeader = 1,

        /// <summary>界面在左、小预览在右。</summary>
        GUIAndPreview = 2,

        /// <summary>头 + 界面在左 + 小预览在右，三样都画。</summary>
        FullEditor = 3,

        /// <summary>只画一个小预览，不画界面。</summary>
        SmallPreview = 4,

        /// <summary>只画一个大预览，不画界面。</summary>
        LargePreview = 5,
    }

    /// <summary>
    /// 内嵌编辑器上方那个对象字段怎么画。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="InlineEditorModes"/> 同款：数值由本包定，默认成员排 0。
    /// 成员顺序按「字段露出多少」由多到少。
    /// </remarks>
    public enum InlineEditorObjectFieldModes
    {
        /// <summary>字段装在框里（默认）。</summary>
        Boxed = 0,

        /// <summary>字段收进折叠头里。</summary>
        Foldout = 1,

        /// <summary>有值时隐藏字段；值为空时仍然露出，好让你能赋值。</summary>
        Hidden = 2,

        /// <summary>恒隐藏字段，值为空时也不露面。</summary>
        CompletelyHidden = 3,
    }

    /// <summary>
    /// 预览相对编辑器界面的位置。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="InlineEditorModes"/> 同款：数值由本包定，默认成员排 0
    /// （本包的默认是**预览在右**，与官方对 <c>GUIAndPreview</c> 的描述一致：
    /// 「界面在左，小预览在右」）。
    /// </remarks>
    public enum PreviewAlignment
    {
        /// <summary>预览在右（默认，与界面并排）。</summary>
        Right = 0,

        /// <summary>预览在左（与界面并排）。</summary>
        Left = 1,

        /// <summary>预览在上（与界面上下堆叠）。</summary>
        Top = 2,

        /// <summary>预览在下（与界面上下堆叠）。</summary>
        Bottom = 3,
    }

    /// <summary>
    /// 把 <c>UnityEngine.Object</c> 字段画成**内嵌编辑器**：在字段下方直接画出被引用对象的
    /// Inspector（组件、材质、网格、预制体……都行）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只对对象引用生效；其它类型告警并退回普通绘制，混合值（多对象编辑）同样退回。
    /// </para>
    /// <para>
    /// <b>嵌套出来的那一层走 Unity 自己的编辑器解析</b>：被引用对象的类型若接了本管线，
    /// 画出来就是一棵 XInspector 树（递归成立，无需本包做任何额外的事）；否则是 Unity 原生绘制。
    /// </para>
    /// <para>
    /// <b>本包自定值（Odin 那边存在它的偏好设置里，官网核不到）：</b>
    /// </para>
    /// <list type="bullet">
    /// <item><description>嵌套深度上限 <c>4</c>（见 <c>InlineEditorDrawContext.MaxDepth</c>）。超出后告警并退回普通对象字段，不静默消失。</description></item>
    /// <item><description>预览默认尺寸：与界面同画时宽 <c>64</c>，单独画时高 <c>64</c>；<see cref="InlineEditorModes.LargePreview"/> 单独画时高 <c>128</c>。</description></item>
    /// <item><description>默认预览位置为<see cref="PreviewAlignment.Right"/>。</description></item>
    /// </list>
    /// <para>
    /// <b>与 Odin 的一处语义差异：</b> <see cref="InlineEditorObjectFieldModes.CompletelyHidden"/>
    /// 且值为空时，本包画一行灰色提示（「字段被隐藏」）而不是留一片空白——本包不接受
    /// 「静默地什么都不画」。
    /// </para>
    /// <para>
    /// 本包不带 <c>[Conditional("UNITY_EDITOR")]</c>（全包既有特性零处使用）：这些特性是纯数据，
    /// 玩家构建里留着不产生任何行为，剥掉反而让「同一声明在两个环境里长得不一样」。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [InlineEditor]                                  // 只画界面
    /// public Material material;
    ///
    /// [InlineEditor(InlineEditorModes.FullEditor)]    // 头 + 界面 + 小预览
    /// public GameObject model;
    ///
    /// [InlineEditor(InlineEditorModes.LargePreview)]  // 只画大预览
    /// public Mesh mesh;
    ///
    /// [InlineEditor(InlineEditorObjectFieldModes.Hidden, MaxHeight = 200f)]
    /// public ScriptableObject config;                 // 字段藏起来，内嵌区超过 200 像素出滚动条
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class InlineEditorAttribute : Attribute
    {
        #region Construction

        /// <summary>
        /// 以模式与对象字段模式构造。
        /// </summary>
        /// <param name="inlineEditorMode">内嵌编辑器画什么。默认只画界面。</param>
        /// <param name="objectFieldMode">上方的对象字段怎么画。默认装在框里。</param>
        /// <remarks>
        /// 模式**在构造期就被拆成三面旗**（<see cref="DrawHeader"/>、<see cref="DrawGUI"/>、
        /// <see cref="DrawPreview"/>）——Odin 的公开面上也没有存模式的地方，本包照此。
        /// 因此具名实参可以事后覆盖任何一面旗，例如
        /// <c>[InlineEditor(InlineEditorModes.GUIOnly, DrawPreview = true)]</c>。
        /// </remarks>
        public InlineEditorAttribute(
            InlineEditorModes inlineEditorMode = InlineEditorModes.GUIOnly,
            InlineEditorObjectFieldModes objectFieldMode = InlineEditorObjectFieldModes.Boxed)
        {
            ObjectFieldMode = objectFieldMode;
            IncrementInlineEditorDrawerDepth = true;
            DisableGUIForVCSLockedAssets = true;
            PreviewAlignment = PreviewAlignment.Right;

            DrawHeader = inlineEditorMode == InlineEditorModes.GUIAndHeader ||
                         inlineEditorMode == InlineEditorModes.FullEditor;

            DrawGUI = inlineEditorMode != InlineEditorModes.SmallPreview &&
                      inlineEditorMode != InlineEditorModes.LargePreview;

            DrawPreview = inlineEditorMode == InlineEditorModes.GUIAndPreview ||
                          inlineEditorMode == InlineEditorModes.FullEditor ||
                          inlineEditorMode == InlineEditorModes.SmallPreview ||
                          inlineEditorMode == InlineEditorModes.LargePreview;

            if (inlineEditorMode == InlineEditorModes.LargePreview)
            {
                // 大预览的默认高度。单独画时才用得上（PreviewHeight 的语义），
                // 具名实参照样能盖掉它。
                PreviewHeight = DefaultLargePreviewHeight;
            }
        }

        /// <summary>
        /// 只指定对象字段模式，编辑器画法取默认（只画界面）。
        /// </summary>
        /// <param name="objectFieldMode">上方的对象字段怎么画。</param>
        public InlineEditorAttribute(InlineEditorObjectFieldModes objectFieldMode)
            : this(InlineEditorModes.GUIOnly, objectFieldMode)
        {
        }

        #endregion

        #region Constants

        /// <summary>大预览模式的默认高度（像素）。</summary>
        /// <remarks>本包自定值——Odin 的默认值在它的偏好设置里，官网核不到。</remarks>
        public const float DefaultLargePreviewHeight = 128f;

        #endregion

        #region Options

        /// <summary>
        /// 对象字段被版本控制锁定时，是否把内嵌内容置灰。
        /// <para>默认 <c>true</c>（与 Odin 同默认）。只影响内嵌内容——上方的对象字段照常可用。</para>
        /// </summary>
        public bool DisableGUIForVCSLockedAssets { get; set; }

        /// <summary>
        /// 是否在对象字段下方画出被引用对象的 Inspector 界面。
        /// </summary>
        public bool DrawGUI { get; set; }

        /// <summary>
        /// 是否在界面之上画出被引用对象的编辑器头（图标、名字、类型）。
        /// </summary>
        public bool DrawHeader { get; set; }

        /// <summary>
        /// 是否画出被引用对象的预览。
        /// </summary>
        public bool DrawPreview { get; set; }

        /// <summary>
        /// 这一层是否计入嵌套深度。
        /// <para>
        /// 默认 <c>true</c>。置 <c>false</c> 表示「这一层不算内嵌编辑器」——
        /// <c>[ShowInInlineEditors]</c> 一族在里面不会生效。它**不解除**成环与上限的守卫。
        /// </para>
        /// </summary>
        public bool IncrementInlineEditorDrawerDepth { get; set; }

        /// <summary>
        /// 内嵌内容的最大高度（像素）。超出出滚动条；非正表示不限，按内容全尺寸展开。
        /// </summary>
        public float MaxHeight { get; set; }

        /// <summary>
        /// 上方的对象字段怎么画。由构造参数设定。
        /// </summary>
        public InlineEditorObjectFieldModes ObjectFieldMode { get; set; }

        /// <summary>
        /// 预览相对界面的位置。默认 <see cref="PreviewAlignment.Right"/>。
        /// </summary>
        public PreviewAlignment PreviewAlignment { get; set; }

        /// <summary>
        /// 单独画预览时的高度（像素）。非正表示用默认值（小预览 64、大预览 128）。
        /// </summary>
        public float PreviewHeight { get; set; }

        /// <summary>
        /// 与界面并排画预览时的宽度（像素）。非正表示用默认值 64。
        /// </summary>
        public float PreviewWidth { get; set; }

        /// <summary>
        /// 内嵌编辑器起始是否展开（只对 <see cref="InlineEditorObjectFieldModes.Foldout"/> 有意义）。
        /// </summary>
        /// <remarks>
        /// 写它会把 <see cref="ExpandedHasValue"/> 一并置真——「显式设过」与「没设过」要能区分，
        /// 与 <c>[FoldoutGroup]</c> 的同类成员一致。
        /// </remarks>
        public bool Expanded
        {
            get => _expanded;
            set
            {
                _expanded = value;
                ExpandedHasValue = true;
            }
        }

        /// <summary>
        /// <see cref="Expanded"/> 是否被显式设过。
        /// </summary>
        public bool ExpandedHasValue { get; private set; }

        #endregion

        #region Private Fields

        private bool _expanded;

        #endregion
    }
}
