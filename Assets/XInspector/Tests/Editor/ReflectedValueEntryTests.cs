using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 反射值入口：后端契约、多目标一致性、以及用户 getter 出事时的兜底。
    /// <para>
    /// 多目标那几条一律**两个方向都断言**——只断言「异值算不一致」的话，
    /// 一个恒返回「不一致」的实现照样能过。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ReflectedValueEntryTests
    {
        #region Setup / Teardown

        /// <summary>复位静态读取计数。</summary>
        [SetUp]
        public void SetUp()
        {
            ReflectedEntryFixture.StaticReads = 0;
        }

        /// <summary>复位静态读取计数。</summary>
        [TearDown]
        public void TearDown()
        {
            ReflectedEntryFixture.StaticReads = 0;
        }

        #endregion

        #region 后端契约

        /// <summary>它不是 Unity 后端，也拿不出序列化属性——这正是第二套后端的样子。</summary>
        [Test]
        public void 反射后端不是Unity后端()
        {
            var entry = Entry(new object[] { new ReflectedEntryFixture() }, "Value", typeof(int));

            Assert.That(entry.IsUnityBacked, Is.False);
            Assert.That(entry.SerializedProperty, Is.Null);
            Assert.That(entry.ValueType, Is.EqualTo(typeof(int)));
        }

        /// <summary>写入恒抛——本后端是只读的，没有静默丢弃的写路径。</summary>
        [Test]
        public void 写入恒抛()
        {
            var entry = Entry(new object[] { new ReflectedEntryFixture() }, "Value", typeof(int));

            Assert.Throws<NotSupportedException>(() => entry.SetValue(1));
        }

        #endregion

        #region 取值与多目标

        /// <summary>单目标：值取得出来，不算不一致，也没出错。</summary>
        [Test]
        public void 单目标取值()
        {
            var entry = Entry(new object[] { new ReflectedEntryFixture { Value = 3 } }, "Value", typeof(int));

            Assert.That(entry.TryGetDisplayValue(out var value, out var mixed, out var error), Is.True);
            Assert.That(value, Is.EqualTo(3));
            Assert.That(mixed, Is.False);
            Assert.That(error, Is.Null);
        }

        /// <summary>多目标同值时不算不一致。</summary>
        [Test]
        public void 多目标同值不算不一致()
        {
            var first = new ReflectedEntryFixture { Value = 5 };
            var second = new ReflectedEntryFixture { Value = 5 };

            var entry = Entry(new object[] { first, second }, "Value", typeof(int));

            Assert.That(entry.TryGetDisplayValue(out var value, out var mixed, out _), Is.True);
            Assert.That(value, Is.EqualTo(5));
            Assert.That(mixed, Is.False);
            Assert.That(entry.HasMultipleDifferentValues, Is.False);
        }

        /// <summary>多目标异值时算不一致，且拿不到「单一的值」。</summary>
        [Test]
        public void 多目标异值算不一致()
        {
            var first = new ReflectedEntryFixture { Value = 5 };
            var second = new ReflectedEntryFixture { Value = 6 };

            var entry = Entry(new object[] { first, second }, "Value", typeof(int));

            Assert.That(entry.TryGetDisplayValue(out _, out var mixed, out _), Is.False);
            Assert.That(mixed, Is.True);
            Assert.That(entry.HasMultipleDifferentValues, Is.True);
            Assert.Throws<NotSupportedException>(() => entry.GetValue());
        }

        /// <summary>发现不一致就收手，不再读后面的目标——结论已定，多读只是多跑几遍用户代码。</summary>
        [Test]
        public void 不一致时不再读剩下的目标()
        {
            var first = new ReflectedEntryFixture { Value = 1 };
            var second = new ReflectedEntryFixture { Value = 2 };
            var third = new ReflectedEntryFixture { Value = 3 };

            var entry = Entry(new object[] { first, second, third }, "Tracked", typeof(int));

            Assert.That(entry.TryGetDisplayValue(out _, out var mixed, out _), Is.False);
            Assert.That(mixed, Is.True);
            Assert.That(first.ReadCount, Is.EqualTo(1));
            Assert.That(second.ReadCount, Is.EqualTo(1));
            Assert.That(third.ReadCount, Is.EqualTo(0), "第三个目标不该被读到。");
        }

        /// <summary>静态成员跨目标天然一致：只读一次，也不参与比较。</summary>
        [Test]
        public void 静态成员只读一次()
        {
            var entry = Entry(
                new object[] { new ReflectedEntryFixture(), new ReflectedEntryFixture() },
                "StaticNumber",
                typeof(int));

            Assert.That(entry.TryGetDisplayValue(out var value, out var mixed, out _), Is.True);
            Assert.That(value, Is.EqualTo(7));
            Assert.That(mixed, Is.False);
            Assert.That(ReflectedEntryFixture.StaticReads, Is.EqualTo(1), "静态成员只该被读一次。");
        }

        /// <summary>某个目标解析不到这个成员时按「不一致」处理——我们确实不知道它的值。</summary>
        [Test]
        public void 解析不到成员的目标算不一致()
        {
            var targets = new object[] { new ReflectedEntryFixture(), new object() };
            var accessors = new[] { Accessor("Value"), null };

            var entry = new ReflectedValueEntry(targets, accessors, typeof(int));

            Assert.That(entry.TryGetDisplayValue(out _, out var mixed, out var error), Is.False);
            Assert.That(mixed, Is.True, "解析不到就是给不出值，不能拿第一个目标的值冒充。");
            Assert.That(error, Is.Null, "这是「不知道」，不是「出错」。");
        }

        /// <summary>一个访问器都没有时同样按不一致处理，不抛异常。</summary>
        [Test]
        public void 没有访问器时不抛异常()
        {
            var entry = new ReflectedValueEntry(new object[0], new ReflectedAccessor[0], typeof(int));

            Assert.DoesNotThrow(() =>
            {
                Assert.That(entry.TryGetDisplayValue(out _, out var mixed, out _), Is.False);
                Assert.That(mixed, Is.True);
            });
        }

        #endregion

        #region 用户代码出事

        /// <summary>getter 抛异常时转成错误消息，不外传——一个写坏的属性不该让整个 Inspector 白屏。</summary>
        [Test]
        public void getter抛异常转成错误()
        {
            var entry = Entry(new object[] { new ReflectedEntryFixture() }, "Broken", typeof(string));

            Assert.DoesNotThrow(() =>
            {
                Assert.That(entry.TryGetDisplayValue(out _, out var mixed, out var error), Is.False);
                Assert.That(mixed, Is.False, "这是出错，不是不一致。");
                Assert.That(error, Does.Contain("取值炸了"));
            });
        }

        /// <summary>出错时 <see cref="PropertyValueEntry.GetValue"/> 也抛，且带上原始消息。</summary>
        [Test]
        public void 出错时取值也抛()
        {
            var entry = Entry(new object[] { new ReflectedEntryFixture() }, "Broken", typeof(string));

            var exception = Assert.Throws<NotSupportedException>(() => entry.GetValue());
            Assert.That(exception.Message, Does.Contain("取值炸了"));
        }

        #endregion

        #region Unity 对象的空值语义

        /// <summary>
        /// 「空」与「已销毁的对象」算同一个值——Unity 自己就是这么判的，
        /// 多目标下不该因此显示成不一致。
        /// </summary>
        [Test]
        public void 已销毁的Unity对象与空算相同()
        {
            var temporary = ScriptableObject.CreateInstance<ScriptableObject>();
            var destroyed = temporary;
            Object.DestroyImmediate(temporary);

            var first = new ReflectedEntryFixture { Asset = null };
            var second = new ReflectedEntryFixture { Asset = destroyed };

            var entry = Entry(new object[] { first, second }, "Asset", typeof(Object));

            Assert.That(entry.TryGetDisplayValue(out var value, out var mixed, out _), Is.True);
            Assert.That(mixed, Is.False, "两者按 Unity 的语义都是「空」，不该算不一致。");
            Assert.That(value, Is.Null);
        }

        /// <summary>
        /// 控制项：声明类型不是 Unity 对象时，<c>null</c> 与已销毁的对象是两个不同的值。
        /// <para>
        /// 没有这一条，上面那条用例无法区分「按声明类型判等」与「一律把已销毁对象当空」。
        /// </para>
        /// </summary>
        [Test]
        public void 声明类型不是Unity对象时按普通判等()
        {
            var temporary = ScriptableObject.CreateInstance<ScriptableObject>();
            var destroyed = temporary;
            Object.DestroyImmediate(temporary);

            var first = new ReflectedEntryFixture { Any = null };
            var second = new ReflectedEntryFixture { Any = destroyed };

            var entry = Entry(new object[] { first, second }, "Any", typeof(object));

            Assert.That(entry.TryGetDisplayValue(out _, out var mixed, out _), Is.False);
            Assert.That(mixed, Is.True, "声明类型是 object 时，空与已销毁对象不是同一个值。");
        }

        #endregion

        #region Private Helpers

        /// <summary>按成员名建一个入口，逐目标各解析一次。</summary>
        /// <param name="targets">目标数组。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="valueType">声明类型。</param>
        /// <returns>值入口。</returns>
        private static ReflectedValueEntry Entry(object[] targets, string memberName, Type valueType)
        {
            var accessors = new ReflectedAccessor[targets.Length];
            for (var i = 0; i < targets.Length; i++)
            {
                accessors[i] = Accessor(memberName);
            }

            return new ReflectedValueEntry(targets, accessors, valueType);
        }

        /// <summary>取夹具上的成员并编译成访问器。</summary>
        /// <param name="memberName">成员名。</param>
        /// <returns>访问器。</returns>
        private static ReflectedAccessor Accessor(string memberName)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.Public | BindingFlags.NonPublic;

            var member = typeof(ReflectedEntryFixture).GetMember(memberName, Flags)[0];
            return ReflectedAccessor.TryCreate(member, out var accessor, out _) ? accessor : null;
        }

        #endregion
    }

    /// <summary>反射值入口的测试夹具。</summary>
    internal sealed class ReflectedEntryFixture
    {
        /// <summary>静态属性的读取次数。</summary>
        public static int StaticReads;

        /// <summary>一个可以随便设的字段。</summary>
        public int Value;

        /// <summary>一个 Unity 对象类型的字段——用来验证伪 null 的判等语义。</summary>
        public Object Asset;

        /// <summary>一个 <c>object</c> 类型的字段——用来验证「声明类型不是 Unity 对象」那一侧。</summary>
        public object Any;

        /// <summary>读取次数。</summary>
        public int ReadCount => _reads;

        /// <summary>每读一次记一笔，返回值由 <see cref="Value"/> 决定。</summary>
        public int Tracked
        {
            get
            {
                _reads++;
                return Value;
            }
        }

        /// <summary>静态属性，每读一次记一笔。</summary>
        public static int StaticNumber
        {
            get
            {
                StaticReads++;
                return 7;
            }
        }

        /// <summary>getter 总是抛异常——用来验证兜底。</summary>
        public string Broken => throw new InvalidOperationException("取值炸了");

        /// <summary>读取计数。</summary>
        private int _reads;
    }
}
