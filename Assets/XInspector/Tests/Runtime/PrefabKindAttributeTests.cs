using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// 预制体上下文族六个特性（<c>[ShowIn]</c>、<c>[HideIn]</c>、<c>[EnableIn]</c>、<c>[DisableIn]</c>、
    /// <c>[RequiredIn]</c>、<c>[DisallowModificationsIn]</c>）与 <c>PrefabKind</c> 的构造行为与用法约束。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// 「当前处在哪种上下文」的判据在编辑器侧，不在本文件。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PrefabKindAttributeTests
    {
        #region PrefabKind

        /// <summary>
        /// 只有这 10 个成员，一个不多一个不少——**不发明第 11 个**（模型预制体归入 <c>Regular</c>，
        /// 那是解析规则的事，不是枚举的事）。
        /// </summary>
        [Test]
        public void PrefabKind成员与官方一致()
        {
            var names = Enum.GetNames(typeof(PrefabKind));

            Assert.That(names, Is.EquivalentTo(new[]
            {
                "None", "InstanceInPrefab", "InstanceInScene", "Regular", "Variant", "NonPrefabInstance",
                "PrefabInstance", "PrefabAsset", "PrefabInstanceAndNonPrefabInstance", "All",
            }));
            Assert.That(names.Length, Is.EqualTo(10), "官方是 10 个成员。");
        }

        /// <summary>是位标志枚举——匹配算法（求交集）建立在这一点上。</summary>
        [Test]
        public void PrefabKind是位标志()
        {
            Assert.That(typeof(PrefabKind).IsDefined(typeof(FlagsAttribute), false), Is.True);
            Assert.That((int)PrefabKind.None, Is.EqualTo(0));
        }

        /// <summary>
        /// 五个具体位两两不相交、各自是 2 的幂——**一次解析恰好命中其中一个**这条契约的前提。
        /// </summary>
        [Test]
        public void 具体位两两不相交()
        {
            var concrete = ConcreteBits();

            foreach (var bit in concrete)
            {
                var value = (int)bit;
                Assert.That(value, Is.GreaterThan(0), $"{bit} 不能是 0。");
                Assert.That(value & (value - 1), Is.EqualTo(0), $"{bit} 必须是 2 的幂。");
            }

            for (var i = 0; i < concrete.Length; i++)
            {
                for (var j = i + 1; j < concrete.Length; j++)
                {
                    Assert.That((int)concrete[i] & (int)concrete[j], Is.EqualTo(0),
                        $"{concrete[i]} 与 {concrete[j]} 不该有交叠。");
                }
            }
        }

        /// <summary>四个复合成员就是具体位的并集——正因如此「求交集」的匹配不需要任何特判。</summary>
        [Test]
        public void 复合成员是具体位的并集()
        {
            Assert.That(PrefabKind.PrefabInstance,
                Is.EqualTo(PrefabKind.InstanceInPrefab | PrefabKind.InstanceInScene));
            Assert.That(PrefabKind.PrefabAsset,
                Is.EqualTo(PrefabKind.Regular | PrefabKind.Variant));
            Assert.That(PrefabKind.PrefabInstanceAndNonPrefabInstance,
                Is.EqualTo(PrefabKind.PrefabInstance | PrefabKind.NonPrefabInstance));

            var all = ConcreteBits().Aggregate(PrefabKind.None, (acc, bit) => acc | bit);
            Assert.That(PrefabKind.All, Is.EqualTo(all), "All 必须覆盖全部五个具体位。");
            Assert.That(PrefabKind.None & PrefabKind.All, Is.EqualTo(PrefabKind.None), "None 恒不匹配。");
        }

        #endregion

        #region 四个条件特性

        /// <summary>四个条件都允许标在方法上——它们对按钮节点确实生效（与那 11 个条件特性同一条判据）。</summary>
        [Test]
        public void 四个条件的用法声明一致()
        {
            foreach (var type in new[]
                     {
                         typeof(ShowInAttribute), typeof(HideInAttribute),
                         typeof(EnableInAttribute), typeof(DisableInAttribute),
                     })
            {
                var usage = UsageOf(type);

                Assert.That(usage.ValidOn,
                    Is.EqualTo(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method),
                    type.Name);
                Assert.That(usage.AllowMultiple, Is.False, type.Name);
                Assert.That(usage.Inherited, Is.True, type.Name);
            }
        }

        /// <summary>构造只收上下文，属性原样带回；<c>None</c> 合法（只是恒不匹配），构造期不拦。</summary>
        [Test]
        public void 四个条件的构造与属性()
        {
            Assert.That(new ShowInAttribute(PrefabKind.Regular).PrefabKind, Is.EqualTo(PrefabKind.Regular));
            Assert.That(new HideInAttribute(PrefabKind.InstanceInScene).PrefabKind,
                Is.EqualTo(PrefabKind.InstanceInScene));
            Assert.That(new EnableInAttribute(PrefabKind.Variant).PrefabKind, Is.EqualTo(PrefabKind.Variant));
            Assert.That(new DisableInAttribute(PrefabKind.PrefabInstance).PrefabKind,
                Is.EqualTo(PrefabKind.PrefabInstance));

            Assert.That(new ShowInAttribute(PrefabKind.None).PrefabKind, Is.EqualTo(PrefabKind.None),
                "None 是合法输入——语义是「恒不匹配」，不是笔误。");
            Assert.That(new ShowInAttribute(PrefabKind.Regular | PrefabKind.Variant).PrefabKind,
                Is.EqualTo(PrefabKind.PrefabAsset), "位组合按值相等，与 PrefabAsset 同义。");
        }

        #endregion

        #region 两个校验特性

        /// <summary>
        /// 两个校验都**不含方法**——它们的核心判据都要值入口（判空要 <c>SerializedProperty</c>，
        /// 「已改过」也要）。照 <c>[Required]</c> 的既有收窄。
        /// </summary>
        [Test]
        public void 两个校验的用法声明一致()
        {
            foreach (var type in new[] { typeof(RequiredInAttribute), typeof(DisallowModificationsInAttribute) })
            {
                var usage = UsageOf(type);

                Assert.That(usage.ValidOn,
                    Is.EqualTo(AttributeTargets.Field | AttributeTargets.Property), type.Name);
                Assert.That(usage.AllowMultiple, Is.False, type.Name);
                Assert.That(usage.Inherited, Is.True, type.Name);
            }
        }

        /// <summary>构造只收上下文；消息默认是 null（绘制侧用本包的默认文本）。</summary>
        [Test]
        public void RequiredIn的构造与消息()
        {
            var attribute = new RequiredInAttribute(PrefabKind.PrefabAsset);

            Assert.That(attribute.PrefabKind, Is.EqualTo(PrefabKind.PrefabAsset));
            Assert.That(attribute.ErrorMessage, Is.Null, "没给消息时用默认文本。");

            attribute.ErrorMessage = "预制体资产上必须指定图标。";
            Assert.That(attribute.ErrorMessage, Is.EqualTo("预制体资产上必须指定图标。"));

            attribute.ErrorMessage = null;
            Assert.That(attribute.ErrorMessage, Is.Null, "显式赋 null 回到默认文本。");
        }

        /// <summary>空白消息判为笔误，当场抛——不能拖到绘制期才让人看见（与 <c>[Required]</c> 同款）。</summary>
        [Test]
        public void RequiredIn的空白消息当场抛()
        {
            var attribute = new RequiredInAttribute(PrefabKind.Regular);

            Assert.Throws<ArgumentException>(() => attribute.ErrorMessage = "   ");
        }

        /// <summary>构造只收上下文，属性原样带回。</summary>
        [Test]
        public void DisallowModificationsIn的构造与属性()
        {
            var attribute = new DisallowModificationsInAttribute(PrefabKind.InstanceInScene);

            Assert.That(attribute.PrefabKind, Is.EqualTo(PrefabKind.InstanceInScene));
        }

        #endregion

        #region Private Helpers

        /// <summary>五个「具体位」——一次解析恰好命中其中一个。</summary>
        /// <returns>具体位。</returns>
        private static PrefabKind[] ConcreteBits()
        {
            return new[]
            {
                PrefabKind.InstanceInPrefab,
                PrefabKind.InstanceInScene,
                PrefabKind.Regular,
                PrefabKind.Variant,
                PrefabKind.NonPrefabInstance,
            };
        }

        /// <summary>取特性的 <see cref="AttributeUsageAttribute"/>，顺手断言它确实声明了。</summary>
        /// <param name="type">特性类型。</param>
        /// <returns>它的用法声明。</returns>
        private static AttributeUsageAttribute UsageOf(Type type)
        {
            var usage = type.GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null, type.Name);
            return usage;
        }

        #endregion
    }
}
