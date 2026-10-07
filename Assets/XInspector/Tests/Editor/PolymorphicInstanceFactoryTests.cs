using System;
using System.Collections.Generic;
using NUnit.Framework;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 多态选择器的造实例：四档 <c>NonDefaultConstructorPreference</c> 的差别只在
    /// 「没有公开无参构造」那一格——逐格钉住。
    /// <para>纯逻辑，无 GUI；「怎么接进菜单」在绘制器侧，不在本文件。</para>
    /// </summary>
    [TestFixture]
    public class PolymorphicInstanceFactoryTests
    {
        #region 有无参构造的那一格

        /// <summary>值类型与有无参构造的类型：四档都一样，直接造。</summary>
        [Test]
        public void 有无参构造时四档都直接造()
        {
            foreach (var preference in AllPreferences())
            {
                Assert.That(
                    PolymorphicInstanceFactory.TryCreate(
                        typeof(FactoryDefaultCtor), preference, out var instance, out var reason),
                    Is.True,
                    $"{preference}：{reason}");
                Assert.That(instance, Is.InstanceOf<FactoryDefaultCtor>());
                Assert.That(((FactoryDefaultCtor)instance).value, Is.EqualTo(7), "构造跑过了。");

                Assert.That(
                    PolymorphicInstanceFactory.TryCreate(typeof(int), preference, out var boxed, out reason),
                    Is.True,
                    $"{preference}：{reason}");
                Assert.That(boxed, Is.EqualTo(0));
            }
        }

        #endregion

        #region 没有无参构造的那一格

        /// <summary><c>ConstructIdeal</c>：挑**参数最少**的公开实例构造（夹具的两个构造留痕可辨）。</summary>
        [Test]
        public void ConstructIdeal挑最直接的构造()
        {
            Assert.That(
                PolymorphicInstanceFactory.TryCreate(
                    typeof(FactoryParameterizedCtor),
                    NonDefaultConstructorPreference.ConstructIdeal,
                    out var instance,
                    out var reason),
                Is.True,
                reason);

            var probe = (FactoryParameterizedCtor)instance;
            Assert.That(probe.Text, Is.EqualTo("单参"), "两参那个构造不该被挑中。");
            Assert.That(probe.Number, Is.EqualTo(0), "参数填的是默认值。");
        }

        /// <summary><c>ConstructIdeal</c>：参数逐个填 <c>default</c>（字符串取空串，不取 null）。</summary>
        [Test]
        public void ConstructIdeal的参数填默认值()
        {
            Assert.That(
                PolymorphicInstanceFactory.TryCreate(
                    typeof(FactoryTwoArg), NonDefaultConstructorPreference.ConstructIdeal,
                    out var instance, out var reason),
                Is.True,
                reason);

            var probe = (FactoryTwoArg)instance;
            Assert.That(probe.Number, Is.EqualTo(0));
            Assert.That(probe.Text, Is.EqualTo(string.Empty), "字符串按 DefaultFor 取空串。");
        }

        /// <summary><c>Exclude</c> 档：不构造（候选里本就不该有它——走到这里说明调用方传错）。</summary>
        [Test]
        public void Exclude档不构造()
        {
            Assert.That(
                PolymorphicInstanceFactory.TryCreate(
                    typeof(FactoryTwoArg), NonDefaultConstructorPreference.Exclude,
                    out var instance, out var reason),
                Is.False);
            Assert.That(instance, Is.Null);
            Assert.That(reason, Does.Contain("无参构造"));
        }

        /// <summary><c>LogWarning</c> 档：**不构造**、只留原因（调用方告警）。</summary>
        [Test]
        public void LogWarning档不构造()
        {
            Assert.That(
                PolymorphicInstanceFactory.TryCreate(
                    typeof(FactoryTwoArg), NonDefaultConstructorPreference.LogWarning,
                    out var instance, out var reason),
                Is.False);
            Assert.That(instance, Is.Null);
            Assert.That(reason, Does.Contain("只留这条告警"));
        }

        /// <summary>
        /// <c>PreferUninitialized</c>：造得出实例，但**构造没跑**（夹具的哨兵字段仍是默认值）。
        /// </summary>
        [Test]
        public void PreferUninitialized不跑构造()
        {
            Assert.That(
                PolymorphicInstanceFactory.TryCreate(
                    typeof(FactoryUninitializedProbe), NonDefaultConstructorPreference.PreferUninitialized,
                    out var instance, out var reason),
                Is.True,
                reason);

            var probe = (FactoryUninitializedProbe)instance;
            Assert.That(probe.ran, Is.EqualTo(0), "构造跑过的话哨兵会被写成别的值。");
        }

        #endregion

        #region 造不出来的形态

        /// <summary>抽象类、接口、开放泛型：一律拒绝并给原因（不静默）。</summary>
        [Test]
        public void 抽象接口与开放泛型拒绝()
        {
            Assert.That(
                PolymorphicInstanceFactory.TryCreate(
                    typeof(FactoryAbstract), NonDefaultConstructorPreference.ConstructIdeal,
                    out _, out var abstractReason),
                Is.False);
            Assert.That(abstractReason, Does.Contain("抽象"));

            Assert.That(
                PolymorphicInstanceFactory.TryCreate(
                    typeof(IDisposable), NonDefaultConstructorPreference.ConstructIdeal,
                    out _, out var interfaceReason),
                Is.False);
            Assert.That(interfaceReason, Does.Contain("接口"));

            Assert.That(
                PolymorphicInstanceFactory.TryCreate(
                    typeof(List<>), NonDefaultConstructorPreference.ConstructIdeal,
                    out _, out var genericReason),
                Is.False);
            Assert.That(genericReason, Does.Contain("开放泛型"));
        }

        /// <summary>只有私有构造：<c>ConstructIdeal</c> 找不到公开实例构造，拒绝。</summary>
        [Test]
        public void 没有公开构造时拒绝()
        {
            Assert.That(
                PolymorphicInstanceFactory.TryCreate(
                    typeof(FactoryPrivateCtor), NonDefaultConstructorPreference.ConstructIdeal,
                    out _, out var reason),
                Is.False);
            Assert.That(reason, Does.Contain("公开的实例构造"));
        }

        /// <summary>构造体自己抛异常：拒绝，原因取 <c>InnerException</c>（不打印反射包装层）。</summary>
        [Test]
        public void 构造抛异常给原因()
        {
            Assert.That(
                PolymorphicInstanceFactory.TryCreate(
                    typeof(FactoryThrows), NonDefaultConstructorPreference.ConstructIdeal,
                    out _, out var reason),
                Is.False);
            Assert.That(reason, Does.Contain("构造里炸了"), "要说构造体自己的话，不是 TargetInvocationException。");
        }

        #endregion

        #region Private Helpers

        /// <summary>四档。</summary>
        /// <returns>全部成员。</returns>
        private static NonDefaultConstructorPreference[] AllPreferences()
        {
            return new[]
            {
                NonDefaultConstructorPreference.ConstructIdeal,
                NonDefaultConstructorPreference.Exclude,
                NonDefaultConstructorPreference.LogWarning,
                NonDefaultConstructorPreference.PreferUninitialized,
            };
        }

        #endregion
    }

    #region Fixtures

    /// <summary>有公开无参构造。</summary>
    internal sealed class FactoryDefaultCtor
    {
        /// <summary>构造写下的值——证明构造跑过了。</summary>
        public int value = 7;
    }

    /// <summary>两个构造（一参 / 两参）——「挑参数最少者」那条用它。</summary>
    internal sealed class FactoryParameterizedCtor
    {
        /// <summary>收到的整型参数。</summary>
        public readonly int Number;

        /// <summary>收到的字符串参数（一参构造写死「单参」，便于辨认挑了哪一个）。</summary>
        public readonly string Text;

        /// <summary>两参构造。</summary>
        /// <param name="number">整型参数。</param>
        /// <param name="text">字符串参数。</param>
        public FactoryParameterizedCtor(int number, string text)
        {
            Number = number;
            Text = text;
        }

        /// <summary>一参构造。</summary>
        /// <param name="number">整型参数。</param>
        public FactoryParameterizedCtor(int number)
        {
            Number = number;
            Text = "单参";
        }
    }

    /// <summary>只有两参构造——参数逐个填默认值那条用它。</summary>
    internal sealed class FactoryTwoArg
    {
        /// <summary>收到的整型参数。</summary>
        public readonly int Number;

        /// <summary>收到的字符串参数。</summary>
        public readonly string Text;

        /// <summary>两参构造。</summary>
        /// <param name="number">整型参数。</param>
        /// <param name="text">字符串参数。</param>
        public FactoryTwoArg(int number, string text)
        {
            Number = number;
            Text = text;
        }
    }

    /// <summary>构造体抛异常。</summary>
    internal sealed class FactoryThrows
    {
        /// <summary>构造即抛。</summary>
        public FactoryThrows()
        {
            throw new InvalidOperationException("构造里炸了");
        }
    }

    /// <summary>只有私有构造。</summary>
    internal sealed class FactoryPrivateCtor
    {
        private FactoryPrivateCtor()
        {
        }
    }

    /// <summary>没有无参构造、构造会写哨兵——<c>PreferUninitialized</c> 用它证明「构造没跑」。</summary>
    internal sealed class FactoryUninitializedProbe
    {
        /// <summary>哨兵：构造跑过就会变成非零。</summary>
        public int ran;

        /// <summary>带参构造（写下哨兵）。</summary>
        /// <param name="number">参数。</param>
        public FactoryUninitializedProbe(int number)
        {
            ran = number + 1;
        }
    }

    /// <summary>抽象类。</summary>
    internal abstract class FactoryAbstract
    {
    }

    #endregion
}
