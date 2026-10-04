using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 三个内嵌环境条件族：判据是绘制期的深度上下文。
    /// <para>
    /// 这里不碰 GUI——<c>PropertyState</c> 的可见性与只读性本来就是「每帧求值的委托」，
    /// 于是「进出内嵌上下文」这件事可以在无头环境里完整复现。
    /// </para>
    /// </summary>
    [TestFixture]
    public class InlineEditorConditionProcessorTests
    {
        #region Setup / Teardown

        private InlineEditorConditionFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;
        private ScriptableObject _nested;

        /// <summary>建宿主资产、树，以及一个用来当「被内嵌对象」的替身。</summary>
        [SetUp]
        public void SetUp()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
            InlineEditorDrawContext.Reset();

            _target = ScriptableObject.CreateInstance<InlineEditorConditionFixture>();
            _nested = ScriptableObject.CreateInstance<ScriptableObject>();
            _serializedObject = new SerializedObject(_target);
            _tree = PropertyTree.Create(_serializedObject);
        }

        /// <summary>释放一切并复位三个静态门面（含深度上下文）。</summary>
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

            if (_nested != null)
            {
                Object.DestroyImmediate(_nested);
                _nested = null;
            }

            InlineEditorDrawContext.Reset();
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 三个条件族

        /// <summary><c>[ShowInInlineEditors]</c>：外层不显示，进了内嵌编辑器才显示。</summary>
        [Test]
        public void 只在内嵌编辑器内可见()
        {
            var node = Find("onlyInside");

            Assert.That(node.IsVisible, Is.False, "外层自己的 Inspector 里不显示。");

            using (Enter(incrementDepth: true))
            {
                Assert.That(node.IsVisible, Is.True, "被内嵌时才显示。");
            }

            Assert.That(node.IsVisible, Is.False, "退出上下文后复原。");
        }

        /// <summary><c>[HideInInlineEditors]</c>：外层照常显示，进了内嵌编辑器才藏起来。</summary>
        [Test]
        public void 在内嵌编辑器内隐藏()
        {
            var node = Find("hiddenInside");

            Assert.That(node.IsVisible, Is.True, "外层照常显示。");

            using (Enter(incrementDepth: true))
            {
                Assert.That(node.IsVisible, Is.False, "被内嵌时藏起来。");
            }

            Assert.That(node.IsVisible, Is.True);
        }

        /// <summary><c>[DisableInInlineEditors]</c>：只在内嵌编辑器里变灰，仍可见。</summary>
        [Test]
        public void 在内嵌编辑器内只读()
        {
            var node = Find("readonlyInside");

            Assert.That(node.State.IsReadOnly, Is.False, "外层可编辑。");
            Assert.That(node.IsVisible, Is.True, "禁用不改可见性。");

            using (Enter(incrementDepth: true))
            {
                Assert.That(node.State.IsReadOnly, Is.True, "被内嵌时只读。");
                Assert.That(node.IsVisible, Is.True, "仍然可见。");
            }

            Assert.That(node.State.IsReadOnly, Is.False);
        }

        /// <summary>
        /// 不递增语义深度的那一层不算「内嵌环境」——这正是
        /// <c>IncrementInlineEditorDrawerDepth = false</c> 的用途：
        /// 画整个编辑器、但让这三个特性当没看见。
        /// </summary>
        [Test]
        public void 不递增深度的层不算内嵌环境()
        {
            using (Enter(incrementDepth: false))
            {
                Assert.That(Find("onlyInside").IsVisible, Is.False, "语义深度没涨。");
                Assert.That(Find("hiddenInside").IsVisible, Is.True);
                Assert.That(Find("readonlyInside").State.IsReadOnly, Is.False);
            }
        }

        /// <summary>纯对照成员没有任何条件族的作用。</summary>
        [Test]
        public void 普通成员不受影响()
        {
            var node = Find("plain");

            Assert.That(node.IsVisible, Is.True);

            using (Enter(incrementDepth: true))
            {
                Assert.That(node.IsVisible, Is.True);
                Assert.That(node.State.IsReadOnly, Is.False);
            }
        }

        #endregion

        #region 进程边界

        /// <summary>
        /// 三个条件族都是**处理器专有**——成员链上只有末端绘制器，没有它们的位置。
        /// </summary>
        /// <remarks>
        /// 这条守的是「处理器与绘制器的分界」：判断不产出像素，塞进绘制器只会让绘制器
        /// 变得既画东西又做决策。
        /// </remarks>
        [Test]
        public void 三个条件族不进绘制器链()
        {
            Assert.That(Find("onlyInside").Chain.Count, Is.EqualTo(1), "只该有末端。");
            Assert.That(Find("hiddenInside").Chain.Count, Is.EqualTo(1));
            Assert.That(Find("readonlyInside").Chain.Count, Is.EqualTo(1));
        }

        #endregion

        #region Private Helpers

        /// <summary>进入内嵌上下文，断言成功并交出作用域。</summary>
        /// <param name="incrementDepth">是否递增语义深度。</param>
        /// <returns>作用域。</returns>
        private InlineEditorDrawContext.Scope Enter(bool incrementDepth)
        {
            var result = InlineEditorDrawContext.TryEnter(_nested, incrementDepth, out var scope);

            Assert.That(result, Is.EqualTo(InlineEditorEnterResult.Entered), "这一步应当进入成功。");
            return scope;
        }

        /// <summary>按路径找成员节点。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>找到的节点。</returns>
        private InspectorProperty Find(string path)
        {
            var children = _tree.Root.Children;
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i].Path == path)
                {
                    return children[i];
                }
            }

            Assert.Fail($"找不到成员 {path}。");
            return null;
        }

        #endregion

        /// <summary>三个内嵌环境条件族的测试宿主，外加一个纯对照。</summary>
        private class InlineEditorConditionFixture : ScriptableObject
        {
            /// <summary>纯对照。</summary>
            public int plain = 1;

            /// <summary>只在内嵌编辑器里显示。</summary>
            [ShowInInlineEditors]
            public int onlyInside = 2;

            /// <summary>在内嵌编辑器里隐藏。</summary>
            [HideInInlineEditors]
            public int hiddenInside = 3;

            /// <summary>在内嵌编辑器里只读。</summary>
            [DisableInInlineEditors]
            public int readonlyInside = 4;
        }
    }
}
