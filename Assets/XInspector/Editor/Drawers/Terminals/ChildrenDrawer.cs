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
        /// <para>
        /// <b>搜索（<see cref="SearchScope"/>）与布局策略是两件事，都要认。</b> 分组节点在
        /// 一棵可搜索的子树里同样由这里画子节点——不认搜索的话，一个靠后代命中活下来的分组
        /// 会把**不命中的成员一起画出来**（嵌套层的分组装配会把成员节点的子节点换成分组节点，
        /// 所以这条路是常走的，不是边角）。
        /// </para>
        /// </remarks>
        public override void DrawPropertyLayout(InspectorProperty property, Attribute attribute, GUIContent label)
        {
            var scope = SearchScope.Find(property);
            var layout = property.State.Get<GroupChildrenLayout>();

            if (layout == null || !layout.HasPolicy)
            {
                DrawAllOrFiltered(property, scope);
                return;
            }

            var children = property.Children;

            // 多行排布：行作用域得在画各格**之间**开关，而只有末端站得到那个位置，
            // 故这一模式下由末端自己开——见 GroupChildrenLayout.RowsManagedByTerminal。
            if (layout.RowsManagedByTerminal && layout.CellRows != null && layout.RowCount > 0)
            {
                DrawRows(children, layout, scope);
                return;
            }

            // 页签：只画被选中的那一页。越界或空选择**回退为画全部**并告警一次——
            // 「什么都不画」是最难归因的一类现象（属性明明在，Inspector 里却是空的）。
            if (layout.OnlyChildIndex != GroupChildrenLayout.AllChildren)
            {
                if (layout.OnlyChildIndex < 0 || layout.OnlyChildIndex >= children.Count)
                {
                    DrawerWarnings.Once(property, nameof(ChildrenDrawer) + ".选页越界",
                        $"[XInspector] 分组「{property.Path}」的选中页（{layout.OnlyChildIndex}）越界，" +
                        "已回退为画出全部子节点。");
                    DrawAllOrFiltered(property, scope);
                    return;
                }

                DrawCell(children[layout.OnlyChildIndex], layout, layout.OnlyChildIndex, scope);
                return;
            }

            for (var i = 0; i < children.Count; i++)
            {
                if (i > 0 && layout.CellGap > 0f)
                {
                    GUILayout.Space(layout.CellGap);
                }

                DrawCell(children[i], layout, i, scope);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>画全部子节点；有搜索在过滤时只画命中的那些。</summary>
        /// <param name="property">容器节点。</param>
        /// <param name="scope">搜索作用域。</param>
        /// <remarks>
        /// 走 <see cref="InspectorProperty.DrawChildren"/> 那条原路径得先确认没有过滤——
        /// 它画的是全部子节点，与过滤是互斥的两件事。
        /// </remarks>
        private static void DrawAllOrFiltered(InspectorProperty property, SearchScope scope)
        {
            if (!scope.IsActive)
            {
                property.DrawChildren();
                return;
            }

            var children = property.Children;

            // 一个都没命中是**正常的**（这一组整体不匹配），故不在这里留提示——
            // 提示归宿主那一层说一次，每个分组各说一句会变成一屏灰字。
            for (var i = 0; i < children.Count; i++)
            {
                if (scope.ShouldDraw(children[i]))
                {
                    children[i].Draw();
                }
            }
        }

        /// <summary>按行画：行号一变就换一个水平作用域。</summary>
        /// <param name="children">子节点。</param>
        /// <param name="layout">分组装下的策略，行号来自 <see cref="GroupChildrenLayout.CellRows"/>。</param>
        /// <param name="scope">搜索作用域。</param>
        /// <remarks>
        /// 手写 <c>BeginHorizontal</c>/<c>EndHorizontal</c> 而不是 <c>using</c> 作用域：
        /// 作用域要跨循环迭代，<c>using</c> 表达不了。收尾放在循环之后，
        /// 中途抛异常会留下一个没关的作用域——IMGUI 里这属于「一次绘制坏了」，
        /// 下一帧重新开始，不会累积。
        /// </remarks>
        private static void DrawRows(System.Collections.Generic.IReadOnlyList<InspectorProperty> children,
            GroupChildrenLayout layout, SearchScope scope)
        {
            var currentRow = -1;

            for (var i = 0; i < children.Count; i++)
            {
                var row = i < layout.CellRows.Length ? layout.CellRows[i] : layout.RowCount - 1;

                if (row != currentRow)
                {
                    if (currentRow >= 0)
                    {
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.BeginHorizontal();
                    currentRow = row;
                }
                else if (layout.CellGap > 0f)
                {
                    GUILayout.Space(layout.CellGap);
                }

                DrawCell(children[i], layout, i, scope);
            }

            if (currentRow >= 0)
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>按策略画一格：宽度与标签宽度各就各位。</summary>
        /// <param name="child">子节点。</param>
        /// <param name="layout">分组装下的策略。</param>
        /// <param name="index">格子的下标。</param>
        /// <param name="scope">搜索作用域；被筛掉的格子直接不画（行作用域照常开关）。</param>
        /// <remarks>
        /// 宽度用嵌套的 <see cref="EditorGUILayout.VerticalScope(GUILayoutOption[])"/> 包住：
        /// GUILayout 的宽度选项只作用于**下一个布局组**，而要约束的是一个子节点的整块内容
        /// （它可能是好几行）。标签宽度是全局状态，用 <c>try/finally</c> 还原。
        /// </remarks>
        private static void DrawCell(InspectorProperty child, GroupChildrenLayout layout, int index, SearchScope scope)
        {
            if (!scope.ShouldDraw(child))
            {
                return;
            }

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
