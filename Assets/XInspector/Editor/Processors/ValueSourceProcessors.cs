using System;
using System.Collections;
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
    /// <para>
    /// 解析走 <see cref="MemberReferenceResolver"/> 的四级阶梯，故来源既可以是**序列化**的
    /// 数组 / List 成员，也可以是普通字段 / 属性 / 无参方法（声明类型须实现
    /// <see cref="IList"/>）。两个形态的消费侧不同（一个吃句柄、一个吃托管值），
    /// 由 <see cref="ValueDropdownState"/> 上哪一个来源非 <c>null</c> 判别。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 判定「源与目标类型是否一致」**不在这里做**，而在复制那一刻由
    /// <c>SerializedValueCopier</c> / <c>ReflectedValueCopier</c> 做。理由是那需要知道元素的
    /// 托管类型（反射），而 <see cref="SerializedProperty"/> 只给得出 <c>propertyType</c>
    /// 与枚举成员名；「类型对不上就按索引复制」在枚举上会静默写错，用枚举成员名比对即可挡住，
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

            if (!MemberReferenceResolver.TryResolveList(
                    property, attribute.ValuesGetter, MemberScope.Object,
                    out var sourceList, out var source, out var elementType, out var reason))
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上的 [ValueDropdown] 选项来源「{attribute.ValuesGetter}」" +
                    $"无法解析：{reason}。该特性已忽略，字段退回普通绘制。" +
                    "来源可以是**序列化**的数组 / List，或声明类型实现 IList 的普通字段 / 属性 / 无参方法" +
                    "（只实现 IEnumerable 的源不收——string 也只实现 IEnumerable<char>，" +
                    "放行会静默变出字符选项表；Odin 的 $/@ 表达式语言本包不做）。");
                return;
            }

            state.Source = source;
            state.SourceList = sourceList;
            state.ElementType = elementType;
            state.Resolved = true;
        }

        #endregion
    }

    /// <summary>
    /// <see cref="ValueDropdownAttribute"/> 的解析结果，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    /// <remarks>
    /// <b>两个形态共用一个状态对象</b>，判别式是「哪一个来源非 <c>null</c>」——
    /// 不另设枚举或布尔开关：「谁有谁没有」是硬事实，再加一个字段就得维护它与事实同步。
    /// 两者都为空表示解析失败（<see cref="Resolved"/> 为 <c>false</c>）。
    /// </remarks>
    internal sealed class ValueDropdownState
    {
        /// <summary>**序列化**形态的来源：数组 / List 的活句柄；反射形态为 <c>null</c>。</summary>
        public SerializedProperty Source;

        /// <summary>
        /// **反射**形态的来源：每帧现读的读取器（实例缺失或来源给回空引用时为 <c>null</c>）；
        /// 序列化形态为 <c>null</c>。
        /// </summary>
        public Func<IList> SourceList;

        /// <summary>
        /// 反射形态的**元素声明类型**（从成员声明类型推，推不出来为 <c>null</c>）——
        /// 选项标签按它格式化；序列化形态恒为 <c>null</c>。
        /// </summary>
        public Type ElementType;

        /// <summary>
        /// 来源是否解析成功；为 <c>true</c> 时 <see cref="Source"/> 与 <see cref="SourceList"/>
        /// **恰有一个**非 <c>null</c>。
        /// </summary>
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
