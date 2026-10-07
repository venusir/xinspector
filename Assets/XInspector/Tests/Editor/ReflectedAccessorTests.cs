using System;
using System.Collections.Generic;
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

        #region 强类型布尔读取器

        /// <summary>
        /// 条件的求值走的是强类型读取器——绘制路径上每帧一次，能省一次装箱就省一次。
        /// </summary>
        [Test]
        public void 布尔读取器读得到活值()
        {
            var fixture = new AccessorFixture();

            Assert.That(
                TryCreateBoolean(nameof(AccessorFixture.Flag), out var reader, out _),
                Is.True);

            Assert.That(reader(fixture), Is.True);

            fixture.Flag = false;

            Assert.That(reader(fixture), Is.False, "读的该是活值。");
        }

        /// <summary>
        /// 非 bool 成员被拒绝，且原因说得出来。
        /// <para>
        /// 控制项是同夹具的 bool 成员**被接受**——否则「拒绝」可能只是因为整个入口恒失败。
        /// </para>
        /// </summary>
        [Test]
        public void 布尔读取器拒绝非布尔成员()
        {
            Assert.That(TryCreateBoolean("_secret", out var reader, out var reason), Is.False);
            Assert.That(reader, Is.Null);
            Assert.That(reason, Does.Contain("不是 bool"));
            Assert.That(TryCreateBoolean(nameof(AccessorFixture.Flag), out _, out _), Is.True);
        }

        /// <summary>索引器、只写属性同样被拒绝——两个入口共用同一段形状校验。</summary>
        [Test]
        public void 布尔读取器与装箱入口的拒绝面一致()
        {
            Assert.That(TryCreateBoolean("Item", out _, out var indexerReason), Is.False);
            Assert.That(indexerReason, Does.Contain("索引器"));

            Assert.That(TryCreateBoolean("WriteOnly", out _, out var writeOnlyReason), Is.False);
            Assert.That(writeOnlyReason, Does.Contain("只写"));
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

        #region 路径访问器（嵌套层的读路径）

        /// <summary>点分路径逐段下钻，读到嵌套实例里的值。</summary>
        [Test]
        public void 路径读到嵌套值()
        {
            var fixture = new PathFixture();

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Stats.Hp", out var accessor, out var reason), Is.True, reason);
            Assert.That(accessor.Read(fixture), Is.EqualTo(10));
            Assert.That(accessor.ValueType, Is.EqualTo(typeof(int)));
        }

        /// <summary>
        /// 读到的是**活值**——链每次穿过字段，而不是某个时刻的快照。
        /// </summary>
        /// <remarks>
        /// 这条是「绑实例」那条被否决方案的反面：绑定的实例在父字段被重新赋值（`Stats = new …`、
        /// Undo、预制体 revert）之后就是旧对象，读出来会**静默地是陈旧的**。
        /// </remarks>
        [Test]
        public void 路径读到的是活值()
        {
            var fixture = new PathFixture();
            ReflectedAccessor.TryCreatePath(typeof(PathFixture), "Stats.Hp", out var accessor, out _);

            fixture.Stats.Hp = 99;
            Assert.That(accessor.Read(fixture), Is.EqualTo(99), "改嵌套实例里的字段，应当立刻读到。");

            fixture.Stats = new PathStats { Hp = 7 };
            Assert.That(accessor.Read(fixture), Is.EqualTo(7), "换掉整个嵌套实例，同样应当读到。");
        }

        /// <summary>中段是值类型（struct）也读得到，且读到的是当前值。</summary>
        [Test]
        public void 路径的中段是值类型也读得到()
        {
            var fixture = new PathFixture();
            fixture.Holder.Value.Number = 3;

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Holder.Value.Number", out var accessor, out var reason),
                Is.True, reason);
            Assert.That(accessor.Read(fixture), Is.EqualTo(3));

            fixture.Holder.Value.Number = 8;
            Assert.That(accessor.Read(fixture), Is.EqualTo(8), "值类型中段的改动同样应当立刻读到。");
        }

        /// <summary>深层路径读得到（三段以上）。</summary>
        [Test]
        public void 深层路径读得到()
        {
            var fixture = new PathFixture();

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Stats.Inner.Name", out var accessor, out var reason), Is.True, reason);
            Assert.That(accessor.Read(fixture), Is.EqualTo("内层"));
        }

        /// <summary>
        /// 中间段为 <c>null</c> 时整条链给 <c>null</c>，**不抛**。
        /// </summary>
        /// <remarks>
        /// 托管对象与序列化数据不同：<c>public Inner inner;</c> 可以真的是 null
        /// （序列化那条路上 Unity 总会补出一个实例）。没有空传播的话，条件求值器会**每帧**
        /// 抛一次 NullReferenceException——而它跑在绘制路径上。
        /// </remarks>
        [Test]
        public void 路径中间段为null时给null()
        {
            var fixture = new PathFixture { Stats = new PathStats { Inner = null } };

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Stats.Inner.Name", out var accessor, out var reason), Is.True, reason);

            Assert.That(accessor.Read(fixture), Is.Null, "中间段是 null，整条链给 null。");

            fixture.Stats.Inner = new PathInner { Name = "回来了" };
            Assert.That(accessor.Read(fixture), Is.EqualTo("回来了"), "补回实例后立刻又能读到。");
        }

        /// <summary>中间段为 <c>null</c> 且字段是值类型时给默认值，同样不抛。</summary>
        [Test]
        public void 路径中间段为null时值类型给默认值()
        {
            var fixture = new PathFixture { Holder = null };

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Holder.Value.Number", out var accessor, out var reason), Is.True, reason);

            Assert.That(accessor.Read(fixture), Is.EqualTo(0));
        }

        /// <summary>路径段解析不到时给得出原因，不抛。</summary>
        [Test]
        public void 路径段解析不到时给原因()
        {
            Assert.DoesNotThrow(() =>
            {
                Assert.That(ReflectedAccessor.TryCreatePath(
                    typeof(PathFixture), "Stats.Missing", out var accessor, out var reason), Is.False);
                Assert.That(accessor, Is.Null);
                Assert.That(reason, Does.Contain("Missing"));
            });
        }

        #endregion

        #region 索引段（元素层的读路径）

        /// <summary>索引段读到数组元素——<c>Array</c> + <c>data[i]</c> 是路径转义，不是类型下钻。</summary>
        /// <remarks>
        /// 2026-10-06 之前这一条断言的是「被拒绝」：那会儿元素还没节点化，拒绝是给这一天
        /// 留的接口（见 <c>Pipeline</c> §十九）。现在它读得到——而且末段是索引时
        /// <see cref="ReflectedAccessor.Member"/> 为 <c>null</c>、<c>ValueType</c> 是元素类型。
        /// </remarks>
        [Test]
        public void 索引段读到数组元素()
        {
            var fixture = new PathFixture();

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Items.Array.data[0]", out var accessor, out var reason), Is.True, reason);

            Assert.That(accessor.Read(fixture), Is.EqualTo(1));
            Assert.That(accessor.ValueType, Is.EqualTo(typeof(int)));
            Assert.That(accessor.Member, Is.Null, "末段是索引：没有单一成员可记。");
        }

        /// <summary>索引段读到 <c>List&lt;T&gt;</c> 的元素，后面还能继续接字段段。</summary>
        [Test]
        public void 索引段读到List元素并继续下钻()
        {
            var fixture = new PathFixture();

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "StatsList.Array.data[1].Hp", out var accessor, out var reason),
                Is.True, reason);

            Assert.That(accessor.Read(fixture), Is.EqualTo(22));
            Assert.That(accessor.Member.Name, Is.EqualTo("Hp"), "末段是字段时照旧记着成员。");
        }

        /// <summary>同一个集合上的两个下标各读各的——逐元素编译，不串号。</summary>
        [Test]
        public void 两个下标不串号()
        {
            var fixture = new PathFixture();

            ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "StatsList.Array.data[0].Hp", out var first, out _);
            ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "StatsList.Array.data[1].Hp", out var second, out _);

            Assert.That(first.Read(fixture), Is.EqualTo(11));
            Assert.That(second.Read(fixture), Is.EqualTo(22));
        }

        /// <summary>索引段读到的是**活值**：改元素里的字段、换掉整个集合都跟着走。</summary>
        /// <remarks>
        /// 与 <see cref="路径读到的是活值"/> 同款——元素层的消费者（条件、按钮、按名回调）
        /// 同样绑定在访问器上，绑死实例会在父集合被重新赋值后**静默陈旧**。
        /// </remarks>
        [Test]
        public void 索引段读到的是活值()
        {
            var fixture = new PathFixture();
            ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "StatsList.Array.data[1].Hp", out var accessor, out _);

            fixture.StatsList[1].Hp = 99;
            Assert.That(accessor.Read(fixture), Is.EqualTo(99), "改元素里的字段，应当立刻读到。");

            fixture.StatsList = new List<PathStats> { new PathStats { Hp = 1 }, new PathStats { Hp = 33 } };
            Assert.That(accessor.Read(fixture), Is.EqualTo(33), "换掉整个集合，同样应当读到。");
        }

        /// <summary>值类型元素（struct）读得到——取出的是副本，只读语义下无差别。</summary>
        [Test]
        public void 索引段读到值类型元素()
        {
            var fixture = new PathFixture();
            fixture.Holders[0].Value.Number = 8;

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Holders.Array.data[0].Value.Number", out var accessor, out var reason),
                Is.True, reason);
            Assert.That(accessor.Read(fixture), Is.EqualTo(8));
        }

        /// <summary>集合为 <c>null</c> 时整条链给不到实例，**不抛**（空传播的又一格）。</summary>
        [Test]
        public void 空集合给不到实例()
        {
            var fixture = new PathFixture { StatsList = null };

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "StatsList.Array.data[0]", out var accessor, out var reason),
                Is.True, reason);
            Assert.That(accessor.Read(fixture), Is.Null);

            fixture.StatsList = new List<PathStats> { new PathStats { Hp = 5 } };
            Assert.That(accessor.Read(fixture), Is.Not.Null, "补回集合后立刻又能读到。");
        }

        /// <summary>
        /// 越界的两种落点：**末段**给 <c>null</c>（「取不到实例」——多选时各目标长度不一致才看得到），
        /// **中间段**给 <c>default(元素类型)</c>（后面还有字段段，null 与默认在「再往下读一个字段」
        /// 这条路上是同一件事）。
        /// </summary>
        [Test]
        public void 越界的落点分两档()
        {
            var fixture = new PathFixture();

            ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "StatsList.Array.data[9]", out var last, out _);
            Assert.That(last.Read(fixture), Is.Null, "末段越界 = 取不到实例。");

            ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "StatsList.Array.data[9].Hp", out var middle, out _);
            Assert.That(middle.Read(fixture), Is.EqualTo(0), "中段越界 = 默认值，后面照常下钻。");
        }

        /// <summary>索引对**不占**深度预算——它是路径转义，不是类型下钻。</summary>
        [Test]
        public void 索引对不占深度预算()
        {
            var fixture = new PathFixture();

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "StatsList.Array.data[0].Inner.Name", out var accessor, out var reason),
                Is.True, reason);
            Assert.That(accessor.Read(fixture), Is.EqualTo("内层"), "两段字段 + 一对索引，仍在预算内。");
        }

        /// <summary>
        /// **两组**索引对：元素层深度 &gt; 1 的路径（每层一对）读得到。
        /// </summary>
        [Test]
        public void 两组索引对读到内层元素()
        {
            var fixture = new PathFixture();

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Outers.Array.data[0].Inner.Array.data[1].Hp",
                out var accessor, out var reason), Is.True, reason);
            Assert.That(accessor.Read(fixture), Is.EqualTo(222));

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Outers.Array.data[0].Inner.Array.data[1]",
                out var last, out reason), Is.True, reason);
            Assert.That(last.Read(fixture), Is.InstanceOf<PathStats>(), "末段索引给元素实例。");
            Assert.That(last.ValueType, Is.EqualTo(typeof(PathStats)));
            Assert.That(last.Member, Is.Null);
        }

        /// <summary>两组索引对的落点与一组时同款：末段 <c>null</c>、中间段默认值；全程不抛。</summary>
        [Test]
        public void 两组索引对的落点分两档()
        {
            var fixture = new PathFixture();

            ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Outers.Array.data[0].Inner.Array.data[9]", out var last, out _);
            Assert.That(last.Read(fixture), Is.Null, "末段越界 = 取不到实例。");

            ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Outers.Array.data[0].Inner.Array.data[9].Hp", out var middle, out _);
            Assert.That(middle.Read(fixture), Is.EqualTo(0), "中段越界 = 默认值，后面照常下钻。");

            fixture.Outers[0].Inner.Clear();
            ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Outers.Array.data[0].Inner.Array.data[0]", out var empty, out _);
            Assert.That(empty.Read(fixture), Is.Null, "空集合同样给不到实例。");
        }

        /// <summary>内层集合为 <c>null</c> 时给不到实例（多组索引的空传播），**不抛**。</summary>
        [Test]
        public void 两组索引对内层为null时给不到实例()
        {
            var fixture = new PathFixture();
            fixture.Outers[0].Inner = null;

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Outers.Array.data[0].Inner.Array.data[0]", out var accessor, out _),
                Is.True);
            Assert.That(accessor.Read(fixture), Is.Null);
        }

        /// <summary>两组索引对 + 值类型元素（struct）的组合。</summary>
        [Test]
        public void 两组索引对里的值类型元素()
        {
            var fixture = new PathFixture();

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Outers.Array.data[0].Values.Array.data[0].Number",
                out var accessor, out var reason), Is.True, reason);
            Assert.That(accessor.Read(fixture), Is.EqualTo(7));
        }

        /// <summary>
        /// 非法索引段一律**响亮拒绝**并说清为什么——退化成「在 X 上找不到名为 Y 的字段」
        /// 那种答非所问的原因，比不说更糟。
        /// </summary>
        [Test]
        public void 非法索引段被拒绝()
        {
            Assert.That(Reject("Items.Array"), Does.Contain("Array"), "以 Array 结尾。");
            Assert.That(Reject("Items.Array.size"), Does.Contain("data[i]"), "只认 data[i] 形态。");
            Assert.That(Reject("Items.Array.data[x]"), Does.Contain("下标"), "下标不是数字。");
            Assert.That(Reject("Items.Array.data[]"), Does.Contain("下标"), "空下标。");
            Assert.That(Reject("Items.Array.data[-1]"), Does.Contain("下标"), "负数下标。");
            Assert.That(Reject("Items.data[0]"), Does.Contain("成对"), "data[0] 前面没有 Array。");
            Assert.That(
                Reject("Stats.Array.data[0]"),
                Does.Contain("Array"),
                "前置类型不是集合，按普通字段名处理（于是「找不到字段」）。");
            Assert.That(Reject("Matrix.Array.data[0]"), Does.Contain("多维"), "多维数组不做。");
        }

        /// <summary>末段是多态引用时**读到的是实例**——路径不必被拒（读路径那一批的开闸处）。</summary>
        [Test]
        public void 多态引用末段读到实例()
        {
            var fixture = new PathFixture { Payload = new PathStats { Hp = 10 } };

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Payload", out var accessor, out var reason), Is.True, reason);
            Assert.That(accessor.Read(fixture), Is.SameAs(fixture.Payload), "读到的是槽位里那个活实例。");
            Assert.That(accessor.Member.Name, Is.EqualTo("Payload"));
        }

        /// <summary>
        /// 末段是多态引用时 <c>ValueType</c> 是**声明类型**——接口槽位上它不等于实例的类型。
        /// </summary>
        /// <remarks>
        /// 这是契约不是缺陷：要「实例是什么类型」走 <c>NestedInstanceScope.InstanceTypeOf</c>
        /// （按名找成员/方法的那几处用的就是它）。
        /// </remarks>
        [Test]
        public void 多态引用末段的ValueType是声明类型()
        {
            var fixture = new PathFixture { PayloadOfInterface = new PathPayload() };

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "PayloadOfInterface", out var accessor, out var reason), Is.True, reason);
            Assert.That(accessor.Read(fixture), Is.SameAs(fixture.PayloadOfInterface));
            Assert.That(accessor.ValueType, Is.EqualTo(typeof(IPathPayload)), "静态类型契约：声明类型。");
        }

        /// <summary>空槽位给 <c>null</c>（引用类型的空传播），不抛。</summary>
        [Test]
        public void 多态引用空槽位给null()
        {
            var fixture = new PathFixture();

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Payload", out var accessor, out var reason), Is.True, reason);
            Assert.DoesNotThrow(() => Assert.That(accessor.Read(fixture), Is.Null));
        }

        /// <summary>**穿过**多态段继续下钻：不给具体类型时响亮拒绝并说明（类型只有调用方知道）。</summary>
        [Test]
        public void 穿过多态段不给具体类型时拒绝并说明()
        {
            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "PayloadOfInterface.Hp", out var accessor, out var reason), Is.False);
            Assert.That(accessor, Is.Null);
            Assert.That(reason, Does.Contain("具体类型"));
        }

        /// <summary>给了具体类型就按它换基读下去——且换实例后跟着走（每帧现读）。</summary>
        [Test]
        public void 穿过多态段按具体类型下钻()
        {
            var fixture = new PathFixture { PayloadOfInterface = new PathPayload { Hp = 10 } };

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "PayloadOfInterface.Hp", new[] { typeof(PathPayload) },
                out var accessor, out var reason), Is.True, reason);
            Assert.That(accessor.Read(fixture), Is.EqualTo(10));

            fixture.PayloadOfInterface = new PathPayload { Hp = 99 };
            Assert.That(accessor.Read(fixture), Is.EqualTo(99), "换实例后跟着走（每帧现读）。");
        }

        /// <summary>
        /// 具体类型对不上时**按空链处置、不抛**（收窄守卫那一格）：后面照走既有空传播——
        /// 值类型字段读到 <c>default</c>，不是别人实例的值、更不是 <c>InvalidCastException</c>。
        /// </summary>
        [Test]
        public void 具体类型对不上时按空链处置不抛()
        {
            var fixture = new PathFixture { PayloadOfInterface = new PathPayload { Hp = 5 } };

            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "PayloadOfInterface.Hp", new[] { typeof(PathPayloadAlt) },
                out var accessor, out var reason), Is.True, reason);
            Assert.DoesNotThrow(() => Assert.That(accessor.Read(fixture), Is.EqualTo(0), "不是 5——没拿别人实例的值。"));
        }

        /// <summary>路径超过深度上限被拒绝。</summary>
        [Test]
        public void 路径超过深度上限被拒绝()
        {
            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Deep.B.C.D.E", out var accessor, out var reason), Is.False);
            Assert.That(accessor, Is.Null);
            Assert.That(reason, Does.Contain("4"));
        }

        /// <summary>
        /// 属性段不被接受——路径的每一段都必须是**实例字段**。
        /// </summary>
        /// <remarks>
        /// 嵌套层的成员本来就全是字段（来自 Unity 的序列化迭代器），故这条收窄不影响用途；
        /// 要认属性就用单段版的 <c>TryCreate</c>。
        /// </remarks>
        [Test]
        public void 属性段不被接受()
        {
            Assert.That(ReflectedAccessor.TryCreatePath(
                typeof(PathFixture), "Stats.Doubled", out var accessor, out var reason), Is.False);
            Assert.That(accessor, Is.Null);
            Assert.That(reason, Does.Contain("实例字段"));
        }

        /// <summary>空路径与空类型被拒绝，不抛。</summary>
        [Test]
        public void 空路径被拒绝()
        {
            Assert.DoesNotThrow(() =>
            {
                Assert.That(ReflectedAccessor.TryCreatePath(
                    typeof(PathFixture), null, out _, out var reason), Is.False);
                Assert.That(reason, Is.Not.Null.And.Not.Empty);

                Assert.That(ReflectedAccessor.TryCreatePath(
                    null, "Stats", out _, out _), Is.False);
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

        /// <summary>按名取夹具上的成员并试着编译成强类型布尔读取器。</summary>
        /// <param name="name">成员名。</param>
        /// <param name="reader">编译出的读取器。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryCreateBoolean(string name, out Func<object, bool> reader, out string reason)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            var members = typeof(AccessorFixture).GetMember(name, Flags);
            Assert.That(members, Is.Not.Empty, $"夹具上没有名叫 {name} 的成员。");

            return ReflectedAccessor.TryCreateBooleanReader(members[0], out reader, out reason);
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

        /// <summary>编译一条**应当失败**的路径，返回原因。</summary>
        /// <param name="path">路径。</param>
        /// <returns>失败原因。</returns>
        private static string Reject(string path)
        {
            Assert.That(
                ReflectedAccessor.TryCreatePath(typeof(PathFixture), path, out var accessor, out var reason),
                Is.False,
                $"「{path}」不该被接受。");
            Assert.That(accessor, Is.Null);
            Assert.That(reason, Is.Not.Null.And.Not.Empty, $"「{path}」被拒时要有原因。");

            return reason;
        }

        #endregion
    }

    /// <summary>取值测试用的夹具：私有字段、私有属性、静态成员、索引器、只写属性各一。</summary>
    internal sealed class AccessorFixture
    {
        /// <summary>静态计数——用来验证静态成员的目标无关性。</summary>
        public static int StaticCount = 5;

        /// <summary>布尔字段——条件的求值走的就是这条。</summary>
        public bool Flag = true;

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

    /// <summary>路径访问器的夹具：两层嵌套、值类型中段、数组、以及一条撞深度上限的长链。</summary>
    internal sealed class PathFixture
    {
        /// <summary>一层嵌套。</summary>
        public PathStats Stats = new PathStats();

        /// <summary>中段是值类型的嵌套。</summary>
        public PathHolder Holder = new PathHolder();

        /// <summary>数组——索引段的对照（<c>Items.Array.data[0]</c> 读得到 1）。</summary>
        public int[] Items = { 1, 2, 3 };

        /// <summary><c>List&lt;T&gt;</c> 形态的集合——元素节点路径（<c>Array.data[i]</c>）的主战场。</summary>
        public List<PathStats> StatsList = new List<PathStats>
        {
            new PathStats { Hp = 11 },
            new PathStats { Hp = 22 },
        };

        /// <summary>中段是值类型的元素数组——索引 + 值类型段的组合。</summary>
        public PathHolder[] Holders = { new PathHolder(), new PathHolder() };

        /// <summary>深度 2：外层元素里嵌着内层集合（两组 <c>Array.data[i]</c> 的主战场）。</summary>
        public List<PathOuter> Outers = new List<PathOuter> { new PathOuter() };

        /// <summary>多维数组——索引段要拒绝它（序列化系统本来就看不见它，节点也不会走这条路径）。</summary>
        public int[,] Matrix = new int[2, 2];

        /// <summary>多态引用（具体类槽位）——末段读实例与空槽位的落点。</summary>
        [UnityEngine.SerializeReference]
        public PathStats Payload;

        /// <summary>多态引用（接口槽位）——`ValueType` 是声明类型、穿段要具体类型两处的落点。</summary>
        [UnityEngine.SerializeReference]
        public IPathPayload PayloadOfInterface;

        /// <summary>撞深度上限用的长链（<c>Deep.B.C.D.E</c> 是五段）。</summary>
        public DeepA Deep = new DeepA();
    }

    /// <summary>路径夹具的一层嵌套。</summary>
    internal sealed class PathStats
    {
        /// <summary>一个值。</summary>
        public int Hp = 10;

        /// <summary>再深一层。</summary>
        public PathInner Inner = new PathInner();

        /// <summary>属性——路径段必须是**字段**，故它读不到。</summary>
        public int Doubled => Hp * 2;
    }

    /// <summary>路径夹具的深层。</summary>
    internal sealed class PathInner
    {
        /// <summary>一个值。</summary>
        public string Name = "内层";
    }

    /// <summary>接口槽位的类型——多态引用的主战场（声明类型是接口）。</summary>
    internal interface IPathPayload
    {
    }

    /// <summary>接口槽位的一个实现：穿段下钻读的就是它。</summary>
    internal sealed class PathPayload : IPathPayload
    {
        /// <summary>穿段下钻的目标字段。</summary>
        public int Hp = 5;
    }

    /// <summary>接口槽位的另一个实现——「具体类型对不上」的那一格。</summary>
    internal sealed class PathPayloadAlt : IPathPayload
    {
        /// <summary>某个字段（内容不重要，类型不同才是要点）。</summary>
        public int Hp = -1;
    }

    /// <summary>值类型成员——中段是 struct 时的读法。</summary>
    internal struct PathValue
    {
        /// <summary>一个值。</summary>
        public int Number;
    }

    /// <summary>元素层深度 2 的路径夹具：外层元素里再嵌一个集合（还有一组值类型元素）。</summary>
    internal sealed class PathOuter
    {
        /// <summary>内层集合——两组索引对的直接对象。</summary>
        public List<PathStats> Inner = new List<PathStats>
        {
            new PathStats { Hp = 111 },
            new PathStats { Hp = 222 },
        };

        /// <summary>值类型元素的数组——两组索引对 + 值类型元素的组合。</summary>
        public PathValue[] Values = { new PathValue { Number = 7 } };
    }

    /// <summary>持有值类型成员的复合类型。</summary>
    internal sealed class PathHolder
    {
        /// <summary>值类型成员。</summary>
        public PathValue Value;
    }

    /// <summary>深度链的第一节。</summary>
    internal sealed class DeepA
    {
        /// <summary>下一节。</summary>
        public DeepB B = new DeepB();
    }

    /// <summary>深度链的第二节。</summary>
    internal sealed class DeepB
    {
        /// <summary>下一节。</summary>
        public DeepC C = new DeepC();
    }

    /// <summary>深度链的第三节。</summary>
    internal sealed class DeepC
    {
        /// <summary>下一节。</summary>
        public DeepD D = new DeepD();
    }

    /// <summary>深度链的第四节。</summary>
    internal sealed class DeepD
    {
        /// <summary>末端。</summary>
        public int E;
    }
}
