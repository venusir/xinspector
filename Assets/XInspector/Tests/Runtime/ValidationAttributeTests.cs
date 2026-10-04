using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// 校验与钳制五特性的构造行为与用法约束。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ValidationAttributeTests
    {
        #region [Required]

        /// <summary>无参构造：错误级别、无自定义消息（绘制期用默认文本）。</summary>
        [Test]
        public void Required_默认级别为错误()
        {
            var attribute = new RequiredAttribute();

            Assert.That(attribute.MessageType, Is.EqualTo(InfoMessageType.Error));
            Assert.That(attribute.ErrorMessage, Is.Null, "null 表示使用默认文本。");
        }

        /// <summary>只给级别的重载。</summary>
        [Test]
        public void Required_可只给级别()
        {
            var attribute = new RequiredAttribute(InfoMessageType.Warning);

            Assert.That(attribute.MessageType, Is.EqualTo(InfoMessageType.Warning));
            Assert.That(attribute.ErrorMessage, Is.Null);
        }

        /// <summary>消息与级别都被原样保留。</summary>
        [Test]
        public void Required_保留消息与级别()
        {
            var attribute = new RequiredAttribute("必须填", InfoMessageType.Info);

            Assert.That(attribute.ErrorMessage, Is.EqualTo("必须填"));
            Assert.That(attribute.MessageType, Is.EqualTo(InfoMessageType.Info));
        }

        /// <summary>显式传 null 等价于「用默认文本」，不是笔误。</summary>
        [Test]
        public void Required_显式null视为默认文本()
        {
            Assert.That(new RequiredAttribute((string)null).ErrorMessage, Is.Null);
        }

        /// <summary>
        /// 空白消息构造期抛异常。
        /// <para>
        /// 与 <c>[Title("")]</c> 同一条理由：空白几乎必然是笔误，而它的表现是
        /// 「框里什么都不显示」——想用默认文本请传 null 或用无参构造。
        /// </para>
        /// </summary>
        [Test]
        public void Required_空白消息构造期报错()
        {
            Assert.Throws<ArgumentException>(() => new RequiredAttribute(""));
            Assert.Throws<ArgumentException>(() => new RequiredAttribute("   "));
            Assert.Throws<ArgumentException>(() => new RequiredAttribute("  ", InfoMessageType.Warning));
        }

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void Required_仅成员且不可重复()
        {
            var usage = typeof(RequiredAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }

        #endregion

        #region [MinValue] / [MaxValue]

        /// <summary>上下限被原样保留。</summary>
        [Test]
        public void 钳制特性_保留边界()
        {
            Assert.That(new MinValueAttribute(-2.5).MinValue, Is.EqualTo(-2.5));
            Assert.That(new MaxValueAttribute(100.5).MaxValue, Is.EqualTo(100.5));
        }

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void 钳制特性_仅成员且不可重复()
        {
            foreach (var type in new[] { typeof(MinValueAttribute), typeof(MaxValueAttribute) })
            {
                var usage = type.GetCustomAttribute<AttributeUsageAttribute>();

                Assert.That(usage, Is.Not.Null, type.Name);
                Assert.That(usage.AllowMultiple, Is.False, type.Name);
                Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True, type.Name);
                Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False, type.Name);
            }
        }

        #endregion

        #region [AssetsOnly] / [SceneObjectsOnly]

        /// <summary>两个引用校验特性都是无参、仅成员、不可重复。</summary>
        [Test]
        public void 引用校验特性_仅成员且不可重复()
        {
            foreach (var type in new[] { typeof(AssetsOnlyAttribute), typeof(SceneObjectsOnlyAttribute) })
            {
                var usage = type.GetCustomAttribute<AttributeUsageAttribute>();

                Assert.That(usage, Is.Not.Null, type.Name);
                Assert.That(usage.AllowMultiple, Is.False, type.Name);
                Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True, type.Name);
                Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Parameter), Is.False, type.Name);
            }
        }

        #endregion
    }
}
