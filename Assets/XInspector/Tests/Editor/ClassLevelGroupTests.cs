using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 类级分组分发——「父级注入」钩子的真正用途。
    /// <para>
    /// 它修的是本包此前的一条已知限制：类级 <c>[BoxGroup]</c> 过去会把**整个 Inspector**
    /// 框起来，而不是让成员归属该分组。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ClassLevelGroupTests
    {
        #region Private Fields

        private ClassGroupFixture _target;
        private PlainClassGroupFixture _plainTarget;

        #endregion

        #region Setup / Teardown

        /// <summary>建立资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ClassGroupFixture>();
            _plainTarget = ScriptableObject.CreateInstance<PlainClassGroupFixture>();

            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        /// <summary>销毁资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            if (_plainTarget != null)
            {
                Object.DestroyImmediate(_plainTarget);
                _plainTarget = null;
            }

            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region 注入

        /// <summary>
        /// 没有自身分组的成员被归入类级分组。
        /// </summary>
        [Test]
        public void UngroupedMember_JoinsClassGroup()
        {
            var tree = BuildTree(_target);

            var outer = Find(tree.Root, "外层");
            Assert.That(outer, Is.Not.Null, "类级 [BoxGroup] 应当被分发到成员上，从而长出分组节点。");

            Assert.That(Find(outer, "plain"), Is.Not.Null, "未分组的成员应当落进类级分组里。");
        }

        /// <summary>
        /// **类的分组恒在最外层**：成员自己的分组嵌在它里面，而不是与之并列。
        /// </summary>
        [Test]
        public void MemberOwnGroup_NestsInsideClassGroup()
        {
            var tree = BuildTree(_target);

            var outer = Find(tree.Root, "外层");
            var nested = Find(outer, "外层/内层");

            Assert.That(nested, Is.Not.Null, "成员自己的分组应当被改写为「类级/自身」的路径。");
            Assert.That(Find(nested, "nested"), Is.Not.Null, "该成员应当落在内层分组里。");
        }

        /// <summary>
        /// 分发之后，类级分组本身不再是一个「框住整个 Inspector」的节点——
        /// 它现在有真正的子成员了。
        /// </summary>
        [Test]
        public void ClassGroup_HasMembersInsteadOfBeingRootLevelOnly()
        {
            var tree = BuildTree(_target);

            var outer = Find(tree.Root, "外层");
            Assert.That(outer, Is.Not.Null);
            Assert.That(outer.Kind, Is.EqualTo(InspectorPropertyKind.Group));
            Assert.That(outer.Children.Count, Is.GreaterThan(0), "分组节点应当有子节点，而不是空的。");
        }

        /// <summary>
        /// 类型上没有分组特性时，什么都不注入——**回归守卫**：
        /// 这个处理器对绝大多数类型都应当是完全惰性的。
        /// </summary>
        [Test]
        public void WithoutClassGroup_NothingIsInjected()
        {
            var tree = BuildTree(_plainTarget);

            foreach (var child in tree.Root.Children)
            {
                Assert.That(child.Kind, Is.EqualTo(InspectorPropertyKind.Member),
                    "没有类级分组时不该出现分组节点。");
            }
        }

        /// <summary>
        /// 注入的是**副本**而不是同一个实例。
        /// <para>
        /// 直接塞同一个实例的话，多个成员会共享它，后续任何一处改写都会串到所有人身上——
        /// 这正是「每个属性一份独立的特性实例」那条不变量要防的事。
        /// </para>
        /// </summary>
        [Test]
        public void InjectedAttributes_AreDistinctInstances()
        {
            var tree = BuildTree(_target);

            var classLevelOriginal = tree.Root.Attributes.Get<PropertyGroupAttribute>();
            Assert.That(classLevelOriginal, Is.Not.Null, "前提不成立：根上没有类级分组特性。");

            // 从**子节点**开始收集：根自己携带的就是那个原始实例，
            // 把它收进来再断言「不等于它自己」是恒假的。
            var collected = new List<PropertyGroupAttribute>();
            foreach (var child in tree.Root.Children)
            {
                CollectGroupAttributes(child, collected);
            }

            Assert.That(collected, Is.Not.Empty, "树里应当存在分组特性。");

            // 逐个比对而不是先查集合是否包含它：集合断言在这里会落到字符串重载上，
            // 而且逐个比对给出的失败信息更直接（指出是哪一个实例被共享了）。
            foreach (var group in collected)
            {
                Assert.That(group, Is.Not.SameAs(classLevelOriginal),
                    "分发出去的分组特性必须是副本：共享同一个实例的话，"
                    + "后续任何一处改写都会串到所有成员身上。");
            }
        }

        #endregion

        #region 不变量

        /// <summary>
        /// 分发之后 <c>GroupID == node.Path</c> 这条不变量仍然成立。
        /// <para>
        /// 改写路径是最容易破坏它的操作——前缀拼错或漏改，分组标题就会张冠李戴。
        /// </para>
        /// </summary>
        [Test]
        public void GroupIdStillMatchesNodePath()
        {
            var tree = BuildTree(_target);
            var visited = 0;

            AssertGroupIdMatchesPath(tree.Root, ref visited);

            Assert.That(visited, Is.GreaterThan(0), "没有访问到任何分组节点，这条不变量等于没检查。");
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

        /// <summary>递归收集树上所有的分组特性实例。</summary>
        /// <param name="node">起始节点。</param>
        /// <param name="collected">收集结果。</param>
        private static void CollectGroupAttributes(InspectorProperty node, List<PropertyGroupAttribute> collected)
        {
            foreach (var attribute in node.Attributes)
            {
                if (attribute is PropertyGroupAttribute group)
                {
                    collected.Add(group);
                }
            }

            foreach (var child in node.Children)
            {
                CollectGroupAttributes(child, collected);
            }
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
                    "分发改写了路径之后，特性描述的路径仍须与节点路径一致。");
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
    /// 类级分组测试用资产：类型带分组，一个成员不带、一个成员自带分组。
    /// </summary>
    [BoxGroup("外层")]
    internal sealed class ClassGroupFixture : ScriptableObject
    {
        /// <summary>没有自身分组的成员。</summary>
        public int plain;

        /// <summary>自带分组的成员——它应当嵌进类级分组里。</summary>
        [BoxGroup("内层")]
        public int nested;
    }

    /// <summary>
    /// 对照组：类型上没有分组特性。
    /// </summary>
    internal sealed class PlainClassGroupFixture : ScriptableObject
    {
        /// <summary>普通成员。</summary>
        public int alpha;

        /// <summary>另一个普通成员。</summary>
        public int beta;
    }
}
