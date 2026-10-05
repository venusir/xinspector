using System;

namespace XInspector
{
    /// <summary>
    /// 指定该成员在 <see cref="TableListAttribute"/> 表格里的**列宽**（像素）。
    /// <para>
    /// 标在**元素类型的成员**上；不标的列按弹性分配（剩余宽度均分）。
    /// </para>
    /// <para>
    /// <b>与官方的差异：</b>官方还有一个「可拖拽调宽」的重载参数（<c>resizable</c>），本包不做
    /// ——声明了不生效的旋钮比不声明更糟，所以写了它的代码会**编译不过**（这是响的失败）。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [Serializable]
    /// public class Row
    /// {
    ///     [TableColumnWidth(60)] public int id;
    ///     public string name;      // 不标：弹性宽度
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class TableColumnWidthAttribute : Attribute
    {
        #region Public API

        /// <summary>以列宽构造。</summary>
        /// <param name="width">列宽，单位像素。</param>
        public TableColumnWidthAttribute(int width)
        {
            Width = width;
        }

        /// <summary>列宽（像素）。</summary>
        public int Width { get; }

        #endregion
    }

    /// <summary>
    /// 让该成员**不进** <see cref="TableListAttribute"/> 表格的列。
    /// <para>
    /// 标在**元素类型的成员**上。它只在表格里有效——单独用时是惰性的（没有表格就没有列可言），
    /// 因此它不参与自动接管的判据（表格要求列表字段上带 <see cref="TableListAttribute"/> 或
    /// <see cref="ListDrawerSettingsAttribute"/>，那一份判据已经能认出该类型）。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class HideInTablesAttribute : Attribute
    {
    }
}
