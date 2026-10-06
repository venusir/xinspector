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
    /// 类级分组进嵌套层与元素层（2026-10-06 的能力轮）：类型自己带的 <c>[BoxGroup]</c> 一族
    /// 分发到它的成员，路径以容器的序列化路径为前缀，同一类型用在两处时两处各是各的。
    /// <para>
    /// 不测 IMGUI——断言的是「分发有没有发生、路径对不对、实例是不是各是各的、
    /// 条件跟不跟随、元素层建没建、重建后还在不在」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class NestedClassLevelGroupTests
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

        #region 嵌套层的分发

        /// <summary>
        /// **只带类级分组**（成员一个特性都没有）的嵌套类型照样被展开、分发到成员——
        /// 判据的类级腿与注入同源。
        /// </summary>
        [Test]
        public void 只带类级分组的嵌套类型被展开并分发()
        {
            var target = ScriptableObject.CreateInstance<NestedClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);
                var only = Find(tree.Root, "only");

                var group = Find(only, "only/嵌套类级组");
                Assert.That(group.Kind, Is.EqualTo(InspectorPropertyKind.Group));
                Assert.That(Find(group, "only.plain"), Is.Not.Null, "未分组的成员应当落进类级分组里。");
                Assert.That(Find(group, "only.second"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **类的分组恒在最外层**：成员自己的分组嵌在它里面（前缀叠加一次、不重复）。
        /// </summary>
        [Test]
        public void 成员自有分组嵌在类级分组里面()
        {
            var target = ScriptableObject.CreateInstance<NestedClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);
                var stats = Find(tree.Root, "stats");

                var classGroup = Find(stats, "stats/嵌套类级组");
                var own = Find(classGroup, "stats/嵌套类级组/自有组");

                Assert.That(Find(own, "stats.nested"), Is.Not.Null, "自有分组应当嵌在类级分组里面。");
                Assert.That(Find(classGroup, "stats.plain"), Is.Not.Null);

                foreach (var child in stats.Children)
                {
                    Assert.That(
                        child.Path,
                        Does.Not.StartWith("stats/stats/"),
                        "容器前缀由装配期统一加——处理器自己再加一遍就会叠成两截。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 分发之后 <c>GroupID == node.Path</c> 这条不变量在嵌套层照旧成立——
        /// 两条前缀（类级分发、嵌套装配）叠加之后最容易拼错。
        /// </summary>
        [Test]
        public void 嵌套组的路径不变量()
        {
            var target = ScriptableObject.CreateInstance<NestedClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);
                var visited = 0;

                AssertGroupIdMatchesPath(tree.Root, ref visited);

                Assert.That(visited, Is.GreaterThan(0), "没有访问到任何分组节点，这条不变量等于没检查。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 注入的是**副本**而不是同一个实例——同容器的两个成员、以及两处容器之间，
        /// 都不许共享（共享意味着后续任何一处改写会串到别处）。
        /// </summary>
        [Test]
        public void 注入的是副本而非共享实例()
        {
            var target = ScriptableObject.CreateInstance<NestedClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);

                var onlyGroup = Find(Find(tree.Root, "only"), "only/嵌套类级组");
                var plain = Find(onlyGroup, "only.plain");
                var second = Find(onlyGroup, "only.second");
                var first = plain.Attributes.Get<PropertyGroupAttribute>();
                var other = second.Attributes.Get<PropertyGroupAttribute>();

                Assert.That(first, Is.Not.Null, "前提不成立：成员身上没有被注入分组特性。");
                Assert.That(other, Is.Not.Null);
                Assert.That(first, Is.Not.SameAs(other), "同容器的两个成员不共享同一份注入。");

                var statsPlain = Find(Find(Find(tree.Root, "stats"), "stats/嵌套类级组"), "stats.plain");
                var otherPlain = Find(Find(Find(tree.Root, "other"), "other/嵌套类级组"), "other.plain");
                Assert.That(
                    statsPlain.Attributes.Get<PropertyGroupAttribute>(),
                    Is.Not.SameAs(otherPlain.Attributes.Get<PropertyGroupAttribute>()),
                    "两处容器各注入各的副本。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>同一个类型用在两处时，两处各长出各的分组节点（路径带各自的前缀）。</summary>
        [Test]
        public void 同一个类型用在两处时两处各是各的()
        {
            var target = ScriptableObject.CreateInstance<NestedClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);

                var first = Find(Find(tree.Root, "stats"), "stats/嵌套类级组");
                var second = Find(Find(tree.Root, "other"), "other/嵌套类级组");

                Assert.That(first, Is.Not.SameAs(second));
                Assert.That(first.Attributes.Get<PropertyGroupAttribute>().GroupID, Is.EqualTo("stats/嵌套类级组"));
                Assert.That(second.Attributes.Get<PropertyGroupAttribute>().GroupID, Is.EqualTo("other/嵌套类级组"));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 类型上挂着两个类级分组时**只取第一个**——与根上同款。
        /// <para>
        /// 不断言取的是哪一个：<c>GetCustomAttributes</c> 的顺序不作承诺，
        /// 断言「恰好一份」才是稳的。
        /// </para>
        /// </summary>
        [Test]
        public void 类级分组只取第一个()
        {
            var target = ScriptableObject.CreateInstance<TwoClassGroupsFixture>();
            try
            {
                var tree = BuildTree(target);
                var stats = Find(tree.Root, "stats");

                var groups = new List<InspectorProperty>();
                foreach (var child in stats.Children)
                {
                    if (child.Kind == InspectorPropertyKind.Group)
                    {
                        groups.Add(child);
                    }
                }

                Assert.That(groups.Count, Is.EqualTo(1), "只取第一个类级分组，不该两个都建。");

                var member = Find(groups[0], "stats.value");
                var injected = 0;
                foreach (var attribute in member.Attributes)
                {
                    if (attribute is PropertyGroupAttribute)
                    {
                        injected++;
                    }
                }

                Assert.That(injected, Is.EqualTo(1), "注入恰好一份。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **容器字段自己的分组不被误当类级分组**：字段标了 <c>[BoxGroup]</c>、类型没标时，
        /// 分组只包住那个字段，容器内部不长出分组节点。
        /// </summary>
        /// <remarks>
        /// 这是把「来源」读错的最直接症状：处理器若读父节点自己的特性列表（那是**字段的**
        /// 特性），字段的分组会被当成这个类型的类级分组、再分发给它的孩子。
        /// </remarks>
        [Test]
        public void 容器字段自己的分组不被误当类级分组()
        {
            var target = ScriptableObject.CreateInstance<NestedClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);

                var fieldNode = Find(Find(tree.Root, "字段的组"), "fieldGrouped");
                Assert.That(fieldNode.Kind, Is.EqualTo(InspectorPropertyKind.Member));

                Assert.That(Find(fieldNode, "fieldGrouped.plain"), Is.Not.Null, "类型照常展开。");

                foreach (var child in fieldNode.Children)
                {
                    Assert.That(
                        child.Kind,
                        Is.Not.EqualTo(InspectorPropertyKind.Group),
                        "容器内部不该长出分组节点——字段的分组不是这个类型的类级分组。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 类级分组条件（<c>[ShowIfGroup]</c>）在嵌套层生效：条件指的是**那个嵌套实例**上的开关，
        /// 不是根上的同名成员。
        /// </summary>
        [Test]
        public void 类级分组条件在嵌套层生效()
        {
            var target = ScriptableObject.CreateInstance<NestedClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);
                var group = Find(Find(tree.Root, "conditional"), "conditional/开关组");

                Assert.That(group.IsVisible, Is.True, "起点：嵌套实例的开关为真。");

                SetBool(target, tree, "conditional.alive", false);

                Assert.That(
                    group.IsVisible,
                    Is.False,
                    "条件解析到的是同容器的 alive——根上的同名成员仍为真，解析错了这里就会仍然可见。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>类级 <c>[TabGroup]</c> 经嵌套层前缀改写后仍是容器（页容器判定扛得住前缀叠加）。</summary>
        [Test]
        public void 类级页签加前缀后仍是容器()
        {
            var target = ScriptableObject.CreateInstance<NestedClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);
                var container = Find(Find(tree.Root, "tabbed"), "tabbed/页签");

                Assert.That(container.Kind, Is.EqualTo(InspectorPropertyKind.Group));
                Assert.That(
                    container.Attributes.Get<TabGroupAttribute>().IsContainer,
                    Is.True,
                    "路径被加了前缀之后，容器仍应被认出来。");
                Assert.That(ChainHas<TabGroupDrawer>(container), Is.True, "容器上应当配着页签绘制器。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>多级嵌套：更深一层类型上的类级分组同样生效，中间层不长出多余的分组节点。</summary>
        [Test]
        public void 多级嵌套的类级分组()
        {
            var target = ScriptableObject.CreateInstance<NestedClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);
                var outer = Find(tree.Root, "outer");
                var inner = Find(outer, "outer.inner");

                var group = Find(inner, "outer.inner/嵌套类级组");
                Assert.That(Find(group, "outer.inner.plain"), Is.Not.Null);
                Assert.That(TryFind(outer, "outer/嵌套类级组"), Is.Null, "中间层自己不带分组。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 原来的「嵌套类型上的类级分组不生效」告警已撤除——分发真的发生了，就不该再说话。
        /// </summary>
        [Test]
        public void 类级分组不再告警()
        {
            var target = ScriptableObject.CreateInstance<NestedClassLevelGroupFixture>();
            try
            {
                BuildTree(target);

                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 元素层

        /// <summary>元素类型只带类级分组 → 元素阀放行、每个元素内部各自分发。</summary>
        [Test]
        public void 元素类型上的类级分组分发到元素成员()
        {
            var target = ScriptableObject.CreateInstance<ElementClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");

                Assert.That(items.Children.Count, Is.EqualTo(2), "元素类型只带类级分组也要建元素层。");

                var first = Find(items.Children[0], "items.Array.data[0]/元素类级组");
                Assert.That(Find(first, "items.Array.data[0].plain"), Is.Not.Null);

                var second = Find(items.Children[1], "items.Array.data[1]/元素类级组");
                Assert.That(second, Is.Not.SameAs(first), "每个元素各是各的组。");
                Assert.That(
                    second.Attributes.Get<PropertyGroupAttribute>().GroupID,
                    Is.EqualTo("items.Array.data[1]/元素类级组"),
                    "组路径以元素路径为前缀。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>元素里的类级条件各元素各判——改一个元素的开关不影响别的元素。</summary>
        [Test]
        public void 元素里的类级条件各元素各判()
        {
            var target = ScriptableObject.CreateInstance<ElementClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);
                var conditioned = Find(tree.Root, "conditioned");
                var first = Find(conditioned.Children[0], "conditioned.Array.data[0]/元素开关组");

                Assert.That(first.IsVisible, Is.True, "起点。");

                SetBool(target, tree, "conditioned.Array.data[0].alive", false);

                Assert.That(first.IsVisible, Is.False, "条件指的是**这个元素**的 alive。");
                Assert.That(
                    Find(conditioned.Children[1], "conditioned.Array.data[1]/元素开关组").IsVisible,
                    Is.True,
                    "另一个元素不受影响。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 元素类型只带类级分组、但**集合没被本包接管**时：不建层，且从静默变成告警
        /// （判据变准的连带结果，行为变化写进了 CHANGELOG）。
        /// </summary>
        [Test]
        public void 未被接管的集合给类级分组的元素类型告警()
        {
            var target = ScriptableObject.CreateInstance<UnmanagedGroupedElementFixture>();
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("没有被本包接管"));

                var tree = BuildTree(target);

                Assert.That(Find(tree.Root, "items").Children.Count, Is.EqualTo(0), "没有容器就没有画元素行的落点。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>分组装配不会扰动元素层本身：集合的直接子节点仍是按序的元素节点（投影不被搬走）。</summary>
        [Test]
        public void 元素节点不会被搬进分组()
        {
            var target = ScriptableObject.CreateInstance<ElementClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");

                for (var i = 0; i < items.Children.Count; i++)
                {
                    Assert.That(items.Children[i].Kind, Is.EqualTo(InspectorPropertyKind.Member));
                    Assert.That(items.Children[i].Path, Is.EqualTo($"items.Array.data[{i}]"), "下标投影保持原序。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>重建（增删之后整层重来）之后类级分组仍在——注入随第一趟处理器重跑。</summary>
        [Test]
        public void 重建后类级分组仍在()
        {
            var target = ScriptableObject.CreateInstance<ElementClassLevelGroupFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");

                SetArraySize(target, tree, "items", 3);

                Assert.That(CollectionElementSync.Reconcile(items), Is.True, "长度对不上就该重建。");

                var group = Find(items.Children[2], "items.Array.data[2]/元素类级组");
                Assert.That(group.Kind, Is.EqualTo(InspectorPropertyKind.Group));
                Assert.That(group.Attributes.Get<PropertyGroupAttribute>().GroupID, Is.EqualTo(group.Path));
                Assert.That(Find(group, "items.Array.data[2].plain"), Is.Not.Null, "重建后分发照旧。");
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

        /// <summary>改一个数组的长度，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="path">数组字段名。</param>
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
        /// <returns>找到的子节点；不存在返回 <c>null</c>。</returns>
        private static InspectorProperty TryFind(InspectorProperty parent, string path)
        {
            foreach (var child in parent.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>按完整路径查找直接子节点；找不到直接判失败。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">子节点完整路径。</param>
        /// <returns>找到的子节点。</returns>
        private static InspectorProperty Find(InspectorProperty parent, string path)
        {
            var found = TryFind(parent, path);
            if (found == null)
            {
                Assert.Fail($"找不到节点 {path}。");
            }

            return found;
        }

        /// <summary>节点链上有没有某种绘制器。</summary>
        /// <typeparam name="T">绘制器类型。</typeparam>
        /// <param name="node">节点。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        private static bool ChainHas<T>(InspectorProperty node)
            where T : class
        {
            foreach (var entry in node.Chain.Entries)
            {
                if (entry.Drawer is T)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>递归校验 <c>GroupID</c> 与节点路径一致。</summary>
        /// <param name="node">当前节点。</param>
        /// <param name="visited">已访问的分组节点计数。</param>
        private static void AssertGroupIdMatchesPath(InspectorProperty node, ref int visited)
        {
            if (node.Kind == InspectorPropertyKind.Group)
            {
                var attribute = node.Attributes.Get<PropertyGroupAttribute>();

                Assert.That(attribute, Is.Not.Null, $"分组节点 {node.Path} 上没有分组特性。");
                Assert.That(attribute.GroupID, Is.EqualTo(node.Path), "两条前缀叠加之后路径仍须自洽。");
                visited++;
            }

            foreach (var child in node.Children)
            {
                AssertGroupIdMatchesPath(child, ref visited);
            }
        }

        #endregion
    }

    #region Fixtures

    /// <summary>只带类级分组的嵌套类型——成员一个特性都没有（钉住判据的类级腿）。</summary>
    [Serializable]
    [BoxGroup("嵌套类级组")]
    internal class GroupOnlyNested
    {
        /// <summary>普通字段。</summary>
        public int plain = 1;

        /// <summary>第二个普通字段——与上一个共享同一份注入，用来验证「注入的是副本」。</summary>
        public int second = 2;
    }

    /// <summary>类级分组 + 一个自带分组的成员。</summary>
    [Serializable]
    [BoxGroup("嵌套类级组")]
    internal class GroupedNested
    {
        /// <summary>没有自身分组的成员——归入类级分组。</summary>
        public int plain = 1;

        /// <summary>自带分组的成员——嵌在类级分组里面。</summary>
        [BoxGroup("自有组")]
        public int nested = 2;
    }

    /// <summary>普通嵌套类型：字段自己的分组不该被当成类级分组。</summary>
    [Serializable]
    internal class UngroupedNested
    {
        /// <summary>带一个与分组无关的成员级特性（让类型被展开）。</summary>
        [ReadOnly]
        public int plain = 1;
    }

    /// <summary>类级分组条件（<c>[ShowIfGroup]</c>）标在类型上。</summary>
    [Serializable]
    [ShowIfGroup("开关组", Condition = nameof(alive))]
    internal class ConditionedNested
    {
        /// <summary>开关——条件指的是**这个嵌套实例**上的它。</summary>
        public bool alive = true;

        /// <summary>被条件分组包着的成员。</summary>
        public int hp = 10;
    }

    /// <summary>类级页签标在类型上——加前缀后仍要认出容器。</summary>
    [Serializable]
    [TabGroup("页签", "基础")]
    internal class TabbedNested
    {
        /// <summary>第一页的成员。</summary>
        public int first = 1;
    }

    /// <summary>更深一层：字段的声明类型带类级分组（中间层自己没有特性）。</summary>
    [Serializable]
    internal class DeepOuter
    {
        /// <summary>字段的声明类型带类级分组。</summary>
        public GroupedNested inner = new GroupedNested();
    }

    /// <summary>类型上挂着两个类级分组——只取第一个。</summary>
    [Serializable]
    [BoxGroup("组甲")]
    [BoxGroup("组乙")]
    internal class TwoClassGroupsNested
    {
        /// <summary>无自有分组——只该注入一份。</summary>
        public int value = 1;
    }

    /// <summary>嵌套层类级分组的主对照资产。</summary>
    [HideMonoScript]
    internal sealed class NestedClassLevelGroupFixture : ScriptableObject
    {
        /// <summary>根上的同名开关——用来验证条件解析看的是嵌套实例。</summary>
        public bool alive = true;

        /// <summary>只带类级分组 → 展开并分发。</summary>
        public GroupOnlyNested only = new GroupOnlyNested();

        /// <summary>类级分组 + 成员自有分组。</summary>
        public GroupedNested stats = new GroupedNested();

        /// <summary>同一个类型用在第二处——两处各是各的。</summary>
        public GroupedNested other = new GroupedNested();

        /// <summary>容器字段自己带分组、类型不带——字段的分组不该变类级分组。</summary>
        [BoxGroup("字段的组")]
        public UngroupedNested fieldGrouped = new UngroupedNested();

        /// <summary>类级分组条件。</summary>
        public ConditionedNested conditional = new ConditionedNested();

        /// <summary>类级页签。</summary>
        public TabbedNested tabbed = new TabbedNested();

        /// <summary>多级嵌套。</summary>
        public DeepOuter outer = new DeepOuter();
    }

    /// <summary>两个类级分组的对照资产。</summary>
    [HideMonoScript]
    internal sealed class TwoClassGroupsFixture : ScriptableObject
    {
        /// <summary>类型上挂着两个类级分组。</summary>
        public TwoClassGroupsNested stats = new TwoClassGroupsNested();
    }

    /// <summary>元素类型上带类级分组（成员一个特性都没有）。</summary>
    [Serializable]
    [BoxGroup("元素类级组")]
    internal class GroupedElement
    {
        /// <summary>普通字段。</summary>
        public int plain = 1;
    }

    /// <summary>元素类型上带类级条件分组。</summary>
    [Serializable]
    [ShowIfGroup("元素开关组", Condition = nameof(alive))]
    internal class ConditionedElement
    {
        /// <summary>开关（**元素内部**的成员）。</summary>
        public bool alive = true;

        /// <summary>被条件分组包着的成员。</summary>
        public int hp = 10;
    }

    /// <summary>元素层类级分组的对照资产。</summary>
    [HideMonoScript]
    internal sealed class ElementClassLevelGroupFixture : ScriptableObject
    {
        /// <summary>元素类型只带类级分组 → 元素阀放行。</summary>
        [ListDrawerSettings]
        public List<GroupedElement> items = new List<GroupedElement>
        {
            new GroupedElement(), new GroupedElement(),
        };

        /// <summary>元素里带类级条件分组 → 各元素各判。</summary>
        [ListDrawerSettings]
        public List<ConditionedElement> conditioned = new List<ConditionedElement>
        {
            new ConditionedElement(), new ConditionedElement(),
        };
    }

    /// <summary>集合未被接管、元素类型只带类级分组——从静默 Inert 变成 NoContainer 告警。</summary>
    [HideMonoScript]
    internal sealed class UnmanagedGroupedElementFixture : ScriptableObject
    {
        /// <summary>没有容器特性 → 不建层、告警一次。</summary>
        public List<GroupedElement> items = new List<GroupedElement> { new GroupedElement() };
    }

    #endregion
}
