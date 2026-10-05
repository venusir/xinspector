using System;

namespace XInspector
{
    /// <summary>
    /// 把数组与列表画成**表格**：每行一个元素、每列一个元素类型的成员。
    /// <para>
    /// 可以单独用（本包会在构建期为它补一份 <see cref="ListDrawerSettingsAttribute"/>，
    /// 故不需要你手写）；与 <see cref="ListDrawerSettingsAttribute"/> 并存时，
    /// 「显示增删按钮 / 只读」取后者，「索引列 / 恒展开」取本特性。
    /// </para>
    /// <para>
    /// 列的顺序与元素类型的声明顺序一致（基类在前）；列宽用 <see cref="TableColumnWidthAttribute"/>
    /// 指定（不指定则该列弹性分配），<see cref="HideInTablesAttribute"/> 的成员不进表格。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>本包不做</b>（旋钮也相应地不声明）：滚动视图、分页、列宽拖拽、单元格自定义绘制、
    /// 工具栏。元素类型不是复合类型（<c>List&lt;int&gt;</c>、<c>string[]</c>……）时表格不成立，
    /// 退回普通列表绘制并记一条告警。
    /// </remarks>
    /// <example>
    /// <code>
    /// [TableList(ShowIndexLabels = true)]
    /// public List&lt;EnemyWave&gt; waves;
    ///
    /// [Serializable]
    /// public class EnemyWave
    /// {
    ///     [TableColumnWidth(80)] public int level;
    ///     public string name;
    ///     [HideInTables] public string memo;   // 不占表格列
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class TableListAttribute : Attribute
    {
        #region Public API

        /// <summary>为真时最左多一列序号（官方语义：每个元素画一个显示下标的标签）。</summary>
        public bool ShowIndexLabels { get; set; }

        /// <summary>为真时**恒展开**、不画折叠头（官方语义：不能再从标题栏折叠）。</summary>
        public bool AlwaysExpanded { get; set; }

        #endregion
    }
}
