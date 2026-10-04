using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[AssetSelector]</c> 的默认值、用法约束与「只声明支持的选项」。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AssetSelectorAttributeTests
    {
        #region 默认值

        /// <summary>三个选项默认都不设——即「整个工程、不额外过滤、树形」。</summary>
        [Test]
        public void 默认值()
        {
            var attribute = new AssetSelectorAttribute();

            Assert.That(attribute.Paths, Is.Null);
            Assert.That(attribute.Filter, Is.Null);
            Assert.That(attribute.FlattenTreeView, Is.False, "与官方一致：默认是树形。");
        }

        /// <summary>三个选项都可具名赋值。</summary>
        [Test]
        public void 选项可具名赋值()
        {
            var attribute = new AssetSelectorAttribute
            {
                Paths = "Assets/A|Assets/B",
                Filter = "t:Material",
                FlattenTreeView = true,
            };

            Assert.That(attribute.Paths, Is.EqualTo("Assets/A|Assets/B"));
            Assert.That(attribute.Filter, Is.EqualTo("t:Material"));
            Assert.That(attribute.FlattenTreeView, Is.True);
        }

        #endregion

        #region 与 Odin 的表面差异

        /// <summary>
        /// <b>只声明实例里支持的那几个选项。</b> Odin 的其余选项要么只对列表有意义
        /// （本包不支持数组形态），要么依赖它自建的弹出层（标题、尺寸等）。
        /// </summary>
        /// <remarks>
        /// 这条守卫的用意：哪天有人「顺手补齐 Odin 的字段」，先被这里问一次。
        /// </remarks>
        [Test]
        public void 只声明支持的选项()
        {
            var declared = typeof(AssetSelectorAttribute)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            var expected = new[] { "Filter", "FlattenTreeView", "Paths" };

            CollectionAssert.AreEqual(expected, declared, "公开属性集合变了——先想清楚新加的那个有没有真行为。");
        }

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void 仅成员且不可重复()
        {
            var usage = typeof(AssetSelectorAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }

        #endregion
    }
}
