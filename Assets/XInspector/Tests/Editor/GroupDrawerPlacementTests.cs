using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 分组绘制器只落在**分组节点**上。
    /// <para>
    /// 成员与根携带分组特性只是为了「归属」——分组装配会读它决定成员搬到哪个节点，
    /// 而特性本身留在原地（成员不被删特性）。但链装配是「逐特性实例 × 逐绘制器」配对，
    /// 不看节点种类，于是**不给过滤的话**：每个分组内的成员会各自再画一个框
    /// （双重框 + 重复标题），类级分组也会继续框住整个 Inspector。
    /// </para>
    /// </summary>
    [TestFixture]
    public class GroupDrawerPlacementTests
    {
        #region Setup / Teardown

        /// <summary>复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 绘制器落点

        /// <summary>分组内的成员自己的链上**没有**分组绘制器。</summary>
        [Test]
        public void 成员节点不画分组框()
        {
            var target = ScriptableObject.CreateInstance<PlacementFixture>();
            try
            {
                var tree = BuildTree(target);
                var member = Find(tree.Root, "Outer").Children[0];

                Assert.That(member.Path, Is.EqualTo("a"), "前提：成员节点保留自己的路径。");
                Assert.That(IndexOf<BoxGroupDrawer>(member), Is.EqualTo(-1),
                    "框只能由分组节点画一次；成员也画一遍就是双重框。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 根节点也不画分组框——类级分组的**归属**由分发解决，框该由分组节点画。
        /// <para>
        /// 这条同时修正一句写在 CHANGELOG 里的话：此前宣称「类级 [BoxGroup] 会框住整个 Inspector」
        /// 已经修掉，但分发只修正了归属，根节点上那一格绘制器还在。
        /// </para>
        /// </summary>
        [Test]
        public void 根节点不画分组框()
        {
            var target = ScriptableObject.CreateInstance<ClassGroupPlacementFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(IndexOf<BoxGroupDrawer>(tree.Root), Is.EqualTo(-1),
                    "类级分组不该继续框住整页。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>分组节点照旧画框——这是本条改动的守卫侧，不能被过滤误伤。</summary>
        [Test]
        public void 分组节点仍然画分组框()
        {
            var target = ScriptableObject.CreateInstance<PlacementFixture>();
            try
            {
                var tree = BuildTree(target);
                var group = Find(tree.Root, "Outer");

                Assert.That(IndexOf<BoxGroupDrawer>(group), Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>未分组成员的链上只有末端（回归守卫）。</summary>
        [Test]
        public void 未分组成员只有末端()
        {
            var target = ScriptableObject.CreateInstance<PlacementFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(Find(tree.Root, "plain").Chain.Count, Is.EqualTo(1));
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

        /// <summary>按路径查找直接子节点。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；不存在返回 <c>null</c>。</returns>
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

        #endregion
    }

    /// <summary>成员级分组的对照资产。</summary>
    internal sealed class PlacementFixture : ScriptableObject
    {
        /// <summary>分组内的成员。</summary>
        [BoxGroup("Outer")]
        public int a;

        /// <summary>未分组的对照。</summary>
        public int plain;
    }

    /// <summary>类级分组的对照资产。</summary>
    [BoxGroup("类级")]
    internal sealed class ClassGroupPlacementFixture : ScriptableObject
    {
        /// <summary>被类级分组收进去的成员。</summary>
        public int x;
    }
}
