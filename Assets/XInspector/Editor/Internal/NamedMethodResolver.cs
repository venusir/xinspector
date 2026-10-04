using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

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
        /// <param name="reason">失败原因；全部目标都解析成功时为 <c>null</c>。</param>
        /// <returns>逐目标的方法表；一个都解析不到时返回 <c>null</c>。</returns>
        /// <remarks>
        /// 部分目标解析不到时**不整体失败**：能调的照调，调不了的跳过，但留一条告警。
        /// </remarks>
        public static MethodInfo[] Resolve(
            InspectorProperty property,
            string methodName,
            string usage,
            out string reason)
        {
            reason = null;

            var targets = property.Owner?.Targets;

            if (targets == null || targets.Length == 0)
            {
                reason = "取不到目标对象，无法调用方法。";
                return null;
            }

            var methods = new MethodInfo[targets.Length];
            var missing = 0;

            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    missing++;
                    continue;
                }

                methods[i] = MethodResolver.ByName(targets[i].GetType(), methodName, out var failure);

                if (methods[i] == null)
                {
                    missing++;
                    reason = reason ?? failure;
                }
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
