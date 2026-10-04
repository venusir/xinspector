using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// 值控件五特性的构造行为与用法约束。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ValueControlAttributeTests
    {
        #region [MultiLineProperty]

        /// <summary>默认 3 行——官方的构造就是带默认值的，不是无参。</summary>
        [Test]
        public void MultiLineProperty_默认三行()
        {
            Assert.That(new MultiLinePropertyAttribute().Lines, Is.EqualTo(3));
            Assert.That(new MultiLinePropertyAttribute(5).Lines, Is.EqualTo(5));
        }

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void MultiLineProperty_仅成员且不可重复()
        {
            AssertMemberOnly(typeof(MultiLinePropertyAttribute));
        }

        #endregion

        #region [DelayedProperty] / [EnumPaging]

        /// <summary>两个无参特性都仅用于成员、不可重复。</summary>
        [Test]
        public void 无参特性_仅成员且不可重复()
        {
            AssertMemberOnly(typeof(DelayedPropertyAttribute));
            AssertMemberOnly(typeof(EnumPagingAttribute));
        }

        #endregion

        #region [PropertyRange]

        /// <summary>范围被保留；getter 字段恒为 null（本包不做 $ 表达式族）。</summary>
        [Test]
        public void PropertyRange_保留范围且getter为空()
        {
            var attribute = new PropertyRangeAttribute(-1, 1);

            Assert.That(attribute.Min, Is.EqualTo(-1));
            Assert.That(attribute.Max, Is.EqualTo(1));
            Assert.That(attribute.MinGetter, Is.Null, "保留字段只为让照 Odin 写的代码编译得过。");
            Assert.That(attribute.MaxGetter, Is.Null);
        }

        /// <summary>范围倒置时构造期报错。</summary>
        [Test]
        public void PropertyRange_范围倒置时构造期报错()
        {
            Assert.Throws<ArgumentException>(() => new PropertyRangeAttribute(10, 0));
            Assert.Throws<ArgumentException>(() => new PropertyRangeAttribute(5, 5));
        }

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void PropertyRange_仅成员且不可重复()
        {
            AssertMemberOnly(typeof(PropertyRangeAttribute));
        }

        #endregion

        #region [Wrap]

        /// <summary>范围被保留。</summary>
        [Test]
        public void Wrap_保留范围()
        {
            var attribute = new WrapAttribute(0, 360);

            Assert.That(attribute.Min, Is.EqualTo(0));
            Assert.That(attribute.Max, Is.EqualTo(360));
        }

        /// <summary>范围倒置时构造期报错（回绕区间反过来没有意义）。</summary>
        [Test]
        public void Wrap_范围倒置时构造期报错()
        {
            Assert.Throws<ArgumentException>(() => new WrapAttribute(360, 0));
            Assert.Throws<ArgumentException>(() => new WrapAttribute(1, 1));
        }

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void Wrap_仅成员且不可重复()
        {
            AssertMemberOnly(typeof(WrapAttribute));
        }

        #endregion

        #region Private Helpers

        /// <summary>断言「仅用于成员、不可重复、可继承」这条统一的用法约束。</summary>
        /// <param name="type">特性类型。</param>
        private static void AssertMemberOnly(Type type)
        {
            var usage = type.GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null, type.Name);
            Assert.That(usage.AllowMultiple, Is.False, type.Name);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True, type.Name);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False, type.Name);
        }

        #endregion
    }
}
