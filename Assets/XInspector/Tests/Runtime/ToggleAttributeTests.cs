using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[Toggle]</c> 的构造行为与用法约束。不依赖 Unity，同时跑在离线通道里。
    /// </summary>
    [TestFixture]
    public class ToggleAttributeTests
    {
        /// <summary>开关名被原样保留。</summary>
        [Test]
        public void Toggle_保留开关名()
        {
            Assert.That(new ToggleAttribute("Enabled").ToggleMemberName, Is.EqualTo("Enabled"));
            Assert.That(new ToggleAttribute("Nested/Flag").ToggleMemberName, Is.EqualTo("Nested/Flag"),
                "支持嵌套路径。");
        }

        /// <summary>空白开关名构造期报错（笔误到无法猜测意图）。</summary>
        [Test]
        public void Toggle_空白开关名构造期报错()
        {
            Assert.Throws<ArgumentException>(() => new ToggleAttribute(""));
            Assert.Throws<ArgumentException>(() => new ToggleAttribute("   "));
            Assert.Throws<ArgumentException>(() => new ToggleAttribute(null));
        }

        /// <summary>CollapseOthersOnExpand 保留字段、默认关闭（不做行为）。</summary>
        [Test]
        public void Toggle_可选字段的默认值()
        {
            Assert.That(new ToggleAttribute("Enabled").CollapseOthersOnExpand, Is.False);
        }

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void Toggle_仅成员且不可重复()
        {
            var usage = typeof(ToggleAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }
    }
}
