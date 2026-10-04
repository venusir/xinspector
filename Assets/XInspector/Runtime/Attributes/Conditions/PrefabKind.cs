using System;

namespace XInspector
{
    // 预制体上下文族共用的枚举（[ShowIn]/[HideIn]/[EnableIn]/[DisableIn]、[RequiredIn]、
    // [DisallowModificationsIn] 六个特性都吃它）。
    //
    // Runtime 侧**只放数据**：属性本身碰不到 UnityEngine（那条契约由 Tests.Native 编译期强制），
    // 所以「当前处在哪种预制体上下文」的判据只能落在编辑器侧的处理器/绘制器里——
    // 与那四个模式条件（判据是 Application.isPlaying）、三个内嵌环境条件（判据是绘制期深度）同构。
    //
    // 数值是**本包自定**的：官网只给成员名与说明、不给数值（与 TitleAlignments、ButtonSizes 同款处理）。
    // 但这里比它们多一层约束——数值必须是一套**干净的位分解**，因为匹配算法就是求交集：
    //
    //   5 个具体位（一次解析恰好命中其中一个）+ 4 个复合成员（具体位的并集）
    //
    // 于是 PrefabInstance / PrefabAsset / PrefabInstanceAndNonPrefabInstance / All 这些成员
    // 天然可用，不需要任何特判；而 None = 0 与任何位求交都是空，恒不匹配。

    /// <summary>
    /// 被检视对象所处的预制体上下文；六个预制体上下文特性共用它。
    /// <para>
    /// 这是<b>位标志</b>枚举：<see cref="PrefabInstance"/>、<see cref="PrefabAsset"/>、
    /// <see cref="PrefabInstanceAndNonPrefabInstance"/> 与 <see cref="All"/> 都是
    /// 前五个「具体位」的并集，可以用 <c>|</c> 自由组合——
    /// <c>[ShowIn(PrefabKind.Regular | PrefabKind.Variant)]</c> 与
    /// <c>[ShowIn(PrefabKind.PrefabAsset)]</c> 等价。
    /// </para>
    /// <para>
    /// <b>匹配规则是求交集：</b>被检视对象解析出恰好一个具体位，与特性给的位求 <c>&amp;</c>，
    /// 非空即匹配。故 <see cref="None"/>（= 0）<b>恒不匹配</b>（<c>[ShowIn(None)]</c> 恒隐藏），
    /// <see cref="All"/> 恒匹配。
    /// </para>
    /// <para>
    /// <b>数值是本包自定的</b>：官网只公布成员名与说明（文档站按字母序排），不给数值。
    /// 成员名与语义照官方，位分解按「具体位 + 复合成员」重排。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 两处与官方名字面不同、但**刻意不发明新成员**的边界（详见包 README 的「已知限制」）：
    /// 模型预制体（<c>PrefabAssetType.Model</c>）归入 <see cref="Regular"/>——
    /// 归成「不匹配」会让 <c>[ShowIn(PrefabKind.PrefabAsset)]</c> 在模型资产上**静默隐藏**；
    /// 而非 Unity 对象（普通 C# 对象）与非预制体资产（<c>ScriptableObject</c> 等）解析为
    /// <see cref="None"/>——官方对 <see cref="NonPrefabInstance"/> 的定义是
    /// 「场景里的非预制体<b>组件或 GameObject 实例</b>」，这两类都不是。
    /// </remarks>
    [Flags]
    public enum PrefabKind
    {
        /// <summary>
        /// 不属于任何预制体上下文。**与任何位求交都是空**，故恒不匹配。
        /// </summary>
        None = 0,

        /// <summary>
        /// 嵌套在**另一个预制体**里的预制体实例（官方：Instances of prefabs nested inside other prefabs）。
        /// </summary>
        InstanceInPrefab = 1 << 0,

        /// <summary>
        /// **场景里**的预制体实例（官方：Instances of prefabs in scenes）。
        /// </summary>
        InstanceInScene = 1 << 1,

        /// <summary>
        /// 普通预制体资产（官方：Regular prefab assets）。模型预制体资产也归这里。
        /// </summary>
        Regular = 1 << 2,

        /// <summary>
        /// 预制体变体资产（官方：Prefab variant assets）。
        /// </summary>
        Variant = 1 << 3,

        /// <summary>
        /// 场景里**不属于任何预制体**的组件或 GameObject（官方：Non-prefab component
        /// or gameobject instances in scenes）。
        /// </summary>
        NonPrefabInstance = 1 << 4,

        /// <summary>
        /// 预制体实例，无论它在场景里还是嵌套在别的预制体里。
        /// </summary>
        PrefabInstance = InstanceInPrefab | InstanceInScene,

        /// <summary>
        /// 预制体资产，普通与变体都算。
        /// </summary>
        PrefabAsset = Regular | Variant,

        /// <summary>
        /// 预制体实例，外加场景里的非预制体实例。
        /// </summary>
        PrefabInstanceAndNonPrefabInstance = PrefabInstance | NonPrefabInstance,

        /// <summary>
        /// 所有种类。
        /// </summary>
        All = PrefabAsset | PrefabInstanceAndNonPrefabInstance,
    }
}
