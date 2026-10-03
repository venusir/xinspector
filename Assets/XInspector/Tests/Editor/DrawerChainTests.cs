using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 绘制器链的装配顺序与执行语义。
    /// <para>
    /// 这些用例全部**不碰 GUI**——链条机制是「特性序列 → 有序格子序列」的纯函数，
    /// 以及在此之上的游标推进。把最容易出错的排序与包裹逻辑放在无头可测的位置，
    /// 是这套架构刻意的取舍。
    /// </para>
    /// </summary>
    [TestFixture]
    public class DrawerChainTests
    {
        #region Setup / Teardown

        /// <summary>
        /// 复位绘制器注册表这个静态门面——本仓约定：fixture 必须复位它触碰的静态门面，
        /// 否则用例之间会互相污染。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DrawerTypeRegistry.Reset();
        }

        /// <summary>
        /// 同样复位，避免本 fixture 的痕迹影响后续 fixture。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region 装配顺序

        /// <summary>
        /// 权重小的排在前面，末端绘制器固定最后。
        /// </summary>
        [Test]
        public void Build_OrdersByAscendingPriorityWithTerminalLast()
        {
            var log = new List<string>();
            var property = PropertyTestFactory.CreateRecordingMember("health", log, new RecordingAttribute("A", log));

            var chain = PropertyTestFactory.AttachChain(property);

            Assert.That(chain.Count, Is.EqualTo(3), "两个特性绘制器 + 一个末端。");
            Assert.That(chain.Entries[0].Drawer, Is.InstanceOf<OuterRecordingDrawer>(), "权重 -500 的应排最外。");
            Assert.That(chain.Entries[1].Drawer, Is.InstanceOf<InnerRecordingDrawer>(), "权重 -100 的应排中间。");
            Assert.That(chain.Entries[2].Drawer, Is.SameAs(RecordingTerminalDrawer.Instance), "末端必须最后。");
        }

        /// <summary>
        /// 没有特性的属性也必须有链，且链里只有末端——
        /// 「链条永不为空」是「不会有属性静默地什么都不画」的全部依据。
        /// </summary>
        [Test]
        public void Build_PropertyWithoutAttributes_GetsTerminalOnlyChain()
        {
            var property = PropertyTestFactory.CreateMember("health");

            var chain = PropertyTestFactory.AttachChain(property);

            Assert.That(chain.Count, Is.EqualTo(1));
            Assert.That(chain.Entries[0].Drawer, Is.SameAs(RecordingTerminalDrawer.Instance));
        }

        /// <summary>
        /// 每个特性实例各自配一份绘制器格子，而不是「每个绘制器只入链一次」。
        /// <para>
        /// 这条区分很重要：属性上可以挂多个同类型特性（<c>AllowMultiple = true</c>），
        /// 若按绘制器去重，第二个 <c>[BoxGroup]</c> 就会被吞掉。
        /// </para>
        /// </summary>
        [Test]
        public void Build_PairsEachAttributeInstanceWithItsOwnEntry()
        {
            var log = new List<string>();
            var property = PropertyTestFactory.CreateRecordingMember(
                "health",
                log,
                new RecordingAttribute("A", log),
                new RecordingAttribute("B", log));

            var chain = PropertyTestFactory.AttachChain(property);

            Assert.That(chain.Count, Is.EqualTo(5), "两个特性 × 两个绘制器 + 末端。");
            Assert.That(chain.Entries[0].Attribute, Is.SameAs(property.Attributes[0]));
            Assert.That(chain.Entries[1].Attribute, Is.SameAs(property.Attributes[1]));
            Assert.That(chain.Entries[2].Attribute, Is.SameAs(property.Attributes[0]));
            Assert.That(chain.Entries[3].Attribute, Is.SameAs(property.Attributes[1]));
        }

        #endregion

        #region 执行语义

        /// <summary>
        /// 执行呈层层包裹：先进入的绘制器最后退出。
        /// <para>
        /// 这正是 <c>[BoxGroup]</c> 能包住 <c>[Title]</c> 的机制——分组绘制器在
        /// <c>CallNextDrawer</c> 之前画框、之后收框，中间的一切自然落在框内。
        /// </para>
        /// </summary>
        [Test]
        public void Draw_ExecutesAsNestedWrapping()
        {
            var log = new List<string>();
            var property = PropertyTestFactory.CreateRecordingMember("health", log, new RecordingAttribute("A", log));
            var chain = PropertyTestFactory.AttachChain(property);

            chain.Draw(property, new GUIContent("Health"));

            Assert.That(log, Is.EqualTo(new[]
            {
                "enter:outer:A",
                "enter:inner:A",
                "terminal",
                "exit:inner:A",
                "exit:outer:A",
            }));
        }

        /// <summary>
        /// 多个特性时，先后由「权重 → 声明顺序」两级决定。
        /// </summary>
        [Test]
        public void Draw_MultipleAttributes_OrdersByPriorityThenDeclaration()
        {
            var log = new List<string>();
            var property = PropertyTestFactory.CreateRecordingMember(
                "health",
                log,
                new RecordingAttribute("A", log),
                new RecordingAttribute("B", log));
            var chain = PropertyTestFactory.AttachChain(property);

            chain.Draw(property, new GUIContent("Health"));

            Assert.That(log, Is.EqualTo(new[]
            {
                "enter:outer:A",
                "enter:outer:B",
                "enter:inner:A",
                "enter:inner:B",
                "terminal",
                "exit:inner:B",
                "exit:inner:A",
                "exit:outer:B",
                "exit:outer:A",
            }));
        }

        /// <summary>
        /// 重入：绘制器在绘制途中再整链画一次同一属性，游标必须被正确保存与恢复。
        /// <para>
        /// 没有保存/恢复时，内层走完后游标停在链尾，外层随后的 <c>CallNextDrawer</c>
        /// 就会越界抛异常——所以这条用例确实挡得住该缺陷，而不是走个形式。
        /// </para>
        /// </summary>
        [Test]
        public void Draw_RestoresCursorAfterReentrancy()
        {
            var log = new List<string>();
            var property = PropertyTestFactory.CreateRecordingMember("health", log, new ReentrantAttribute(log));
            var chain = PropertyTestFactory.AttachChain(property);

            Assert.DoesNotThrow(() => chain.Draw(property, new GUIContent("Health")));

            Assert.That(log, Is.EqualTo(new[]
            {
                "enter",
                "enter",
                "terminal",
                "exit",
                "terminal",
                "exit",
            }));
        }

        /// <summary>
        /// 链尾仍调用下一个绘制器时必须抛异常。
        /// <para>
        /// 这种情形意味着构建期漏装了末端绘制器——若静默忽略，症状会是
        /// 「某属性少画了一部分」，而异常把原因直接点出来。
        /// </para>
        /// </summary>
        [Test]
        public void Draw_CallingNextPastChainEnd_Throws()
        {
            var log = new List<string>();
            var property = PropertyTestFactory.CreateRecordingMember("health", log, new DoubleNextAttribute(log));
            var chain = PropertyTestFactory.AttachChain(property);

            Assert.Throws<InvalidOperationException>(() => chain.Draw(property, new GUIContent("Health")));
            Assert.That(log, Does.Contain("first-next-returned"), "第一次调用应正常返回。");
            Assert.That(log, Does.Not.Contain("second-next-returned"), "第二次调用不应返回。");
        }

        /// <summary>
        /// 绘制器抛异常后，链仍可正常再次绘制——游标靠 <c>finally</c> 恢复。
        /// </summary>
        [Test]
        public void Draw_RestoresCursorAfterException()
        {
            var log = new List<string>();
            var property = PropertyTestFactory.CreateRecordingMember("health", log, new DoubleNextAttribute(log));
            var chain = PropertyTestFactory.AttachChain(property);

            Assert.Throws<InvalidOperationException>(() => chain.Draw(property, new GUIContent("Health")));

            var afterFailure = log.Count;
            Assert.Throws<InvalidOperationException>(() => chain.Draw(property, new GUIContent("Health")));

            Assert.That(log.Count, Is.EqualTo(afterFailure * 2), "第二次绘制应复现同样的调用序列，说明游标未残留。");
        }

        /// <summary>
        /// 不可见的属性直接返回，不进入链条。
        /// </summary>
        [Test]
        public void Draw_WhenInvisible_SkipsEntireChain()
        {
            var log = new List<string>();
            var property = PropertyTestFactory.CreateRecordingMember("health", log, new RecordingAttribute("A", log));
            PropertyTestFactory.AttachChain(property);
            property.State.SetVisible(false);

            property.Draw();

            Assert.That(log, Is.Empty);
        }

        #endregion
    }
}
