using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// 结构与门控五特性的构造行为与用法约束。
    /// <para>不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。</para>
    /// </summary>
    [TestFixture]
    public class MiscAttributeTests
    {
        #region [EnableGUI] / [DrawWithUnity] / [ChildGameObjectsOnly]

        /// <summary>三个成员级特性都仅用于成员、不可重复。</summary>
        [Test]
        public void 成员级特性_仅成员且不可重复()
        {
            foreach (var type in new[] { typeof(EnableGUIAttribute), typeof(DrawWithUnityAttribute), typeof(ChildGameObjectsOnlyAttribute) })
            {
                var usage = type.GetCustomAttribute<AttributeUsageAttribute>();

                Assert.That(usage, Is.Not.Null, type.Name);
                Assert.That(usage.AllowMultiple, Is.False, type.Name);
                Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True, type.Name);
                Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False, type.Name);
            }
        }

        /// <summary>可选开关的默认值都是「不放开」。</summary>
        [Test]
        public void 可选开关的默认值()
        {
            var childOnly = new ChildGameObjectsOnlyAttribute();
            Assert.That(childOnly.IncludeInactive, Is.False);
            Assert.That(childOnly.IncludeSelf, Is.False);

            Assert.That(new DrawWithUnityAttribute().PreferImGUI, Is.False,
                "该字段不产生行为，但默认值照官方保留。");
        }

        #endregion

        #region [TypeInfoBox] / [HideMonoScript]

        /// <summary>两个类级特性都只用于类，且 TypeInfoBox 可重复、HideMonoScript 不可。</summary>
        [Test]
        public void 类级特性_仅类()
        {
            var infoBox = typeof(TypeInfoBoxAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.That(infoBox, Is.Not.Null);
            Assert.That(infoBox.AllowMultiple, Is.True, "类上可以放多条信息框。");
            Assert.That(infoBox.ValidOn.HasFlag(AttributeTargets.Class), Is.True);
            Assert.That(infoBox.ValidOn.HasFlag(AttributeTargets.Field), Is.False);

            var hide = typeof(HideMonoScriptAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.That(hide, Is.Not.Null);
            Assert.That(hide.AllowMultiple, Is.False);
            Assert.That(hide.ValidOn.HasFlag(AttributeTargets.Class), Is.True);
            Assert.That(hide.ValidOn.HasFlag(AttributeTargets.Struct), Is.False);
        }

        /// <summary>空白消息构造期报错（与 [InfoBox] 同一条规则）。</summary>
        [Test]
        public void TypeInfoBox_空白消息构造期报错()
        {
            Assert.Throws<ArgumentException>(() => new TypeInfoBoxAttribute(""));
            Assert.Throws<ArgumentException>(() => new TypeInfoBoxAttribute("   "));
            Assert.Throws<ArgumentException>(() => new TypeInfoBoxAttribute(null));
        }

        /// <summary>消息被原样保留。</summary>
        [Test]
        public void TypeInfoBox_保留消息()
        {
            Assert.That(new TypeInfoBoxAttribute("说明").Message, Is.EqualTo("说明"));
        }

        #endregion
    }
}
