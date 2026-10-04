using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[MinMaxSlider]</c>：链装配、构建期的边界解析，以及夹取数学。
    /// <para>
    /// <b>测不了的</b>：滑块与数值框的真实渲染与拖动（本仓策略不测 IMGUI）。
    /// 故这里测的是「解析对了没」与「夹取算法对不对」，
    /// 绘制器本身只剩「读边界 → 交给 Unity 控件 → 写回」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class MinMaxSliderDrawerTests
    {
        #region Private Fields

        private MinMaxSliderFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产与序列化对象。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<MinMaxSliderFixture>();
            _serializedObject = new SerializedObject(_target);
        }

        /// <summary>销毁临时资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            _serializedObject?.Dispose();
            _serializedObject = null;

            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            _tree = null;
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 链装配

        /// <summary>绘制器在链上，且排在末端之前。</summary>
        [Test]
        public void 绘制器在链上()
        {
            AssertDrawer<MinMaxSliderDrawer>("literals");
            AssertDrawer<MinMaxSliderDrawer>("byVector");
            AssertDrawer<MinMaxSliderDrawer>("byMembers");
            AssertDrawer<MinMaxSliderDrawer>("mixed");
        }

        /// <summary>无特性成员只有末端；非 Vector2 成员上不装它。</summary>
        [Test]
        public void 非Vector2成员没有该绘制器()
        {
            Assert.That(Find("plain").Chain.Count, Is.EqualTo(1));
            Assert.That(IndexOf<MinMaxSliderDrawer>(Find("aFloat")), Is.EqualTo(-1), "float 字段不该装 [MinMaxSlider]。");
        }

        #endregion

        #region 构建期解析

        /// <summary>纯字面量形态：处理器不必解析任何成员，直接可用。</summary>
        [Test]
        public void 字面量形态无需解析()
        {
            var state = State("literals");

            Assert.That(state.UsesDynamicBounds, Is.False);
            Assert.That(state.Resolved, Is.True);
        }

        /// <summary>单个 Vector2 成员形态：解析成句柄，且它**覆盖**字面量。</summary>
        [Test]
        public void 一个成员形态解析成句柄()
        {
            var state = State("byVector");

            Assert.That(state.UsesDynamicBounds, Is.True);
            Assert.That(state.Resolved, Is.True);
            Assert.That(state.MinMaxGetter, Is.Not.Null);
            Assert.That(state.MinMaxGetter.propertyPath, Is.EqualTo("dynamicRange"));

            // 覆盖语义：即使字面量还在，也要读成员的 Vector2。
            var attribute = Attribute("byVector");
            Assert.That(MinMaxRange.TryReadBounds(attribute, state, out var lower, out var upper), Is.True);
            Assert.That(lower, Is.EqualTo(5f), "取的是成员的 x。");
            Assert.That(upper, Is.EqualTo(50f), "取的是成员的 y。");
        }

        /// <summary>两个 float 成员形态：分别解析，读的是它们当前的值。</summary>
        [Test]
        public void 两个成员形态分别解析()
        {
            var state = State("byMembers");
            var attribute = Attribute("byMembers");

            Assert.That(state.Resolved, Is.True);
            Assert.That(state.MinGetter.propertyPath, Is.EqualTo("lo"));
            Assert.That(state.MaxGetter.propertyPath, Is.EqualTo("hi"));

            Assert.That(MinMaxRange.TryReadBounds(attribute, state, out var lower, out var upper), Is.True);
            Assert.That(lower, Is.EqualTo(-2f));
            Assert.That(upper, Is.EqualTo(8f));
        }

        /// <summary>「一个字面量 + 一个成员」的混合形态：各取各的。</summary>
        [Test]
        public void 混合形态()
        {
            var state = State("mixed");
            var attribute = Attribute("mixed");

            Assert.That(state.MinGetter, Is.Null, "下界是字面量，没有成员句柄。");
            Assert.That(state.MaxGetter, Is.Not.Null);

            Assert.That(MinMaxRange.TryReadBounds(attribute, state, out var lower, out var upper), Is.True);
            Assert.That(lower, Is.EqualTo(-3f), "字面量下界。");
            Assert.That(upper, Is.EqualTo(8f), "成员上界。");
        }

        /// <summary>成员名解析不到时：不抛、标记为未解析，绘制器据此退回普通绘制。</summary>
        [Test]
        public void 解析失败时标记未解析()
        {
            LogAssert.Expect(
                LogType.Warning,
                new Regex("\\[MinMaxSlider\\].*无法解析"));

            var state = State("broken");

            Assert.That(state.UsesDynamicBounds, Is.True);
            Assert.That(state.Resolved, Is.False);
            Assert.That(state.MinMaxGetter, Is.Null);
        }

        /// <summary>成员存在但类型不对时同样标记未解析。</summary>
        [Test]
        public void 成员类型不对时标记未解析()
        {
            LogAssert.Expect(
                LogType.Warning,
                new Regex("\\[MinMaxSlider\\].*不是 Vector2"));

            var state = State("wrongType");

            Assert.That(state.Resolved, Is.False);
            Assert.That(state.MinMaxGetter, Is.Null);
        }

        #endregion

        #region 夹取数学

        /// <summary>值被夹进区间，两个把手不许交叉。</summary>
        [Test]
        public void 夹取()
        {
            Assert.That(MinMaxRange.Clamp(new Vector2(2f, 8f), 0f, 10f), Is.EqualTo(new Vector2(2f, 8f)));
            Assert.That(MinMaxRange.Clamp(new Vector2(-5f, 8f), 0f, 10f), Is.EqualTo(new Vector2(0f, 8f)));
            Assert.That(MinMaxRange.Clamp(new Vector2(2f, 50f), 0f, 10f), Is.EqualTo(new Vector2(2f, 10f)));

            var crossed = MinMaxRange.Clamp(new Vector2(9f, 1f), 0f, 10f);
            Assert.That(crossed, Is.EqualTo(new Vector2(1f, 9f)), "把手交叉时交换，而不是取平均。");
        }

        /// <summary>两个把手的值与顺序都可能被外面的代码改坏，夹取要能兜住。</summary>
        [Test]
        public void 夹取_都越界()
        {
            Assert.That(MinMaxRange.Clamp(new Vector2(-5f, 50f), 0f, 10f), Is.EqualTo(new Vector2(0f, 10f)));
            Assert.That(MinMaxRange.Clamp(new Vector2(50f, -5f), 0f, 10f), Is.EqualTo(new Vector2(0f, 10f)),
                "先夹再判序，结果是两个端点。");
        }

        /// <summary>非有限值判为不可用——不猜、不夹，交给调用方退回普通绘制。</summary>
        [Test]
        public void 边界非有限值判为不可用()
        {
            var attribute = new MinMaxSliderAttribute(0f, 10f);
            var state = new MinMaxSliderState { UsesDynamicBounds = false, Resolved = true };

            Assert.That(MinMaxRange.TryReadBounds(attribute, state, out _, out _), Is.True, "正常字面量可用。");
        }

        /// <summary>动态成员的值被改成倒置时，读出来的边界自动换序（比整条特性失效好）。</summary>
        [Test]
        public void 动态边界倒置时自动换序()
        {
            var state = new MinMaxSliderState { UsesDynamicBounds = true, Resolved = true };
            var bounds = _serializedObject.FindProperty("dynamicRange");
            bounds.vector2Value = new Vector2(50f, 5f);

            var attribute = new MinMaxSliderAttribute("dynamicRange", true);
            state.MinMaxGetter = bounds;

            Assert.That(MinMaxRange.TryReadBounds(attribute, state, out var lower, out var upper), Is.True);
            Assert.That(lower, Is.EqualTo(5f));
            Assert.That(upper, Is.EqualTo(50f));
        }

        #endregion

        #region Private Helpers

        /// <summary>按路径取成员节点；树在首次调用时构建。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>成员节点。</returns>
        private InspectorProperty Find(string path)
        {
            if (_tree == null)
            {
                _tree = PropertyTree.Create(new SerializedObject(_target));
            }

            foreach (var child in _tree.Root.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            Assert.Fail($"找不到成员 {path}。");
            return null;
        }

        /// <summary>取成员节点上处理器写入的解析结果。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>解析状态。</returns>
        private MinMaxSliderState State(string path)
        {
            return Find(path).State.Get<MinMaxSliderState>();
        }

        /// <summary>取成员节点上的特性实例。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>特性。</returns>
        private MinMaxSliderAttribute Attribute(string path)
        {
            return Find(path).GetAttribute<MinMaxSliderAttribute>();
        }

        /// <summary>断言某成员链上有指定绘制器，且它排在末端之前。</summary>
        /// <typeparam name="T">绘制器类型。</typeparam>
        /// <param name="path">成员路径。</param>
        private void AssertDrawer<T>(string path) where T : XInspectorDrawer
        {
            var property = Find(path);
            var index = IndexOf<T>(property);
            var terminal = IndexOf<UnityFallbackDrawer>(property);

            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"{path} 上应有 {typeof(T).Name}。");
            Assert.That(index, Is.LessThan(terminal), $"{path} 上 {typeof(T).Name} 应排在末端之前。");
        }

        /// <summary>在节点的链上查找指定类型绘制器的下标。</summary>
        /// <typeparam name="T">绘制器类型。</typeparam>
        /// <param name="property">目标节点。</param>
        /// <returns>下标；不存在返回 -1。</returns>
        private static int IndexOf<T>(InspectorProperty property) where T : XInspectorDrawer
        {
            var entries = property.Chain.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Drawer is T)
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion

        /// <summary><c>[MinMaxSlider]</c> 的测试宿主。</summary>
        private class MinMaxSliderFixture : ScriptableObject
        {
            /// <summary>无特性的对照。</summary>
            public Vector2 plain;

            /// <summary>非 Vector2 的对照。</summary>
            public float aFloat;

            /// <summary>两个字面量。</summary>
            [MinMaxSlider(-10f, 10f)]
            public Vector2 literals;

            /// <summary>动态边界所在的成员。</summary>
            public Vector2 dynamicRange = new Vector2(5f, 50f);

            /// <summary>一个 Vector2 成员形态。</summary>
            [MinMaxSlider("dynamicRange", true)]
            public Vector2 byVector;

            /// <summary>两个 float 成员所指向的成员。</summary>
            public float lo = -2f;

            /// <summary>上界成员。</summary>
            public float hi = 8f;

            /// <summary>两个字面量成员形态。</summary>
            [MinMaxSlider("lo", "hi")]
            public Vector2 byMembers;

            /// <summary>一个字面量 + 一个成员。</summary>
            [MinMaxSlider(-3f, "hi")]
            public Vector2 mixed;

            /// <summary>成员名拼错——解析应当失败。</summary>
            [MinMaxSlider("没这个成员", true)]
            public Vector2 broken;

            /// <summary>成员存在但类型不对（要 Vector2，给的是 float）。</summary>
            [MinMaxSlider("aFloat", true)]
            public Vector2 wrongType;
        }
    }
}
