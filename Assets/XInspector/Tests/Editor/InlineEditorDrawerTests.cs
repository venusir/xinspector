using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[InlineEditor]</c>：链装配、摆放决策与编辑器实例的生命周期。
    /// <para>
    /// <b>测不了的</b>：内嵌编辑器画出来长什么样（本仓策略不测 IMGUI），以及
    /// <c>Editor.CreateEditor</c> 的内容本身（那是 Unity 的编辑器解析）。故这里测的是
    /// 全部**决策**（尺寸、滚动、并排还是堆叠、字段画不画、能不能内嵌、要不要置灰）
    /// 与**生命周期**（什么时候建、什么时候销毁）——两者都在无 GUI 的位置。
    /// </para>
    /// </summary>
    [TestFixture]
    public class InlineEditorDrawerTests
    {
        #region Private Fields

        private InlineEditorFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;
        private InlineEditorFixture _subject;

        #endregion

        #region Setup / Teardown

        /// <summary>建宿主资产、序列化对象与树；另建一个可当被引用对象的资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<InlineEditorFixture>();
            _subject = ScriptableObject.CreateInstance<InlineEditorFixture>();
            _serializedObject = new SerializedObject(_target);
            _tree = PropertyTree.Create(_serializedObject);
        }

        /// <summary>释放一切并复位两个静态门面。</summary>
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

            if (_subject != null)
            {
                Object.DestroyImmediate(_subject);
                _subject = null;
            }

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 链装配

        /// <summary>两个形态的绘制器都在链上，且排在末端之前。</summary>
        [Test]
        public void 绘制器在链上()
        {
            AssertDrawer<InlineEditorDrawer>("bare");
            AssertDrawer<InlineEditorDrawer>("full");
        }

        /// <summary>无特性成员只有末端。</summary>
        [Test]
        public void 无特性成员只有末端()
        {
            Assert.That(Find("plain").Chain.Count, Is.EqualTo(1));
        }

        #endregion

        #region 摆放决策

        /// <summary>预览尺寸：非正取默认，指定就用指定的；宽度还要夹到可用宽度内。</summary>
        [Test]
        public void 预览尺寸解析()
        {
            Assert.That(
                InlineEditorLayout.ResolvePreviewWidth(0f, 400f),
                Is.EqualTo(InlineEditorLayout.DefaultPreviewWidth));
            Assert.That(InlineEditorLayout.ResolvePreviewWidth(120f, 400f), Is.EqualTo(120f));
            Assert.That(InlineEditorLayout.ResolvePreviewWidth(500f, 400f), Is.EqualTo(400f), "夹到可用宽度。");
            Assert.That(InlineEditorLayout.ResolvePreviewWidth(500f, 0f), Is.EqualTo(500f), "可用宽度未知时不夹。");

            Assert.That(
                InlineEditorLayout.ResolvePreviewHeight(0f),
                Is.EqualTo(InlineEditorLayout.DefaultPreviewHeight));
            Assert.That(InlineEditorLayout.ResolvePreviewHeight(-1f), Is.EqualTo(InlineEditorLayout.DefaultPreviewHeight));
            Assert.That(InlineEditorLayout.ResolvePreviewHeight(200f), Is.EqualTo(200f));
        }

        /// <summary>只有正的最大高度才包滚动视图。</summary>
        [Test]
        public void 滚动判定()
        {
            Assert.That(InlineEditorLayout.UsesScrollView(0f), Is.False, "未指定 → 不限高度。");
            Assert.That(InlineEditorLayout.UsesScrollView(-10f), Is.False);
            Assert.That(InlineEditorLayout.UsesScrollView(200f), Is.True);
        }

        /// <summary>左右并排、上下堆叠。</summary>
        [Test]
        public void 并排与堆叠()
        {
            Assert.That(InlineEditorLayout.IsSideBySide(PreviewAlignment.Left), Is.True);
            Assert.That(InlineEditorLayout.IsSideBySide(PreviewAlignment.Right), Is.True);
            Assert.That(InlineEditorLayout.IsSideBySide(PreviewAlignment.Top), Is.False);
            Assert.That(InlineEditorLayout.IsSideBySide(PreviewAlignment.Bottom), Is.False);
        }

        /// <summary>预览在左或在上时排在界面之前；两种摆法共用这一个判断。</summary>
        [Test]
        public void 预览的前后()
        {
            Assert.That(InlineEditorLayout.PreviewComesFirst(PreviewAlignment.Left), Is.True);
            Assert.That(InlineEditorLayout.PreviewComesFirst(PreviewAlignment.Top), Is.True);
            Assert.That(InlineEditorLayout.PreviewComesFirst(PreviewAlignment.Right), Is.False);
            Assert.That(InlineEditorLayout.PreviewComesFirst(PreviewAlignment.Bottom), Is.False);
        }

        /// <summary>
        /// 四种对象字段模式 × 有值/空值，逐格断言「画不画字段、给不给提示」。
        /// <para>
        /// <c>Hidden</c> 与 <c>CompletelyHidden</c> 的区别只在空值那一格：
        /// 前者露出字段让你能赋值，后者恒藏、改为给一行灰字提示（本包与 Odin 的差异）。
        /// </para>
        /// </summary>
        [Test]
        public void 对象字段计划()
        {
            AssertPlan(InlineEditorObjectFieldModes.Boxed, hasValue: true, field: true, hint: false);
            AssertPlan(InlineEditorObjectFieldModes.Boxed, hasValue: false, field: true, hint: false);
            AssertPlan(InlineEditorObjectFieldModes.Foldout, hasValue: true, field: true, hint: false);
            AssertPlan(InlineEditorObjectFieldModes.Foldout, hasValue: false, field: true, hint: false);
            AssertPlan(InlineEditorObjectFieldModes.Hidden, hasValue: true, field: false, hint: false);
            AssertPlan(InlineEditorObjectFieldModes.Hidden, hasValue: false, field: true, hint: false);
            AssertPlan(InlineEditorObjectFieldModes.CompletelyHidden, hasValue: true, field: false, hint: false);
            AssertPlan(InlineEditorObjectFieldModes.CompletelyHidden, hasValue: false, field: false, hint: true);
        }

        /// <summary>空值与混合值都不内嵌。</summary>
        [Test]
        public void 可内嵌的判定()
        {
            Assert.That(InlineEditorLayout.CanInline(hasMultipleDifferentValues: false, hasValue: true), Is.True);
            Assert.That(InlineEditorLayout.CanInline(hasMultipleDifferentValues: false, hasValue: false), Is.False);
            Assert.That(InlineEditorLayout.CanInline(hasMultipleDifferentValues: true, hasValue: true), Is.False);
            Assert.That(InlineEditorLayout.CanInline(hasMultipleDifferentValues: true, hasValue: false), Is.False);
        }

        /// <summary>只有「是资产」且「不可编辑」才判定为被锁；非资产一律视为可编辑。</summary>
        [Test]
        public void 版本控制锁定判定()
        {
            Assert.That(InlineEditorVcs.IsLockedForEditing(isAsset: true, isOpenForEdit: false), Is.True);
            Assert.That(InlineEditorVcs.IsLockedForEditing(isAsset: true, isOpenForEdit: true), Is.False);
            Assert.That(InlineEditorVcs.IsLockedForEditing(isAsset: false, isOpenForEdit: false), Is.False);
            Assert.That(InlineEditorVcs.IsLockedForEditing(isAsset: false, isOpenForEdit: true), Is.False);
        }

        /// <summary>折叠头：没显式设过时默认展开。</summary>
        [Test]
        public void 折叠初值()
        {
            Assert.That(InlineEditorLayout.ResolveInitialExpanded(expandedHasValue: false, expanded: false), Is.True);
            Assert.That(InlineEditorLayout.ResolveInitialExpanded(expandedHasValue: true, expanded: true), Is.True);
            Assert.That(InlineEditorLayout.ResolveInitialExpanded(expandedHasValue: true, expanded: false), Is.False);
        }

        #endregion

        #region 编辑器实例的生命周期

        /// <summary>同一个目标复用同一个实例；目标换了才重建，且旧实例被销毁。</summary>
        [Test]
        public void 目标变化时重建编辑器()
        {
            var state = new InlineEditorState();

            try
            {
                var first = state.GetOrCreateEditor(_target);
                Assert.That(first, Is.Not.Null);

                Assert.That(state.GetOrCreateEditor(_target), Is.SameAs(first), "同一目标不重建。");

                var second = state.GetOrCreateEditor(_subject);
                Assert.That(second, Is.Not.SameAs(first), "换目标要换实例。");
                Assert.That(first == null, Is.True, "旧实例已销毁（按 == 判定为 null）。");
            }
            finally
            {
                state.Dispose();
            }
        }

        /// <summary>释放状态会销毁持有的编辑器实例；重复释放是空操作。</summary>
        [Test]
        public void 释放状态会销毁编辑器实例()
        {
            var state = new InlineEditorState();
            var editor = state.GetOrCreateEditor(_target);

            state.Dispose();

            Assert.That(editor == null, Is.True);
            Assert.That(() => state.Dispose(), Throws.Nothing);
        }

        /// <summary>
        /// 端到端：绘制器放进节点状态里的编辑器实例，随释放树被销毁——
        /// 走的是「状态 → 树 → 宿主」那四环。
        /// </summary>
        [Test]
        public void 释放树会销毁状态里的编辑器实例()
        {
            var state = _tree.Root.Children[0].State.GetOrCreate<InlineEditorState>();
            var editor = state.GetOrCreateEditor(_subject);

            _tree.Dispose();

            Assert.That(editor == null, Is.True);
        }

        #endregion

        #region Private Helpers

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

        /// <summary>断言某个对象字段模式在给定值状态下画什么。</summary>
        /// <param name="mode">模式。</param>
        /// <param name="hasValue">引用是否非空。</param>
        /// <param name="field">期望画字段。</param>
        /// <param name="hint">期望给提示。</param>
        private static void AssertPlan(InlineEditorObjectFieldModes mode, bool hasValue, bool field, bool hint)
        {
            var plan = InlineEditorLayout.ResolveObjectField(hasValue, mode);

            Assert.That(plan.DrawField, Is.EqualTo(field), $"{mode}（有值={hasValue}）画不画字段。");
            Assert.That(plan.ShowHint, Is.EqualTo(hint), $"{mode}（有值={hasValue}）给不给提示。");
        }

        #endregion

        /// <summary><c>[InlineEditor]</c> 的测试宿主。</summary>
        private class InlineEditorFixture : ScriptableObject
        {
            /// <summary>无特性的对照。</summary>
            public Object plain;

            /// <summary>无参形态。</summary>
            [InlineEditor]
            public Object bare;

            /// <summary>带模式与选项的形态。</summary>
            [InlineEditor(InlineEditorModes.FullEditor, InlineEditorObjectFieldModes.Foldout, MaxHeight = 200f)]
            public GameObject full;
        }
    }
}
