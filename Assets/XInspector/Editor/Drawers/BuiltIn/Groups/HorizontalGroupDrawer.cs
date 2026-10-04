using System;
using UnityEditor;
using UnityEngine;
using XInspector.Internal;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="HorizontalGroupAttribute"/>：把子节点排成一行，每格按宽度分数分配。
    /// <para>
    /// 权重 <c>-100</c>：分组带里**最内**的一档。行作用域要直接包住子节点列表，
    /// 中间再夹一层「不调下一个」的绘制器就会让行失效。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 它不自己驱动子节点，而是把逐格宽度装进 <see cref="GroupChildrenLayout"/>，
    /// 由末端 <see cref="ChildrenDrawer"/> 消费——见那个类的说明。
    /// </remarks>
    [DrawerPriority(-100d)]
    internal sealed class HorizontalGroupDrawer : AttributeDrawer<HorizontalGroupAttribute>
    {
        #region Private Fields

        /// <summary>自动标签宽度的占比与上下限。</summary>
        private const float AutoLabelRatio = 0.4f;

        /// <summary>自动标签宽度的下限（像素）。</summary>
        private const float AutoLabelMin = 40f;

        /// <summary>自动标签宽度的上限（像素）。</summary>
        private const float AutoLabelMax = 120f;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, HorizontalGroupAttribute attribute, GUIContent label)
        {
            var layout = property.State.GetOrCreate<GroupChildrenLayout>();

            // 行内可用宽度：在当前布局组里取一个「高 0、撑满宽」的矩形。
            // 它天然扣掉了缩进、盒子的内边距与滚动条——比拿窗口宽度近似准得多。
            var available = GUILayoutUtility.GetRect(1f, 0f, GUILayout.ExpandWidth(true)).width
                            - attribute.MarginLeft - attribute.MarginRight
                            - attribute.PaddingLeft - attribute.PaddingRight;

            InstallPolicy(property, attribute, layout, available);

            if (!string.IsNullOrWhiteSpace(attribute.Title))
            {
                EditorGUILayout.LabelField(attribute.Title, EditorStyles.boldLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSpace(attribute.MarginLeft);
                DrawSpace(attribute.PaddingLeft);

                CallNextDrawer(property, label);

                DrawSpace(attribute.PaddingRight);
                DrawSpace(attribute.MarginRight);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>算好逐格宽度与标签宽度，装进策略对象。</summary>
        /// <param name="property">分组节点。</param>
        /// <param name="attribute">水平分组特性。</param>
        /// <param name="layout">策略对象（就地复用）。</param>
        /// <param name="available">行内可用宽度（像素）。</param>
        private static void InstallPolicy(
            InspectorProperty property,
            HorizontalGroupAttribute attribute,
            GroupChildrenLayout layout,
            float available)
        {
            var children = property.Children;
            var count = children.Count;

            layout.EnsureCapacity(count);

            for (var i = 0; i < count; i++)
            {
                // 每个成员自己的那份声明带着它的宽度分数；嵌套分组节点没有，按「未指定」处理。
                var child = children[i].Attributes.Get<HorizontalGroupAttribute>();

                layout.ScratchFractions[i] = child != null ? child.Width : 0f;
                layout.ScratchMinWidths[i] = child != null ? child.MinWidth : 0f;
                layout.ScratchMaxWidths[i] = child != null ? child.MaxWidth : 0f;
            }

            var widths = HorizontalGroupWeights.Resolve(
                layout.ScratchFractions,
                layout.ScratchMinWidths,
                layout.ScratchMaxWidths,
                available,
                attribute.Gap);

            Array.Copy(widths, layout.CellWidths, count);

            for (var i = 0; i < count; i++)
            {
                var child = children[i].Attributes.Get<HorizontalGroupAttribute>();

                layout.CellLabelWidths[i] = LabelWidthFor(
                    child != null ? child.LabelWidth : 0f,
                    child != null && child.DisableAutomaticLabelWidth,
                    layout.CellWidths[i]);
            }

            layout.HasPolicy = true;
            layout.OnlyChildIndex = GroupChildrenLayout.AllChildren;
            layout.CellGap = attribute.Gap;
        }

        /// <summary>
        /// 算一格里的标签宽度。
        /// </summary>
        /// <param name="declared">特性上显式给的标签宽度；非正值表示没给。</param>
        /// <param name="disabled">是否关掉了自动折算。</param>
        /// <param name="cellWidth">本格宽度（像素）。</param>
        /// <returns>标签宽度；0 表示不调整。</returns>
        /// <remarks>
        /// 自动折算是为了让窄格子里的标签别吃掉整格：Inspector 宿主按**整页宽**设的
        /// <c>labelWidth</c> 放进 1/3 宽的格子会把值控件挤成一条缝。
        /// </remarks>
        private static float LabelWidthFor(float declared, bool disabled, float cellWidth)
        {
            if (declared > 0f)
            {
                return declared;
            }

            if (disabled || cellWidth <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp(cellWidth * AutoLabelRatio, AutoLabelMin, AutoLabelMax);
        }

        /// <summary>画一段间距，非正值跳过。</summary>
        /// <param name="pixels">间距（像素）。</param>
        private static void DrawSpace(float pixels)
        {
            if (pixels > 0f)
            {
                GUILayout.Space(pixels);
            }
        }

        #endregion
    }
}
