using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 搜索框那一行：一行输入框，外加「没有匹配的项」的提示。
    /// <para>
    /// 只有两件事，但两处宿主（复合成员的末端、集合绘制器）都要画，故独立成一格——
    /// 免得两处各写一遍，长歪成两种长相。
    /// </para>
    /// </summary>
    internal static class SearchBox
    {
        #region Public API

        /// <summary>
        /// 画搜索框并写回输入。**宿主节点自己调**（状态就是在这里按需建出来的）。
        /// </summary>
        /// <param name="host">挂着 <c>[Searchable]</c> 的节点。</param>
        /// <remarks>
        /// 用 <c>toolbarSearchField</c> 的样式（带放大镜图标）。样式名写错是**编译错误**，
        /// 不是静默失效——与「不猜 Odin 的 API 形状」那条纪律不冲突。
        /// </remarks>
        public static void Draw(InspectorProperty host)
        {
            var state = host.State.GetOrCreate<SearchFilterState>();
            var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

            state.Query = EditorGUI.TextField(row, state.Query ?? string.Empty, EditorStyles.toolbarSearchField);
        }

        /// <summary>一条「没有匹配的项」的灰字提示。</summary>
        /// <remarks>
        /// 一块**空**的 Inspector 区域是最难归因的现象之一（是搜索筛掉了？还是字段不见了？），
        /// 故宁可多说一句。
        /// </remarks>
        public static void DrawNoMatchHint()
        {
            EditorGUILayout.LabelField("没有匹配的项。", EditorStyles.miniLabel);
        }

        #endregion
    }
}
