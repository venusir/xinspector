using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 状态三特性的处理器行为：只读、标签文本、悬停提示。
    /// <para>
    /// 三条都断言**状态**而不是像素——这正是把它们做成处理器的意义：
    /// 「这个属性处于什么状态」完全不碰 GUI，可以无头验证。
    /// </para>
    /// </summary>
    [TestFixture]
    public class StateProcessorTests
    {
        #region Private Fields

        private StateFixture _target;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<StateFixture>();
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

        #region [ReadOnly]

        /// <summary>带 <c>[ReadOnly]</c> 的成员状态为只读。</summary>
        [Test]
        public void ReadOnly_状态为只读()
        {
            Assert.That(Find("lockedValue").State.IsReadOnly, Is.True);
        }

        /// <summary>没有该特性的成员照常可编辑。</summary>
        [Test]
        public void ReadOnly_无特性的成员可编辑()
        {
            Assert.That(Find("plain").State.IsReadOnly, Is.False);
        }

        /// <summary>
        /// 与条件族并存时，**恒只读赢**。
        /// <para>
        /// <c>[DisableIf]</c> 的条件为假、本该可编辑，但 <c>[ReadOnly]</c> 说的是恒只读。
        /// 这条靠处理器的显式优先级成立（100 &gt; 0），不是靠类型名排序的巧合。
        /// </para>
        /// </summary>
        [Test]
        public void ReadOnly_与条件族并存时恒只读赢()
        {
            var property = Find("lockedValueWithCondition");

            Assert.That(_target.flag, Is.False, "前提：条件为假，[DisableIf] 本该放行。");
            Assert.That(property.State.IsReadOnly, Is.True, "[ReadOnly] 是恒只读，不该被条件覆盖。");
        }

        #endregion

        #region [LabelText] / [PropertyTooltip]

        /// <summary>标签文本被替换成指定值。</summary>
        [Test]
        public void LabelText_覆盖标签文本()
        {
            Assert.That(Find("health").Label.text, Is.EqualTo("玩家生命"));
        }

        /// <summary>打开可读化时，驼峰名被拆成词。</summary>
        [Test]
        public void LabelText_可读化拆分驼峰()
        {
            Assert.That(Find("playerScore").Label.text, Is.EqualTo("Player Score"));
        }

        /// <summary>提示被挂到标签上。</summary>
        [Test]
        public void Tooltip_挂到标签上()
        {
            Assert.That(Find("regen").Label.tooltip, Is.EqualTo("每秒恢复"));
        }

        /// <summary>
        /// 两个特性并存时文本与提示都在——这条守的是处理器顺序：
        /// 提示处理器若跑在标签处理器之前（或读了字段名而非标签），文本就会被打回去。
        /// </summary>
        [Test]
        public void LabelText与Tooltip_并存时互不覆盖()
        {
            var label = Find("mana").Label;

            Assert.That(label.text, Is.EqualTo("法力"));
            Assert.That(label.tooltip, Is.EqualTo("施法消耗"));
        }

        /// <summary>无标签特性的成员仍用字段名。</summary>
        [Test]
        public void LabelText_无特性成员用字段名()
        {
            Assert.That(Find("plain").Label.text, Is.EqualTo("plain"));
        }

        #endregion

        #region 不参与绘制

        /// <summary>
        /// 处理器**不产生链格子**——它们改的是状态，不是画法。
        /// <para>
        /// 这条是刻意的架构约束：一旦处理器能画东西，「谁包住谁」就不再只由
        /// <see cref="DrawerPriority"/> 决定，绘制器链的顺序语义会被绕过去。
        /// </para>
        /// </summary>
        [Test]
        public void 处理器特性不产生链格子()
        {
            foreach (var path in new[] { "lockedValue", "health", "regen", "mana" })
            {
                var property = Find(path);

                Assert.That(
                    property.Chain.Count,
                    Is.EqualTo(1),
                    $"字段 {path} 的链上出现了处理器带来的格子——处理器不得参与绘制。");
                Assert.That(property.Chain.Entries[0].Drawer, Is.TypeOf<UnityFallbackDrawer>());
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>按路径取成员节点；树在首次调用时构建，同一用例内复用。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>成员节点。</returns>
        private InspectorProperty Find(string path)
        {
            if (_tree == null)
            {
                _tree = PropertyTree.Create(new SerializedObject(_target));
            }

            foreach (var child in _tree.Root.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            Assert.Fail($"找不到成员 {path}。");
            return null;
        }

        #endregion
    }

    /// <summary>状态三特性测试用的资产。</summary>
    internal sealed class StateFixture : ScriptableObject
    {
        /// <summary>条件成员，保持 false 以便验证「恒只读赢」。</summary>
        public bool flag;

        /// <summary>恒只读。</summary>
        [ReadOnly]
        public int lockedValue = 1;

        /// <summary>恒只读 + 一个为假的条件。</summary>
        [ReadOnly]
        [DisableIf(nameof(flag))]
        public int lockedValueWithCondition = 2;

        /// <summary>替换标签文本。</summary>
        [LabelText("玩家生命")]
        public int health = 100;

        /// <summary>替换标签文本并做可读化。</summary>
        [LabelText("playerScore", true)]
        public int playerScore = 20;

        /// <summary>挂提示。</summary>
        [PropertyTooltip("每秒恢复")]
        public float regen = 1f;

        /// <summary>文本与提示并存。</summary>
        [LabelText("法力")]
        [PropertyTooltip("施法消耗")]
        public float mana = 10f;

        /// <summary>无任何特性的对照。</summary>
        public int plain;
    }
}
