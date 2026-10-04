using UnityEditor;

namespace XInspector.Editor
{
    // 预制体种类解析拆成两半，这里是**纯映射那一半**：
    // 吃一个纯数据描述子（PrefabContextFacts），吐一个 PrefabKind，一行 Unity API 都不调。
    // 另一半是 PrefabContextProbe——把 UnityEngine.Object 探成那个描述子。
    //
    // 为什么值得拆：本仓的既有测试**一个都不碰 AssetDatabase、也不用预制体 API**
    // （所有 Editor 夹具都是内存里的 ScriptableObject），而预制体夹具必须往盘上写资产。
    // 拆开之后，判定阶梯的全部分支（含模型资产、缺资产、隔离编辑模式、预览场景这些
    // 贵或造不出来的情形）都在这一半用描述子直测，只有薄薄的探测层才需要真预制体。
    //
    // 本文件里出现 UnityEditor 的枚举类型（PrefabAssetType）只当**数据**用——
    // 它是 Unity 的取值集合，照着它写死一份平行枚举没有好处。这里没有任何调用。

    /// <summary>
    /// 目标相对于预制体隔离编辑模式（Prefab Stage）的位置。
    /// </summary>
    internal enum PrefabStagePosition
    {
        /// <summary>不在任何隔离编辑模式里。</summary>
        NotInStage,

        /// <summary>在隔离编辑模式的内容里，但不是内容根（即某个子物体）。</summary>
        Content,

        /// <summary>就是隔离编辑模式正在编辑的那个内容根。</summary>
        Root,
    }

    /// <summary>
    /// 一次预制体上下文探测的纯数据结果。
    /// <para>
    /// 每个字段都是一条**事实**（「它在不在资产里」），不含任何判定——判定全在
    /// <see cref="PrefabKindResolver.Resolve"/> 里。构造参数全带默认值，故测试可以只写关心的那几项：
    /// <c>new PrefabContextFacts(isGameObjectOrComponent: true, isInScene: true)</c>。
    /// </para>
    /// </summary>
    internal readonly struct PrefabContextFacts
    {
        /// <summary>目标是不是 GameObject 或组件（POCO、ScriptableObject、材质都不是）。</summary>
        public readonly bool IsGameObjectOrComponent;

        /// <summary>目标相对于隔离编辑模式的位置。</summary>
        public readonly PrefabStagePosition StagePosition;

        /// <summary>隔离编辑模式正在编辑的那个资产的种类。</summary>
        public readonly PrefabAssetType StageAssetType;

        /// <summary>是不是某个预制体实例的一部分（资产内部的实例也算）。</summary>
        public readonly bool IsPartOfPrefabInstance;

        /// <summary>是不是身处**工程资产**里（而不是场景里）。</summary>
        public readonly bool IsInAsset;

        /// <summary>是不是变体资产**自身**（而不是嵌在它里面的另一个实例）。</summary>
        public readonly bool IsVariantSelf;

        /// <summary>是不是嵌套实例（最近的实例根 ≠ 最外层的实例根）。</summary>
        public readonly bool IsNestedInstance;

        /// <summary>在不在一个有效的、非预览的场景里。</summary>
        public readonly bool IsInScene;

        /// <summary>目标自身的预制体资产种类（不属于实例时才有意义）。</summary>
        public readonly PrefabAssetType AssetType;

        /// <summary>
        /// 以各条事实构造。
        /// </summary>
        /// <param name="isGameObjectOrComponent">是不是 GameObject 或组件。</param>
        /// <param name="stagePosition">相对于隔离编辑模式的位置。</param>
        /// <param name="stageAssetType">隔离编辑模式编辑的资产的种类。</param>
        /// <param name="isPartOfPrefabInstance">是不是预制体实例的一部分。</param>
        /// <param name="isInAsset">是不是身处工程资产里。</param>
        /// <param name="isVariantSelf">是不是变体资产自身。</param>
        /// <param name="isNestedInstance">是不是嵌套实例。</param>
        /// <param name="isInScene">在不在有效且非预览的场景里。</param>
        /// <param name="assetType">目标自身的预制体资产种类。</param>
        public PrefabContextFacts(
            bool isGameObjectOrComponent = false,
            PrefabStagePosition stagePosition = PrefabStagePosition.NotInStage,
            PrefabAssetType stageAssetType = PrefabAssetType.NotAPrefab,
            bool isPartOfPrefabInstance = false,
            bool isInAsset = false,
            bool isVariantSelf = false,
            bool isNestedInstance = false,
            bool isInScene = false,
            PrefabAssetType assetType = PrefabAssetType.NotAPrefab)
        {
            IsGameObjectOrComponent = isGameObjectOrComponent;
            StagePosition = stagePosition;
            StageAssetType = stageAssetType;
            IsPartOfPrefabInstance = isPartOfPrefabInstance;
            IsInAsset = isInAsset;
            IsVariantSelf = isVariantSelf;
            IsNestedInstance = isNestedInstance;
            IsInScene = isInScene;
            AssetType = assetType;
        }
    }

    /// <summary>
    /// 预制体上下文的判定阶梯：描述子 → <see cref="PrefabKind"/>。纯函数，可无头测试。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 每次解析返回**恰好一个具体位**（或 <see cref="PrefabKind.None"/>），
    /// 复合成员是具体位的并集，所以匹配就只是求交集——见 <see cref="PrefabKind"/> 的说明。
    /// </para>
    /// <para>
    /// <b>阶梯的次序本身是设计：</b>隔离编辑模式必须最先判，因为那里资产判定是失效的
    /// （常规预制体的内容根会报 <c>NotAPrefab</c>）；故那一段绕道「正在编辑的那个资产」
    /// 判种类，<b>不依赖</b>内容根自身的资产类型。
    /// </para>
    /// <para>
    /// 两处不确定处一律往「不会误判成资产」的一侧兜底：
    /// 缺资产实例的两个实例根查询行为未实证 → 落 <see cref="PrefabKind.InstanceInScene"/>；
    /// 「加进实例但未应用」的对象（官方只保证实例根查询返回 <c>null</c>）→ 落
    /// <see cref="PrefabKind.NonPrefabInstance"/>。
    /// </para>
    /// </remarks>
    internal static class PrefabKindResolver
    {
        #region Public API

        /// <summary>
        /// 按判定阶梯解析出目标所处的上下文。
        /// </summary>
        /// <param name="facts">探测得到的事实。</param>
        /// <returns>恰好一个具体位（或 <see cref="PrefabKind.None"/>）。</returns>
        public static PrefabKind Resolve(in PrefabContextFacts facts)
        {
            // 0. 不是 GameObject/组件：POCO、ScriptableObject、材质、内置资源……
            //    官方对 NonPrefabInstance 的定义是「场景里的非预制体**组件或 GameObject 实例**」，
            //    这些都不是，故没有上下文。
            if (!facts.IsGameObjectOrComponent)
            {
                return PrefabKind.None;
            }

            // A. 隔离编辑模式——必须最先判。
            if (facts.StagePosition == PrefabStagePosition.Root)
            {
                return KindOf(facts.StageAssetType);
            }

            if (facts.StagePosition == PrefabStagePosition.Content)
            {
                // A1 内容里的嵌套实例（变体的内容根也是实例，但它在上面那支已经返回了）。
                return facts.IsPartOfPrefabInstance
                    ? PrefabKind.InstanceInPrefab
                    : KindOf(facts.StageAssetType);
            }

            // B. 属于预制体实例。
            if (facts.IsPartOfPrefabInstance)
            {
                if (facts.IsInAsset)
                {
                    // B1 资产内部的实例：变体资产自身要认出「它是资产」而不是「它是实例」。
                    return facts.IsVariantSelf ? PrefabKind.Variant : PrefabKind.InstanceInPrefab;
                }

                // B2 场景里的实例：嵌套的（最近根 ≠ 最外层根）算「在预制体里」。
                return facts.IsNestedInstance ? PrefabKind.InstanceInPrefab : PrefabKind.InstanceInScene;
            }

            // C. 不属于任何实例的预制体资产（普通预制体资产的根与子物体走这里——
            //    IsPartOfPrefabInstance 对它们为假，那是变体才为真的判据）。
            var assetKind = KindOf(facts.AssetType);
            if (assetKind != PrefabKind.None)
            {
                return assetKind;
            }

            // D. 场景里的非预制体对象。**资产对象到不了这里**（它们的 IsInScene 为假）。
            if (facts.IsInScene)
            {
                return PrefabKind.NonPrefabInstance;
            }

            // E. 其余（预览场景里的非内容对象、既非资产又不在场景的怪对象）。
            return PrefabKind.None;
        }

        /// <summary>
        /// 解析出的上下文与要求的是否匹配——**求交集，非空即匹配**。
        /// </summary>
        /// <param name="current">解析出的上下文。</param>
        /// <param name="requested">特性要求的上下文。</param>
        /// <returns>匹配返回 <c>true</c>。</returns>
        /// <remarks><see cref="PrefabKind.None"/> 与任何值求交都是空，故恒不匹配。</remarks>
        public static bool Matches(PrefabKind current, PrefabKind requested)
        {
            return (current & requested) != 0;
        }

        /// <summary>
        /// 把一个 Unity 的资产种类映射成本包的位。
        /// </summary>
        /// <param name="assetType">Unity 的资产种类。</param>
        /// <returns>对应的位；给不出上下文时是 <see cref="PrefabKind.None"/>。</returns>
        /// <remarks>
        /// <b>模型预制体归入 <see cref="PrefabKind.Regular"/></b>——它是「非变体的预制体资产」，
        /// 与常规预制体同类。这不是笔误：归成「不匹配」会让
        /// <c>[ShowIn(PrefabKind.PrefabAsset)]</c> 在模型资产上**静默隐藏**。
        /// </remarks>
        public static PrefabKind KindOf(PrefabAssetType assetType)
        {
            switch (assetType)
            {
                case PrefabAssetType.Regular:
                case PrefabAssetType.Model:
                    return PrefabKind.Regular;
                case PrefabAssetType.Variant:
                    return PrefabKind.Variant;
                default:
                    return PrefabKind.None;
            }
        }

        #endregion
    }
}
