using System;

namespace XInspector
{
    /// <summary>
    /// **本包自绘的类型选择器**（<c>[TypeDrawerSettings]</c> 与 <c>[PolymorphicDrawerSettings]</c>）
    /// 上的旋钮：一个候选过滤函数 + 三个显示开关。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>它自己不产生任何选择器。</b> 标在一个字段上、而该字段上既没有 <c>[TypeDrawerSettings]</c>
    /// 也没有 <c>[PolymorphicDrawerSettings]</c> 时，没有任何选择器由本包渲染，这个特性不会生效
    /// ——构建期有一条告警点名（本包不做静默 no-op）。
    /// </para>
    /// <para>
    /// <b><see cref="FilterTypesFunction"/> 是「单参 <c>Type</c>、返回 <c>bool</c>」的方法名</b>
    /// （官方 resolved string 的形态；形参名官方约定为 <c>"type"</c>，本包**按位置调用、不校验名字**）。
    /// 每个候选类型拿进去问一次，<c>true</c> 才进菜单。
    /// 解析失败时**只忽略过滤器**（一条告警；候选不做这道收窄，选择器照常可用）——
    /// 与「特性整个失效」不是一回事。
    /// 嵌套层 / 元素层 / 多态层里找的是**那一层的实例**（与条件族那条「看错对象」的纪律同款）。
    /// <b>多态那条候选路径会把谓词调两次</b>（收窄发生在两处）——请写成纯函数。
    /// </para>
    /// <para>
    /// <b>三个显示开关（默认全为 <c>true</c>）：</b>
    /// <see cref="PreferNamespaces"/> 关掉时类别名改取**程序集简单名**
    /// （本包**默认是命名空间分层**——与 Odin 的默认档相反：Odin 不写就是程序集类别。
    /// 理由：属性在不在不该改变菜单形状）；
    /// <see cref="ShowCategories"/> 关掉时菜单**拍平**（没有搜索框，几百项时不可用）；
    /// <see cref="ShowNoneItem"/> **只做「抑制」**——「（无）」项本来就只在**有值时**出现，
    /// 关掉它只是连那时也不给清空入口（空槽位放一项点了什么都不发生的菜单项，正是本包拒绝声明的那类；
    /// 故这比官方的字面读法窄，README 有说明）。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 只列「具体的类」，且只留满足自己那条规则的类型。
    /// [SerializeReference, TypeDrawerSettings, TypeSelectorSettings(FilterTypesFunction = nameof(Allowed))]
    /// public Type picked;
    ///
    /// private bool Allowed(Type type) =&gt; type.Name.StartsWith("Showcase", StringComparison.Ordinal);
    ///
    /// // 按程序集类别分组（本包默认是命名空间分层）：
    /// [SerializeReference, TypeDrawerSettings, TypeSelectorSettings(PreferNamespaces = false)]
    /// public Type byAssembly;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class TypeSelectorSettingsAttribute : Attribute
    {
        /// <summary>
        /// 过滤函数形参的**官方约定名**（<c>"type"</c>，与官网常量逐字一致）。
        /// </summary>
        /// <remarks>
        /// 本包**不校验**形参名（按位置调用——硬校验会把能用的方法判死），只把它用进解析失败的
        /// 那句说明里，让人知道官方约定长什么样。
        /// </remarks>
        public const string FILTER_TYPES_FUNCTION_NAMED_VALUE = "type";

        /// <summary>
        /// 候选过滤函数的方法名：**单参 <c>Type</c>、返回 <c>bool</c>** 的实例或静态方法。
        /// 不写（或空白）表示不过滤。
        /// </summary>
        /// <remarks>
        /// 官方形状是**字段**（本包逐字保留）。解析失败 → 一条构建期告警 + 该过滤器被忽略。
        /// </remarks>
        public string FilterTypesFunction;

        /// <summary>
        /// <c>false</c> 时类别名取**程序集简单名**而不是命名空间。
        /// 默认 <c>true</c>（命名空间分层）——**与 Odin 的默认档相反**，见类注释。
        /// </summary>
        public bool PreferNamespaces { get; set; } = true;

        /// <summary>
        /// <c>false</c> 时菜单**拍平**（不分子菜单）。默认 <c>true</c>。
        /// </summary>
        /// <remarks>拍平后没有搜索框（菜单是编辑器自带的），几百项的菜单不可用。</remarks>
        public bool ShowCategories { get; set; } = true;

        /// <summary>
        /// <c>false</c> 时**连有值也不给「（无）」清空入口**。默认 <c>true</c>。
        /// </summary>
        /// <remarks>只做「抑制」——空槽位那一半维持本包的既有判据（见类注释）。</remarks>
        public bool ShowNoneItem { get; set; } = true;
    }
}
