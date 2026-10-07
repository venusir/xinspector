using System;

namespace XInspector
{
    /// <summary>
    /// 把多态引用（<c>[SerializeReference]</c>）字段的**改类型**换成**本包自绘的类型选择器**：
    /// 一行「当前类型名」按钮，点开是候选菜单（与 <c>[TypeDrawerSettings]</c> 同一套菜单）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>不标这个特性时，本包一个字都不动那一行</b>——多态字段仍由 Unity 原生 UI 承担
    /// （改类型用它自己的入口）。标了之后本包接管那一行：按钮显示当前具体类型，菜单只列
    /// **装得进这个槽位、且造得出实例**的类型。
    /// </para>
    /// <para>
    /// <b>只标字段，不标属性。</b> 本包的属性只有经 <c>[ShowInInspector]</c> 的只读反射路径
    /// 才进树，而本特性存在的意义是**换类型**——标在属性上必然是「编译得过但什么都不发生」。
    /// 与 <c>[TypeDrawerSettings]</c> 同款理由。
    /// </para>
    /// <para>
    /// <b>本批只声明三个旋钮</b>（官方的第四个 <c>CreateInstanceFunction</c> 是单参
    /// <c>Type type</c> 的 resolved string，要另一条名字解析通道——下一批与
    /// <c>[TypeSelectorSettings]</c> 的过滤器一起做；<b>不声明</b>，写了编译不过）。
    /// 官方的只读 <c>…IsSet</c> 属性同样不声明：它们只在「默认值来自全局配置」时有意义，
    /// 而本包的默认值就写在字段/属性初始化器里。
    /// </para>
    /// <para>
    /// <b>回退边界：</b>本包只接管「用得到本包」的具体类型（外观不变那条安全阀）。
    /// 槽位里有值、但那个类型用不到本包（或撞上多态守卫）时，这一行**退回 Unity 原生**——
    /// 类型用不到本包那档会在控制台留一条说明（只报一次）；此时下面两个旋钮不生效。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 不标特性：改类型仍用 Unity 原生那一行。
    /// [SerializeReference] public IShape shape;
    ///
    /// // 标了特性：改用本包的选择器（行上显示「Circle （IShape）」）。
    /// [SerializeReference, PolymorphicDrawerSettings(ShowBaseType = true)] public IShape outlined;
    ///
    /// // 赋过一次值之后不许再换类型；选中没有无参构造的类型时告警而不构造。
    /// [SerializeReference, PolymorphicDrawerSettings(
    ///     ReadOnlyIfNotNullReference = true,
    ///     NonDefaultConstructorPreference = NonDefaultConstructorPreference.LogWarning)]
    /// public IShape locked;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class PolymorphicDrawerSettingsAttribute : Attribute
    {
        /// <summary>
        /// <c>true</c> 时，槽位**有值之后**「改类型」那一行变灰——<b>只锁那一行，不锁子字段</b>
        /// （锁整棵子树是 <c>[ReadOnly]</c> 的事；这个旋钮的语义是「赋过一次值之后不许再换类型」）。
        /// 默认为 <c>false</c>。（官方形状：本项是**字段**。）
        /// </summary>
        public bool ReadOnlyIfNotNullReference;

        /// <summary>
        /// <c>true</c> 时在行上显示**基类型**：文本变成「具体类型名 （声明类型名）」
        /// （本包自定格式）。「基类型」取**字段的声明类型**（接口/抽象类那一侧），不是运行时的具体类型。
        /// 默认为 <c>false</c>——与原生那一行只显示类型名对齐。（官方形状：本项是**属性**。）
        /// </summary>
        public bool ShowBaseType { get; set; }

        /// <summary>
        /// 选中类型没有公开无参构造时怎么处置；默认 <see cref="XInspector.NonDefaultConstructorPreference.ConstructIdeal"/>。
        /// （官方形状：本项是**属性**，另带只读的 <c>NonDefaultConstructorPreferenceIsSet</c>——本包不声明后者。）
        /// </summary>
        public NonDefaultConstructorPreference NonDefaultConstructorPreference { get; set; } =
            NonDefaultConstructorPreference.ConstructIdeal;
    }
}
