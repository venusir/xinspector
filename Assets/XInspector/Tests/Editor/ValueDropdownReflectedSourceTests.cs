using System;
using System.Collections;
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
    /// <c>[ValueDropdown]</c> 的**第二种数据源形态**：声明类型实现 <see cref="IList"/> 的
    /// 普通字段 / 属性 / 无参方法。
    /// <para>
    /// 断三件事：解析出的状态形状（哪一个来源非空）、选项表怎么造、以及**来源形态收不下的那些
    /// 情况都明说**（字符串、只实现 <c>IEnumerable</c> 的类型——它们是静默出错的重灾区）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ValueDropdownReflectedSourceTests
    {
        #region Private Fields

        private ReflectedSourceFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产与序列化对象。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ReflectedSourceFixture>();
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

            _tree?.Dispose();
            _tree = null;
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 解析出的状态

        /// <summary>反射属性源：状态里给的是**读取器**，句柄与元素类型各归各位。</summary>
        [Test]
        public void 反射属性源解析成读取器()
        {
            var state = State("pickedByProperty");

            Assert.That(state.Resolved, Is.True);
            Assert.That(state.Source, Is.Null, "反射形态没有句柄。");
            Assert.That(state.SourceList, Is.Not.Null);
            Assert.That(state.SourceList(), Is.EqualTo(new List<string> { "甲", "乙" }));
            Assert.That(state.ElementType, Is.EqualTo(typeof(string)), "元素声明类型从 List<string> 推出来。");
        }

        /// <summary>反射方法源同样解析成读取器，且**每帧现读**。</summary>
        [Test]
        public void 反射方法源解析成读取器()
        {
            var state = State("pickedByMethod");

            Assert.That(state.SourceList(), Is.EqualTo(new List<int> { 1, 2 }));
            Assert.That(state.ElementType, Is.EqualTo(typeof(int)));

            _target.offset = 10;

            Assert.That(state.SourceList(), Is.EqualTo(new List<int> { 11, 12 }), "方法每次调用吃当时的字段。");
        }

        /// <summary>序列化源照旧：**给句柄、不给读取器**——两个形态的判别式就是这一对。</summary>
        [Test]
        public void 序列化源仍给句柄()
        {
            var state = State("pickedByArray");

            Assert.That(state.Resolved, Is.True);
            Assert.That(state.Source, Is.Not.Null);
            Assert.That(state.Source.propertyPath, Is.EqualTo("serializedOptions"));
            Assert.That(state.SourceList, Is.Null);
            Assert.That(state.ElementType, Is.Null, "序列化形态的元素类型由句柄自己给。");
        }

        /// <summary>
        /// 字符串**不是**选项来源：它只实现 <c>IEnumerable&lt;char&gt;</c>，放行会静默变出
        /// 一张字符表（或一张空表）。解析必须失败并说清原因。
        /// </summary>
        [Test]
        public void 字符串源解析失败并说清原因()
        {
            LogAssert.Expect(LogType.Warning, new Regex("\\[ValueDropdown\\].*不是\\s*数组或 List"));

            var state = State("brokenByString");

            Assert.That(state.Resolved, Is.False);
            Assert.That(state.Source, Is.Null);
            Assert.That(state.SourceList, Is.Null);
        }

        /// <summary>
        /// 元素层的来源指向**那个元素实例**上的成员——拥有者上放着同名陷阱。
        /// </summary>
        /// <remarks>
        /// 起点是**元素里面的成员节点**而不是元素节点本身：元素节点往上的「最近的复合成员容器」
        /// 是**集合**（`List&lt;T&gt;`），那是这条阶梯既有的语义，与元素层无关。
        /// 元素类型里写 <c>[ValueDropdown]</c> 的字段，节点本来就落在元素**里面**。
        /// </remarks>
        [Test]
        public void 元素层解析到元素实例上的列表源()
        {
            var node = Find("items.Array.data[0].picked");

            Assert.That(
                MemberReferenceResolver.TryResolveList(
                    node, "Options", MemberScope.Object,
                    out var read, out _, out var elementType, out var reason),
                Is.True,
                reason);
            Assert.That(read(), Is.EqualTo(new List<string> { "元素甲" }), "不是拥有者那份。");
            Assert.That(elementType, Is.EqualTo(typeof(string)));
            Assert.That(_target.Options, Is.EqualTo(new List<string> { "甲", "乙" }), "控制项：拥有者那份确实不同。");
        }

        #endregion

        #region 选项表

        /// <summary>标签走的是**共享的格式化器**（与 <c>[ShowInInspector]</c> 的只读展示同一个）。</summary>
        /// <remarks>
        /// 声明类型给 <c>null</c> 时退回运行时类型——不这么做，数字会落到跟随当前文化的
        /// <c>ToString()</c>，于是这条断言在德语环境下会红。
        /// </remarks>
        [Test]
        public void 反射源的标签走共享格式化器()
        {
            var values = new List<object> { "甲", 2, true, 1.5f, string.Empty };
            var options = ValueDropdownOptions.Build(values, null, false, false);

            Assert.That(options[0].Path, Is.EqualTo("甲"));
            Assert.That(options[1].Path, Is.EqualTo("2"));
            Assert.That(options[2].Path, Is.EqualTo("True"));
            Assert.That(options[3].Path, Is.EqualTo("1.5"), "浮点用不变文化，且不拖尾巴。");
            Assert.That(options[4].Path, Is.EqualTo("(空)"), "空文本与序列化形态同款——否则菜单里是一行看不见的东西。");
        }

        /// <summary>元素声明类型参与格式化：同一个空值，Unity 对象当 <c>None</c>、其余当 <c>null</c>。</summary>
        [Test]
        public void 声明类型参与空值的格式化()
        {
            Assert.That(
                ValueDropdownOptions.Build(new List<object> { null }, typeof(GameObject), false, false)[0].Path,
                Is.EqualTo("None"));
            Assert.That(
                ValueDropdownOptions.Build(new List<object> { null }, null, false, false)[0].Path,
                Is.EqualTo("null"));
        }

        /// <summary>树形、排序与「空文本」三条规矩与序列化形态逐字同款。</summary>
        [Test]
        public void 树形与排序与序列化形态同款()
        {
            var treed = ValueDropdownOptions.Build(
                new List<object> { "武器/剑", "防具/盾" }, typeof(string), false, false);
            Assert.That(treed[0].Path, Is.EqualTo("武器/剑"), "斜杠原样交给菜单分层的。");

            var flat = ValueDropdownOptions.Build(new List<object> { "武器/剑" }, typeof(string), true, false);
            Assert.That(flat[0].Path, Is.EqualTo("武器›剑"));

            // 排序是**序数比较**（跨平台稳定），故这里用 ASCII 让期望值一眼可读。
            var sorted = ValueDropdownOptions.Build(new List<object> { "b", "a" }, typeof(string), false, true);
            Assert.That(sorted[0].Path, Is.EqualTo("a"));
            Assert.That(sorted[1].Path, Is.EqualTo("b"));
            Assert.That(sorted[0].Index, Is.EqualTo(1), "下标跟着那一条走，不因排序而重排。");
        }

        /// <summary>同一批字符串走两个形态，标签逐字相同（共有部分；浮点那类差异见已知限制）。</summary>
        [Test]
        public void 两形态在字符串上标签一致()
        {
            var serialized = ValueDropdownOptions.Build(
                _serializedObject.FindProperty("serializedOptions"), false, false);
            var reflected = ValueDropdownOptions.Build(
                new List<object> { "x", "y" }, typeof(string), false, false);

            Assert.That(reflected.Count, Is.EqualTo(serialized.Count));

            for (var i = 0; i < serialized.Count; i++)
            {
                Assert.That(reflected[i].Path, Is.EqualTo(serialized[i].Path));
            }
        }

        /// <summary>来源为 <c>null</c>（此刻取不到实例）与「有值但是空的」都返回空表。</summary>
        [Test]
        public void 空来源与取不到的来源都给空表()
        {
            Assert.That(ValueDropdownOptions.Build((IList)null, typeof(string), false, false).Count, Is.EqualTo(0));
            Assert.That(
                ValueDropdownOptions.Build(new List<object>(), typeof(string), false, false).Count,
                Is.EqualTo(0));
        }

        #endregion

        #region 元素类型推导

        /// <summary>从静态类型推元素类型：数组、泛型、自定义派生各一档，推不出来给 <c>null</c>。</summary>
        [Test]
        public void 列表类型推元素类型()
        {
            Assert.That(ListElementType.TypeOf(typeof(int[])), Is.EqualTo(typeof(int)));
            Assert.That(ListElementType.TypeOf(typeof(List<string>)), Is.EqualTo(typeof(string)));
            Assert.That(ListElementType.TypeOf(typeof(ReflectedSourceList)), Is.EqualTo(typeof(string)), "自定义派生。");
            Assert.That(ListElementType.TypeOf(typeof(string)), Is.Null, "字符串不是列表。");
            Assert.That(ListElementType.TypeOf(typeof(HashSet<int>)), Is.Null, "只实现 IEnumerable 的不认。");
            Assert.That(ListElementType.TypeOf(typeof(ArrayList)), Is.Null, "非泛型集合没有元素类型可言。");
            Assert.That(ListElementType.TypeOf(null), Is.Null);
        }

        #endregion

        #region Private Helpers

        /// <summary>按路径取节点，树在首次调用时构建。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>节点。</returns>
        private InspectorProperty Find(string path)
        {
            if (_tree == null)
            {
                _tree = PropertyTree.Create(new SerializedObject(_target));
            }

            return Search(_tree.Root, path) ?? Fail(path);
        }

        /// <summary>取某个成员节点上的解析结果。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>解析结果。</returns>
        private ValueDropdownState State(string path)
        {
            var state = Find(path).State.Get<ValueDropdownState>();

            Assert.That(state, Is.Not.Null, $"{path} 上没有解析结果——处理器没跑？");
            return state;
        }

        /// <summary>递归搜索节点。</summary>
        /// <param name="node">当前节点。</param>
        /// <param name="path">目标路径。</param>
        /// <returns>节点；不存在返回 <c>null</c>。</returns>
        private static InspectorProperty Search(InspectorProperty node, string path)
        {
            foreach (var child in node.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }

                var nested = Search(child, path);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        /// <summary>找不到节点时让断言失败。</summary>
        /// <param name="path">目标路径。</param>
        /// <returns>不会返回。</returns>
        private static InspectorProperty Fail(string path)
        {
            Assert.Fail($"找不到节点 {path}。");
            return null;
        }

        #endregion
    }

    /// <summary>反射数据源的夹具：来源侧三种形态各一，目标侧一一对应。</summary>
    internal sealed class ReflectedSourceFixture : ScriptableObject
    {
        /// <summary>方法源要读的字段——改它就能看出「每帧现读」。</summary>
        public int offset;

        /// <summary>与元素层同名的非序列化列表属性（拥有者那份）——「看错对象」的陷阱。</summary>
        public List<string> Options => new List<string> { "甲", "乙" };

        /// <summary>无参返回列表的方法——反射方法那一格。</summary>
        /// <returns>内容跟着 <see cref="offset"/> 走。</returns>
        public List<int> Numbers()
        {
            return new List<int> { offset + 1, offset + 2 };
        }

        /// <summary>序列化的数组——对照组。</summary>
        public string[] serializedOptions = { "x", "y" };

        /// <summary>序列化的字符串：当来源时必须被拒绝。</summary>
        public string textSource = "abc";

        /// <summary>目标：来源是反射属性。</summary>
        [ValueDropdown(nameof(Options))]
        public string pickedByProperty;

        /// <summary>目标：来源是反射方法。</summary>
        [ValueDropdown(nameof(Numbers))]
        public int pickedByMethod;

        /// <summary>目标：来源是序列化数组。</summary>
        [ValueDropdown(nameof(serializedOptions))]
        public string pickedByArray;

        /// <summary>目标：来源是字符串——解析失败。</summary>
        [ValueDropdown(nameof(textSource))]
        public string brokenByString;

        /// <summary>元素层：元素实例上也有一个 <c>Options</c>。</summary>
        [ListDrawerSettings]
        public List<ReflectedSourceElement> items = new List<ReflectedSourceElement>
        {
            new ReflectedSourceElement(),
        };
    }

    /// <summary>元素层的元素：自己带一份非序列化的选项列表。</summary>
    [Serializable]
    internal class ReflectedSourceElement
    {
        /// <summary>元素实例上的非序列化列表（与拥有者同名，内容不同）。</summary>
        public List<string> Options => new List<string> { "元素甲" };

        /// <summary>让元素类型进管线（也是元素层那条解析的起点）。</summary>
        [ValueDropdown(nameof(Options))]
        public string picked;
    }

    /// <summary>自定义的列表派生类型——推元素类型那一格用它。</summary>
    internal sealed class ReflectedSourceList : List<string>
    {
    }
}
