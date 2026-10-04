using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="ShowDrawerChainAttribute"/>：把该属性的绘制器链画成一张表。
    /// <para>
    /// 权重 <c>-950</c>：几乎最外层（只让位给 <c>SuperPriority</c>）。
    /// 链信息要能看见「一切」，包括最外的配色与缩进，故它得比它们还外。
    /// 它自己也会出现在表里（第 0 格）——这不是花絮，正是「链是真实的」那部分自证。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 表的内容在**首次绘制时**构建一次并缓存在 <see cref="PropertyState"/>：
    /// 链在构建期装配后即冻结，逐帧重建字符串纯属浪费（每帧路径禁字符串拼接）。
    /// </remarks>
    [DrawerPriority(-950d)]
    internal sealed class ShowDrawerChainDrawer : AttributeDrawer<ShowDrawerChainAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, ShowDrawerChainAttribute attribute, GUIContent label)
        {
            var state = property.State.GetOrCreate<ShowDrawerChainState>();

            if (state.Rows == null)
            {
                state.Header = DrawerChainReport.BuildHeader(property);
                state.Rows = DrawerChainReport.BuildRows(property);
            }

            state.Expanded = EditorGUILayout.Foldout(state.Expanded, state.Header, true);

            if (state.Expanded)
            {
                EditorGUI.indentLevel++;
                try
                {
                    for (var i = 0; i < state.Rows.Length; i++)
                    {
                        EditorGUILayout.LabelField(state.Rows[i], EditorStyles.miniLabel);
                    }
                }
                finally
                {
                    EditorGUI.indentLevel--;
                }
            }

            CallNextDrawer(property, label);
        }

        #endregion
    }

    /// <summary>
    /// 绘制器链的表格内容，挂在 <see cref="PropertyState"/> 上（首次绘制构建一次）。
    /// </summary>
    internal sealed class ShowDrawerChainState
    {
        /// <summary>是否已展开。</summary>
        public bool Expanded;

        /// <summary>折叠标题（含链长）。</summary>
        public string Header;

        /// <summary>每格一行的说明；尚未构建时为 <c>null</c>。</summary>
        public string[] Rows;
    }

    /// <summary>
    /// 把一条绘制器链渲染成文本行。纯函数（只读链），可无头测试。
    /// </summary>
    internal static class DrawerChainReport
    {
        #region Public API

        /// <summary>
        /// 构建折叠标题。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <returns>形如 <c>绘制器链（3 格）</c> 的标题。</returns>
        public static string BuildHeader(InspectorProperty property)
        {
            return $"绘制器链（{property.Chain.Entries.Length} 格）";
        }

        /// <summary>
        /// 构建逐格说明。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <returns>与链上格子一一对应的文本行。</returns>
        public static string[] BuildRows(InspectorProperty property)
        {
            var entries = property.Chain.Entries;
            var rows = new string[entries.Length];

            for (var i = 0; i < entries.Length; i++)
            {
                rows[i] = Describe(entries[i], i);
            }

            return rows;
        }

        /// <summary>
        /// 描述一格：序号、绘制器名、权重、触发它的特性。
        /// </summary>
        /// <param name="entry">链上的格子。</param>
        /// <param name="index">格子下标（0 为最外层）。</param>
        /// <returns>说明文本。</returns>
        public static string Describe(DrawerChainEntry entry, int index)
        {
            return $"[{index}] {entry.Drawer.GetType().Name}   权重 {DescribePriority(entry.Priority)}   {DescribeTrigger(entry)}";
        }

        #endregion

        #region Private Helpers

        /// <summary>说明这一格为什么在链上。</summary>
        /// <param name="entry">链上的格子。</param>
        /// <returns>触发来源的描述。</returns>
        private static string DescribeTrigger(DrawerChainEntry entry)
        {
            if (entry.Attribute != null)
            {
                return "← " + entry.Attribute.GetType().Name;
            }

            return entry.Priority.Value == double.MaxValue
                ? "末端 · 构建期显式追加"
                : "CanDraw 匹配";
        }

        /// <summary>权重的显示形式；末端用名字而不是 <c>double.MaxValue</c>。</summary>
        /// <param name="priority">权重。</param>
        /// <returns>显示文本。</returns>
        private static string DescribePriority(DrawerPriority priority)
        {
            return priority.Value == double.MaxValue
                ? "末端"
                : priority.Value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
