using System;
using System.Reflection;
using NUnit.Framework;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 反射取值访问器：私有成员读得到吗、值是不是活的、哪些成员必须被拒绝。
    /// <para>
    /// 这个 fixture 同时是一个 <b>spike</b>：表达式树在本仓的运行环境里能不能访问私有成员，
    /// 只能跑一遍才知道。跑通了才有后面的反射值入口。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ReflectedAccessorTests
    {
        #region Setup / Teardown

        /// <summary>复位夹具的静态状态。</summary>
        [SetUp]
        public void SetUp()
        {
            AccessorFixture.StaticCount = 5;
        }

        /// <summary>复位夹具的静态状态。</summary>
        [TearDown]
        public void TearDown()
        {
            AccessorFixture.StaticCount = 5;
        }

        #endregion

        #region 读得到

        /// <summary>私有字段读得到——这正是反射成员存在的理由。</summary>
        [Test]
        public void 私有字段读得到()
        {
            var fixture = new AccessorFixture();

            Assert.That(TryCreate("_secret", out var accessor), Is.True, "私有字段应当被接受。");
            Assert.That(accessor.Read(fixture), Is.EqualTo(41));
        }

        /// <summary>私有属性的 getter 读得到。</summary>
        [Test]
        public void 私有属性读得到()
        {
            var fixture = new AccessorFixture();

            Assert.That(TryCreate("Doubled", out var accessor), Is.True, "私有属性应当被接受。");
            Assert.That(accessor.Read(fixture), Is.EqualTo(82));
        }

        /// <summary>值类型成员读出来的是装箱的值本身，不是某个引用。</summary>
        [Test]
        public void 值类型成员装箱返回()
        {
            var fixture = new AccessorFixture();

            TryCreate("_secret", out var accessor);

            Assert.That(accessor.ValueType, Is.EqualTo(typeof(int)));
            Assert.That(accessor.Read(fixture), Is.TypeOf<int>());
        }

        /// <summary>静态成员读得到，目标传 <c>null</c> 也不炸——它本来就不作用于某个实例。</summary>
        [Test]
        public void 静态成员读得到()
        {
            Assert.That(TryCreate("StaticCount", out var accessor), Is.True);
            Assert.That(accessor.IsStatic, Is.True, "静态属性应当被标记为静态。");
            Assert.That(accessor.Read(null), Is.EqualTo(5));

            AccessorFixture.StaticCount = 9;

            Assert.That(accessor.Read(null), Is.EqualTo(9));
        }

        /// <summary>
        /// 读的是**当前值**，不是建访问器时的快照。
        /// <para>
        /// 这条必须钉住：若实现改成「编译时把值抄一份」，一切看起来都对，
        /// 直到用户发现改了字段而 Inspector 纹丝不动。
        /// </para>
        /// </summary>
        [Test]
        public void 读到的是活值()
        {
            var fixture = new AccessorFixture();

            TryCreate("_secret", out var accessor);
            Assert.That(accessor.Read(fixture), Is.EqualTo(41));

            fixture.Mutate(99);

            Assert.That(accessor.Read(fixture), Is.EqualTo(99), "改过之后应当读到新值。");
        }

        /// <summary>值类型的声明类型也能读——这条走的是拆箱而不是引用转换。</summary>
        [Test]
        public void 结构体成员读得到()
        {
            var boxed = (object)new AccessorStruct { Value = 6 };

            Assert.That(TryCreateStructMember("Value", out var accessor), Is.True);
            Assert.That(accessor.Read(boxed), Is.EqualTo(6));
        }

        /// <summary>访问器记着它是从哪个成员编译来的——告警文本要用它。</summary>
        [Test]
        public void 访问器记着来源成员()
        {
            TryCreate("Name", out var accessor);

            Assert.That(accessor.Member, Is.Not.Null);
            Assert.That(accessor.Member.Name, Is.EqualTo("Name"));
            Assert.That(accessor.ValueType, Is.EqualTo(typeof(string)));
            Assert.That(accessor.IsStatic, Is.False);
        }

        #endregion

        #region 必须拒绝

        /// <summary>
        /// 索引器被拒绝。
        /// <para>
        /// 控制项是同夹具的普通属性**被接受**——否则「拒绝」可能只是因为整个 TryCreate 恒失败，
        /// 那条断言就等于什么都没测。
        /// </para>
        /// </summary>
        [Test]
        public void 索引器被拒绝()
        {
            Assert.That(TryCreate("Item", out var accessor, out var reason), Is.False);
            Assert.That(accessor, Is.Null);
            Assert.That(reason, Does.Contain("索引器"));
            Assert.That(TryCreate("Name", out _, out _), Is.True, "同夹具的普通属性应当仍被接受。");
        }

        /// <summary>只写属性读不到值，被拒绝。</summary>
        [Test]
        public void 只写属性被拒绝()
        {
            Assert.That(TryCreate("WriteOnly", out var accessor, out var reason), Is.False);
            Assert.That(accessor, Is.Null);
            Assert.That(reason, Does.Contain("只写"));
            Assert.That(TryCreate("Name", out _, out _), Is.True, "同夹具的普通属性应当仍被接受。");
        }

        /// <summary>方法既不是字段也不是属性，被拒绝且原因说得出来。</summary>
        [Test]
        public void 方法被拒绝()
        {
            var method = typeof(AccessorFixture).GetMethod(
                nameof(AccessorFixture.Mutate),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            Assert.That(ReflectedAccessor.TryCreate(method, out var accessor, out var reason), Is.False);
            Assert.That(accessor, Is.Null);
            Assert.That(reason, Does.Contain("既不是字段也不是属性"));
        }

        /// <summary><c>null</c> 被拒绝，不抛异常。</summary>
        [Test]
        public void 空成员被拒绝()
        {
            Assert.DoesNotThrow(() =>
            {
                Assert.That(ReflectedAccessor.TryCreate(null, out var accessor, out var reason), Is.False);
                Assert.That(accessor, Is.Null);
                Assert.That(reason, Is.Not.Null.And.Not.Empty);
            });
        }

        #endregion

        #region Private Helpers

        /// <summary>按名取夹具上的成员并试着编译。</summary>
        /// <param name="name">成员名。</param>
        /// <param name="accessor">编译出的访问器。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryCreate(string name, out ReflectedAccessor accessor)
        {
            return TryCreate(name, out accessor, out _);
        }

        /// <summary>按名取夹具上的成员并试着编译，带出原因。</summary>
        /// <param name="name">成员名。</param>
        /// <param name="accessor">编译出的访问器。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryCreate(string name, out ReflectedAccessor accessor, out string reason)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            var members = typeof(AccessorFixture).GetMember(name, Flags);
            Assert.That(members, Is.Not.Empty, $"夹具上没有名叫 {name} 的成员。");

            return ReflectedAccessor.TryCreate(members[0], out accessor, out reason);
        }

        /// <summary>取结构体夹具上的成员并试着编译。</summary>
        /// <param name="name">成员名。</param>
        /// <param name="accessor">编译出的访问器。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryCreateStructMember(string name, out ReflectedAccessor accessor)
        {
            var field = typeof(AccessorStruct).GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"结构体夹具上没有名叫 {name} 的字段。");

            return ReflectedAccessor.TryCreate(field, out accessor, out _);
        }

        #endregion
    }

    /// <summary>取值测试用的夹具：私有字段、私有属性、静态成员、索引器、只写属性各一。</summary>
    internal sealed class AccessorFixture
    {
        /// <summary>静态计数——用来验证静态成员的目标无关性。</summary>
        public static int StaticCount = 5;

        /// <summary>私有字段。</summary>
        private int _secret = 41;

        /// <summary>自动属性。</summary>
        public string Name { get; set; } = "默认";

        /// <summary>private get-only 属性。</summary>
        private int Doubled => _secret * 2;

        /// <summary>只写属性——取值访问器必须拒绝它。</summary>
        public int WriteOnly
        {
            set => _secret = value;
        }

        /// <summary>索引器——取值访问器必须拒绝它。</summary>
        /// <param name="index">下标。</param>
        public int this[int index] => index * 10;

        /// <summary>改一改私有字段，用来验证读到的是活值。</summary>
        /// <param name="value">新值。</param>
        public void Mutate(int value)
        {
            _secret = value;
        }
    }

    /// <summary>值类型夹具：走拆箱那条路径。</summary>
    internal struct AccessorStruct
    {
        /// <summary>一个普通字段。</summary>
        public int Value;
    }
}
