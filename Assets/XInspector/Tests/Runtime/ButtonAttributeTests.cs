using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// 按钮族四个特性（<c>[Button]</c>、<c>[ButtonGroup]</c>、<c>[ResponsiveButtonGroup]</c>、
    /// <c>[InlineButton]</c>）与 <c>ButtonSizes</c> 的构造行为与用法约束。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ButtonAttributeTests
    {
        #region Button

        /// <summary>只用于方法、不可重复、**不继承**——三条都照抄官方签名。</summary>
        [Test]
        public void Button只标在方法上且不继承()
        {
            var usage = UsageOf(typeof(ButtonAttribute));

            Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Method), "官方是 All，但本包刻意收窄到方法。");
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.False, "官方就是 false：覆写方法不继承特性。");
        }

        /// <summary>默认中号、无自定义文本。</summary>
        [Test]
        public void Button默认值()
        {
            var button = new ButtonAttribute();

            Assert.That(button.Name, Is.Null, "没给文本时沿用方法名。");
            Assert.That(button.Size, Is.EqualTo(ButtonSizes.Medium));
        }

        /// <summary>三组构造参数各就各位。</summary>
        [Test]
        public void Button构造重载()
        {
            Assert.That(new ButtonAttribute("执行").Name, Is.EqualTo("执行"));
            Assert.That(new ButtonAttribute("执行").Size, Is.EqualTo(ButtonSizes.Medium), "只给名字时高度仍是默认。");

            Assert.That(new ButtonAttribute(ButtonSizes.Gigantic).Name, Is.Null);
            Assert.That(new ButtonAttribute(ButtonSizes.Gigantic).Size, Is.EqualTo(ButtonSizes.Gigantic));

            var both = new ButtonAttribute("执行", ButtonSizes.Small);
            Assert.That(both.Name, Is.EqualTo("执行"));
            Assert.That(both.Size, Is.EqualTo(ButtonSizes.Small));
        }

        /// <summary>空白文本照原样存下——「没给」的判据是空白，由绘制侧统一处理，构造期不报错。</summary>
        [Test]
        public void Button空白文本不报错()
        {
            Assert.That(new ButtonAttribute("  ").Name, Is.EqualTo("  "));
        }

        /// <summary>
        /// 高度表的成员名照抄官方、数值是本包自定，且**默认成员排 0**——
        /// 于是 <c>default</c> 就是本包的默认，不会出现「没设过却拿到 Small」。
        /// </summary>
        [Test]
        public void ButtonSizes默认成员排零()
        {
            Assert.That((int)ButtonSizes.Medium, Is.EqualTo(0));
            Assert.That(default(ButtonSizes), Is.EqualTo(ButtonSizes.Medium));
            Assert.That(Enum.GetNames(typeof(ButtonSizes)),
                Is.EquivalentTo(new[] { "Medium", "Small", "Large", "Gigantic" }),
                "成员名取自官方，改动是破坏性变更。");
        }

        #endregion

        #region 按钮分组

        /// <summary>两个分组特性都只标在方法上、可重复、继承。</summary>
        [Test]
        public void 按钮分组只标在方法上()
        {
            foreach (var type in new[] { typeof(ButtonGroupAttribute), typeof(ResponsiveButtonGroupAttribute) })
            {
                var usage = UsageOf(type);

                Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Method), type.Name);
                Assert.That(usage.AllowMultiple, Is.True, type.Name);
                Assert.That(usage.Inherited, Is.True, type.Name);
                Assert.That(typeof(PropertyGroupAttribute).IsAssignableFrom(type), Is.True, $"{type.Name} 应是分组特性。");
            }
        }

        /// <summary>不写组名时的默认组名照抄官方——自造一个会平白多一处差异。</summary>
        [Test]
        public void 分组默认组名照抄官方()
        {
            Assert.That(((PropertyGroupAttribute)new ButtonGroupAttribute()).GroupID, Is.EqualTo("_DefaultGroup"));
            Assert.That(
                ((PropertyGroupAttribute)new ResponsiveButtonGroupAttribute()).GroupID,
                Is.EqualTo("_DefaultResponsiveButtonGroup"));
        }

        /// <summary>组名走的是分组路径规范化：逐段去空白、丢空段。</summary>
        [Test]
        public void 分组组名被规范化()
        {
            Assert.That(new ButtonGroupAttribute(" 危险 / 批量 ").GroupID, Is.EqualTo("危险/批量"));
            Assert.Throws<ArgumentException>(() => new ButtonGroupAttribute("   "));
        }

        /// <summary>按钮组的高度取**先出现的非零值**，不做累加、也不是后者覆盖。</summary>
        [Test]
        public void 按钮组合并取先出现的非零高度()
        {
            var first = new ButtonGroupAttribute("G");
            var second = new ButtonGroupAttribute("G") { ButtonHeight = 30 };

            ((PropertyGroupAttribute)first).Combine(second);
            Assert.That(first.ButtonHeight, Is.EqualTo(30), "先出现的是 0（未设），该被非零值补上。");

            var third = new ButtonGroupAttribute("G") { ButtonHeight = 40 };
            ((PropertyGroupAttribute)third).Combine(second);
            Assert.That(third.ButtonHeight, Is.EqualTo(40), "先出现的就是非零，后来的不覆盖它。");
        }

        /// <summary>等宽开关取逻辑或：组里任一成员要求即成立，不依赖声明顺序。</summary>
        [Test]
        public void 响应式分组等宽取逻辑或()
        {
            var plain = new ResponsiveButtonGroupAttribute("G");
            var uniform = new ResponsiveButtonGroupAttribute("G") { UniformLayout = true };

            ((PropertyGroupAttribute)plain).Combine(uniform);
            Assert.That(plain.UniformLayout, Is.True);

            var alsoPlain = new ResponsiveButtonGroupAttribute("G");
            ((PropertyGroupAttribute)uniform).Combine(alsoPlain);
            Assert.That(uniform.UniformLayout, Is.True, "已经为真就不会被拉回假。");
        }

        /// <summary>默认高度取先出现的非默认值；显式写 Medium 与不写等效（结果相同）。</summary>
        [Test]
        public void 响应式分组默认高度取非默认值()
        {
            var medium = new ResponsiveButtonGroupAttribute("G");
            var large = new ResponsiveButtonGroupAttribute("G") { DefaultButtonSize = ButtonSizes.Large };

            Assert.That(medium.DefaultButtonSize, Is.EqualTo(ButtonSizes.Medium), "默认就是中号。");

            ((PropertyGroupAttribute)medium).Combine(large);
            Assert.That(medium.DefaultButtonSize, Is.EqualTo(ButtonSizes.Large));
        }

        #endregion

        #region 行内按钮

        /// <summary>可重复（同一字段多个按钮）、继承；只用于成员。</summary>
        [Test]
        public void InlineButton可重复且只用于成员()
        {
            var usage = UsageOf(typeof(InlineButtonAttribute));

            Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Field | AttributeTargets.Property));
            Assert.That(usage.AllowMultiple, Is.True, "同一字段可以挂多个行内按钮。");
            Assert.That(usage.Inherited, Is.True);
        }

        /// <summary>方法名不能为空——空名字的按钮点了必然什么都没发生，构造期就该响。</summary>
        [Test]
        public void InlineButton方法名不能为空()
        {
            Assert.Throws<ArgumentException>(() => new InlineButtonAttribute(null));
            Assert.Throws<ArgumentException>(() => new InlineButtonAttribute("   "));

            Assert.That(new InlineButtonAttribute(" Randomize ").MethodName, Is.EqualTo("Randomize"), "首尾空白被去掉。");
            Assert.That(new InlineButtonAttribute("Randomize").Label, Is.Null, "没给文本时沿用方法名。");
            Assert.That(new InlineButtonAttribute("Randomize", "随机").Label, Is.EqualTo("随机"));
        }

        #endregion

        #region Private Helpers

        /// <summary>取特性的 <see cref="AttributeUsageAttribute"/>，顺手断言它确实声明了。</summary>
        /// <param name="type">特性类型。</param>
        /// <returns>它的用法声明。</returns>
        private static AttributeUsageAttribute UsageOf(Type type)
        {
            var usage = type.GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null, type.Name);
            return usage;
        }

        #endregion
    }
}
