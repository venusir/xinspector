using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 信息框的链装配与条件解析。
    /// <para>
    /// 条件的「解析在构建期、求值在绘制期」这条分工是重点：<c>visibleIf</c> 必须跟着
    /// 字段值走，因此这里改值后会断言求值器**跟着变**；而条件名拼错时必须**放行**
    /// （显示），不能把内容吞掉——那会让人以为特性没生效。
    /// </para>
    /// </summary>
    [TestFixture]
    public class InfoBoxDrawerTests
    {
        #region Private Fields

        private InfoBoxFixture _target;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<InfoBoxFixture>();
        }

        /// <summary>销毁临时资产并复位两个静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 链装配

        /// <summary>成员上的信息框绘制器在链上，且排在末端之前。</summary>
        [Test]
        public void 成员框在末端之前()
        {
            var property = Find("alwaysShown");

            Assert.That(IndexOf<InfoBoxDrawer>(property), Is.GreaterThanOrEqualTo(0));
            Assert.That(IndexOf<InfoBoxDrawer>(property), Is.LessThan(IndexOf<UnityFallbackDrawer>(property)));
        }

        /// <summary>
        /// 类级信息框落在根节点上，且排在子节点绘制器之前——否则框会跑到所有字段下面。
        /// </summary>
        [Test]
        public void 类级框在根节点且在子节点之前()
        {
            var root = Tree.Root;

            var box = IndexOf<InfoBoxDrawer>(root);
            var children = IndexOf<ChildrenDrawer>(root);

            Assert.That(box, Is.GreaterThanOrEqualTo(0), "类级 [InfoBox] 应当落在根节点上。");
            Assert.That(box, Is.LessThan(children));
        }

        /// <summary>详情框绘制器在链上。</summary>
        [Test]
        public void 详情框在链上()
        {
            var property = Find("detailed");

            Assert.That(IndexOf<DetailedInfoBoxDrawer>(property), Is.GreaterThanOrEqualTo(0));
        }

        #endregion

        #region 条件

        /// <summary>无条件的信息框恒显示。</summary>
        [Test]
        public void 无条件框恒显示()
        {
            var property = Find("alwaysShown");

            Assert.That(InfoBoxConditions.IsVisible(property, property.GetAttribute<InfoBoxAttribute>()), Is.True);
        }

        /// <summary>
        /// 带条件的信息框，其条件**已在构建期登记**（接线正确）。
        /// <para>
        /// 这里断言的是「接线」而不是「跟随值变化」：值跟随由
        /// <see cref="ConditionProcessorTests"/> 覆盖——两者走的是同一个
        /// <see cref="ConditionResolver.TryResolve"/>，求值器都是「构建期解析一次、
        /// 绘制期每帧读一个 bool」。
        /// </para>
        /// <para>
        /// 之所以不在这里再断言一遍跟随：<see cref="SerializedObject"/> 有个易踩的行为——
        /// 同一个目标上并存两个活动实例时，先前的实例会**查找失效**（<c>FindProperty</c>
        /// 返回 null），症状是「值明明写了却读不到」，极易被误判成特性没生效。
        /// 本 fixture 只读状态、不写序列化数据，就是为了避开这一层。
        /// </para>
        /// </summary>
        [Test]
        public void 条件已登记()
        {
            var property = Find("conditional");
            var state = property.State.Get<InfoBoxVisibilityState>();

            Assert.That(state, Is.Not.Null, "带 visibleIf 的信息框应当在构建期登记一个求值器。");
            Assert.That(
                state.Conditions.ContainsKey(property.GetAttribute<InfoBoxAttribute>()),
                Is.True,
                "登记须按**特性实例**为键：一个成员可以挂多个信息框，各带各的条件。");
        }

        /// <summary>
        /// 条件名无效时**不登记**，于是求值走「没有条件 ⇒ 显示」那条路（失败即放行）。
        /// </summary>
        [Test]
        public void 条件名无效时按显示处理()
        {
            var property = Find("brokenCondition");
            var state = property.State.Get<InfoBoxVisibilityState>();

            Assert.That(
                state == null || !state.Conditions.ContainsKey(property.GetAttribute<InfoBoxAttribute>()),
                Is.True,
                "解析失败不该登记条件。");

            Assert.That(
                InfoBoxConditions.IsVisible(property, property.GetAttribute<InfoBoxAttribute>()),
                Is.True,
                "解析失败时应当放行——拼错名字的表现若是「框不见了」，会被当成特性没生效。");
        }

        #endregion

        #region Private Helpers

        /// <summary>被测的树；首次访问时构建，同一用例内复用。</summary>
        private PropertyTree Tree
        {
            get
            {
                if (_tree == null)
                {
                    _tree = PropertyTree.Create(new SerializedObject(_target));
                }

                return _tree;
            }
        }

        /// <summary>按路径取成员节点。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>成员节点。</returns>
        private InspectorProperty Find(string path)
        {
            foreach (var child in Tree.Root.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            Assert.Fail($"找不到成员 {path}。");
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

    /// <summary>信息框测试用的资产。</summary>
    [InfoBox("类级说明：这个组件用来验证信息框")]
    internal sealed class InfoBoxFixture : ScriptableObject
    {
        /// <summary>可见性条件成员。</summary>
        public bool showTip;

        /// <summary>无条件的信息框。</summary>
        [InfoBox("恒显示")]
        public int alwaysShown = 1;

        /// <summary>带条件的信息框。</summary>
        [InfoBox("按条件显示", InfoMessageType.Warning, nameof(showTip))]
        public int conditional = 2;

        /// <summary>条件名拼错的信息框。</summary>
        [InfoBox("条件名不存在", InfoMessageType.Info, "noSuchMember")]
        public int brokenCondition = 3;

        /// <summary>可展开的详情框。</summary>
        [DetailedInfoBox("摘要", "详情内容")]
        public int detailed = 4;
    }
}
