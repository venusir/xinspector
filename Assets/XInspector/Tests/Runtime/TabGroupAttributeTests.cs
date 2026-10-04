using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[TabGroup]</c> 的路径合成与容器判定。不依赖 Unity，同时跑在离线通道里。
    /// <para>
    /// 「容器还是页」这个判定是页签的全部机巧所在：容器的特性由 <c>CloneForPath</c>
    /// 改写路径而来，靠那个字段认出来——所以它值得逐条钉住。
    /// </para>
    /// </summary>
    [TestFixture]
    public class TabGroupAttributeTests
    {
        /// <summary>路径由「组名/页签名」合成，两段都规范化。</summary>
        [Test]
        public void 路径合成()
        {
            var attribute = new TabGroupAttribute(" 设置 ", " 基础 ");

            Assert.That(attribute.GroupID, Is.EqualTo("设置/基础"));
            Assert.That(attribute.TabsGroupID, Is.EqualTo("设置"));
            Assert.That(attribute.TabName, Is.EqualTo("基础"));
            Assert.That(attribute.GroupName, Is.EqualTo("基础"), "显示名是路径末段，即页签名。");
        }

        /// <summary>只给页签名时归入默认组。</summary>
        [Test]
        public void 默认分组()
        {
            var attribute = new TabGroupAttribute("基础");

            Assert.That(attribute.GroupID, Is.EqualTo(TabGroupAttribute.DEFAULT_NAME + "/基础"));
            Assert.That(attribute.TabsGroupID, Is.EqualTo(TabGroupAttribute.DEFAULT_NAME));
        }

        /// <summary>嵌套组名（多段路径）同样工作：父路径就是容器。</summary>
        [Test]
        public void 嵌套组名()
        {
            var attribute = new TabGroupAttribute("外层/设置", "基础");

            Assert.That(attribute.GroupID, Is.EqualTo("外层/设置/基础"));
            Assert.That(attribute.TabsGroupID, Is.EqualTo("外层/设置"));
        }

        /// <summary>
        /// 声明的那个实例**不是**容器；<c>CloneForPath</c> 到组名路径的克隆**是**。
        /// </summary>
        [Test]
        public void 容器判定()
        {
            var page = new TabGroupAttribute("设置", "基础");
            Assert.That(page.IsContainer, Is.False, "声明实例描述的是页。");

            // CloneForPath 的静态返回类型是基类；实际对象仍是子类（MemberwiseClone）。
            var container = (TabGroupAttribute)page.CloneForPath("设置");
            Assert.That(container.IsContainer, Is.True, "路径被改写为组名之后就是容器。");
            Assert.That(container.TabsGroupID, Is.EqualTo("设置"), "克隆保留原字段，判定据此成立。");
            Assert.That(container.TabName, Is.EqualTo("基础"), "页名也一并带过去（容器不再用它）。");
        }

        /// <summary>空白组名或页签名构造期报错。</summary>
        [Test]
        public void 空白名构造期报错()
        {
            Assert.Throws<ArgumentException>(() => new TabGroupAttribute(""));
            Assert.Throws<ArgumentException>(() => new TabGroupAttribute("  "));
            Assert.Throws<ArgumentException>(() => new TabGroupAttribute("设置", "  "));
            Assert.Throws<ArgumentException>(() => new TabGroupAttribute("", "基础"));
        }

        /// <summary>可选开关的默认值。</summary>
        [Test]
        public void 可选开关的默认值()
        {
            var attribute = new TabGroupAttribute("设置", "基础");

            Assert.That(attribute.UseFixedHeight, Is.False);
            Assert.That(attribute.HideTabGroupIfTabGroupOnlyHasOneTab, Is.False);
        }

        /// <summary>分组特性可用在类上、可重复。</summary>
        [Test]
        public void 可用于类且可重复()
        {
            var usage = typeof(TabGroupAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.True);
        }
    }
}
