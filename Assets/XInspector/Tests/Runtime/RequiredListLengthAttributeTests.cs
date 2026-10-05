using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[RequiredListLength]</c> 的构造与参数校验。不依赖 Unity，同时跑在离线通道里。
    /// </summary>
    [TestFixture]
    public class RequiredListLengthAttributeTests
    {
        /// <summary>单参构造 = 恰好这么多项（上下限相等）。</summary>
        [Test]
        public void 单参构造等于恰好()
        {
            var attribute = new RequiredListLengthAttribute(3);

            Assert.That(attribute.MinLength, Is.EqualTo(3));
            Assert.That(attribute.MaxLength, Is.EqualTo(3));
        }

        /// <summary>双参构造允许只给一侧（官方样例里的 <c>[RequiredListLength(1, null)]</c>）。</summary>
        [Test]
        public void 双参构造允许只给一侧()
        {
            Assert.That(new RequiredListLengthAttribute(1, null).MaxLength, Is.Null);
            Assert.That(new RequiredListLengthAttribute(null, 8).MinLength, Is.Null);
            Assert.That(new RequiredListLengthAttribute(1, 8).MaxLength, Is.EqualTo(8));
        }

        /// <summary>笔误当场炸，而不是变成一个恒假的检查静默留在那里。</summary>
        [Test]
        public void 非法参数当场抛()
        {
            Assert.Throws<ArgumentException>(() => new RequiredListLengthAttribute(-1));
            Assert.Throws<ArgumentException>(() => new RequiredListLengthAttribute(null, null));
            Assert.Throws<ArgumentException>(() => new RequiredListLengthAttribute(5, 2));
            Assert.Throws<ArgumentException>(() => new RequiredListLengthAttribute(null, -3));
        }

        /// <summary>消息与级别：默认错误级、默认无自定义文本。</summary>
        [Test]
        public void 消息默认值()
        {
            var attribute = new RequiredListLengthAttribute(2);

            Assert.That(attribute.ErrorMessage, Is.Null);
            Assert.That(attribute.MessageType, Is.EqualTo(InfoMessageType.Error));
        }

        /// <summary>只标在字段上、不可重复、可继承。</summary>
        [Test]
        public void 用于字段()
        {
            var usage = typeof(RequiredListLengthAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
        }
    }
}
