using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 值绘制四特性：链装配，以及两处可无头测试的纯逻辑（文本化、进度条数学）。
    /// <para>
    /// <b>测不了的</b>：替换型绘制器「不调下一个」的效果、真实渲染（文本裁切、进度条外观、
    /// 工具栏与逐位开关的排布）。前者是绘制期分支，后者按本仓策略不测 IMGUI——
    /// 目视在 <c>Samples/AttributeShowcase</c>。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ValueDrawerTests
    {
        #region Private Fields

        private ValueDrawerFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ValueDrawerFixture>();
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

        /// <summary>四个替换型绘制器都配在各自的成员上，且都排在末端之前。</summary>
        [Test]
        public void 四个替换型绘制器都在链上()
        {
            Assert.That(IndexOf<DisplayAsStringDrawer>(Find("display")), Is.GreaterThanOrEqualTo(0));
            Assert.That(IndexOf<DisplayAsStringDrawer>(Find("display")), Is.LessThan(IndexOf<UnityFallbackDrawer>(Find("display"))));

            Assert.That(IndexOf<ToggleLeftDrawer>(Find("toggle")), Is.GreaterThanOrEqualTo(0));
            Assert.That(IndexOf<ProgressBarDrawer>(Find("progress")), Is.GreaterThanOrEqualTo(0));
            Assert.That(IndexOf<EnumToggleButtonsDrawer>(Find("enumValue")), Is.GreaterThanOrEqualTo(0));
        }

        /// <summary>
        /// 包裹型修饰在替换型外侧。
        /// <para>
        /// 顺序是功能性的：缩进/配色要包住替换型画的那一行；反过来它们就作用不到。
        /// </para>
        /// </summary>
        [Test]
        public void 包裹型修饰在替换型外侧()
        {
            var property = Find("decorated");
            var indent = IndexOf<IndentDrawer>(property);
            var display = IndexOf<DisplayAsStringDrawer>(property);

            Assert.That(indent, Is.GreaterThanOrEqualTo(0));
            Assert.That(display, Is.GreaterThanOrEqualTo(0));
            Assert.That(indent, Is.LessThan(display), "缩进必须在替换型外侧。");
        }

        /// <summary>不带任何特性的成员，链上只有末端。</summary>
        [Test]
        public void 无特性成员只有末端()
        {
            Assert.That(Find("plain").Chain.Count, Is.EqualTo(1));
        }

        #endregion

        #region 文本化

        /// <summary>支持的类型集合——多一个少一个都是行为变化。</summary>
        [Test]
        public void 文本化_支持的类型集合()
        {
            Assert.That(ValueTextFormatter.IsSupported(Property("intValue")), Is.True);
            Assert.That(ValueTextFormatter.IsSupported(Property("boolValue")), Is.True);
            Assert.That(ValueTextFormatter.IsSupported(Property("floatValue")), Is.True);
            Assert.That(ValueTextFormatter.IsSupported(Property("stringValue")), Is.True);
            Assert.That(ValueTextFormatter.IsSupported(Property("enumValue")), Is.True);
            Assert.That(ValueTextFormatter.IsSupported(Property("objectValue")), Is.True);
            Assert.That(ValueTextFormatter.IsSupported(Property("vectorValue")), Is.False, "复合类型退回普通绘制。");
            Assert.That(ValueTextFormatter.IsSupported(null), Is.False);
        }

        /// <summary>各类型的文本形状。</summary>
        [Test]
        public void 文本化_各类型的文本()
        {
            Assert.That(ValueTextFormatter.Format(Property("intValue")), Is.EqualTo("42"));
            Assert.That(ValueTextFormatter.Format(Property("boolValue")), Is.EqualTo("True"));
            Assert.That(ValueTextFormatter.Format(Property("floatValue")), Is.EqualTo("0.25"),
                "浮点按 0.###### 格式化——默认格式会拖出一串二进制尾巴。");
            Assert.That(ValueTextFormatter.Format(Property("stringValue")), Is.EqualTo("文本"));
            Assert.That(ValueTextFormatter.Format(Property("enumValue")), Is.EqualTo("Beta"), "枚举显示名字，不是下标。");
            Assert.That(ValueTextFormatter.Format(Property("objectValue")), Is.EqualTo("None"), "空对象引用显示 None。");
        }

        /// <summary>
        /// 多对象编辑且值不一致时显示占位符，而不是抛异常。
        /// <para>
        /// 值入口的 <c>GetValue()</c> 在这种情形下会抛——文本化因此不走它。
        /// </para>
        /// </summary>
        [Test]
        public void 文本化_多对象不一致显示占位符()
        {
            var second = ScriptableObject.CreateInstance<ValueDrawerFixture>();
            try
            {
                second.intValue = 7;
                using (var serializedObject = new SerializedObject(new Object[] { _target, second }))
                {
                    var property = serializedObject.FindProperty("intValue");
                    Assert.That(property.hasMultipleDifferentValues, Is.True, "前提不成立：两个目标的值应当不同。");
                    Assert.That(ValueTextFormatter.Format(property), Is.EqualTo(ValueTextFormatter.MixedValues));
                }
            }
            finally
            {
                Object.DestroyImmediate(second);
            }
        }

        #endregion

        #region 进度条数学

        /// <summary>归一化：端点与范围外都夹到 0–1。</summary>
        [Test]
        public void 进度条_归一化()
        {
            Assert.That(ProgressBarValues.Normalize(50, 0, 100), Is.EqualTo(0.5f));
            Assert.That(ProgressBarValues.Normalize(-10, 0, 100), Is.EqualTo(0f));
            Assert.That(ProgressBarValues.Normalize(999, 0, 100), Is.EqualTo(1f));
            Assert.That(ProgressBarValues.Normalize(5, 0, 100), Is.EqualTo(0.05f).Within(1e-6f));
        }

        /// <summary>反算与归一化互逆。</summary>
        [Test]
        public void 进度条_反算与归一化互逆()
        {
            var value = ProgressBarValues.FromNormalized(0.25f, -50, 150);
            Assert.That(value, Is.EqualTo(0.0).Within(1e-9), "-50 + 0.25 × 200 = 0。");

            Assert.That(ProgressBarValues.FromNormalized(2f, 0, 10), Is.EqualTo(10.0), "比例越界时夹到端点。");
            Assert.That(ProgressBarValues.FromNormalized(-1f, 0, 10), Is.EqualTo(0.0));
        }

        /// <summary>显示文本按类型格式化。</summary>
        [Test]
        public void 进度条_显示文本()
        {
            Assert.That(ProgressBarValues.FormatValue(75, true), Is.EqualTo("75"));
            Assert.That(ProgressBarValues.FormatValue(75.4321, false), Is.EqualTo("75.432"));
            Assert.That(ProgressBarValues.FormatValue(0.5, false), Is.EqualTo("0.5"));
        }

        /// <summary>鼠标横坐标换算：左右端与越界。</summary>
        [Test]
        public void 进度条_鼠标换算()
        {
            var rect = new Rect(10f, 0f, 200f, 18f);

            Assert.That(ProgressBarValues.NormalizedFromMouse(rect, new Vector2(10f, 5f)), Is.EqualTo(0f));
            Assert.That(ProgressBarValues.NormalizedFromMouse(rect, new Vector2(110f, 5f)), Is.EqualTo(0.5f));
            Assert.That(ProgressBarValues.NormalizedFromMouse(rect, new Vector2(999f, 5f)), Is.EqualTo(1f));
            Assert.That(ProgressBarValues.NormalizedFromMouse(new Rect(0f, 0f, 0f, 0f), Vector2.zero), Is.EqualTo(0f),
                "宽度为 0 不除零。");
        }

        /// <summary>支持的类型集合。</summary>
        [Test]
        public void 进度条_支持的类型集合()
        {
            Assert.That(ProgressBarValues.IsSupported(Property("intValue")), Is.True);
            Assert.That(ProgressBarValues.IsSupported(Property("floatValue")), Is.True);
            Assert.That(ProgressBarValues.IsSupported(Property("stringValue")), Is.False);
            Assert.That(ProgressBarValues.IsSupported(null), Is.False);
        }

        /// <summary>读写往返：整数四舍五入，浮点原样。</summary>
        [Test]
        public void 进度条_读写往返()
        {
            using (var serializedObject = new SerializedObject(_target))
            {
                var intProperty = serializedObject.FindProperty("intValue");
                ProgressBarValues.Write(intProperty, 3.6);
                Assert.That(ProgressBarValues.Read(intProperty), Is.EqualTo(4.0), "整数类型四舍五入。");

                var floatProperty = serializedObject.FindProperty("floatValue");
                ProgressBarValues.Write(floatProperty, 0.75);
                Assert.That(ProgressBarValues.Read(floatProperty), Is.EqualTo(0.75).Within(1e-6));
            }
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

        /// <summary>取临时资产上某字段的序列化属性。</summary>
        /// <param name="path">字段名。</param>
        /// <returns>序列化属性；绑定在夹具共用的 <see cref="SerializedObject"/> 上，随 TearDown 释放。</returns>
        private SerializedProperty Property(string path)
        {
            return _serializedObject.FindProperty(path);
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

    /// <summary>值绘制测试用的资产。</summary>
    internal sealed class ValueDrawerFixture : ScriptableObject
    {
        /// <summary>文本化。</summary>
        [DisplayAsString]
        public int display = 42;

        /// <summary>左侧开关。</summary>
        [ToggleLeft]
        public bool toggle;

        /// <summary>进度条。</summary>
        [ProgressBar(0, 100)]
        public float progress = 50f;

        /// <summary>枚举工具栏。</summary>
        [EnumToggleButtons]
        public ValueFixtureEnum enumValue = ValueFixtureEnum.Beta;

        /// <summary>包裹型与替换型叠加。</summary>
        [Indent]
        [DisplayAsString]
        public int decorated = 1;

        /// <summary>纯文本化用的各类型字段。</summary>
        public int intValue = 42;

        /// <summary>bool 文本化。</summary>
        public bool boolValue = true;

        /// <summary>浮点文本化。</summary>
        public float floatValue = 0.25f;

        /// <summary>字符串文本化。</summary>
        public string stringValue = "文本";

        /// <summary>枚举文本化。</summary>
        public ValueFixtureEnum enumValuePlain = ValueFixtureEnum.Beta;

        /// <summary>对象引用文本化（保持为 None）。</summary>
        public Object objectValue;

        /// <summary>复合类型：不该被文本化支持。</summary>
        public Vector3 vectorValue;

        /// <summary>无特性的对照。</summary>
        public int plain;
    }

    /// <summary>枚举测试用类型。</summary>
    internal enum ValueFixtureEnum
    {
        /// <summary>第一个成员。</summary>
        Alpha,

        /// <summary>第二个成员。</summary>
        Beta,

        /// <summary>第三个成员。</summary>
        Gamma,
    }
}
