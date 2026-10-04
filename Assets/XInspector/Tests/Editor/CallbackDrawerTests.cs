using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 三个回调特性：<c>[OnInspectorGUI]</c>、<c>[CustomContextMenu]</c>、<c>[OnValueChanged]</c>。
    /// <para>
    /// 绘制本身测不了，但「解析成没成、菜单项收齐没、值变了判断得出吗」全在这里——
    /// 那正是「配了却没反应」的成因。
    /// </para>
    /// </summary>
    [TestFixture]
    public class CallbackDrawerTests
    {
        #region Private Fields

        private CallbackFixture _target;

        #endregion

        #region Setup / Teardown

        /// <summary>建立测试资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<CallbackFixture>();
        }

        /// <summary>销毁资产并复位静态门面。</summary>
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

        #region OnInspectorGUI

        /// <summary>标了它的方法拿一个方法节点，链上有自己的绘制器。</summary>
        [Test]
        public void 自定义绘制拿一个方法节点()
        {
            using (var tree = Build())
            {
                var node = Find(tree, "DrawIt()");

                Assert.That(node, Is.Not.Null, "[OnInspectorGUI] 该像 [Button] 那样拿到方法节点。");
                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.Method));
                Assert.That(node.ValueEntry, Is.Null);

                var entries = node.Chain.Entries;
                Assert.That(entries[entries.Length - 1].Drawer, Is.TypeOf<MethodTerminalDrawer>());

                var found = false;
                for (var i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Drawer is OnInspectorGUIDrawer)
                    {
                        found = true;
                    }
                }

                Assert.That(found, Is.True, "链上没有 OnInspectorGUIDrawer。");
            }
        }

        /// <summary>备好了单元素的方法与目标数组——绘制是每帧路径，不该现建。</summary>
        [Test]
        public void 自定义绘制备好单元素数组()
        {
            using (var tree = Build())
            {
                var state = Find(tree, "DrawIt()").State.Get<CustomGuiState>();

                Assert.That(state.Reason, Is.Null);
                Assert.That(state.SingleMethod.Length, Is.EqualTo(1));
                Assert.That(state.SingleTarget.Length, Is.EqualTo(1));
                Assert.That(state.SingleTarget[0], Is.SameAs(_target));
            }
        }

        /// <summary>带参数时给出原因并告警——调不动的方法不该占着一个空位置。</summary>
        [Test]
        public void 自定义绘制带参数时告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex("无参、非泛型"));

            var broken = ScriptableObject.CreateInstance<CallbackBrokenFixture>();

            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(broken)))
                {
                    var state = Find(tree, "DrawWithParameter()").State.Get<CustomGuiState>();

                    Assert.That(state.SingleMethod, Is.Null);
                    Assert.That(state.Reason, Does.Contain("无参"));
                }
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        #endregion

        #region CustomContextMenu

        /// <summary>同一个字段上的多项按声明顺序进菜单。</summary>
        [Test]
        public void 菜单项按声明顺序收集()
        {
            using (var tree = Build())
            {
                var state = Find(tree, "twoMenus").State.Get<ContextMenuState>();

                Assert.That(state.Entries.Count, Is.EqualTo(2));
                Assert.That(state.Entries[0].MenuItem, Is.EqualTo("第一项"));
                Assert.That(state.Entries[1].MenuItem, Is.EqualTo("第二项"));
                Assert.That(state.Entries[0].Methods[0].Name, Is.EqualTo("First"));
                Assert.That(state.Entries[1].Methods[0].Name, Is.EqualTo("Second"));
            }
        }

        /// <summary>处理右键的是**第一项**——每项各有一格绘制器，但右键只该弹一次菜单。</summary>
        [Test]
        public void 右键由第一项处理()
        {
            using (var tree = Build())
            {
                var state = Find(tree, "twoMenus").State.Get<ContextMenuState>();
                var attributes = Find(tree, "twoMenus").Attributes;

                var first = attributes.Get<CustomContextMenuAttribute>();

                Assert.That(state.Owner, Is.Not.Null);
                Assert.That(ReferenceEquals(state.Owner, first), Is.True, "该由第一个特性实例处理。");
            }
        }

        /// <summary>方法解析不到时告警，且该项没有可调用的方法（菜单里会是灰的）。</summary>
        [Test]
        public void 菜单项方法不存在时告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex("无法调用"));

            var broken = ScriptableObject.CreateInstance<CallbackBrokenFixture>();

            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(broken)))
                {
                    var state = Find(tree, "brokenMenu").State.Get<ContextMenuState>();

                    Assert.That(state.Entries.Count, Is.EqualTo(1));
                    Assert.That(state.Entries[0].Methods, Is.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        #endregion

        #region OnValueChanged

        /// <summary>监听项被解析出来。</summary>
        [Test]
        public void 值变化监听被解析()
        {
            using (var tree = Build())
            {
                var state = Find(tree, "watched").State.Get<ValueChangedState>();

                Assert.That(state.Entries.Count, Is.EqualTo(1));
                Assert.That(state.Entries[0].Methods[0].Name, Is.EqualTo("OnWatchedChanged"));
            }
        }

        /// <summary>值真的变了才判定为变。</summary>
        [Test]
        public void 值快照察觉变化()
        {
            var serialized = new SerializedObject(_target);
            var property = serialized.FindProperty(nameof(CallbackFixture.number));

            var before = ValueSnapshot.Capture(property);
            property.intValue = 42;
            var after = ValueSnapshot.Capture(property);

            Assert.That(after.DiffersFrom(before), Is.True);
        }

        /// <summary>值没变就不该误报——否则每帧都会触发一次。</summary>
        [Test]
        public void 值快照不误报()
        {
            var serialized = new SerializedObject(_target);
            var property = serialized.FindProperty(nameof(CallbackFixture.number));

            var before = ValueSnapshot.Capture(property);
            var after = ValueSnapshot.Capture(property);

            Assert.That(after.DiffersFrom(before), Is.False);
        }

        /// <summary>字符串、向量与对象引用都在支持集里。</summary>
        [Test]
        public void 支持常见类型()
        {
            Assert.That(ValueSnapshot.IsSupported(SerializedPropertyType.String), Is.True);
            Assert.That(ValueSnapshot.IsSupported(SerializedPropertyType.Vector3), Is.True);
            Assert.That(ValueSnapshot.IsSupported(SerializedPropertyType.ObjectReference), Is.True);
            Assert.That(ValueSnapshot.IsSupported(SerializedPropertyType.Color), Is.True);
        }

        /// <summary>
        /// 数组这类没有覆盖到的类型**明确判为不支持**——调用方据此告警一次，
        /// 而不是假装监听着（「改了但没反应」比「压根没监听」难查得多）。
        /// </summary>
        [Test]
        public void 数组类型判为不支持()
        {
            var serialized = new SerializedObject(_target);
            var property = serialized.FindProperty(nameof(CallbackFixture.numbers));

            Assert.That(property.propertyType, Is.EqualTo(SerializedPropertyType.Generic));
            Assert.That(ValueSnapshot.IsSupported(property.propertyType), Is.False);
        }

        #endregion

        #region Private Helpers

        /// <summary>建一棵树。</summary>
        /// <returns>属性树。</returns>
        private PropertyTree Build()
        {
            return PropertyTree.Create(new SerializedObject(_target));
        }

        /// <summary>按路径找根下的子节点。</summary>
        /// <param name="tree">属性树。</param>
        /// <param name="path">路径。</param>
        /// <returns>节点；找不到时返回 <c>null</c>。</returns>
        private static InspectorProperty Find(PropertyTree tree, string path)
        {
            foreach (var child in tree.Root.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            return null;
        }

        #endregion
    }

    /// <summary>回调特性测试用的资产：三条路径各一份。</summary>
    internal sealed class CallbackFixture : ScriptableObject
    {
        /// <summary>给值快照用的整型字段。</summary>
        public int number;

        /// <summary>给「不支持的类型」用的列表。</summary>
        public List<int> numbers = new List<int>();

        /// <summary>自定义绘制。</summary>
        [OnInspectorGUI]
        private void DrawIt()
        {
        }

        /// <summary>两项菜单，用来验证顺序与「谁处理右键」。</summary>
        [CustomContextMenu("第一项", nameof(First))]
        [CustomContextMenu("第二项", nameof(Second))]
        public int twoMenus;

        /// <summary>值变化监听。</summary>
        [OnValueChanged(nameof(OnWatchedChanged))]
        public int watched;

        /// <summary>菜单项之一。</summary>
        private void First()
        {
        }

        /// <summary>菜单项之二。</summary>
        private void Second()
        {
        }

        /// <summary>值变化的回调。</summary>
        private void OnWatchedChanged()
        {
        }
    }

    /// <summary>三处失败情形各来一个：带参的绘制方法、解析不到的方法名。</summary>
    internal sealed class CallbackBrokenFixture : ScriptableObject
    {
        /// <summary>带参数的绘制方法——调不动。</summary>
        [OnInspectorGUI]
        private void DrawWithParameter(int amount)
        {
        }

        /// <summary>菜单项指向一个不存在的方法。</summary>
        [CustomContextMenu("坏的", "NoSuchMethod")]
        public int brokenMenu;
    }
}
