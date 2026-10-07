using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[TypeSelectorSettings]</c> 的用法约束与形状：只标字段、旋钮按官方形状（一个字段 + 三个属性）、
    /// 默认全为 <c>true</c>、常量与官方一致。
    /// <para>不依赖 Unity；「过滤器怎么解析、菜单怎么变」的判据在编辑器侧，不在本文件。</para>
    /// </summary>
    [TestFixture]
    public class TypeSelectorSettingsAttributeTests
    {
        /// <summary>
        /// 用法声明**只到字段**：它配的是本包自绘的选择器，而两个选择器特性都只长在字段上——
        /// 标在属性上必然是「编译得过但什么都不发生」。
        /// </summary>
        [Test]
        public void 用法声明只到字段()
        {
            var usage = typeof(TypeSelectorSettingsAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null, "特性必须声明 AttributeUsage。");
            Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Field), "只标字段。");
        }

        /// <summary>多次标注无意义；继承照常（与三个姊妹特性逐字一致）。</summary>
        [Test]
        public void 标注次数与继承()
        {
            var usage = typeof(TypeSelectorSettingsAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.True);
        }

        /// <summary>
        /// <c>FilterTypesFunction</c> 按**官方形状**是 <c>public</c> **字段**——被「顺手统一成属性」
        /// 就与官方形状不符了。
        /// </summary>
        [Test]
        public void 过滤函数按官方形状是字段()
        {
            var type = typeof(TypeSelectorSettingsAttribute);

            Assert.That(type.GetField("FilterTypesFunction"), Is.Not.Null, "官方是 public 字段。");
            Assert.That(type.GetProperty("FilterTypesFunction"), Is.Null);
        }

        /// <summary>三个显示旋钮按**官方形状**都是 <c>public</c> **属性**。</summary>
        [Test]
        public void 三个显示旋钮按官方形状是属性()
        {
            var type = typeof(TypeSelectorSettingsAttribute);

            foreach (var name in new[] { "PreferNamespaces", "ShowCategories", "ShowNoneItem" })
            {
                Assert.That(type.GetProperty(name), Is.Not.Null, $"{name} 官方是 public 属性。");
                Assert.That(type.GetField(name), Is.Null, $"{name} 不该是字段。");
            }
        }

        /// <summary>三个旋钮默认全 <c>true</c>（＝基座既有行为），且可写入读回。</summary>
        [Test]
        public void 三个旋钮的默认值与可写性()
        {
            var attribute = new TypeSelectorSettingsAttribute();

            Assert.That(attribute.FilterTypesFunction, Is.Null, "不写过滤器就是不过滤。");
            Assert.That(attribute.PreferNamespaces, Is.True, "默认命名空间分层（与 Odin 的默认档相反）。");
            Assert.That(attribute.ShowCategories, Is.True);
            Assert.That(attribute.ShowNoneItem, Is.True);

            attribute.FilterTypesFunction = "Allowed";
            attribute.PreferNamespaces = false;
            attribute.ShowCategories = false;
            attribute.ShowNoneItem = false;

            Assert.That(attribute.FilterTypesFunction, Is.EqualTo("Allowed"));
            Assert.That(attribute.PreferNamespaces, Is.False);
            Assert.That(attribute.ShowCategories, Is.False);
            Assert.That(attribute.ShowNoneItem, Is.False);
        }

        /// <summary>形参名的官方约定常量逐字保留（解析失败文案引用它；官方改口径时这条先红）。</summary>
        [Test]
        public void 形参名常量与官方一致()
        {
            Assert.That(
                TypeSelectorSettingsAttribute.FILTER_TYPES_FUNCTION_NAMED_VALUE,
                Is.EqualTo("type"));
        }
    }
}
