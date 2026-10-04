using NUnit.Framework;
using UnityEditor;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 预制体上下文判定阶梯的**纯映射那一半**：描述子 → <c>PrefabKind</c>。
    /// <para>
    /// 这一半不调任何 Unity API，故能把全部阶梯分支——包括模型资产、缺资产、隔离编辑模式、
    /// 预览场景这些贵或造不出来的情形——都用描述子直测。真正需要落盘预制体夹具的只有
    /// <c>PrefabContextProbeTests</c> 与 <c>PrefabStageProbeTests</c> 两处。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PrefabKindResolverTests
    {
        #region 阶梯

        /// <summary>POCO、ScriptableObject、材质都不是「组件或 GameObject」，没有上下文。</summary>
        [Test]
        public void 不是组件或GameObject时没有上下文()
        {
            Assert.That(PrefabKindResolver.Resolve(new PrefabContextFacts()), Is.EqualTo(PrefabKind.None));

            // 防御性：其余事实全为真也不该翻案——第 0 级先拦。
            var contradictory = new PrefabContextFacts(
                isGameObjectOrComponent: false,
                isPartOfPrefabInstance: true,
                isInScene: true,
                assetType: PrefabAssetType.Regular);

            Assert.That(PrefabKindResolver.Resolve(contradictory), Is.EqualTo(PrefabKind.None));
        }

        /// <summary>场景里不属于任何预制体的对象——最常见的一种。</summary>
        [Test]
        public void 场景里的普通对象()
        {
            var facts = new PrefabContextFacts(isGameObjectOrComponent: true, isInScene: true);

            Assert.That(PrefabKindResolver.Resolve(facts), Is.EqualTo(PrefabKind.NonPrefabInstance));
        }

        /// <summary>场景里的预制体实例。</summary>
        [Test]
        public void 场景里的预制体实例()
        {
            var facts = new PrefabContextFacts(
                isGameObjectOrComponent: true,
                isPartOfPrefabInstance: true,
                isInScene: true);

            Assert.That(PrefabKindResolver.Resolve(facts), Is.EqualTo(PrefabKind.InstanceInScene));
        }

        /// <summary>场景里的**嵌套**实例（最近实例根 ≠ 最外层实例根）算「在预制体里」。</summary>
        [Test]
        public void 场景里的嵌套实例()
        {
            var facts = new PrefabContextFacts(
                isGameObjectOrComponent: true,
                isPartOfPrefabInstance: true,
                isNestedInstance: true,
                isInScene: true);

            Assert.That(PrefabKindResolver.Resolve(facts), Is.EqualTo(PrefabKind.InstanceInPrefab));
        }

        /// <summary>普通预制体资产。</summary>
        [Test]
        public void 普通预制体资产()
        {
            var facts = new PrefabContextFacts(isGameObjectOrComponent: true, assetType: PrefabAssetType.Regular);

            Assert.That(PrefabKindResolver.Resolve(facts), Is.EqualTo(PrefabKind.Regular));
        }

        /// <summary>
        /// 模型预制体资产归 <c>Regular</c>——**偏差，不是笔误**。
        /// </summary>
        /// <remarks>
        /// 官方枚举里没有模型对应的成员。归成「不匹配」会让 <c>[ShowIn(PrefabKind.PrefabAsset)]</c>
        /// 在模型资产上静默隐藏——本仓最忌的一类失效，故宁可让 Regular 的含义宽一格。
        /// </remarks>
        [Test]
        public void 模型资产归入普通预制体()
        {
            var facts = new PrefabContextFacts(isGameObjectOrComponent: true, assetType: PrefabAssetType.Model);

            Assert.That(PrefabKindResolver.Resolve(facts), Is.EqualTo(PrefabKind.Regular));
        }

        /// <summary>变体资产：它同时是「实例」（内容是基预制体的实例），但必须认出「它是资产」。</summary>
        [Test]
        public void 变体资产()
        {
            var facts = new PrefabContextFacts(
                isGameObjectOrComponent: true,
                isPartOfPrefabInstance: true,
                isInAsset: true,
                isVariantSelf: true,
                assetType: PrefabAssetType.Variant);

            Assert.That(PrefabKindResolver.Resolve(facts), Is.EqualTo(PrefabKind.Variant));
        }

        /// <summary>资产内部的嵌套实例：同样是「实例」，但不是变体资产自身。</summary>
        [Test]
        public void 资产内部的嵌套实例()
        {
            var facts = new PrefabContextFacts(
                isGameObjectOrComponent: true,
                isPartOfPrefabInstance: true,
                isInAsset: true,
                assetType: PrefabAssetType.Regular);

            Assert.That(PrefabKindResolver.Resolve(facts), Is.EqualTo(PrefabKind.InstanceInPrefab));
        }

        /// <summary>
        /// 隔离编辑模式的内容根：种类取自**正在编辑的那个资产**，不看内容根自己的资产类型。
        /// </summary>
        /// <remarks>
        /// 这一条同时钉住阶梯的次序：变体的内容根也是「实例」，若不先判 stage 就会被判成实例。
        /// </remarks>
        [Test]
        public void 隔离编辑模式的内容根()
        {
            var regular = new PrefabContextFacts(
                isGameObjectOrComponent: true,
                stagePosition: PrefabStagePosition.Root,
                stageAssetType: PrefabAssetType.Regular);

            Assert.That(PrefabKindResolver.Resolve(regular), Is.EqualTo(PrefabKind.Regular));

            var variantContentRoot = new PrefabContextFacts(
                isGameObjectOrComponent: true,
                stagePosition: PrefabStagePosition.Root,
                stageAssetType: PrefabAssetType.Variant,
                isPartOfPrefabInstance: true,
                assetType: PrefabAssetType.NotAPrefab);

            Assert.That(PrefabKindResolver.Resolve(variantContentRoot), Is.EqualTo(PrefabKind.Variant),
                "内容根即便是「实例」，也该按正在编辑的那个资产算。");
        }

        /// <summary>隔离编辑模式里的普通子物体：算「在编辑这个资产」，不算实例。</summary>
        [Test]
        public void 隔离编辑模式里的子物体()
        {
            var facts = new PrefabContextFacts(
                isGameObjectOrComponent: true,
                stagePosition: PrefabStagePosition.Content,
                stageAssetType: PrefabAssetType.Regular);

            Assert.That(PrefabKindResolver.Resolve(facts), Is.EqualTo(PrefabKind.Regular));
        }

        /// <summary>隔离编辑模式里嵌着的另一个预制体实例，算「在预制体里」。</summary>
        [Test]
        public void 隔离编辑模式里的嵌套实例()
        {
            var facts = new PrefabContextFacts(
                isGameObjectOrComponent: true,
                stagePosition: PrefabStagePosition.Content,
                stageAssetType: PrefabAssetType.Regular,
                isPartOfPrefabInstance: true);

            Assert.That(PrefabKindResolver.Resolve(facts), Is.EqualTo(PrefabKind.InstanceInPrefab));
        }

        /// <summary>缺资产的实例：绕过「在不在资产里」那一支，按场景实例兜底（不误判成资产）。</summary>
        [Test]
        public void 缺资产的实例不误判成资产()
        {
            // 变体资产那条路要有 isVariantSelf 才成立；缺资产时它拿不到，走 InstanceInPrefab。
            var missingInAsset = new PrefabContextFacts(
                isGameObjectOrComponent: true,
                isPartOfPrefabInstance: true,
                isInAsset: true,
                assetType: PrefabAssetType.MissingAsset);

            Assert.That(PrefabKindResolver.Resolve(missingInAsset), Is.EqualTo(PrefabKind.InstanceInPrefab));

            // 场景里的缺资产实例：两个实例根查询给不出嵌套判据时，落 InstanceInScene 而不是资产。
            var missingInScene = new PrefabContextFacts(
                isGameObjectOrComponent: true,
                isPartOfPrefabInstance: true,
                isInScene: true,
                assetType: PrefabAssetType.MissingAsset);

            Assert.That(PrefabKindResolver.Resolve(missingInScene), Is.EqualTo(PrefabKind.InstanceInScene));
        }

        /// <summary>缺资产的资产种类给不出上下文（NotAPrefab / MissingAsset 都是）。</summary>
        [Test]
        public void 给不出资产种类的都不成上下文()
        {
            var missing = new PrefabContextFacts(isGameObjectOrComponent: true, assetType: PrefabAssetType.MissingAsset);
            var notAPrefab = new PrefabContextFacts(isGameObjectOrComponent: true, assetType: PrefabAssetType.NotAPrefab);

            Assert.That(PrefabKindResolver.Resolve(missing), Is.EqualTo(PrefabKind.None));
            Assert.That(PrefabKindResolver.Resolve(notAPrefab), Is.EqualTo(PrefabKind.None),
                "既不在场景里、又不属于资产的对象没有上下文。");
        }

        /// <summary>预览场景（隔离编辑模式用的那个）不算「在场景里」。</summary>
        [Test]
        public void 预览场景里的对象没有上下文()
        {
            var facts = new PrefabContextFacts(isGameObjectOrComponent: true, isInScene: false);

            Assert.That(PrefabKindResolver.Resolve(facts), Is.EqualTo(PrefabKind.None));
        }

        #endregion

        #region 匹配与映射

        /// <summary>匹配就是求交集：复合成员是具体位的并集，故天然可用。</summary>
        [Test]
        public void 匹配是求交集()
        {
            Assert.That(PrefabKindResolver.Matches(PrefabKind.Regular, PrefabKind.PrefabAsset), Is.True);
            Assert.That(PrefabKindResolver.Matches(PrefabKind.Variant, PrefabKind.PrefabAsset), Is.True);
            Assert.That(PrefabKindResolver.Matches(PrefabKind.InstanceInScene, PrefabKind.PrefabAsset), Is.False);
            Assert.That(PrefabKindResolver.Matches(PrefabKind.InstanceInScene, PrefabKind.PrefabInstance), Is.True);
            Assert.That(PrefabKindResolver.Matches(PrefabKind.InstanceInPrefab, PrefabKind.PrefabInstance), Is.True);
            Assert.That(PrefabKindResolver.Matches(PrefabKind.NonPrefabInstance, PrefabKind.PrefabInstance), Is.False);
            Assert.That(PrefabKindResolver.Matches(
                PrefabKind.NonPrefabInstance, PrefabKind.PrefabInstanceAndNonPrefabInstance), Is.True);

            Assert.That(PrefabKindResolver.Matches(PrefabKind.Regular, PrefabKind.All), Is.True);
            Assert.That(PrefabKindResolver.Matches(PrefabKind.NonPrefabInstance, PrefabKind.All), Is.True);
        }

        /// <summary><c>None</c> 恒不匹配——包括与 <c>All</c> 求交。</summary>
        [Test]
        public void None恒不匹配()
        {
            Assert.That(PrefabKindResolver.Matches(PrefabKind.None, PrefabKind.All), Is.False);
            Assert.That(PrefabKindResolver.Matches(PrefabKind.Regular, PrefabKind.None), Is.False);
            Assert.That(PrefabKindResolver.Matches(PrefabKind.None, PrefabKind.None), Is.False);
        }

        /// <summary>资产种类到位的映射：三种给出位，两种给不出。</summary>
        [Test]
        public void 资产种类的映射()
        {
            Assert.That(PrefabKindResolver.KindOf(PrefabAssetType.Regular), Is.EqualTo(PrefabKind.Regular));
            Assert.That(PrefabKindResolver.KindOf(PrefabAssetType.Model), Is.EqualTo(PrefabKind.Regular));
            Assert.That(PrefabKindResolver.KindOf(PrefabAssetType.Variant), Is.EqualTo(PrefabKind.Variant));
            Assert.That(PrefabKindResolver.KindOf(PrefabAssetType.NotAPrefab), Is.EqualTo(PrefabKind.None));
            Assert.That(PrefabKindResolver.KindOf(PrefabAssetType.MissingAsset), Is.EqualTo(PrefabKind.None));
        }

        #endregion
    }
}
