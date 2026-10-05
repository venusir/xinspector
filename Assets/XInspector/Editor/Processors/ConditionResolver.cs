using System;
using UnityEditor;
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
        /// <param name="property">目标属性，用来拿到它所属的序列化对象。</param>
        /// <param name="conditionName">条件成员名。</param>
        /// <param name="condition">解析出的求值器；失败时为 <c>null</c>。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// 公开给同程序集内的其它构建期解析者（如信息框的 <c>visibleIf</c>）复用——
        /// 「找成员、校类型、失败告警」这段逻辑只该有一份。
        /// </para>
        /// <para>
        /// <b>解析失败不抛异常，只告警并放弃。</b> 一个拼错的条件名不该让整个 Inspector 白屏——
        /// 那是使用方看到本插件的第一眼。放弃的后果是「条件不生效、字段照常显示」，
        /// 配合告警足以定位；而抛异常会让后果升级成「什么都看不见」。
        /// </para>
        /// <para>
        /// <b>解析顺序固定：同一嵌套对象里的兄弟成员（只对嵌套成员）→ 本对象上的序列化成员
        /// → 反射字段/属性 → 无参返回 bool 的方法。</b>
        /// 第一级是「嵌套层里写 <c>[ShowIf("flag")]</c> 指的是同层的 flag」这条直觉的落点；
        /// 顶层成员没有它（父节点是根而非成员），顺序对既有行为零变化。
        /// 序列化成员是本包的主路（它免费带着 Undo、预制体覆盖那一整套），反射那两级是兜底。
        /// 顺序固定下来，失败信息才可解释——「找不到」与「找到了但形状不对」是两句不同的话，
        /// 而「找到的是哪一级」也一样。
        /// </para>
        /// </remarks>
        internal static bool TryResolve(InspectorProperty property, string conditionName, out Func<bool> condition)
        {
            condition = null;

            // 第 0 级（**只在嵌套成员上生效**）：同一嵌套对象里的兄弟成员。
            // 顶层成员没有这一级——它的父节点是根而不是成员——因此对既有行为**零变化**。
            // 顺序是刻意的：嵌套层里写 [ShowIf("flag")] 指的是同层的 flag；
            // 只有当同层没有它时，才回落到根上的绝对名。
            var container = NestedScopeOf(property);
            if (container != null)
            {
                var sibling = container.FindPropertyRelative(conditionName);
                if (sibling != null)
                {
                    if (sibling.propertyType != SerializedPropertyType.Boolean)
                    {
                        // 找到了却类型不符：**不再往下找**（与绝对名那一级同款的理由——
                        // 继续找会报第二次警，而两条消息互相矛盾）。
                        Warn(property, conditionName,
                            $"找到的「{conditionName}」是 {sibling.propertyType}，条件必须是 bool");
                        return false;
                    }

                    condition = () => sibling.boolValue;
                    return true;
                }
            }

            // 第一级：序列化成员。这一段与 [Toggle]/[ToggleGroup] 共用类型判定——见 SerializedMemberResolver。
            var serializedObject = SerializedMemberResolver.FindSerializedObject(property);

            if (serializedObject != null)
            {
                var member = serializedObject.FindProperty(conditionName);
                if (member != null)
                {
                    if (member.propertyType != SerializedPropertyType.Boolean)
                    {
                        // 找到了却类型不符：**不再往下找**。继续找反射成员的话，
                        // 同一个名字会报第二次警，而两条消息互相矛盾。
                        Warn(property, conditionName,
                            $"找到的「{conditionName}」是 {member.propertyType}，条件必须是 bool");
                        return false;
                    }

                    // 每帧只读一个 bool，不分配、不查找——SerializedProperty 是活句柄，
                    // 跨 Update() 依然有效，所以解析一次就够。
                    condition = () => member.boolValue;
                    return true;
                }
            }

            // 嵌套成员**到此为止**，不走下面那两级反射兜底：反射是在**被检视对象**上找成员，
            // 拿到的是根上的同名成员——「条件看错了对象」，静默且极难归因。
            // 与「嵌套 [Serializable] 里的 [Button]/[ShowInInspector] 不生效」是同一条限制
            // （拿到嵌套实例需要一条本包没有的只读反射路径解析）。
            if (container != null)
            {
                Warn(property, conditionName,
                    "嵌套层里的条件必须是**序列化的兄弟成员**——反射字段/属性与方法的取值" +
                    "需要嵌套实例，本包没有那条读路径");
                return false;
            }

            // 第二、三级：反射成员与方法。同样在构建期解析一次，绘制期只有委托调用。
            if (!ReflectedMemberResolver.TryResolveBooleanCondition(
                    property?.Owner?.Targets, conditionName, out condition, out var reason))
            {
                Warn(property, conditionName, reason);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 取「同一嵌套对象」的序列化属性——嵌套成员的父节点是另一个成员时才有。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <returns>嵌套容器的序列化属性；顶层成员返回 <c>null</c>。</returns>
        private static SerializedProperty NestedScopeOf(InspectorProperty property)
        {
            var parent = property?.Parent;

            return parent != null && parent.Kind == InspectorPropertyKind.Member
                ? parent.ValueEntry?.SerializedProperty
                : null;
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
