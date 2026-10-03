using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 特性处理器层：发现、顺序、以及两个钩子的分工。
    /// <para>
    /// 所有测试处理器都以**自定义特性为门**——只有当属性/父级真的带了那个特性才动作。
    /// 这不是洁癖：处理器注册表扫全部编辑器程序集，测试程序集里的处理器同样会被扫到，
    /// 一个不设门的处理器会污染**其它所有** fixture 构建的树。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AttributeProcessorTests
    {
        #region Private Fields

        private ProcessorFixture _target;
        private ChildProcessorFixture _childTarget;

        #endregion

        #region Setup / Teardown

        /// <summary>建立资产并清空探针记录。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ProcessorFixture>();
            _childTarget = ScriptableObject.CreateInstance<ChildProcessorFixture>();

            ProbeProcessor.ResetCalls();
            ChildProbeProcessor.ResetCalls();
            OrderProbeProcessor.ResetCalls();
            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        /// <summary>销毁资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                UnityEngine.Object.DestroyImmediate(_target);
                _target = null;
            }

            if (_childTarget != null)
            {
                UnityEngine.Object.DestroyImmediate(_childTarget);
                _childTarget = null;
            }

            ProbeProcessor.ResetCalls();
            ChildProbeProcessor.ResetCalls();
            OrderProbeProcessor.ResetCalls();
            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region 发现

        /// <summary>
        /// 本测试程序集里定义的处理器被自动发现——即「使用方零注册扩展」这条承诺。
        /// </summary>
        [Test]
        public void Registry_DiscoversProcessorFromThisAssembly()
        {
            Assert.That(Find<ProbeProcessor>(), Is.Not.Null,
                "本程序集里定义的处理器未被发现，说明扫描范围被收窄了。");
        }

        /// <summary>
        /// 抽象处理器不被实例化。
        /// </summary>
        [Test]
        public void Registry_SkipsAbstractTypes()
        {
            Assert.That(Find<AbstractProbeProcessor>(), Is.Null);
        }

        /// <summary>
        /// 没有公开无参构造的处理器被跳过，而不是让扫描整个失败。
        /// </summary>
        [Test]
        public void Registry_SkipsTypesWithoutPublicParameterlessConstructor()
        {
            Assert.That(Find<NoDefaultConstructorProcessor>(), Is.Null);
        }

        /// <summary>
        /// 注册结果按优先级升序，顺序确定。
        /// </summary>
        [Test]
        public void Registry_OrdersByAscendingPriority()
        {
            var processors = AttributeProcessorRegistry.Processors;

            for (var i = 1; i < processors.Length; i++)
            {
                Assert.That(
                    processors[i - 1].ProcessorPriority,
                    Is.LessThanOrEqualTo(processors[i].ProcessorPriority),
                    $"第 {i - 1} 与 {i} 条的优先级顺序不对。");
            }
        }

        #endregion

        #region 两个钩子的分工

        /// <summary>
        /// 属性自身带特性时走「自身」钩子。
        /// </summary>
        [Test]
        public void ProcessSelf_FiresForPropertyWithAttribute()
        {
            BuildTree(_target);

            Assert.That(ProbeProcessor.Calls, Is.EquivalentTo(new[] { "probed" }));
        }

        /// <summary>
        /// 不带该特性的属性不触发。
        /// </summary>
        [Test]
        public void ProcessSelf_DoesNotFireWithoutAttribute()
        {
            BuildTree(_childTarget);

            Assert.That(ProbeProcessor.Calls, Is.Empty);
        }

        /// <summary>
        /// **父级**带特性时走「子成员」钩子，且对每个成员各触发一次。
        /// <para>
        /// 这条钉的是钩子的分工：<c>ProcessChildMember</c> 的触发者是**父属性**而不是子成员。
        /// 弄反了的话类级分组分发就无从实现——那种情况下父级特性永远不会被看到。
        /// </para>
        /// </summary>
        [Test]
        public void ProcessChildMember_FiresForEveryChildWhenParentHasAttribute()
        {
            BuildTree(_childTarget);

            Assert.That(ChildProbeProcessor.Calls, Does.Contain("first"));
            Assert.That(ChildProbeProcessor.Calls, Does.Contain("second"));
            Assert.That(ChildProbeProcessor.Calls.Count, Is.EqualTo(2));
        }

        /// <summary>
        /// 父级不带该特性时「子成员」钩子不触发。
        /// </summary>
        [Test]
        public void ProcessChildMember_DoesNotFireWithoutParentAttribute()
        {
            BuildTree(_target);

            Assert.That(ChildProbeProcessor.Calls, Is.Empty);
        }

        #endregion

        #region 顺序

        /// <summary>
        /// **每个成员先跑「自身」再跑「父级注入」。**
        /// <para>
        /// 顺序是契约：注入的特性不该影响「这个成员自己有什么」的判断；反过来，
        /// 后跑的注入能被已经跑过的处理器看到。改顺序会静默改变处理器之间的可见性，
        /// 所以在这里钉住，而不是写在注释里指望别人读到。
        /// </para>
        /// </summary>
        [Test]
        public void Processors_RunSelfBeforeChildMember()
        {
            var fixture = ScriptableObject.CreateInstance<OrderFixture>();

            try
            {
                BuildTree(fixture);

                var order = OrderProbeProcessor.Calls;

                // 三条而不是两条：根节点自身也带 [OrderProbe]（它必须是父级，否则
                // 「父级注入」这个钩子根本不会触发），所以它会先贡献一次「自身」。
                // 要验的是**同一个成员**身上两者谁先。
                Assert.That(order.Count, Is.EqualTo(3), $"应恰好三次调用，实得：{string.Join(", ", order)}");
                Assert.That(order[0], Is.EqualTo("self"), "根节点自身的钩子最先跑。");
                Assert.That(order[1], Is.EqualTo("self"), "成员的自身钩子先于该成员的父级注入钩子。");
                Assert.That(order[2], Is.EqualTo("child"), "父级注入钩子后跑。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>构建被测的树。</summary>
        /// <param name="target">目标资产。</param>
        /// <returns>属性树。</returns>
        private static PropertyTree BuildTree(ScriptableObject target)
        {
            return PropertyTree.Create(new SerializedObject(target));
        }

        /// <summary>在注册表里查找指定类型的处理器。</summary>
        /// <typeparam name="T">处理器类型。</typeparam>
        /// <returns>找到的处理器；未找到返回 <c>null</c>。</returns>
        private static AttributeProcessor Find<T>() where T : AttributeProcessor
        {
            foreach (var processor in AttributeProcessorRegistry.Processors)
            {
                if (processor is T)
                {
                    return processor;
                }
            }

            return null;
        }

        #endregion
    }

    /// <summary>
    /// 探针特性——只有带它的属性才会触发 <see cref="ProbeProcessor"/>。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class ProbeAttribute : Attribute
    {
    }

    /// <summary>
    /// 类级探针特性——只有带它的<strong>类型</strong>才会触发 <see cref="ChildProbeProcessor"/>。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class ClassProbeAttribute : Attribute
    {
    }

    /// <summary>
    /// 记录「自身」钩子被调用的处理器。
    /// </summary>
    internal sealed class ProbeProcessor : AttributeProcessor<ProbeAttribute>
    {
        /// <summary>调用记录。</summary>
        public static readonly List<string> Calls = new List<string>();

        /// <summary>清空记录。</summary>
        public static void ResetCalls() => Calls.Clear();

        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, ProbeAttribute attribute, IList<Attribute> attributes)
        {
            Calls.Add(property.Name);
        }
    }

    /// <summary>
    /// 记录「子成员」钩子被调用的处理器。
    /// </summary>
    internal sealed class ChildProbeProcessor : AttributeProcessor<ClassProbeAttribute>
    {
        /// <summary>调用记录。</summary>
        public static readonly List<string> Calls = new List<string>();

        /// <summary>清空记录。</summary>
        public static void ResetCalls() => Calls.Clear();

        /// <inheritdoc/>
        protected override void ProcessChildMember(
            InspectorProperty parentProperty,
            MemberInfo member,
            ClassProbeAttribute attribute,
            IList<Attribute> attributes)
        {
            Calls.Add(member.Name);
        }
    }

    /// <summary>
    /// 抽象处理器：不应被实例化。
    /// </summary>
    internal abstract class AbstractProbeProcessor : AttributeProcessor<ProbeAttribute>
    {
    }

    /// <summary>
    /// 没有公开无参构造的处理器：不应被实例化。
    /// </summary>
    internal sealed class NoDefaultConstructorProcessor : AttributeProcessor<ProbeAttribute>
    {
        /// <summary>构造，故意只提供带参版本。</summary>
        /// <param name="unused">占位参数。</param>
        public NoDefaultConstructorProcessor(int unused)
        {
            _ = unused;
        }
    }

    /// <summary>
    /// 顺序探针特性：同时挂在类型与成员上。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field)]
    internal sealed class OrderProbeAttribute : Attribute
    {
    }

    /// <summary>
    /// 记录两个钩子先后顺序的处理器。
    /// </summary>
    /// <remarks>
    /// <c>ProcessorPriority</c> 刻意设成负值：注册表按优先级升序跑，
    /// 本处理器要在其它探针处理器之前跑，记录才干净。
    /// </remarks>
    internal sealed class OrderProbeProcessor : AttributeProcessor<OrderProbeAttribute>
    {
        /// <summary>调用记录。</summary>
        public static readonly List<string> Calls = new List<string>();

        /// <summary>清空记录。</summary>
        public static void ResetCalls() => Calls.Clear();

        /// <inheritdoc/>
        public override float ProcessorPriority => -1000f;

        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, OrderProbeAttribute attribute, IList<Attribute> attributes)
        {
            Calls.Add("self");
        }

        /// <inheritdoc/>
        protected override void ProcessChildMember(
            InspectorProperty parentProperty,
            MemberInfo member,
            OrderProbeAttribute attribute,
            IList<Attribute> attributes)
        {
            Calls.Add("child");
        }
    }

    /// <summary>
    /// 探针测试用资产：一个带探针的字段、一个不带。
    /// </summary>
    internal sealed class ProcessorFixture : ScriptableObject
    {
        /// <summary>带探针的字段。</summary>
        [Probe]
        public int probed;

        /// <summary>不带探针的字段。</summary>
        public int plain;
    }

    /// <summary>
    /// 子成员钩子测试用资产：类型上带探针，字段上不带。
    /// </summary>
    [ClassProbe]
    internal sealed class ChildProcessorFixture : ScriptableObject
    {
        /// <summary>第一个字段。</summary>
        public int first;

        /// <summary>第二个字段。</summary>
        public int second;
    }

    /// <summary>
    /// 顺序测试用资产：类型与成员**同时**带顺序探针。
    /// </summary>
    [OrderProbe]
    internal sealed class OrderFixture : ScriptableObject
    {
        /// <summary>带探针的字段。</summary>
        [OrderProbe]
        public int value;
    }
}
