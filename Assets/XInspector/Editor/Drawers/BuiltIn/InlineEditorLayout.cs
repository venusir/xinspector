using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 内嵌编辑器的摆放与降级决策：全是纯函数，可无头测试。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="PreviewFieldLayout"/> 同一条分工：绘制器只负责画与调下一个，
    /// 「尺寸取多少、要不要滚动、字段画不画」这类判断全在这里，因而能被无 GUI 的单测覆盖。
    /// </remarks>
    internal static class InlineEditorLayout
    {
        #region Constants

        /// <summary>与界面并排画预览时的默认宽度（像素）。</summary>
        /// <remarks>本包自定值——Odin 的默认值在它的偏好设置里，官网核不到。</remarks>
        public const float DefaultPreviewWidth = 64f;

        /// <summary>预览的默认高度（像素）。</summary>
        /// <remarks>同上，本包自定。大预览的默认高度在特性那边（128）。</remarks>
        public const float DefaultPreviewHeight = 64f;

        #endregion

        #region Public API

        /// <summary>
        /// 内嵌内容是否要包进滚动视图。
        /// </summary>
        /// <param name="maxHeight">特性上的最大高度；非正表示不限。</param>
        /// <returns>要包返回 <c>true</c>。</returns>
        public static bool UsesScrollView(float maxHeight)
        {
            return maxHeight > 0f;
        }

        /// <summary>
        /// 预览宽度：非正取默认，且不超过可用宽度。
        /// </summary>
        /// <param name="requested">特性上的宽度；非正表示未指定。</param>
        /// <param name="availableWidth">可用宽度；非正表示未知（此时不夹）。</param>
        /// <returns>实际宽度。</returns>
        /// <remarks>
        /// 与 <see cref="PreviewFieldLayout.PreviewRect"/> 的夹取同理：一个超宽的预览列
        /// 会把编辑器界面挤成一条缝，那等于值改不了。
        /// </remarks>
        public static float ResolvePreviewWidth(float requested, float availableWidth)
        {
            var width = requested > 0f ? requested : DefaultPreviewWidth;
            return availableWidth > 0f ? Mathf.Min(width, availableWidth) : width;
        }

        /// <summary>
        /// 预览高度：非正取默认。
        /// </summary>
        /// <param name="requested">特性上的高度；非正表示未指定。</param>
        /// <returns>实际高度。</returns>
        public static float ResolvePreviewHeight(float requested)
        {
            return requested > 0f ? requested : DefaultPreviewHeight;
        }

        /// <summary>
        /// 预览是否与界面**并排**（左右）。
        /// </summary>
        /// <param name="alignment">预览位置。</param>
        /// <returns>并排返回 <c>true</c>；否则上下堆叠。</returns>
        public static bool IsSideBySide(PreviewAlignment alignment)
        {
            return alignment == PreviewAlignment.Left || alignment == PreviewAlignment.Right;
        }

        /// <summary>
        /// 预览是否排在界面**之前**（左或上）。
        /// </summary>
        /// <param name="alignment">预览位置。</param>
        /// <returns>排在前返回 <c>true</c>。</returns>
        /// <remarks>
        /// 并排与堆叠两种摆法共用这一个判断，于是「预览在左」与「预览在上」自然落在同一支。
        /// </remarks>
        public static bool PreviewComesFirst(PreviewAlignment alignment)
        {
            return alignment == PreviewAlignment.Left || alignment == PreviewAlignment.Top;
        }

        /// <summary>
        /// 能不能内嵌。
        /// </summary>
        /// <param name="hasMultipleDifferentValues">是否为多对象编辑下的混合值。</param>
        /// <param name="hasValue">目标引用是否非空。</param>
        /// <returns>可以内嵌返回 <c>true</c>。</returns>
        /// <remarks>
        /// 混合值一律不内嵌——没有「多个不同对象的编辑器」这种东西，而拿第一个目标的值
        /// 冒充全体正是本包要避免的（与 <c>[MinMaxSlider]</c> 同一条规则）。
        /// </remarks>
        public static bool CanInline(bool hasMultipleDifferentValues, bool hasValue)
        {
            return hasValue && !hasMultipleDifferentValues;
        }

        /// <summary>
        /// 折叠头的初始展开状态。
        /// </summary>
        /// <param name="expandedHasValue">特性上的 <c>Expanded</c> 是否被显式设过。</param>
        /// <param name="expanded">特性上的 <c>Expanded</c> 值。</param>
        /// <returns>初始是否展开。</returns>
        /// <remarks>
        /// 没显式设过时**默认展开**：折叠着的内嵌编辑器等于没画。
        /// </remarks>
        public static bool ResolveInitialExpanded(bool expandedHasValue, bool expanded)
        {
            return !expandedHasValue || expanded;
        }

        /// <summary>
        /// 对象字段那一行怎么画。
        /// </summary>
        /// <param name="hasValue">目标引用是否非空（混合值按非空处理）。</param>
        /// <param name="mode">特性上的对象字段模式。</param>
        /// <returns>绘制计划。</returns>
        /// <remarks>
        /// <para>
        /// 四种模式的区别只在两处：字段画不画、空值时要不要给一行提示。
        /// </para>
        /// <para>
        /// <b>本包与 Odin 的一处差异在最后一行代码里</b>：<see cref="InlineEditorObjectFieldModes.CompletelyHidden"/>
        /// 且值为空时画一行灰字提示，而不是留一片空白——本包不接受「静默地什么都不画」。
        /// 有值时不给提示（内嵌内容就在下面，那一行不是空白）。
        /// </para>
        /// </remarks>
        public static InlineEditorObjectFieldPlan ResolveObjectField(bool hasValue, InlineEditorObjectFieldModes mode)
        {
            switch (mode)
            {
                case InlineEditorObjectFieldModes.Hidden:
                    // 有值就藏起来；值为空时露出来，好让你能赋值。
                    return new InlineEditorObjectFieldPlan(!hasValue, false);

                case InlineEditorObjectFieldModes.CompletelyHidden:
                    // 恒藏。空值时别无他物可画，给一行提示。
                    return new InlineEditorObjectFieldPlan(false, !hasValue);

                default:
                    // Boxed / Foldout：字段照画（装箱与折叠是外面那层的事）。
                    return new InlineEditorObjectFieldPlan(true, false);
            }
        }

        #endregion
    }

    /// <summary>
    /// 内嵌编辑器上方那个对象字段的绘制计划。
    /// </summary>
    internal readonly struct InlineEditorObjectFieldPlan
    {
        /// <summary>是否画出可编辑的对象字段。</summary>
        public readonly bool DrawField;

        /// <summary>是否画「字段被隐藏」的灰字提示。</summary>
        public readonly bool ShowHint;

        /// <summary>构造。</summary>
        /// <param name="drawField">是否画字段。</param>
        /// <param name="showHint">是否画提示。</param>
        public InlineEditorObjectFieldPlan(bool drawField, bool showHint)
        {
            DrawField = drawField;
            ShowHint = showHint;
        }
    }

    /// <summary>
    /// 版本控制锁定判定：内嵌内容要不要置灰。
    /// </summary>
    internal static class InlineEditorVcs
    {
        /// <summary>
        /// 内嵌内容是否该置灰。
        /// </summary>
        /// <param name="isAsset">目标是不是工程内的资产（场景对象、工程外对象都不是）。</param>
        /// <param name="isOpenForEdit">该资产是否可编辑（版本控制插件回答；非资产传 <c>true</c>）。</param>
        /// <returns>该置灰返回 <c>true</c>。</returns>
        /// <remarks>
        /// 两个事实分开传而不是在这里查 <c>AssetDatabase</c>，是为了能无头测试这条规则：
        /// **只有「是资产」且「不可编辑」才算被锁**。非资产（场景对象、运行时对象）一律视为
        /// 可编辑——它们根本不在版本控制的管辖内。开关那一层由调用方短路。
        /// </remarks>
        public static bool IsLockedForEditing(bool isAsset, bool isOpenForEdit)
        {
            return isAsset && !isOpenForEdit;
        }
    }
}
