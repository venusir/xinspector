using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 预制体**隔离编辑模式**（Prefab Stage）下的探测。
    /// <para>
    /// 独立成一个 fixture 而不是并进 <c>PrefabContextProbeTests</c>：stage 开着的时候
    /// 对它的资产做删除是不确定的，故收尾必须**先回到主 stage、再删资产**；
    /// 把这条顺序关在一个 fixture 里，别的用例就完全不必关心 stage 的存在。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PrefabStageProbeTests
    {
        #region Setup / Teardown

        private const string FolderName = "__XInspectorStageTests__";
        private const string TempFolder = "Assets/" + FolderName;

        private readonly List<GameObject> _sceneObjects = new List<GameObject>();
        private readonly List<string> _assetPaths = new List<string>();

        /// <summary>建临时资产目录（先清残留）。</summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            DeleteTempFolder();
            AssetDatabase.CreateFolder("Assets", FolderName);
        }

        /// <summary>销毁场景对象并删掉整个临时目录。</summary>
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            DestroySceneObjects();
            DeleteTempFolder();
        }

        /// <summary>**先退出隔离编辑模式**，再清场景对象与资产——顺序不能反。</summary>
        /// <remarks>
        /// 资产按创建顺序倒着删：变体引用基预制体，先删被依赖的那个会让 Unity
        /// 立刻重导入引用方并报「Missing Prefab」错误。
        /// </remarks>
        [TearDown]
        public void TearDown()
        {
            StageUtility.GoToMainStage();

            DestroySceneObjects();

            for (var i = _assetPaths.Count - 1; i >= 0; i--)
            {
                AssetDatabase.DeleteAsset(_assetPaths[i]);
            }

            _assetPaths.Clear();
        }

        #endregion

        #region 隔离编辑模式

        /// <summary>
        /// 正在编辑的普通预制体：内容根与它的普通子物体算「这个资产」，
        /// 而**不**因为它们身处预览场景就被判成「不在场景里」而失去上下文。
        /// </summary>
        [Test]
        public void 隔离编辑模式里编辑的是普通预制体()
        {
            var path = SavePrefab(CreateSceneObject("StageSource"), "Stage");
            var stage = PrefabStageUtility.OpenPrefab(path);

            Assert.That(stage, Is.Not.Null, "stage 没打开的话后面全是假绿。");
            Assert.That(stage.prefabContentsRoot, Is.Not.Null);

            var contentRoot = stage.prefabContentsRoot;

            Assert.That(PrefabContextProbe.Resolve(contentRoot), Is.EqualTo(PrefabKind.Regular));

            var child = new GameObject("StageChild");
            child.transform.SetParent(contentRoot.transform);

            Assert.That(PrefabContextProbe.Resolve(child), Is.EqualTo(PrefabKind.Regular),
                "内容里的普通子物体是「正在编辑的这个资产」的一部分。");
        }

        /// <summary>
        /// 内容里嵌着的**另一个**预制体实例算 <c>InstanceInPrefab</c>——
        /// 它是「嵌在预制体里的实例」，与被编辑的资产本身必须分开。
        /// </summary>
        [Test]
        public void 隔离编辑模式里的嵌套实例()
        {
            var innerPath = SavePrefab(CreateSceneObject("StageInnerSource"), "StageInner");
            var path = SavePrefab(CreateSceneObject("StageOuterSource"), "StageOuter");

            var stage = PrefabStageUtility.OpenPrefab(path);

            Assert.That(stage, Is.Not.Null, "stage 没打开的话后面全是假绿。");

            var innerInstance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(innerPath));
            innerInstance.transform.SetParent(stage.prefabContentsRoot.transform);

            Assert.That(PrefabContextProbe.Resolve(innerInstance), Is.EqualTo(PrefabKind.InstanceInPrefab));
        }

        /// <summary>
        /// 正在编辑的是**变体**资产：内容根同时满足「是实例」（变体的内容是基预制体的实例），
        /// 故这一条是「stage 必须最先判」的守卫——次序反了它会变成 <c>InstanceInPrefab</c>。
        /// </summary>
        [Test]
        public void 隔离编辑模式里编辑的是变体()
        {
            var basePath = SavePrefab(CreateSceneObject("StageVariantBase"), "StageVariantBase");
            var baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset);
            _sceneObjects.Add(instance);

            var variantPath = TempFolder + "/StageVariant.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            _assetPaths.Add(variantPath);

            var stage = PrefabStageUtility.OpenPrefab(variantPath);

            Assert.That(stage, Is.Not.Null, "stage 没打开的话后面全是假绿。");
            Assert.That(PrefabContextProbe.Resolve(stage.prefabContentsRoot), Is.EqualTo(PrefabKind.Variant));
        }

        #endregion

        #region Private Helpers

        /// <summary>造一个场景对象，收尾时自动销毁。</summary>
        /// <param name="name">对象名。</param>
        /// <returns>新对象。</returns>
        private GameObject CreateSceneObject(string name)
        {
            var gameObject = new GameObject(name);
            _sceneObjects.Add(gameObject);
            return gameObject;
        }

        /// <summary>把对象存成预制体资产，收尾时自动删除。</summary>
        /// <param name="source">场景里的对象。</param>
        /// <param name="fileName">文件名（不带扩展名）。</param>
        /// <returns>资产路径。</returns>
        private string SavePrefab(GameObject source, string fileName)
        {
            var path = TempFolder + "/" + fileName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(source, path);
            _assetPaths.Add(path);
            return path;
        }

        /// <summary>销毁本用例造出来的场景对象（在预览场景里的由 stage 关闭时一并回收）。</summary>
        private void DestroySceneObjects()
        {
            foreach (var gameObject in _sceneObjects)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }

            _sceneObjects.Clear();
        }

        /// <summary>删掉临时资产目录（连带里面的资产）。</summary>
        private static void DeleteTempFolder()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        #endregion
    }
}
