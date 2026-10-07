using System;

namespace XInspector
{
    // 类型选择器（[TypeDrawerSettings]）的候选集过滤位。
    //
    // Runtime 侧**只放数据**：判据（把类型归到哪一类、求交）落在编辑器侧的
    // TypeCandidateFilter 里——与 PrefabKind（判据在编辑器侧的 PrefabContextProbe）同构。
    //
    // 数值是**本包自定**的：官方只给成员名与说明、不给数值（与 PrefabKind、TitleAlignments
    // 同款处理）。匹配算法是**求交非空**：每个候选类型算出一个「种类掩码」，与过滤器求 &，
    // 非空即收——于是 None = 0 恒不匹配、IncludeAll 恒匹配，不需要任何特判。
    //
    // 四个具体位（抽象 / 具体 / 泛型 / 接口）互不相交，但**一个类型可以同时命中多位**：
    // 泛型接口既是「泛型」也是「接口」——这是刻意的，位标志的描述能力正在于此。

    /// <summary>
    /// 类型选择器的候选集过滤——哪几类类型算数；六个成员照官方名单。
    /// <para>
    /// 这是<b>位标志</b>枚举，匹配规则是<b>求交非空</b>：候选类型解析出自己的种类掩码，
    /// 与特性给的位求 <c>&amp;</c>，非空即收。故 <see cref="None"/>（= 0）<b>恒不匹配</b>
    /// （候选表恒空），<see cref="IncludeAll"/> 恒匹配。
    /// </para>
    /// <para>
    /// 一个类型可以同时命中多位：<b>泛型接口</b>（<c>IFoo&lt;T&gt;</c>）同时命中
    /// <see cref="IncludeGenerics"/> 与 <see cref="IncludeInterfaces"/>——
    /// 想要「只要非泛型的接口」就写单个 <see cref="IncludeInterfaces"/>
    /// （泛型接口仍会命中它，故这条要配合使用方的判断；本包不提供「排除」位）。
    /// </para>
    /// <para>
    /// <b>数值是本包自定的</b>：官网只公布成员名，不给数值。成员名照官方，位分解按
    /// 「四个具体位 + 复合成员」重排。
    /// </para>
    /// <para>
    /// <b>静态类归 <see cref="IncludeAbstracts"/></b>：IL 里它就是 <c>abstract sealed</c>，
    /// 照 <c>IsAbstract</c> 走、不特判——静态类不能实例化，归抽象比归具体诚实。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>编译器生成的类型（闭包、匿名类型）无条件不进候选</b>——那不在本枚举的描述范围里，
    /// 是编辑器侧候选枚举的固定收窄（<c>TypeCandidateFilter.IsNoise</c>）：它们的名字长成
    /// <c>&lt;&gt;c</c> 那样，出现在菜单里是纯噪音。
    /// </remarks>
    [Flags]
    public enum TypeInclusionFilter
    {
        /// <summary>
        /// 一个都不收。**与任何种类求交都是空**，故候选表恒空（合法输入，不是笔误）。
        /// </summary>
        None = 0,

        /// <summary>
        /// 抽象类（含静态类——IL 里是 <c>abstract sealed</c>）。
        /// </summary>
        IncludeAbstracts = 1 << 0,

        /// <summary>
        /// 具体类型：非抽象类、结构体、枚举、基元类型。
        /// </summary>
        IncludeConcreteTypes = 1 << 1,

        /// <summary>
        /// 泛型：泛型定义与开放构造类型（<c>List&lt;&gt;</c>、<c>IFoo&lt;T&gt;</c>）。
        /// </summary>
        IncludeGenerics = 1 << 2,

        /// <summary>
        /// 接口（含泛型接口——它同时命中 <see cref="IncludeGenerics"/>）。
        /// </summary>
        IncludeInterfaces = 1 << 3,

        /// <summary>
        /// 四类都收——默认值。
        /// </summary>
        IncludeAll = IncludeAbstracts | IncludeConcreteTypes | IncludeGenerics | IncludeInterfaces,
    }
}
