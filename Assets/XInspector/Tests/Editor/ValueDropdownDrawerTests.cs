using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[ValueDropdown]</c>：链装配、来源解析、选项表与值复制。
    /// <para>
    /// <b>测不了的</b>：弹出菜单本身的渲染与点击（<c>GenericMenu</c> 是原生菜单，
    /// 本仓策略不测 IMGUI）。故这里测的是「来源解析对不对」「选项表怎么排」
    /// 「值怎么复制、什么时候**拒绝**复制」——菜单只是把它们串起来。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ValueDropdownDrawerTests
    {
        #region Private Fields

        private ValueDropdownFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产与序列化对象。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ValueDropdownFixture>();
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

        #region 链装配与来源解析

        /// <summary>绘制器在链上，且排在末端之前。</summary>
        [Test]
        public void 绘制器在链上()
        {
            AssertDrawer<ValueDropdownDrawer>("picked");
            AssertDrawer<ValueDropdownDrawer>("treePicked");
            AssertDrawer<ValueDropdownDrawer>("appended");
        }

        /// <summary>来源解析成句柄，并记进状态。</summary>
        [Test]
        public void 来源解析成句柄()
        {
            var state = Find("picked").State.Get<ValueDropdownState>();

            Assert.That(state, Is.Not.Null);
            Assert.That(state.Resolved, Is.True);
            Assert.That(state.Source, Is.Not.Null);
            Assert.That(state.Source.propertyPath, Is.EqualTo("names"));
            Assert.That(state.Source.isArray, Is.True);
        }

        /// <summary>来源不存在时解析失败、告警一次，绘制器据此退回普通绘制。</summary>
        [Test]
        public void 来源不存在时解析失败()
        {
            LogAssert.Expect(LogType.Warning, new Regex("\\[ValueDropdown\\].*无法解析"));

            var state = Find("broken").State.Get<ValueDropdownState>();

            Assert.That(state.Resolved, Is.False);
            Assert.That(state.Source, Is.Null);
        }

        /// <summary>来源存在但不是数组/List 时同样失败——类型要求写在告警里。</summary>
        [Test]
        public void 来源不是数组时解析失败()
        {
            LogAssert.Expect(LogType.Warning, new Regex("\\[ValueDropdown\\].*不是\\s*数组或 List"));

            var state = Find("notArray").State.Get<ValueDropdownState>();

            Assert.That(state.Resolved, Is.False);
        }

        #endregion

        #region 选项表

        /// <summary>选项表按来源顺序建，路径就是元素的可读文本。</summary>
        [Test]
        public void 选项表按来源顺序()
        {
            var options = ValueDropdownOptions.Build(Source("names"), false, false);

            Assert.That(options.Count, Is.EqualTo(3));
            Assert.That(options[0].Path, Is.EqualTo("简单"));
            Assert.That(options[1].Path, Is.EqualTo("普通"));
            Assert.That(options[2].Path, Is.EqualTo("困难"));
            Assert.That(options[0].Index, Is.EqualTo(0), "下标要指回来源数组。");
        }

        /// <summary>树形：路径里的 <c>/</c> 原样留着，交给 GenericMenu 切子菜单。</summary>
        [Test]
        public void 树形路径原样保留()
        {
            var options = ValueDropdownOptions.Build(Source("paths"), false, false);

            Assert.That(options[0].Path, Is.EqualTo("武器/剑"));
        }

        /// <summary>拍平：斜杠换成中点，不再分层。</summary>
        [Test]
        public void 拍平把斜杠换掉()
        {
            var options = ValueDropdownOptions.Build(Source("paths"), true, false);

            Assert.That(options[0].Path, Is.EqualTo("武器›剑"));
        }

        /// <summary>排序按路径的序数比较（跨平台稳定），下标跟着走。</summary>
        [Test]
        public void 排序()
        {
            var options = ValueDropdownOptions.Build(Source("unsorted"), false, true);

            Assert.That(options[0].Path, Is.EqualTo("A"));
            Assert.That(options[1].Path, Is.EqualTo("B"));
            Assert.That(options[2].Path, Is.EqualTo("C"));
            Assert.That(options[0].Index, Is.EqualTo(2), "下标指向来源里的原位（来源是 C/B/A，A 在 2）。");
        }

        /// <summary>空来源返回空表——调用方据此不弹菜单。</summary>
        [Test]
        public void 空来源返回空表()
        {
            Assert.That(ValueDropdownOptions.Build(Source("empty"), false, false).Count, Is.EqualTo(0));
            Assert.That(ValueDropdownOptions.Build(null, false, false).Count, Is.EqualTo(0));
        }

        /// <summary>枚举元素取的是成员名，不是序号。</summary>
        [Test]
        public void 枚举元素取成员名()
        {
            var options = ValueDropdownOptions.Build(Source("colors"), false, false);

            Assert.That(options[0].Path, Is.EqualTo("Physical"));
            Assert.That(options[1].Path, Is.EqualTo("Fire"));
        }

        #endregion

        #region 值复制

        /// <summary>同类型的值照常复制。</summary>
        [Test]
        public void 同类型复制()
        {
            var source = Source("names").GetArrayElementAtIndex(1);
            var destination = Property("picked");

            Assert.That(SerializedValueCopier.TryCopy(source, destination), Is.True);
            Assert.That(destination.stringValue, Is.EqualTo("普通"));
        }

        /// <summary>类型不同时**什么都不写**并返回 false。</summary>
        [Test]
        public void 类型不同拒绝复制()
        {
            var source = Source("names").GetArrayElementAtIndex(0);
            var destination = Property("count");

            Assert.That(SerializedValueCopier.TryCopy(source, destination), Is.False);
            Assert.That(destination.longValue, Is.EqualTo(7), "拒绝时必须原样不动。");
        }

        /// <summary>
        /// 两个**不同的**枚举：<c>propertyType</c> 都是 Enum，但成员名对不上，必须拒绝。
        /// </summary>
        /// <remarks>只比 <c>propertyType</c> 的话这里会静默写错——这正是加第二道判定的理由。</remarks>
        [Test]
        public void 不同枚举拒绝复制()
        {
            var source = Source("colors").GetArrayElementAtIndex(0);
            var destination = Property("status");

            Assert.That(SerializedValueCopier.TryCopy(source, destination), Is.False);
            Assert.That(destination.enumValueIndex, Is.EqualTo(0), "拒绝时必须原样不动。");
        }

        /// <summary>同一个枚举的两端可以复制。</summary>
        [Test]
        public void 同枚举复制()
        {
            var source = Source("colors").GetArrayElementAtIndex(1);
            var destination = Property("damage");

            Assert.That(SerializedValueCopier.TryCopy(source, destination), Is.True);
            Assert.That(destination.enumValueIndex, Is.EqualTo(1));
        }

        /// <summary>对象引用复制的是引用本身。</summary>
        [Test]
        public void 对象引用复制()
        {
            // 元素得真有东西——空数组取下标是越界的（这条用例第一版就栽在这）。
            _target.objects[1] = _target;
            _serializedObject.Update();

            var source = Source("objects").GetArrayElementAtIndex(1);
            var destination = Property("target");

            Assert.That(SerializedValueCopier.TryCopy(source, destination), Is.True);
            Assert.That(destination.objectReferenceValue, Is.SameAs(_target), "指向同一个对象。");
        }

        /// <summary>null 不抛，直接判失败。</summary>
        [Test]
        public void 空属性判失败()
        {
            Assert.That(SerializedValueCopier.TryCopy(null, Property("picked")), Is.False);
            Assert.That(SerializedValueCopier.TryCopy(Source("names").GetArrayElementAtIndex(0), null), Is.False);
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

        /// <summary>取某个数组字段的序列化属性。</summary>
        /// <param name="path">字段名。</param>
        /// <returns>序列化属性。</returns>
        private SerializedProperty Source(string path)
        {
            return _serializedObject.FindProperty(path);
        }

        /// <summary>取某个字段的序列化属性。</summary>
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

        /// <summary>来源用的枚举。</summary>
        private enum DamageType
        {
            /// <summary>物理。</summary>
            Physical = 0,

            /// <summary>火焰。</summary>
            Fire = 1,
        }

        /// <summary>另一个枚举——成员名与上一个不同，用于验证「不同枚举拒绝复制」。</summary>
        private enum StatusFlags
        {
            /// <summary>无。</summary>
            None = 0,

            /// <summary>中毒。</summary>
            Poisoned = 1,
        }

        /// <summary><c>[ValueDropdown]</c> 的测试宿主。</summary>
        private class ValueDropdownFixture : ScriptableObject
        {
            /// <summary>字符串来源。</summary>
            public string[] names = { "简单", "普通", "困难" };

            /// <summary>树形路径来源。</summary>
            public string[] paths = { "武器/剑", "防具/盾" };

            /// <summary>乱序来源（排序用）。</summary>
            public string[] unsorted = { "C", "B", "A" };

            /// <summary>空来源。</summary>
            public string[] empty = new string[0];

            /// <summary>枚举来源。</summary>
            public DamageType[] colors = { DamageType.Physical, DamageType.Fire };

            /// <summary>对象引用来源（留两格，用例自己填）。</summary>
            public Object[] objects = new Object[2];

            /// <summary>被下拉标注的字符串字段。</summary>
            [ValueDropdown("names")]
            public string picked;

            /// <summary>树形下拉。</summary>
            [ValueDropdown("paths")]
            public string treePicked;

            /// <summary>小按钮形态。</summary>
            [ValueDropdown("names", AppendNextDrawer = true)]
            public string appended;

            /// <summary>来源名不存在。</summary>
            [ValueDropdown("没有这个成员")]
            public string broken;

            /// <summary>来源存在但不是数组。</summary>
            [ValueDropdown("count")]
            public int notArray;

            /// <summary>非数组的对照（类型校验用）。</summary>
            public int count = 7;

            /// <summary>另一个枚举的对照（枚举判定用）。</summary>
            public StatusFlags status;

            /// <summary>与来源同枚举的字段。</summary>
            [ValueDropdown("colors")]
            public DamageType damage;

            /// <summary>对象引用字段。</summary>
            [ValueDropdown("objects")]
            public Object target;
        }
    }
}
