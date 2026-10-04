using System;

namespace XInspector
{
    /// <summary>
    /// 预览方块在可用宽度里的位置。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是本包自建的枚举（Odin 也有一个同名的）。文档站按**字母序**排成员，
    /// **数值未核实**——故这里按 <see cref="Left"/> / <see cref="Center"/> / <see cref="Right"/>
    /// 从 0 起排，与 <c>InfoMessageType</c>/<c>TitleAlignments</c> 同款处理。
    /// </para>
    /// <para>不要写数值转换（如 <c>(ObjectFieldAlignment)1</c>）——顺序将来可能变。</para>
    /// </remarks>
    public enum ObjectFieldAlignment
    {
        /// <summary>靠左。</summary>
        Left = 0,

        /// <summary>居中。</summary>
        Center = 1,

        /// <summary>靠右。</summary>
        Right = 2,
    }

    /// <summary>
    /// 把 <c>UnityEngine.Object</c> 字段画成**带预览的方块**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只对对象引用生效；其它类型告警并退回普通绘制。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异（三处，都要知道）：</b>
    /// </para>
    /// <para>
    /// 其一，<b>方块是预览、不是控件</b>。Odin 让那个方块本身既是预览又是对象字段
    /// （可拖入、可 Ctrl+点击清空、可 Ctrl+拖拽替换）；本包把预览画成一方块、
    /// 把**可编辑的对象字段**排在它旁边（宽度不够时排到下一行）。拖拽赋值照常可用，
    /// 但 Ctrl+点击清空、Ctrl+拖拽替换这类交互**不做**——那需要自绘对象字段的拖拽与点击处理，
    /// 成本远高于收益。
    /// </para>
    /// <para>
    /// 其二，<b>默认高度是本包定的</b>（<c>64</c> 像素）。Odin 的默认值存在它的偏好设置里，
    /// 官网核不到；<see cref="Height"/> 未指定时一律按 64。
    /// </para>
    /// <para>
    /// 其三，<b>默认对齐是本包定的</b>（<see cref="ObjectFieldAlignment.Left"/>），
    /// 理由同上——Odin 的默认对齐也在偏好设置里。
    /// </para>
    /// <para>
    /// 官方另有两个含 <c>UnityEngine.FilterMode</c> 的重载与一个 <c>previewGetter</c>
    /// 字符串参数，**永久不做**：前者会把 UnityEngine 类型带进 Runtime，
    /// 而「Runtime 零 Unity 依赖」是编译期强制的（<c>Tests.Native</c> 用纯 .NET 编译整份 Runtime）；
    /// 后者属 resolved string 族。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [PreviewField]
    /// public Texture2D icon;
    ///
    /// [PreviewField(80f, ObjectFieldAlignment.Right)]
    /// [HideLabel]
    /// public GameObject model;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class PreviewFieldAttribute : Attribute
    {
        /// <summary>
        /// 以默认高度与默认对齐构造。
        /// </summary>
        public PreviewFieldAttribute()
        {
        }

        /// <summary>
        /// 以高度构造。
        /// </summary>
        /// <param name="height">预览方块边长（像素）。非正值按默认高度处理。</param>
        public PreviewFieldAttribute(float height)
        {
            Height = height;
        }

        /// <summary>
        /// 以对齐方式构造。
        /// </summary>
        /// <param name="alignment">预览方块的位置。</param>
        public PreviewFieldAttribute(ObjectFieldAlignment alignment)
        {
            Alignment = alignment;
        }

        /// <summary>
        /// 以高度与对齐方式构造。
        /// </summary>
        /// <param name="height">预览方块边长（像素）。非正值按默认高度处理。</param>
        /// <param name="alignment">预览方块的位置。</param>
        public PreviewFieldAttribute(float height, ObjectFieldAlignment alignment)
        {
            Height = height;
            Alignment = alignment;
        }

        /// <summary>
        /// 预览方块边长（像素）。
        /// <para>非正值表示「未指定」，绘制时按默认的 64 处理。可具名赋值（<c>Height = 150</c>）。</para>
        /// </summary>
        public float Height { get; set; }

        /// <summary>预览方块的位置。默认 <see cref="ObjectFieldAlignment.Left"/>。</summary>
        public ObjectFieldAlignment Alignment { get; set; }
    }
}
