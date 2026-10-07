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
    /// <b><see cref="CreateInstanceFunction"/> 是「换类型时怎么造新实例」的自定义出口</b>：
    /// 单参 <c>Type</c>、返回实例的方法名（官方示例 <c>public object Method(Type type)</c>；
    /// 形参名官方约定 <c>"type"</c>，本包按位置调用）。四种处置：
    /// 解析成功 → 写回**只走它**（<see cref="NonDefaultConstructorPreference"/> 与构造和候选收窄都无关）；
    /// **解析失败（构建期告警）→ 回落内置的造实例**、候选收窄照旧（配置错不该让换类型整个不可用）；
    /// **返回 <c>null</c> 或抛异常 → 告警且不写**（不回落——函数说「不给」时拿内置档造一个，
    /// 那是静默换值的近亲）；**返回的实例不是选中的类型 → 拒绝**（否则「显示的是 X、装进去的是 Y」）。
    /// </para>
    /// <para>
    /// <b>本批只声明这些。</b> 官方的只读 <c>…IsSet</c> 属性不声明：它们只在「默认值来自全局配置」
    /// 时有意义，而本包的默认值就写在字段/属性初始化器里。
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
    ///
    /// // 自定义造实例：换类型/选类型时走自己的方法（返回 null 表示「这个类型不给」）。
    /// [SerializeReference, PolymorphicDrawerSettings(CreateInstanceFunction = nameof(Make))]
    /// public IShape made;
    ///
    /// private object Make(Type type) =&gt; type == typeof(Circle) ? new Circle() : null;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class PolymorphicDrawerSettingsAttribute : Attribute
    {
        /// <summary>
        /// 自定义造实例函数的方法名：**单参 <c>Type</c>、返回实例**的实例或静态方法。
        /// 不写（或空白、解析失败）就走内置的造实例。官方形状是**字段**。
        /// </summary>
        /// <remarks>四种处置见类注释（解析期回落、点击期不回落）。</remarks>
        public string CreateInstanceFunction;

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
