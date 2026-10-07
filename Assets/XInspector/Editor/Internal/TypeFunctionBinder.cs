using System;
using System.Reflection;

namespace XInspector.Editor
{
    /// <summary>
    /// 把「单参 <c>Type</c> 的方法名」绑成委托——<c>[TypeSelectorSettings].FilterTypesFunction</c>
    /// 与 <c>[PolymorphicDrawerSettings].CreateInstanceFunction</c> 共用这一条通道。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>单目标编排，刻意不复用 <see cref="NamedMethodResolver"/>。</b> 那条的契约是
    /// 「逐目标 + 部分失败**跳过**」，与本消费者的「一个目标说了算、整体成败」三处错配：
    /// ① 部分失败时它会打「这些目标将被跳过」——我们一个都没跳过，那句话在控制台里是假话；
    /// ② 它的失败文案没有「过滤器已忽略（≠ 特性失效）」那半句，而那正是本包文案的硬要求；
    /// ③「取不到目标对象」那一支它**不告警**（直接静默返回）。判据本身仍只有一份——
    /// 容器四件（<see cref="NestedInstanceScope"/>）与「按名 + 形状找方法」（<see cref="MethodResolver"/>）
    /// 全部复用，重叠的只是十几行编排。
    /// </para>
    /// <para>
    /// <b>实例每次现读</b>（闭包捕获访问器而不是实例）：父字段被重新赋值之后要跟着走——
    /// 与条件族、按名回调同一条纪律（绑死的实例会**静默陈旧**）。
    /// </para>
    /// <para>
    /// <b>目标取「第一个存活且解析得到方法」的那个</b>（「只看第一个存活目标」那条单值纪律的推广）：
    /// 跳过已销毁的、跳过解析不到的；一个都没有时用第一个失败原因收场。
    /// </para>
    /// </remarks>
    internal static class TypeFunctionBinder
    {
        #region Private Fields

        /// <summary>单参 <c>Type</c> 的形状表（交给 <c>MethodResolver</c> 挑方法）。</summary>
        private static readonly Type[][] SingleTypeParameter = { new[] { typeof(Type) } };

        #endregion

        #region Public API

        /// <summary>
        /// 绑定候选过滤器（<c>bool f(Type)</c>）。
        /// </summary>
        /// <param name="property">带特性的节点。</param>
        /// <param name="methodName">方法名。</param>
        /// <param name="filter">绑出的过滤器；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因（中文）；成功时为 <c>null</c>。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>实例取不到时过滤器给 <c>true</c>——**给不出意见就不收窄**。</remarks>
        public static bool TryBindFilter(
            InspectorProperty property, string methodName, out Func<Type, bool> filter, out string reason)
        {
            filter = null;

            if (!TryBindCore(
                    property, methodName,
                    out Func<object, Type, bool> invoker,
                    out var scopes, out var targets, out var index, out reason))
            {
                return false;
            }

            filter = type =>
            {
                var instance = NestedInstanceScope.Read(scopes, targets, index);
                return instance == null || invoker(instance, type);
            };

            return true;
        }

        /// <summary>
        /// 绑定自定义造实例（<c>object f(Type)</c>）。
        /// </summary>
        /// <param name="property">带特性的节点。</param>
        /// <param name="methodName">方法名。</param>
        /// <param name="factory">绑出的工厂；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因（中文）；成功时为 <c>null</c>。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>实例取不到时给 <c>null</c>——写回侧把它转成原因（不静默）。</remarks>
        public static bool TryBindFactory(
            InspectorProperty property, string methodName, out Func<Type, object> factory, out string reason)
        {
            factory = null;

            if (!TryBindCore(
                    property, methodName,
                    out Func<object, Type, object> invoker,
                    out var scopes, out var targets, out var index, out reason))
            {
                return false;
            }

            factory = type =>
            {
                var instance = NestedInstanceScope.Read(scopes, targets, index);
                return instance == null ? null : invoker(instance, type);
            };

            return true;
        }

        #endregion

        #region Private Helpers

        /// <summary>共用的单目标编排：容器 → 第一个解析得到方法的目标 → 编译调用器。</summary>
        /// <typeparam name="TResult">方法返回的类型。</typeparam>
        /// <param name="property">带特性的节点。</param>
        /// <param name="methodName">方法名。</param>
        /// <param name="invoker">编译出的调用器；失败时为 <c>null</c>。</param>
        /// <param name="scopes">逐目标的容器实例访问器；顶层为 <c>null</c>。</param>
        /// <param name="targets">根目标。</param>
        /// <param name="index">说了算的那个目标下标。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryBindCore<TResult>(
            InspectorProperty property,
            string methodName,
            out Func<object, Type, TResult> invoker,
            out ReflectedAccessor[] scopes,
            out object[] targets,
            out int index,
            out string reason)
        {
            invoker = null;
            scopes = null;
            index = -1;
            reason = null;

            targets = property?.Owner?.Targets;

            if (targets == null || targets.Length == 0)
            {
                reason = "取不到目标对象，无法调用方法。";
                return false;
            }

            // 值类型容器上的调用一律拒绝：判据与文案收在 NestedInstanceScope 一处。
            reason = NestedInstanceScope.ValueTypeContainerReason(property);

            if (reason != null)
            {
                return false;
            }

            var container = NestedInstanceScope.ContainerOf(property);
            if (container != null)
            {
                scopes = NestedInstanceScope.Compile(targets, container);
            }

            for (var i = 0; i < targets.Length; i++)
            {
                if (!TargetObjects.IsAlive(targets[i]))
                {
                    continue;
                }

                var type = scopes == null
                    ? targets[i].GetType()
                    : (i < scopes.Length ? NestedInstanceScope.InstanceTypeOf(scopes[i], targets[i]) : null);

                if (type == null)
                {
                    reason = reason ?? "取不到嵌套实例（父字段为空或路径解析不到）";
                    continue;
                }

                var method = MethodResolver.ByName(type, methodName, SingleTypeParameter, out var failure);

                if (method == null)
                {
                    reason = reason ?? failure;
                    continue;
                }

                if (!ReflectedAccessor.TryCreateInvoker<Type, TResult>(method, out invoker, out reason))
                {
                    return false;
                }

                index = i;
                return true;
            }

            reason = reason ?? $"目标对象上找不到名为「{methodName}」、单参 Type 的方法。";
            return false;
        }

        #endregion
    }
}
