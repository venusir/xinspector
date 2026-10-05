using System;
using System.Reflection;
using NUnit.Framework;
using XInspector.Internal;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[PropertyOrder]</c> 的构造行为与用法约束。不依赖 Unity，同时跑在离线通道里。
    /// </summary>
    [TestFixture]
    public class PropertyOrderAttributeTests
    {
        /// <summary>两个构造：默认 0，或给一个数值。</summary>
        [Test]
        public void 两个构造()
        {
            Assert.That(new PropertyOrderAttribute().Order, Is.EqualTo(0f));
            Assert.That(new PropertyOrderAttribute(-1f).Order, Is.EqualTo(-1f));
        }

        /// <summary>0 是合法排序值，而不是「未指定」——与分组 Order 的规则刻意不同。</summary>
        [Test]
        public void 零是合法排序值()
        {
            Assert.That(new PropertyOrderAttribute(0f).Order, Is.EqualTo(0f));
        }

        /// <summary>属性可写——官方的具名实参写法 <c>[PropertyOrder(Order = -1)]</c> 同样编译得过。</summary>
        [Test]
        public void 属性可写()
        {
            Assert.That(new PropertyOrderAttribute { Order = -1f }.Order, Is.EqualTo(-1f));
        }

        /// <summary>可用于字段、属性与方法（方法与字段同在成员列表里）；不可用于类。</summary>
        [Test]
        public void 可用于字段属性与方法()
        {
            var usage = typeof(PropertyOrderAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Property), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Method), Is.True, "方法会产生节点，排序对它同样生效。");
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False, "成员顺序是成员的事。");
        }

        /// <summary>
        /// 它实现 <see cref="ITreeOrderingAttribute"/>——自动接管的判据靠这个标记认出
        /// 「只标了它」的类型；没有这条，排序会**静默失效**。
        /// </summary>
        [Test]
        public void 实现建树期标记接口()
        {
            Assert.That(new PropertyOrderAttribute(), Is.InstanceOf<ITreeOrderingAttribute>());
        }
    }
}
