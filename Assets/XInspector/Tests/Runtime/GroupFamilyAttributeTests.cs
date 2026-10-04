using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// 分组族三个呈现型（竖直 / 标题 / 折叠）的构造行为与合并规则。
    /// <para>不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。</para>
    /// </summary>
    [TestFixture]
    public class GroupFamilyAttributeTests
    {
        #region [VerticalGroup]

        /// <summary>无参构造落进默认分组；带路径的构造按路径走。</summary>
        [Test]
        public void VerticalGroup_默认分组与路径分组()
        {
            Assert.That(new VerticalGroupAttribute().GroupID, Is.EqualTo(VerticalGroupAttribute.DEFAULT_NAME));
            Assert.That(new VerticalGroupAttribute("左列").GroupID, Is.EqualTo("左列"));
            Assert.That(new VerticalGroupAttribute(" 左列 ").GroupID, Is.EqualTo("左列"), "路径构造时即规范化。");
        }

        /// <summary>内边距默认 0（不画）。</summary>
        [Test]
        public void VerticalGroup_内边距默认不画()
        {
            var attribute = new VerticalGroupAttribute("列");

            Assert.That(attribute.PaddingTop, Is.EqualTo(0f));
            Assert.That(attribute.PaddingBottom, Is.EqualTo(0f));
        }

        /// <summary>合并时内边距取**先出现的非零值**（与 Order 同一规则）。</summary>
        [Test]
        public void VerticalGroup_合并取先出现的非零内边距()
        {
            var first = new VerticalGroupAttribute("列") { PaddingTop = 4f };
            var second = new VerticalGroupAttribute("列") { PaddingTop = 9f, PaddingBottom = 6f };

            first.Combine(second);

            Assert.That(first.PaddingTop, Is.EqualTo(4f), "先声明者优先，不被后声明覆盖。");
            Assert.That(first.PaddingBottom, Is.EqualTo(6f), "先前没设的才接受后者的值。");
        }

        /// <summary>分组特性可用在类上、可重复。</summary>
        [Test]
        public void VerticalGroup_可用于类且可重复()
        {
            AssertGroupUsage(typeof(VerticalGroupAttribute));
        }

        #endregion

        #region [TitleGroup]

        /// <summary>标题即路径，其余呈现设定被原样保留。</summary>
        [Test]
        public void TitleGroup_保留全部呈现设定()
        {
            var attribute = new TitleGroupAttribute(
                "战斗属性", "只影响命中率", TitleAlignments.Centered, false, false, true, 3f);

            Assert.That(attribute.GroupID, Is.EqualTo("战斗属性"));
            Assert.That(attribute.Subtitle, Is.EqualTo("只影响命中率"));
            Assert.That(attribute.Alignment, Is.EqualTo(TitleAlignments.Centered));
            Assert.That(attribute.HorizontalLine, Is.False);
            Assert.That(attribute.BoldTitle, Is.False);
            Assert.That(attribute.Indent, Is.True);
            Assert.That(attribute.Order, Is.EqualTo(3f));
        }

        /// <summary>默认值：左对齐、有分隔线、加粗、不缩进。</summary>
        [Test]
        public void TitleGroup_默认呈现设定()
        {
            var attribute = new TitleGroupAttribute("标题");

            Assert.That(attribute.Alignment, Is.EqualTo(TitleAlignments.Left));
            Assert.That(attribute.HorizontalLine, Is.True);
            Assert.That(attribute.BoldTitle, Is.True);
            Assert.That(attribute.Indent, Is.False);
            Assert.That(attribute.Subtitle, Is.Null);
        }

        /// <summary>空白标题/副标题构造期报错（与 [Title] 同一条规则）。</summary>
        [Test]
        public void TitleGroup_空白文本构造期报错()
        {
            Assert.Throws<ArgumentException>(() => new TitleGroupAttribute(""));
            Assert.Throws<ArgumentException>(() => new TitleGroupAttribute("   "));
            Assert.Throws<ArgumentException>(() => new TitleGroupAttribute("标题", "   "));
            Assert.DoesNotThrow(() => new TitleGroupAttribute("标题", null), "null 副标题是合法写法。");
        }

        /// <summary>合并时副标题取先出现的非空值。</summary>
        [Test]
        public void TitleGroup_合并取先出现的副标题()
        {
            var first = new TitleGroupAttribute("标题");
            var second = new TitleGroupAttribute("标题", "副标题");

            first.Combine(second);
            Assert.That(first.Subtitle, Is.EqualTo("副标题"));
        }

        /// <summary>分组特性可用在类上、可重复。</summary>
        [Test]
        public void TitleGroup_可用于类且可重复()
        {
            AssertGroupUsage(typeof(TitleGroupAttribute));
        }

        #endregion

        #region [FoldoutGroup]

        /// <summary>两个构造：显式展开与默认收起。</summary>
        [Test]
        public void FoldoutGroup_两个构造()
        {
            var expanded = new FoldoutGroupAttribute("组", true);
            Assert.That(expanded.Expanded, Is.True);
            Assert.That(expanded.HasDefinedExpanded, Is.True);

            var collapsed = new FoldoutGroupAttribute("组");
            Assert.That(collapsed.Expanded, Is.False);
            Assert.That(collapsed.HasDefinedExpanded, Is.False,
                "布尔量区分不了「未设置」与「显式设为 false」，故需要这个信号。");
        }

        /// <summary>合并时展开状态取**先出现的显式值**——这才是 HasDefinedExpanded 的用途。</summary>
        [Test]
        public void FoldoutGroup_合并取先出现的显式展开状态()
        {
            var implicitCollapsed = new FoldoutGroupAttribute("组");
            var explicitExpanded = new FoldoutGroupAttribute("组", true);

            implicitCollapsed.Combine(explicitExpanded);

            Assert.That(implicitCollapsed.Expanded, Is.True, "先前没显式设过，接受后者的显式值。");
            Assert.That(implicitCollapsed.HasDefinedExpanded, Is.True);

            var explicitCollapsed = new FoldoutGroupAttribute("组", false);
            explicitCollapsed.Combine(explicitExpanded);
            Assert.That(explicitCollapsed.Expanded, Is.False, "显式的 false 不该被后者的 true 覆盖。");
        }

        /// <summary>分组特性可用在类上、可重复。</summary>
        [Test]
        public void FoldoutGroup_可用于类且可重复()
        {
            AssertGroupUsage(typeof(FoldoutGroupAttribute));
        }

        #endregion

        #region [HorizontalGroup]

        /// <summary>两种构造：默认分组与路径分组；宽度与外边距被保留。</summary>
        [Test]
        public void HorizontalGroup_两种构造()
        {
            var defaulted = new HorizontalGroupAttribute(0.5f, 2, 3);
            Assert.That(defaulted.GroupID, Is.EqualTo(HorizontalGroupAttribute.DEFAULT_NAME));
            Assert.That(defaulted.Width, Is.EqualTo(0.5f));
            Assert.That(defaulted.MarginLeft, Is.EqualTo(2f), "ctor 参数是 int，字段是 float。");
            Assert.That(defaulted.MarginRight, Is.EqualTo(3f));

            Assert.That(new HorizontalGroupAttribute("一行", 0.25f).GroupID, Is.EqualTo("一行"));
        }

        /// <summary>间距默认 4（浏览器式的贴边很难看；官方默认值未核实，这是本包自定的）。</summary>
        [Test]
        public void HorizontalGroup_间距默认四像素()
        {
            Assert.That(new HorizontalGroupAttribute().Gap, Is.EqualTo(4f));
        }

        /// <summary>合并时呈现设定取先出现的非零/非空值。</summary>
        [Test]
        public void HorizontalGroup_合并先声明者优先()
        {
            var first = new HorizontalGroupAttribute("行", 0.6f) { Gap = 8f, Title = "标题" };
            var second = new HorizontalGroupAttribute("行", 0.2f) { Gap = 2f, Title = "别的", MaxWidth = 50f };

            first.Combine(second);

            Assert.That(first.Width, Is.EqualTo(0.6f), "先声明的宽度不被覆盖。");
            Assert.That(first.Gap, Is.EqualTo(8f));
            Assert.That(first.Title, Is.EqualTo("标题"));
            Assert.That(first.MaxWidth, Is.EqualTo(50f), "先前没设的才接受后者的值。");
        }

        /// <summary>分组特性可用在类上、可重复。</summary>
        [Test]
        public void HorizontalGroup_可用于类且可重复()
        {
            AssertGroupUsage(typeof(HorizontalGroupAttribute));
        }

        #endregion

        #region [TitleAlignments]

        /// <summary>四个成员按官方文档的顺序从 0 起排（数值未从官网核实，这条钉的是本包的取值）。</summary>
        [Test]
        public void TitleAlignments_成员顺序()
        {
            Assert.That((int)TitleAlignments.Centered, Is.EqualTo(0));
            Assert.That((int)TitleAlignments.Left, Is.EqualTo(1));
            Assert.That((int)TitleAlignments.Right, Is.EqualTo(2));
            Assert.That((int)TitleAlignments.Split, Is.EqualTo(3));
        }

        #endregion

        #region Private Helpers

        /// <summary>断言分组特性的统一用法：可用于成员与类、可重复、可继承。</summary>
        /// <param name="type">分组特性类型。</param>
        private static void AssertGroupUsage(Type type)
        {
            var usage = type.GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null, type.Name);
            Assert.That(usage.AllowMultiple, Is.True, $"{type.Name}：同一成员可挂多个分组数组。");
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True, type.Name);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.True, $"{type.Name}：类级分组要能分发。");
        }

        #endregion
    }
}
