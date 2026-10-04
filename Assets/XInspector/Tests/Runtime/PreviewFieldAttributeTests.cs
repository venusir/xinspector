using System;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[PreviewField]</c> 与 <c>ObjectFieldAlignment</c> 的构造行为与用法约束。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PreviewFieldAttributeTests
    {
        #region 四组构造

        /// <summary>官方的四组纯 BCL 重载各自把参数落到该落的属性上。</summary>
        [Test]
        public void 四组构造()
        {
            var bare = new PreviewFieldAttribute();
            Assert.That(bare.Height, Is.EqualTo(0f), "未指定 → 0，绘制时按默认处理。");
            Assert.That(bare.Alignment, Is.EqualTo(ObjectFieldAlignment.Left), "本包定的默认对齐。");

            var byHeight = new PreviewFieldAttribute(80f);
            Assert.That(byHeight.Height, Is.EqualTo(80f));
            Assert.That(byHeight.Alignment, Is.EqualTo(ObjectFieldAlignment.Left));

            var byAlignment = new PreviewFieldAttribute(ObjectFieldAlignment.Right);
            Assert.That(byAlignment.Height, Is.EqualTo(0f));
            Assert.That(byAlignment.Alignment, Is.EqualTo(ObjectFieldAlignment.Right));

            var both = new PreviewFieldAttribute(120f, ObjectFieldAlignment.Center);
            Assert.That(both.Height, Is.EqualTo(120f));
            Assert.That(both.Alignment, Is.EqualTo(ObjectFieldAlignment.Center));
        }

        /// <summary>官方样例里有 <c>[PreviewField(对齐, Height = 150)]</c> 的写法，故 <c>Height</c> 必须可具名赋值。</summary>
        [Test]
        public void Height可具名赋值()
        {
            var attribute = new PreviewFieldAttribute(ObjectFieldAlignment.Left) { Height = 150f };

            Assert.That(attribute.Height, Is.EqualTo(150f));
        }

        #endregion

        #region 对齐枚举

        /// <summary>
        /// 文档站按字母序排成员、数值未核实，故本包按 Left/Center/Right 从 0 起排。
        /// 这条守卫钉住这个约定——改顺序是破坏性变更。
        /// </summary>
        [Test]
        public void 对齐枚举的数值是本包定的()
        {
            Assert.That((int)ObjectFieldAlignment.Left, Is.EqualTo(0));
            Assert.That((int)ObjectFieldAlignment.Center, Is.EqualTo(1));
            Assert.That((int)ObjectFieldAlignment.Right, Is.EqualTo(2));
        }

        #endregion

        #region 用法与边界

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void 仅成员且不可重复()
        {
            var usage = typeof(PreviewFieldAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False);
        }

        /// <summary>
        /// 两个带 <c>UnityEngine.FilterMode</c> 的重载**永久不做**——它们会把 UnityEngine 类型
        /// 带进 Runtime，而「Runtime 零 Unity 依赖」是编译期强制的。
        /// </summary>
        /// <remarks>
        /// 这条守卫的用意：哪天有人「顺手补上」，先被这里问一次。
        /// 注意不能只查构造器——<c>previewGetter</c> 那个字符串参数也不做。
        /// </remarks>
        [Test]
        public void 不含Unity类型的重载()
        {
            var constructors = typeof(PreviewFieldAttribute).GetConstructors();
            Assert.That(constructors.Length, Is.EqualTo(4), "只该有四个纯 BCL 重载。");

            foreach (var constructor in constructors)
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    Assert.That(
                        parameter.ParameterType.Namespace,
                        Does.Not.StartWith("UnityEngine"),
                        $"{constructor} 的参数 {parameter.Name} 带进了 Unity 类型。");
                }
            }
        }

        #endregion
    }
}
