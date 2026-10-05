using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// 表格三件（<c>[TableList]</c> / <c>[TableColumnWidth]</c> / <c>[HideInTables]</c>）的
    /// 形状与用法约束。不依赖 Unity，同时跑在离线通道里。
    /// </summary>
    [TestFixture]
    public class TableListAttributeTests
    {
        /// <summary>两个旋钮默认关闭。</summary>
        [Test]
        public void 旋钮默认值()
        {
            var attribute = new TableListAttribute();

            Assert.That(attribute.ShowIndexLabels, Is.False);
            Assert.That(attribute.AlwaysExpanded, Is.False);
        }

        /// <summary>旋钮可写（官方的具名实参写法）。</summary>
        [Test]
        public void 旋钮可写()
        {
            var attribute = new TableListAttribute { ShowIndexLabels = true, AlwaysExpanded = true };

            Assert.That(attribute.ShowIndexLabels, Is.True);
            Assert.That(attribute.AlwaysExpanded, Is.True);
        }

        /// <summary>标在成员上、不可重复、可继承。</summary>
        [Test]
        public void 表格特性用于成员()
        {
            var usage = typeof(TableListAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Property), Is.True);
        }

        /// <summary>列宽只以像素给出——官方的 <c>resizable</c> 参数本包不声明（不做拖拽调宽）。</summary>
        [Test]
        public void 列宽特性只有宽度()
        {
            var attribute = new TableColumnWidthAttribute(57);

            Assert.That(attribute.Width, Is.EqualTo(57));

            var constructors = typeof(TableColumnWidthAttribute).GetConstructors();
            Assert.That(constructors.Length, Is.EqualTo(1), "只有一个构造（带 resizable 的那个不声明）。");
            Assert.That(constructors[0].GetParameters().Length, Is.EqualTo(1));

            var usage = typeof(TableColumnWidthAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.AllowMultiple, Is.False);
        }

        /// <summary><c>[HideInTables]</c> 是空标记，标在字段上。</summary>
        [Test]
        public void 隐藏特性是空标记()
        {
            var usage = typeof(HideInTablesAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.AllowMultiple, Is.False);

            // DeclaredOnly：不带它会把 System.Attribute 自己的 TypeId 也算进来。
            const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            Assert.That(typeof(HideInTablesAttribute).GetProperties(Declared).Length, Is.EqualTo(0));
            Assert.That(typeof(HideInTablesAttribute).GetFields(Declared).Length, Is.EqualTo(0));
        }
    }
}
