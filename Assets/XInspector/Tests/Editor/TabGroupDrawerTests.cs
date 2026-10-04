using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 页签：节点结构、绘制器落点与档位。
    /// <para>
    /// <b>测不了的</b>：页签栏外观、点击切换、以及「只画选中页」（绘制期由末端消费策略）。
    /// 容器/页的判定与路径合成在 Runtime 侧逐条钉住（<c>TabGroupAttributeTests</c>）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class TabGroupDrawerTests
    {
        #region Private Fields

        private TabFixture _target;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<TabFixture>();
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

            _tree = null;
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 结构

        /// <summary>容器 → 页 → 成员三层结构，且页的顺序等于声明顺序。</summary>
        [Test]
        public void 三层结构与页顺序()
        {
            var container = Find("设置");

            Assert.That(container.Kind, Is.EqualTo(InspectorPropertyKind.Group));
            Assert.That(container.Attributes.Get<TabGroupAttribute>().IsContainer, Is.True);

            Assert.That(ChildOf(container, "设置/基础"), Is.Not.Null);
            Assert.That(ChildOf(container, "设置/高级"), Is.Not.Null);
            Assert.That(ChildOf(container, "设置/基础").Children[0].Path, Is.EqualTo("health"),
                "成员落在页节点下，路径不带分组前缀。");
        }

        /// <summary>容器与页**都**带绘制器——页那一格走「只把内容传下去」的分支。</summary>
        [Test]
        public void 容器与页都有绘制器()
        {
            var container = Find("设置");
            var page = ChildOf(container, "设置/基础");

            Assert.That(IndexOf<TabGroupDrawer>(container), Is.GreaterThanOrEqualTo(0));
            Assert.That(page, Is.Not.Null);
            Assert.That(IndexOf<TabGroupDrawer>(page), Is.GreaterThanOrEqualTo(0));
        }

        /// <summary>页签（-170）在框（-130）之外：否则框会跟着每一页各画一次。</summary>
        [Test]
        public void 页签在框之外()
        {
            var node = Find("页签与框");

            Assert.That(IndexOf<TabGroupDrawer>(node), Is.LessThan(IndexOf<BoxGroupDrawer>(node)));
        }

        /// <summary>成员节点上没有页签绘制器（分组绘制器只落在分组节点上）。</summary>
        [Test]
        public void 成员节点上没有绘制器()
        {
            var member = ChildOf(ChildOf(Find("设置"), "设置/基础"), "health");

            Assert.That(member, Is.Not.Null);
            Assert.That(IndexOf<TabGroupDrawer>(member), Is.EqualTo(-1));
        }

        #endregion

        #region 状态默认值

        /// <summary>新建的页签状态默认选中第一页、尚未缓存页名。</summary>
        [Test]
        public void 状态默认值()
        {
            var state = new TabGroupState();

            Assert.That(state.SelectedIndex, Is.EqualTo(0));
            Assert.That(state.TabNames, Is.Null, "页名在首次绘制时才缓存。");
        }

        #endregion

        #region Private Helpers

        /// <summary>取树根；树在首次调用时构建，同一用例内复用。</summary>
        /// <returns>根节点。</returns>
        private InspectorProperty TreeRoot()
        {
            if (_tree == null)
            {
                _tree = PropertyTree.Create(new SerializedObject(_target));
            }

            return _tree.Root;
        }

        /// <summary>按路径递归查找节点。</summary>
        /// <param name="path">完整路径。</param>
        /// <returns>节点。</returns>
        private InspectorProperty Find(string path)
        {
            var found = Search(TreeRoot(), path);

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

        #endregion
    }

    /// <summary>页签测试用的资产。</summary>
    internal sealed class TabFixture : ScriptableObject
    {
        /// <summary>第一页的成员。</summary>
        [TabGroup("设置", "基础")]
        public int health = 100;

        /// <summary>第一页的第二个成员。</summary>
        [TabGroup("设置", "基础")]
        public int mana = 50;

        /// <summary>第二页的成员。</summary>
        [TabGroup("设置", "高级")]
        public int debugLevel;

        /// <summary>页签 + 框同路径：验证档位。</summary>
        [TabGroup("页签与框", "一")]
        [BoxGroup("页签与框")]
        public int framed;
    }
}
