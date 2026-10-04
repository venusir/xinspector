using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[PreviewField]</c>：链装配与几何。
    /// <para>
    /// <b>测不了的</b>：预览图本身（<c>AssetPreview</c> 的结果依赖 Unity 的资产导入管线）、
    /// 以及方块的渲染（本仓策略不测 IMGUI）。故这里测的是**摆放算术**——
    /// 「方块放哪、字段放哪、放不下时怎么办」这三件事全在纯函数里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PreviewFieldDrawerTests
    {
        #region Private Fields

        /// <summary>一块足够宽的可用区域。</summary>
        private static readonly Rect Wide = new Rect(0f, 0f, 400f, 64f);

        private PreviewFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产与序列化对象。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<PreviewFixture>();
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

        /// <summary>三个形态的绘制器都在链上，且排在末端之前。</summary>
        [Test]
        public void 绘制器在链上()
        {
            AssertDrawer<PreviewFieldDrawer>("bare");
            AssertDrawer<PreviewFieldDrawer>("sized");
            AssertDrawer<PreviewFieldDrawer>("aligned");
        }

        /// <summary>无特性成员只有末端；非对象引用成员上不装它。</summary>
        [Test]
        public void 非对象引用成员没有该绘制器()
        {
            Assert.That(Find("plain").Chain.Count, Is.EqualTo(1));
            Assert.That(IndexOf<PreviewFieldDrawer>(Find("aString")), Is.EqualTo(-1), "string 字段不该装 [PreviewField]。");
        }

        #endregion

        #region 高度

        /// <summary>未指定（含非正值）时用默认高度；指定了就用指定的。</summary>
        [Test]
        public void 高度换算()
        {
            Assert.That(PreviewFieldLayout.ResolveHeight(0f), Is.EqualTo(PreviewFieldLayout.DefaultHeight));
            Assert.That(PreviewFieldLayout.ResolveHeight(-5f), Is.EqualTo(PreviewFieldLayout.DefaultHeight));
            Assert.That(PreviewFieldLayout.ResolveHeight(120f), Is.EqualTo(120f));
        }

        #endregion

        #region 方块摆放

        /// <summary>三种对齐各自把方块贴到该贴的边上。</summary>
        [Test]
        public void 方块摆放()
        {
            var left = PreviewFieldLayout.PreviewRect(Wide, 64f, ObjectFieldAlignment.Left);
            Assert.That(left.x, Is.EqualTo(0f));
            Assert.That(left.width, Is.EqualTo(64f));

            var center = PreviewFieldLayout.PreviewRect(Wide, 64f, ObjectFieldAlignment.Center);
            Assert.That(center.x, Is.EqualTo(168f), "（400 - 64）/ 2。");
            Assert.That(center.width, Is.EqualTo(64f));

            var right = PreviewFieldLayout.PreviewRect(Wide, 64f, ObjectFieldAlignment.Right);
            Assert.That(right.xMax, Is.EqualTo(400f));
            Assert.That(right.width, Is.EqualTo(64f));
        }

        /// <summary>方块永远不超过可用宽度。</summary>
        [Test]
        public void 方块不超过可用宽度()
        {
            var narrow = new Rect(0f, 0f, 30f, 64f);
            var rect = PreviewFieldLayout.PreviewRect(narrow, 64f, ObjectFieldAlignment.Left);

            Assert.That(rect.width, Is.EqualTo(30f));
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(narrow.xMax));
        }

        #endregion

        #region 同排还是下一行

        /// <summary>够宽就同排；不够宽就判为放不下，由调用方把字段排到下一行。</summary>
        [Test]
        public void 放得下与否()
        {
            Assert.That(PreviewFieldLayout.FitsBeside(Wide, 64f, 60f, 2f), Is.True, "400 宽绰绰有余。");
            Assert.That(PreviewFieldLayout.FitsBeside(new Rect(0f, 0f, 120f, 64f), 64f, 60f, 2f), Is.False,
                "120 < 64 + 60 + 2，同排会把字段挤成一条缝。");
            Assert.That(PreviewFieldLayout.FitsBeside(new Rect(0f, 0f, 126f, 64f), 64f, 60f, 2f), Is.True, "刚好。");
        }

        /// <summary>方块在左时字段填右边，方块在右时字段填左边；居中按左边算。</summary>
        [Test]
        public void 字段摆放()
        {
            var leftPreview = PreviewFieldLayout.PreviewRect(Wide, 64f, ObjectFieldAlignment.Left);
            var rightField = PreviewFieldLayout.FieldRectBeside(Wide, leftPreview, ObjectFieldAlignment.Left, 2f);
            Assert.That(rightField.x, Is.EqualTo(66f), "方块右边 2 像素起。");
            Assert.That(rightField.xMax, Is.EqualTo(400f));

            var rightPreview = PreviewFieldLayout.PreviewRect(Wide, 64f, ObjectFieldAlignment.Right);
            var leftField = PreviewFieldLayout.FieldRectBeside(Wide, rightPreview, ObjectFieldAlignment.Right, 2f);
            Assert.That(leftField.x, Is.EqualTo(0f));
            Assert.That(leftField.xMax, Is.EqualTo(334f), "方块左边 2 像素止。");
        }

        /// <summary>字段宽度不会是负数（矩形宽度为负会让 GUI 报错）。</summary>
        [Test]
        public void 字段宽度不为负()
        {
            var content = new Rect(0f, 0f, 64f, 64f);
            var preview = PreviewFieldLayout.PreviewRect(content, 64f, ObjectFieldAlignment.Left);
            var field = PreviewFieldLayout.FieldRectBeside(content, preview, ObjectFieldAlignment.Left, 2f);

            Assert.That(field.width, Is.GreaterThanOrEqualTo(0f));
        }

        #endregion

        #region 贴图缓存

        /// <summary>对象被清空时缓存要跟着清——否则会继续贴着上一个对象的图。</summary>
        [Test]
        public void 清空对象时清缓存()
        {
            var state = new PreviewFieldState { CachedFor = _target };

            Assert.That(PreviewFieldContent.ResolveTexture(null, state), Is.Null);
            Assert.That(state.Cached, Is.Null);
            Assert.That(state.CachedFor, Is.Null);
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

        /// <summary><c>[PreviewField]</c> 的测试宿主。</summary>
        private class PreviewFixture : ScriptableObject
        {
            /// <summary>无特性的对照。</summary>
            public Object plain;

            /// <summary>非对象引用的对照。</summary>
            public string aString;

            /// <summary>无参形态。</summary>
            [PreviewField]
            public Texture2D bare;

            /// <summary>带高度。</summary>
            [PreviewField(80f)]
            public Texture2D sized;

            /// <summary>带对齐。</summary>
            [PreviewField(64f, ObjectFieldAlignment.Right)]
            public Texture2D aligned;
        }
    }
}
