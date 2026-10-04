using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 容器节点的末端绘制器：依次画出所有子节点。
    /// <para>
    /// 根节点与分组节点共用同一实现——两者在这个位置上做的事完全一样（画子节点），
    /// 差别只在**兄弟链上还挂了什么**：分组节点多一个分组绘制器负责画框，
    /// 根节点则通常只有类级特性合成出来的绘制器。差别在链的其他格子里，
    /// 不在这里，所以没有必要为它们各写一个类。
    /// </para>
    /// <para>
    /// 由构建期**显式追加**到链尾，不经注册表匹配——
    /// 见 <see cref="DrawerChainBuilder"/> 对「末端是结构性的」的说明。
    /// </para>
    /// </summary>
    internal sealed class ChildrenDrawer : XInspectorDrawer
    {
        #region XInspectorDrawer

        /// <summary>
        /// 恒为 <c>false</c>：末端绘制器由构建期显式追加，从不参与自动匹配。
        /// <para>
        /// 若这里返回 <c>true</c>，它会被自动加进每一条容器属性的链，
        /// 于是与显式追加的那一格重复，子节点会被画两遍。
        /// </para>
        /// </summary>
        /// <param name="property">候选属性。</param>
        /// <returns>恒为 <c>false</c>。</returns>
        public override bool CanDraw(InspectorProperty property)
        {
            return false;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// 分组绘制器可以先把一份 <see cref="GroupChildrenLayout"/> 装进属性状态，
        /// 再调到这里——水平分组与页签就是这样改变「内侧怎么画」的。
        /// 没装策略时走原路径（画全部子节点）。
        /// </remarks>
        public override void DrawPropertyLayout(InspectorProperty property, Attribute attribute, GUIContent label)
        {
            var layout = property.State.Get<GroupChildrenLayout>();

            if (layout == null || !layout.HasPolicy)
            {
                property.DrawChildren();
                return;
            }

            var children = property.Children;

            // 页签：只画被选中的那一页。越界或空选择**回退为画全部**并告警一次——
            // 「什么都不画」是最难归因的一类现象（属性明明在，Inspector 里却是空的）。
            if (layout.OnlyChildIndex != GroupChildrenLayout.AllChildren)
            {
                if (layout.OnlyChildIndex < 0 || layout.OnlyChildIndex >= children.Count)
                {
                    DrawerWarnings.Once(property, nameof(ChildrenDrawer) + ".选页越界",
                        $"[XInspector] 分组「{property.Path}」的选中页（{layout.OnlyChildIndex}）越界，" +
                        "已回退为画出全部子节点。");
                    property.DrawChildren();
                    return;
                }

                DrawCell(children[layout.OnlyChildIndex], layout, layout.OnlyChildIndex);
                return;
            }

            for (var i = 0; i < children.Count; i++)
            {
                if (i > 0 && layout.CellGap > 0f)
                {
                    GUILayout.Space(layout.CellGap);
                }

                DrawCell(children[i], layout, i);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>按策略画一格：宽度与标签宽度各就各位。</summary>
        /// <param name="child">子节点。</param>
        /// <param name="layout">分组装下的策略。</param>
        /// <param name="index">格子的下标。</param>
        /// <remarks>
        /// 宽度用嵌套的 <see cref="EditorGUILayout.VerticalScope(GUILayoutOption[])"/> 包住：
        /// GUILayout 的宽度选项只作用于**下一个布局组**，而要约束的是一个子节点的整块内容
        /// （它可能是好几行）。标签宽度是全局状态，用 <c>try/finally</c> 还原。
        /// </remarks>
        private static void DrawCell(InspectorProperty child, GroupChildrenLayout layout, int index)
        {
            var width = layout.CellWidths != null && index < layout.CellWidths.Length ? layout.CellWidths[index] : 0f;
            var labelWidth = layout.CellLabelWidths != null && index < layout.CellLabelWidths.Length
                ? layout.CellLabelWidths[index]
                : 0f;

            if (width <= 0f && labelWidth <= 0f)
            {
                child.Draw();
                return;
            }

            using (new EditorGUILayout.VerticalScope(
                       width > 0f ? GUILayout.Width(width) : GUILayout.ExpandWidth(true)))
            {
                var previousLabelWidth = EditorGUIUtility.labelWidth;

                try
                {
                    if (labelWidth > 0f)
                    {
                        EditorGUIUtility.labelWidth = labelWidth;
                    }

                    child.Draw();
                }
                finally
                {
                    EditorGUIUtility.labelWidth = previousLabelWidth;
                }
            }
        }

        #endregion
    }
}
