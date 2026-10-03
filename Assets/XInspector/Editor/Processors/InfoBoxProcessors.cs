using System;
using System.Collections.Generic;

namespace XInspector.Editor
{
    /// <summary>
    /// 信息框的可见性条件：按**特性实例**存放。
    /// </summary>
    /// <remarks>
    /// 键必须是特性实例而不是特性类型：一个成员上可以挂多个 <c>[InfoBox]</c>
    /// （官方与本包都允许重复标注），各带各的 <c>visibleIf</c>。
    /// 用类型作键会让它们互相覆盖，症状是「两个框一个亮一个灭，且灭的是不该灭的那个」。
    /// </remarks>
    internal sealed class InfoBoxVisibilityState
    {
        /// <summary>特性实例 → 每帧求值的可见性条件。</summary>
        public readonly Dictionary<Attribute, Func<bool>> Conditions = new Dictionary<Attribute, Func<bool>>();
    }

    /// <summary>
    /// 信息框条件的解析（构建期）与求值（绘制期）。
    /// <para>
    /// 分工与条件族一致：解析要报错、要提示，属于构建期的一次性工作；
    /// 求值必须便宜到每帧无感（读一个 bool）。
    /// </para>
    /// </summary>
    internal static class InfoBoxConditions
    {
        #region Public API

        /// <summary>
        /// 解析并登记一个可见性条件；名字为空表示无条件（不登记）。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="attribute">信息框特性实例。</param>
        /// <param name="visibleIfMemberName">条件成员名；为空表示始终显示。</param>
        /// <remarks>
        /// 解析失败时**不登记**（<see cref="ConditionResolver.TryResolve"/> 已记告警），
        /// 于是 <see cref="IsVisible"/> 走「没有条件 ⇒ 显示」那条路——与条件族
        /// 「失败即放行」的取舍一致：拼错名字不该让内容消失。
        /// </remarks>
        public static void Register(InspectorProperty property, Attribute attribute, string visibleIfMemberName)
        {
            if (property == null || attribute == null || string.IsNullOrWhiteSpace(visibleIfMemberName))
            {
                return;
            }

            if (!ConditionResolver.TryResolve(property, visibleIfMemberName, out var condition))
            {
                return;
            }

            property.State.GetOrCreate<InfoBoxVisibilityState>().Conditions[attribute] = condition;
        }

        /// <summary>
        /// 判断该信息框当前是否应当显示。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="attribute">信息框特性实例。</param>
        /// <returns>应当显示返回 <c>true</c>（无条件时恒为 <c>true</c>）。</returns>
        public static bool IsVisible(InspectorProperty property, Attribute attribute)
        {
            var state = property.State.Get<InfoBoxVisibilityState>();
            if (state == null || !state.Conditions.TryGetValue(attribute, out var condition))
            {
                return true;
            }

            return condition();
        }

        #endregion
    }

    /// <summary>
    /// <see cref="InfoBoxAttribute"/>：在构建期解析它的 <c>visibleIf</c>。
    /// </summary>
    internal sealed class InfoBoxProcessor : AttributeProcessor<InfoBoxAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            InfoBoxAttribute attribute,
            IList<Attribute> attributes)
        {
            InfoBoxConditions.Register(property, attribute, attribute.VisibleIfMemberName);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="DetailedInfoBoxAttribute"/>：在构建期解析它的 <c>visibleIf</c>。
    /// </summary>
    internal sealed class DetailedInfoBoxProcessor : AttributeProcessor<DetailedInfoBoxAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            DetailedInfoBoxAttribute attribute,
            IList<Attribute> attributes)
        {
            InfoBoxConditions.Register(property, attribute, attribute.VisibleIf);
        }

        #endregion
    }
}
