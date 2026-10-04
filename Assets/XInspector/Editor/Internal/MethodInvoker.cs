using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// 调用目标对象上的方法，并处理 Undo 与异常。
    /// <para>
    /// 抽成独立的一类而不是写在绘制器里：绘制器只该做「画 + 调下一个」，
    /// 而「调谁、调几次、出错怎么办」是可无头测试的纯逻辑——本仓不测 IMGUI，
    /// 这类决策就必须离绘制远一点。
    /// </para>
    /// </summary>
    internal static class MethodInvoker
    {
        #region Public API

        /// <summary>
        /// 调用方法：实例方法对**每个目标各一次**，静态方法只调一次。
        /// </summary>
        /// <param name="methods">逐目标解析出的方法，与 <paramref name="targets"/> 一一对应。</param>
        /// <param name="targets">目标对象；单检视时只有一个。</param>
        /// <param name="arguments">实参；无参方法传 <c>null</c> 或空数组。</param>
        /// <param name="undoEnabled">是否记入 Undo（窗口路径为 <c>false</c>）。</param>
        /// <param name="undoLabel">撤销栈里显示的这一步的名字。</param>
        /// <remarks>
        /// <para>
        /// <b>方法按目标逐个给</b>，而不是一个方法配一串目标：覆写链上不同目标的实际方法可能不是同一个
        /// <see cref="MethodInfo"/>，多选时更是如此。某个目标解析不到（对应项为 <c>null</c>）就跳过它。
        /// </para>
        /// <para>
        /// <b>异常一律吞掉并打进 Console。</b> 按钮是在 <c>OnGUI</c> 里被点的，
        /// 让用户的异常冒到那里会打断整个 Inspector 的绘制（半张界面画不出来）。
        /// 逐个目标各包一层，于是一个目标抛异常不会挡住其余目标。
        /// </para>
        /// <para>
        /// <b>静态方法只调一次、也不记 Undo。</b> 它不作用于某个具体对象，
        /// 无从知道该把哪些对象记进撤销栈——猜一个比不记更糟。
        /// </para>
        /// </remarks>
        public static void Invoke(
            MethodInfo[] methods,
            object[] targets,
            object[] arguments,
            bool undoEnabled,
            string undoLabel)
        {
            if (methods == null || methods.Length == 0 || methods[0] == null)
            {
                return;
            }

            if (methods[0].IsStatic)
            {
                InvokeOn(methods[0], null, arguments);
                return;
            }

            if (targets == null || targets.Length == 0)
            {
                return;
            }

            // 只有 Unity 目标数组才可能、才应该记 Undo。
            //
            // 目标列表是 object[]（反射树的目标可以是 POCO），而序列化路径传进来的
            // 仍然是运行时的 Object[]——这个判断零分配，也不必为 POCO 造一套假的撤销语义。
            // 顺带解决了「POCO 树也能画出按钮」这件事：按钮照常可点，只是不记撤销。
            if (undoEnabled && targets is Object[] unityTargets && unityTargets.Length > 0)
            {
                // 一次传整个数组：多选下点击一次只该产生**一步**撤销。
                try
                {
                    Undo.RecordObjects(unityTargets, undoLabel);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            var count = Math.Min(targets.Length, methods.Length);

            for (var i = 0; i < count; i++)
            {
                if (TargetObjects.IsAlive(targets[i]) && methods[i] != null)
                {
                    InvokeOn(methods[i], targets[i], arguments);
                }
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>调一次，异常转成日志。</summary>
        /// <param name="method">方法。</param>
        /// <param name="target">目标对象；静态方法为 <c>null</c>。</param>
        /// <param name="arguments">实参。</param>
        private static void InvokeOn(MethodInfo method, object target, object[] arguments)
        {
            try
            {
                method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                // 反射会把用户代码的异常包一层，直接打外层只会看到「Exception has been thrown by
                // the target of an invocation」，等于什么都没说。
                Debug.LogException(exception.InnerException ?? exception);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        #endregion
    }
}
