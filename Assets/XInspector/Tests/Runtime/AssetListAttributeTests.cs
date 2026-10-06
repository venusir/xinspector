using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[AssetList]</c> 的默认值、用法约束与「只声明支持的选项」。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AssetListAttributeTests
    {
        #region 默认值

        /// <summary>两个选项默认都不设——即「整个工程、不额外过滤名字」。</summary>
        [Test]
        public void 默认值()
        {
            var attribute = new AssetListAttribute();

            Assert.That(attribute.Path, Is.Null);
            Assert.That(attribute.AssetNamePrefix, Is.Null);
        }

        /// <summary>两个选项都可具名赋值（官方样例的写法）。</summary>
        [Test]
        public void 选项可具名赋值()
        {
            var attribute = new AssetListAttribute
            {
                Path = "/Plugins/Sirenix/|Assets/Art",
                AssetNamePrefix = "Rock",
            };

            Assert.That(attribute.Path, Is.EqualTo("/Plugins/Sirenix/|Assets/Art"));
            Assert.That(attribute.AssetNamePrefix, Is.EqualTo("Rock"));
        }

        #endregion

        #region 与 Odin 的表面差异

        /// <summary>
        /// <b>只声明本包支持的那两个选项。</b> 官方那四个各有明确理由：
        /// <c>AutoPopulate</c>（绘制即搜工程 + 绘制即改数据）、<c>Tags</c> / <c>LayerNames</c>
        /// （说的是 GameObject 的标签与层，与 <c>AssetDatabase</c> 的资产标签不是一回事）、
        /// <c>CustomFilterMethod</c>（resolved string：方法一律不做）。
        /// </summary>
        /// <remarks>
        /// 这条守卫的用意：哪天有人「顺手补齐 Odin 的字段」，先被这里问一次。
        /// </remarks>
        [Test]
        public void 只声明支持的选项()
        {
            var declared = typeof(AssetListAttribute)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            var expected = new[] { "AssetNamePrefix", "Path" };

            CollectionAssert.AreEqual(expected, declared, "公开属性集合变了——先想清楚新加的那个有没有真行为。");
        }

        /// <summary>用于成员（字段与属性）、不可重复、可继承。</summary>
        [Test]
        public void 仅成员且不可重复()
        {
            var usage = typeof(AssetListAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Property), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }

        #endregion
    }
}
