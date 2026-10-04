using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="FilePathAttribute"/>：字符串画成文件路径输入框 + 「浏览…」按钮。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 替换型绘制器（自己画完、不调用下一个），故**必须自己套
    /// <see cref="EditorGUI.DisabledScope"/>**——末端绘制器那层只读保护被绕过了。
    /// </para>
    /// <para>
    /// 判定与字符串处理全在纯函数 <see cref="PathEditing"/> 里，本类只做「画、读面板返回值、写回」。
    /// </para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class FilePathDrawer : AttributeDrawer<FilePathAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, FilePathAttribute attribute, GUIContent label)
        {
            var serializedProperty = PathFieldGUI.RequireString(property, nameof(FilePathDrawer), "[FilePath]");
            if (serializedProperty == null)
            {
                CallNextDrawer(property, label);
                return;
            }

            var options = new PathFieldOptions(
                isFolder: false,
                parentFolder: attribute.ParentFolder,
                absolute: attribute.AbsolutePath,
                requireExisting: attribute.RequireExistingPath,
                useBackslashes: attribute.UseBackslashes,
                extensions: attribute.Extensions);

            PathFieldGUI.Draw(property, serializedProperty, label, options);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="FolderPathAttribute"/>：字符串画成目录路径输入框 + 「浏览…」按钮。
    /// </summary>
    /// <remarks>与 <see cref="FilePathDrawer"/> 同一套绘制，只差「打开的是文件夹面板、且没有扩展名过滤」。</remarks>
    [DrawerPriority(0d)]
    internal sealed class FolderPathDrawer : AttributeDrawer<FolderPathAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, FolderPathAttribute attribute, GUIContent label)
        {
            var serializedProperty = PathFieldGUI.RequireString(property, nameof(FolderPathDrawer), "[FolderPath]");
            if (serializedProperty == null)
            {
                CallNextDrawer(property, label);
                return;
            }

            var options = new PathFieldOptions(
                isFolder: true,
                parentFolder: attribute.ParentFolder,
                absolute: attribute.AbsolutePath,
                requireExisting: attribute.RequireExistingPath,
                useBackslashes: attribute.UseBackslashes,
                extensions: null);

            PathFieldGUI.Draw(property, serializedProperty, label, options);
        }

        #endregion
    }

    /// <summary>
    /// 两个路径特性共用的绘制。
    /// </summary>
    internal static class PathFieldGUI
    {
        #region Private Fields

        /// <summary>「浏览…」按钮的宽度（像素）。</summary>
        private const float BrowseWidth = 56f;

        /// <summary>文本框与按钮之间的间距（像素）。</summary>
        private const float Gap = 2f;

        #endregion

        #region Public API

        /// <summary>
        /// 取字符串序列化属性；不是字符串就告警。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="drawerName">绘制器名（告警去重键用）。</param>
        /// <param name="attributeName">特性名（含方括号）。</param>
        /// <returns>序列化属性；不适用时返回 <c>null</c>——**调用方须自行 <c>CallNextDrawer</c> 退回**。</returns>
        public static SerializedProperty RequireString(InspectorProperty property, string drawerName, string attributeName)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.String)
            {
                DrawerWarnings.Once(property, drawerName,
                    DrawerWarnings.TypeMismatch(property, attributeName, "字符串（string）"));
                return null;
            }

            return serializedProperty;
        }

        /// <summary>
        /// 画「标签 + 文本框 + 浏览按钮」，并在需要时于下方补一条路径不存在的错误框。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="serializedProperty">字符串序列化属性。</param>
        /// <param name="label">标签。</param>
        /// <param name="options">特性参数。</param>
        public static void Draw(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            GUIContent label,
            PathFieldOptions options)
        {
            var state = property.State.GetOrCreate<PathFieldState>();
            state.EnsureInitialized(options);

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                DrawRow(property, serializedProperty, label, options, state);
            }

            DrawExistenceError(property, serializedProperty, options, state);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 一行：标签 + 文本框 + 浏览按钮。整行占一个带标签的控件矩形，再手工切成两段。
        /// </summary>
        /// <remarks>
        /// 用 rect 形式而不是 <c>EditorGUILayout.TextField</c>，为的是能套
        /// <see cref="EditorGUI.BeginProperty"/>——预制体覆盖的右键菜单与混合值显示都挂在它上面。
        /// 不用 <c>HorizontalScope</c>：那会让 <c>GetControlRect</c> 在横向布局里的语义变得依赖
        /// 前面的控件，切出来的宽度不好推理。
        /// </remarks>
        private static void DrawRow(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            GUIContent label,
            PathFieldOptions options,
            PathFieldState state)
        {
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginProperty(rect, label, serializedProperty);

            if (label != null && label != GUIContent.none)
            {
                rect = EditorGUI.PrefixLabel(rect, label);
            }

            var buttonRect = new Rect(rect.xMax - BrowseWidth, rect.y, BrowseWidth, rect.height);
            var textRect = new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - BrowseWidth - Gap), rect.height);

            var previousMixed = EditorGUI.showMixedValue;
            string edited;
            try
            {
                // showMixedValue 是全局状态，必须还原——漏还原会让别的 Inspector 显示成混合态。
                EditorGUI.showMixedValue = serializedProperty.hasMultipleDifferentValues;
                edited = EditorGUI.TextField(textRect, serializedProperty.stringValue);
            }
            finally
            {
                EditorGUI.showMixedValue = previousMixed;
            }

            if (edited != serializedProperty.stringValue)
            {
                serializedProperty.stringValue = PathEditing.Normalize(edited, options.UseBackslashes);
            }

            EditorGUI.EndProperty();

            if (GUI.Button(buttonRect, state.BrowseLabel, EditorStyles.miniButton))
            {
                Browse(property, serializedProperty, options, state);
            }
        }

        /// <summary>
        /// 打开系统面板；选中则把绝对路径换算成字段的存储形态写回。
        /// </summary>
        private static void Browse(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            PathFieldOptions options,
            PathFieldState state)
        {
            var projectRoot = PathEditing.ProjectRoot();
            var current = PathEditing.ToAbsolute(
                serializedProperty.stringValue, projectRoot, options.ParentFolder, options.Absolute);

            var startDirectory = PathEditing.ToPanelDirectory(current, options.IsFolder, projectRoot);

            string picked;
            if (options.IsFolder)
            {
                picked = EditorUtility.OpenFolderPanel("选择文件夹", startDirectory, string.Empty);
            }
            else if (state.ExtensionFilter != null)
            {
                picked = EditorUtility.OpenFilePanelWithFilters(
                    "选择文件", startDirectory, state.ExtensionFilter);
            }
            else
            {
                picked = EditorUtility.OpenFilePanel("选择文件", startDirectory, string.Empty);
            }

            if (string.IsNullOrEmpty(picked))
            {
                // 用户取消：什么都不做（尤其不要把手填的值抹掉）。
                return;
            }

            var stored = PathEditing.ToStored(picked, projectRoot, options.ParentFolder, options.Absolute, options.UseBackslashes);
            if (stored == serializedProperty.stringValue)
            {
                return;
            }

            serializedProperty.stringValue = stored;

            // 值刚变，存在性判定要重来一次。
            state.InvalidateExistence();

            if (!PathEditing.IsExtensionAllowed(stored, state.Extensions))
            {
                DrawerWarnings.Once(property, nameof(PathFieldGUI) + ".ext",
                    $"[XInspector] 属性「{property.Path}」选中的文件扩展名不在允许列表（{options.Extensions}）内。" +
                    "已照常写入——允许列表只用于过滤对话框，不拦手填与已选的值。");
            }
        }

        /// <summary>
        /// <c>RequireExistingPath</c> 时，路径不存在就在下方画一条错误框。
        /// </summary>
        /// <remarks>
        /// 存在性检查**带缓存**：只在字符串变化时碰一次磁盘。
        /// 每帧 <c>File.Exists</c> 是绘制路径上不该有的 I/O。
        /// </remarks>
        private static void DrawExistenceError(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            PathFieldOptions options,
            PathFieldState state)
        {
            if (!options.RequireExisting)
            {
                return;
            }

            var value = serializedProperty.stringValue;
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (!state.TryGetExistence(value, options, out var exists) || exists)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                $"[XInspector] 路径不存在：{value}（[{(options.IsFolder ? "FolderPath" : "FilePath")}(RequireExistingPath = true)]）",
                MessageType.Error);
        }

        #endregion
    }

    /// <summary>
    /// 路径字段的参数集合（两个特性字段名一致，用一个结构体传，免得一堆同类型位置参数写反）。
    /// </summary>
    internal readonly struct PathFieldOptions
    {
        /// <summary>以参数构造。</summary>
        /// <param name="isFolder"><c>true</c> 表示目录（决定打开哪个面板与错误框文案）。</param>
        /// <param name="parentFolder">相对目录；可为 <c>null</c>。</param>
        /// <param name="absolute">是否存绝对路径。</param>
        /// <param name="requireExisting">是否提示路径不存在。</param>
        /// <param name="useBackslashes">是否用反斜杠。</param>
        /// <param name="extensions">扩展名白名单原文；目录形为 <c>null</c>。</param>
        public PathFieldOptions(
            bool isFolder,
            string parentFolder,
            bool absolute,
            bool requireExisting,
            bool useBackslashes,
            string extensions)
        {
            IsFolder = isFolder;
            ParentFolder = parentFolder;
            Absolute = absolute;
            RequireExisting = requireExisting;
            UseBackslashes = useBackslashes;
            Extensions = extensions;
        }

        /// <summary>是否目录形态。</summary>
        public bool IsFolder { get; }

        /// <summary>相对目录。</summary>
        public string ParentFolder { get; }

        /// <summary>是否存绝对路径。</summary>
        public bool Absolute { get; }

        /// <summary>是否提示路径不存在。</summary>
        public bool RequireExisting { get; }

        /// <summary>是否用反斜杠。</summary>
        public bool UseBackslashes { get; }

        /// <summary>扩展名白名单原文。</summary>
        public string Extensions { get; }
    }

    /// <summary>
    /// 路径字段的每属性状态，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    /// <remarks>
    /// 绘制器是共享单例，不得有可变字段——缓存一律放这里。
    /// </remarks>
    internal sealed class PathFieldState
    {
        #region Private Fields

        /// <summary>存在性判定的缓存键（字符串值）。</summary>
        private string _existenceKey;

        /// <summary>存在性判定的缓存结果。</summary>
        private bool _existenceValue;

        /// <summary>是否已经做过一次存在性判定。</summary>
        private bool _existenceValid;

        #endregion

        #region Public API

        /// <summary>是否已初始化（解析只做一次）。</summary>
        public bool Initialized { get; private set; }

        /// <summary>解析后的扩展名白名单（小写、无点）。</summary>
        public string[] Extensions { get; private set; }

        /// <summary>给 <c>OpenFilePanelWithFilters</c> 用的过滤器数组；无扩展名限制时为 <c>null</c>。</summary>
        public string[] ExtensionFilter { get; private set; }

        /// <summary>缓存的「浏览…」按钮文本。</summary>
        public GUIContent BrowseLabel { get; private set; }

        /// <summary>
        /// 首次绘制时解析一次参数。
        /// </summary>
        /// <param name="options">特性参数。</param>
        public void EnsureInitialized(PathFieldOptions options)
        {
            if (Initialized)
            {
                return;
            }

            Initialized = true;
            BrowseLabel = new GUIContent("浏览…", "从磁盘选择路径");

            Extensions = PathEditing.ParseExtensions(options.Extensions);

            if (Extensions.Length > 0)
            {
                // 官方要求：扩展名不带点、逗号分隔且**不带空格**（而 [FilePath] 的写法允许带空格）。
                ExtensionFilter = new[]
                {
                    "允许的类型", string.Join(",", Extensions),
                    "所有文件", "*",
                };
            }
        }

        /// <summary>
        /// 取存在性判定结果，必要时查一次磁盘。
        /// </summary>
        /// <param name="value">当前路径字符串。</param>
        /// <param name="options">特性参数。</param>
        /// <param name="exists">路径是否存在。</param>
        /// <returns>本次是否得到了有效结论（空路径返回 <c>false</c>）。</returns>
        public bool TryGetExistence(string value, PathFieldOptions options, out bool exists)
        {
            if (_existenceValid && _existenceKey == value)
            {
                exists = _existenceValue;
                return true;
            }

            var absolute = PathEditing.ToAbsolute(value, PathEditing.ProjectRoot(), options.ParentFolder, options.Absolute);

            _existenceKey = value;
            _existenceValue = options.IsFolder ? Directory.Exists(absolute) : File.Exists(absolute);
            _existenceValid = true;

            exists = _existenceValue;
            return true;
        }

        /// <summary>
        /// 让存在性缓存失效（值刚被改过时调用）。
        /// </summary>
        public void InvalidateExistence()
        {
            _existenceValid = false;
            _existenceKey = null;
        }

        #endregion
    }

    /// <summary>
    /// 路径的字符串处理：全部是纯函数（除 <see cref="ProjectRoot"/> 外不碰 Unity、不碰磁盘），
    /// 可无头测试。
    /// </summary>
    /// <remarks>
    /// <b>工程根只从 <see cref="ProjectRoot"/> 一处取</b>，其余函数一律把它当参数收下——
    /// 这样测试不必伪造 <c>Application.dataPath</c>，直接传一个假根即可。
    /// </remarks>
    internal static class PathEditing
    {
        #region Public API

        /// <summary>
        /// 当前工程的根目录（绝对路径，不含结尾分隔符）。
        /// </summary>
        /// <returns>形如 <c>E:/Proj</c>；<c>Application.dataPath</c> 不可用时返回空串。</returns>
        public static string ProjectRoot()
        {
            var dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
            {
                return string.Empty;
            }

            var root = Path.GetDirectoryName(dataPath);
            return string.IsNullOrEmpty(root) ? string.Empty : Normalize(root, false);
        }

        /// <summary>
        /// 判定是否为绝对路径。
        /// </summary>
        /// <param name="path">待判定的路径。</param>
        /// <returns>绝对返回 <c>true</c>。</returns>
        /// <remarks>
        /// Windows 上认盘符（<c>C:\</c> 与 <c>C:/</c> 都算）与 UNC（<c>\\server</c>）；
        /// 以 <c>/</c> 开头的路径在 Windows 上算**工程相对**（Unity 的写法），在 Unix 上算绝对。
        /// </remarks>
        public static bool IsAbsolute(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (path.Length >= 2 && path[1] == ':')
            {
                return true;
            }

            if (path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            {
                return true;
            }

            return Path.DirectorySeparatorChar == '/' && path[0] == '/';
        }

        /// <summary>
        /// 把分隔符统一成指定方向。
        /// </summary>
        /// <param name="path">路径。</param>
        /// <param name="useBackslashes"><c>true</c> 用反斜杠。</param>
        /// <returns>归一化后的路径；空输入原样返回。</returns>
        public static string Normalize(string path, bool useBackslashes)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            return useBackslashes
                ? path.Replace('/', '\\')
                : path.Replace('\\', '/');
        }

        /// <summary>
        /// 解析扩展名白名单：逗号分隔、点可选、大小写不敏感、自动去空白。
        /// </summary>
        /// <param name="extensions">原文；可为 <c>null</c> 或空白。</param>
        /// <returns>小写、无点的扩展名数组；无限制时为空数组。</returns>
        public static string[] ParseExtensions(string extensions)
        {
            if (string.IsNullOrWhiteSpace(extensions))
            {
                return Array.Empty<string>();
            }

            var parts = extensions.Split(',');
            var result = new List<string>(parts.Length);

            for (var i = 0; i < parts.Length; i++)
            {
                var extension = parts[i].Trim().TrimStart('.').ToLowerInvariant();
                if (extension.Length > 0)
                {
                    result.Add(extension);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 判定路径的扩展名是否在白名单内。
        /// </summary>
        /// <param name="path">路径。</param>
        /// <param name="allowed">白名单（<see cref="ParseExtensions"/> 的结果）。</param>
        /// <returns>允许返回 <c>true</c>；白名单为空表示不限制。</returns>
        public static bool IsExtensionAllowed(string path, string[] allowed)
        {
            if (allowed == null || allowed.Length == 0)
            {
                return true;
            }

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var extension = Path.GetExtension(path);
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            extension = extension.TrimStart('.').ToLowerInvariant();
            for (var i = 0; i < allowed.Length; i++)
            {
                if (string.Equals(allowed[i], extension, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 拼接两段路径（后者为绝对路径时原样返回）。
        /// </summary>
        /// <param name="left">左段。</param>
        /// <param name="right">右段。</param>
        /// <returns>拼接结果，分隔符为正斜杠。</returns>
        public static string Combine(string left, string right)
        {
            if (string.IsNullOrEmpty(right))
            {
                return left ?? string.Empty;
            }

            if (IsAbsolute(right))
            {
                return Normalize(right, false);
            }

            if (string.IsNullOrEmpty(left))
            {
                return Normalize(right, false);
            }

            return Normalize(left.TrimEnd('/', '\\') + "/" + right.TrimStart('/', '\\'), false);
        }

        /// <summary>
        /// 把基准目录算出来：<c>ParentFolder</c> 优先，它本身是绝对路径时就以它为准。
        /// </summary>
        /// <param name="projectRoot">工程根（绝对）。</param>
        /// <param name="parentFolder">相对目录；可为 <c>null</c>。</param>
        /// <returns>绝对路径的基准目录。</returns>
        public static string BaseDirectory(string projectRoot, string parentFolder)
        {
            if (string.IsNullOrWhiteSpace(parentFolder))
            {
                return projectRoot ?? string.Empty;
            }

            return IsAbsolute(parentFolder)
                ? Normalize(parentFolder, false)
                : Combine(projectRoot, parentFolder);
        }

        /// <summary>
        /// 字段存储形态 → 绝对路径。
        /// </summary>
        /// <param name="stored">字段里的字符串。</param>
        /// <param name="projectRoot">工程根（绝对）。</param>
        /// <param name="parentFolder">相对目录；可为 <c>null</c>。</param>
        /// <param name="absolute">字段存的是否已经是绝对路径。</param>
        /// <returns>绝对路径；空输入返回空串。</returns>
        public static string ToAbsolute(string stored, string projectRoot, string parentFolder, bool absolute)
        {
            if (string.IsNullOrEmpty(stored))
            {
                return string.Empty;
            }

            if (absolute || IsAbsolute(stored))
            {
                return Normalize(stored, false);
            }

            return Combine(BaseDirectory(projectRoot, parentFolder), stored);
        }

        /// <summary>
        /// 绝对路径 → 字段存储形态。
        /// </summary>
        /// <param name="absolutePath">绝对路径。</param>
        /// <param name="projectRoot">工程根（绝对）。</param>
        /// <param name="parentFolder">相对目录；可为 <c>null</c>。</param>
        /// <param name="absolute">字段是否存绝对路径。</param>
        /// <param name="useBackslashes">是否用反斜杠。</param>
        /// <returns>写入字段的字符串。</returns>
        /// <remarks>
        /// 选中的路径若**不在基准目录之下**（例如工程外的文件），无法表达成相对路径，
        /// 此时存入绝对路径——字段显示的仍是用户真正选中的那个位置，不悄悄改成别的。
        /// </remarks>
        public static string ToStored(
            string absolutePath, string projectRoot, string parentFolder, bool absolute, bool useBackslashes)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return string.Empty;
            }

            if (absolute)
            {
                return Normalize(absolutePath, useBackslashes);
            }

            var baseDirectory = BaseDirectory(projectRoot, parentFolder);
            if (IsUnder(absolutePath, baseDirectory))
            {
                var relative = absolutePath.Substring(baseDirectory.Length).TrimStart('/', '\\');
                return Normalize(relative, useBackslashes);
            }

            return Normalize(absolutePath, useBackslashes);
        }

        /// <summary>
        /// 判定路径是否位于某目录之下（大小写不敏感的段级比较）。
        /// </summary>
        /// <param name="path">路径。</param>
        /// <param name="directory">目录。</param>
        /// <returns>在之下返回 <c>true</c>。</returns>
        /// <remarks>
        /// 比字符串前缀多一层：<c>Assets/PluginsExtra</c> 不该被当成 <c>Assets/Plugins</c> 之下。
        /// </remarks>
        public static bool IsUnder(string path, string directory)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory))
            {
                return false;
            }

            var normalizedPath = Normalize(path, false).TrimEnd('/');
            var normalizedDirectory = Normalize(directory, false).TrimEnd('/');

            if (normalizedPath.Length <= normalizedDirectory.Length)
            {
                return false;
            }

            if (!normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return normalizedPath[normalizedDirectory.Length] == '/';
        }

        /// <summary>
        /// 系统面板的起始位置：目标是文件时取它**所在的目录**，是目录时用它自己。
        /// </summary>
        /// <param name="absolutePath">当前值的绝对路径；可为空——空值表示没有起点。</param>
        /// <param name="isFolder">当前值本身是不是目录。</param>
        /// <param name="projectRoot">工程根。</param>
        /// <returns>
        /// 工程相对目录（在工程外时原样返回绝对路径）；无从判断时返回空串
        /// （此时面板会开在系统默认位置）。
        /// </returns>
        /// <remarks>
        /// 官方对 <c>OpenFilePanel</c> 的 <c>directory</c> 参数写的是「相对工程目录」，
        /// 故工程内一律给工程相对路径；工程外给绝对路径（Unity 实际接受）。
        /// </remarks>
        public static string ToPanelDirectory(string absolutePath, bool isFolder, string projectRoot)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return string.Empty;
            }

            var normalized = Normalize(absolutePath, false);
            var directory = normalized;

            if (!isFolder)
            {
                // 目标是文件：起点是它所在的目录，而不是它自己。
                directory = Path.GetDirectoryName(normalized);
                if (string.IsNullOrEmpty(directory))
                {
                    return string.Empty;
                }

                directory = Normalize(directory, false);
            }

            if (IsUnder(directory, projectRoot) || string.Equals(
                    directory.TrimEnd('/'), (projectRoot ?? string.Empty).TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                var relative = directory.Substring((projectRoot ?? string.Empty).TrimEnd('/').Length).TrimStart('/');
                return relative;
            }

            return directory;
        }

        #endregion
    }
}
