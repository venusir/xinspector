using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[ShowIfGroup]</c> / <c>[HideIfGroup]</c> 的构造、克隆与合并规则。不依赖 Unity，
    /// 同时跑在离线通道里。
    /// </summary>
    [TestFixture]
    public class GroupConditionAttributeTests
    {
        /// <summary>组名兼条件名：条件默认取路径末段。</summary>
        [Test]
        public void 条件默认取路径末段()
        {
            var attribute = new ShowIfGroupAttribute("Box/Toggle");

            Assert.That(attribute.GroupID, Is.EqualTo("Box/Toggle"));
            Assert.That(attribute.GroupName, Is.EqualTo("Toggle"));
            Assert.That(attribute.Condition, Is.EqualTo("Toggle"));
        }

        /// <summary>取的是**规范化之后**的末段（多余空白与空段先被丢掉）。</summary>
        [Test]
        public void 条件默认取规范化后的末段()
        {
            Assert.That(new ShowIfGroupAttribute(" Box / Toggle ").Condition, Is.EqualTo("Toggle"));
        }

        /// <summary>显式条件覆盖默认值。</summary>
        [Test]
        public void 显式条件可覆盖()
        {
            var attribute = new ShowIfGroupAttribute("战斗组") { Condition = "flag" };

            Assert.That(attribute.Condition, Is.EqualTo("flag"));
        }

        /// <summary>空白路径构造期报错（它同时是分组的身份，空白无法归并）。</summary>
        [Test]
        public void 空路径构造期报错()
        {
            Assert.Throws<ArgumentException>(() => new ShowIfGroupAttribute(""));
            Assert.Throws<ArgumentException>(() => new HideIfGroupAttribute("   "));
            Assert.Throws<ArgumentException>(() => new ShowIfGroupAttribute(null));
        }

        /// <summary>祖先克隆不把条件带过去——兄弟分组不该被误伤。</summary>
        [Test]
        public void 祖先克隆不挂条件()
        {
            var declared = new ShowIfGroupAttribute("Box/Toggle");

            var leaf = (GroupConditionAttribute)declared.CloneForPath("Box/Toggle");
            var ancestor = (GroupConditionAttribute)declared.CloneForPath("Box");

            Assert.That(leaf.AppliesToOwnNode, Is.True, "声明那一节照常生效。");
            Assert.That(ancestor.AppliesToOwnNode, Is.False, "祖先节点只贡献路径，不做判据。");
        }

        /// <summary>类级分组加前缀的改写认作新声明路径，条件跟着走（条件名本身不变）。</summary>
        [Test]
        public void 前缀改写后条件跟着走()
        {
            var rewritten = (GroupConditionAttribute)new ShowIfGroupAttribute("Toggle").CloneForPath("Root/Toggle");

            Assert.That(rewritten.AppliesToOwnNode, Is.True);
            Assert.That(rewritten.Condition, Is.EqualTo("Toggle"), "条件名取声明时的末段，不随前缀变。");
        }

        /// <summary>归并时真正声明在本路径上的那份赢——先到的祖先克隆不该把条件压掉。</summary>
        [Test]
        public void 归并时真正声明者赢()
        {
            var ancestorClone = (GroupConditionAttribute)new ShowIfGroupAttribute("Box/Toggle").CloneForPath("Box");
            var declaredHere = new ShowIfGroupAttribute("Box");

            ancestorClone.Combine(declaredHere);

            Assert.That(ancestorClone.AppliesToOwnNode, Is.True);
            Assert.That(ancestorClone.Condition, Is.EqualTo("Box"));
        }

        /// <summary>同路径两份真声明仍是先声明者优先（分组族的既有规则）。</summary>
        [Test]
        public void 同路径两份声明先声明者优先()
        {
            var first = new ShowIfGroupAttribute("flag") { Condition = "a" };
            var second = new ShowIfGroupAttribute("flag") { Condition = "b" };

            first.Combine(second);

            Assert.That(first.Condition, Is.EqualTo("a"));
        }

        /// <summary>分组条件可用在类上、可重复（Odin 的链式用法靠后者）。</summary>
        [Test]
        public void 可用于类且可重复()
        {
            var usage = typeof(ShowIfGroupAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.True);
        }
    }
}
