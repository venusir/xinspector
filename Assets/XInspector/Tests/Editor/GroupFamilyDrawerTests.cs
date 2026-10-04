using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 分组族三个呈现型：链装配、优先级档位、祖先合成带子类字段。
    /// <para>
    /// <b>测不了的</b>：标题外观、分隔线、折叠三角与「收起时不画内容」的效果
    /// （那是绘制期分支，且按本仓策略不测 IMGUI）。折叠状态在首次绘制时才初始化，
    /// 因此由绘制期的 <c>PropertyState</c> 承担的初值逻辑没有自动化断言——
    /// 目视在 <c>Samples/AttributeShowcase</c>。
    /// </para>
    /// </summary>
    [TestFixture]
    public class GroupFamilyDrawerTests
    {
        #region Private Fields

        private GroupFamilyFixture _target;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<GroupFamilyFixture>();
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

        /// <summary>三个绘制器各落在自己的分组节点上。</summary>
        [Test]
        public void 三个绘制器都在链上()
        {
            Assert.That(IndexOf<VerticalGroupDrawer>(Find("竖列")), Is.GreaterThanOrEqualTo(0));
            Assert.That(IndexOf<TitleGroupDrawer>(Find("标题组")), Is.GreaterThanOrEqualTo(0));
            Assert.That(IndexOf<FoldoutGroupDrawer>(Find("折叠组")), Is.GreaterThanOrEqualTo(0));
        }

        /// <summary>
        /// 同路径多种分组特性各占一格，且按档位排序：折叠（-190）在框（-130）之外、
        /// 标题（-150）在框之外。
        /// <para>
        /// 档位不是审美而是功能：折叠要把内侧一切的框与标题**一起**收掉，
        /// 排在框内侧就会出现「收起了内容、留下一个空框」。
        /// </para>
        /// </summary>
        [Test]
        public void 档位由外到内()
        {
            var foldoutOnBox = Find("折叠与框");
            Assert.That(IndexOf<FoldoutGroupDrawer>(foldoutOnBox), Is.LessThan(IndexOf<BoxGroupDrawer>(foldoutOnBox)),
                "折叠必须包住框。");

            var titleOnBox = Find("标题与框");
            Assert.That(IndexOf<TitleGroupDrawer>(titleOnBox), Is.LessThan(IndexOf<BoxGroupDrawer>(titleOnBox)),
                "标题在框之外。");

            var verticalInBox = Find("框与竖列");
            Assert.That(IndexOf<BoxGroupDrawer>(verticalInBox), Is.LessThan(IndexOf<VerticalGroupDrawer>(verticalInBox)),
                "竖直容器在框之内。");
        }

        #endregion

        #region 祖先合成

        /// <summary>
        /// 合成的祖先节点带上子类字段（<c>CloneForPath</c> 走 MemberwiseClone 的既有语义）。
        /// </summary>
        [Test]
        public void 祖先继承子类字段()
        {
            var ancestor = Find("深");

            var foldout = ancestor.Attributes.Get<FoldoutGroupAttribute>();
            Assert.That(foldout, Is.Not.Null, "深 是从 深/折叠 合成出来的。");
            Assert.That(foldout.Expanded, Is.True, "合成时子类字段（Expanded）一并带上来。");
            Assert.That(foldout.GroupName, Is.EqualTo("深"), "路径被改写为祖先自己的那一段。");
        }

        /// <summary>标题组的副标题随祖先合成一起带上来。</summary>
        [Test]
        public void 标题组的呈现设定随合成带上()
        {
            var node = Find("标题组");
            var attribute = node.Attributes.Get<TitleGroupAttribute>();

            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.Subtitle, Is.EqualTo("副标题"));
            Assert.That(attribute.GroupID, Is.EqualTo("标题组"));
        }

        /// <summary>不带任何分组特性的成员仍是根的直接子节点（回归守卫）。</summary>
        [Test]
        public void 未分组成员留在根下()
        {
            Assert.That(Find("plain").Parent, Is.SameAs(TreeRoot()));
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

    /// <summary>分组族测试用的资产。</summary>
    internal sealed class GroupFamilyFixture : ScriptableObject
    {
        /// <summary>竖直分组。</summary>
        [VerticalGroup("竖列")]
        public int vertical;

        /// <summary>标题组（带副标题）。</summary>
        [TitleGroup("标题组", "副标题")]
        public int titled;

        /// <summary>折叠组（初始展开）。</summary>
        [FoldoutGroup("折叠组", true)]
        public int folded;

        /// <summary>深层折叠组：外层节点由合成产生。</summary>
        [FoldoutGroup("深/折叠", true)]
        public int deepFoldout;

        /// <summary>折叠 + 框同路径：验证两格并存与档位。</summary>
        [FoldoutGroup("折叠与框")]
        [BoxGroup("折叠与框")]
        public int foldoutOnBox;

        /// <summary>标题 + 框同路径。</summary>
        [TitleGroup("标题与框")]
        [BoxGroup("标题与框")]
        public int titleOnBox;

        /// <summary>框 + 竖直容器同路径。</summary>
        [BoxGroup("框与竖列")]
        [VerticalGroup("框与竖列")]
        public int verticalInBox;

        /// <summary>未分组的对照。</summary>
        public int plain;
    }
}
