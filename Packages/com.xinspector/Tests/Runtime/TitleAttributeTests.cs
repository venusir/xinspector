using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <see cref="TitleAttribute"/> 的行为与用法约束。
    /// </summary>
    [TestFixture]
    public class TitleAttributeTests
    {
        #region 构造

        /// <summary>
        /// 标题被原样保留，副标题默认为空。
        /// </summary>
        [Test]
        public void 构造_保留标题且副标题默认为空()
        {
            var attribute = new TitleAttribute("玩家档案");

            Assert.That(attribute.Title, Is.EqualTo("玩家档案"));
            Assert.That(attribute.Subtitle, Is.Null);
        }

        /// <summary>
        /// 空白标题在构造时报错。
        /// <para>
        /// 不静默忽略的理由：<c>[Title("")]</c> 的表现是「什么都没画」，
        /// 而那正是最难归因的一类现象。宁可在构建期明确报错。
        /// </para>
        /// </summary>
        /// <param name="title">无效的标题。</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t")]
        public void 构造_空白标题抛异常(string title)
        {
            Assert.Throws<ArgumentException>(() => new TitleAttribute(title));
        }

        #endregion

        #region 用法约束

        /// <summary>
        /// <see cref="TitleAttribute"/> 可用于类与成员，但不可重复标注。
        /// <para>
        /// 不可重复是刻意的：一个属性两个标题没有明确语义，允许它只会引出「哪个赢」
        /// 这种无谓的规则。这条约束由 <see cref="AttributeUsageAttribute"/> 声明，
        /// 用例把它钉住以免日后被无意放宽。
        /// </para>
        /// </summary>
        [Test]
        public void 用法约束_可用于类与成员且不可重复()
        {
            var usage = typeof(TitleAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null, "TitleAttribute 必须声明 AttributeUsage。");
            Assert.That(usage.AllowMultiple, Is.False, "TitleAttribute 不应允许重复标注。");

            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.True, "类级标题由特性处理器合成，必须允许用在类上。");
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Struct), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Property), Is.True);
        }

        #endregion
    }
}
