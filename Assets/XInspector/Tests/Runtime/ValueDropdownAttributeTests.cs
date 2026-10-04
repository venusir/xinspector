using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[ValueDropdown]</c> 的构造、选项默认值与用法约束。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ValueDropdownAttributeTests
    {
        #region 构造与默认值

        /// <summary>唯一的构造参数是来源成员名；四个选项默认全关。</summary>
        [Test]
        public void 构造与默认值()
        {
            var attribute = new ValueDropdownAttribute("options");

            Assert.That(attribute.ValuesGetter, Is.EqualTo("options"));
            Assert.That(attribute.AppendNextDrawer, Is.False);
            Assert.That(attribute.DisableGUIInAppendedDrawer, Is.False);
            Assert.That(attribute.FlattenTreeView, Is.False, "与官方一致：默认是树形。");
            Assert.That(attribute.SortDropdownItems, Is.False);
        }

        /// <summary>四个选项都可具名赋值。</summary>
        [Test]
        public void 选项可具名赋值()
        {
            var attribute = new ValueDropdownAttribute("options")
            {
                AppendNextDrawer = true,
                DisableGUIInAppendedDrawer = true,
                FlattenTreeView = true,
                SortDropdownItems = true,
            };

            Assert.That(attribute.AppendNextDrawer, Is.True);
            Assert.That(attribute.DisableGUIInAppendedDrawer, Is.True);
            Assert.That(attribute.FlattenTreeView, Is.True);
            Assert.That(attribute.SortDropdownItems, Is.True);
        }

        #endregion

        #region 与 Odin 的表面差异

        /// <summary>
        /// <b>只声明实例里支持的那几个选项。</b> Odin 的其余选项要么只对列表有意义
        /// （本包不支持数组形态），要么依赖它自建的弹出层（搜索框、双击确认、标题、尺寸）；
        /// 声明成静默 no-op 正是本包最想避免的现象——**编译不过才是响的**。
        /// </summary>
        /// <remarks>
        /// 这条守卫的用意：哪天有人「顺手补齐 Odin 的字段」，先被这里问一次。
        /// </remarks>
        [Test]
        public void 只声明支持的选项()
        {
            // DeclaredOnly 必不可少：System.Attribute 自带一个公开的 TypeId。
            var declared = typeof(ValueDropdownAttribute)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            var expected = new[]
            {
                "AppendNextDrawer",
                "DisableGUIInAppendedDrawer",
                "FlattenTreeView",
                "SortDropdownItems",
                "ValuesGetter",
            };

            CollectionAssert.AreEqual(expected, declared, "公开属性集合变了——先想清楚新加的那个有没有真行为。");
        }

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void 仅成员且不可重复()
        {
            var usage = typeof(ValueDropdownAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }

        #endregion
    }
}
