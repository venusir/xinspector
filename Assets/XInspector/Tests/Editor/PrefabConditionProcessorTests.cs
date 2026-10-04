using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 预制体上下文条件族的处理器接线：判据落在 <c>PropertyState</c> 的两个求值器上，
    /// 而它读的是**树的目标列表**——故夹具必须是真实的组件（上下文属于对象，不属于字段）。
    /// <para>
    /// 这里不碰 GUI：可见性与只读性本来就是「每帧求值的委托」，
    /// 于是「带这些特性的字段在场景对象上表现如何」可以在无头环境里完整复现。
    /// </para>
    /// <para>
    /// <b>覆盖边界（有意划的，不是漏了）：</b> 本 fixture 只能造出「场景里的非预制体对象」这一种
    /// 上下文——判据本身在 <c>PrefabContextProbeTests</c> 里用真预制体资产、变体、场景实例、
    /// 嵌套实例、缺资产实例与多选混合逐条钉着，这里只验「处理器有没有把那条判据接上、
    /// 方向有没有接反」。接不上的原因是 Unity 的硬约束，见下面 <c>PrefabConditionBehaviour</c> 的说明。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PrefabConditionProcessorTests
    {
        #region Setup / Teardown

        private readonly List<GameObject> _sceneObjects = new List<GameObject>();
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        /// <summary>销毁场景对象、释放树与序列化对象，并复位两张注册表。</summary>
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
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }

            _sceneObjects.Clear();

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 两个方向

        /// <summary>
        /// 场景对象上：<c>NonPrefabInstance</c> 那一面生效、<c>PrefabAsset</c> 那一面不生效。
        /// </summary>
        /// <remarks>
        /// 四个处理器的取反方向在这一条里全被覆盖到——展示/隐藏两族各露出「匹配」与「不匹配」
        /// 的一面：<c>[ShowIn(PrefabAsset)]</c> 不匹配故藏起来，<c>[HideIn(PrefabAsset)]</c>
        /// 不匹配故照常显示，而 <c>[EnableIn]/[DisableIn(NonPrefabInstance)]</c> 恰好相反。
        /// </remarks>
        [Test]
        public void 场景对象上只有非预制体那一组生效()
        {
            var tree = BuildTree(CreateSceneComponent("Scene"));

            Assert.That(Find(tree, "plain").IsVisible, Is.True, "没有条件的成员照常。");
            Assert.That(Find(tree, "onlyOnAsset").IsVisible, Is.False, "只在资产上显示的，场景里不显示。");
            Assert.That(Find(tree, "notOnAsset").IsVisible, Is.True, "在资产上隐藏的，场景里照常。");

            Assert.That(Find(tree, "editableInScene").State.IsReadOnly, Is.False, "这一族匹配 NonPrefabInstance。");
            Assert.That(Find(tree, "readonlyInScene").State.IsReadOnly, Is.True, "在非预制体上下文里只读。");
            Assert.That(Find(tree, "onlyOnAsset").State.IsReadOnly, Is.False, "禁用族与可见性族互不干扰。");
        }

        /// <summary>
        /// 多选且**每一个**目标都落在要求里时才算匹配。
        /// </summary>
        /// <remarks>
        /// 反过来的那一半（混了一个别的上下文就不匹配）需要两个不同上下文的目标，
        /// 而本 fixture 造不出第二种上下文——那条在 <c>PrefabContextProbeTests.多选要求全部匹配</c>
        /// 里用「场景对象 + 预制体资产」钉着。这里钉的是「全部匹配时不误判成不匹配」。
        /// </remarks>
        [Test]
        public void 多选时每个目标都匹配才算匹配()
        {
            var tree = BuildTree(new SerializedObject(new UnityEngine.Object[]
            {
                CreateSceneComponent("SceneOne"),
                CreateSceneComponent("SceneTwo"),
            }));

            Assert.That(Find(tree, "editableInScene").State.IsReadOnly, Is.False, "两个都匹配。");
            Assert.That(Find(tree, "readonlyInScene").State.IsReadOnly, Is.True);
            Assert.That(Find(tree, "onlyOnAsset").IsVisible, Is.False);
            Assert.That(Find(tree, "notOnAsset").IsVisible, Is.True);
        }

        #endregion

        #region 进程边界

        /// <summary>
        /// 四个条件族都是**处理器专有**——成员链上只有末端绘制器，没有它们的位置。
        /// </summary>
        /// <remarks>
        /// 与内嵌环境三兄弟同一条守卫：判断不产出像素，塞进绘制器只会让绘制器
        /// 既画东西又做决策。
        /// </remarks>
        [Test]
        public void 四个条件族不进绘制器链()
        {
            var tree = BuildTree(CreateSceneComponent("Chain"));

            foreach (var path in new[] { "plain", "onlyOnAsset", "notOnAsset", "editableInScene", "readonlyInScene" })
            {
                Assert.That(Find(tree, path).Chain.Count, Is.EqualTo(1), $"{path} 只该有末端。");
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>在场景里造一个带夹具组件的对象。</summary>
        /// <param name="name">对象名。</param>
        /// <returns>组件。</returns>
        private PrefabConditionBehaviour CreateSceneComponent(string name)
        {
            var gameObject = new GameObject(name);
            _sceneObjects.Add(gameObject);
            return gameObject.AddComponent<PrefabConditionBehaviour>();
        }

        /// <summary>按组件建树，收尾时自动释放。</summary>
        /// <param name="component">目标组件。</param>
        /// <returns>属性树。</returns>
        private PropertyTree BuildTree(Component component)
        {
            return BuildTree(new SerializedObject(component));
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

    /// <summary>
    /// 预制体上下文条件族的测试宿主。
    /// </summary>
    /// <remarks>
    /// <b>刻意与测试类同文件、且文件名与类名不一致</b>——别把它抽成
    /// <c>PrefabConditionBehaviour.cs</c>。Unity 有两道规矩在这里打架：
    /// 其一，脚本进了资产就得靠 MonoScript（文件名 ↔ 类名）解析；
    /// 其二，<b>`Editor/` 目录下的脚本一律是「编辑器脚本」，不许挂成组件</b>
    /// （实测原话：<c>Can't add script behaviour '…' because it is an editor script.
    /// To attach a script it needs to be outside the 'Editor' folder.</c>）。
    /// 测试程序集是 <c>includePlatforms: ["Editor"]</c> 的，两道规矩同时命中的结果是：
    /// 类名对上文件名的版本**挂都挂不上**，而对不上的版本能挂进内存里的对象。
    /// 本仓既有的 <c>ChildOnlyBehaviour</c> 正是靠后者成立的。
    /// </remarks>
    internal sealed class PrefabConditionBehaviour : MonoBehaviour
    {
        /// <summary>纯对照。</summary>
        public int plain = 1;

        /// <summary>只在预制体资产上显示。</summary>
        [ShowIn(PrefabKind.PrefabAsset)]
        public int onlyOnAsset = 2;

        /// <summary>在预制体资产上隐藏。</summary>
        [HideIn(PrefabKind.PrefabAsset)]
        public int notOnAsset = 3;

        /// <summary>只在场景里的非预制体对象上可编辑。</summary>
        [EnableIn(PrefabKind.NonPrefabInstance)]
        public int editableInScene = 4;

        /// <summary>在场景里的非预制体对象上只读。</summary>
        [DisableIn(PrefabKind.NonPrefabInstance)]
        public int readonlyInScene = 5;
    }
}
