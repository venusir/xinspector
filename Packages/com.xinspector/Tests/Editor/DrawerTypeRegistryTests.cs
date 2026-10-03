using NUnit.Framework;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 绘制器发现与注册。
    /// <para>
    /// 这里最重要的一条是「本测试程序集里定义的绘制器会被发现」——它验证的不是某个实现细节，
    /// 而是**对使用方的承诺**：在任意编辑器程序集里写一个绘制器，无需注册调用即可生效。
    /// 若扫描范围哪天被收窄成「只扫本包」，这条会立刻变红。
    /// </para>
    /// </summary>
    [TestFixture]
    public class DrawerTypeRegistryTests
    {
        #region Setup / Teardown

        /// <summary>复位静态门面。</summary>
        [SetUp]
        public void SetUp()
        {
            DrawerTypeRegistry.Reset();
        }

        /// <summary>复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region 发现

        /// <summary>
        /// 本测试程序集里定义的绘制器被自动发现——即「使用方零注册扩展」这条承诺。
        /// </summary>
        [Test]
        public void Registry_DiscoversDrawerFromThisAssembly()
        {
            Assert.That(Find<OuterRecordingDrawer>(), Is.Not.Null,
                "本程序集里定义的绘制器未被发现，说明扫描范围被收窄了。");
        }

        /// <summary>
        /// 抽象绘制器不被实例化。
        /// </summary>
        [Test]
        public void Registry_SkipsAbstractTypes()
        {
            Assert.That(Find<AbstractTestDrawer>(), Is.Null);
        }

        /// <summary>
        /// 没有公开无参构造函数的绘制器被跳过，而不是让扫描整个失败。
        /// <para>
        /// 取舍是「跳过并告警」而非抛异常：一个写坏的第三方绘制器不该让整个 Inspector 瘫掉。
        /// </para>
        /// </summary>
        [Test]
        public void Registry_SkipsTypesWithoutPublicParameterlessConstructor()
        {
            Assert.That(Find<NoDefaultConstructorDrawer>(), Is.Null);
        }

        /// <summary>
        /// 发现的绘制器是**同一实例**，反复读取不会重新扫描、也不会新建。
        /// <para>
        /// 「共享单例」是内存上的硬要求，也是「绘制器不得持有可变字段」这条纪律的由来；
        /// 这条用例把前半句钉住。
        /// </para>
        /// </summary>
        [Test]
        public void Registry_ReusesSingletonInstances()
        {
            var first = Find<OuterRecordingDrawer>();
            var second = Find<OuterRecordingDrawer>();

            Assert.That(first, Is.Not.Null, "先确认它确实被发现了，否则下面的断言只是在测 null 解引用。");
            Assert.That(second, Is.Not.Null);
            Assert.That(first.Value.Drawer, Is.SameAs(second.Value.Drawer));
        }

        #endregion

        #region 权重解析

        /// <summary>
        /// 有 <see cref="DrawerPriorityAttribute"/> 时以它为准，虚属性被忽略。
        /// </summary>
        [Test]
        public void Priority_AttributeOverridesVirtualProperty()
        {
            var registered = Find<AttributeOverridesVirtualDrawer>();

            Assert.That(registered, Is.Not.Null);
            Assert.That(registered.Value.Priority.Value, Is.EqualTo(42d),
                "应取特性上的 42，而不是虚属性返回的 SuperPriority(-1000)。");
        }

        /// <summary>
        /// 没有特性时用虚属性声明的权重。
        /// </summary>
        [Test]
        public void Priority_FallsBackToVirtualProperty()
        {
            var registered = Find<VirtualPriorityDrawer>();

            Assert.That(registered, Is.Not.Null);
            Assert.That(registered.Value.Priority.Value, Is.EqualTo(-777d));
        }

        /// <summary>
        /// 注册结果按权重升序，且同权重时以类型名兜底，保证顺序确定。
        /// </summary>
        [Test]
        public void Registry_OrdersByAscendingPriority()
        {
            var registered = DrawerTypeRegistry.Registered;

            for (var i = 1; i < registered.Length; i++)
            {
                Assert.That(
                    registered[i - 1].Priority.CompareTo(registered[i].Priority),
                    Is.LessThanOrEqualTo(0),
                    $"第 {i - 1} 与 {i} 条的权重顺序不对。");
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 在注册表里查找指定类型的绘制器。
        /// </summary>
        /// <typeparam name="T">绘制器类型。</typeparam>
        /// <returns>找到的记录；未找到返回 <c>null</c>。</returns>
        private static RegisteredDrawer? Find<T>() where T : XInspectorDrawer
        {
            var registered = DrawerTypeRegistry.Registered;
            for (var i = 0; i < registered.Length; i++)
            {
                if (registered[i].Drawer is T)
                {
                    return registered[i];
                }
            }

            return null;
        }

        #endregion
    }
}
