using System;

namespace XInspector
{
    // 多态选择器（[PolymorphicDrawerSettings]）的「选中类型没有公开无参构造时怎么处置」。
    //
    // Runtime 侧**只放数据**：四档的判据与施行都在编辑器侧的 PolymorphicInstanceFactory 里——
    // 与 PrefabKind（判据在 PrefabContextProbe）、TypeInclusionFilter（判据在 TypeCandidateFilter）同构。
    //
    // 数值是**本包自定**的：官方只给成员名与说明、不给数值（照 PrefabKind 先例）。

    /// <summary>
    /// 多态字段换类型时，**选中类型没有公开无参构造**该怎么处置；四个成员照官方名单。
    /// <para>
    /// 本包的选择器换类型时当场造一个新实例（否则槽位里没有值）。有公开无参构造的类型四档
    /// 一视同仁（全都直接 <c>Activator.CreateInstance</c>）；四档的差别**只在「没有无参构造」那一格**。
    /// </para>
    /// <para>
    /// <b>数值是本包自定的</b>：官网只公布成员名与说明，不给数值。成员名与说明照官方。
    /// </para>
    /// </summary>
    public enum NonDefaultConstructorPreference
    {
        /// <summary>
        /// 挑「最直接」的构造来调：公开实例构造里**参数最少**的一个，参数逐个填默认值
        /// （官方：attempts to find the most straightforward constructor to call,
        /// prioritizing default values）。**默认档**。
        /// </summary>
        ConstructIdeal = 0,

        /// <summary>
        /// 把没有公开无参构造的类型**从候选里剔除**——菜单里根本不出现
        /// （官方：excludes types with non default constructors from the Selector）。
        /// </summary>
        Exclude = 1,

        /// <summary>
        /// **不构造**，只记一条告警（官方：logs a warning instead of constructing the object,
        /// indicating that an attempt was made to construct an object without a default constructor）。
        /// </summary>
        LogWarning = 2,

        /// <summary>
        /// 没有无参构造时用 <c>System.Runtime.Serialization.FormatterServices.GetUninitializedObject</c>
        /// ——造出对象但**不跑构造**（官方原话就点了这个 API）。
        /// </summary>
        PreferUninitialized = 3,
    }
}
