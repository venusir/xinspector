using System;

namespace XInspector
{
    /// <summary>
    /// 把字段画成**下拉框**，选项来自同一个对象上的另一个成员。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>数据源有两个形态。</b> 其一是**序列化**的数组 / <c>List</c> 成员（选项就是它的元素）；
    /// 其二是声明类型**实现 <c>IList</c>** 的普通字段 / 属性 / 无参方法——Odin 那个字符串参数
    /// 是 resolved string，官方原文是「求值成一个可赋给 <c>IList</c> 的值」，样例里多数是
    /// **方法**或**普通属性**。名字解析走与条件族、<c>[MinMaxSlider]</c> 动态边界同一条阶梯
    /// （嵌套层里先找**同层**的，找不到再回落根上的绝对名，最后才看反射成员）。
    /// </para>
    /// <para>
    /// <b>只实现 <c>IEnumerable</c> 的源不收</b>（<c>HashSet</c>、LINQ 查询、<c>Dictionary.Values</c>…），
    /// 且会**明确告警**而不是静默。理由是一条具体的坑：<c>string</c> 也只实现
    /// <c>IEnumerable&lt;char&gt;</c>——放宽会把一个字符串字段静默变成「字符选项表」。
    /// 同理，声明成 <c>IList&lt;T&gt;</c>（泛型接口本身不继承非泛型 <c>IList</c>）的成员会被拒绝，
    /// 改声明成 <c>List&lt;T&gt;</c> 或数组即可。
    /// </para>
    /// <para>
    /// <b>被标注的字段必须是单值。</b> 挂在数组/List 上时本包不做——那要按元素画，
    /// 属集合自绘那一层。因此 Odin 那几个**只对列表有意义**的选项
    /// （<c>IsUniqueList</c>、<c>DrawDropdownForListElements</c>、<c>ExcludeExistingValuesInList</c>、
    /// <c>DisableListAddButtonBehaviour</c>）本包**不声明**：声明了只能是静默 no-op，
    /// 而「编译不过」是响的，正是本包要的。
    /// </para>
    /// <para>
    /// <b>选项的值类型必须与字段类型一致。</b> 不做数值互转（<c>int</c> 源配
    /// <c>float</c> 字段这类）；枚举还要求**成员名与顺序完全一致**——不符则拒绝这次选择并告警，
    /// 绝不按索引硬写。反射源那个形态里，无参方法在**每次弹出菜单时**被调用一次
    /// （不是每帧），有副作用或开销的方法请自重。
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
    ///
    /// // 反射源：不必是序列化成员，方法也行
    /// [ValueDropdown(nameof(GetLevels))]
    /// public int level;
    ///
    /// private List&lt;int&gt; GetLevels() =&gt; new List&lt;int&gt; { 1, 10, 20 };
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ValueDropdownAttribute : Attribute
    {
        /// <summary>
        /// 以「选项来源成员名」构造。
        /// </summary>
        /// <param name="valuesGetter">
        /// 提供选项的成员名。可以是**序列化**的数组 / <c>List</c>，也可以是声明类型实现
        /// <c>IList</c> 的普通字段 / 属性 / 无参方法；元素类型须与字段类型一致。
        /// </param>
        public ValueDropdownAttribute(string valuesGetter)
        {
            ValuesGetter = valuesGetter;
        }

        /// <summary>
        /// 提供选项的成员名（序列化数组 / List，或实现 <c>IList</c> 的字段 / 属性 / 无参方法）。
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
