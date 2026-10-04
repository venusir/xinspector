using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="ButtonGroupAttribute"/>：把一组按钮排成**一行**，每格等分宽度。
    /// <para>
    /// 权重 <c>-100</c>：与水平分组同档，是分组带里最内的一档——行作用域要直接包住子节点列表，
    /// 中间再夹一层「不调下一个」的绘制器就会让行失效。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 等分靠按钮自己 <c>ExpandWidth</c> 伸展，故这里不必像水平分组那样算逐格宽度：
    /// 按钮组的每一格都是同一种东西（按钮），没有「这一格该占几分」的问题。
    /// 需要按标签宽度排布的是 <see cref="ResponsiveButtonGroupAttribute"/>。
    /// </remarks>
    [DrawerPriority(-100d)]
    internal sealed class ButtonGroupDrawer : AttributeDrawer<ButtonGroupAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property,
            ButtonGroupAttribute attribute,
            GUIContent label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                CallNextDrawer(property, label);
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="ResponsiveButtonGroupAttribute"/>：按可用宽度**折行**的按钮带。
    /// <para>
    /// 它不算逐格宽度就算不出行，而分行必须在画各格之间换作用域——于是这里只装策略，
    /// 真正开关作用域的是末端（见 <see cref="GroupChildrenLayout.RowsManagedByTerminal"/>）。
    /// 分行规则本身是纯函数 <see cref="ResponsiveButtonRows"/>，可无头测试。
    /// </para>
    /// </summary>
    [DrawerPriority(-100d)]
    internal sealed class ResponsiveButtonGroupDrawer : AttributeDrawer<ResponsiveButtonGroupAttribute>
    {
        #region Private Fields

        /// <summary>格间距（像素）。</summary>
        private const float Gap = 2f;

        /// <summary>测量标签宽度时给按钮留的边距（像素）。</summary>
        private const float Padding = 6f;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property,
            ResponsiveButtonGroupAttribute attribute,
            GUIContent label)
        {
            var layout = property.State.GetOrCreate<GroupChildrenLayout>();
            var children = property.Children;
            var count = children.Count;

            layout.EnsureCapacity(count);

            // 行内可用宽度：在当前布局组里取一个「高 0、撑满宽」的矩形，与水平分组同款做法
            // ——它天然扣掉了缩进、盒子的内边距与滚动条。
            var available = GUILayoutUtility.GetRect(1f, 0f, GUILayout.ExpandWidth(true)).width;

            for (var i = 0; i < count; i++)
            {
                layout.CellWidths[i] = Measure(children[i]);
            }

            layout.RowCount = ResponsiveButtonRows.Resolve(
                layout.CellWidths, count, Gap, available, attribute.UniformLayout, layout.CellRows);

            layout.HasPolicy = true;
            layout.RowsManagedByTerminal = true;
            layout.OnlyChildIndex = GroupChildrenLayout.AllChildren;
            layout.CellGap = Gap;

            CallNextDrawer(property, label);
        }

        #endregion

        #region Private Helpers

        /// <summary>量一个按钮要多宽。</summary>
        /// <param name="child">按钮节点。</param>
        /// <returns>宽度（像素），已含边距。</returns>
        /// <remarks>
        /// 用节点自己的 <see cref="InspectorProperty.Label"/>：<c>[Button("名字")]</c> 是通过标签通道
        /// 生效的，故这里量到的就是按钮上真正会写的字。
        /// </remarks>
        private static float Measure(InspectorProperty child)
        {
            return EditorStyles.miniButton.CalcSize(child.Label).x + Padding;
        }

        #endregion
    }
}
