using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// 三个内嵌环境特性（<c>[ShowInInlineEditors]</c> / <c>[HideInInlineEditors]</c> /
    /// <c>[DisableInInlineEditors]</c>）的声明面。
    /// <para>
    /// 它们刻意是**空标记**：判据在编辑器侧（读绘制期的深度上下文），Runtime 侧不能碰 Unity。
    /// 行为在 <c>InlineEditorConditionProcessorTests</c> 里测。
    /// </para>
    /// </summary>
    [TestFixture]
    public class InlineEditorConditionAttributeTests
    {
        #region Private Fields

        /// <summary>三个条件族。</summary>
        private static readonly Type[] ConditionTypes =
        {
            typeof(ShowInInlineEditorsAttribute),
            typeof(HideInInlineEditorsAttribute),
            typeof(DisableInInlineEditorsAttribute),
        };

        #endregion

        #region 声明面

        /// <summary>三个都是零参构造的空标记——不接任何选项。</summary>
        [Test]
        public void 三个都是零参空标记()
        {
            foreach (var type in ConditionTypes)
            {
                var constructors = type.GetConstructors();
                Assert.That(constructors.Length, Is.EqualTo(1), $"{type.Name} 只该有一个公开构造。");
                Assert.That(constructors[0].GetParameters(), Is.Empty, $"{type.Name} 不该接参数。");

                Assert.That(
                    type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                    Is.Empty,
                    $"{type.Name} 多出了属性——空标记不接选项。");
                Assert.That(
                    type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                    Is.Empty,
                    $"{type.Name} 多出了字段——空标记不接选项。");
            }
        }

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void 仅成员且不可重复()
        {
            foreach (var type in ConditionTypes)
            {
                var usage = type.GetCustomAttribute<AttributeUsageAttribute>();

                Assert.That(usage, Is.Not.Null, $"{type.Name} 缺 [AttributeUsage]。");
                Assert.That(usage.AllowMultiple, Is.False, $"{type.Name} 不该可重复。");
                Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True, $"{type.Name} 应能标在字段上。");
                Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False, $"{type.Name} 不收类级用法。");
            }
        }

        #endregion
    }
}
