using System;
using System.Reflection;
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
    /// 方法调用：调几次、调谁、出错怎么办、记不记 Undo。
    /// <para>
    /// 这一层刻意与绘制器分开，正是为了能这样测——按钮的「点了之后发生什么」
    /// 全部落在这里，而绘制器里只剩「画一个按钮、点了就调它」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class MethodInvokerTests
    {
        #region Private Fields

        private InvocationFixture _first;
        private InvocationFixture _second;

        #endregion

        #region Setup / Teardown

        /// <summary>建立两个测试资产与静态计数器。</summary>
        [SetUp]
        public void SetUp()
        {
            _first = ScriptableObject.CreateInstance<InvocationFixture>();
            _second = ScriptableObject.CreateInstance<InvocationFixture>();
            InvocationFixture.StaticCalls = 0;
        }

        /// <summary>销毁资产。</summary>
        [TearDown]
        public void TearDown()
        {
            Destroy(ref _first);
            Destroy(ref _second);
            InvocationFixture.StaticCalls = 0;
        }

        #endregion

        #region 调用次数

        /// <summary>实例方法对**每个目标各调一次**——多选下这就是用户期望的语义。</summary>
        [Test]
        public void 实例方法对每个目标各调一次()
        {
            var method = Method(nameof(InvocationFixture.Bump));

            MethodInvoker.Invoke(new[] { method, method }, new Object[] { _first, _second }, null, false, "测试");

            Assert.That(_first.calls, Is.EqualTo(1));
            Assert.That(_second.calls, Is.EqualTo(1));
        }

        /// <summary>静态方法只调一次——它不作用于某个具体对象。</summary>
        [Test]
        public void 静态方法只调一次()
        {
            var method = Method(nameof(InvocationFixture.BumpStatic));

            MethodInvoker.Invoke(
                new[] { method, method },
                new Object[] { _first, _second },
                null,
                false,
                "测试");

            Assert.That(InvocationFixture.StaticCalls, Is.EqualTo(1));
        }

        /// <summary>参数被原样传给方法。</summary>
        [Test]
        public void 实参被传给方法()
        {
            var method = Method(nameof(InvocationFixture.Add));

            MethodInvoker.Invoke(new[] { method }, new Object[] { _first }, new object[] { 7 }, false, "测试");

            Assert.That(_first.total, Is.EqualTo(7));
        }

        /// <summary>某个目标解析不到方法（对应项为 <c>null</c>）时跳过它，其余照调。</summary>
        [Test]
        public void 解析不到的目标被跳过()
        {
            var method = Method(nameof(InvocationFixture.Bump));

            MethodInvoker.Invoke(new[] { method, null }, new Object[] { _first, _second }, null, false, "测试");

            Assert.That(_first.calls, Is.EqualTo(1));
            Assert.That(_second.calls, Is.EqualTo(0));
        }

        /// <summary>目标被销毁（引用为 <c>null</c>）时跳过，不抛异常。</summary>
        [Test]
        public void 空目标被跳过()
        {
            var method = Method(nameof(InvocationFixture.Bump));

            Assert.DoesNotThrow(() => MethodInvoker.Invoke(
                new[] { method, method },
                new Object[] { _first, null },
                null,
                false,
                "测试"));

            Assert.That(_first.calls, Is.EqualTo(1));
        }

        #endregion

        #region 异常

        /// <summary>
        /// 方法抛异常时**不外传**，而是记一条日志。
        /// <para>
        /// 按钮是在 <c>OnGUI</c> 里被点的，异常冒到那里会打断整个 Inspector 的绘制。
        /// 且反射会把它包一层，直接打外层等于什么都没说——这里同时断言**原始异常**被报出来。
        /// </para>
        /// </summary>
        [Test]
        public void 方法异常被吞掉并记录()
        {
            LogAssert.Expect(LogType.Exception, new Regex("按钮里炸了"));

            var method = Method(nameof(InvocationFixture.Throw));

            Assert.DoesNotThrow(() =>
                MethodInvoker.Invoke(new[] { method }, new Object[] { _first }, null, false, "测试"));
        }

        /// <summary>前一个目标抛异常不影响后一个目标——逐个目标各包一层。</summary>
        [Test]
        public void 一个目标抛异常不影响其余目标()
        {
            LogAssert.Expect(LogType.Exception, new Regex("按钮里炸了"));

            _first.ThrowOnCall = true;
            var method = Method(nameof(InvocationFixture.ThrowOrBump));

            MethodInvoker.Invoke(
                new[] { method, method },
                new Object[] { _first, _second },
                null,
                false,
                "测试");

            Assert.That(_first.calls, Is.EqualTo(0), "第一个目标照着夹具的设定抛了异常。");
            Assert.That(_second.calls, Is.EqualTo(1), "第二个目标不该被连累。");
        }

        #endregion

        #region Undo

        /// <summary>记 Undo 时，点一次按钮产生的改动可以撤销回去。</summary>
        [Test]
        public void 记Undo时可撤销()
        {
            var method = Method(nameof(InvocationFixture.Bump));

            MethodInvoker.Invoke(new[] { method }, new Object[] { _first }, null, true, "点按钮");

            Assert.That(_first.calls, Is.EqualTo(1));

            Undo.PerformUndo();

            Assert.That(_first.calls, Is.EqualTo(0), "撤销该把方法造成的改动收回去。");
        }

        /// <summary>
        /// 窗口路径（<c>undoEnabled = false</c>）的改动**撤销收不回去**。
        /// <para>
        /// <b>判据不是 <c>Undo.GetCurrentGroupName()</c>。</b> 那条路看着对，其实恒真：
        /// 组名只在显式 <c>SetCurrentGroupName</c> 之后才有，<c>RecordObjects</c> 不会给它命名，
        /// 于是「没记时组名不等于我们给的名字」在任何情况下都成立——一个什么都测不到的断言。
        /// 这里改成可观测的判据：**先记一步已知可撤销的**（撤销栈因此非空、行为确定），
        /// 再来一步不记的，然后撤一次，看收回的是哪一步。
        /// </para>
        /// </summary>
        [Test]
        public void 不记Undo时的改动撤销收不回去()
        {
            var method = Method(nameof(InvocationFixture.Bump));

            MethodInvoker.Invoke(new[] { method }, new Object[] { _first }, null, true, "记一步");
            MethodInvoker.Invoke(new[] { method }, new Object[] { _first }, null, false, "点按钮");

            Assert.That(_first.calls, Is.EqualTo(2));

            Undo.PerformUndo();

            // 收回的是**第一步之前**的状态。若第二步也被记了，撤销会停在 1。
            Assert.That(_first.calls, Is.EqualTo(0), "收回的该是记过 Undo 的那一步，而不是后一步。");
        }

        /// <summary>
        /// 目标是普通对象、且开了 Undo 时，方法照调、也不抛。
        /// <para>
        /// 反射树上的按钮走的就是这条路径。<c>Undo.RecordObjects</c> 只认
        /// <see cref="UnityEngine.Object"/>，对 POCO 无从下手（类型系统就挡着，
        /// 那正是「<c>is Object[]</c> 判断」想表达的语义）；而按钮本身该照常可点——
        /// 否则「POCO 窗口里有按钮却点不动」又是一个新的静默面。
        /// </para>
        /// </summary>
        [Test]
        public void 反射目标上的方法照常可调()
        {
            var poco = new PocoInvocationFixture();
            var method = typeof(PocoInvocationFixture).GetMethod(nameof(PocoInvocationFixture.Bump));

            Assert.That(
                () => MethodInvoker.Invoke(new[] { method }, new object[] { poco }, null, true, "点按钮"),
                Throws.Nothing);

            Assert.That(poco.calls, Is.EqualTo(1));
        }

        #endregion

        #region Private Helpers

        /// <summary>按名取夹具上的方法。</summary>
        /// <param name="name">方法名。</param>
        /// <returns>方法。</returns>
        private static MethodInfo Method(string name)
        {
            return typeof(InvocationFixture).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
        }

        /// <summary>销毁资产并把引用清空。</summary>
        /// <param name="target">资产引用。</param>
        private static void Destroy(ref InvocationFixture target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
                target = null;
            }
        }

        #endregion
    }

    /// <summary>反射树那种普通对象目标——刻意不是 Unity 对象。</summary>
    internal sealed class PocoInvocationFixture
    {
        /// <summary>调用计数。</summary>
        public int calls;

        /// <summary>数一次。</summary>
        public void Bump()
        {
            calls++;
        }
    }

    /// <summary>调用测试用的资产：每个方法做一件可观测的小事。</summary>
    internal sealed class InvocationFixture : ScriptableObject
    {
        /// <summary>静态调用计数。</summary>
        public static int StaticCalls;

        /// <summary>实例调用计数。</summary>
        public int calls;

        /// <summary>累加结果。</summary>
        public int total;

        /// <summary>置真时 <see cref="ThrowOrBump"/> 会抛异常。</summary>
        public bool ThrowOnCall;

        /// <summary>数一次。</summary>
        public void Bump()
        {
            calls++;
        }

        /// <summary>数一次（静态）。</summary>
        public static void BumpStatic()
        {
            StaticCalls++;
        }

        /// <summary>累加。</summary>
        /// <param name="amount">加多少。</param>
        public void Add(int amount)
        {
            total += amount;
        }

        /// <summary>总是抛异常。</summary>
        public void Throw()
        {
            throw new InvalidOperationException("按钮里炸了");
        }

        /// <summary>按 <see cref="ThrowOnCall"/> 决定抛异常还是计数——用来验证逐个目标的隔离。</summary>
        public void ThrowOrBump()
        {
            if (ThrowOnCall)
            {
                throw new InvalidOperationException("按钮里炸了");
            }

            calls++;
        }
    }
}
