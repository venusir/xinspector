using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <see cref="ReadOnlyAttribute"/>、<see cref="LabelTextAttribute"/>、
    /// <see cref="PropertyTooltipAttribute"/> 的构造行为与用法约束。
    /// <para>
    /// 这些断言不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class StateAttributeTests
    {
        #region [ReadOnly]

        /// <summary>
        /// 只读特性只用于成员，且不可重复。
        /// <para>
        /// 不允许用在类上是**刻意收窄**（Odin 声明的是 <c>All</c>）：类级「只读」没有定义语义，
        /// 放宽只会得到「编译得过但什么都不发生」，那正是本包最想避免的一类现象。
        /// </para>
        /// </summary>
        [Test]
        public void Usage_只读特性仅用于成员且不可重复()
        {
            var usage = typeof(ReadOnlyAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null, "ReadOnlyAttribute 必须声明 AttributeUsage。");
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Property), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }

        #endregion

        #region [LabelText]

        /// <summary>标签文本被原样保留，不做可读化。</summary>
        [Test]
        public void LabelText_保留文本且默认不做可读化()
        {
            var attribute = new LabelTextAttribute("玩家生命");

            Assert.That(attribute.Text, Is.EqualTo("玩家生命"));
            Assert.That(attribute.NicifyText, Is.False);
        }

        /// <summary>两参构造打开可读化。</summary>
        [Test]
        public void LabelText_两参构造打开可读化()
        {
            var attribute = new LabelTextAttribute("playerScore", true);

            Assert.That(attribute.Text, Is.EqualTo("playerScore"));
            Assert.That(attribute.NicifyText, Is.True);
        }

        /// <summary>
        /// 空白标签在构造时报错。
        /// <para>
        /// 不静默忽略的理由：<c>[LabelText("")]</c> 的表现是「标签不见了」，
        /// 而那正是最难归因的一类现象。想撤掉标签有 <c>[HideLabel]</c>，那才是它的用途。
        /// </para>
        /// </summary>
        /// <param name="text">无效的标签文本。</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void LabelText_空白文本抛异常(string text)
        {
            Assert.Throws<ArgumentException>(() => new LabelTextAttribute(text));
        }

        #endregion

        #region [PropertyTooltip]

        /// <summary>提示文本被原样保留。</summary>
        [Test]
        public void Tooltip_保留文本()
        {
            Assert.That(new PropertyTooltipAttribute("每秒恢复").Tooltip, Is.EqualTo("每秒恢复"));
        }

        /// <summary>空白提示在构造时报错——空提示等于没提示，几乎必然是笔误。</summary>
        /// <param name="tooltip">无效的提示文本。</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Tooltip_空白文本抛异常(string tooltip)
        {
            Assert.Throws<ArgumentException>(() => new PropertyTooltipAttribute(tooltip));
        }

        #endregion
    }
}
