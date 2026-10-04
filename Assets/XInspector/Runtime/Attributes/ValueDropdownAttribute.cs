using System;

namespace XInspector
{
    /// <summary>
    /// 把字段画成**下拉框**，选项来自同一个对象上的另一个成员。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>数据源只认序列化数组/List 成员。</b> Odin 的那个字符串参数是 resolved string——
    /// 官方原文是「求值成一个可赋给 <c>IList</c> 的值」，样例里多数是**方法**或**普通属性**。
    /// 本包只认序列化字段，与条件族、<c>[MinMaxSlider]</c> 的动态边界同一条边界。
    /// </para>
    /// <para>
    /// <b>被标注的字段必须是单值。</b> 挂在数组/List 上时本包不做——那要按元素画，
    /// 属集合自绘那一层。因此 Odin 那几个**只对列表有意义**的选项
    /// （<c>IsUniqueList</c>、<c>DrawDropdownForListElements</c>、<c>ExcludeExistingValuesInList</c>、
    /// <c>DisableListAddButtonBehaviour</c>）本包**不声明**：声明了只能是静默 no-op，
    /// 而「编译不过」是响的，正是本包要的。
    /// </para>
    /// <para>
    /// <b>选项的值类型必须与字段类型完全一致。</b> 不做数值互转（<c>int</c> 源配
    /// <c>float</c> 字段这类），因为「按索引复制」在两侧类型不同时是错的。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public string[] difficultyNames = { "简单", "普通", "困难" };
    ///
    /// [ValueDropdown("difficultyNames")]
    /// public string difficulty;
    ///
    /// // 树形：选项里带 "/" 就会分子菜单
    /// public string[] paths = { "武器/剑", "武器/斧", "防具/盾" };
    ///
    /// [ValueDropdown("paths")]
    /// public string equipped;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ValueDropdownAttribute : Attribute
    {
        /// <summary>
        /// 以「选项来源成员名」构造。
        /// </summary>
        /// <param name="valuesGetter">
        /// 提供选项的**序列化成员名**（数组或 <c>List</c>，且与字段类型一致）。
        /// </param>
        public ValueDropdownAttribute(string valuesGetter)
        {
            ValuesGetter = valuesGetter;
        }

        /// <summary>
        /// 提供选项的序列化成员名。
        /// </summary>
        public string ValuesGetter { get; }

        /// <summary>
        /// <c>true</c> 时把下拉框画成一个**小按钮**，并继续绘制内侧的普通控件
        /// （即「下拉选 + 手填」并存）。默认为 <c>false</c>（下拉框占满整行）。
        /// </summary>
        public bool AppendNextDrawer { get; set; }

        /// <summary>
        /// 与 <see cref="AppendNextDrawer"/> 配套：把内侧那个普通控件置为**不可编辑**
        /// （只能从下拉里选）。默认为 <c>false</c>。
        /// </summary>
        public bool DisableGUIInAppendedDrawer { get; set; }

        /// <summary>
        /// <c>true</c> 时把树形选项**拍平成一层**（选项名里带 <c>/</c> 也不分子菜单）。
        /// 默认为 <c>false</c>——与官方一致，默认是树形。
        /// </summary>
        public bool FlattenTreeView { get; set; }

        /// <summary>
        /// <c>true</c> 时选项按名字排序。默认为 <c>false</c>（保持成员里的顺序）。
        /// </summary>
        public bool SortDropdownItems { get; set; }
    }
}
