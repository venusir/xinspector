using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 标题特性与绘制器的配对。
    /// <para>
    /// 重点验证**类级与成员级走同一条路径**：构建期把类型上的特性直接放到根节点，
    /// 于是类级标题不需要任何特例代码——它就是同一个绘制器出现在了根节点上。
    /// 这条一旦退化（例如有人改成在构建期特殊处理类级标题），本 fixture 会立刻变红。
    /// </para>
    /// <para>
    /// 绘制本身（文字长什么样）不在这里验证：IMGUI 的渲染结果无法有意义地断言，
    /// 伪造 GUI 上下文只会得到「测试断言了自己的 mock」。这里验证的是**链上有没有它、
    /// 以及它在第几位**，那才是会出错的部分。
    /// </para>
    /// </summary>
    [TestFixture]
    public class TitleDrawerTests
    {
        #region Private Fields

        private TitleFixture _target;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<TitleFixture>();
        }

        /// <summary>销毁临时资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region 类级标题

        /// <summary>
        /// 类上的 <c>[Title]</c> 出现在根节点上。
        /// </summary>
        [Test]
        public void Build_ClassLevelTitleIsOnRootNode()
        {
            var tree = BuildTree();

            Assert.That(tree.Root.HasAttribute<TitleAttribute>(), Is.True,
                "类型上的特性应当由构建期直接放到根节点上。");
            Assert.That(tree.Root.GetAttribute<TitleAttribute>().Title, Is.EqualTo("Fixture Title"));
        }

        /// <summary>
        /// 根节点的链上有标题绘制器，且排在子节点绘制器**之前**——
        /// 顺序反了标题就会跑到所有字段下面去。
        /// </summary>
        [Test]
        public void Build_ClassLevelTitleDrawerRunsBeforeChildren()
        {
            var tree = BuildTree();

            var titleIndex = IndexOfDrawer<TitleAttributeDrawer>(tree.Root);
            var childrenIndex = IndexOfDrawer<ChildrenDrawer>(tree.Root);

            Assert.That(titleIndex, Is.GreaterThanOrEqualTo(0), "根节点的链上应有标题绘制器。");
            Assert.That(childrenIndex, Is.GreaterThanOrEqualTo(0), "根节点的链上应有子节点绘制器。");
            Assert.That(titleIndex, Is.LessThan(childrenIndex), "标题必须画在子节点之前。");
        }

        #endregion

        #region 成员级标题

        /// <summary>
        /// 成员上的 <c>[Title]</c> 落在该成员自己的链上，且排在末端绘制器之前。
        /// </summary>
        [Test]
        public void Build_MemberTitleDrawerRunsBeforeTerminal()
        {
            var tree = BuildTree();
            var member = Find(tree.Root, "titled");

            Assert.That(member, Is.Not.Null);

            var titleIndex = IndexOfDrawer<TitleAttributeDrawer>(member);
            var terminalIndex = IndexOfDrawer<UnityFallbackDrawer>(member);

            Assert.That(titleIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(terminalIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(titleIndex, Is.LessThan(terminalIndex), "标题在内侧内容之前。");
        }

        /// <summary>
        /// 没有标题的成员，链上不应出现标题绘制器。
        /// </summary>
        [Test]
        public void Build_UntitledMemberHasNoTitleDrawer()
        {
            var tree = BuildTree();
            var member = Find(tree.Root, "plain");

            Assert.That(member, Is.Not.Null);
            Assert.That(IndexOfDrawer<TitleAttributeDrawer>(member), Is.EqualTo(-1));
        }

        #endregion

        #region Private Helpers

        /// <summary>构建被测的树。</summary>
        /// <returns>属性树。</returns>
        private PropertyTree BuildTree()
        {
            return PropertyTree.Create(new SerializedObject(_target));
        }

        /// <summary>在节点的链上查找指定类型绘制器的下标。</summary>
        /// <typeparam name="T">绘制器类型。</typeparam>
        /// <param name="property">目标节点。</param>
        /// <returns>下标；不存在返回 -1。</returns>
        private static int IndexOfDrawer<T>(InspectorProperty property) where T : XInspectorDrawer
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

        /// <summary>按完整路径查找直接子节点。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">子节点完整路径。</param>
        /// <returns>找到的子节点；不存在返回 <c>null</c>。</returns>
        private static InspectorProperty Find(InspectorProperty parent, string path)
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

        #endregion
    }

    /// <summary>
    /// 标题测试用资产：类级与成员级各一个标题，外加一个无标题成员作对照。
    /// </summary>
    [Title("Fixture Title")]
    internal sealed class TitleFixture : ScriptableObject
    {
        /// <summary>带标题的成员。</summary>
        [Title("Member Title")]
        public int titled;

        /// <summary>无标题的成员，用作对照。</summary>
        public int plain;
    }
}
