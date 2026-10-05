using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[ListDrawerSettings]</c> 的旋钮默认值与用法约束。不依赖 Unity，同时跑在离线通道里。
    /// </summary>
    [TestFixture]
    public class ListDrawerSettingsAttributeTests
    {
        /// <summary>五个旋钮的默认值：增删可见、可折叠、可编辑、不显示下标。</summary>
        [Test]
        public void 旋钮默认值()
        {
            var attribute = new ListDrawerSettingsAttribute();

            Assert.That(attribute.HideAddButton, Is.False);
            Assert.That(attribute.HideRemoveButton, Is.False);
            Assert.That(attribute.IsReadOnly, Is.False);
            Assert.That(attribute.ShowIndexLabels, Is.False);
            Assert.That(attribute.ShowFoldout, Is.True, "默认画可折叠的头部。");
        }

        /// <summary>旋钮可写——官方的具名实参写法（<c>[ListDrawerSettings(ShowFoldout = false)]</c>）。</summary>
        [Test]
        public void 旋钮可写()
        {
            var attribute = new ListDrawerSettingsAttribute
            {
                HideAddButton = true,
                HideRemoveButton = true,
                IsReadOnly = true,
                ShowIndexLabels = true,
                ShowFoldout = false,
            };

            Assert.That(attribute.HideAddButton, Is.True);
            Assert.That(attribute.HideRemoveButton, Is.True);
            Assert.That(attribute.IsReadOnly, Is.True);
            Assert.That(attribute.ShowIndexLabels, Is.True);
            Assert.That(attribute.ShowFoldout, Is.False);
        }

        /// <summary>
        /// 只标在成员上、不可重复。官方是 <c>AttributeTargets.All</c> + <c>AllowMultiple = true</c>
        /// ——本包收窄（同一字段挂多份列表设置没有语义），写进文档。
        /// </summary>
        [Test]
        public void 用于成员且不可重复()
        {
            var usage = typeof(ListDrawerSettingsAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Property), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False, "集合设置是成员的事。");
        }
    }
}
