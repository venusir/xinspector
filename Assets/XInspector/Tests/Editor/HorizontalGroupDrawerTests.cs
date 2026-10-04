using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 水平分组：链装配与档位，以及行状态容器的复用行为。
    /// <para>
    /// <b>测不了的</b>：真实列宽与排布（要读当前布局组的宽度，只在绘制期成立）、
    /// 「策略由分组绘制器装、末端读」这条时序本身。分数换算的规则在 Runtime 侧
    /// 逐条钉住（<c>HorizontalGroupWeightsTests</c>），那才是容易错的部分。
    /// </para>
    /// </summary>
    [TestFixture]
    public class HorizontalGroupDrawerTests
    {
        #region Private Fields

        private HorizontalFixture _target;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<HorizontalFixture>();
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

        #region 链装配与档位

        /// <summary>绘制器落在分组节点上，且成员节点上**没有**它（分组绘制器只在分组节点上）。</summary>
        [Test]
        public void 绘制器只落在分组节点上()
        {
            var group = Find("一行");

            // 成员节点的路径**不带分组前缀**（分组只改归属、不改路径）——按名字找直接子节点。
            var member = ChildOf(group, "wide");

            Assert.That(IndexOf<HorizontalGroupDrawer>(group), Is.GreaterThanOrEqualTo(0));
            Assert.That(member, Is.Not.Null);
            Assert.That(IndexOf<HorizontalGroupDrawer>(member), Is.EqualTo(-1),
                "成员携带特性只是为了「分数」——画由分组节点负责。");
        }

        /// <summary>档位：水平（-100）在框（-130）之内——行作用域要直接包住子节点列表。</summary>
        [Test]
        public void 水平在最内()
        {
            var node = Find("行与框");

            Assert.That(IndexOf<BoxGroupDrawer>(node), Is.LessThan(IndexOf<HorizontalGroupDrawer>(node)),
                "框在外、行在内。");
        }

        /// <summary>深路径同样合成祖先节点。</summary>
        [Test]
        public void 深路径合成祖先()
        {
            Assert.That(Find("深/行"), Is.Not.Null);
            Assert.That(Find("深"), Is.Not.Null);
        }

        #endregion

        #region 行状态容器

        /// <summary>子节点数不变时**就地复用**数组；变了才重新分配（绘制路径禁分配）。</summary>
        [Test]
        public void 行状态数组就地复用()
        {
            var layout = new GroupChildrenLayout();

            layout.EnsureCapacity(3);
            var widths = layout.CellWidths;

            layout.EnsureCapacity(3);
            Assert.That(layout.CellWidths, Is.SameAs(widths), "同长度应当复用。");

            layout.EnsureCapacity(2);
            Assert.That(layout.CellWidths, Is.Not.SameAs(widths), "长度变了才重建。");
            Assert.That(layout.CellWidths.Length, Is.EqualTo(2));
            Assert.That(layout.ScratchFractions.Length, Is.EqualTo(2), "暂存数组一并跟上。");
        }

        /// <summary>默认策略：不筛子节点、无策略标记——末端因此走原路径。</summary>
        [Test]
        public void 默认策略不生效()
        {
            var layout = new GroupChildrenLayout();

            Assert.That(layout.HasPolicy, Is.False);
            Assert.That(layout.OnlyChildIndex, Is.EqualTo(GroupChildrenLayout.AllChildren));
            Assert.That(layout.CellGap, Is.EqualTo(0f));
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

    /// <summary>水平分组测试用的资产。</summary>
    internal sealed class HorizontalFixture : ScriptableObject
    {
        /// <summary>一行里的第一格。</summary>
        [HorizontalGroup("一行", 0.7f)]
        public int wide = 1;

        /// <summary>一行里的第二格。</summary>
        [HorizontalGroup("一行", 0.3f)]
        public int narrow = 2;

        /// <summary>框与行同路径：验证档位。</summary>
        [BoxGroup("行与框")]
        [HorizontalGroup("行与框")]
        public int inside = 3;

        /// <summary>深层路径：验证祖先合成。</summary>
        [HorizontalGroup("深/行", 0.5f)]
        public int deep = 4;
    }
}
