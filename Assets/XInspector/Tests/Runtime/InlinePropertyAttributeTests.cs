using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[InlineProperty]</c> 的构造行为与用法约束。不依赖 Unity，同时跑在离线通道里。
    /// </summary>
    [TestFixture]
    public class InlinePropertyAttributeTests
    {
        /// <summary>标签宽度默认 0（不改），可显式设置。</summary>
        [Test]
        public void 标签宽度默认为零且可设置()
        {
            Assert.That(new InlinePropertyAttribute().LabelWidth, Is.EqualTo(0));
            Assert.That(new InlinePropertyAttribute { LabelWidth = 60 }.LabelWidth, Is.EqualTo(60));
        }

        /// <summary>类与成员都能标（标在类上时该类型的字段一律内联）；不可重复、不继承。</summary>
        [Test]
        public void 可用于类与成员()
        {
            var usage = typeof(InlinePropertyAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.False, "官方的 Inherited = false：标记写在哪一层，哪一层才内联。");
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Property), Is.True);
        }
    }
}
