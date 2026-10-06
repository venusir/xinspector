using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[ColorPalette]</c> 的公开形状。不依赖 Unity，同时跑在离线通道里。
    /// </summary>
    [TestFixture]
    public class ColorPaletteAttributeTests
    {
        /// <summary>两个形态各一个构造：无参（工程里唯一那份）与具名。</summary>
        [Test]
        public void 两个构造且只声明一个属性()
        {
            var constructors = typeof(ColorPaletteAttribute).GetConstructors();

            Assert.That(constructors.Length, Is.EqualTo(2));
            Assert.That(
                Array.Exists(constructors, c => c.GetParameters().Length == 0),
                Is.True,
                "无参形态（工程里唯一的那份调色板）。");
            Assert.That(
                Array.Exists(
                    constructors,
                    c => c.GetParameters().Length == 1 &&
                         c.GetParameters()[0].ParameterType == typeof(string)),
                Is.True,
                "具名形态。");

            // 官方这两个构造都是字面量，本包也多一个旋钮都没有——写了会编译不过，而不是静默无效。
            Assert.That(
                typeof(ColorPaletteAttribute).GetProperties(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                Has.Length.EqualTo(1),
                "只有 PaletteName。");
        }

        /// <summary>无参形态的名是 <c>null</c>——那正是「用唯一那份」的判据。</summary>
        [Test]
        public void 无参形态的名字为空()
        {
            Assert.That(new ColorPaletteAttribute().PaletteName, Is.Null);
        }

        /// <summary>具名形态把名字原样收下（不 trim、不规范化——判据在查找那一侧）。</summary>
        [Test]
        public void 具名形态收下名字()
        {
            Assert.That(new ColorPaletteAttribute("UI").PaletteName, Is.EqualTo("UI"));
        }

        /// <summary>只标在成员上、不可重复（一个字段一行色块）。</summary>
        [Test]
        public void 用于成员且不可重复()
        {
            var usage = typeof(ColorPaletteAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Property), Is.True);
            Assert.That(
                usage.ValidOn.HasFlag(AttributeTargets.Class),
                Is.False,
                "类级形态不做。");
        }
    }
}
