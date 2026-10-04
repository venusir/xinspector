using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="AssetSelectorAttribute"/>：对象字段前一个小按钮，点开是工程资产列表。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>透传型</b>：画完按钮照常 <c>CallNextDrawer</c>，所以对象字段本身还是 Unity 原生那个
    /// （类型限制、拖拽赋值、预制体覆盖一样不少）。这是与其它值绘制器最大的不同——
    /// 那几个是替换型，得自己套 <see cref="EditorGUI.DisabledScope"/>；这里不需要。
    /// </para>
    /// <para>
    /// 弹出层是 <see cref="GenericMenu"/>，**不自建窗口**：官方那个带搜索框、图标、多选的
    /// 弹出层本包不做，那几个只为它存在的选项因此**不声明**（见特性注释）。
    /// </para>
    /// <para>
    /// 菜单项的构造（树形分层、拍平、排序）是纯逻辑，在
    /// <see cref="AssetSelectorOptions"/> 里；<c>AssetDatabase</c> 只在
    /// <see cref="AssetSelectorQuery"/> 里出现一次，那样大部分判定仍可无头测试。
    /// </para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class AssetSelectorDrawer : AttributeDrawer<AssetSelectorAttribute>
    {
        #region Private Fields

        /// <summary>小按钮的宽度（像素）。</summary>
        private const float MiniButtonWidth = 20f;

        /// <summary>小按钮的文本。</summary>
        private static readonly GUIContent MiniLabel = new GUIContent("▼", "从工程资产中选择");

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, AssetSelectorAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.ObjectReference)
            {
                DrawerWarnings.Once(property, nameof(AssetSelectorDrawer),
                    DrawerWarnings.TypeMismatch(property, "[AssetSelector]", "对象引用（UnityEngine.Object）"));
                CallNextDrawer(property, label);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
                {
                    if (GUILayout.Button(MiniLabel, EditorStyles.miniButton, GUILayout.Width(MiniButtonWidth)))
                    {
                        ShowMenu(property, serializedProperty, attribute);
                    }
                }

                CallNextDrawer(property, label);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 弹出资产菜单；选中则把资产填进字段。
        /// </summary>
        /// <remarks>
        /// 与 <c>[ValueDropdown]</c> 同一手法：<c>MenuFunction2</c> + 路径当 <c>userData</c>，
        /// 不为每个选项建闭包。
        /// </remarks>
        private static void ShowMenu(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            AssetSelectorAttribute attribute)
        {
            var folders = AssetSelectorOptions.SplitPaths(attribute.Paths);
            var assetPaths = AssetSelectorQuery.FindAssetPaths(folders, attribute.Filter);

            if (assetPaths.Count == 0)
            {
                DrawerWarnings.Once(property, nameof(AssetSelectorDrawer) + ".empty",
                    $"[XInspector] 属性「{property.Path}」上的 [AssetSelector] 没找到任何资产" +
                    $"（Paths = {attribute.Paths ?? "（整个工程）"}，Filter = {attribute.Filter ?? "（无）"}）。");
                return;
            }

            var options = AssetSelectorOptions.Build(assetPaths, attribute.FlattenTreeView);

            var menu = new GenericMenu();
            var target = serializedProperty.Copy();

            for (var i = 0; i < options.Count; i++)
            {
                menu.AddItem(new GUIContent(options[i].Path), false, OnSelected, new Selection(target, options[i].AssetPath));
            }

            menu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
        }

        /// <summary>
        /// 菜单回调：把选中的资产填进字段。
        /// </summary>
        /// <param name="userData">目标属性 + 资产路径。</param>
        private static void OnSelected(object userData)
        {
            var selection = (Selection)userData;
            var asset = AssetDatabase.LoadMainAssetAtPath(selection.AssetPath);

            if (asset == null)
            {
                Debug.LogWarning($"[XInspector] [AssetSelector] 选中的资产加载不了：{selection.AssetPath}");
                return;
            }

            selection.Target.objectReferenceValue = asset;
        }

        #endregion

        /// <summary>
        /// 菜单回调的载荷。
        /// </summary>
        private readonly struct Selection
        {
            /// <summary>要写入的目标。</summary>
            public readonly SerializedProperty Target;

            /// <summary>选中的资产路径。</summary>
            public readonly string AssetPath;

            /// <summary>以两段构造。</summary>
            /// <param name="target">目标属性。</param>
            /// <param name="assetPath">资产路径。</param>
            public Selection(SerializedProperty target, string assetPath)
            {
                Target = target;
                AssetPath = assetPath;
            }
        }
    }

    /// <summary>
    /// 资产路径列表 → 菜单项。纯逻辑，可无头测试。
    /// </summary>
    internal static class AssetSelectorOptions
    {
        #region Public API

        /// <summary>
        /// 菜单里的一项。
        /// </summary>
        public readonly struct Option
        {
            /// <summary>以菜单路径与资产路径构造。</summary>
            /// <param name="path">菜单路径（树形时用 <c>/</c> 分层）。</param>
            /// <param name="assetPath">资产的工程路径。</param>
            public Option(string path, string assetPath)
            {
                Path = path;
                AssetPath = assetPath;
            }

            /// <summary>菜单路径。</summary>
            public string Path { get; }

            /// <summary>资产的工程路径。</summary>
            public string AssetPath { get; }
        }

        /// <summary>
        /// 拆分 <c>Paths</c>：<c>|</c> 分隔，逐个去空白，丢掉空段。
        /// </summary>
        /// <param name="paths">原文；可为 <c>null</c>。</param>
        /// <returns>目录数组；没有有效目录时为空数组。</returns>
        public static string[] SplitPaths(string paths)
        {
            if (string.IsNullOrWhiteSpace(paths))
            {
                return System.Array.Empty<string>();
            }

            var parts = paths.Split('|');
            var result = new List<string>(parts.Length);

            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim().TrimEnd('/');
                if (part.Length > 0)
                {
                    result.Add(part);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 把资产路径列表变成菜单项。
        /// </summary>
        /// <param name="assetPaths">资产的工程路径（形如 <c>Assets/Art/a.mat</c>）。</param>
        /// <param name="flatten"><c>true</c> 时只显示文件名、不分层。</param>
        /// <returns>菜单项；按菜单路径排序。</returns>
        /// <remarks>
        /// 树形路径**去掉开头的 <c>Assets/</c>**：菜单里那一层人人相同，占着地方没有信息量。
        /// </remarks>
        public static List<Option> Build(IList<string> assetPaths, bool flatten)
        {
            var result = new List<Option>();

            if (assetPaths == null)
            {
                return result;
            }

            for (var i = 0; i < assetPaths.Count; i++)
            {
                var assetPath = assetPaths[i];
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                var path = flatten
                    ? System.IO.Path.GetFileName(assetPath)
                    : StripAssetsPrefix(assetPath);

                result.Add(new Option(path, assetPath));
            }

            result.Sort(CompareByPath);
            return result;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 去掉开头的 <c>Assets/</c>。
        /// </summary>
        /// <param name="assetPath">资产路径。</param>
        /// <returns>去掉前缀的路径。</returns>
        private static string StripAssetsPrefix(string assetPath)
        {
            const string prefix = "Assets/";

            return assetPath.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)
                ? assetPath.Substring(prefix.Length)
                : assetPath;
        }

        /// <summary>
        /// 按菜单路径排序（序数比较，跨平台稳定）。
        /// </summary>
        /// <param name="left">左。</param>
        /// <param name="right">右。</param>
        /// <returns>比较结果。</returns>
        private static int CompareByPath(Option left, Option right)
        {
            return string.CompareOrdinal(left.Path, right.Path);
        }

        #endregion
    }

    /// <summary>
    /// 资产查询：<c>AssetDatabase</c> 只在这里出现，这样其余判定都还能无头测试。
    /// </summary>
    internal static class AssetSelectorQuery
    {
        #region Public API

        /// <summary>
        /// 找符合条件的主资产路径。
        /// </summary>
        /// <param name="folders">限定目录；为空表示整个工程。</param>
        /// <param name="filter">AssetDatabase 搜索过滤串；可为 <c>null</c>。</param>
        /// <returns>资产路径列表（已去重）。</returns>
        /// <remarks>
        /// 只在**菜单弹出时**调用（事件路径），不是每帧——全工程搜索是重活，
        /// 放在绘制路径上会拖垮 Inspector。
        /// </remarks>
        public static List<string> FindAssetPaths(string[] folders, string filter)
        {
            var result = new List<string>();

            var guids = folders == null || folders.Length == 0
                ? AssetDatabase.FindAssets(filter ?? string.Empty)
                : AssetDatabase.FindAssets(filter ?? string.Empty, folders);

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (string.IsNullOrEmpty(path) || result.Contains(path))
                {
                    continue;
                }

                result.Add(path);
            }

            return result;
        }

        #endregion
    }
}
