using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 两个预制体上下文校验特性：<c>[RequiredIn]</c> 与 <c>[DisallowModificationsIn]</c>。
    /// <para>
    /// <b>测不了的</b>：提示框的真实渲染（与全仓一致不测 IMGUI）。故这里钉三样东西——
    /// 链装配与次序、只读门控（处理器装的那个求值器）、以及「该不该报覆盖」那条纯判定。
    /// 提示文本本身只有一句，不另有逻辑。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PrefabValidationTests
    {
        #region Setup / Teardown

        private readonly List<GameObject> _sceneObjects = new List<GameObject>();
        private readonly List<ScriptableObject> _scriptableObjects = new List<ScriptableObject>();
        private readonly List<System.IDisposable> _disposables = new List<System.IDisposable>();

        /// <summary>释放一切、清掉场景对象与临时资产，并复位两张注册表。</summary>
        [TearDown]
        public void TearDown()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }

            _disposables.Clear();

            foreach (var gameObject in _sceneObjects)
            {
                if (gameObject != null)
                {
                    Object.DestroyImmediate(gameObject);
                }
            }

            _sceneObjects.Clear();

            foreach (var scriptableObject in _scriptableObjects)
            {
                if (scriptableObject != null)
                {
                    Object.DestroyImmediate(scriptableObject);
                }
            }

            _scriptableObjects.Clear();

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 链装配

        /// <summary>两个绘制器都配在各自的成员上。</summary>
        [Test]
        public void 两个校验绘制器都在链上()
        {
            var tree = BuildAssetTree();

            AssertDrawer<RequiredInDrawer>(tree, "requiredOnAsset");
            AssertDrawer<DisallowModificationsInDrawer>(tree, "frozenOnAsset");
        }

        /// <summary>
        /// <c>[RequiredIn]</c> 紧挨在 <c>[Required]</c> 之内——两者是同一个字段的两条校验。
        /// </summary>
        [Test]
        public void 上下文校验在普通校验之内()
        {
            var tree = BuildAssetTree();
            var property = Find(tree, "bothRequired");

            Assert.That(IndexOf<RequiredDrawer>(property), Is.LessThan(IndexOf<RequiredInDrawer>(property)),
                "权重 -600 在 -590 之外。");
            Assert.That(IndexOf<RequiredInDrawer>(property), Is.LessThan(IndexOf<UnityFallbackDrawer>(property)));
        }

        #endregion

        #region 只读门控

        /// <summary>
        /// <c>[DisallowModificationsIn]</c> 的禁用那一半由处理器装：处在上下文里就只读。
        /// </summary>
        /// <remarks>
        /// 夹具是场景里的组件，故只造得出「非预制体」这一种上下文；另一种上下文下应为可编辑，
        /// 这里一并钉住（两条一起才说明它真的是**按上下文**生效，而不是恒只读）。
        /// </remarks>
        [Test]
        public void 只读门控跟着上下文走()
        {
            var gameObject = new GameObject("PrefabValidationScene");
            _sceneObjects.Add(gameObject);
            var behaviour = gameObject.AddComponent<PrefabValidationBehaviour>();

            var tree = BuildTree(new SerializedObject(behaviour));

            Assert.That(Find(tree, "frozenInScene").State.IsReadOnly, Is.True, "匹配 NonPrefabInstance → 只读。");
            Assert.That(Find(tree, "frozenInScene").IsVisible, Is.True, "禁用不改可见性。");
            Assert.That(Find(tree, "frozenOnAsset").State.IsReadOnly, Is.False, "不匹配 → 可编辑。");
        }

        #endregion

        #region 覆盖判定

        /// <summary>
        /// 「该不该报覆盖」：单目标 + 有值入口 + 确实是覆盖，三条同时成立才报。
        /// </summary>
        [Test]
        public void 覆盖判定的真值表()
        {
            Assert.That(PrefabModificationRules.ShouldReportOverride(1, true, true), Is.True);

            Assert.That(PrefabModificationRules.ShouldReportOverride(1, true, false), Is.False, "值本来就该是这个样。");
            Assert.That(PrefabModificationRules.ShouldReportOverride(1, false, true), Is.False, "反射成员没有值入口。");
            Assert.That(PrefabModificationRules.ShouldReportOverride(2, true, true), Is.False,
                "多选下 prefabOverride 的语义官方没写明，照它报会给出查不出所以然的结论。");
        }

        #endregion

        #region Private Helpers

        /// <summary>建一棵资产（ScriptableObject）上的树，收尾时自动释放。</summary>
        /// <returns>属性树。</returns>
        private PropertyTree BuildAssetTree()
        {
            var target = ScriptableObject.CreateInstance<PrefabValidationFixture>();
            _scriptableObjects.Add(target);

            return BuildTree(new SerializedObject(target));
        }

        /// <summary>按序列化对象建树，收尾时自动释放。</summary>
        /// <param name="serializedObject">序列化对象。</param>
        /// <returns>属性树。</returns>
        private PropertyTree BuildTree(SerializedObject serializedObject)
        {
            _disposables.Add(serializedObject);

            var tree = PropertyTree.Create(serializedObject);
            _disposables.Add(tree);

            return tree;
        }

        /// <summary>断言某个绘制器在指定成员上、且排在末端之前。</summary>
        /// <typeparam name="T">绘制器类型。</typeparam>
        /// <param name="tree">属性树。</param>
        /// <param name="path">成员路径。</param>
        private static void AssertDrawer<T>(PropertyTree tree, string path) where T : XInspectorDrawer
        {
            var property = Find(tree, path);
            var index = IndexOf<T>(property);

            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"{path} 上应有 {typeof(T).Name}。");
            Assert.That(index, Is.LessThan(IndexOf<UnityFallbackDrawer>(property)),
                $"{path} 上 {typeof(T).Name} 应排在末端之前。");
        }

        /// <summary>在节点的链上查找指定类型绘制器的下标。</summary>
        /// <typeparam name="T">绘制器类型。</typeparam>
        /// <param name="property">目标节点。</param>
        /// <returns>下标；不存在返回 -1。</returns>
        private static int IndexOf<T>(InspectorProperty property) where T : XInspectorDrawer
        {
            var entries = property.Chain.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Drawer is T)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>按路径找成员节点。</summary>
        /// <param name="tree">属性树。</param>
        /// <param name="path">成员路径。</param>
        /// <returns>找到的节点。</returns>
        private static InspectorProperty Find(PropertyTree tree, string path)
        {
            var children = tree.Root.Children;
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i].Path == path)
                {
                    return children[i];
                }
            }

            Assert.Fail($"找不到成员 {path}。");
            return null;
        }

        #endregion
    }

    /// <summary>两个预制体上下文校验特性的链装配宿主。</summary>
    internal sealed class PrefabValidationFixture : ScriptableObject
    {
        /// <summary>只在预制体资产上必填。</summary>
        [RequiredIn(PrefabKind.PrefabAsset)]
        public string requiredOnAsset;

        /// <summary>两条校验同时挂——用来钉次序。</summary>
        [Required]
        [RequiredIn(PrefabKind.PrefabAsset, ErrorMessage = "资产上必须填。")]
        public string bothRequired;

        /// <summary>在预制体资产上禁止修改。</summary>
        [DisallowModificationsIn(PrefabKind.PrefabAsset)]
        public int frozenOnAsset;
    }

    /// <summary>
    /// <see cref="DisallowModificationsInAttribute"/> 只读门控的宿主。
    /// </summary>
    /// <remarks>
    /// 与 <c>PrefabConditionBehaviour</c> 同款：**文件名与类名不一致是刻意的**，
    /// 别把它抽成 <c>PrefabValidationBehaviour.cs</c>——理由（<c>Editor/</c> 目录下的脚本
    /// 不许挂成组件）写在那边的注释里。
    /// </remarks>
    internal sealed class PrefabValidationBehaviour : MonoBehaviour
    {
        /// <summary>在场景里的非预制体对象上禁止修改（当场匹配）。</summary>
        [DisallowModificationsIn(PrefabKind.NonPrefabInstance)]
        public int frozenInScene;

        /// <summary>在预制体资产上禁止修改（场景对象上不匹配）。</summary>
        [DisallowModificationsIn(PrefabKind.PrefabAsset)]
        public int frozenOnAsset;
    }
}
