using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 元素层的对账与重建：长度变了整层重建、长度没变不重建、旧子树被释放、
    /// 重建出来的节点照样带链与处理器，以及跨趟消费者（搜索命中集、重置名单）把元素子树排掉。
    /// <para>
    /// 不测 IMGUI——对账与重建全是可无头调用的纯逻辑（<see cref="CollectionElementSync"/>）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class CollectionElementRebuildTests
    {
        #region Fixture

        /// <summary>复位静态门面：建树会初始化绘制器与处理器两张注册表。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 对账

        /// <summary>长度变了 → 整层重建：节点数与路径都对上，新节点也能回到所属的树。</summary>
        [Test]
        public void 长度变化后对账重建()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementRebuildFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");

                SetArraySize(target, tree, "items", 4);

                Assert.That(CollectionElementSync.Reconcile(items), Is.True, "长度对不上就该重建。");
                Assert.That(items.Children.Count, Is.EqualTo(4));
                Assert.That(items.Children[3].Path, Is.EqualTo("items.Array.data[3]"));
                Assert.That(items.Children[3].Owner, Is.SameAs(tree), "Owner 由 AddChild 从父节点继承。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>长度没变 → 不重建：节点对象还是那一个（证明不是每帧重建）。</summary>
        [Test]
        public void 长度不变时不重建()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementRebuildFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");
                var before = items.Children[0];

                Assert.That(CollectionElementSync.Reconcile(items), Is.False);
                Assert.That(CollectionElementSync.Reconcile(items), Is.False);
                Assert.That(items.Children[0], Is.SameAs(before));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 结构改过（增删**真的落地**）就重建——哪怕净长度恰好没变：旧节点里的
        /// <c>SerializedProperty</c> 句柄不再可信，该换一批新的。
        /// </summary>
        [Test]
        public void 结构改过就重建()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementRebuildFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");
                var before = items.Children[0];

                CollectionElementExpansion.MarkLayerDirty(items);

                Assert.That(CollectionElementSync.Reconcile(items), Is.True, "长度没变，但结构脏了。");
                Assert.That(items.Children[0], Is.Not.SameAs(before));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>删中间元素：整层重建，路径仍按**位置**投影（下标重排、数据跟着走）。</summary>
        [Test]
        public void 删除中间元素后下标重排()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementRebuildFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");
                var oldSecond = items.Children[1];

                // 三个元素各写一个认得出来的 hp，删掉中间那个。
                SetInt(target, tree, "items.Array.data[0].hp", 10);
                SetInt(target, tree, "items.Array.data[1].hp", 20);
                SetInt(target, tree, "items.Array.data[2].hp", 30);
                RemoveAt(target, tree, "items", 1);

                Assert.That(CollectionElementSync.Reconcile(items), Is.True);

                Assert.That(items.Children.Count, Is.EqualTo(2));
                Assert.That(items.Children[1], Is.Not.SameAs(oldSecond), "旧节点整体作废。");
                Assert.That(
                    HpOf(items.Children[1]),
                    Is.EqualTo(30),
                    "下标 1 现在指向原来的第三个元素——节点是按位置的投影。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>旧子树的状态被释放（<c>IDisposable</c> 状态按销毁语义释放，不泄漏）。</summary>
        [Test]
        public void 旧子树的状态被释放()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementRebuildFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");
                var hp = Find(items.Children[0], "items.Array.data[0].hp");
                var probe = hp.State.GetOrCreate<DisposableProbe>();

                SetArraySize(target, tree, "items", 2);
                CollectionElementSync.Reconcile(items);

                Assert.That(probe.Disposed, Is.True, "被丢掉的元素子树要释放状态——重建一次泄漏一次就糟了。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>整树释放同样覆盖元素子树（对账与 <c>Dispose</c> 走同一条递归）。</summary>
        [Test]
        public void 整树释放覆盖元素子树()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementRebuildFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");
                var probe = Find(items.Children[0], "items.Array.data[0].hp").State.GetOrCreate<DisposableProbe>();

                tree.Dispose();

                Assert.That(probe.Disposed, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>重建出来的节点照样装了处理器（条件跟随）与链（末端齐全）。</summary>
        [Test]
        public void 重建后的节点照样带处理器与链()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementRebuildFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");

                SetArraySize(target, tree, "items", 4);
                CollectionElementSync.Reconcile(items);

                var hp = Find(items.Children[3], "items.Array.data[3].hp");
                Assert.That(hp.IsVisible, Is.True);

                SetBool(target, tree, "items.Array.data[3].alive", false);

                Assert.That(hp.IsVisible, Is.False, "重建期重跑了第一趟处理器，条件求值器装上了。");

                var entry = hp.Chain.Entries[hp.Chain.Count - 1].Drawer;
                Assert.That(entry, Is.Not.Null, "链必须齐全——缺链会在 Draw 时直接抛。");

                var group = Find(items.Children[3], "items.Array.data[3]/基础");
                Assert.That(
                    group.Kind,
                    Is.EqualTo(InspectorPropertyKind.Group),
                    "分组装配也在重建流水线里重跑了。");
                Assert.That(Find(group, "items.Array.data[3].level"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 跨趟消费者

        /// <summary>搜索的节点命中集跳过元素子树（行过滤由容器的行掩码负责）。</summary>
        [Test]
        public void 搜索的节点命中集跳过元素子树()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementRebuildFixture>();
            try
            {
                var tree = BuildTree(target);
                var host = Find(tree.Root, "searchable");
                var state = host.State.GetOrCreate<SearchFilterState>();

                state.Query = "hp";
                state.EnsureNodes(host);

                var hp = Find(host.Children[0], "searchable.Array.data[0].hp");

                Assert.That(
                    state.VisibilityOf(hp),
                    Is.EqualTo(SearchVisibility.Hidden),
                    "元素子树不参与节点级过滤——两套过滤叠加会把整行命中的元素拆碎。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>按路径重置的成员名单排除元素子树（集合字段自己的路径已经覆盖整份数组）。</summary>
        [Test]
        public void 重置名单排除元素子树()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementRebuildFixture>();
            try
            {
                var tree = BuildTree(target);
                var paths = PropertyTreeReset.CollectMemberPaths(tree);

                Assert.That(paths, Does.Contain("items"));

                foreach (var path in paths)
                {
                    Assert.That(path, Does.Not.Contain("Array.data"), $"元素路径不该进名单：{path}");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 测量

        /// <summary>
        /// 测量：元素的展开态（<c>SerializedProperty.isExpanded</c>）在**重新 FindProperty** 之后
        /// 是否保留。
        /// </summary>
        /// <remarks>
        /// 元素行沿用 Unity 自己的展开态（集合绘制器读的是 <c>element.isExpanded</c>），
        /// 而重建元素层时句柄是**重新取**的——这条测量钉住「重取之后展开态还在不在」，
        /// 也就是「增删一次之后用户的展开状态会不会丢」。Unity 哪天换了行为，这条先红。
        /// </remarks>
        [Test]
        public void 测量_元素展开态在重新查找后是否保留()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementRebuildFixture>();
            try
            {
                var serializedObject = new SerializedObject(target);
                var items = serializedObject.FindProperty("items");

                items.GetArrayElementAtIndex(0).isExpanded = true;

                var again = serializedObject.FindProperty("items").GetArrayElementAtIndex(0);

                Assert.That(again.isExpanded, Is.True, "实测：同一序列化对象内按路径重取，展开态保留。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>构建被测的树。</summary>
        /// <param name="target">目标资产。</param>
        /// <returns>属性树。</returns>
        private static PropertyTree BuildTree(ScriptableObject target)
        {
            return PropertyTree.Create(new SerializedObject(target));
        }

        /// <summary>改数组长度（走**另一个**序列化对象，模拟撤销 / 外部改动）。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="path">数组路径。</param>
        /// <param name="size">新长度。</param>
        private static void SetArraySize(ScriptableObject target, PropertyTree tree, string path, int size)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).arraySize = size;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        /// <summary>删掉下标处的元素（走另一个序列化对象）。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="path">数组路径。</param>
        /// <param name="index">要删的下标。</param>
        private static void RemoveAt(ScriptableObject target, PropertyTree tree, string path, int index)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        /// <summary>改一个 int 字段的值，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="path">序列化路径。</param>
        /// <param name="value">新值。</param>
        private static void SetInt(ScriptableObject target, PropertyTree tree, string path, int value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        /// <summary>改一个 bool 字段的值，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="path">序列化路径。</param>
        /// <param name="value">新值。</param>
        private static void SetBool(ScriptableObject target, PropertyTree tree, string path, bool value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        /// <summary>读元素节点的 hp 值。</summary>
        /// <param name="elementNode">元素节点。</param>
        /// <returns>hp 的值。</returns>
        private static int HpOf(InspectorProperty elementNode)
        {
            foreach (var child in elementNode.Children)
            {
                if (child.Name == "hp")
                {
                    return child.ValueEntry.SerializedProperty.intValue;
                }
            }

            Assert.Fail("元素节点上没有 hp。");
            return 0;
        }

        /// <summary>按路径查找直接子节点。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；不存在时断言失败。</returns>
        private static InspectorProperty Find(InspectorProperty parent, string path)
        {
            foreach (var child in parent.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            Assert.Fail($"找不到节点 {path}。");
            return null;
        }

        #endregion
    }

    /// <summary>重建用的探针状态：释放时留个记号。</summary>
    internal sealed class DisposableProbe : IDisposable
    {
        /// <summary>是否被释放过。</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Disposed = true;
        }
    }

    /// <summary>对账与重建的对照资产。</summary>
    [HideMonoScript]
    internal sealed class CollectionElementRebuildFixture : ScriptableObject
    {
        /// <summary>元素类型用到了本包 → 建元素层。</summary>
        [ListDrawerSettings]
        public List<ElementItem> items = new List<ElementItem>
        {
            new ElementItem(), new ElementItem(), new ElementItem(),
        };

        /// <summary>搜索宿主：容器靠处理器注入（顺带钉住「注入之后才判容器项」这条顺序）。</summary>
        [Searchable]
        public List<ElementItem> searchable = new List<ElementItem> { new ElementItem() };
    }
}
