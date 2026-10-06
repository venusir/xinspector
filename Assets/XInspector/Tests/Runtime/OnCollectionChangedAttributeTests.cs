using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[OnCollectionChanged]</c> 与它的 <c>CollectionChangeInfo</c> 的公开形状。
    /// 不依赖 Unity，同时跑在离线通道里。
    /// </summary>
    [TestFixture]
    public class OnCollectionChangedAttributeTests
    {
        /// <summary>两个方法名可以只给一个；给的那一个原样收下。</summary>
        [Test]
        public void 只给一个方法名是合法的()
        {
            Assert.That(new OnCollectionChangedAttribute("Before").After, Is.Null);
            Assert.That(new OnCollectionChangedAttribute("Before").Before, Is.EqualTo("Before"));
            Assert.That(new OnCollectionChangedAttribute(after: "After").Before, Is.Null);
            Assert.That(new OnCollectionChangedAttribute(after: "After").After, Is.EqualTo("After"));
        }

        /// <summary>
        /// 两个都不给是**构造失败**而不是「安静地什么都不做」——本包最忌讳后者，
        /// 而这里连字段都还没建树，正是能大声说出来的地方。
        /// </summary>
        [Test]
        public void 两个方法名都为空时构造失败()
        {
            Assert.Throws<ArgumentException>(() => new OnCollectionChangedAttribute());
            Assert.Throws<ArgumentException>(() => new OnCollectionChangedAttribute("  ", null));
            Assert.Throws<ArgumentException>(() => new OnCollectionChangedAttribute(null, "   "));
        }

        /// <summary>空白名归一为 <c>null</c>（「这个方向不要」只有一种写法）。</summary>
        [Test]
        public void 空白名归一为null()
        {
            var attribute = new OnCollectionChangedAttribute("  ", " After ");

            Assert.That(attribute.Before, Is.Null);
            Assert.That(attribute.After, Is.EqualTo("After"), "首尾空白要去掉——那不是方法名的一部分。");
        }

        /// <summary>只标在成员上、不可重复（一个字段只有一个集合，两个方向由参数说）。</summary>
        [Test]
        public void 用于成员且不可重复()
        {
            var usage = typeof(OnCollectionChangedAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Property), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False, "集合回调是成员的事。");
        }

        /// <summary>
        /// 改动类型的取值是**本包真会产生的那几个**：多声明一个永远不出现的取值，
        /// 就是「编译得过但什么都不发生」。日后真做了移动/排序再加。
        /// </summary>
        [Test]
        public void 改动类型只声明本包会产生的取值()
        {
            var names = Enum.GetNames(typeof(CollectionChangeType));

            Assert.That(names, Is.EquivalentTo(new[] { "Add", "RemoveAt" }));
        }

        /// <summary>变更描述把三样东西原样收着（构造之后不可改）。</summary>
        [Test]
        public void 变更描述收下三样东西()
        {
            var info = new CollectionChangeInfo(CollectionChangeType.RemoveAt, 3, "盾");

            Assert.That(info.Type, Is.EqualTo(CollectionChangeType.RemoveAt));
            Assert.That(info.Index, Is.EqualTo(3));
            Assert.That(info.Value, Is.EqualTo("盾"));
        }
    }
}
