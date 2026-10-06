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
    /// 嵌套层的**分组装配**：分组节点挂在复合成员之下、路径以父成员的序列化路径为前缀。
    /// <para>
    /// 不测 IMGUI——断言的是节点形状、路径、链的构成与可见性求值器，都是构建期可无头检查的东西。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 这一层与顶层的两处结构差别，是本文件大部分用例的由来：
    /// **其一**，嵌套子节点在收集期就已挂在父节点下，装配要先把它们搬走（顶层是装配负责挂）；
    /// **其二**，同一个嵌套类型会用在不同字段上，分组路径必须带前缀才不互相串组。
    /// </remarks>
    [TestFixture]
    public class NestedGroupingTests
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

        #region 装配形状

        /// <summary>嵌套层的分组长出节点，成员落进最深的那一节。</summary>
        [Test]
        public void 嵌套层的分组长出节点且成员落进去()
        {
            var target = ScriptableObject.CreateInstance<NestedGroupingFixture>();
            try
            {
                var tree = BuildTree(target);
                var stats = Find(tree.Root, "stats");
                var outer = Find(stats, "stats/外框");

                Assert.That(outer.Kind, Is.EqualTo(InspectorPropertyKind.Group));
                Assert.That(outer.Parent, Is.SameAs(stats), "分组节点挂在**复合成员**之下。");

                var inner = Find(outer, "stats/外框/内框");
                Assert.That(inner.Kind, Is.EqualTo(InspectorPropertyKind.Group));
                Assert.That(ChildOf(inner, "stats.b"), Is.Not.Null, "b 落在最深的那一节里。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>未分组字段留在原地——「分组落在其首个成员出现的位置」在嵌套层逐字继承。</summary>
        [Test]
        public void 嵌套层未分组成员留在原地()
        {
            var target = ScriptableObject.CreateInstance<NestedGroupingFixture>();
            try
            {
                var tree = BuildTree(target);
                var stats = Find(tree.Root, "stats");

                Assert.That(
                    Paths(stats),
                    Is.EqualTo(new[] { "stats.before", "stats/外框", "stats.middle", "stats.after" }),
                    "外框落在 a 的位置；middle 与 after 不被挤到末尾。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 不生成以成员名命名的多余分组节点——前缀**不是**分组段。
        /// </summary>
        /// <remarks>
        /// 把 <c>"stats/外框"</c> 整条交给逐段造节点的实现，会先造出一个名为 <c>stats</c> 的
        /// **分组**节点挂在同样叫 <c>stats</c> 的**成员**节点下：既是多余的一层框，
        /// 又让节点 <c>Path</c> 不再唯一。这是本次改动最核心的回归守卫。
        /// </remarks>
        [Test]
        public void 不生成以成员名命名的多余分组节点()
        {
            var target = ScriptableObject.CreateInstance<NestedGroupingFixture>();
            try
            {
                var tree = BuildTree(target);
                var stats = Find(tree.Root, "stats");

                Assert.That(ChildOf(stats, "stats"), Is.Null,
                    "stats 之下不该再有一个叫 stats 的分组节点。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>嵌套层的每份分组特性都满足路径不变量（全树递归）。</summary>
        [Test]
        public void 嵌套组的路径不变量()
        {
            var target = ScriptableObject.CreateInstance<NestedGroupingFixture>();
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
        /// 同一个嵌套类型用在两处时，两组分组节点各自独立——**这条是这一轮的立项理由**。
        /// </summary>
        [Test]
        public void 同名分组在不同嵌套容器里各自独立()
        {
            var target = ScriptableObject.CreateInstance<NestedGroupingFixture>();
            try
            {
                var tree = BuildTree(target);
                var statsOuter = Find(tree.Root, "stats/外框");
                var otherOuter = Find(tree.Root, "other/外框");

                Assert.That(statsOuter, Is.Not.SameAs(otherOuter), "两处各有自己的分组节点。");
                Assert.That(ChildOf(statsOuter, "stats.a"), Is.Not.Null);
                Assert.That(ChildOf(otherOuter, "stats.a"), Is.Null, "两处的成员不会串到对方组里。");
                Assert.That(ChildOf(otherOuter, "other.a"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>再深一层（<c>outer.deep</c>）同样装配，路径逐层带上父成员的路径。</summary>
        [Test]
        public void 嵌套层再深一层同样装配()
        {
            var target = ScriptableObject.CreateInstance<NestedGroupingFixture>();
            try
            {
                var tree = BuildTree(target);
                var deep = Find(tree.Root, "outer.deep");
                var outer = Find(deep, "outer.deep/外框");

                Assert.That(outer.Kind, Is.EqualTo(InspectorPropertyKind.Group));
                Assert.That(outer.Parent, Is.SameAs(deep));
                Assert.That(ChildOf(outer, "outer.deep.a"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>嵌套层的分组按 <c>Order</c> 重排——递归必须进得了成员节点。</summary>
        [Test]
        public void 嵌套层分组按Order重排()
        {
            var target = ScriptableObject.CreateInstance<NestedGroupingFixture>();
            try
            {
                var tree = BuildTree(target);
                var ordered = Find(tree.Root, "ordered");

                Assert.That(
                    Paths(ordered),
                    Is.EqualTo(new[] { "ordered/先", "ordered/后" }),
                    "声明顺序是「后」在前，Order 把「先」排了上来。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>分组绘制器只落在分组节点上：新节点自带链与绘制器。</summary>
        [Test]
        public void 嵌套分组节点配了分组绘制器()
        {
            var target = ScriptableObject.CreateInstance<NestedGroupingFixture>();
            try
            {
                var tree = BuildTree(target);
                var outer = Find(tree.Root, "stats/外框");

                Assert.That(IndexOf<BoxGroupDrawer>(outer), Is.GreaterThanOrEqualTo(0));
                Assert.That(
                    IndexOf<BoxGroupDrawer>(Find(tree.Root, "stats.a")),
                    Is.EqualTo(-1),
                    "成员节点上没有分组绘制器。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 嵌套层的分组条件

        /// <summary>
        /// 嵌套分组的条件跟随**同容器**的兄弟成员，不是根上的同名成员。
        /// </summary>
        /// <remarks>
        /// 夹具里根与嵌套层各有一个 <c>gate</c>，值**相反**——不这么放假的话，
        /// 「解析到了根上」与「解析到了同层」在某些取值下结果相同，这条就测不出「看错对象」。
        /// </remarks>
        [Test]
        public void 嵌套层的分组条件跟随同容器成员()
        {
            var target = ScriptableObject.CreateInstance<NestedGroupingFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "conditional/闸门");

                // 根上的 gate 为真、嵌套层的为假：若解析到了根上，这里就会显示。
                Assert.That(gated.IsVisible, Is.False, "条件指的是**同容器**的 gate。");

                SetBool(target, tree, "conditional.gate", true);
                Assert.That(gated.IsVisible, Is.True, "同容器的 gate 打开后可见。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 条件挂在**两段路径**的分组上时同样解析到本容器——该节点的父节点是**分组节点**。
        /// </summary>
        /// <remarks>
        /// 只看直接父节点的实现在这里会返回 null，于是落到「根上的绝对名」：
        /// 根上那个 <c>gate</c> 为真，整组就会**静默地**跟着根走。这正是
        /// <c>SerializedMemberResolver.FindNestedScope</c> 要沿父链跳过分组节点的理由。
        /// </remarks>
        [Test]
        public void 深一层分组上的条件仍解析到本容器()
        {
            var target = ScriptableObject.CreateInstance<NestedGroupingFixture>();
            try
            {
                var tree = BuildTree(target);
                var outer = Find(tree.Root, "conditional/外");

                Assert.That(outer.Kind, Is.EqualTo(InspectorPropertyKind.Group),
                    "「外」是祖先合成出来的分组节点，条件不该落在它身上。");
                Assert.That(outer.IsVisible, Is.True, "祖先不继承条件。");

                var deep = Find(outer, "conditional/外/条件");
                Assert.That(deep.Parent, Is.SameAs(outer), "它的父节点是分组节点。");
                Assert.That(deep.IsVisible, Is.False, "条件仍解析到**同容器**的 gate。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 不变量

        /// <summary>
        /// 嵌套层的分组**不会**误配到根上的类级分组条件声明。
        /// </summary>
        /// <remarks>
        /// 分组条件的第二来源是「根上那份类级声明」，判据是
        /// <c>rootAttr.GroupID == property.Path</c>。前缀让嵌套分组节点的路径必然与根上的
        /// 声明不同名，这条判据因此天然不成立——**前缀在这里是有功能收益的**，
        /// 不只是为了路径唯一。少了它，嵌套层里一个同名的分组会被根上的条件顺手改掉可见性。
        /// </remarks>
        [Test]
        public void 嵌套分组不误配根上的类级条件声明()
        {
            var target = ScriptableObject.CreateInstance<ClassLevelConditionFixture>();
            try
            {
                var tree = BuildTree(target);
                SetBool(target, tree, "flag", false);

                // 顶层那个「闸门」就是类级声明落点，它跟着条件走。
                Assert.That(Find(tree.Root, "闸门").IsVisible, Is.False);

                // 嵌套层里同名的那个分组：路径是 nested/闸门，与根上的声明无关。
                Assert.That(
                    Find(tree.Root, "nested/闸门").IsVisible,
                    Is.True,
                    "嵌套层的同名分组不该被根上的类级条件顺手改掉可见性。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 分组前缀**不**出现在成员的序列化路径上——按路径重置那条通道靠它。
        /// </summary>
        /// <remarks>
        /// 成员的 <c>Path</c> 会被直接交给 <c>SerializedObject.FindProperty</c>，
        /// 一旦带上分组前缀就再也找不到属性（症状是「重置静默地什么都不做」）。
        /// </remarks>
        [Test]
        public void 嵌套分组不改变成员的序列化路径()
        {
            var target = ScriptableObject.CreateInstance<NestedGroupingFixture>();
            try
            {
                var tree = BuildTree(target);
                var paths = PropertyTreeReset.CollectMemberPaths(tree);

                Assert.That(paths, Does.Contain("stats.a"));
                Assert.That(paths, Does.Contain("stats.b"));
                Assert.That(
                    paths,
                    Has.None.StartsWith("stats/"),
                    "分组前缀不该出现在成员路径里。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>没有分组特性的嵌套层照旧不重排——「没用到本包的类型零改动」的分支保证。</summary>
        [Test]
        public void 无分组的嵌套层原样不动()
        {
            var target = ScriptableObject.CreateInstance<NestedGroupingFixture>();
            try
            {
                var tree = BuildTree(target);
                var plain = Find(tree.Root, "plain");

                Assert.That(
                    Paths(plain),
                    Is.EqualTo(new[] { "plain.first", "plain.second" }),
                    "顺序与声明一致，且没有多出任何分组节点。");
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

        /// <summary>收集一层子节点的路径。</summary>
        /// <param name="parent">父节点。</param>
        /// <returns>按顺序排列的路径。</returns>
        private static string[] Paths(InspectorProperty parent)
        {
            var paths = new List<string>();
            foreach (var child in parent.Children)
            {
                paths.Add(child.Path);
            }

            return paths.ToArray();
        }

        /// <summary>递归查找节点（按完整路径）。</summary>
        /// <param name="root">搜索起点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；不存在时断言失败。</returns>
        private static InspectorProperty Find(InspectorProperty root, string path)
        {
            var found = Search(root, path);

            Assert.That(found, Is.Not.Null, $"找不到节点 {path}。");
            return found;
        }

        /// <summary>递归搜索。</summary>
        /// <param name="node">当前节点。</param>
        /// <param name="path">目标路径。</param>
        /// <returns>节点；不存在返回 <c>null</c>。</returns>
        private static InspectorProperty Search(InspectorProperty node, string path)
        {
            foreach (var child in node.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }

                var nested = Search(child, path);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        /// <summary>按路径取直接子节点。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">子节点路径。</param>
        /// <returns>节点；不存在返回 <c>null</c>。</returns>
        private static InspectorProperty ChildOf(InspectorProperty parent, string path)
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

        /// <summary>全树检查「分组节点的特性路径与节点路径一致」。</summary>
        /// <param name="node">当前节点。</param>
        /// <param name="visited">访问到的分组节点数。</param>
        private static void AssertGroupIdMatchesPath(InspectorProperty node, ref int visited)
        {
            if (node.Kind == InspectorPropertyKind.Group)
            {
                var attribute = node.Attributes.Get<PropertyGroupAttribute>();

                Assert.That(attribute, Is.Not.Null, $"分组节点 {node.Path} 上没有分组特性。");
                Assert.That(attribute.GroupID, Is.EqualTo(node.Path),
                    $"分组特性描述的路径与节点路径不一致——绘制器将无从判断它在画哪一层。");
                Assert.That(attribute.GroupName, Is.EqualTo(node.Name));
                visited++;
            }

            foreach (var child in node.Children)
            {
                AssertGroupIdMatchesPath(child, ref visited);
            }
        }

        #endregion
    }

    /// <summary>嵌套层的分组装配：一个类型被用在两处、另有一层带顺序的分组。</summary>
    [Serializable]
    internal class NestedGrouped
    {
        /// <summary>分组之前的散字段。</summary>
        public int before;

        /// <summary>外层分组的成员。</summary>
        [BoxGroup("外框")]
        public int a;

        /// <summary>夹在两个分组之间的散字段——它必须留在原地。</summary>
        public int middle;

        /// <summary>深一层分组的成员。</summary>
        [BoxGroup("外框/内框")]
        public int b;

        /// <summary>分组之后的散字段。</summary>
        public int after;
    }

    /// <summary>嵌套层里带顺序的分组：声明顺序与 Order 相反。</summary>
    [Serializable]
    internal class NestedOrdered
    {
        /// <summary>后一个。</summary>
        [BoxGroup("后", Order = 1f)]
        public int late;

        /// <summary>前一个。</summary>
        [BoxGroup("先", Order = -1f)]
        public int early;
    }

    /// <summary>嵌套层里的分组条件：<c>gate</c> 与根上同名、值相反。</summary>
    [Serializable]
    internal class NestedConditional
    {
        /// <summary>同容器的开关（根上那个为真，这个为假）。</summary>
        public bool gate;

        /// <summary>条件挂在一段路径的分组上。</summary>
        [ShowIfGroup("闸门", Condition = nameof(gate))]
        public int gated;

        /// <summary>条件挂在**两段路径**的分组上——该节点的父节点是分组节点。</summary>
        [BoxGroup("外/条件")]
        [ShowIfGroup("外/条件", Condition = nameof(gate))]
        public int deepGated;
    }

    /// <summary>没有分组特性的嵌套层——对照：装配不该动它。</summary>
    [Serializable]
    internal class NestedPlain
    {
        /// <summary>带本包特性（触发展开），但不是分组。</summary>
        [Title("普通标题")]
        public int first;

        /// <summary>普通字段。</summary>
        public int second;
    }

    /// <summary>再深一层的容器。</summary>
    [Serializable]
    internal class NestedOuter
    {
        /// <summary>再深一层。</summary>
        public NestedGrouped deep = new NestedGrouped();
    }

    /// <summary>类级分组条件 + 嵌套同名分组：两层各有一个叫「闸门」的分组。</summary>
    [ShowIfGroup("闸门", Condition = nameof(flag))]
    [HideMonoScript]
    internal sealed class ClassLevelConditionFixture : ScriptableObject
    {
        /// <summary>类级条件的开关。</summary>
        public bool flag = true;

        /// <summary>嵌套层里也有一个叫「闸门」的分组。</summary>
        public NestedGate nested = new NestedGate();
    }

    /// <summary>嵌套层里的同名分组。</summary>
    [Serializable]
    internal class NestedGate
    {
        /// <summary>与根上那个分组同名——前缀让它不会误配到根上的声明。</summary>
        [BoxGroup("闸门")]
        public int value;
    }

    /// <summary>嵌套层分组装配的对照资产。</summary>
    [HideMonoScript]
    internal sealed class NestedGroupingFixture : ScriptableObject
    {
        /// <summary>根上的同名开关——用来验证条件解析的是同容器那个。</summary>
        public bool gate = true;

        /// <summary>嵌套层带分组 → 装配出分组节点。</summary>
        public NestedGrouped stats = new NestedGrouped();

        /// <summary>同一个类型用在第二处——两组分组节点必须各自独立。</summary>
        public NestedGrouped other = new NestedGrouped();

        /// <summary>再深一层。</summary>
        public NestedOuter outer = new NestedOuter();

        /// <summary>带 Order 的嵌套分组。</summary>
        public NestedOrdered ordered = new NestedOrdered();

        /// <summary>带分组条件的嵌套层。</summary>
        public NestedConditional conditional = new NestedConditional();

        /// <summary>没有分组特性的嵌套层——对照。</summary>
        public NestedPlain plain = new NestedPlain();
    }
}
