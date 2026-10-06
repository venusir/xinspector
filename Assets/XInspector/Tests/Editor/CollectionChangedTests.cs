using System;
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
    /// <c>[OnCollectionChanged]</c>：注入、两个方向的解析，以及**回调真的跑了没有**。
    /// <para>
    /// 用例直接调绘制器调的那个函数（<see cref="CollectionChangeInvoker.ApplyAdd"/> /
    /// <see cref="CollectionChangeInvoker.ApplyRemove"/>）——IMGUI 那一半不测，
    /// 而「谁在什么时机被调、拿到什么」这一半是可无头测的，正是该钉住的部分。
    /// </para>
    /// </summary>
    [TestFixture]
    public class CollectionChangedTests
    {
        #region Fixture

        /// <summary>复位静态门面：建树会初始化绘制器与处理器两张注册表。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 注入

        /// <summary>没标列表设置时由处理器补一份——不补就「写了没反应」（增删只有集合绘制器有落点）。</summary>
        [Test]
        public void 标了集合回调时注入列表设置()
        {
            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "numbers");

                    Assert.That(node.Attributes.Has<ListDrawerSettingsAttribute>(), Is.True);
                    Assert.That(IndexOf<ListDrawerSettingsDrawer>(node), Is.GreaterThanOrEqualTo(0), "链上要有增删的落点。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>标在非集合上：构建期告警、不注入（没有增删就无从触发）。</summary>
        [Test]
        public void 标在非集合上时告警且不注入()
        {
            LogAssert.Expect(LogType.Warning, new Regex("只支持数组或 List"));

            var target = ScriptableObject.CreateInstance<CollectionChangedOnScalarFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "count");

                    Assert.That(node.Attributes.Has<ListDrawerSettingsAttribute>(), Is.False, "不该为一个不生效的特性改外观。");
                    Assert.That(IndexOf<ListDrawerSettingsDrawer>(node), Is.EqualTo(-1));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 解析

        /// <summary>两个方向各解析各的，名字与形状都记在状态里。</summary>
        [Test]
        public void 两个回调按名解析出来()
        {
            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var state = StateOf(tree, "numbers");

                    Assert.That(state.Before, Is.Not.Null);
                    Assert.That(state.After, Is.Not.Null);
                    Assert.That(state.Before.Name, Is.EqualTo("Before"));
                    Assert.That(state.Before.Methods[0].Name, Is.EqualTo("Before"));
                    Assert.That(state.After.Methods[0].Name, Is.EqualTo("After"));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>两种形状都收：无参的与收 <c>(CollectionChangeInfo, object)</c> 的。</summary>
        [Test]
        public void 无参形状与带信息形状都能解析()
        {
            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(StateOf(tree, "numbers").Before.TakesInfo, Is.True, "两参的那一份要收到信息。");
                    Assert.That(StateOf(tree, "onlyAfter").After.TakesInfo, Is.False, "无参的那一份不收。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>只配一个方向时另一个方向为 null——不触发。</summary>
        [Test]
        public void 只配一个方向时另一个为空()
        {
            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(StateOf(tree, "onlyBefore").Before, Is.Not.Null);
                    Assert.That(StateOf(tree, "onlyBefore").After, Is.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>形状不对时：解析不到、告警说明**可接受的形状**。</summary>
        [Test]
        public void 形状不对时告警并说明可接受的形状()
        {
            LogAssert.Expect(LogType.Warning, new Regex("参数表必须是以下之一"));

            var target = ScriptableObject.CreateInstance<CollectionChangedBadShapeFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(StateOf(tree, "wrongShape").Before, Is.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>方法名不存在时同样只是解析不到，增删照常（回调缺失不是数据的错）。</summary>
        [Test]
        public void 方法解析不到时增删照常()
        {
            LogAssert.Expect(LogType.Warning, new Regex("找不到"));

            var target = ScriptableObject.CreateInstance<CollectionChangedMissingFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "numbers");
                    var array = node.ValueEntry.SerializedProperty;

                    Assert.That(CollectionChangeInvoker.ApplyRemove(node, array, 0), Is.True);
                    Assert.That(array.arraySize, Is.EqualTo(2));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 触发

        /// <summary>删除：改动前与改动后**成对**触发，顺序与次数都对。</summary>
        [Test]
        public void 删除时改动前与改动后成对触发()
        {
            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "texts");
                    var array = node.ValueEntry.SerializedProperty;

                    Assert.That(CollectionChangeInvoker.ApplyRemove(node, array, 1), Is.True);

                    Assert.That(target.Log, Is.EqualTo(new[] { "before|RemoveAt|1|盾", "after|RemoveAt|1|盾" }));
                    Assert.That(array.arraySize, Is.EqualTo(1));
                    Assert.That(array.GetArrayElementAtIndex(0).stringValue, Is.EqualTo("剑"));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>改动前拿到的是**被删掉的那个值**——删完就读不到了，只能在这里读。</summary>
        [Test]
        public void 改动前拿到的是被删掉的那个值()
        {
            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "texts");

                    CollectionChangeInvoker.ApplyRemove(node, node.ValueEntry.SerializedProperty, 0);

                    Assert.That(target.LastValue, Is.EqualTo("剑"), "拿到的必须是下标 0 那一个。");
                    Assert.That(target.LastValue, Is.TypeOf<string>());
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>追加：下标是**改动前的长度**（新元素的位置），值为 <c>null</c>（副本没有信息量）。</summary>
        [Test]
        public void 追加时改动后拿到空值且下标是改动前的长度()
        {
            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "numbers");
                    var array = node.ValueEntry.SerializedProperty;

                    Assert.That(CollectionChangeInvoker.ApplyAdd(node, array), Is.True);

                    Assert.That(array.arraySize, Is.EqualTo(4));
                    Assert.That(
                        target.Log,
                        Is.EqualTo(new[] { "before|Add|3|", "after|Add|3|" }),
                        "下标 3 是新元素的位置（改动前的长度），值为 null。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>无参形状的回调也要真的被调起来（实参数组按形状给 null）。</summary>
        [Test]
        public void 无参回调也能被调起来()
        {
            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "onlyAfter");
                    var array = node.ValueEntry.SerializedProperty;

                    Assert.That(CollectionChangeInvoker.ApplyAdd(node, array), Is.True);

                    Assert.That(array.arraySize, Is.EqualTo(2));
                    Assert.That(target.Log, Is.EqualTo(new[] { "noargs" }), "只配了改动后，且它无参。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>只配一个方向时另一个真的不触发。</summary>
        [Test]
        public void 只配一个方向时另一个不触发()
        {
            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "onlyBefore");

                    Assert.That(CollectionChangeInvoker.ApplyAdd(node, node.ValueEntry.SerializedProperty), Is.True);

                    Assert.That(target.Log, Is.EqualTo(new[] { "before|Add|1|" }), "改动前触发、改动后为空。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>回调抛异常被吞掉并记录，**增删照常**（异常不该让数据改一半）。</summary>
        [Test]
        public void 回调抛异常不影响增删()
        {
            LogAssert.Expect(LogType.Exception, new Regex("boom"));

            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "throwing");
                    var array = node.ValueEntry.SerializedProperty;

                    Assert.That(CollectionChangeInvoker.ApplyRemove(node, array, 0), Is.True);
                    Assert.That(array.arraySize, Is.EqualTo(1));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>枚举元素给**枚举值本身**：不是下标、也不是底层数值（用户那句强转要能成立）。</summary>
        [Test]
        public void 枚举值按枚举类型装箱()
        {
            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "flavours");

                    CollectionChangeInvoker.ApplyRemove(node, node.ValueEntry.SerializedProperty, 1);

                    Assert.That(target.LastValue, Is.TypeOf<Flavour>());
                    Assert.That(target.LastValue, Is.EqualTo(Flavour.Bitter), "非连续取值也要给对（下标那套会给出 0）。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>整型与浮点按**声明类型**装箱：<c>int</c> 的元素给 <c>int</c>，不是 Unity 的 <c>long</c>。</summary>
        [Test]
        public void 数值按声明类型装箱()
        {
            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var numbers = Find(tree.Root, "numbers");
                    CollectionChangeInvoker.ApplyRemove(numbers, numbers.ValueEntry.SerializedProperty, 0);
                    Assert.That(target.LastValue, Is.TypeOf<int>());
                    Assert.That(target.LastValue, Is.EqualTo(1));

                    var weights = Find(tree.Root, "weights");
                    CollectionChangeInvoker.ApplyRemove(weights, weights.ValueEntry.SerializedProperty, 0);
                    Assert.That(target.LastValue, Is.TypeOf<float>());
                    Assert.That(target.LastValue, Is.EqualTo(1.5f));                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>复合元素读不出值：<c>Value</c> 为 null、告警一次，**回调照常触发**。</summary>
        [Test]
        public void 复合元素的值读不出来时回调照常触发()
        {
            LogAssert.Expect(LogType.Warning, new Regex("读不出被移除元素的值"));

            var target = ScriptableObject.CreateInstance<CollectionChangedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "rows");
                    var array = node.ValueEntry.SerializedProperty;

                    Assert.That(CollectionChangeInvoker.ApplyRemove(node, array, 0), Is.True);

                    Assert.That(array.arraySize, Is.EqualTo(0), "只有一个元素，删掉就空了。");
                    Assert.That(target.Log, Is.EqualTo(new[] { "before|RemoveAt|0|", "after|RemoveAt|0|" }));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 解析器

        /// <summary>形状表**按顺序**取第一个匹配——重载同名时写在前的赢。</summary>
        [Test]
        public void 形状表按顺序取第一个匹配()
        {
            var overloaded = MethodResolver.ByName(
                typeof(CollectionCallbackProbe),
                "Overloaded",
                new[] { Type.EmptyTypes, new[] { typeof(CollectionChangeInfo), typeof(object) } },
                out var reason);

            Assert.That(reason, Is.Null);
            Assert.That(overloaded, Is.Not.Null);
            Assert.That(overloaded.GetParameters().Length, Is.EqualTo(0), "空形状写在前面，它赢。");

            var reverse = MethodResolver.ByName(
                typeof(CollectionCallbackProbe),
                "Overloaded",
                new[] { new[] { typeof(CollectionChangeInfo), typeof(object) }, Type.EmptyTypes },
                out _);

            Assert.That(reverse.GetParameters().Length, Is.EqualTo(2), "顺序反过来，两参的那份赢。");
        }

        /// <summary>名字在、形状都不对时，原因里列出**可接受的形状**（用 C# 的写法）。</summary>
        [Test]
        public void 形状不对时列出可接受的形状()
        {
            var method = MethodResolver.ByName(
                typeof(CollectionCallbackProbe),
                "Bad",
                new[] { Type.EmptyTypes, new[] { typeof(CollectionChangeInfo), typeof(object) } },
                out var reason);

            Assert.That(method, Is.Null);
            Assert.That(reason, Does.Contain("()"));
            Assert.That(reason, Does.Contain("(CollectionChangeInfo, object)"), "印 object 而不是 Object——用户源码里就是那么写的。");
        }

        /// <summary>旧的按名解析（只收无参）**文案与行为都不变**——本条守着既有调用方的告警文本。</summary>
        [Test]
        public void 旧的按名解析文案不变()
        {
            var method = MethodResolver.ByName(typeof(CollectionCallbackProbe), "Bad", out var reason);

            Assert.That(method, Is.Null);
            Assert.That(reason, Does.Contain("无参且非泛型"));
        }

        #endregion

        #region Private Helpers

        /// <summary>构建被测的树。</summary>
        /// <param name="target">目标资产。</param>
        /// <returns>属性树。</returns>
        private static PropertyTree BuildTree(ScriptableObject target)
        {
            return PropertyTree.Create(new SerializedObject(target));
        }

        /// <summary>取某字段上的集合回调状态。</summary>
        /// <param name="tree">属性树。</param>
        /// <param name="path">字段路径。</param>
        /// <returns>状态。</returns>
        private static CollectionChangedState StateOf(PropertyTree tree, string path)
        {
            var state = Find(tree.Root, path).State.Get<CollectionChangedState>();

            Assert.That(state, Is.Not.Null, "处理器没跑（状态不是取出来的，是处理器装的）。");
            return state;
        }

        /// <summary>按路径查找直接子节点。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；不存在时断言失败。</returns>
        private static InspectorProperty Find(InspectorProperty parent, string path)
        {
            foreach (var child in parent.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            Assert.Fail($"找不到节点 {path}。");
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

    /// <summary>非连续的枚举：下标与取值不是一回事，专门用来钉「给的是值不是下标」。</summary>
    internal enum Flavour
    {
        /// <summary>取值 1。</summary>
        Sour = 1,

        /// <summary>取值 5（下标是 1）。</summary>
        Bitter = 5,
    }

    /// <summary>复合元素：值读不出来那条路的对照。</summary>
    [Serializable]
    internal struct CollectionRow
    {
        /// <summary>一个字段。</summary>
        public int level;
    }

    /// <summary>集合回调的对照资产。</summary>
    [HideMonoScript]
    internal sealed class CollectionChangedFixture : ScriptableObject
    {
        /// <summary>两参形状。</summary>
        [OnCollectionChanged(nameof(Before), nameof(After))]
        public int[] numbers = { 1, 2, 3 };

        /// <summary>字符串数组：值按 <c>string</c> 装箱。</summary>
        [OnCollectionChanged(nameof(Before), nameof(After))]
        public string[] texts = { "剑", "盾" };

        /// <summary>枚举元素：给枚举值本身。</summary>
        [OnCollectionChanged(nameof(Before), nameof(After))]
        public List<Flavour> flavours = new List<Flavour> { Flavour.Sour, Flavour.Bitter };

        /// <summary>浮点元素：给 <c>float</c>。</summary>
        [OnCollectionChanged(nameof(Before), nameof(After))]
        public List<float> weights = new List<float> { 1.5f };

        /// <summary>复合元素：读不出值那一档。</summary>
        [OnCollectionChanged(nameof(Before), nameof(After))]
        public List<CollectionRow> rows = new List<CollectionRow> { new CollectionRow { level = 3 } };

        /// <summary>只有改动后，而且是无参形状。</summary>
        [OnCollectionChanged(after: nameof(NoArgs))]
        public int[] onlyAfter = { 1 };

        /// <summary>只有改动前。</summary>
        [OnCollectionChanged(nameof(Before))]
        public int[] onlyBefore = { 1 };

        /// <summary>回调抛异常。</summary>
        [OnCollectionChanged(nameof(Throwing))]
        public int[] throwing = { 1, 2 };

        /// <summary>回调留下的文字轨迹。</summary>
        [NonSerialized]
        public readonly List<string> Log = new List<string>();

        /// <summary>最后一次回调看到的元素值（原样，不转字符串）。</summary>
        [NonSerialized]
        public object LastValue;

        private void Before(CollectionChangeInfo info, object value)
        {
            Log.Add($"before|{info.Type}|{info.Index}|{value}");
            LastValue = value;
        }

        private void After(CollectionChangeInfo info, object value)
        {
            Log.Add($"after|{info.Type}|{info.Index}|{value}");
            LastValue = value;
        }

        private void NoArgs()
        {
            Log.Add("noargs");
        }

        private void Throwing(CollectionChangeInfo info, object value)
        {
            throw new InvalidOperationException("boom");
        }
    }

    /// <summary>标在非集合上的对照资产（形状不对，构建期告警）。</summary>
    [HideMonoScript]
    internal sealed class CollectionChangedOnScalarFixture : ScriptableObject
    {
        /// <summary>标在标量上。</summary>
        [OnCollectionChanged("Whatever")]
        public int count = 1;

        private void Whatever(CollectionChangeInfo info, object value)
        {
        }
    }

    /// <summary>回调形状不对的对照资产。</summary>
    [HideMonoScript]
    internal sealed class CollectionChangedBadShapeFixture : ScriptableObject
    {
        /// <summary>回调收一个 <c>int</c>：两种形状都不是。</summary>
        [OnCollectionChanged(nameof(BadShape))]
        public int[] wrongShape = { 1 };

        private void BadShape(int value)
        {
        }
    }

    /// <summary>回调名字写错的对照资产。</summary>
    [HideMonoScript]
    internal sealed class CollectionChangedMissingFixture : ScriptableObject
    {
        /// <summary>方法名根本不存在。</summary>
        [OnCollectionChanged("NotHere")]
        public int[] numbers = { 1, 2, 3 };
    }

    /// <summary>解析器用的探针：重载与错形状各一。</summary>
    internal sealed class CollectionCallbackProbe
    {
        /// <summary>无参重载。</summary>
        public void Overloaded()
        {
        }

        /// <summary>两参重载。</summary>
        public void Overloaded(CollectionChangeInfo info, object value)
        {
        }

        /// <summary>两种形状都不是。</summary>
        public void Bad(int value)
        {
        }
    }
}
