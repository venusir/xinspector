using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[TypeDrawerSettings]</c> 与 <c>TypeInclusionFilter</c> 的构造行为与用法约束。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// 「候选怎么扫、菜单怎么建」的判据在编辑器侧，不在本文件。
    /// </para>
    /// </summary>
    [TestFixture]
    public class TypeDrawerSettingsAttributeTests
    {
        #region TypeInclusionFilter

        /// <summary>只有这 6 个成员，一个不多一个不少——**不发明第 7 个**。</summary>
        [Test]
        public void TypeInclusionFilter成员与官方一致()
        {
            var names = Enum.GetNames(typeof(TypeInclusionFilter));

            Assert.That(names, Is.EquivalentTo(new[]
            {
                "None", "IncludeAll", "IncludeAbstracts", "IncludeConcreteTypes",
                "IncludeGenerics", "IncludeInterfaces",
            }));
            Assert.That(names.Length, Is.EqualTo(6), "官方是 6 个成员。");
        }

        /// <summary>是位标志枚举——匹配算法（求交非空）建立在这一点上。</summary>
        [Test]
        public void TypeInclusionFilter是位标志()
        {
            Assert.That(typeof(TypeInclusionFilter).IsDefined(typeof(FlagsAttribute), false), Is.True);
            Assert.That((int)TypeInclusionFilter.None, Is.EqualTo(0));
        }

        /// <summary>
        /// 四个具体位两两不相交、各自是 2 的幂——「一个类型可以同时命中多位」这条描述能力的前提。
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

        /// <summary>复合成员就是四个具体位的并集——正因如此「求交」的匹配不需要任何特判。</summary>
        [Test]
        public void IncludeAll是具体位的并集()
        {
            var all = ConcreteBits().Aggregate(TypeInclusionFilter.None, (acc, bit) => acc | bit);

            Assert.That(TypeInclusionFilter.IncludeAll, Is.EqualTo(all), "IncludeAll 必须覆盖全部四个具体位。");
            Assert.That(
                TypeInclusionFilter.None & TypeInclusionFilter.IncludeAll,
                Is.EqualTo(TypeInclusionFilter.None),
                "None 恒不匹配。");
        }

        #endregion

        #region 特性

        /// <summary>
        /// 用法声明**只到字段**：本包的属性只有经 <c>[ShowInInspector]</c> 的只读反射路径才进树，
        /// 而本特性存在的意义是**写**——标在属性上必然是「编译得过但什么都不发生」。
        /// </summary>
        [Test]
        public void 用法声明只到字段()
        {
            var usage = typeof(TypeDrawerSettingsAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null, "特性必须声明 AttributeUsage。");
            Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Field), "只标字段。");
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.Inherited, Is.True);
        }

        /// <summary>两个旋钮可读写，默认值：不收窄（<c>null</c> 基类型）与全收。</summary>
        [Test]
        public void 两个旋钮的默认值()
        {
            var attribute = new TypeDrawerSettingsAttribute();

            Assert.That(attribute.BaseType, Is.Null, "不设约束时按 object 收（接口要显式写基类型）。");
            Assert.That(attribute.Filter, Is.EqualTo(TypeInclusionFilter.IncludeAll), "不写 Filter 就是不额外收窄。");

            attribute.BaseType = typeof(IDisposable);
            attribute.Filter = TypeInclusionFilter.IncludeConcreteTypes;

            Assert.That(attribute.BaseType, Is.EqualTo(typeof(IDisposable)));
            Assert.That(attribute.Filter, Is.EqualTo(TypeInclusionFilter.IncludeConcreteTypes));
        }

        /// <summary>
        /// 两个旋钮按官方形状是 <b>public 字段</b>（本包其余特性一律用属性，这里是刻意的例外）
        /// ——被「顺手改成属性」就与官方形状不符了，故钉住。
        /// </summary>
        [Test]
        public void 两个旋钮是字段不是属性()
        {
            var type = typeof(TypeDrawerSettingsAttribute);

            Assert.That(type.GetField("BaseType"), Is.Not.Null, "官方形状是 public 字段。");
            Assert.That(type.GetProperty("BaseType"), Is.Null);
            Assert.That(type.GetField("Filter"), Is.Not.Null);
            Assert.That(type.GetProperty("Filter"), Is.Null);
        }

        #endregion

        #region Private Helpers

        /// <summary>四个「具体位」——一个类型按其归类恰好命中其中若干位。</summary>
        /// <returns>具体位。</returns>
        private static TypeInclusionFilter[] ConcreteBits()
        {
            return new[]
            {
                TypeInclusionFilter.IncludeAbstracts,
                TypeInclusionFilter.IncludeConcreteTypes,
                TypeInclusionFilter.IncludeGenerics,
                TypeInclusionFilter.IncludeInterfaces,
            };
        }

        #endregion
    }
}
