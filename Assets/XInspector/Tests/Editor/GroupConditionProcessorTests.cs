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
    /// <c>[ShowIfGroup]</c> / <c>[HideIfGroup]</c>：条件落在**分组节点**上，
    /// 祖先与兄弟分组不受牵连，成员自己不装求值器。
    /// </summary>
    [TestFixture]
    public class GroupConditionProcessorTests
    {
        #region Fixture

        [SetUp]
        public void SetUp()
        {
            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region 条件生效

        /// <summary>组节点跟随条件值：条件为假整组消失，为真恢复。</summary>
        [Test]
        public void 组节点跟随条件值()
        {
            var target = ScriptableObject.CreateInstance<GroupConditionFixture>();
            try
            {
                var tree = BuildTree(target);
                var group = Find(tree.Root, "Box/toggle");

                Assert.That(group.IsVisible, Is.True, "条件为真，先确认起点可见。");

                SetBool(target, tree, "toggle", false);
                Assert.That(group.IsVisible, Is.False, "条件为假时整组消失。");

                SetBool(target, tree, "toggle", true);
                Assert.That(group.IsVisible, Is.True, "条件每帧求值，改回来就恢复。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>祖先与兄弟分组不受牵连——条件只挂在声明的那一节上。</summary>
        [Test]
        public void 祖先与兄弟分组不受条件牵连()
        {
            var target = ScriptableObject.CreateInstance<GroupConditionFixture>();
            try
            {
                var tree = BuildTree(target);
                SetBool(target, tree, "toggle", false);

                Assert.That(Find(tree.Root, "Box").IsVisible, Is.True,
                    "Box 是合成出来的祖先，只贡献路径、不做判据。");
                Assert.That(Find(tree.Root, "Box/Other").IsVisible, Is.True,
                    "同祖先下的兄弟分组不该被 toggle 牵着走。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>隐藏靠分组节点短路，成员自己不装求值器——这是判据挂点的直接证据。</summary>
        [Test]
        public void 成员自身不受条件影响()
        {
            var target = ScriptableObject.CreateInstance<GroupConditionFixture>();
            try
            {
                var tree = BuildTree(target);
                SetBool(target, tree, "toggle", false);

                Assert.That(Find(tree.Root, "Box/toggle/Shown").IsVisible, Is.True,
                    "成员自己的状态没变；整组消失是分组节点的可见性在短路。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary><c>[HideIfGroup]</c> 是同一套机制取反。</summary>
        [Test]
        public void HideIfGroup取反()
        {
            var target = ScriptableObject.CreateInstance<GroupConditionFixture>();
            try
            {
                var tree = BuildTree(target);
                var group = Find(tree.Root, "隐藏组");

                Assert.That(group.IsVisible, Is.True, "hide 为假，组可见。");

                SetBool(target, tree, "hide", true);
                Assert.That(group.IsVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>组名与条件名不同时，显式 <c>Condition</c> 覆盖末段默认。</summary>
        [Test]
        public void 显式条件覆盖末段默认()
        {
            var target = ScriptableObject.CreateInstance<GroupConditionFixture>();
            try
            {
                var tree = BuildTree(target);
                var group = Find(tree.Root, "战斗组");

                SetBool(target, tree, "toggle", false);
                Assert.That(group.IsVisible, Is.False, "「战斗组」判的是 toggle，不是「战斗组」这个成员名。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 与其它分组特性共存

        /// <summary>同路径配一个 <c>[BoxGroup]</c>：两份特性并存，框照画、条件照判。</summary>
        [Test]
        public void 与BoxGroup同路径时并存且条件生效()
        {
            var target = ScriptableObject.CreateInstance<BoxPairedFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "开关");

                Assert.That(node.Attributes.Has<ShowIfGroupAttribute>(), Is.True);
                Assert.That(node.Attributes.Has<BoxGroupAttribute>(), Is.True, "不同类型同路径并存。");
                Assert.That(IndexOf<BoxGroupDrawer>(node), Is.GreaterThanOrEqualTo(0), "视觉的框来自 BoxGroup。");

                SetBool(target, tree, "toggle", false);
                Assert.That(node.IsVisible, Is.False, "条件仍挂在同一个节点上。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>纯条件载体：不带其它分组特性时链上只有末端，不画任何东西。</summary>
        [Test]
        public void 单独用时链上只有末端()
        {
            var target = ScriptableObject.CreateInstance<GroupConditionFixture>();
            try
            {
                var tree = BuildTree(target);
                var group = Find(tree.Root, "战斗组");

                Assert.That(group.Chain.Count, Is.EqualTo(1), "只做条件载体，不画任何东西。");
                Assert.That(IndexOf<BoxGroupDrawer>(group), Is.EqualTo(-1));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 类级声明

        /// <summary>类级声明分发给成员后，整组照常可用（成员都不带自有分组）。</summary>
        [Test]
        public void 类级声明整组可用()
        {
            var target = ScriptableObject.CreateInstance<ClassLevelSimpleFixture>();
            try
            {
                var tree = BuildTree(target);
                var group = Find(tree.Root, "条件组");

                SetBool(target, tree, "toggle", false);
                Assert.That(group.IsVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 类级声明 + 成员**全部**自带分组：类级那份到不了任何节点，
        /// 靠「根上回退」仍要生效。
        /// </summary>
        [Test]
        public void 类级声明经根上回退生效()
        {
            var target = ScriptableObject.CreateInstance<ClassLevelOwnGroupFixture>();
            try
            {
                var tree = BuildTree(target);
                var group = Find(tree.Root, "条件组");

                Assert.That(group.Attributes.Has<ShowIfGroupAttribute>(), Is.False,
                    "前提：自有分组那条分支只改写路径前缀，类级特性本身到不了节点。");

                SetBool(target, tree, "toggle", false);
                Assert.That(group.IsVisible, Is.False, "条件作为数据没丢——从根上取到的那份生效了。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 解析失败

        /// <summary>条件名不存在：整组保持可见并记一条告警（拼错的名字不该让一整组字段消失）。</summary>
        [Test]
        public void 条件名不存在时保持可见并告警()
        {
            var target = ScriptableObject.CreateInstance<BrokenGroupConditionFixture>();
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("无法求值"));

                var tree = BuildTree(target);

                Assert.That(Find(tree.Root, "没这个成员").IsVisible, Is.True);
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
        /// <param name="name">字段名。</param>
        /// <param name="value">新值。</param>
        private static void SetBool(ScriptableObject target, PropertyTree tree, string name, bool value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(name).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        /// <summary>按完整路径查找节点（递归整棵子树——分组有嵌套，不能只看直接子节点）。</summary>
        /// <param name="parent">起始节点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；不存在时断言失败。</returns>
        private static InspectorProperty Find(InspectorProperty parent, string path)
        {
            if (TryFind(parent, path, out var found))
            {
                return found;
            }

            Assert.Fail($"找不到节点 {path}。");
            return null;
        }

        /// <summary>递归查找的先序遍历。</summary>
        /// <param name="parent">起始节点。</param>
        /// <param name="path">完整路径。</param>
        /// <param name="found">找到的节点。</param>
        /// <returns>找到返回 <c>true</c>。</returns>
        private static bool TryFind(InspectorProperty parent, string path, out InspectorProperty found)
        {
            var children = parent.Children;

            for (var i = 0; i < children.Count; i++)
            {
                if (children[i].Path == path)
                {
                    found = children[i];
                    return true;
                }
            }

            for (var i = 0; i < children.Count; i++)
            {
                if (TryFind(children[i], path, out found))
                {
                    return true;
                }
            }

            found = null;
            return false;
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

    /// <summary>成员级分组条件：深层路径、兄弟分组、取反、显式覆盖各一。</summary>
    internal sealed class GroupConditionFixture : ScriptableObject
    {
        /// <summary>条件开关。</summary>
        public bool toggle = true;

        /// <summary>取反组的开关。</summary>
        public bool hide;

        /// <summary>条件组内层再套一个框——组路径是它的祖先。</summary>
        [ShowIfGroup("Box/toggle")]
        [BoxGroup("Box/toggle/Shown")]
        public int shown;

        /// <summary>同祖先下的兄弟分组，不该被 toggle 牵着走。</summary>
        [BoxGroup("Box/Other")]
        public int other;

        /// <summary>取反：hide 为真时整组隐藏。组名与条件名不同，显式覆盖。</summary>
        [HideIfGroup("隐藏组", Condition = nameof(hide))]
        public int hidden;

        /// <summary>组名与条件名不同：显式覆盖。</summary>
        [ShowIfGroup("战斗组", Condition = nameof(toggle))]
        public int renamed;
    }

    /// <summary>与 <c>[BoxGroup]</c> 同路径配对——官方样例的配法。</summary>
    internal sealed class BoxPairedFixture : ScriptableObject
    {
        /// <summary>条件开关。</summary>
        public bool toggle = true;

        /// <summary>同路径的框。</summary>
        [ShowIfGroup("开关", Condition = nameof(toggle))]
        [BoxGroup("开关")]
        public int value;
    }

    /// <summary>类级分组条件：成员都不带自有分组。</summary>
    [ShowIfGroup("条件组", Condition = nameof(ClassLevelSimpleFixture.toggle))]
    internal sealed class ClassLevelSimpleFixture : ScriptableObject
    {
        /// <summary>条件开关。</summary>
        public bool toggle = true;

        /// <summary>被类级分组收进去的成员。</summary>
        public int value;
    }

    /// <summary>类级分组条件：成员**全部**自带分组，逼出「根上回退」那条路。</summary>
    [ShowIfGroup("条件组", Condition = nameof(ClassLevelOwnGroupFixture.toggle))]
    internal sealed class ClassLevelOwnGroupFixture : ScriptableObject
    {
        /// <summary>条件开关（自带分组，于是类级那份到不了节点）。</summary>
        [BoxGroup("开关")]
        public bool toggle = true;

        /// <summary>同样自带分组的另一个成员。</summary>
        [BoxGroup("详情")]
        public int details;
    }

    /// <summary>条件名解析失败的对照资产。</summary>
    internal sealed class BrokenGroupConditionFixture : ScriptableObject
    {
        /// <summary>条件名指向一个不存在的成员。</summary>
        [ShowIfGroup("没这个成员")]
        public int value;
    }
}
