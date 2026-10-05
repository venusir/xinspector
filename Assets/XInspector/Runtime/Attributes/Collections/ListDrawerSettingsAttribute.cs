using System;

namespace XInspector
{
    /// <summary>
    /// 自定义数组与列表的呈现：自绘行、增删按钮与索引标签。
    /// <para>
    /// 不标它时，数组整个交给 Unity 的 <c>PropertyField(includeChildren: true)</c>
    /// （外观与原生逐像素一致）；标上它之后由本包自绘——**元素本身仍由原生绘制器逐个画**，
    /// 本包只接管容器、行与增删按钮。
    /// </para>
    /// <para>
    /// <b>本包不做</b>（旋钮也相应地不声明：写了会编译不过，而不是静默无效）：
    /// 拖拽排序、多选、分页、滚动、标题栏 GUI、<c>CustomAddFunction</c> 一族，
    /// 以及用元素某个成员的值当行标签（每帧读字符串会破「绘制路径零分配」的约定）。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [ListDrawerSettings(ShowIndexLabels = true)]
    /// public int[] damageByLevel;
    ///
    /// [ListDrawerSettings(HideAddButton = true, IsReadOnly = true)]
    /// public string[] bakedIds;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ListDrawerSettingsAttribute : Attribute
    {
        #region Public API

        /// <summary>为真时不画底部的「＋」（官方同名旋钮）。</summary>
        public bool HideAddButton { get; set; }

        /// <summary>为真时不画每行的「−」（官方同名旋钮）。</summary>
        public bool HideRemoveButton { get; set; }

        /// <summary>
        /// 为真时**去掉增删等编辑能力，但不把每个元素画灰**——与 <see cref="ReadOnlyAttribute"/>
        /// 的差别正在后半句（官方语义）。
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>为真时每行的标签用元素的下标（默认是 <c>Element N</c>）。</summary>
        public bool ShowIndexLabels { get; set; }

        /// <summary>
        /// 为真时画可折叠的头部；为假时**恒展开**（官方原文：the collection will always be
        /// expanded）。默认 <c>true</c>。
        /// </summary>
        public bool ShowFoldout { get; set; } = true;

        #endregion
    }
}
