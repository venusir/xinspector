using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 表格形态的绘制：列头 + 每行一格的元素。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与列表形态共用同一套增删意图（趟末统一施加）与同一个标题行（「+」在标题行右端）。
    /// </para>
    /// <para>
    /// <b>用切矩形而不是 GUILayout 的弹性分配</b>：列宽是算出来的，列头与每一行用**同一个**
    /// <see cref="AllocateWidths"/> 结果切——列对齐不靠布局系统的分配巧合，且分配函数是纯数学，
    /// 可以无头测试。
    /// </para>
    /// </remarks>
    internal static class TableLayout
    {
        #region Private Fields

        /// <summary>序号列的宽度（像素）。</summary>
        private const float IndexColumnWidth = 24f;

        /// <summary>格与格之间的间距（像素）。</summary>
        private const float CellSpacing = 2f;

        /// <summary>行尾删除按钮的宽度（像素）。</summary>
        private const float ButtonWidth = 20f;

        /// <summary>弹性列的宽度下限（像素）——列多到装不下时也不缩成负数。</summary>
        private const float MinimumColumnWidth = 24f;

        #endregion

        #region Public API

        /// <summary>
        /// 按列宽模型分配宽度：固定宽的先扣，剩余宽度由弹性列均分（每列至少一个下限）。
        /// </summary>
        /// <param name="available">整行可用宽度（已扣除行尾按钮与序号列）。</param>
        /// <param name="columns">列。</param>
        /// <param name="indexColumn">是否有序号列。</param>
        /// <returns>每列的宽度；长度 = 序号列（若有）+ <paramref name="columns"/>。</returns>
        /// <remarks>
        /// 列多到装不下时**不缩到负数**：超出部分被右侧裁掉（写进已知限制，本轮不做横向滚动）。
        /// </remarks>
        public static float[] AllocateWidths(float available, TableColumn[] columns, bool indexColumn)
        {
            var count = columns.Length + (indexColumn ? 1 : 0);
            var widths = new float[count];

            var flexible = 0;
            var used = 0f;

            for (var i = 0; i < columns.Length; i++)
            {
                if (columns[i].Width > 0f)
                {
                    widths[(indexColumn ? 1 : 0) + i] = columns[i].Width;
                    used += columns[i].Width;
                }
                else
                {
                    flexible++;
                }
            }

            if (indexColumn)
            {
                widths[0] = IndexColumnWidth;
                used += IndexColumnWidth;
            }

            if (flexible > 0)
            {
                var each = Mathf.Max(MinimumColumnWidth, (available - used) / flexible);
                var offset = indexColumn ? 1 : 0;

                for (var i = 0; i < columns.Length; i++)
                {
                    if (columns[i].Width <= 0f)
                    {
                        widths[offset + i] = each;
                    }
                }
            }

            return widths;
        }

        /// <summary>画表格：列头一行 + 每行一个元素。</summary>
        /// <param name="property">集合节点（告警要用）。</param>
        /// <param name="array">集合的序列化属性。</param>
        /// <param name="attribute">列表设置（增删按钮的可见性取自它）。</param>
        /// <param name="model">构建期建好的表格模型。</param>
        /// <param name="canResize">此刻允许增删吗。</param>
        /// <param name="rows">
        /// 搜索的行掩码；<c>null</c> 表示不过滤。被筛掉的行不画，
        /// 但**传给 <c>−</c> 的仍是真实下标**。
        /// </param>
        /// <param name="removeIndex">请求删除的下标（原地更新，趟末施加）。</param>
        public static void DrawRows(
            InspectorProperty property,
            SerializedProperty array,
            ListDrawerSettingsAttribute attribute,
            TableModel model,
            bool canResize,
            bool[] rows,
            ref int removeIndex)
        {
            var showRemove = attribute.HideRemoveButton == false;
            var available = EditorGUIUtility.currentViewWidth - EditorGUI.indentLevel * 15f;

            if (showRemove)
            {
                available -= ButtonWidth + CellSpacing;
            }

            var widths = AllocateWidths(available, model.Columns, model.ShowIndexLabels);

            DrawHeaderRow(model, widths);

            var count = array.arraySize;
            for (var i = 0; i < count; i++)
            {
                if (rows != null && !rows[i])
                {
                    continue;
                }

                DrawRow(property, array.GetArrayElementAtIndex(i), model, widths, i, showRemove, canResize, ref removeIndex);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>列头行：序号列（可选）+ 各列名。</summary>
        /// <param name="model">表格模型。</param>
        /// <param name="widths">列宽。</param>
        private static void DrawHeaderRow(TableModel model, float[] widths)
        {
            var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            var x = row.x;
            var offset = 0;

            if (model.ShowIndexLabels)
            {
                EditorGUI.LabelField(new Rect(x, row.y, widths[0], row.height), "#", EditorStyles.miniBoldLabel);
                x += widths[0] + CellSpacing;
                offset = 1;
            }

            for (var i = 0; i < model.Columns.Length; i++)
            {
                EditorGUI.LabelField(
                    new Rect(x, row.y, widths[offset + i], row.height), model.Columns[i].Label, EditorStyles.miniBoldLabel);
                x += widths[offset + i] + CellSpacing;
            }
        }

        /// <summary>画一行：序号格（可选）+ 每列一格 + 行尾的「−」。</summary>
        /// <param name="property">集合节点（告警要用）。</param>
        /// <param name="element">本行的元素。</param>
        /// <param name="model">表格模型。</param>
        /// <param name="widths">列宽。</param>
        /// <param name="index">元素下标。</param>
        /// <param name="showRemove">是否画删除按钮。</param>
        /// <param name="canResize">此刻允许增删吗。</param>
        /// <param name="removeIndex">请求删除的下标（原地更新）。</param>
        private static void DrawRow(
            InspectorProperty property,
            SerializedProperty element,
            TableModel model,
            float[] widths,
            int index,
            bool showRemove,
            bool canResize,
            ref int removeIndex)
        {
            var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            var x = row.x;
            var offset = 0;

            if (model.ShowIndexLabels)
            {
                EditorGUI.LabelField(new Rect(x, row.y, widths[0], row.height), GUIContent.none);
                x += widths[0] + CellSpacing;
                offset = 1;
            }

            for (var i = 0; i < model.Columns.Length; i++)
            {
                var rect = new Rect(x, row.y, widths[offset + i], row.height);
                DrawCell(property, element, model.Columns[i], rect, index);
                x += widths[offset + i] + CellSpacing;
            }

            if (showRemove)
            {
                using (new EditorGUI.DisabledScope(!canResize))
                {
                    if (GUI.Button(
                            new Rect(row.xMax - ButtonWidth, row.y, ButtonWidth, row.height), "−", EditorStyles.miniButton))
                    {
                        removeIndex = index;
                    }
                }
            }
        }

        /// <summary>画一格：取相对属性、单值直接画，取不到或复合类型画占位并各告警一次。</summary>
        /// <param name="property">集合节点（告警要用）。</param>
        /// <param name="element">本行的元素。</param>
        /// <param name="column">这一列。</param>
        /// <param name="rect">该格的矩形。</param>
        /// <param name="index">元素下标（告警文案用）。</param>
        private static void DrawCell(
            InspectorProperty property, SerializedProperty element, TableColumn column, Rect rect, int index)
        {
            var cell = element.FindPropertyRelative(column.Name);

            if (cell == null)
            {
                // 构建期的序列化核对漏掉的极少数情形（例如元素里根本没有这个成员）。
                DrawerWarnings.Once(property, "TableList.missing." + column.Name,
                    $"[XInspector] 表格列「{column.Name}」在元素上找不到对应的序列化属性（元素 {index}），"
                    + "该列已跳过。");
                return;
            }

            if (cell.propertyType == SerializedPropertyType.Generic || cell.isArray)
            {
                // 复合类型不做单元格（那是嵌套类型的展开，另一件事）。**占位而不是藏起来**——
                // 藏了会让人以为数据没了；想彻底去掉这一列，用 [HideInTables] 显式声明。
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.LabelField(rect, "（复合类型，不做单元格）", EditorStyles.miniLabel);
                }

                DrawerWarnings.Once(property, "TableList.compound." + column.Name,
                    $"[XInspector] 表格列「{column.Name}」是复合类型，本包不做单元格绘制；" +
                    "用 [HideInTables] 把它从表格里去掉。");
                return;
            }

            EditorGUI.PropertyField(rect, cell, GUIContent.none, false);
        }

        #endregion
    }
}
