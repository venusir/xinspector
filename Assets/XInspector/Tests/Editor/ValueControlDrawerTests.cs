using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 值控件五特性：链装配与回绕数学。
    /// <para>
    /// <b>测不了的</b>：文本域/延迟控件/滑块/翻页按钮的真实渲染与交互（本仓策略不测 IMGUI），
    /// 以及「延迟提交」本身（那是 Unity 控件的行为）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ValueControlDrawerTests
    {
        #region Private Fields

        private ValueControlFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产与序列化对象。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ValueControlFixture>();
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

        /// <summary>四个替换型绘制器各就各位；回绕是包裹型，排在末端之前。</summary>
        [Test]
        public void 绘制器都在链上()
        {
            AssertDrawer<MultiLinePropertyDrawer>("notes");
            AssertDrawer<DelayedPropertyDrawer>("delayedInt");
            AssertDrawer<EnumPagingDrawer>("paged");
            AssertDrawer<PropertyRangeDrawer>("ranged");
            AssertDrawer<WrapDrawer>("angle");
        }

        /// <summary>无特性的对照只有末端。</summary>
        [Test]
        public void 无特性成员只有末端()
        {
            Assert.That(Find("plain").Chain.Count, Is.EqualTo(1));
        }

        #endregion

        #region 枚举 flags 判定

        /// <summary>位标志与普通枚举被正确区分（两个绘制器共用这份缓存）。</summary>
        [Test]
        public void flags判定()
        {
            Assert.That(EnumSupport.IsFlags(Find("paged")), Is.False);
            Assert.That(EnumSupport.IsFlags(Find("flags")), Is.True);
        }

        #endregion

        #region 回绕数学

        /// <summary>半开区间的回绕：等于上限回到下限，负值绕到上端。</summary>
        [Test]
        public void 回绕_半开区间()
        {
            Assert.That(WrapValues.Wrap(370, 0, 360), Is.EqualTo(10).Within(1e-9), "超出一圈。");
            Assert.That(WrapValues.Wrap(-10, 0, 360), Is.EqualTo(350).Within(1e-9), "负值绕到上端。");
            Assert.That(WrapValues.Wrap(0, 0, 360), Is.EqualTo(0).Within(1e-9));
            Assert.That(WrapValues.Wrap(360, 0, 360), Is.EqualTo(0).Within(1e-9), "等于上限回绕到下限。");
            Assert.That(WrapValues.Wrap(720 + 45, 0, 360), Is.EqualTo(45).Within(1e-9), "多圈取余。");
            Assert.That(WrapValues.Wrap(-370, 0, 360), Is.EqualTo(350).Within(1e-9));
        }

        /// <summary>非零起点：区间是 [min, max)。</summary>
        [Test]
        public void 回绕_非零起点()
        {
            Assert.That(WrapValues.Wrap(5, 10, 20), Is.EqualTo(15).Within(1e-9), "低于下限绕到上端。");
            Assert.That(WrapValues.Wrap(25, 10, 20), Is.EqualTo(15).Within(1e-9), "高于上限绕回。");
        }

        /// <summary>非法范围与非有限值原样返回，不抛、不产生 NaN。</summary>
        [Test]
        public void 回绕_非法输入原样返回()
        {
            Assert.That(WrapValues.Wrap(5, 10, 10), Is.EqualTo(5), "空区间不动。");
            Assert.That(double.IsNaN(WrapValues.Wrap(double.NaN, 0, 1)), Is.True);
            Assert.That(double.IsPositiveInfinity(WrapValues.Wrap(double.PositiveInfinity, 0, 1)), Is.True);
        }

        /// <summary>写回序列化属性：整数四舍五入，浮点原值。</summary>
        [Test]
        public void 回绕_写回序列化属性()
        {
            var intProperty = Property("wrappedInt");
            Assert.That(WrapValues.TryWrap(intProperty, 0, 10), Is.True);
            Assert.That(intProperty.longValue, Is.EqualTo(5), "25 绕回 [0,10) → 5。");
            Assert.That(WrapValues.TryWrap(intProperty, 0, 10), Is.False, "已就位时不再写、返回 false。");

            var floatProperty = Property("angle");
            Assert.That(WrapValues.TryWrap(floatProperty, 0f, 360f), Is.True);
            Assert.That(floatProperty.doubleValue, Is.EqualTo(40f).Within(1e-4), "400 绕回 40。");
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
        /// <returns>序列化属性。</returns>
        private SerializedProperty Property(string path)
        {
            return _serializedObject.FindProperty(path);
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
    }

    /// <summary>值控件测试用的资产。</summary>
    internal sealed class ValueControlFixture : ScriptableObject
    {
        /// <summary>多行文本域。</summary>
        [MultiLineProperty(5)]
        public string notes = "";

        /// <summary>延迟提交的整数。</summary>
        [DelayedProperty]
        public int delayedInt = 1;

        /// <summary>翻页枚举。</summary>
        [EnumPaging]
        public ControlEnum paged = ControlEnum.Alpha;

        /// <summary>滑块。</summary>
        [PropertyRange(0, 100)]
        public float ranged = 30f;

        /// <summary>浮点回绕。</summary>
        [Wrap(0f, 360f)]
        public float angle = 400f;

        /// <summary>整数回绕。</summary>
        [Wrap(0, 10)]
        public int wrappedInt = 25;

        /// <summary>位标志（EnumSupport 判定用）。</summary>
        public ControlFlags flags;

        /// <summary>无特性的对照。</summary>
        public int plain;
    }

    /// <summary>枚举测试用类型。</summary>
    internal enum ControlEnum
    {
        /// <summary>第一项。</summary>
        Alpha,

        /// <summary>第二项。</summary>
        Beta,

        /// <summary>第三项。</summary>
        Gamma,
    }

    /// <summary>位标志测试用类型。</summary>
    [System.Flags]
    internal enum ControlFlags
    {
        /// <summary>无。</summary>
        None = 0,

        /// <summary>甲。</summary>
        First = 1,

        /// <summary>乙。</summary>
        Second = 2,
    }
}
