using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[MinMaxSlider]</c> 的五组构造与用法约束。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class MinMaxSliderAttributeTests
    {
        #region 五组构造

        /// <summary>官方五组重载各自把参数落到该落的字段上。</summary>
        [Test]
        public void 五组构造()
        {
            var literals = new MinMaxSliderAttribute(-10f, 10f);
            Assert.That(literals.MinValue, Is.EqualTo(-10f));
            Assert.That(literals.MaxValue, Is.EqualTo(10f));
            Assert.That(literals.MinValueGetter, Is.Null);
            Assert.That(literals.MaxValueGetter, Is.Null);
            Assert.That(literals.MinMaxValueGetter, Is.Null);
            Assert.That(literals.ShowFields, Is.False);

            var oneMember = new MinMaxSliderAttribute("range", true);
            Assert.That(oneMember.MinMaxValueGetter, Is.EqualTo("range"));
            Assert.That(oneMember.ShowFields, Is.True);
            Assert.That(oneMember.MinValueGetter, Is.Null, "一个成员形态不设 min/max getter。");

            var minMember = new MinMaxSliderAttribute("lo", 10f, true);
            Assert.That(minMember.MinValueGetter, Is.EqualTo("lo"));
            Assert.That(minMember.MaxValue, Is.EqualTo(10f));
            Assert.That(minMember.MaxValueGetter, Is.Null);
            Assert.That(minMember.ShowFields, Is.True);

            var maxMember = new MinMaxSliderAttribute(-10f, "hi", true);
            Assert.That(maxMember.MinValue, Is.EqualTo(-10f));
            Assert.That(maxMember.MaxValueGetter, Is.EqualTo("hi"));
            Assert.That(maxMember.ShowFields, Is.True);

            var twoMembers = new MinMaxSliderAttribute("lo", "hi", true);
            Assert.That(twoMembers.MinValueGetter, Is.EqualTo("lo"));
            Assert.That(twoMembers.MaxValueGetter, Is.EqualTo("hi"));
            Assert.That(twoMembers.ShowFields, Is.True);
        }

        /// <summary><c>showFields</c> 有默认值，可省。</summary>
        [Test]
        public void showFields可省()
        {
            Assert.That(new MinMaxSliderAttribute(0f, 1f).ShowFields, Is.False);
            Assert.That(new MinMaxSliderAttribute("range").ShowFields, Is.False);
            Assert.That(new MinMaxSliderAttribute("lo", 1f).ShowFields, Is.False);
            Assert.That(new MinMaxSliderAttribute(0f, "hi").ShowFields, Is.False);
            Assert.That(new MinMaxSliderAttribute("lo", "hi").ShowFields, Is.False);
        }

        #endregion

        #region 字面量形态的构造期校验

        /// <summary>两个字面量倒置（或相等）时构造期报错——与 [PropertyRange]/[Wrap] 同一条规则。</summary>
        /// <remarks>
        /// 只校验**两个字面量**那一组：动态边界要等到绘制期才有值，构造期无从校验。
        /// </remarks>
        [Test]
        public void 字面量倒置时构造期报错()
        {
            Assert.Throws<ArgumentException>(() => new MinMaxSliderAttribute(10f, 0f));
            Assert.Throws<ArgumentException>(() => new MinMaxSliderAttribute(5f, 5f));
        }

        /// <summary>带成员的那几组不做构造期校验（值在绘制期才知道）。</summary>
        [Test]
        public void 成员形态不做构造期校验()
        {
            Assert.DoesNotThrow(() => new MinMaxSliderAttribute("lo", "hi"));
            Assert.DoesNotThrow(() => new MinMaxSliderAttribute("lo", -5f));
        }

        #endregion

        #region 用法

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void 仅成员且不可重复()
        {
            var usage = typeof(MinMaxSliderAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }

        #endregion
    }
}
