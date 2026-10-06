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
    /// 条件族指向普通属性与无参 bool 方法——L3 之后条件不再必须是序列化成员。
    /// <para>
    /// 断的是 <c>IsVisible</c> / <c>IsReadOnly</c> 这类**决策**：它们每帧由求值器算出来，
    /// 不碰 GUI，正是本仓测试策略依赖的那条分界。
    /// </para>
    /// <para>
    /// 失败的那几条一律**保持可见 + 告警**，故它们各自用自己的夹具——
    /// 否则建树时那条告警会污染同一个 fixture 里所有别的用例。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ReflectedConditionResolverTests
    {
        #region Teardown

        /// <summary>复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 成功路径

        /// <summary>条件指向一个普通属性：值一变，可见性跟着变——不必重建树。</summary>
        [Test]
        public void 条件指向普通属性()
        {
            var target = ScriptableObject.CreateInstance<ReflectedConditionFixture>();

            try
            {
                target.flag = true;

                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = ReflectedMemberTests.Find(tree.Root, "byProperty");
                    Assert.That(node, Is.Not.Null, "被打条件的成员应当进树。");

                    Assert.That(node.IsVisible, Is.True);

                    target.flag = false;

                    Assert.That(node.IsVisible, Is.False, "求值器每帧现读，改值之后不必重建树。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>条件指向一个无参返回 bool 的方法。</summary>
        [Test]
        public void 条件指向无参bool方法()
        {
            var target = ScriptableObject.CreateInstance<ReflectedConditionFixture>();

            try
            {
                target.flag = true;

                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = ReflectedMemberTests.Find(tree.Root, "byMethod");

                    Assert.That(node.IsVisible, Is.True);

                    target.flag = false;

                    Assert.That(node.IsVisible, Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 控制项：条件指向序列化成员照旧——三级解析的第一级没被后加的路径弄坏。
        /// </summary>
        [Test]
        public void 条件指向序列化成员照旧()
        {
            var target = ScriptableObject.CreateInstance<ReflectedConditionFixture>();

            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = ReflectedMemberTests.Find(tree.Root, "bySerialized");

                    Assert.That(node.IsVisible, Is.True);

                    // 序列化条件读的是**树那个 SerializedObject 的缓存副本**：
                    // 直接改目标对象的字段它看不见，得经同一个序列化对象改才作数。
                    tree.SerializedObject.FindProperty(nameof(ReflectedConditionFixture.serializedFlag)).boolValue = false;

                    Assert.That(node.IsVisible, Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>条件可以标在**反射成员**自己身上，同样生效。</summary>
        [Test]
        public void 条件标在反射成员上也生效()
        {
            var target = ScriptableObject.CreateInstance<ReflectedConditionFixture>();

            try
            {
                target.flag = true;

                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = ReflectedMemberTests.Find(tree.Root, "ReflectedWithCondition");
                    Assert.That(node, Is.Not.Null);
                    Assert.That(node.ValueEntry.IsUnityBacked, Is.False, "它确实是反射成员。");

                    Assert.That(node.IsVisible, Is.True);

                    target.flag = false;

                    Assert.That(node.IsVisible, Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>反射树（没有序列化对象）上的条件一样解析得出来——它直接走第二级。</summary>
        [Test]
        public void 反射树上的条件也能解析()
        {
            var poco = new ReflectedConditionPoco { flag = true };

            using (var tree = PropertyTree.CreateReflected(poco))
            {
                var node = ReflectedMemberTests.Find(tree.Root, "byProperty");

                Assert.That(node, Is.Not.Null);
                Assert.That(node.IsVisible, Is.True);

                poco.flag = false;

                Assert.That(node.IsVisible, Is.False);
            }
        }

        #endregion

        #region 失败路径：保持可见 + 告警

        /// <summary>找到的同名成员不是 bool 时**保持可见**并告警，而不是抛。</summary>
        [Test]
        public void 同名成员不是bool时保持可见()
        {
            // 告警文本里的成员名是**挂着条件的那一个**（value），不是条件指向的那一个。
            // 后半截的措辞由「找成员」那一层给（`找到的「NotABool」属性是 Int32，必须是 bool`）
            // ——它不再写「条件必须是」，因为同一层现在也服务 [Toggle] 一族与 [MinMaxSlider]。
            LogAssert.Expect(LogType.Warning, new Regex("「value」上的条件「NotABool」.*必须是 bool"));

            AssertDoesNotThrowAndStaysVisible<WrongConditionTypeFixture>();

            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        /// <summary>名字一个都找不到时保持可见并告警。</summary>
        [Test]
        public void 找不到条件成员时保持可见()
        {
            LogAssert.Expect(LogType.Warning, new Regex("找不到名为「nope」"));

            AssertDoesNotThrowAndStaysVisible<MissingConditionFixture>();

            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        /// <summary>条件方法带参数时保持可见并告警——「找到了但形状不对」是另一句话。</summary>
        [Test]
        public void 条件方法带参数时保持可见()
        {
            LogAssert.Expect(LogType.Warning, new Regex("NeedsArgument.*必须无参"));

            AssertDoesNotThrowAndStaysVisible<MethodWithParameterConditionFixture>();

            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        /// <summary>条件方法返回的不是 bool 时保持可见并告警。</summary>
        [Test]
        public void 条件方法返回非bool时保持可见()
        {
            LogAssert.Expect(LogType.Warning, new Regex("ReturnsInt.*必须返回 bool"));

            AssertDoesNotThrowAndStaysVisible<MethodReturningIntConditionFixture>();

            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region Private Helpers

        /// <summary>建一棵树，断言目标成员仍在且可见——解析失败的后果就该是这个。</summary>
        /// <typeparam name="T">夹具类型。</typeparam>
        private static void AssertDoesNotThrowAndStaysVisible<T>() where T : ScriptableObject
        {
            var target = ScriptableObject.CreateInstance<T>();

            try
            {
                Assert.DoesNotThrow(() =>
                {
                    using (var tree = PropertyTree.Create(new SerializedObject(target)))
                    {
                        var node = ReflectedMemberTests.Find(tree.Root, "value");

                        Assert.That(node, Is.Not.Null, "条件解析失败不该让成员消失。");
                        Assert.That(node.IsVisible, Is.True, "条件解析失败时按无条件处理。");
                    }
                });
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion
    }

    /// <summary>条件测试用的资产：三条成功路径各一个成员。</summary>
    internal sealed class ReflectedConditionFixture : ScriptableObject
    {
        /// <summary>条件取值来源。</summary>
        public bool flag = true;

        /// <summary>序列化成员，供第一级用。</summary>
        public bool serializedFlag = true;

        /// <summary>普通属性（非序列化），供第二级用。</summary>
        public bool PlainProperty => flag;

        /// <summary>无参返回 bool 的方法，供第三级用。</summary>
        public bool PlainMethod()
        {
            return flag;
        }

        /// <summary>条件指向普通属性。</summary>
        [ShowIf(nameof(PlainProperty))]
        public int byProperty = 1;

        /// <summary>条件指向无参 bool 方法。</summary>
        [ShowIf(nameof(PlainMethod))]
        public int byMethod = 2;

        /// <summary>条件指向序列化成员。</summary>
        [ShowIf(nameof(serializedFlag))]
        public int bySerialized = 3;

        /// <summary>带条件的反射成员本身。</summary>
        [ShowInInspector]
        [ShowIf(nameof(PlainProperty))]
        public int ReflectedWithCondition => byProperty;
    }

    /// <summary>条件指向的成员类型不对（<c>int</c> 属性）。</summary>
    internal sealed class WrongConditionTypeFixture : ScriptableObject
    {
        /// <summary>被打条件的成员。</summary>
        [ShowIf(nameof(NotABool))]
        public int value = 1;

        /// <summary>同名但类型不对。</summary>
        public int NotABool => 5;
    }

    /// <summary>条件名根本不存在。</summary>
    internal sealed class MissingConditionFixture : ScriptableObject
    {
        /// <summary>被打条件的成员。</summary>
        [ShowIf("nope")]
        public int value = 1;
    }

    /// <summary>条件方法带参数。</summary>
    internal sealed class MethodWithParameterConditionFixture : ScriptableObject
    {
        /// <summary>被打条件的成员。</summary>
        [ShowIf(nameof(NeedsArgument))]
        public int value = 1;

        /// <summary>带参数，不能当条件。</summary>
        /// <param name="ignored">参数。</param>
        /// <returns>恒真。</returns>
        public bool NeedsArgument(int ignored)
        {
            return true;
        }
    }

    /// <summary>条件方法返回的不是 bool。</summary>
    internal sealed class MethodReturningIntConditionFixture : ScriptableObject
    {
        /// <summary>被打条件的成员。</summary>
        [ShowIf(nameof(ReturnsInt))]
        public int value = 1;

        /// <summary>返回 int，不能当条件。</summary>
        /// <returns>恒为 1。</returns>
        public int ReturnsInt()
        {
            return 1;
        }
    }

    /// <summary>反射树用的普通对象——条件那条路在没有序列化对象时也要通。</summary>
    internal sealed class ReflectedConditionPoco
    {
        /// <summary>条件取值来源。</summary>
        public bool flag = true;

        /// <summary>普通属性。</summary>
        public bool PlainProperty => flag;

        /// <summary>
        /// 被打条件的成员。
        /// <para>
        /// 它自己也得带 <c>[ShowInInspector]</c>——反射树只收带标记的成员，
        /// 而条件指向谁与谁进树是两件事。
        /// </para>
        /// </summary>
        [ShowInInspector]
        [ShowIf(nameof(PlainProperty))]
        public int byProperty = 1;
    }
}
