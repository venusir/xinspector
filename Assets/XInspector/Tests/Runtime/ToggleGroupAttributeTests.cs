using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[ToggleGroup]</c> 的构造行为与合并规则。不依赖 Unity，同时跑在离线通道里。
    /// </summary>
    [TestFixture]
    public class ToggleGroupAttributeTests
    {
        /// <summary>开关成员名**就是**分组 ID——同组必须写同一个名字。</summary>
        [Test]
        public void 开关成员名充当分组ID()
        {
            var attribute = new ToggleGroupAttribute("showAdvanced");

            Assert.That(attribute.ToggleMemberName, Is.EqualTo("showAdvanced"));
            Assert.That(attribute.GroupID, Is.EqualTo("showAdvanced"));
            Assert.That(attribute.GroupName, Is.EqualTo("showAdvanced"));
        }

        /// <summary>两个构造：只给开关名，或连标题一起给。</summary>
        [Test]
        public void 两个构造()
        {
            Assert.That(new ToggleGroupAttribute("flag").ToggleGroupTitle, Is.Null, "null 表示绘制期用开关成员名。");
            Assert.That(new ToggleGroupAttribute("flag", "高级选项").ToggleGroupTitle, Is.EqualTo("高级选项"));
            Assert.That(new ToggleGroupAttribute("flag", 3f, "标题").Order, Is.EqualTo(3f));
        }

        /// <summary>空白开关名构造期报错（它同时是分组 ID，空白无法归并）。</summary>
        [Test]
        public void 空白开关名构造期报错()
        {
            Assert.Throws<ArgumentException>(() => new ToggleGroupAttribute(""));
            Assert.Throws<ArgumentException>(() => new ToggleGroupAttribute("   "));
            Assert.Throws<ArgumentException>(() => new ToggleGroupAttribute(null));
        }

        /// <summary>合并时标题取先出现的非空值。</summary>
        [Test]
        public void 合并取先出现的标题()
        {
            var first = new ToggleGroupAttribute("flag");
            var second = new ToggleGroupAttribute("flag", "标题");

            first.Combine(second);
            Assert.That(first.ToggleGroupTitle, Is.EqualTo("标题"));

            var explicitFirst = new ToggleGroupAttribute("flag", "先声明的");
            explicitFirst.Combine(new ToggleGroupAttribute("flag", "后声明的"));
            Assert.That(explicitFirst.ToggleGroupTitle, Is.EqualTo("先声明的"), "先声明者优先。");
        }

        /// <summary>CollapseOthersOnExpand 保留字段、默认关闭（不做行为）。</summary>
        [Test]
        public void 可选字段的默认值()
        {
            Assert.That(new ToggleGroupAttribute("flag").CollapseOthersOnExpand, Is.False);
        }

        /// <summary>分组特性可用在类上、可重复。</summary>
        [Test]
        public void 可用于类且可重复()
        {
            var usage = typeof(ToggleGroupAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.True);
        }
    }
}
