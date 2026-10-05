using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 「分组装配之后的第二趟处理器」的路由守卫：只处理分组特性的处理器只对分组节点跑，
    /// 根与成员一个都不碰；第二趟排在 <c>[OnInspectorInit]</c> 之前。
    /// </summary>
    /// <remarks>
    /// 探针处理器带静态可变状态——处理器本该无状态（跨调用状态进 <see cref="PropertyState"/>），
    /// 这里是测试程序集里的**故意违规**，只为留下「谁被触发了」的证据。
    /// </remarks>
    [TestFixture]
    public class GroupProcessorPassTests
    {
        #region Fixture

        private PassProbeFixture _target;
        private PassProbeInitOrderFixture _initTarget;

        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<PassProbeFixture>();
            _initTarget = ScriptableObject.CreateInstance<PassProbeInitOrderFixture>();
            PassProbeProcessor.Reset();
            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            if (_initTarget != null)
            {
                Object.DestroyImmediate(_initTarget);
                _initTarget = null;
            }

            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region Tests

        /// <summary>
        /// 处理分组特性的处理器只在分组节点上被触发——成员与根都不该出现在记录里。
        /// </summary>
        /// <remarks>
        /// 记录里出现成员，就说明路由把这类处理器放回了第一趟；而分组节点在第一趟时还不存在，
        /// 「判据挂在分组节点上」这类需求会随之**静默失效**。
        /// </remarks>
        [Test]
        public void 处理分组特性的处理器只在分组节点上跑()
        {
            PropertyTree.Create(new SerializedObject(_target));

            Assert.That(
                PassProbeProcessor.Calls,
                Is.EqualTo(new[] { "Group:探测分组" }),
                "只该看见分组节点——声明处是成员、类级分发落在根上，两边都不该触发。");
        }

        /// <summary>
        /// 第二趟排在 <c>[OnInspectorInit]</c> 之前：Init 的契约是「整棵树建好之后」，
        /// 第二趟是建树的收尾（把状态定下来），排在它后面才成立。
        /// </summary>
        [Test]
        public void 第二趟在OnInspectorInit之前跑()
        {
            PropertyTree.Create(new SerializedObject(_initTarget));

            Assert.That(
                PassProbeProcessor.Calls,
                Is.EqualTo(new[] { "Group:探测分组", "Init" }),
                "状态该在 Init 之前定下来，否则 Init 里读到的可见性是未安装的空档。");
        }

        #endregion
    }

    /// <summary>
    /// 测试专用的分组特性——只有它会触发 <see cref="PassProbeProcessor"/>。
    /// </summary>
    internal sealed class PassProbeGroupAttribute : PropertyGroupAttribute
    {
        /// <summary>以路径构造。</summary>
        /// <param name="groupID">分组路径。</param>
        public PassProbeGroupAttribute(string groupID)
            : base(groupID)
        {
        }
    }

    /// <summary>
    /// 记录自己被哪类节点触发的处理器，形如 <c>Group:探测分组</c>。
    /// </summary>
    /// <remarks>
    /// 本该无状态（见处理器的三条纪律），测试夹具故意违规以留下证据——
    /// 它只匹配自己的探针特性，不会干扰别的 fixture。
    /// </remarks>
    internal sealed class PassProbeProcessor : AttributeProcessor<PassProbeGroupAttribute>
    {
        /// <summary>调用记录。</summary>
        public static readonly List<string> Calls = new List<string>();

        /// <summary>清空记录。</summary>
        public static void Reset() => Calls.Clear();

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property, PassProbeGroupAttribute attribute, IList<Attribute> attributes)
        {
            Calls.Add(property.Kind + ":" + property.Path);
        }
    }

    /// <summary>夹具：一个带分组探针的字段。</summary>
    internal sealed class PassProbeFixture : ScriptableObject
    {
        /// <summary>普通字段，让树有内容。</summary>
        public int value = 1;

        /// <summary>带分组探针的字段——分组装配会为它造出「探测分组」节点。</summary>
        [PassProbeGroup("探测分组")]
        public int probed;
    }

    /// <summary>夹具：分组探针之外再挂一个 <c>[OnInspectorInit]</c>，用来钉两趟的先后。</summary>
    internal sealed class PassProbeInitOrderFixture : ScriptableObject
    {
        /// <summary>带分组探针的字段。</summary>
        [PassProbeGroup("探测分组")]
        public int probed;

        /// <summary>建树完成后记一笔——顺序即断言对象。</summary>
        [OnInspectorInit]
        private void OnInit()
        {
            PassProbeProcessor.Calls.Add("Init");
        }
    }
}
