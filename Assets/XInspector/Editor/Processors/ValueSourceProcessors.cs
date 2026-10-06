using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="MinMaxSliderAttribute"/>：把「边界取自哪个成员」在**构建期**解析出来。
    /// <para>
    /// 解析一次、绘制期每帧只读值——与条件族同一条分工。成员特性不存在
    /// <c>[ToggleGroup]</c> 那个「分组节点在处理器阶段还不存在」的问题，
    /// 故不需要退到绘制期解析。解析本身走 <see cref="MemberReferenceResolver"/>
    /// （四级阶梯），边界因此也可以指向普通字段/属性或无参方法。
    /// </para>
    /// </summary>
    internal sealed class MinMaxSliderProcessor : AttributeProcessor<MinMaxSliderAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            MinMaxSliderAttribute attribute,
            IList<Attribute> attributes)
        {
            var state = property.State.GetOrCreate<MinMaxSliderState>();

            state.UsesDynamicBounds =
                attribute.MinMaxValueGetter != null ||
                attribute.MinValueGetter != null ||
                attribute.MaxValueGetter != null;

            if (!state.UsesDynamicBounds)
            {
                // 纯字面量：构建期无事可做。
                state.Resolved = true;
                return;
            }

            if (attribute.MinMaxValueGetter != null)
            {
                // 官方语义：这一个非 null 时覆盖其余四个。
                if (!MemberReferenceResolver.TryResolveVector2(
                        property, attribute.MinMaxValueGetter, out var bounds, out var reason))
                {
                    Warn(property, attribute.MinMaxValueGetter, reason);
                    return;
                }

                state.MinMaxGetter = bounds;
                state.Resolved = true;
                return;
            }

            if (attribute.MinValueGetter != null)
            {
                if (!MemberReferenceResolver.TryResolveFloat(
                        property, attribute.MinValueGetter, out var min, out var minReason))
                {
                    Warn(property, attribute.MinValueGetter, minReason);
                    return;
                }

                state.MinGetter = min;
            }

            if (attribute.MaxValueGetter != null)
            {
                if (!MemberReferenceResolver.TryResolveFloat(
                        property, attribute.MaxValueGetter, out var max, out var maxReason))
                {
                    Warn(property, attribute.MaxValueGetter, maxReason);
                    return;
                }

                state.MaxGetter = max;
            }

            state.Resolved = true;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 记一条解析失败的构建期告警。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="reason">失败原因。</param>
        private static void Warn(InspectorProperty property, string memberName, string reason)
        {
            Debug.LogWarning(
                $"[XInspector] 属性「{property.Path}」上的 [MinMaxSlider] 边界「{memberName}」无法解析：{reason}。" +
                "该特性的动态边界已忽略，字段退回普通绘制。" +
                "边界可以是序列化成员、普通字段/属性，或无参返回 float / Vector2 的方法" +
                "（Odin 的 $/@ 表达式语言本包不做）。");
        }

        #endregion
    }

    /// <summary>
    /// <see cref="ValueDropdownAttribute"/>：把「选项来自哪个成员」在**构建期**解析出来。
    /// </summary>
    /// <remarks>
    /// 判定「源与目标类型是否一致」**不在这里做**，而在复制那一刻由
    /// <c>SerializedValueCopier</c> 做。理由是那需要知道元素的托管类型（反射），
    /// 而 <see cref="SerializedProperty"/> 只给得出 <c>propertyType</c> 与枚举成员名；
    /// 「类型对不上就按索引复制」在枚举上会静默写错，用枚举成员名比对即可挡住，
    /// 且这条判定放在复制处**离得最近**、也能无头测试。
    /// </remarks>
    internal sealed class ValueDropdownProcessor : AttributeProcessor<ValueDropdownAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            ValueDropdownAttribute attribute,
            IList<Attribute> attributes)
        {
            var state = property.State.GetOrCreate<ValueDropdownState>();

            SerializedMemberResolver.TryResolve(
                property, attribute.ValuesGetter, MemberScope.Object, MemberKind.Array,
                out var source, out var reason);

            if (source == null)
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上的 [ValueDropdown] 选项来源「{attribute.ValuesGetter}」" +
                    $"无法解析：{reason}。该特性已忽略，字段退回普通绘制。" +
                    "注意来源必须是**序列化**的数组或 List 字段——Odin 的 $/@ 表达式与方法调用本包不做。");
                return;
            }

            state.Source = source;
            state.Resolved = true;
        }

        #endregion
    }

    /// <summary>
    /// <see cref="ValueDropdownAttribute"/> 的解析结果，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    internal sealed class ValueDropdownState
    {
        /// <summary>提供选项的数组/List 成员；解析失败为 <c>null</c>。</summary>
        public SerializedProperty Source;

        /// <summary>来源是否解析成功。</summary>
        public bool Resolved;
    }

    /// <summary>
    /// <see cref="MinMaxSliderAttribute"/> 的解析结果，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    /// <remarks>
    /// 三个读取器都是**每帧现读**的委托：序列化成员绑的是活句柄，反射成员绑的是构建期编译出来的
    /// 访问器（<b>不装箱</b>——边界在绘制路径上每帧读一次）。两者对绘制器没有差别，
    /// 这正是它不再存 <see cref="SerializedProperty"/> 的原因。
    /// </remarks>
    internal sealed class MinMaxSliderState
    {
        /// <summary>同时提供上下界的成员（<c>Vector2</c>）；未使用或解析失败为 <c>null</c>。</summary>
        public Func<Vector2> MinMaxGetter;

        /// <summary>提供下界的成员（<c>float</c>）；未使用或解析失败为 <c>null</c>。</summary>
        public Func<float> MinGetter;

        /// <summary>提供上界的成员（<c>float</c>）；未使用或解析失败为 <c>null</c>。</summary>
        public Func<float> MaxGetter;

        /// <summary>特性是否用了任一个「取自成员」的形态。</summary>
        public bool UsesDynamicBounds;

        /// <summary>动态边界是否全部解析成功（纯字面量形态恒为 <c>true</c>）。</summary>
        public bool Resolved;
    }
}
