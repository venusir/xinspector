using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[Searchable]</c> 的公开形状。不依赖 Unity，同时跑在离线通道里。
    /// </summary>
    [TestFixture]
    public class SearchableAttributeTests
    {
        /// <summary>
        /// 没有任何旋钮——本包不声明核不到的选项（<c>Recursive</c> / <c>FilterOptions</c>）。
        /// </summary>
        [Test]
        public void 无构造参数也没有旋钮()
        {
            var constructors = typeof(SearchableAttribute).GetConstructors();

            Assert.That(constructors.Length, Is.EqualTo(1));
            Assert.That(constructors[0].GetParameters().Length, Is.EqualTo(0));
            Assert.That(
                typeof(SearchableAttribute).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                Is.Empty,
                "写了会编译不过，而不是静默无效。");
        }

        /// <summary>只标在成员上、不可重复（一个字段一个搜索框）。</summary>
        [Test]
        public void 用于成员且不可重复()
        {
            var usage = typeof(SearchableAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Property), Is.True);
            Assert.That(
                usage.ValidOn.HasFlag(AttributeTargets.Class),
                Is.False,
                "标在类型上的形态本轮不做（类级特性只对被检视的最外层类型收集）。");
        }
    }
}
