using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 一棵树的全部生命周期钩子。
    /// <para>
    /// 形状是「按方法分组、组内逐目标」：<c>Hooks[方法序号][目标序号]</c>。
    /// 与按钮同一套理由——覆写链上不同目标实际要调的可能不是同一个 <see cref="MethodInfo"/>，
    /// 多选时更是如此。
    /// </para>
    /// </summary>
    internal sealed class TreeLifecycleHooks
    {
        #region Public Fields

        /// <summary>建树时调用的方法。为 <c>null</c> 表示没有。</summary>
        public MethodInfo[][] Init;

        /// <summary>释放时调用的方法。</summary>
        public MethodInfo[][] Dispose;

        /// <summary>每趟 GUI 布局调用的方法。</summary>
        public MethodInfo[][] Update;

        #endregion

        #region Public API

        /// <summary>有没有任何钩子——没有的话调用方可以整个跳过。</summary>
        /// <returns>有钩子返回 <c>true</c>。</returns>
        public bool HasAny => Init != null || Dispose != null || Update != null;

        #endregion
    }

    /// <summary>
    /// 生命周期钩子的收集与调用。
    /// <para>
    /// 三个特性（<see cref="OnInspectorInitAttribute"/>、<see cref="OnInspectorDisposeAttribute"/>、
    /// <see cref="OnStateUpdateAttribute"/>）都标在**方法**上，但都**不产生属性树节点**
    /// ——它们不在某个位置上画东西，故走这条独立通道，而不是混进方法节点里
    /// （混进去的后果是每次绘制多出几行空白）。
    /// </para>
    /// </summary>
    internal static class TreeLifecycle
    {
        #region Private Fields

        /// <summary>逐层查找用的标志。</summary>
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        /// <summary>按元数据令牌比较，把声明顺序稳定下来。</summary>
        private static readonly Comparison<MethodInfo> CompareByToken =
            (left, right) => left.MetadataToken.CompareTo(right.MetadataToken);

        #endregion

        #region Public API

        /// <summary>
        /// 收集三类钩子。
        /// </summary>
        /// <param name="targetType">目标对象的运行时类型。</param>
        /// <param name="targets">目标对象列表。</param>
        /// <returns>收集结果；一个都没有时各字段为 <c>null</c>。</returns>
        public static TreeLifecycleHooks Collect(Type targetType, object[] targets)
        {
            var hooks = new TreeLifecycleHooks
            {
                Init = CollectOne(targetType, targets, typeof(OnInspectorInitAttribute)),
                Dispose = CollectOne(targetType, targets, typeof(OnInspectorDisposeAttribute)),
                Update = CollectOne(targetType, targets, typeof(OnStateUpdateAttribute)),
            };

            return hooks.HasAny ? hooks : null;
        }

        /// <summary>
        /// 这一趟 GUI 该不该跑 <c>[OnStateUpdate]</c>。
        /// </summary>
        /// <param name="eventType">当前事件类型。</param>
        /// <returns>该跑返回 <c>true</c>。</returns>
        /// <remarks>
        /// 取 <c>Layout</c> 趟：IMGUI 每帧先布局再重绘，两趟都 <c>Draw</c> 一次，
        /// 只认 Layout 正好每帧一次。抽成纯函数是为了能被无头测到——
        /// 测试里没有 GUI 上下文，整条路径跑不起来。
        /// </remarks>
        public static bool ShouldRunStateUpdate(EventType eventType)
        {
            return eventType == EventType.Layout;
        }

        /// <summary>
        /// 依次调用一组钩子。
        /// </summary>
        /// <param name="hooks">收集结果，可为 <c>null</c>。</param>
        /// <param name="targets">目标对象列表。</param>
        /// <param name="undoLabel">撤销栈里显示的名字（生命周期钩子不记 Undo，仅作日志用）。</param>
        /// <remarks>
        /// 不记 Undo：生命周期钩子是「没人再看这个对象了」这类通知，不是用户的编辑。
        /// </remarks>
        public static void InvokeAll(MethodInfo[][] hooks, object[] targets, string undoLabel)
        {
            if (hooks == null)
            {
                return;
            }

            for (var i = 0; i < hooks.Length; i++)
            {
                MethodInvoker.Invoke(hooks[i], targets, null, false, undoLabel);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>收集某一类钩子，并逐目标解析出可调用的那一份。</summary>
        /// <param name="targetType">目标对象的运行时类型。</param>
        /// <param name="targets">目标对象列表。</param>
        /// <param name="attributeType">要查的特性类型。</param>
        /// <returns>逐方法、逐目标的方法表；没有则为 <c>null</c>。</returns>
        private static MethodInfo[][] CollectOne(Type targetType, object[] targets, Type attributeType)
        {
            var discovered = new List<MethodInfo>();

            for (var type = targetType; type != null; type = type.BaseType)
            {
                var declared = type.GetMethods(Flags);
                Array.Sort(declared, CompareByToken);

                for (var i = 0; i < declared.Length; i++)
                {
                    var method = declared[i];
                    if (method.GetCustomAttributes(attributeType, false).Length == 0)
                    {
                        continue;
                    }

                    if (method.GetParameters().Length > 0 || method.ContainsGenericParameters)
                    {
                        Debug.LogWarning(
                            $"[XInspector] 方法「{method.Name}」上的 [{attributeType.Name.Replace("Attribute", string.Empty)}] "
                            + "标在了一个带参数（或泛型）的方法上，本包调不动它——已跳过。");
                        continue;
                    }

                    discovered.Add(method);
                }
            }

            if (discovered.Count == 0 || targets == null || targets.Length == 0)
            {
                return null;
            }

            var hooks = new MethodInfo[discovered.Count][];

            for (var i = 0; i < discovered.Count; i++)
            {
                var perTarget = new MethodInfo[targets.Length];

                for (var t = 0; t < targets.Length; t++)
                {
                    perTarget[t] = TargetObjects.IsAlive(targets[t])
                        ? MethodResolver.BySignature(targets[t].GetType(), discovered[i])
                        : null;
                }

                hooks[i] = perTarget;
            }

            return hooks;
        }

        #endregion
    }
}
