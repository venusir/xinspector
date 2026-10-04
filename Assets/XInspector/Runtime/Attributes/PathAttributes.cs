using System;

namespace XInspector
{
    /// <summary>
    /// 把字符串字段画成**文件路径**：可手填，也可点「浏览…」从磁盘选。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只对 <c>string</c> 字段生效；其它类型告警并退回普通绘制。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异（两处，都是刻意收窄）：</b>
    /// </para>
    /// <para>
    /// 其一，<b>参数只认字面量</b>。Odin 的 <see cref="ParentFolder"/> 与
    /// <see cref="Extensions"/> 都支持 <c>$</c> 成员引用（如 <c>"$DynamicParent"</c>），
    /// 本包不做——那属于 resolved string 族，与条件族、<c>[CustomValueDrawer]</c>
    /// 的取舍同一条线。
    /// </para>
    /// <para>
    /// 其二，<b>不支持 <c>string[]</c></b>。数组形态要按元素画，而数组在本包里整个交给
    /// Unity 的 <c>PropertyField(includeChildren: true)</c>；按元素绘制要先接管数组绘制，
    /// 那是集合自绘那一层的事。故本特性**只作用于单个 <c>string</c>**。
    /// </para>
    /// <para>
    /// 官方另有一个 <c>IncludeFileExtension</c> 字段，本包**不声明**：官方只给了它一句话
    /// （"If true the file path will include the file's extension."），默认值与确切语义都
    /// 没核对到。一个「设了也不产生行为」的开关属于本包最想避免的那类静默现象，
    /// 宁可让它编译不过。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [FilePath]
    /// public string configPath;                  // 工程相对路径
    ///
    /// [FilePath(Extensions = "cs, unity")]
    /// public string scriptPath;                  // 只允许这两类扩展名（点可选）
    ///
    /// [FilePath(ParentFolder = "Assets/Resources")]
    /// public string resourcePath;                // 相对 Assets/Resources
    ///
    /// [FilePath(AbsolutePath = true)]
    /// public string absolutePath;                // 存绝对路径
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class FilePathAttribute : Attribute
    {
        /// <summary>
        /// 路径相对哪个目录。<c>null</c> 表示相对工程根（即以 <c>Assets/…</c> 开头）。
        /// <para>可以是工程相对路径，也可以是绝对路径。</para>
        /// </summary>
        public string ParentFolder { get; set; }

        /// <summary>
        /// 允许的扩展名，逗号分隔，**点可选**（<c>"cs, unity"</c> 与 <c>".cs,.unity"</c> 等价）。
        /// <para><c>null</c> 或空白表示不限制。只影响「浏览…」对话框的过滤器，不校验手填的值。</para>
        /// </summary>
        public string Extensions { get; set; }

        /// <summary>
        /// <c>true</c> 时字段存**绝对路径**而不是相对路径。默认为 <c>false</c>。
        /// </summary>
        public bool AbsolutePath { get; set; }

        /// <summary>
        /// <c>true</c> 时路径不存在会在字段下方显示一条错误框。默认为 <c>false</c>。
        /// <para>只提示、不拦——与校验族的姿态一致，不悄悄改数据也不阻止填写。</para>
        /// </summary>
        public bool RequireExistingPath { get; set; }

        /// <summary>
        /// <c>true</c> 时路径用反斜杠分隔。默认为 <c>false</c>（一律正斜杠，与 Unity 一致）。
        /// <para>归一化发生在**写入时**，显示与存储都是归一化后的形式。</para>
        /// </summary>
        public bool UseBackslashes { get; set; }
    }

    /// <summary>
    /// 把字符串字段画成**目录路径**：可手填，也可点「浏览…」选文件夹。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与 <see cref="FilePathAttribute"/> 是同一套东西的两个形态（选文件 / 选目录），
    /// 参数、收窄与边界完全一致，差别只有「浏览」打开的是文件夹面板、且没有扩展名过滤。
    /// </para>
    /// <para>
    /// 只对单个 <c>string</c> 生效，不支持 <c>string[]</c>；参数只认字面量、不做
    /// <c>$</c> 成员引用——理由同 <see cref="FilePathAttribute"/>。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [FolderPath]
    /// public string outputDir;
    ///
    /// [FolderPath(ParentFolder = "Assets", RequireExistingPath = true)]
    /// public string existingFolder;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class FolderPathAttribute : Attribute
    {
        /// <summary>
        /// 路径相对哪个目录。<c>null</c> 表示相对工程根（即以 <c>Assets/…</c> 开头）。
        /// <para>可以是工程相对路径，也可以是绝对路径。</para>
        /// </summary>
        public string ParentFolder { get; set; }

        /// <summary>
        /// <c>true</c> 时字段存**绝对路径**而不是相对路径。默认为 <c>false</c>。
        /// </summary>
        public bool AbsolutePath { get; set; }

        /// <summary>
        /// <c>true</c> 时路径不存在会在字段下方显示一条错误框。默认为 <c>false</c>。
        /// </summary>
        public bool RequireExistingPath { get; set; }

        /// <summary>
        /// <c>true</c> 时路径用反斜杠分隔。默认为 <c>false</c>（一律正斜杠，与 Unity 一致）。
        /// </summary>
        public bool UseBackslashes { get; set; }
    }
}
