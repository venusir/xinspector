using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 分组装配：节点位置、祖先合成、归属、排序。
    /// <para>
    /// 分组是「声明式」的——每个字段各自声明自己属于谁，没有任何集中登记。
    /// 于是装配规则必须精确，否则同一份声明在不同字段顺序下会长出不同的树，
    /// 而现象（「分组里少了个字段」「分组跑到底下去了」）很难联想到装配规则。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PropertyGroupNestingTests
    {
        #region Private Fields

        private GroupingFixture _target;
        private OrderingFixture _ordering;
        private SynthesisFixture _synthesis;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<GroupingFixture>();
            _ordering = ScriptableObject.CreateInstance<OrderingFixture>();
            _synthesis = ScriptableObject.CreateInstance<SynthesisFixture>();
        }

        /// <summary>销毁临时资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            Destroy(ref _target);
            Destroy(ref _ordering);
            Destroy(ref _synthesis);

            DrawerTypeRegistry.Reset();
        }

        /// <summary>销毁一个临时资产。</summary>
        /// <typeparam name="T">资产的具体类型。</typeparam>
        /// <param name="target">资产引用，销毁后置空。</param>
        /// <remarks>
        /// 必须是泛型：<c>ref</c> 参数不支持协变，<c>ref GroupingFixture</c>
        /// 无法传给 <c>ref ScriptableObject</c> 形参。
        /// </remarks>
        private static void Destroy<T>(ref T target) where T : ScriptableObject
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
                target = null;
            }
        }

        #endregion

        #region 节点位置

        /// <summary>
        /// 分组节点落在其首个成员出现的位置，夹在中间的未分组字段留在原地。
        /// <para>
        /// 这条挡住的是「先摆所有分组、再摆散字段」那种朴素实现——它的现象是
        /// 未分组字段全被挤到末尾，一眼就能看出不对。
        /// </para>
        /// </summary>
        [Test]
        public void Build_GroupSitsAtFirstMemberPositionAndUngroupedStayPut()
        {
            var tree = BuildTree(_target);
            var order = ChildPathsOf(tree.Root);

            var before = order.IndexOf("before");
            var outer = order.IndexOf("Outer");
            var middle = order.IndexOf("middle");
            var after = order.IndexOf("after");

            Assert.That(before, Is.GreaterThanOrEqualTo(0));
            Assert.That(outer, Is.GreaterThanOrEqualTo(0));
            Assert.That(middle, Is.GreaterThanOrEqualTo(0));
            Assert.That(after, Is.GreaterThanOrEqualTo(0));

            Assert.That(before, Is.LessThan(outer), $"Outer 应在 before 之后。实际顺序：{Dump(tree.Root)}");
            Assert.That(outer, Is.LessThan(middle), $"Outer 应在 middle 之前（它落在首个成员的位置）。实际顺序：{Dump(tree.Root)}");
            Assert.That(middle, Is.LessThan(after), $"middle 应留在原地。实际顺序：{Dump(tree.Root)}");
        }

        /// <summary>
        /// 未声明分组的成员仍是根的直接子节点。
        /// </summary>
        [Test]
        public void Build_UngroupedMembersStayUnderRoot()
        {
            var tree = BuildTree(_target);

            Assert.That(Find(tree.Root, "before"), Is.Not.Null);
            Assert.That(Find(tree.Root, "before").Parent, Is.SameAs(tree.Root));
            Assert.That(Find(tree.Root, "middle").Parent, Is.SameAs(tree.Root));
        }

        #endregion

        #region 祖先合成

        /// <summary>
        /// 只声明深层路径时，祖先节点由构建期合成出来。
        /// <para>
        /// 用 <see cref="SynthesisFixture"/> 而不是 <see cref="GroupingFixture"/>：
        /// 后者另有一个字段显式声明了外层分组，那样测到的就不是「纯合成」了。
        /// </para>
        /// </summary>
        [Test]
        public void Build_DeepPathSynthesizesAncestors()
        {
            var tree = BuildTree(_synthesis);

            var solo = Find(tree.Root, "Solo");
            var leaf = Find(solo, "Solo/Leaf");

            Assert.That(solo, Is.Not.Null, "只写了 Solo/Leaf，Solo 应当被合成。");
            Assert.That(leaf, Is.Not.Null);
            Assert.That(leaf.Parent, Is.SameAs(solo));
            Assert.That(solo.Kind, Is.EqualTo(InspectorPropertyKind.Group));
            Assert.That(leaf.Kind, Is.EqualTo(InspectorPropertyKind.Group));
        }

        /// <summary>
        /// 分组被多次声明时，呈现设定取**先声明者**的值。
        /// <para>
        /// 这是 <see cref="PropertyGroupAttribute.Combine"/> 明文规定的规则，
        /// 用例把它钉住——「先声明者优先」若哪天变成「后声明者覆盖」，
        /// 分组标题与排序会随字段顺序悄悄变化。
        /// </para>
        /// </summary>
        [Test]
        public void Build_GroupPresentationFollowsFirstDeclaration()
        {
            var tree = BuildTree(_target);

            var outer = Find(tree.Root, "Outer");
            var attribute = outer.Attributes.Get<BoxGroupAttribute>();

            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.ShowLabel, Is.True,
                "Outer 先由字段 a 以默认 ShowLabel = true 声明，后来字段 b 的 ShowLabel = false 不应覆盖它。");
        }

        /// <summary>
        /// 深层分组的成员是**叶子**分组节点的子节点，而不是祖先的直接子节点。
        /// </summary>
        [Test]
        public void Build_MemberOfDeepGroupIsChildOfLeaf()
        {
            var tree = BuildTree(_target);

            var inner = Find(Find(tree.Root, "Outer"), "Outer/Inner");
            var b = Find(inner, "b");

            Assert.That(b, Is.Not.Null);
            Assert.That(b.Parent, Is.SameAs(inner));
        }

        /// <summary>
        /// 合成的祖先继承来源分组特性的子类字段。
        /// <para>
        /// 用的是 <c>CloneForPath</c>（MemberwiseClone）而非重新构造，
        /// 所以 <see cref="BoxGroupAttribute.ShowLabel"/> 这类子类字段会一并带上来。
        /// </para>
        /// </summary>
        [Test]
        public void Build_SynthesizedAncestorInheritsSubclassFields()
        {
            var tree = BuildTree(_synthesis);

            var solo = Find(tree.Root, "Solo");
            var attribute = solo.Attributes.Get<BoxGroupAttribute>();

            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.ShowLabel, Is.False,
                "Solo 是从 Solo/Leaf 上复制出来的，ShowLabel = false 应一并带上来。");
            Assert.That(attribute.GroupName, Is.EqualTo("Solo"), "复制时必须改写路径，否则标题会显示成 Leaf。");
        }

        #endregion

        #region 不变量

        /// <summary>
        /// 每个分组节点上的分组特性，其 <c>GroupID</c> 必须等于该节点的路径。
        /// <para>
        /// 这是分组装配的核心不变量：绘制器因此不必猜「这个特性在描述哪一层」。
        /// 它一旦被破坏，分组标题会张冠李戴，而那种错很难看出根源在装配期。
        /// </para>
        /// </summary>
        [Test]
        public void Build_GroupAttributeIdMatchesNodePath()
        {
            var tree = BuildTree(_target);
            var visited = 0;

            AssertGroupIdMatchesPath(tree.Root, ref visited);

            Assert.That(visited, Is.GreaterThan(0), "没有访问到任何分组节点，这条不变量等于没检查。");
        }

        /// <summary>
        /// 分组节点挂上了分组绘制器。
        /// </summary>
        [Test]
        public void Build_GroupNodeHasBoxGroupDrawerInChain()
        {
            var tree = BuildTree(_target);
            var outer = Find(tree.Root, "Outer");

            var found = false;
            foreach (var entry in outer.Chain.Entries)
            {
                if (entry.Drawer is BoxGroupDrawer)
                {
                    found = true;
                    break;
                }
            }

            Assert.That(found, Is.True, "分组节点的链上应有 BoxGroupDrawer——特性与绘制器的配对在构建期完成。");
        }

        #endregion

        #region 排序

        /// <summary>
        /// 同层分组之间按 Order 重排。
        /// </summary>
        [Test]
        public void Build_GroupOrderReordersGroups()
        {
            var tree = BuildTree(_ordering);
            var order = ChildPathsOf(tree.Root);

            var zed = order.IndexOf("Zed");
            var alpha = order.IndexOf("Alpha");

            Assert.That(zed, Is.GreaterThanOrEqualTo(0));
            Assert.That(alpha, Is.GreaterThanOrEqualTo(0));
            Assert.That(alpha, Is.LessThan(zed),
                $"Alpha 的 Order 更小，应排在前面。实际顺序：{Dump(tree.Root)}");
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

        /// <summary>取某节点全部直接子节点的路径。</summary>
        /// <param name="parent">父节点。</param>
        /// <returns>路径列表。</returns>
        private static List<string> ChildPathsOf(InspectorProperty parent)
        {
            var paths = new List<string>();
            foreach (var child in parent.Children)
            {
                paths.Add(child.Path);
            }

            return paths;
        }

        /// <summary>按**完整路径**查找直接子节点。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">子节点的完整路径。</param>
        /// <returns>找到的子节点；不存在返回 <c>null</c>。</returns>
        /// <remarks>
        /// 匹配的是 <see cref="InspectorProperty.Path"/> 而不是 <see cref="InspectorProperty.Name"/>。
        /// 对嵌套分组来说两者不同：<c>Outer/Inner</c> 节点显示名是 <c>Inner</c>，
        /// 路径却是 <c>Outer/Inner</c>。这正是「分组特性描述的路径等于节点路径」那条不变量的体现——
        /// 节点身份是全路径，因为显示名在树里可能重名。
        /// </remarks>
        private static InspectorProperty Find(InspectorProperty parent, string path)
        {
            if (parent == null)
            {
                return null;
            }

            foreach (var child in parent.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>把整棵树的形状转成一行文本，供断言失败时定位。</summary>
        /// <param name="node">起始节点。</param>
        /// <returns>形如 <c>Root[before, Outer[a, Inner[b]], middle]</c> 的字符串。</returns>
        private static string Dump(InspectorProperty node)
        {
            if (node.Children.Count == 0)
            {
                return node.Path.Length == 0 ? "(empty)" : node.Path;
            }

            var parts = new List<string>();
            foreach (var child in node.Children)
            {
                parts.Add(Dump(child));
            }

            var name = node.Path.Length == 0 ? "Root" : node.Name;
            return $"{name}[{string.Join(", ", parts)}]";
        }

        /// <summary>递归校验 GroupID 与节点路径一致。</summary>
        /// <param name="node">当前节点。</param>
        /// <param name="visited">已访问的分组节点计数。</param>
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

    /// <summary>
    /// 分组位置测试用资产：未分组字段刻意夹在分组字段之间。
    /// </summary>
    internal sealed class GroupingFixture : ScriptableObject
    {
        /// <summary>分组之前的散字段。</summary>
        public int before;

        /// <summary>外层分组的首个成员。</summary>
        [BoxGroup("Outer")]
        public int a;

        /// <summary>夹在两个分组之间的散字段。</summary>
        public int middle;

        /// <summary>深层分组的成员——Outer 与 Inner 都应由它合成。</summary>
        [BoxGroup("Outer/Inner", ShowLabel = false)]
        public int b;

        /// <summary>末尾的散字段。</summary>
        public int after;
    }

    /// <summary>
    /// 纯合成测试用资产：全树只有一处声明，且是两段的深层路径，
    /// 于是外层分组只可能来自构建期的合成。
    /// </summary>
    internal sealed class SynthesisFixture : ScriptableObject
    {
        /// <summary>唯一的分组声明。</summary>
        [BoxGroup("Solo/Leaf", ShowLabel = false)]
        public int value;
    }

    /// <summary>
    /// 排序测试用资产：声明顺序与 Order 顺序刻意相反。
    /// </summary>
    internal sealed class OrderingFixture : ScriptableObject
    {
        /// <summary>声明在前但 Order 较大。</summary>
        [BoxGroup("Zed", 10f)]
        public int z;

        /// <summary>声明在后但 Order 较小，应被排到前面。</summary>
        [BoxGroup("Alpha", -10f)]
        public int alpha;
    }
}
