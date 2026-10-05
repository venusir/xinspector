using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[PropertyOrder]</c>：成员的**稳定**排序、跨段（字段 / 反射成员 / 方法）生效，
    /// 以及它带来的连带后果（分组锚点随首个成员移动）。
    /// </summary>
    [TestFixture]
    public class PropertyOrderTests
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

        #region Tests

        /// <summary>
        /// 三段一起排：负值靠前、同权重稳定、**方法能插到字段之间**、未标注者保持相对次序。
        /// </summary>
        [Test]
        public void 按权重稳定排序()
        {
            var target = ScriptableObject.CreateInstance<PropertyOrderFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(
                    PathsOf(tree),
                    Is.EqualTo(new[]
                    {
                        "Reflected",    // -2：反射成员同样参与排序
                        "negA",         // -1
                        "negB",         // -1：与 negA 同权重，稳定排序保持声明先后
                        "MidAction()",  // -0.5：方法插到字段之间（新能力）
                        "unmarkedA",    // 0：未标注者之间保持声明先后
                        "unmarkedB",
                        "posA",         // 1
                    }));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>一个都不标时不改变任何次序——「稳定」这条性质的可观测形态。</summary>
        [Test]
        public void 未标注时保持声明顺序()
        {
            var target = ScriptableObject.CreateInstance<PlainOrderFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(PathsOf(tree), Is.EqualTo(new[] { "a", "b", "c" }));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 连带后果：分组节点落在**其首个成员出现的位置**，因此成员被排到前面时，
        /// 它所属的分组会跟着移动（默认情形见
        /// <c>PropertyGroupNestingTests.Build_GroupSitsAtFirstMemberPositionAndUngroupedStayPut</c>）。
        /// </summary>
        [Test]
        public void 分组锚点随首个成员移动()
        {
            var target = ScriptableObject.CreateInstance<OrderGroupFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(PathsOf(tree), Is.EqualTo(new[] { "ungrouped", "甲" }),
                    "未分组字段被排到最前，分组随之落到它后面。");
                Assert.That(Find(tree.Root, "甲").Children[0].Path, Is.EqualTo("groupedA"),
                    "组内次序不受影响：两个成员都未标注，保持声明先后。");
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

        /// <summary>按顺序取根的直接子节点路径。</summary>
        /// <param name="tree">属性树。</param>
        /// <returns>路径数组。</returns>
        private static string[] PathsOf(PropertyTree tree)
        {
            var children = tree.Root.Children;
            var paths = new string[children.Count];

            for (var i = 0; i < children.Count; i++)
            {
                paths[i] = children[i].Path;
            }

            return paths;
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

    /// <summary>三段混合的排序对照：字段、反射成员、方法各一，权重刻意交叉。</summary>
    [HideMonoScript]
    internal sealed class PropertyOrderFixture : ScriptableObject
    {
        /// <summary>排到最前。</summary>
        [PropertyOrder(-1f)]
        public int negA;

        /// <summary>未标注。</summary>
        public int unmarkedA;

        /// <summary>排到最后。</summary>
        [PropertyOrder(1f)]
        public int posA;

        /// <summary>与 negA 同权重——稳定排序下应排在它之后。</summary>
        [PropertyOrder(-1f)]
        public int negB;

        /// <summary>未标注。</summary>
        public int unmarkedB;

        /// <summary>反射成员也能排序。</summary>
        [ShowInInspector, PropertyOrder(-2f)]
        public int Reflected => 7;

        /// <summary>方法带顺序，插到字段之间。</summary>
        [Button, PropertyOrder(-0.5f)]
        private void MidAction()
        {
        }
    }

    /// <summary>一个都不标的对照。</summary>
    [HideMonoScript]
    internal sealed class PlainOrderFixture : ScriptableObject
    {
        /// <summary>声明在最先。</summary>
        public int a;

        /// <summary>声明居中。</summary>
        public int b;

        /// <summary>声明在最后。</summary>
        public int c;
    }

    /// <summary>分组锚点的对照：未分组字段被排到最前。</summary>
    [HideMonoScript]
    internal sealed class OrderGroupFixture : ScriptableObject
    {
        /// <summary>分组的首个成员（声明在前）。</summary>
        [BoxGroup("甲")]
        public int groupedA;

        /// <summary>排到最前——分组因此后移。</summary>
        [PropertyOrder(-1f)]
        public int ungrouped;

        /// <summary>同组的第二个成员。</summary>
        [BoxGroup("甲")]
        public int groupedB;
    }
}
