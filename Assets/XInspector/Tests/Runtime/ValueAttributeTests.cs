using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// 值绘制四特性的构造行为与用法约束。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ValueAttributeTests
    {
        #region [DisplayAsString]

        /// <summary>无参构造默认不溢出（裁到一行）。</summary>
        [Test]
        public void DisplayAsString_默认不溢出()
        {
            Assert.That(new DisplayAsStringAttribute().Overflow, Is.False);
        }

        /// <summary>溢出开关被原样保留。</summary>
        [Test]
        public void DisplayAsString_可指定溢出()
        {
            Assert.That(new DisplayAsStringAttribute(true).Overflow, Is.True);
            Assert.That(new DisplayAsStringAttribute(false).Overflow, Is.False);
        }

        /// <summary>
        /// 重载集合与官方一致（14 个里除去 8 个带 <c>TextAlignment</c> 的）。
        /// </summary>
        /// <remarks>
        /// 官网 2026-10-07 核过：`()`、`(bool)`、`(int)`、`(bool, int)`、`(int, bool)`、
        /// `(bool, int, bool)` 正是全部不带 Unity 类型的重载。
        /// 守卫按**参数形状的集合**比，而不是构造器个数——将来加一个别的形状时照样红。
        /// </remarks>
        [Test]
        public void DisplayAsString_重载集合与官方一致()
        {
            var shapes = new System.Collections.Generic.List<string>();

            foreach (var constructor in typeof(DisplayAsStringAttribute).GetConstructors())
            {
                shapes.Add(string.Join(",", Array.ConvertAll(constructor.GetParameters(), p => p.ParameterType.Name)));
            }

            shapes.Sort(StringComparer.Ordinal);

            Assert.That(
                shapes,
                Is.EqualTo(new System.Collections.Generic.List<string>
                {
                    string.Empty,
                    "Boolean",
                    "Boolean,Int32",
                    "Boolean,Int32,Boolean",
                    "Int32",
                    "Int32,Boolean",
                }),
                "多一个少一个都要红——带 TextAlignment 的那 8 个永久不做（Runtime 零 Unity 依赖）。");
        }

        /// <summary>三个选项各自进属性，且都有默认值。</summary>
        [Test]
        public void DisplayAsString_字号与富文本进属性()
        {
            var plain = new DisplayAsStringAttribute();
            Assert.That(plain.FontSize, Is.EqualTo(0), "0 ＝ 编辑器默认字号。");
            Assert.That(plain.EnableRichText, Is.False);

            var sized = new DisplayAsStringAttribute(20);
            Assert.That(sized.FontSize, Is.EqualTo(20));
            Assert.That(sized.Overflow, Is.False);

            var full = new DisplayAsStringAttribute(true, 20, true);
            Assert.That(full.Overflow, Is.True);
            Assert.That(full.FontSize, Is.EqualTo(20));
            Assert.That(full.EnableRichText, Is.True);

            Assert.That(new DisplayAsStringAttribute(20, true).EnableRichText, Is.True);
            Assert.That(new DisplayAsStringAttribute(false, 20).Overflow, Is.False);
        }

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void DisplayAsString_仅成员且不可重复()
        {
            var usage = typeof(DisplayAsStringAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }

        #endregion

        #region [ToggleLeft]

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void ToggleLeft_仅成员且不可重复()
        {
            var usage = typeof(ToggleLeftAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }

        #endregion

        #region [ProgressBar]

        /// <summary>范围与颜色被原样保留；颜色有默认值。</summary>
        [Test]
        public void ProgressBar_保留范围与颜色()
        {
            var attribute = new ProgressBarAttribute(0, 100);

            Assert.That(attribute.Min, Is.EqualTo(0));
            Assert.That(attribute.Max, Is.EqualTo(100));
            Assert.That(attribute.R, Is.EqualTo(0.15f));
            Assert.That(attribute.G, Is.EqualTo(0.47f));
            Assert.That(attribute.B, Is.EqualTo(0.74f));

            var custom = new ProgressBarAttribute(-1, 1, 0.5f, 0.6f, 0.7f);
            Assert.That(custom.R, Is.EqualTo(0.5f));
            Assert.That(custom.G, Is.EqualTo(0.6f));
            Assert.That(custom.B, Is.EqualTo(0.7f));
        }

        /// <summary>
        /// 上限不大于下限时构造期抛异常。
        /// <para>
        /// 范围反过来几乎必然是笔误（不支持反向进度条），而它的表现是「条子画得莫名其妙」——
        /// 与其画出一个说不清的东西，不如在构建期明确报错。
        /// </para>
        /// </summary>
        [Test]
        public void ProgressBar_范围倒置时构造期报错()
        {
            Assert.Throws<ArgumentException>(() => new ProgressBarAttribute(100, 0));
            Assert.Throws<ArgumentException>(() => new ProgressBarAttribute(5, 5));
        }

        /// <summary>可选具名参数的默认值。</summary>
        [Test]
        public void ProgressBar_可选参数的默认值()
        {
            var attribute = new ProgressBarAttribute(0, 1);

            Assert.That(attribute.Height, Is.EqualTo(0f), "0 表示用默认高度。");
            Assert.That(attribute.Segmented, Is.False);
            Assert.That(attribute.DrawValueLabel, Is.True, "默认显示数值——不显示时拖动前不知道值是多少。");
        }

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void ProgressBar_仅成员且不可重复()
        {
            var usage = typeof(ProgressBarAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }

        #endregion

        #region [EnumToggleButtons]

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void EnumToggleButtons_仅成员且不可重复()
        {
            var usage = typeof(EnumToggleButtonsAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }

        #endregion
    }
}
