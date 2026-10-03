using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 布局与外观六特性的链装配：有没有它、在第几位。
    /// <para>
    /// 顺序在这里不是审美问题而是**功能问题**：颜色绘制器若排到替换型值绘制器内侧，
    /// 就染不到那个控件；<c>[HideLabel]</c> 若排到 <c>[LabelWidth]</c> 外侧，
    /// 宽度就作用在一个已经没有标签的字段上。故逐条钉住。
    /// </para>
    /// </summary>
    [TestFixture]
    public class LayoutDrawerTests
    {
        #region Private Fields

        private LayoutFixture _target;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<LayoutFixture>();
        }

        /// <summary>销毁临时资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 链顺序

        /// <summary>
        /// 三个包裹型修饰的相对顺序：颜色 → 缩进 → 间距 → 末端。
        /// <para>
        /// 颜色最外：它要染的是「一切」，包括缩进与间距占的区域之外的内容。
        /// </para>
        /// </summary>
        [Test]
        public void 颜色缩进间距_由外到内()
        {
            var property = Find("decorated");

            var color = IndexOf<GUIColorDrawer>(property);
            var indent = IndexOf<IndentDrawer>(property);
            var space = IndexOf<PropertySpaceDrawer>(property);
            var terminal = IndexOf<UnityFallbackDrawer>(property);

            Assert.That(color, Is.GreaterThanOrEqualTo(0));
            Assert.That(indent, Is.GreaterThanOrEqualTo(0));
            Assert.That(space, Is.GreaterThanOrEqualTo(0));

            Assert.That(color, Is.LessThan(indent), "颜色必须在缩进外侧（先染色，再缩进）。");
            Assert.That(indent, Is.LessThan(space), "缩进在间距外侧。");
            Assert.That(space, Is.LessThan(terminal), "间距要包住末端绘制的内容。");
        }

        /// <summary>标签宽度在撤标签之前——宽度要作用在一个还有标签的字段上。</summary>
        [Test]
        public void 标签宽度在撤标签之前()
        {
            var property = Find("wide");

            var width = IndexOf<LabelWidthDrawer>(property);
            var hide = IndexOf<HideLabelDrawer>(property);
            var terminal = IndexOf<UnityFallbackDrawer>(property);

            Assert.That(width, Is.GreaterThanOrEqualTo(0));
            Assert.That(hide, Is.GreaterThanOrEqualTo(0));
            Assert.That(width, Is.LessThan(hide));
            Assert.That(hide, Is.LessThan(terminal));
        }

        /// <summary>后缀绘制器在链上，且排在末端之前。</summary>
        [Test]
        public void 后缀在末端之前()
        {
            var property = Find("duration");

            var suffix = IndexOf<SuffixLabelDrawer>(property);
            var terminal = IndexOf<UnityFallbackDrawer>(property);

            Assert.That(suffix, Is.GreaterThanOrEqualTo(0));
            Assert.That(suffix, Is.LessThan(terminal), "后缀要包住值控件才能并排画。");
        }

        /// <summary>不带任何布局特性的成员，链上只有末端。</summary>
        [Test]
        public void 无特性成员只有末端()
        {
            var property = Find("plain");

            Assert.That(property.Chain.Count, Is.EqualTo(1));
        }

        #endregion

        #region Private Helpers

        /// <summary>按路径取成员节点；树在首次调用时构建，同一用例内复用。</summary>
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
    }

    /// <summary>布局六特性测试用的资产。</summary>
    internal sealed class LayoutFixture : ScriptableObject
    {
        /// <summary>颜色 + 缩进 + 间距叠在同一字段上。</summary>
        [GUIColor(1f, 0.5f, 0.25f)]
        [Indent]
        [PropertySpace(10f)]
        public int decorated = 1;

        /// <summary>标签宽度 + 撤标签。</summary>
        [LabelWidth(200f)]
        [HideLabel]
        public string wide = "值";

        /// <summary>普通后缀。</summary>
        [SuffixLabel("秒")]
        public float duration = 1f;

        /// <summary>叠在控件上的后缀。</summary>
        [SuffixLabel("叠着", true)]
        public float overlayValue = 2f;

        /// <summary>无任何特性的对照。</summary>
        public int plain;
    }
}
