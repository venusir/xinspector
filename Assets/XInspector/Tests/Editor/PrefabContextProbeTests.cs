using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 预制体上下文**薄探测那一半**的真预制体用例。
    /// <para>
    /// 这是本仓第一处往盘上写资产的测试——此前所有 Editor 夹具都是内存里的
    /// <c>ScriptableObject</c>。判定阶梯本身在 <c>PrefabKindResolverTests</c> 里用描述子直测，
    /// 故这里只覆盖「探测层有没有把事实探对」的几个代表情形，贵的情形（模型资产、预览场景）
    /// 留给那一半。
    /// </para>
    /// <para>
    /// 临时资产建在 <c>Assets/__XInspectorPrefabTests__/</c> 下（<c>SaveAsPrefabAsset</c> 的硬要求），
    /// 一次性建目录、逐用例清理产物、收尾删目录；<c>OneTimeSetUp</c> 还会先做一次防御性删除，
    /// 免得上一轮跑崩留下的残留撞名。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PrefabContextProbeTests
    {
        #region Setup / Teardown

        private const string FolderName = "__XInspectorPrefabTests__";
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

        /// <summary>逐用例清理：场景对象与本用例产生的资产。</summary>
        /// <remarks>
        /// 资产**按创建顺序倒着删**：变体引用基预制体、外层预制体嵌着内层预制体，
        /// 先删被依赖的那个会让 Unity 立刻重导入引用方并报「Missing Prefab」错误
        /// ——那是真的错误日志，测试框架会把它算成失败。
        /// </remarks>
        [TearDown]
        public void TearDown()
        {
            DestroySceneObjects();

            for (var i = _assetPaths.Count - 1; i >= 0; i--)
            {
                AssetDatabase.DeleteAsset(_assetPaths[i]);
            }

            _assetPaths.Clear();
        }

        #endregion

        #region 各种上下文

        /// <summary>场景里不属于任何预制体的对象。</summary>
        [Test]
        public void 场景里的普通对象()
        {
            var go = CreateSceneObject("Plain");

            Assert.That(PrefabContextProbe.Resolve(go), Is.EqualTo(PrefabKind.NonPrefabInstance));
            Assert.That(PrefabContextProbe.Resolve(go.transform), Is.EqualTo(PrefabKind.NonPrefabInstance),
                "组件与它所在的 GameObject 同一种上下文。");
        }

        /// <summary>普通预制体资产（根对象与它的组件）。</summary>
        [Test]
        public void 预制体资产()
        {
            var source = CreateSceneObject("AssetSource");
            var path = SavePrefab(source, "Asset");

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(PrefabContextProbe.Resolve(asset), Is.EqualTo(PrefabKind.Regular));
            Assert.That(PrefabContextProbe.Resolve(asset.transform), Is.EqualTo(PrefabKind.Regular));
        }

        /// <summary>变体资产——把实例根存成预制体即得变体。</summary>
        [Test]
        public void 变体资产()
        {
            var basePath = SavePrefab(CreateSceneObject("VariantBase"), "VariantBase");
            var instance = Instantiate(basePath);
            var variantPath = SavePrefab(instance, "Variant");

            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);

            Assert.That(PrefabContextProbe.Resolve(variant), Is.EqualTo(PrefabKind.Variant));
            Assert.That(PrefabContextProbe.Resolve(variant.transform), Is.EqualTo(PrefabKind.Variant),
                "变体资产的子物体也属于这个变体资产，不是「嵌在里面的实例」。");
        }

        /// <summary>场景里的预制体实例。</summary>
        [Test]
        public void 场景里的实例()
        {
            var path = SavePrefab(CreateSceneObject("InstanceSource"), "Instance");
            var instance = Instantiate(path);

            Assert.That(PrefabContextProbe.Resolve(instance), Is.EqualTo(PrefabKind.InstanceInScene));
            Assert.That(PrefabContextProbe.Resolve(instance.transform), Is.EqualTo(PrefabKind.InstanceInScene));
        }

        /// <summary>场景里的实例里再嵌一个实例——里层那个算「在预制体里」。</summary>
        [Test]
        public void 场景里的嵌套实例()
        {
            var innerPath = SavePrefab(CreateSceneObject("InnerSource"), "Inner");

            var outer = CreateSceneObject("OuterSource");
            var innerInstance = Instantiate(innerPath);
            innerInstance.transform.SetParent(outer.transform);

            var outerPath = SavePrefab(outer, "Outer");
            var outerInstance = Instantiate(outerPath);
            var nestedRoot = outerInstance.transform.GetChild(0).gameObject;

            Assert.That(PrefabContextProbe.Resolve(outerInstance), Is.EqualTo(PrefabKind.InstanceInScene),
                "外层那颗是场景实例。");
            Assert.That(PrefabContextProbe.Resolve(nestedRoot), Is.EqualTo(PrefabKind.InstanceInPrefab),
                "里层那颗是嵌套实例。");
        }

        /// <summary>
        /// 缺资产的实例：判据拿不到嵌套信息，一律兜底成场景实例——**不误判成资产**。
        /// </summary>
        [Test]
        public void 缺资产的实例兜底成场景实例()
        {
            var path = SavePrefab(CreateSceneObject("OrphanSource"), "Orphan");
            var instance = Instantiate(path);

            Assert.That(AssetDatabase.DeleteAsset(path), Is.True);
            _assetPaths.Remove(path);

            Assert.That(PrefabUtility.GetPrefabInstanceStatus(instance),
                Is.EqualTo(PrefabInstanceStatus.MissingAsset),
                "前提：这一步之后实例确实成了缺资产实例。");

            Assert.That(PrefabContextProbe.Resolve(instance), Is.EqualTo(PrefabKind.InstanceInScene));
        }

        /// <summary>非 Unity 对象、空引用、非预制体资产（ScriptableObject）都没有上下文。</summary>
        [Test]
        public void 非Unity对象没有上下文()
        {
            var scriptableObject = ScriptableObject.CreateInstance<ScriptableObject>();

            try
            {
                Assert.That(PrefabContextProbe.Resolve(null), Is.EqualTo(PrefabKind.None));
                Assert.That(PrefabContextProbe.Resolve(new object()), Is.EqualTo(PrefabKind.None));
                Assert.That(PrefabContextProbe.Resolve(scriptableObject), Is.EqualTo(PrefabKind.None),
                    "SO 既不是组件也不是 GameObject 实例。");
            }
            finally
            {
                Object.DestroyImmediate(scriptableObject);
            }
        }

        #endregion

        #region 多选语义

        /// <summary>多选要求**全部**目标都匹配；已销毁的目标不参与判定。</summary>
        [Test]
        public void 多选要求全部匹配()
        {
            var first = CreateSceneObject("First");
            var second = CreateSceneObject("Second");
            var assetPath = SavePrefab(CreateSceneObject("MixedSource"), "Mixed");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            Assert.That(PrefabContextProbe.MatchesAll(new Object[] { first, second }, PrefabKind.NonPrefabInstance),
                Is.True);
            Assert.That(PrefabContextProbe.MatchesAll(new Object[] { first, asset }, PrefabKind.NonPrefabInstance),
                Is.False, "混了一个资产就不算全部匹配。");
            Assert.That(PrefabContextProbe.MatchesAll(new Object[] { first, asset }, PrefabKind.All), Is.True,
                "All 什么都能收。");

            var dead = CreateSceneObject("Dead");
            var deadReference = (Object)dead;
            Object.DestroyImmediate(dead);

            Assert.That(PrefabContextProbe.MatchesAll(new Object[] { deadReference, first },
                PrefabKind.NonPrefabInstance), Is.True, "已销毁的目标不参与判定。");
            Assert.That(PrefabContextProbe.MatchesAll(new Object[] { deadReference }, PrefabKind.All), Is.False,
                "一个存活目标都没有 = 不匹配。");
        }

        /// <summary>空目标列表不匹配——没有对象就没有上下文。</summary>
        [Test]
        public void 空目标列表不匹配()
        {
            Assert.That(PrefabContextProbe.MatchesAll(null, PrefabKind.All), Is.False);
            Assert.That(PrefabContextProbe.MatchesAll(new Object[0], PrefabKind.All), Is.False);
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
        /// <param name="source">场景里的对象（普通对象或最外层实例根）。</param>
        /// <param name="fileName">文件名（不带扩展名）。</param>
        /// <returns>资产路径。</returns>
        private string SavePrefab(GameObject source, string fileName)
        {
            var path = TempFolder + "/" + fileName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(source, path);
            _assetPaths.Add(path);
            return path;
        }

        /// <summary>从资产实例化一份到场景里，收尾时自动销毁。</summary>
        /// <param name="path">资产路径。</param>
        /// <returns>实例根。</returns>
        private GameObject Instantiate(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);

            _sceneObjects.Add(instance);
            return instance;
        }

        /// <summary>销毁本用例造出来的场景对象。</summary>
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
