using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <see cref="InfoBoxAttribute"/>、<see cref="DetailedInfoBoxAttribute"/> 与
    /// <see cref="InfoMessageType"/> 的构造行为与用法约束。
    /// </summary>
    [TestFixture]
    public class InfoBoxAttributeTests
    {
        #region [InfoBox]

        /// <summary>只给消息时样式默认 Info、无条件。</summary>
        [Test]
        public void InfoBox_默认样式与无条件()
        {
            var attribute = new InfoBoxAttribute("说明");

            Assert.That(attribute.Message, Is.EqualTo("说明"));
            Assert.That(attribute.InfoMessageType, Is.EqualTo(InfoMessageType.Info));
            Assert.That(attribute.VisibleIfMemberName, Is.Null);
        }

        /// <summary>两参构造接的是**条件成员名**，样式仍取默认。</summary>
        [Test]
        public void InfoBox_两参构造接条件名()
        {
            var attribute = new InfoBoxAttribute("说明", "showTip");

            Assert.That(attribute.VisibleIfMemberName, Is.EqualTo("showTip"));
            Assert.That(attribute.InfoMessageType, Is.EqualTo(InfoMessageType.Info));
        }

        /// <summary>三参构造三件都保留。</summary>
        [Test]
        public void InfoBox_三参构造()
        {
            var attribute = new InfoBoxAttribute("说明", InfoMessageType.Error, "showTip");

            Assert.That(attribute.InfoMessageType, Is.EqualTo(InfoMessageType.Error));
            Assert.That(attribute.VisibleIfMemberName, Is.EqualTo("showTip"));
        }

        /// <summary>空白消息在构造时报错。</summary>
        /// <param name="message">无效的消息文本。</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void InfoBox_空白消息抛异常(string message)
        {
            Assert.Throws<ArgumentException>(() => new InfoBoxAttribute(message));
        }

        /// <summary>
        /// 可用在类上且允许重复——与 Odin 一致。
        /// <para>
        /// 允许重复是必要的：一个成员可以挂多条说明，样式与条件各不相同。
        /// </para>
        /// </summary>
        [Test]
        public void InfoBox_可用在类上且允许重复()
        {
            var usage = typeof(InfoBoxAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
        }

        #endregion

        #region [DetailedInfoBox]

        /// <summary>摘要与详情都被保留，样式与条件默认。</summary>
        [Test]
        public void DetailedInfoBox_保留摘要与详情()
        {
            var attribute = new DetailedInfoBoxAttribute("摘要", "详情");

            Assert.That(attribute.Message, Is.EqualTo("摘要"));
            Assert.That(attribute.Details, Is.EqualTo("详情"));
            Assert.That(attribute.InfoMessageType, Is.EqualTo(InfoMessageType.Info));
            Assert.That(attribute.VisibleIf, Is.Null);
        }

        /// <summary>空白摘要报错；详情为空是合法的（只画摘要）。</summary>
        [Test]
        public void DetailedInfoBox_摘要不可为空而详情可()
        {
            Assert.Throws<ArgumentException>(() => new DetailedInfoBoxAttribute("  ", "详情"));
            Assert.DoesNotThrow(() => new DetailedInfoBoxAttribute("摘要", null));
        }

        #endregion

        #region [InfoMessageType]

        /// <summary>
        /// 四个成员与取值——这是本包自造的类型，取值由我们定，故在此钉住。
        /// <para>
        /// 官方文档站按字母序列出成员，**声明顺序与数值没有公开**；本包取
        /// <c>None = 0</c>，其余与 Unity 的 <c>MessageType</c> 同序，便于映射。
        /// </para>
        /// </summary>
        [Test]
        public void InfoMessageType_取值()
        {
            Assert.That((int)InfoMessageType.None, Is.EqualTo(0));
            Assert.That((int)InfoMessageType.Info, Is.EqualTo(1));
            Assert.That((int)InfoMessageType.Warning, Is.EqualTo(2));
            Assert.That((int)InfoMessageType.Error, Is.EqualTo(3));
        }

        #endregion
    }
}
