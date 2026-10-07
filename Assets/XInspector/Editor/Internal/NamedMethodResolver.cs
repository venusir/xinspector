using System;
using System.Reflection;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 「特性里写着一个方法名」这一族（<c>[InlineButton]</c>、<c>[OnValueChanged]</c>、
    /// <c>[CustomContextMenu]</c>）的共用解析：按名在**每个目标**上找出可调用的那一份。
    /// <para>
    /// 三者的差别只在「拿解析结果去做什么」，解析规则本身一模一样：无参、非泛型、
    /// 目标对象上没有就告警而不是静默。抽在这里，免得三份实现慢慢长歪。
    /// </para>
    /// </summary>
    internal static class NamedMethodResolver
    {
        #region Public API

        /// <summary>
        /// 逐目标解析一个无参方法。
        /// </summary>
        /// <param name="property">挂着该特性的属性。</param>
        /// <param name="methodName">方法名。</param>
        /// <param name="usage">用于告警文案的用法名，如 <c>[InlineButton]</c>。</param>
        /// <param name="scopes">
        /// 逐目标的嵌套实例来源；顶层为 <c>null</c>。嵌套层的方法在**实例的类型**上找，
        /// 调用时也在那个实例上（见 <see cref="NestedInstanceScope"/>）。
        /// </param>
        /// <param name="reason">失败原因；全部目标都解析成功时为 <c>null</c>。</param>
        /// <returns>逐目标的方法表；一个都解析不到时返回 <c>null</c>。</returns>
        /// <remarks>
        /// 部分目标解析不到时**不整体失败**：能调的照调，调不了的跳过，但留一条告警。
        /// <para>
        /// 这是**形状收窄成「无参」的特例**——要收多种参数表用另一个重载。
        /// </para>
        /// </remarks>
        public static MethodInfo[] Resolve(
            InspectorProperty property,
            string methodName,
            string usage,
            out ReflectedAccessor[] scopes,
            out string reason)
        {
            return ResolveCore(property, methodName, usage, null, out scopes, out reason);
        }

        /// <summary>
        /// 逐目标解析一个方法，**参数表落在给定形状表里**即可。
        /// </summary>
        /// <param name="property">挂着该特性的属性。</param>
        /// <param name="methodName">方法名。</param>
        /// <param name="usage">用于告警文案的用法名，如 <c>[OnCollectionChanged]</c>。</param>
        /// <param name="shapes">可接受的参数表，按顺序试；其中空数组表示「无参」。</param>
        /// <param name="scopes">逐目标的嵌套实例来源；顶层为 <c>null</c>。</param>
        /// <param name="reason">失败原因；全部目标都解析成功时为 <c>null</c>。</param>
        /// <returns>逐目标的方法表；一个都解析不到时返回 <c>null</c>。</returns>
        /// <remarks>
        /// <b>逐目标的形状必须一致。</b> 调用侧只有**一份**实参数组
        /// （见 <see cref="MethodInvoker.Invoke(MethodInfo[], object[], object[], bool, string)"/>），
        /// 形状不同的那个目标会抛 <c>TargetParameterCountException</c> 并被吞掉——
        /// 表现为「这个目标的回调静默地没跑」。故以**第一个解析成功的目标**的形状为准，
        /// 形状不同的目标按「解析不到」处理（跳过 + 告警）。
        /// </remarks>
        public static MethodInfo[] Resolve(
            InspectorProperty property,
            string methodName,
            string usage,
            Type[][] shapes,
            out ReflectedAccessor[] scopes,
            out string reason)
        {
            return ResolveCore(property, methodName, usage, shapes, out scopes, out reason);
        }

        /// <summary>找一条**第一个**目标能解析成功的路径：先按实例类型，再按形状表。</summary>
        /// <param name="property">挂着该特性的属性。</param>
        /// <param name="methodName">方法名。</param>
        /// <param name="usage">用于告警文案的用法名。</param>
        /// <param name="shapes">形状表；<c>null</c> 表示旧口径（只收无参、沿用旧失败文案）。</param>
        /// <param name="scopes">逐目标的嵌套实例来源。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>逐目标的方法表。</returns>
        private static MethodInfo[] ResolveCore(
            InspectorProperty property,
            string methodName,
            string usage,
            Type[][] shapes,
            out ReflectedAccessor[] scopes,
            out string reason)
        {
            reason = null;
            scopes = null;

            var targets = property.Owner?.Targets;

            if (targets == null || targets.Length == 0)
            {
                reason = "取不到目标对象，无法调用方法。";
                return null;
            }

            // 嵌套层（或元素层）：方法要在**同一个实例**的类型上找，调用时也要在那个实例上——
            // 在根对象上按名找，根上恰好有同名方法就会被静默调走。
            // 值类型容器上的调用一律拒绝：判据与文案收在 NestedInstanceScope 一处
            // （与其让用户「点了没反应」，不如明说并让调用方把条目画成不可用）。
            reason = NestedInstanceScope.ValueTypeContainerReason(property);

            if (reason != null)
            {
                Debug.LogWarning($"[XInspector] 属性「{property.Path}」上的 {usage} 未生效：{reason}");
                return null;
            }

            var container = NestedInstanceScope.ContainerOf(property);
            if (container != null)
            {
                scopes = NestedInstanceScope.Compile(targets, container);
            }

            var methods = new MethodInfo[targets.Length];
            var missing = 0;
            Type[] accepted = null;

            for (var i = 0; i < targets.Length; i++)
            {
                if (!TargetObjects.IsAlive(targets[i]))
                {
                    missing++;
                    continue;
                }

                // 嵌套层按**实例的类型**找；某个目标算不出实例类型就算它找不到。
                // （末段是多态引用时 `ValueType` 只是声明类型——`InstanceTypeOf` 现读实例。）
                var type = scopes == null
                    ? targets[i].GetType()
                    : (i < scopes.Length ? NestedInstanceScope.InstanceTypeOf(scopes[i], targets[i]) : null);

                if (type == null)
                {
                    missing++;
                    reason = reason ?? "取不到嵌套实例（父字段为空或路径解析不到）";
                    continue;
                }

                string failure;
                var resolved = shapes == null
                    ? MethodResolver.ByName(type, methodName, out failure)
                    : MethodResolver.ByName(type, methodName, shapes, out failure);

                if (resolved == null)
                {
                    missing++;
                    reason = reason ?? failure;
                    continue;
                }

                if (accepted == null)
                {
                    accepted = ParameterTypesOf(resolved);
                }
                else if (!HasShape(resolved, accepted))
                {
                    // 形状不一致：这个目标调不了（实参数组只有一份），跳过并说明。
                    missing++;
                    reason = reason
                        ?? $"各目标上解析到的「{methodName}」形状不一致（有的收参数、有的不收），"
                           + "本包只按同一种形状调用。";
                    continue;
                }

                methods[i] = resolved;
            }

            if (missing == targets.Length)
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上的 {usage} 无法调用「{methodName}」：{reason}");
                return null;
            }

            if (missing > 0)
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上的 {usage}「{methodName}」"
                    + $"在 {missing} 个目标上找不到，这些目标将被跳过。");
            }

            return methods;
        }

        /// <summary>取一个方法的参数表。</summary>
        /// <param name="method">方法。</param>
        /// <returns>参数类型，按声明顺序。</returns>
        private static Type[] ParameterTypesOf(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var types = new Type[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                types[i] = parameters[i].ParameterType;
            }

            return types;
        }

        /// <summary>这个方法的参数表是不是给定的那一份。</summary>
        /// <param name="method">方法。</param>
        /// <param name="shape">期望的参数表。</param>
        /// <returns>一致返回 <c>true</c>。</returns>
        private static bool HasShape(MethodInfo method, Type[] shape)
        {
            var parameters = method.GetParameters();

            if (parameters.Length != shape.Length)
            {
                return false;
            }

            for (var i = 0; i < shape.Length; i++)
            {
                if (parameters[i].ParameterType != shape[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 找一个特性实例在节点的特性列表里的**次序**（只数同类型的）。
        /// </summary>
        /// <typeparam name="TAttribute">特性类型。</typeparam>
        /// <param name="property">目标属性。</param>
        /// <param name="attribute">要找的实例。</param>
        /// <returns>次序；不在列表里时返回 <c>-1</c>。</returns>
        /// <remarks>
        /// 用引用相等而不是 <c>Equals</c>：同一个字段上挂两个写法完全相同的特性是合法的，
        /// 按值比较会把它们当成同一个（<see cref="Attribute"/> 重写了 <c>Equals</c>）。
        /// </remarks>
        public static int IndexOf<TAttribute>(InspectorProperty property, TAttribute attribute)
            where TAttribute : Attribute
        {
            var attributes = property.Attributes.Raw;
            var index = 0;

            for (var i = 0; i < attributes.Count; i++)
            {
                if (!(attributes[i] is TAttribute candidate))
                {
                    continue;
                }

                if (ReferenceEquals(candidate, attribute))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        /// <summary>数一数列表里有几个指定类型的特性。</summary>
        /// <typeparam name="TAttribute">特性类型。</typeparam>
        /// <param name="attributes">特性列表。</param>
        /// <returns>个数。</returns>
        public static int CountOf<TAttribute>(System.Collections.Generic.IList<Attribute> attributes)
            where TAttribute : Attribute
        {
            var count = 0;

            for (var i = 0; i < attributes.Count; i++)
            {
                if (attributes[i] is TAttribute)
                {
                    count++;
                }
            }

            return count;
        }

        #endregion
    }
}
