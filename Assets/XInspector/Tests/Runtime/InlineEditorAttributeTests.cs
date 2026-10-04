using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <c>[InlineEditor]</c> 与三个枚举的构造行为、模式映射与用法约束。
    /// <para>
    /// 不依赖 Unity，因此同时跑在 Unity 测试与离线的 <c>Tests.Native</c> 通道里。
    /// 摆放与降级决策在 Editor 侧的纯函数里测（<c>InlineEditorDrawerTests</c>）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class InlineEditorAttributeTests
    {
        #region 构造与默认值

        /// <summary>两组构造各自把参数落到该落的地方，其余取本包定的默认值。</summary>
        [Test]
        public void 两组构造()
        {
            var bare = new InlineEditorAttribute();

            Assert.That(bare.ObjectFieldMode, Is.EqualTo(InlineEditorObjectFieldModes.Boxed), "默认装箱。");
            Assert.That(bare.DrawGUI, Is.True, "默认只画界面。");
            Assert.That(bare.DrawHeader, Is.False);
            Assert.That(bare.DrawPreview, Is.False);
            Assert.That(bare.PreviewAlignment, Is.EqualTo(PreviewAlignment.Right), "本包定的默认：预览在右。");
            Assert.That(bare.PreviewHeight, Is.EqualTo(0f), "未指定 → 0，绘制时按默认处理。");
            Assert.That(bare.PreviewWidth, Is.EqualTo(0f));
            Assert.That(bare.MaxHeight, Is.EqualTo(0f), "未指定 → 不限高度。");
            Assert.That(bare.IncrementInlineEditorDrawerDepth, Is.True, "默认计入嵌套深度。");
            Assert.That(bare.DisableGUIForVCSLockedAssets, Is.True, "默认对锁定资产置灰（与 Odin 同默认）。");
            Assert.That(bare.Expanded, Is.False);
            Assert.That(bare.ExpandedHasValue, Is.False);

            var byFieldMode = new InlineEditorAttribute(InlineEditorObjectFieldModes.Hidden);

            Assert.That(byFieldMode.ObjectFieldMode, Is.EqualTo(InlineEditorObjectFieldModes.Hidden));
            Assert.That(byFieldMode.DrawGUI, Is.True, "第二组构造的编辑器画法取默认。");
        }

        /// <summary>
        /// 六种模式各自映射到「头 / 界面 / 预览」三面旗——映射据官方对每种模式的描述推导。
        /// </summary>
        [Test]
        public void 模式映射到三面旗()
        {
            AssertFlags(InlineEditorModes.GUIOnly, header: false, gui: true, preview: false);
            AssertFlags(InlineEditorModes.GUIAndHeader, header: true, gui: true, preview: false);
            AssertFlags(InlineEditorModes.GUIAndPreview, header: false, gui: true, preview: true);
            AssertFlags(InlineEditorModes.FullEditor, header: true, gui: true, preview: true);
            AssertFlags(InlineEditorModes.SmallPreview, header: false, gui: false, preview: true);
            AssertFlags(InlineEditorModes.LargePreview, header: false, gui: false, preview: true);
        }

        /// <summary>
        /// 模式在构造期就被拆成三面旗（Odin 的公开面上也没有存模式的地方），
        /// 因此具名实参可以事后覆盖任何一面旗。
        /// </summary>
        [Test]
        public void 三面旗可在构造之后覆盖()
        {
            var attribute = new InlineEditorAttribute(InlineEditorModes.GUIOnly) { DrawPreview = true };

            Assert.That(attribute.DrawGUI, Is.True);
            Assert.That(attribute.DrawPreview, Is.True);
        }

        /// <summary>大预览的默认高度是本包定的（Odin 的存在它的偏好设置里，官网核不到）。</summary>
        [Test]
        public void 大预览的默认高度由本包定()
        {
            var large = new InlineEditorAttribute(InlineEditorModes.LargePreview);
            var small = new InlineEditorAttribute(InlineEditorModes.SmallPreview);

            Assert.That(large.PreviewHeight, Is.EqualTo(InlineEditorAttribute.DefaultLargePreviewHeight));
            Assert.That(small.PreviewHeight, Is.EqualTo(0f), "小预览不预置高度，绘制期按默认 64 处理。");

            var overridden = new InlineEditorAttribute(InlineEditorModes.LargePreview) { PreviewHeight = 40f };
            Assert.That(overridden.PreviewHeight, Is.EqualTo(40f), "具名实参盖过模式的初值。");
        }

        /// <summary>
        /// <c>Expanded</c> 只有被显式设过才置 <c>ExpandedHasValue</c>——
        /// 「显式设成 false」与「根本没设」必须能区分（与 <c>[FoldoutGroup]</c> 同款）。
        /// </summary>
        [Test]
        public void Expanded显式设过才标记()
        {
            var untouched = new InlineEditorAttribute();
            Assert.That(untouched.ExpandedHasValue, Is.False);

            var setFalse = new InlineEditorAttribute { Expanded = false };
            Assert.That(setFalse.ExpandedHasValue, Is.True);
            Assert.That(setFalse.Expanded, Is.False);

            var setTrue = new InlineEditorAttribute { Expanded = true };
            Assert.That(setTrue.ExpandedHasValue, Is.True);
        }

        #endregion

        #region 三个枚举

        /// <summary>
        /// 文档站按字母序排成员、数值未核实，故本包自定顺序：**默认成员排 0**，其余按语义排。
        /// 这条守卫钉住这个约定——改顺序是破坏性变更。
        /// </summary>
        [Test]
        public void 模式枚举的数值是本包定的()
        {
            Assert.That((int)InlineEditorModes.GUIOnly, Is.EqualTo(0));
            Assert.That((int)InlineEditorModes.GUIAndHeader, Is.EqualTo(1));
            Assert.That((int)InlineEditorModes.GUIAndPreview, Is.EqualTo(2));
            Assert.That((int)InlineEditorModes.FullEditor, Is.EqualTo(3));
            Assert.That((int)InlineEditorModes.SmallPreview, Is.EqualTo(4));
            Assert.That((int)InlineEditorModes.LargePreview, Is.EqualTo(5));
        }

        /// <summary>对象字段模式：默认成员排 0，其余按「字段露出多少」由多到少。</summary>
        [Test]
        public void 对象字段模式枚举的数值是本包定的()
        {
            Assert.That((int)InlineEditorObjectFieldModes.Boxed, Is.EqualTo(0));
            Assert.That((int)InlineEditorObjectFieldModes.Foldout, Is.EqualTo(1));
            Assert.That((int)InlineEditorObjectFieldModes.Hidden, Is.EqualTo(2));
            Assert.That((int)InlineEditorObjectFieldModes.CompletelyHidden, Is.EqualTo(3));
        }

        /// <summary>预览位置：本包的默认（在右）排 0，先左右后上下。</summary>
        [Test]
        public void 对齐枚举的数值是本包定的()
        {
            Assert.That((int)PreviewAlignment.Right, Is.EqualTo(0));
            Assert.That((int)PreviewAlignment.Left, Is.EqualTo(1));
            Assert.That((int)PreviewAlignment.Top, Is.EqualTo(2));
            Assert.That((int)PreviewAlignment.Bottom, Is.EqualTo(3));
        }

        /// <summary>
        /// 「默认成员排 0」这条约定的直接后果：<c>default</c> 就是默认行为。
        /// </summary>
        [Test]
        public void 默认值就是零值()
        {
            Assert.That(default(InlineEditorModes), Is.EqualTo(InlineEditorModes.GUIOnly));
            Assert.That(default(InlineEditorObjectFieldModes), Is.EqualTo(InlineEditorObjectFieldModes.Boxed));
            Assert.That(default(PreviewAlignment), Is.EqualTo(PreviewAlignment.Right));

            var bare = new InlineEditorAttribute();
            Assert.That(bare.ObjectFieldMode, Is.EqualTo(default(InlineEditorObjectFieldModes)));
            Assert.That(bare.PreviewAlignment, Is.EqualTo(default(PreviewAlignment)));
        }

        #endregion

        #region 用法与边界

        /// <summary>仅用于成员且不可重复。</summary>
        [Test]
        public void 仅成员且不可重复()
        {
            var usage = typeof(InlineEditorAttribute).GetCustomAttribute<AttributeUsageAttribute>();

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.AllowMultiple, Is.False);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Field), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Property), Is.True);
            Assert.That(usage.ValidOn.HasFlag(AttributeTargets.Class), Is.False, "本包不收类型级用法。");
        }

        /// <summary>两组重载的参数全在 BCL 里——「Runtime 零 Unity 依赖」由此守住。</summary>
        [Test]
        public void 不含Unity类型的重载()
        {
            var constructors = typeof(InlineEditorAttribute).GetConstructors();
            Assert.That(constructors.Length, Is.EqualTo(2), "只该有两组纯 BCL 重载。");

            foreach (var constructor in constructors)
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    Assert.That(
                        parameter.ParameterType.Namespace,
                        Does.Not.StartWith("UnityEngine"),
                        $"{constructor} 的参数 {parameter.Name} 带进了 Unity 类型。");
                }
            }
        }

        /// <summary>
        /// <b>公开面恰好是 Odin 那一组，一个不多、一个不少。</b>
        /// 这条守卫的用意：哪天有人「顺手补齐 Odin 的字段」或「加个自己的开关」，
        /// 先被这里问一次——多出来的那个要么有真行为（那就连同绘制器与测试一起加），
        /// 要么就是本包最忌讳的静默 no-op。
        /// </summary>
        /// <remarks>
        /// 同时查属性与字段：Odin 把这些选项写成字段，照抄它的源码时只查属性会漏掉。
        /// </remarks>
        [Test]
        public void 只声明支持的选项()
        {
            // DeclaredOnly 必不可少：System.Attribute 自带一个公开的 TypeId。
            var declared = typeof(InlineEditorAttribute)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            var expected = new[]
            {
                "DisableGUIForVCSLockedAssets",
                "DrawGUI",
                "DrawHeader",
                "DrawPreview",
                "Expanded",
                "ExpandedHasValue",
                "IncrementInlineEditorDrawerDepth",
                "MaxHeight",
                "ObjectFieldMode",
                "PreviewAlignment",
                "PreviewHeight",
                "PreviewWidth",
            };

            CollectionAssert.AreEqual(expected, declared, "公开属性集合变了——先想清楚新加的那个有没有真行为。");

            var publicFields = typeof(InlineEditorAttribute)
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.That(publicFields, Is.Empty, "选项一律走属性；公开字段会绕过上面那条守卫。");
        }

        #endregion

        #region Private Helpers

        /// <summary>断言某模式拆出的三面旗。</summary>
        /// <param name="mode">模式。</param>
        /// <param name="header">是否画头。</param>
        /// <param name="gui">是否画界面。</param>
        /// <param name="preview">是否画预览。</param>
        private static void AssertFlags(InlineEditorModes mode, bool header, bool gui, bool preview)
        {
            var attribute = new InlineEditorAttribute(mode);

            Assert.That(attribute.DrawHeader, Is.EqualTo(header), $"{mode} 的头。");
            Assert.That(attribute.DrawGUI, Is.EqualTo(gui), $"{mode} 的界面。");
            Assert.That(attribute.DrawPreview, Is.EqualTo(preview), $"{mode} 的预览。");
        }

        #endregion
    }
}
