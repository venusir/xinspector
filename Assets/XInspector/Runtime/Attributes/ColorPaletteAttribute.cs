using System;

namespace XInspector
{
    /// <summary>
    /// 把 <c>Color</c> 字段画成**调色板**：字段上方多一行色块，点一下就把那个颜色填进去。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>它不替代取色器。</b> 原生颜色字段照常画在下面（本特性是**透传型**），
    /// 精确值、alpha、吸管都在那儿——调色板是「常用色的快捷入口」。
    /// </para>
    /// <para>
    /// <b>调色板是工程里的一份资产</b>（<c>XInspectorColorPalette</c>，右键
    /// <c>Create/XInspector/Color Palette</c> 建）。之所以不做成编辑器偏好：那样不进版本控制、
    /// 不跨机器，团队里每个人得各自配一份。
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>[ColorPalette("名字")]</c>：找**资产文件名（不含扩展名）**等于它的那一份，
    /// 大小写不敏感。</description></item>
    /// <item><description><c>[ColorPalette]</c>：用工程里**唯一**的那一份——一份都没有、
    /// 或不止一份时**告警并退回普通绘制**（多份时告警里会列出候选名）。</description></item>
    /// </list>
    /// <para>
    /// <b>找不到调色板不会让字段消失</b>：告警一次，字段退回普通绘制（本包对「特性配置不对」
    /// 的一贯兜底）。**只作用单个 <c>Color</c> 字段**——数组与 <c>List&lt;Color&gt;</c> 不做
    /// （那要按元素画，而「给每个元素套同一份调色板」的语义没定，凭空发明比不做更糟）。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public XInspectorColorPalette uiPalette;   // 工程里的调色板资产（可选：直接引用它）
    ///
    /// [ColorPalette("UI")]      // 找名为 UI 的那份资产
    /// public Color accent;
    ///
    /// [ColorPalette]            // 工程里唯一的那份
    /// public Color background;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ColorPaletteAttribute : Attribute
    {
        /// <summary>
        /// 用工程里**唯一**那份调色板。
        /// </summary>
        /// <remarks>
        /// 「唯一」是刻意选的规则：无参形态要一份不会含糊的默认，而「工程里恰好只有一份时
        /// 才无歧义」既可解释、失败时也响亮（多一份就告警，而不是悄悄挑一份）。
        /// </remarks>
        public ColorPaletteAttribute()
        {
        }

        /// <summary>
        /// 以调色板名构造。
        /// </summary>
        /// <param name="paletteName">
        /// 调色板名 = **资产文件名（不含扩展名）**，大小写不敏感。
        /// </param>
        public ColorPaletteAttribute(string paletteName)
        {
            PaletteName = paletteName;
        }

        /// <summary>
        /// 调色板名；无参形态为 <c>null</c>（那时用工程里唯一的那一份）。
        /// </summary>
        public string PaletteName { get; }
    }
}
