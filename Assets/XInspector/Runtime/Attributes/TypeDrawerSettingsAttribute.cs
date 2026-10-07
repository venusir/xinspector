using System;

namespace XInspector
{
    /// <summary>
    /// 把 <c>System.Type</c> 字段画成**类型选择器**：一行「当前类型名」按钮，点开是本包自绘的
    /// 候选菜单；候选集用 <see cref="BaseType"/> 与 <see cref="Filter"/> 收窄。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>字段必须加 <c>[SerializeReference]</c>——这是本包与 Odin 的一处差异。</b>
    /// Unity 不序列化裸的 <c>System.Type</c> 字段（实测 <c>FindProperty</c> 为 null），
    /// 加一个 <c>[SerializeReference]</c> 之后它在序列化数据里是<b>托管引用</b>：进得了树、
    /// 写得住、清得掉，选择器才有写回的落点。Odin 用自己的序列化器兜住了这一层，
    /// 本包选择「让使用方多写一个 Unity 自带的特性」而不是自研序列化。
    /// </para>
    /// <para>
    /// <b>只标字段，不标属性。</b> 本包的属性只有经 <c>[ShowInInspector]</c> 的只读反射路径
    /// 才进树，而本特性存在的意义是<b>写</b>——标在属性上必然是「编译得过但什么都不发生」。
    /// 故用法声明收窄到 <see cref="AttributeTargets.Field"/>：<b>编译不过才是响的</b>。
    /// </para>
    /// <para>
    /// <b>候选集是本包自己扫的</b>（Unity 没有公开的候选集入口），只在<b>点击菜单那一刻</b>
    /// 现算：来源是 <c>TypeCache</c> 的「<see cref="BaseType"/> 的派生」，
    /// 再按 <see cref="Filter"/> 求交过滤、剔除编译器生成的噪音。
    /// <see cref="BaseType"/> 为 <c>null</c> 时按 <c>object</c> 收——<b>接口不在其列</b>
    /// （CLR 语义下接口不「派生自」object），要接口请把 <see cref="BaseType"/> 写成那个接口。
    /// </para>
    /// <para>
    /// <b>多选（各目标的值不一致）时退回 Unity 原生的那一行</b>：本包的选择器表达不了
    /// 「各目标类型不同」，画哪个都是拿一个目标冒充全体。原生多态行本来就支持多目标赋值，
    /// 故退回不是能力倒退。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // 注意 [SerializeReference]——裸的 System.Type 字段进不了序列化数据。
    /// [SerializeReference, TypeDrawerSettings(BaseType = typeof(IShape))]
    /// public Type chosen;
    ///
    /// // 只要具体的类与接口（不要抽象类、不要泛型）：
    /// [SerializeReference, TypeDrawerSettings(
    ///     BaseType = typeof(IShape),
    ///     Filter = TypeInclusionFilter.IncludeConcreteTypes | TypeInclusionFilter.IncludeInterfaces)]
    /// public Type narrowed;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class TypeDrawerSettingsAttribute : Attribute
    {
        /// <summary>
        /// 候选的基类型约束（用官方形状：<b>public 字段</b>）。
        /// <para>
        /// <c>null</c>（默认）表示不额外约束——按 <c>object</c> 收，<b>接口不在候选里</b>；
        /// 把基类型写成接口时，实现它的类与派生接口都算派生。
        /// </para>
        /// </summary>
        public Type BaseType;

        /// <summary>
        /// 候选的种类过滤（用官方形状：<b>public 字段</b>）。默认 <see cref="TypeInclusionFilter.IncludeAll"/>。
        /// </summary>
        public TypeInclusionFilter Filter = TypeInclusionFilter.IncludeAll;
    }
}
