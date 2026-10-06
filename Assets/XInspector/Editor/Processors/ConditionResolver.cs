using System;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 把「另一个成员的当前值」变成一条可每帧求值的条件。
    /// <para>
    /// 条件的解析（找成员、校类型）在**构建期**做一次，求值（读值）在**绘制期**每帧做。
    /// 这个分工是刻意的：解析要报错、要提示，属于构建期的一次性工作；求值必须便宜到每帧无感。
    /// </para>
    /// <para>
    /// 求值器是 <see cref="Func{TResult}"/> 而非直接设一个 bool——
    /// **这让「要不要显示」完全不碰 GUI，可以无头测试**，正是本仓测试策略依赖的那条分界。
    /// </para>
    /// </summary>
    internal static class ConditionResolver
    {
        #region Public API

        /// <summary>
        /// 为属性装一个基于条件的可见性求值器。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="conditionName">条件成员名。</param>
        /// <param name="invert">是否取反（<c>[HideIf]</c> 用反，<c>[ShowIf]</c> 用正）。</param>
        public static void InstallVisibility(InspectorProperty property, string conditionName, bool invert)
        {
            var condition = Resolve(property, conditionName);
            if (condition == null)
            {
                return;
            }

            property.State.VisibilityResolver = invert ? (Func<bool>)(() => !condition()) : condition;
        }

        /// <summary>
        /// 为属性装一个基于条件的只读求值器。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="conditionName">条件成员名。</param>
        /// <param name="invert">
        /// 是否对条件取反。求值器的语义是「返回 true 表示**只读**」，因此：
        /// <c>[DisableIf]</c>（条件为真则只读）用 <c>false</c>；
        /// <c>[EnableIf]</c>（条件为真则**可编辑**，即条件为假才只读）用 <c>true</c>。
        /// </param>
        public static void InstallReadOnly(InspectorProperty property, string conditionName, bool invert)
        {
            var condition = Resolve(property, conditionName);
            if (condition == null)
            {
                return;
            }

            property.State.ReadOnlyResolver = invert ? (Func<bool>)(() => !condition()) : condition;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 解析条件成员，返回一个每帧求值的委托。
        /// </summary>
        /// <param name="property">目标属性，用来拿到它所属的序列化对象与容器。</param>
        /// <param name="conditionName">条件成员名。</param>
        /// <param name="condition">解析出的求值器；失败时为 <c>null</c>。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// 公开给同程序集内的其它构建期解析者（如信息框的 <c>visibleIf</c>）复用——
        /// 「找成员、校类型、失败告警」这段逻辑只该有一份。
        /// </para>
        /// <para>
        /// <b>找成员那件事在 <see cref="MemberReferenceResolver"/> 里</b>（四级阶梯、次序、
        /// 类型判定都记在那里），本类只剩两件自己的事：装进 <see cref="PropertyState"/>，
        /// 以及**告警**——告警的时机与去重方式是各消费者自己的（条件族是构建期直接
        /// <c>LogWarning</c>，<c>[ToggleGroup]</c> 是绘制期 <see cref="DrawerWarnings.Once"/>），
        /// 故不跟着解析一起收上去。
        /// </para>
        /// <para>
        /// <b>解析失败不抛异常，只告警并放弃。</b> 一个拼错的条件名不该让整个 Inspector 白屏——
        /// 那是使用方看到本插件的第一眼。放弃的后果是「条件不生效、字段照常显示」，
        /// 配合告警足以定位；而抛异常会让后果升级成「什么都看不见」。
        /// </para>
        /// </remarks>
        internal static bool TryResolve(InspectorProperty property, string conditionName, out Func<bool> condition)
        {
            if (MemberReferenceResolver.TryResolveBoolean(
                    property, conditionName, MemberScope.Object, out condition, out _, out var reason))
            {
                return true;
            }

            Warn(property, conditionName, reason);
            return false;
        }

        /// <summary>
        /// 取条件成员的解析结果，失败时返回 <c>null</c>。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="conditionName">条件成员名。</param>
        /// <returns>求值器；解析失败返回 <c>null</c>。</returns>
        private static Func<bool> Resolve(InspectorProperty property, string conditionName)
        {
            TryResolve(property, conditionName, out var condition);
            return condition;
        }

        /// <summary>
        /// 记一条条件解析失败的告警。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="conditionName">条件名。</param>
        /// <param name="reason">失败原因。</param>
        private static void Warn(InspectorProperty property, string conditionName, string reason)
        {
            Debug.LogWarning(
                $"[XInspector] 属性「{property?.Path}」上的条件「{conditionName}」无法求值：{reason}。" +
                "条件已忽略，该属性按无条件处理。" +
                "条件可以是序列化成员（public 字段或 [SerializeField] 私有字段）、" +
                "普通字段/属性，或**无参、非泛型、返回 bool** 的方法；" +
                "写在别的对象上的 \"@other.field\" 语法不支持。");
        }

        #endregion
    }
}
