using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 属性状态与属性树的释放通道：谁被释放、谁不被碰、重复释放，以及嵌套
    /// <c>Editor</c> 实例的端到端销毁。
    /// <para>
    /// 这条通道是内嵌编辑器一族的基建：绘制器不得持有可变字段，嵌套 <c>Editor</c> 实例只能
    /// 放进 <see cref="PropertyState"/> 的附加状态袋，而它必须被显式销毁。四个环节
    /// （状态 → 树 → 宿主/编辑器）里漏掉任何一个，症状都是「原生对象随每次选中/关闭累积」，
    /// 与漏改处看起来毫无关系——所以每一环都在这里有守卫。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PropertyTreeDisposalTests
    {
        #region Setup / Teardown

        private DisposalFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;

        /// <summary>建宿主资产与树；复位两个静态门面。</summary>
        [SetUp]
        public void SetUp()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();

            _target = ScriptableObject.CreateInstance<DisposalFixture>();
            _serializedObject = new SerializedObject(_target);
            _tree = PropertyTree.Create(_serializedObject);
        }

        /// <summary>释放树、序列化对象与资产，再复位一次静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            _tree?.Dispose();
            _tree = null;

            _serializedObject?.Dispose();
            _serializedObject = null;

            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 复位单个状态

        /// <summary>复位状态会释放其中实现 <see cref="IDisposable"/> 的附加状态。</summary>
        [Test]
        public void 复位状态会释放可释放的附加状态()
        {
            var state = _tree.Root.Children[0].State;
            var probe = state.GetOrCreate<DisposableProbeState>();

            state.Reset();

            Assert.That(probe.DisposeCount, Is.EqualTo(1));
            Assert.That(state.Get<DisposableProbeState>(), Is.Null, "袋已清空。");
        }

        /// <summary>
        /// 不实现 <see cref="IDisposable"/> 的状态只是被移出袋，对象本身不被动过
        /// ——复位不是「销毁一切」，只释放声明过要释放的那些。
        /// </summary>
        [Test]
        public void 复位后附加状态已清空()
        {
            var state = _tree.Root.Children[0].State;
            var plain = state.GetOrCreate<PlainProbeState>();
            plain.Value = 42;

            state.Reset();

            Assert.That(state.Get<PlainProbeState>(), Is.Null, "袋已清空。");
            Assert.That(plain.Value, Is.EqualTo(42), "对象本身没被动过。");
        }

        #endregion

        #region 释放整棵树

        /// <summary>释放树会走到每一个节点，分组节点也在内。</summary>
        [Test]
        public void 释放树会复位全部节点的状态()
        {
            var probes = new List<DisposableProbeState>();
            Collect(_tree.Root, probes);

            Assert.That(probes.Count, Is.GreaterThan(1), "宿主里不止一个节点（含分组节点）。");

            _tree.Dispose();

            for (var i = 0; i < probes.Count; i++)
            {
                Assert.That(probes[i].DisposeCount, Is.EqualTo(1), $"第 {i} 个节点没被释放。");
            }
        }

        /// <summary>重复释放是幂等的：每个状态只被释放一次。</summary>
        [Test]
        public void 重复释放是幂等的()
        {
            var probe = _tree.Root.Children[0].State.GetOrCreate<DisposableProbeState>();

            _tree.Dispose();
            _tree.Dispose();

            Assert.That(probe.DisposeCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 端到端：附加状态里持有真实 <c>Editor</c> 实例时，释放树会把它销毁。
        /// 这正是内嵌编辑器依赖的那条路径（生产代码里的状态与
        /// <see cref="EditorHolderState"/> 同形）。
        /// </summary>
        [Test]
        public void 释放树会销毁状态里的编辑器实例()
        {
            var subject = ScriptableObject.CreateInstance<DisposalFixture>();

            try
            {
                var editor = UnityEditor.Editor.CreateEditor(subject);
                var holder = _tree.Root.Children[0].State.GetOrCreate<EditorHolderState>();
                holder.Instance = editor;

                _tree.Dispose();

                Assert.That(holder.Instance, Is.Null, "持有者已清空引用。");
                Assert.That(editor == null, Is.True, "Unity 对象已销毁（按 == 判定为 null）。");
            }
            finally
            {
                Object.DestroyImmediate(subject);
            }
        }

        #endregion

        #region 销毁的连带效应

        /// <summary>
        /// 实测：销毁一个 <c>Editor</c> 实例会触发它的 <c>OnDisable</c>。
        /// <para>
        /// 内嵌编辑器画的那一层由 Unity 自己创建的编辑器承担，它释放自己的属性树正是靠这条
        /// 连带效应。若这条哪天不再成立，<c>XInspectorEditor</c> 需要补一个同样幂等的
        /// <c>OnDestroy</c> 兜底——那时这条测试会红，正是它的用处。
        /// </para>
        /// </summary>
        [Test]
        public void 销毁编辑器实例时其OnDisable被调用()
        {
            var subject = ScriptableObject.CreateInstance<DisposalFixture>();

            try
            {
                var editor = UnityEditor.Editor.CreateEditor(subject, typeof(DisposalProbeEditor));
                Assert.That(editor, Is.InstanceOf<DisposalProbeEditor>(), "探针编辑器应当被建出来。");

                DisposalProbeEditor.DisableCount = 0;
                Object.DestroyImmediate(editor);

                Assert.That(DisposalProbeEditor.DisableCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(subject);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>递归收集每个节点上的可释放状态探针。</summary>
        /// <param name="node">起始节点。</param>
        /// <param name="probes">收集结果。</param>
        private static void Collect(InspectorProperty node, List<DisposableProbeState> probes)
        {
            probes.Add(node.State.GetOrCreate<DisposableProbeState>());

            for (var i = 0; i < node.Children.Count; i++)
            {
                Collect(node.Children[i], probes);
            }
        }

        #endregion
    }

    #region 测试替身

    /// <summary>释放通道的测试宿主：一个平铺成员、一个分组、一个成员在分组里。</summary>
    internal class DisposalFixture : ScriptableObject
    {
        public int plain;

        [BoxGroup("组")]
        public int grouped;

        [BoxGroup("组")]
        public string alsoGrouped;
    }

    /// <summary>可释放的附加状态探针：只记释放次数。</summary>
    internal sealed class DisposableProbeState : IDisposable
    {
        /// <summary>被释放的次数。</summary>
        public int DisposeCount;

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeCount++;
        }
    }

    /// <summary>不带释放语义的附加状态探针。</summary>
    internal sealed class PlainProbeState
    {
        /// <summary>任意值，用来证明对象本身没被动过。</summary>
        public int Value;
    }

    /// <summary>
    /// 持有真实 <c>Editor</c> 实例的附加状态：释放时销毁它。
    /// 与生产代码里内嵌编辑器的状态同形（差别只是生产那边还要处理目标切换与标记位）。
    /// </summary>
    internal sealed class EditorHolderState : IDisposable
    {
        /// <summary>持有的编辑器实例。</summary>
        public UnityEditor.Editor Instance;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Instance != null)
            {
                // 编辑模式下必须用 DestroyImmediate——Destroy 会报「may not be called from edit mode」。
                Object.DestroyImmediate(Instance);
                Instance = null;
            }
        }
    }

    /// <summary>
    /// 观察「销毁是否触发 <c>OnDisable</c>」的探针编辑器。
    /// <para>
    /// 刻意不带 <c>[CustomEditor]</c>：只能按类型显式创建，因此不会影响别的测试，
    /// 也不会被自动接管的扫描碰到。
    /// </para>
    /// </summary>
    internal sealed class DisposalProbeEditor : UnityEditor.Editor
    {
        /// <summary><c>OnDisable</c> 被调用的次数。</summary>
        public static int DisableCount;

        private void OnDisable()
        {
            DisableCount++;
        }
    }

    #endregion
}
