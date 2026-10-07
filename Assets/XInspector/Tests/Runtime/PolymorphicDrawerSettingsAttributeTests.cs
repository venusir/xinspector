using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[PolymorphicDrawerSettings]</c> 与 <c>NonDefaultConstructorPreference</c> 的构造行为与用法约束。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// 「行怎么画、菜单怎么建、实例怎么造」的判据在编辑器侧，不在本文件。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PolymorphicDrawerSettingsAttributeTests
    {
        #region NonDefaultConstructorPreference

        /// <summary>只有这 4 个成员、数值 0..3，一个不多一个不少——**不发明第 5 个**。</summary>
        [Test]
        public void NonDefaultConstructorPreference成员与数值()
        {
            var names = Enum.GetNames(typeof(NonDefaultConstructorPreference));

            Assert.That(names, Is.EquivalentTo(new[]
            {
                "ConstructIdeal", "Exclude", "LogWarning", "PreferUninitialized",
            }));
            Assert.That(names.Length, Is.EqualTo(4), "官方是 4 个成员。");

            Assert.That((int)NonDefaultConstructorPreference.ConstructIdeal, Is.EqualTo(0), "默认档是 0。");
            Assert.That((int)NonDefaultConstructorPreference.Exclude, Is.EqualTo(1));
            Assert.That((int)NonDefaultConstructorPreference.LogWarning, Is.EqualTo(2));
            Assert.That((int)NonDefaultConstructorPreference.PreferUninitialized, Is.EqualTo(3));
        }

        /// <summary>不是位标志——四档互斥，一次只取一个。</summary>
        [Test]
        public void NonDefaultConstructorPreference不是位标志()
        {
            Assert.That(
                typeof(NonDefaultConstructorPreference).IsDefined(typeof(FlagsAttribute), false),
                Is.False);
        }

        #endregion

        #region 特性

        /// <summary>
        /// 用法声明**只到字段**：本包的属性只有经 <c>[ShowInInspector]</c> 的只读反射路径才进树，
        /// 而本特性存在的意义是**换类型**——标在属性上必然是「编译得过但什么都不发生」。
        /// </summary>
        [Test]
        public void 用法声明只到字段()
        {
            var usage = typeof(PolymorphicDrawerSettingsAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null, "特性必须声明 AttributeUsage。");
            Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Field), "只标字段。");
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.True);
        }

        /// <summary>三个旋钮的默认值：两个开关默认关、构造偏好默认 <c>ConstructIdeal</c>。</summary>
        [Test]
        public void 三个旋钮的默认值()
        {
            var attribute = new PolymorphicDrawerSettingsAttribute();

            Assert.That(attribute.ReadOnlyIfNotNullReference, Is.False);
            Assert.That(attribute.ShowBaseType, Is.False, "默认与原生那一行只显示类型名对齐。");
            Assert.That(
                attribute.NonDefaultConstructorPreference,
                Is.EqualTo(NonDefaultConstructorPreference.ConstructIdeal));

            attribute.ReadOnlyIfNotNullReference = true;
            attribute.ShowBaseType = true;
            attribute.NonDefaultConstructorPreference = NonDefaultConstructorPreference.PreferUninitialized;

            Assert.That(attribute.ReadOnlyIfNotNullReference, Is.True);
            Assert.That(attribute.ShowBaseType, Is.True);
            Assert.That(
                attribute.NonDefaultConstructorPreference,
                Is.EqualTo(NonDefaultConstructorPreference.PreferUninitialized));
        }

        /// <summary>
        /// 三个旋钮按**官方形状**：<c>ReadOnlyIfNotNullReference</c> 是 public 字段，
        /// 另两个是 public 属性（2026-10-07 从官网逐字核过）——被「顺手统一成一种」就与官方形状不符了。
        /// </summary>
        [Test]
        public void 三个旋钮按官方形状声明()
        {
            var type = typeof(PolymorphicDrawerSettingsAttribute);

            Assert.That(type.GetField("ReadOnlyIfNotNullReference"), Is.Not.Null, "官方是 public 字段。");
            Assert.That(type.GetProperty("ReadOnlyIfNotNullReference"), Is.Null);

            Assert.That(type.GetProperty("ShowBaseType"), Is.Not.Null, "官方是 public 属性。");
            Assert.That(type.GetField("ShowBaseType"), Is.Null);

            Assert.That(type.GetProperty("NonDefaultConstructorPreference"), Is.Not.Null);
            Assert.That(type.GetField("NonDefaultConstructorPreference"), Is.Null);
        }

        /// <summary>
        /// <c>CreateInstanceFunction</c> 按**官方形状**声明：<c>public string</c> **字段**、默认 <c>null</c>
        /// （第三十批与单参解名通道一起落地）。官方的只读 <c>IsSet</c> 一族同样不声明。
        /// </summary>
        [Test]
        public void CreateInstanceFunction按官方形状声明()
        {
            var type = typeof(PolymorphicDrawerSettingsAttribute);
            var field = type.GetField("CreateInstanceFunction");

            Assert.That(field, Is.Not.Null, "官方是 public 字段。");
            Assert.That(type.GetProperty("CreateInstanceFunction"), Is.Null);
            Assert.That(
                field.GetValue(new PolymorphicDrawerSettingsAttribute()),
                Is.Null,
                "不写函数就是 null（走内置的造实例）。");

            Assert.That(type.GetProperty("ShowBaseTypeIsSet"), Is.Null);
            Assert.That(type.GetProperty("NonDefaultConstructorPreferenceIsSet"), Is.Null);
        }

        #endregion
    }
}
