using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    // 预制体种类解析的**薄探测那一半**：把 UnityEngine.Object 探成 PrefabContextFacts，
    // 判定本身在 PrefabKindResolver（纯映射、可无头测试）里。
    //
    // 拆开的理由是测试成本：本仓此前的 Editor 夹具全是内存里的 ScriptableObject，
    // 一个都不碰 AssetDatabase；而预制体夹具**必须**往盘上写资产。
    // 于是把「判定」留在可无头测试的一侧，落盘测试只覆盖这一层的几个真实用例。

    /// <summary>
    /// 探测目标对象所处的预制体上下文。
    /// </summary>
    /// <remarks>
    /// 判据每帧现取（不缓存结果）：目标进出隔离编辑模式、被实例化或被断开，
    /// 都应当在下一帧反映出来，而不必重建属性树。
    /// </remarks>
    internal static class PrefabContextProbe
    {
        #region Private Fields

        // 隔离编辑模式里唯一会分配的东西是 PrefabStage.assetPath（string），
        // 而求值器每帧都跑，故给它一格 memo。键是 stage 引用本身，
        // stage 一换（关闭后新开一个是另一个对象）就自然失效，**不需要任何 Reset**——
        // 这与 InlineEditorDrawContext 那种「测试必须复位」的静态状态不同。
        // 判空走 == null（Unity 的重载），故持有的是一个已销毁的 stage 也不会误命中。
        private static PrefabStage _stageMemo;
        private static PrefabAssetType _stageMemoAssetType;

        #endregion

        #region Public API

        /// <summary>
        /// 解析单个目标的预制体上下文。
        /// </summary>
        /// <param name="target">目标对象，可为 <c>null</c> 或 POCO。</param>
        /// <returns>恰好一个具体位；给不出上下文时是 <see cref="PrefabKind.None"/>。</returns>
        public static PrefabKind Resolve(object target)
        {
            return TryDescribe(target, out var facts) ? PrefabKindResolver.Resolve(facts) : PrefabKind.None;
        }

        /// <summary>
        /// 全部**存活**目标是否都落在要求的上下文里。
        /// </summary>
        /// <param name="targets">树的目标列表。</param>
        /// <param name="requested">特性要求的上下文。</param>
        /// <returns>全部匹配返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// 语义是「全部匹配」：多选里混了一个别的上下文的实例，就整体不匹配——
        /// 预制体上下文是整个选择的性质，不是某一个目标的事。
        /// </para>
        /// <para>
        /// 已销毁/为 <c>null</c> 的目标**不参与判定**（判空一律走 <see cref="TargetObjects.IsAlive"/>）；
        /// 一个存活目标都没有时判为不匹配。
        /// </para>
        /// </remarks>
        public static bool MatchesAll(object[] targets, PrefabKind requested)
        {
            if (targets == null || targets.Length == 0)
            {
                return false;
            }

            var anyAlive = false;

            foreach (var target in targets)
            {
                if (!TargetObjects.IsAlive(target))
                {
                    continue;
                }

                anyAlive = true;

                if (!PrefabKindResolver.Matches(Resolve(target), requested))
                {
                    return false;
                }
            }

            return anyAlive;
        }

        #endregion

        #region Private Helpers

        /// <summary>把目标探成一组事实；不是 GameObject/组件时返回 <c>false</c>。</summary>
        /// <param name="target">目标对象。</param>
        /// <param name="facts">探测结果。</param>
        /// <returns>探得动返回 <c>true</c>。</returns>
        private static bool TryDescribe(object target, out PrefabContextFacts facts)
        {
            facts = default;

            if (!(target is Object unity))
            {
                return false;
            }

            var gameObject = unity as GameObject ?? (unity as Component)?.gameObject;

            if (gameObject == null)
            {
                return false;
            }

            // 隔离编辑模式**必须最先判**：那里 GetPrefabAssetType 报的是内容根自己的类型，
            // 而常规预制体的内容根会报 NotAPrefab——故绕道「正在编辑的那个资产」。
            var stagePosition = PrefabStagePosition.NotInStage;
            var stageAssetType = PrefabAssetType.NotAPrefab;
            var stage = PrefabStageUtility.GetPrefabStage(gameObject);

            if (stage != null && stage.IsPartOfPrefabContents(gameObject))
            {
                stagePosition = stage.prefabContentsRoot == gameObject
                    ? PrefabStagePosition.Root
                    : PrefabStagePosition.Content;
                stageAssetType = StageAssetType(stage);
            }

            var nearest = PrefabUtility.GetNearestPrefabInstanceRoot(gameObject);
            var outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
            var scene = gameObject.scene;

            facts = new PrefabContextFacts(
                isGameObjectOrComponent: true,
                stagePosition: stagePosition,
                stageAssetType: stageAssetType,
                isPartOfPrefabInstance: PrefabUtility.IsPartOfPrefabInstance(gameObject),
                // 在不在资产里：IsPersistent 是「存到盘上了」，场景对象（含场景里的实例）为 false。
                // 补一条 IsPartOfPrefabAsset 是防御性的——两条都为假时必定在场景侧。
                isInAsset: EditorUtility.IsPersistent(gameObject) || PrefabUtility.IsPartOfPrefabAsset(gameObject),
                // 变体资产**自身**：它同时满足「是变体」与「实例根就是它自己」。
                // 只看第一个条件会把嵌在变体资产里的其它实例也误认成变体资产。
                isVariantSelf: PrefabUtility.IsPartOfVariantPrefab(gameObject) &&
                               nearest != null &&
                               nearest.transform == gameObject.transform.root,
                // 嵌套实例：最近的实例根与最外层的实例根不是同一个。
                isNestedInstance: nearest != null && outermost != null && nearest != outermost,
                // 预览场景（隔离编辑模式用的那个）不算「在场景里」。
                isInScene: scene.IsValid() && !EditorSceneManager.IsPreviewScene(scene),
                assetType: PrefabUtility.GetPrefabAssetType(gameObject));

            return true;
        }

        /// <summary>隔离编辑模式正在编辑的那个资产的种类（带一格 memo）。</summary>
        /// <param name="stage">当前 stage。</param>
        /// <returns>资产的种类；加载不到时是 <c>NotAPrefab</c>。</returns>
        private static PrefabAssetType StageAssetType(PrefabStage stage)
        {
            if (_stageMemo == null || !ReferenceEquals(_stageMemo, stage))
            {
                _stageMemo = stage;
                _stageMemoAssetType = LoadAssetType(stage.assetPath);
            }

            return _stageMemoAssetType;
        }

        /// <summary>按路径取资产的预制体种类。</summary>
        /// <param name="assetPath">资产路径。</param>
        /// <returns>种类；取不到时是 <c>NotAPrefab</c>。</returns>
        private static PrefabAssetType LoadAssetType(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return PrefabAssetType.NotAPrefab;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);

            return asset == null ? PrefabAssetType.NotAPrefab : PrefabUtility.GetPrefabAssetType(asset);
        }

        #endregion
    }
}
