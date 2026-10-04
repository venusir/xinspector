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
        /// 不记 Undo 时（窗口路径）撤销栈里没有这一步。
        /// <para>
        /// 判据用**当前撤销组的名字**：记了就会是我们给的那个名字，
        /// 没记就还是上一步的名字。比「组号有没有变」可靠——<c>RecordObjects</c> 是往
        /// 当前组里追加记录，组号本来就可能不变。
        /// </para>
        /// </summary>
        [Test]
        public void 不记Undo时撤销栈无此步()
        {
            var method = Method(nameof(InvocationFixture.Bump));

            MethodInvoker.Invoke(new[] { method }, new Object[] { _first }, null, false, "点按钮");

            Assert.That(_first.calls, Is.EqualTo(1));
            Assert.That(Undo.GetCurrentGroupName(), Is.Not.EqualTo("点按钮"));
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
