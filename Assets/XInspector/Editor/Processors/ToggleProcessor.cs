using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="ToggleAttribute"/>：把开关那个 bool 解析出来，装进
    /// <see cref="PropertyState.ReadOnlyResolver"/>。
    /// <para>
    /// <b>解析在构建期一次、求值每帧</b>——与条件族同一条分工。处理器拿得到成员节点的
    /// 值入口（成员节点在处理器阶段已经带上了 <c>ValueEntry</c>），故
    /// <c>FindPropertyRelative</c> 在构建期就能做完，绘制期只剩「读一个 bool」。
    /// </para>
    /// </summary>
    internal sealed class ToggleProcessor : AttributeProcessor<ToggleAttribute>
    {
        #region Public API

        /// <summary>
        /// 排在条件族（0）之后、<c>[ReadOnly]</c>（100）之前：
        /// 与 <c>[DisableIf]</c> 并存时本特性赢（门控更具体），与 <c>[ReadOnly]</c> 并存时后者赢。
        /// </summary>
        public override float ProcessorPriority => 50f;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            ToggleAttribute attribute,
            IList<Attribute> attributes)
        {
            var state = property.State.GetOrCreate<ToggleState>();
            state.Toggle = Resolve(property, attribute.ToggleMemberName, out var reason);

            if (state.Toggle == null)
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上的 [Toggle] 开关「{attribute.ToggleMemberName}」" +
                    $"无法解析：{reason}。开关已忽略，该属性保持可编辑。");
                return;
            }

            // 闭包只在这里分配一次（构建期）；绘制期每帧只是读一个 bool。
            var toggle = state.Toggle;
            property.State.ReadOnlyResolver = () => !toggle.boolValue;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 在成员的值对象内部解析指定 bool 成员。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="memberName">相对路径。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>序列化属性；失败返回 <c>null</c>。</returns>
        private static SerializedProperty Resolve(InspectorProperty property, string memberName, out string reason)
        {
            reason = null;

            var serializedProperty = property.ValueEntry?.SerializedProperty;
            if (serializedProperty == null)
            {
                reason = "该属性没有序列化后端";
                return null;
            }

            var toggle = serializedProperty.FindPropertyRelative(memberName);
            if (toggle == null)
            {
                reason = "在值对象内部找不到这个成员（名字拼错，或它不在序列化范围内）";
                return null;
            }

            if (toggle.propertyType != SerializedPropertyType.Boolean)
            {
                reason = $"该成员不是 bool（实为 {toggle.propertyType}）";
                return null;
            }

            return toggle;
        }

        #endregion
    }

    /// <summary>
    /// <see cref="ToggleAttribute"/> 的解析结果，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    /// <remarks>
    /// 解析失败时 <see cref="Toggle"/> 为 <c>null</c>——绘制器据此决定不画开关，
    /// 但内侧照常往下传（字段不会因此消失）。
    /// </remarks>
    internal sealed class ToggleState
    {
        /// <summary>开关的序列化属性；解析失败为 <c>null</c>。</summary>
        public SerializedProperty Toggle;
    }
}
