using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 集合元素节点化：安全阀（什么时候建元素层、什么时候不建但告警）、元素节点的形状，
    /// 以及**元素类型里的特性第一次生效**。
    /// <para>
    /// 不测 IMGUI——断言的是「有没有元素节点、路径对不对、条件跟不跟随、分组落在哪、
    /// 回退时末端是谁」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class CollectionElementNodeTests
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

        #region 元素层的形状

        /// <summary>元素类型用到了本包 → 每个元素一个真节点，路径是 <c>items.Array.data[i]</c>。</summary>
        [Test]
        public void 元素类型用到本包时元素成为子节点()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");

                Assert.That(items.Children.Count, Is.EqualTo(3));

                var first = items.Children[0];
                Assert.That(first.Path, Is.EqualTo("items.Array.data[0]"));
                Assert.That(
                    first.Kind,
                    Is.EqualTo(InspectorPropertyKind.Member),
                    "元素节点仍是 Member——单列新 Kind 会让按名解析容器认不出它。");
                Assert.That(first.Type, Is.EqualTo(typeof(ElementItem)));
                Assert.That(first.Member, Is.Null, "元素上标不了特性，没有成员自己的特性可读。");
                Assert.That(first.ValueEntry.IsUnityBacked, Is.True);
                Assert.That(
                    first.ValueEntry.SerializedProperty.propertyPath,
                    Is.EqualTo("items.Array.data[0]"),
                    "值的句柄按路径取得（独立实例）。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 对照：元素类型没用本包（原生装饰器 / 标量）时不建元素节点——「没用到本包的集合
        /// 外观逐字不变」这条契约的回归守卫。
        /// </summary>
        [Test]
        public void 未用到的元素类型不建元素节点()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(Find(tree.Root, "native").Children.Count, Is.EqualTo(0));
                Assert.That(Find(tree.Root, "numbers").Children.Count, Is.EqualTo(0), "标量元素没有特性可生效。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// <b>「让特性作用于元素」的直接证据：</b>元素成员上的 <c>[ShowIf]</c> 跟随**同层**的开关。
        /// </summary>
        [Test]
        public void 元素成员的条件生效()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");
                var hp = Find(items.Children[0], "items.Array.data[0].hp");

                Assert.That(hp.IsVisible, Is.True, "起点：同层 alive 为真。");

                SetBool(target, tree, "items.Array.data[0].alive", false);

                Assert.That(hp.IsVisible, Is.False, "条件指的是**这个元素内部**的 alive。");
                Assert.That(
                    Find(items.Children[1], "items.Array.data[1].hp").IsVisible,
                    Is.True,
                    "每个元素各是各的：改一个元素不影响别的元素。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>元素成员上的 <c>[BoxGroup]</c> 在元素**内部**装配，分组路径带元素前缀。</summary>
        [Test]
        public void 元素成员的分组在元素内部装配()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");
                var group = Find(items.Children[0], "items.Array.data[0]/基础");
                var level = Find(group, "items.Array.data[0].level");

                Assert.That(group.Kind, Is.EqualTo(InspectorPropertyKind.Group));
                Assert.That(
                    group.Path,
                    Is.EqualTo("items.Array.data[0]/基础"),
                    "分组路径以元素路径为前缀——两个元素的组各是各的，不会并成一个。");
                Assert.That(
                    group.Attributes.Get<PropertyGroupAttribute>().GroupID,
                    Is.EqualTo(group.Path),
                    "「分组节点恒有 GroupID == node.Path」这条不变量在元素层照旧成立。");
                Assert.That(level.Parent, Is.SameAs(group));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>元素成员上的 <c>[PropertyOrder]</c> 在元素内部同样生效。</summary>
        [Test]
        public void 元素成员的顺序在元素内部生效()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");

                Assert.That(
                    items.Children[0].Children[0].Name,
                    Is.EqualTo("priority"),
                    "声明序是 alive/hp/level/priority，[PropertyOrder(-1)] 把它排到最前。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>嵌套类型里的集合照样节点化（祖先链里没有元素层，深度判据不拦它）。</summary>
        [Test]
        public void 嵌套类型里的集合也节点化()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementFixture>();
            try
            {
                var tree = BuildTree(target);
                var list = Find(Find(tree.Root, "holder"), "holder.list");

                Assert.That(list.Children.Count, Is.EqualTo(1));
                Assert.That(list.Children[0].Path, Is.EqualTo("holder.list.Array.data[0]"));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 元素层容器的末端是**值末端**：链上的集合绘制器一旦放行（绘制期降级），整份要交回
        /// Unity 的原生数组画法，而不是把元素节点当折叠头逐个画。
        /// </summary>
        [Test]
        public void 元素层容器的末端是值末端()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");
                var list = Find(Find(tree.Root, "holder"), "holder.list");

                Assert.That(items.Chain.Entries[items.Chain.Count - 1].Drawer, Is.InstanceOf<UnityFallbackDrawer>());
                Assert.That(list.Chain.Entries[list.Chain.Count - 1].Drawer, Is.InstanceOf<UnityFallbackDrawer>());
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 规模

        /// <summary>
        /// 规模用例：1000 个元素照样建得出来，且对账是 O(1)。
        /// </summary>
        /// <remarks>
        /// 元素节点化是**按需**的，但一旦发生，节点数就是「元素个数 × 元素字段数」。
        /// 这条用例把**规模是可预期的**钉住：不设静默上限（悄悄只节点化前 N 个是本包最忌讳的
        /// 「静默」），绘制路径上也没有非线性的事（对账只比长度）。
        /// **实测**（2026-10-06，本机）：1000 个元素、约 5000 个节点，建树 574 ms——
        /// 一次性成本（每次选中重建一次），换来的是元素里的特性生效；不设上限。
        /// 耗时由 <c>TestContext</c> 打出来，环境变化时能一眼看见数量级有没有变。
        /// </remarks>
        [Test]
        public void 一千个元素照样建得出来()
        {
            var target = ScriptableObject.CreateInstance<CollectionElementScaleFixture>();
            try
            {
                for (var i = 0; i < 1000; i++)
                {
                    target.items.Add(new ElementItem());
                }

                var watch = System.Diagnostics.Stopwatch.StartNew();
                var tree = BuildTree(target);
                watch.Stop();
                TestContext.Progress.WriteLine(
                    $"[规模] 1000 个元素建树耗时 {watch.ElapsedMilliseconds} ms");

                var items = Find(tree.Root, "items");

                Assert.That(items.Children.Count, Is.EqualTo(1000));
                Assert.That(items.Children[999].Path, Is.EqualTo("items.Array.data[999]"));
                Assert.That(items.Children[999].Children.Count, Is.GreaterThan(0), "每个元素各是一棵子树。");
                Assert.That(
                    CollectionElementSync.Reconcile(items),
                    Is.False,
                    "对账只比长度（O(1)），长度没变就不重建。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 安全阀的边界（用到了本包却不建层——一律告警，不静默）

        /// <summary>没有容器（字段上没有任何会让本包接管它的特性）时不建层，并告警一次。</summary>
        [Test]
        public void 没有容器的集合不建层并告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"给它加 \[ListDrawerSettings\]"));

            var target = ScriptableObject.CreateInstance<NoContainerElementFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(Find(tree.Root, "items").Children.Count, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>表格形态不节点化（单元格画法逐字不变），并告警一次。</summary>
        [Test]
        public void 表格形态不建层并告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex("表格形态"));

            var target = ScriptableObject.CreateInstance<TableElementFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(Find(tree.Root, "rows").Children.Count, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>元素层**里面**的集合不再建层（深度只做一层），并告警一次。</summary>
        [Test]
        public void 元素里的集合不递归并告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex("只做一层"));

            var target = ScriptableObject.CreateInstance<NestedCollectionElementFixture>();
            try
            {
                var tree = BuildTree(target);
                var outer = Find(tree.Root, "outer");

                Assert.That(outer.Children.Count, Is.EqualTo(1), "外层照常节点化。");

                var inner = Find(outer.Children[0], "outer.Array.data[0].inner");
                Assert.That(inner.Children.Count, Is.EqualTo(0), "内层不递归；内层元素类型里的特性不会生效。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 元素类型里**只有** <c>[ShowInInspector]</c> / <c>[Button]</c> 时**也建层**——
        /// 元素阀自 2026-10-06（读路径落地）起两条腿都数。
        /// </summary>
        /// <remarks>
        /// 2026-10-06 之前这一条断言的是「不建层 + 告警读路径留下一轮」：那会儿元素里的
        /// 反射成员取不到实例，建了层等于「展开了却什么都画不出来」。读路径落地后判据与
        /// 消费者同批放开，这条就反了过来。
        /// </remarks>
        [Test]
        public void 元素里只有反射成员也建层()
        {
            var target = ScriptableObject.CreateInstance<ReflectedOnlyElementFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");

                Assert.That(items.Children.Count, Is.EqualTo(1), "元素类型用到了本包 → 建层。");
                Assert.That(
                    Find(items.Children[0], "items.Array.data[0].Tag"),
                    Is.Not.Null,
                    "只放反射成员的元素类型也会展开（与嵌套层的第三道闸同款）。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 元素类型里**另有**序列化字段的特性时层照建，反射成员**也照样出现**——
        /// 「展开过却少画了几样」的那个缺口自 2026-10-06（读路径落地）起关上了。
        /// </summary>
        [Test]
        public void 元素里的反射成员出现()
        {
            var target = ScriptableObject.CreateInstance<InspectedElementFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");
                var element = items.Children[0];

                Assert.That(items.Children.Count, Is.EqualTo(1), "序列化字段那一半照常节点化。");
                Assert.That(Find(element, "items.Array.data[0].hp"), Is.Not.Null);

                var tag = Find(element, "items.Array.data[0].Tag");
                Assert.That(tag.Kind, Is.EqualTo(InspectorPropertyKind.ReflectedMember));
                Assert.That(tag.Parent, Is.SameAs(element), "反射成员挂在**那个元素**之下。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>元素里的方法节点同样出现，挂在元素之下（路径带元素前缀）。</summary>
        [Test]
        public void 元素里的方法节点出现()
        {
            var target = ScriptableObject.CreateInstance<InspectedElementFixture>();
            try
            {
                var tree = BuildTree(target);
                var element = Find(tree.Root, "items").Children[0];

                Assert.That(
                    Find(element, "items.Array.data[0].Refresh()").Kind,
                    Is.EqualTo(InspectorPropertyKind.Method));
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

        /// <summary>改一个 bool 字段的值，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树——条件求值器读的是**树自己的**序列化对象。</param>
        /// <param name="path">序列化路径（可点分）。</param>
        /// <param name="value">新值。</param>
        private static void SetBool(ScriptableObject target, PropertyTree tree, string path, bool value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
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

    /// <summary>元素类型的对照：条件、分组、顺序三样都齐，且有一个「用不到本包」的对照类型。</summary>
    [Serializable]
    internal class ElementItem
    {
        /// <summary>条件开关（**元素内部**的成员）。</summary>
        public bool alive = true;

        /// <summary>带条件的成员——元素里的特性靠它证明生效。</summary>
        [ShowIf(nameof(alive))]
        public int hp = 10;

        /// <summary>带分组的成员——分组要装配在元素**内部**。</summary>
        [BoxGroup("基础")]
        public int level = 1;

        /// <summary>带顺序的成员——排到元素那一层的最前。</summary>
        [PropertyOrder(-1f)]
        public int priority;
    }

    /// <summary>只带原生装饰器的元素类型——不该被节点化。</summary>
    [Serializable]
    internal class NativeOnlyItem
    {
        /// <summary>Unity 自己的装饰器（写全名：NUnit 也有一个 <c>[Range]</c>）。</summary>
        [UnityEngine.Range(0f, 1f)]
        public float ratio;
    }

    /// <summary>元素里嵌套一个集合——深度只做一层。</summary>
    [Serializable]
    internal class OuterWithCollection
    {
        /// <summary>元素**里面**的集合：本轮不节点化。</summary>
        [ListDrawerSettings]
        public List<ElementItem> inner = new List<ElementItem> { new ElementItem() };
    }

    /// <summary>元素类型里只有反射成员——元素阀两条腿都数，照样建层。</summary>
    [Serializable]
    internal class ReflectedOnlyItem
    {
        /// <summary>普通字段。</summary>
        public int plain = 1;

        /// <summary>唯一的用法——序列化通道看不见它。</summary>
        [ShowInInspector]
        public int Tag => plain;
    }

    /// <summary>元素类型里既有序列化字段的特性、也有反射成员与按钮（现在都生效）。</summary>
    [Serializable]
    internal class InspectedElementItem
    {
        /// <summary>条件开关。</summary>
        public bool alive = true;

        /// <summary>序列化字段上的特性——这一半一直生效。</summary>
        [ShowIf(nameof(alive))]
        public int hp = 10;

        /// <summary>反射成员——元素里的读路径自 2026-10-06 起生效。</summary>
        [ShowInInspector]
        public int Tag => hp;

        /// <summary>元素里的按钮——调的是**那个元素实例**上的方法。</summary>
        [Button("元素里的按钮：满血")]
        private void Refresh()
        {
            hp = 100;
        }
    }

    /// <summary>嵌套类型里带一个集合——这个集合照样节点化（祖先链里没有元素层）。</summary>
    [Serializable]
    internal class NestedCollectionHolder
    {
        /// <summary>容器与锚：嵌套类型里有本包特性才会展开。</summary>
        [ListDrawerSettings]
        public List<ElementItem> list = new List<ElementItem> { new ElementItem() };
    }

    /// <summary>元素节点化的主对照资产。</summary>
    [HideMonoScript]
    internal sealed class CollectionElementFixture : ScriptableObject
    {
        /// <summary>元素类型用到了本包 → 建元素层。</summary>
        [ListDrawerSettings]
        public List<ElementItem> items = new List<ElementItem>
        {
            new ElementItem(), new ElementItem(), new ElementItem(),
        };

        /// <summary>对照：元素类型只带原生装饰器 → 不建层。</summary>
        [ListDrawerSettings]
        public List<NativeOnlyItem> native = new List<NativeOnlyItem> { new NativeOnlyItem() };

        /// <summary>对照：标量元素没有特性可生效 → 不建层。</summary>
        [ListDrawerSettings]
        public List<int> numbers = new List<int> { 1, 2 };

        /// <summary>嵌套类型里的集合 → 照样建层。</summary>
        public NestedCollectionHolder holder = new NestedCollectionHolder();
    }

    /// <summary>元素类型用到了本包，但字段上没有任何会让本包接管它的特性。</summary>
    [HideMonoScript]
    internal sealed class NoContainerElementFixture : ScriptableObject
    {
        /// <summary>没有被接管的集合——元素里的特性不会生效（告警一次）。</summary>
        public List<ElementItem> items = new List<ElementItem> { new ElementItem() };
    }

    /// <summary>表格形态的集合——本轮不节点化。</summary>
    [HideMonoScript]
    internal sealed class TableElementFixture : ScriptableObject
    {
        /// <summary>表格：单元格画法逐字不变。</summary>
        [TableList]
        public List<ElementItem> rows = new List<ElementItem> { new ElementItem() };
    }

    /// <summary>元素**里面**的集合——深度只做一层。</summary>
    [HideMonoScript]
    internal sealed class NestedCollectionElementFixture : ScriptableObject
    {
        /// <summary>外层照常节点化；内层不递归。</summary>
        [ListDrawerSettings]
        public List<OuterWithCollection> outer = new List<OuterWithCollection> { new OuterWithCollection() };
    }

    /// <summary>元素类型里只有反射成员——与嵌套层的「只放反射成员也会展开」同款。</summary>
    [HideMonoScript]
    internal sealed class ReflectedOnlyElementFixture : ScriptableObject
    {
        /// <summary>唯一的用法在元素类型的反射成员上。</summary>
        [ListDrawerSettings]
        public List<ReflectedOnlyItem> items = new List<ReflectedOnlyItem> { new ReflectedOnlyItem() };
    }

    /// <summary>元素类型里既有序列化字段的特性、也有反射成员与按钮——三样都画得出来。</summary>
    [HideMonoScript]
    internal sealed class InspectedElementFixture : ScriptableObject
    {
        /// <summary>层照建，反射成员与方法节点都收进来。</summary>
        [ListDrawerSettings]
        public List<InspectedElementItem> items = new List<InspectedElementItem> { new InspectedElementItem() };
    }

    /// <summary>规模用例的对照资产：元素在测试里现填（1000 个）。</summary>
    [HideMonoScript]
    internal sealed class CollectionElementScaleFixture : ScriptableObject
    {
        /// <summary>元素类型用到本包 → 每个元素各建一棵子树。</summary>
        [ListDrawerSettings]
        public List<ElementItem> items = new List<ElementItem>();
    }
}
