using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// 布局与外观六特性的构造行为与用法约束。
    /// <para>
    /// 这些断言不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class LayoutAttributeTests
    {
        #region [GUIColor]

        /// <summary>未给 alpha 时默认不透明。</summary>
        [Test]
        public void GUIColor_默认不透明()
        {
            var attribute = new GUIColorAttribute(1f, 0.5f, 0.25f);

            Assert.That(attribute.R, Is.EqualTo(1f));
            Assert.That(attribute.G, Is.EqualTo(0.5f));
            Assert.That(attribute.B, Is.EqualTo(0.25f));
            Assert.That(attribute.A, Is.EqualTo(1f));
        }

        /// <summary>四个分量都被原样保留。</summary>
        [Test]
        public void GUIColor_保留四个分量()
        {
            var attribute = new GUIColorAttribute(0.1f, 0.2f, 0.3f, 0.4f);

            Assert.That(attribute.A, Is.EqualTo(0.4f));
        }

        #endregion

        #region [LabelWidth] / [HideLabel]

        /// <summary>宽度被原样保留（负值也照留——它等价于「交还默认」）。</summary>
        [Test]
        public void LabelWidth_保留宽度()
        {
            Assert.That(new LabelWidthAttribute(200f).Width, Is.EqualTo(200f));
            Assert.That(new LabelWidthAttribute(-1f).Width, Is.EqualTo(-1f));
        }

        /// <summary>无标签特性可用于成员且不可重复。</summary>
        [Test]
        public void HideLabel_仅用于成员且不可重复()
        {
            var usage = typeof(HideLabelAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }

        #endregion

        #region [PropertySpace]

        /// <summary>无参构造是前后各 8 像素。</summary>
        [Test]
        public void PropertySpace_无参为前后各八像素()
        {
            var attribute = new PropertySpaceAttribute();

            Assert.That(attribute.SpaceBefore, Is.EqualTo(8f));
            Assert.That(attribute.SpaceAfter, Is.EqualTo(8f));
        }

        /// <summary>单参构造只设前置，后置保持 0——与官方参数名 <c>spaceBefore</c> 一致。</summary>
        [Test]
        public void PropertySpace_单参只设前置()
        {
            var attribute = new PropertySpaceAttribute(20f);

            Assert.That(attribute.SpaceBefore, Is.EqualTo(20f));
            Assert.That(attribute.SpaceAfter, Is.EqualTo(0f));
        }

        /// <summary>双参构造两个都设。</summary>
        [Test]
        public void PropertySpace_双参都设()
        {
            var attribute = new PropertySpaceAttribute(20f, 30f);

            Assert.That(attribute.SpaceBefore, Is.EqualTo(20f));
            Assert.That(attribute.SpaceAfter, Is.EqualTo(30f));
        }

        /// <summary>两个间距可具名赋值——官方示例的写法。</summary>
        [Test]
        public void PropertySpace_具名赋值()
        {
            var attribute = new PropertySpaceAttribute
            {
                SpaceBefore = 30f,
                SpaceAfter = 60f,
            };

            Assert.That(attribute.SpaceBefore, Is.EqualTo(30f));
            Assert.That(attribute.SpaceAfter, Is.EqualTo(60f));
        }

        #endregion

        #region [Indent]

        /// <summary>默认缩进一级。</summary>
        [Test]
        public void Indent_默认一级()
        {
            Assert.That(new IndentAttribute().IndentLevel, Is.EqualTo(1));
        }

        /// <summary>级数可指定，负值表示反向缩进。</summary>
        [Test]
        public void Indent_级数可指定()
        {
            Assert.That(new IndentAttribute(3).IndentLevel, Is.EqualTo(3));
            Assert.That(new IndentAttribute(-1).IndentLevel, Is.EqualTo(-1));
        }

        /// <summary>
        /// 允许重复标注：多个 <c>[Indent]</c> 各加一级。
        /// <para>
        /// 链按**特性实例**配对，所以两个实例就是两格、各缩进一级——这条约束若被改成
        /// 不可重复，叠加语义就没了，故在此钉住。
        /// </para>
        /// </summary>
        [Test]
        public void Indent_允许重复标注()
        {
            var usage = typeof(IndentAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.True);
        }

        #endregion

        #region [SuffixLabel]

        /// <summary>文本与 overlay 被原样保留，overlay 默认关闭。</summary>
        [Test]
        public void SuffixLabel_保留文本与overlay()
        {
            var plain = new SuffixLabelAttribute("秒");
            var overlay = new SuffixLabelAttribute("叠着", true);

            Assert.That(plain.Label, Is.EqualTo("秒"));
            Assert.That(plain.Overlay, Is.False);
            Assert.That(overlay.Overlay, Is.True);
        }

        /// <summary>空白后缀在构造时报错。</summary>
        /// <param name="label">无效的后缀文本。</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void SuffixLabel_空白文本抛异常(string label)
        {
            Assert.Throws<ArgumentException>(() => new SuffixLabelAttribute(label));
        }

        /// <summary>
        /// 不可重复标注——这是与官方的一处**刻意差异**（Odin 允许）。
        /// 多个后缀在右侧争同一块宽度没有明确语义，与其发明规则不如让它编译不过。
        /// </summary>
        [Test]
        public void SuffixLabel_不可重复标注()
        {
            var usage = typeof(SuffixLabelAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
        }

        #endregion
    }
}
