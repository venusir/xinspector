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
    /// 元素层深度 &gt; 1（第十九批）：元素类型里的集合也按需节点化，两道守卫
    /// （**类型链去重**挡自引用、**层数预算**挡过大类型链）各自响亮拒绝。
    /// <para>
    /// 不测 IMGUI——断言的是「建没建层、路径对不对、守卫有没有响、重建级联后内层还在不在、
    /// 登记名单涨没涨」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class CollectionElementDepthTests
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

        #region 递归建层

        /// <summary>元素类型里的集合照样节点化——内层元素的路径是两组索引对。</summary>
        [Test]
        public void 深度二的内层集合建层()
        {
            var target = ScriptableObject.CreateInstance<ElementDepthFixture>();
            try
            {
                var tree = BuildTree(target);
                var two = Find(tree.Root, "two");

                Assert.That(two.Children.Count, Is.EqualTo(1), "外层照常节点化。");

                var inner = Find(two.Children[0], "two.Array.data[0].inners");
                Assert.That(inner.Children.Count, Is.EqualTo(2), "内层也建层。");

                var innerElement = Find(inner, "two.Array.data[0].inners.Array.data[1]");
                Assert.That(innerElement.Kind, Is.EqualTo(InspectorPropertyKind.Member));
                Assert.That(innerElement.Type, Is.EqualTo(typeof(DepthInner)));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>深度三照常（唯一类型链、预算内）。</summary>
        [Test]
        public void 深度三的集合照常建层()
        {
            var target = ScriptableObject.CreateInstance<ElementDepthFixture>();
            try
            {
                var tree = BuildTree(target);
                var three = Find(tree.Root, "three");

                var mid = Find(three.Children[0], "three.Array.data[0].mids");
                Assert.That(mid.Children.Count, Is.EqualTo(1), "第二层。");

                var leaf = Find(mid.Children[0], "three.Array.data[0].mids.Array.data[0].leaves");
                Assert.That(leaf.Children.Count, Is.EqualTo(1), "第三层。");

                Assert.That(
                    Find(leaf, "three.Array.data[0].mids.Array.data[0].leaves.Array.data[0]").Kind,
                    Is.EqualTo(InspectorPropertyKind.Member));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 两道守卫

        /// <summary>
        /// 自引用类型在第二层被挡：<c>Node { List&lt;Node&gt; children; }</c> 的元素类型
        /// 已出现在祖先元素层的类型链上——再展开就是无限递归。
        /// <para>
        /// 同时钉**去重**：3 个外层元素各有一个被挡的 <c>children</c>，只报**一条**告警
        /// （锚点在最外层集合、键里带归一化路径）。
        /// </para>
        /// </summary>
        [Test]
        public void 自引用类型的第二层被挡并告警()
        {
            var target = ScriptableObject.CreateInstance<SelfRefFixture>();
            try
            {
                // 深度 2 的数据用 C# 直接构造（字段初始化器里 new 自己会无限递归）。
                for (var i = 0; i < 3; i++)
                {
                    var node = new SelfRefNode();
                    node.children.Add(new SelfRefNode());
                    target.roots.Add(node);
                }

                var tree = BuildTreeCapturingWarnings(target, out var warnings);
                var roots = Find(tree.Root, "roots");

                Assert.That(roots.Children.Count, Is.EqualTo(3), "第一层照建。");

                for (var i = 0; i < 3; i++)
                {
                    Assert.That(
                        Find(roots.Children[i], $"roots.Array.data[{i}].children").Children.Count,
                        Is.EqualTo(0),
                        "第二层被类型链去重挡住。");
                }

                Assert.That(
                    CountContaining(warnings, "类型链"),
                    Is.EqualTo(1),
                    "3 个外层元素各有一处被挡，按最外层集合去重后只报一条。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 互递归 <c>A{B} B{List&lt;A&gt;}</c>：中间的复合层级（<c>b</c>）**不进类型链**，
        /// 被挡的原因是 <c>alphas</c> 的元素类型 <c>A</c> 与最外层集合的元素类型重复。
        /// </summary>
        [Test]
        public void 互递归在类型重复的那一层被挡()
        {
            var target = ScriptableObject.CreateInstance<MutualRecursionFixture>();
            try
            {
                var tree = BuildTreeCapturingWarnings(target, out var warnings);
                var roots = Find(tree.Root, "roots");

                var alphas = Find(
                    Find(roots.Children[0], "roots.Array.data[0].b"),
                    "roots.Array.data[0].b.alphas");

                Assert.That(alphas.Children.Count, Is.EqualTo(0), "元素类型 A 已在链上。");
                Assert.That(CountContaining(warnings, "类型链"), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 层数预算：前 4 层照建，第 5 层的尝试被挡（正则「上限 4 层」）；
        /// 两个外层元素各有一处被挡，同样只报**一条**。
        /// </summary>
        [Test]
        public void 元素层深度达到上限时被挡并告警()
        {
            var target = ScriptableObject.CreateInstance<DepthLimitFixture>();
            try
            {
                SeedArraySize(target, "roots", 2);

                var tree = BuildTreeCapturingWarnings(target, out var warnings);
                var roots = Find(tree.Root, "roots");

                // 逐层下钻：layer1 = roots，layer2..4 = 各层 next。
                var fourth = Find(
                    Find(
                        Find(
                            Find(roots.Children[0], "roots.Array.data[0].next").Children[0],
                            "roots.Array.data[0].next.Array.data[0].next").Children[0],
                        "roots.Array.data[0].next.Array.data[0].next.Array.data[0].next").Children[0],
                    "roots.Array.data[0].next.Array.data[0].next.Array.data[0].next.Array.data[0].next");

                Assert.That(fourth.Children.Count, Is.EqualTo(0), "第 5 层的尝试被预算挡住。");
                Assert.That(
                    CountContaining(warnings, "上限 4 层"),
                    Is.EqualTo(1),
                    "两个外层元素各有一处被挡，去重后只报一条。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 重建级联与登记簿

        /// <summary>外层长度变化 → 整层重建 → 新子树里的内层**重新建层**（旧节点作废）。</summary>
        [Test]
        public void 外层长度变化后内层仍在()
        {
            var target = ScriptableObject.CreateInstance<ElementDepthFixture>();
            try
            {
                var tree = BuildTree(target);
                var two = Find(tree.Root, "two");
                var before = Find(two.Children[0], "two.Array.data[0].inners").Children[0];
                var entriesBefore = tree.ElementCollections.Count;

                SetArraySize(target, tree, "two", 2);

                Assert.That(CollectionElementSync.ReconcileAll(tree), Is.GreaterThan(0));

                Assert.That(two.Children.Count, Is.EqualTo(2));

                var inner = Find(two.Children[0], "two.Array.data[0].inners");
                Assert.That(inner.Children.Count, Is.EqualTo(2), "内层在新子树里被重建。");
                Assert.That(inner.Children[0], Is.Not.SameAs(before), "旧节点整体作废。");

                Assert.That(
                    tree.ElementCollections.Count,
                    Is.EqualTo(entriesBefore + 1),
                    "旧内层 1 个 → 新内层 2 个（two 自己与 three 链那 3 个都不动）。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 反复重建后登记名单**不增长**——旧内层条目在子树释放前被注销，
        /// 否则每次重建都留一批强引用作废子树的僵尸。
        /// </summary>
        [Test]
        public void 重复重建后登记名单不增长()
        {
            var target = ScriptableObject.CreateInstance<ElementDepthFixture>();
            try
            {
                var tree = BuildTree(target);

                for (var size = 2; size <= 6; size++)
                {
                    SetArraySize(target, tree, "two", size);
                    CollectionElementSync.ReconcileAll(tree);
                }

                Assert.That(
                    tree.ElementCollections.Count,
                    Is.EqualTo(10),
                    "three 链 3 个 + two 1 个 + 内层 6 个 = 10——连改 5 次后名单仍不多不少。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>内层增删只脏内层：外层元素节点引用不变。</summary>
        [Test]
        public void 内层增删只脏内层()
        {
            var target = ScriptableObject.CreateInstance<ElementDepthFixture>();
            try
            {
                var tree = BuildTree(target);
                var outerElement = Find(tree.Root, "two").Children[0];

                SetArraySize(target, tree, "two.Array.data[0].inners", 3);

                Assert.That(CollectionElementSync.ReconcileAll(tree), Is.GreaterThan(0));

                Assert.That(
                    Find(tree.Root, "two").Children[0],
                    Is.SameAs(outerElement),
                    "外层长度没变、没标脏 → 不重建。");

                Assert.That(
                    Find(outerElement, "two.Array.data[0].inners").Children.Count,
                    Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 规模

        /// <summary>40 × 20 的两层规模测量（照「一千个元素照样建得出来」的口径打耗时）。</summary>
        [Test]
        public void 深度二的规模测量()
        {
            var target = ScriptableObject.CreateInstance<ElementDepthFixture>();
            try
            {
                target.two.Clear();
                for (var i = 0; i < 40; i++)
                {
                    var outer = new DepthOuter();
                    outer.inners.Clear();
                    for (var j = 0; j < 20; j++)
                    {
                        outer.inners.Add(new DepthInner());
                    }

                    target.two.Add(outer);
                }

                var watch = System.Diagnostics.Stopwatch.StartNew();
                var tree = BuildTree(target);
                watch.Stop();

                var two = Find(tree.Root, "two");
                var inner = Find(two.Children[39], "two.Array.data[39].inners");

                TestContext.Progress.WriteLine(
                    $"[规模] 40 × 20 的两层元素树建树耗时 {watch.ElapsedMilliseconds} ms，" +
                    $"内层元素节点 {inner.Children.Count} 个");

                Assert.That(two.Children.Count, Is.EqualTo(40));
                Assert.That(inner.Children.Count, Is.EqualTo(20));
                Assert.That(Find(inner, "two.Array.data[39].inners.Array.data[19]"), Is.Not.Null);
                Assert.That(
                    CollectionElementSync.ReconcileAll(tree),
                    Is.EqualTo(0),
                    "全部新鲜——对账只比长度。");
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

        /// <summary>
        /// 建树，并把这一趟收到的告警文案收进 <paramref name="warnings"/>。
        /// </summary>
        /// <param name="target">目标资产。</param>
        /// <param name="warnings">收到的告警（<c>condition</c> 文本）。</param>
        /// <returns>属性树。</returns>
        /// <remarks>
        /// 用 <c>Application.logMessageReceived</c> 而不是 <c>LogAssert</c>：**递归类型的夹具**
        /// 会触发 Unity 自己的「Serialization depth limit」告警（那是类型的账，不是本包的），
        /// <c>LogAssert.NoUnexpectedReceived</c> 会把它一起算成意外；而这里要断言的恰恰是
        /// **本包那条警告的条数**（去重有没有生效）。
        /// </remarks>
        private static PropertyTree BuildTreeCapturingWarnings(
            ScriptableObject target, out List<string> warnings)
        {
            var collected = new List<string>();

            void Handler(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Warning)
                {
                    collected.Add(condition);
                }
            }

            Application.logMessageReceived += Handler;
            try
            {
                return PropertyTree.Create(new SerializedObject(target));
            }
            finally
            {
                Application.logMessageReceived -= Handler;
                warnings = collected;
            }
        }

        /// <summary>数一数这些告警里有多少条包含某个片段。</summary>
        /// <param name="messages">告警文案。</param>
        /// <param name="fragment">片段。</param>
        /// <returns>条数。</returns>
        private static int CountContaining(List<string> messages, string fragment)
        {
            var count = 0;

            for (var i = 0; i < messages.Count; i++)
            {
                if (messages[i].Contains(fragment))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>建树**之前**给数组预置长度（自引用夹具不能靠字段初始化器造数据）。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="path">数组字段路径。</param>
        /// <param name="size">长度。</param>
        private static void SeedArraySize(ScriptableObject target, string path, int size)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).arraySize = size;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>改一个数组的长度，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="path">数组字段路径。</param>
        /// <param name="size">新长度。</param>
        private static void SetArraySize(ScriptableObject target, PropertyTree tree, string path, int size)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).arraySize = size;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        /// <summary>按完整路径查找**直接子节点**。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">子节点完整路径。</param>
        /// <returns>找到的子节点。</returns>
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

    #region Fixtures

    /// <summary>深度二链的内层元素类型。</summary>
    [Serializable]
    internal class DepthInner
    {
        /// <summary>普通成员（带特性让元素阀放行）。</summary>
        [BoxGroup("内层组")]
        public int value = 1;
    }

    /// <summary>深度二链的外层元素类型——里面再嵌一个集合。</summary>
    [Serializable]
    internal class DepthOuter
    {
        /// <summary>元素里的集合（自深度 &gt; 1 起建层）。</summary>
        [ListDrawerSettings]
        public List<DepthInner> inners = new List<DepthInner> { new DepthInner(), new DepthInner() };
    }

    /// <summary>深度三链的叶元素类型。</summary>
    [Serializable]
    internal class DeepLeaf
    {
        /// <summary>普通成员。</summary>
        [BoxGroup("叶组")]
        public int value = 1;
    }

    /// <summary>深度三链的中间元素类型。</summary>
    [Serializable]
    internal class DeepMid
    {
        /// <summary>再深一层的集合。</summary>
        [ListDrawerSettings]
        public List<DeepLeaf> leaves = new List<DeepLeaf> { new DeepLeaf() };
    }

    /// <summary>深度三链的最外层元素类型。</summary>
    [Serializable]
    internal class DeepTop
    {
        /// <summary>元素里的集合。</summary>
        [ListDrawerSettings]
        public List<DeepMid> mids = new List<DeepMid> { new DeepMid() };
    }

    /// <summary>深度测试主对照资产：一条深度二链、一条深度三链。</summary>
    [HideMonoScript]
    internal sealed class ElementDepthFixture : ScriptableObject
    {
        /// <summary>深度二：元素里嵌一个集合。</summary>
        [ListDrawerSettings]
        public List<DepthOuter> two = new List<DepthOuter> { new DepthOuter() };

        /// <summary>深度三：三层的唯一类型链。</summary>
        [ListDrawerSettings]
        public List<DeepTop> three = new List<DeepTop> { new DeepTop() };
    }

    /// <summary>自引用的元素类型——<c>Node { List&lt;Node&gt; children; }</c>。</summary>
    [Serializable]
    internal class SelfRefNode
    {
        /// <summary>普通字段（带特性让类型用到了本包）。</summary>
        [BoxGroup("节点组")]
        public int value = 1;

        /// <summary>
        /// 再嵌一层自己——**带容器**，判据才能走到两道守卫。
        /// （字段初始化器里**不要** <c>new</c> 自己：那会无限递归，数据由用例用序列化通道预置。）
        /// </summary>
        [ListDrawerSettings]
        public List<SelfRefNode> children = new List<SelfRefNode>();
    }

    /// <summary>自引用对照资产（数据由用例预置）。</summary>
    [HideMonoScript]
    internal sealed class SelfRefFixture : ScriptableObject
    {
        /// <summary>根层的自引用集合。</summary>
        [ListDrawerSettings]
        public List<SelfRefNode> roots = new List<SelfRefNode>();
    }

    /// <summary>互递归的 A：里面嵌一个 B（B 只是复合层级，不进类型链）。</summary>
    [Serializable]
    internal class MutualA
    {
        /// <summary>普通复合字段。</summary>
        public MutualB b = new MutualB();
    }

    /// <summary>互递归的 B：里面又嵌回 A 的集合。</summary>
    [Serializable]
    internal class MutualB
    {
        /// <summary>再套回 A 的集合。</summary>
        [ListDrawerSettings]
        public List<MutualA> alphas = new List<MutualA>();
    }

    /// <summary>互递归对照资产。</summary>
    [HideMonoScript]
    internal sealed class MutualRecursionFixture : ScriptableObject
    {
        /// <summary>最外层（元素类型 A）。</summary>
        [ListDrawerSettings]
        public List<MutualA> roots = new List<MutualA> { new MutualA() };
    }

    /// <summary>深度四链的叶类型。</summary>
    [Serializable]
    internal class LimitFive
    {
        /// <summary>普通成员。</summary>
        [BoxGroup("叶")]
        public int value = 1;
    }

    /// <summary>深度四链的第四层。</summary>
    [Serializable]
    internal class LimitFour
    {
        /// <summary>再深一层的集合（第 5 层的尝试，会被预算挡住）。</summary>
        [ListDrawerSettings]
        public List<LimitFive> next = new List<LimitFive> { new LimitFive() };
    }

    /// <summary>深度四链的第三层。</summary>
    [Serializable]
    internal class LimitThree
    {
        /// <summary>再深一层的集合。</summary>
        [ListDrawerSettings]
        public List<LimitFour> next = new List<LimitFour> { new LimitFour() };
    }

    /// <summary>深度四链的第二层。</summary>
    [Serializable]
    internal class LimitTwo
    {
        /// <summary>再深一层的集合。</summary>
        [ListDrawerSettings]
        public List<LimitThree> next = new List<LimitThree> { new LimitThree() };
    }

    /// <summary>深度四链的第一层（最外层集合的元素类型）。</summary>
    [Serializable]
    internal class LimitOne
    {
        /// <summary>再深一层的集合。</summary>
        [ListDrawerSettings]
        public List<LimitTwo> next = new List<LimitTwo> { new LimitTwo() };
    }

    /// <summary>层数预算对照资产（数据长度由用例预置）。</summary>
    [HideMonoScript]
    internal sealed class DepthLimitFixture : ScriptableObject
    {
        /// <summary>最外层集合。</summary>
        [ListDrawerSettings]
        public List<LimitOne> roots = new List<LimitOne> { new LimitOne() };
    }

    #endregion
}
