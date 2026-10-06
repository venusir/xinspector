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
    /// 值入口（成员节点在处理器阶段已经带上了 <c>ValueEntry</c>），故解析在构建期就能做完，
    /// 绘制期只剩「读一个 bool」。解析本身走 <see cref="MemberReferenceResolver"/>，
    /// 与条件族、<c>[ToggleGroup]</c>、<c>[MinMaxSlider]</c> 共用同一层。
    /// </para>
    /// <para>
    /// <b>范围是 <see cref="MemberScope.Relative"/></b>：官方示例确认被指的 bool 在**值对象内部**
    /// （<c>t.Enabled</c> 那种）。这一格**没有反射腿**——值对象只有一个
    /// <see cref="UnityEditor.SerializedProperty"/>，没有实例句柄可以下钻。
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

            if (!MemberReferenceResolver.TryResolveBoolean(
                    property, attribute.ToggleMemberName, MemberScope.Relative,
                    out var read, out var toggle, out var reason))
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上的 [Toggle] 开关「{attribute.ToggleMemberName}」" +
                    $"无法解析：{reason}。开关已忽略，该属性保持可编辑。");
                return;
            }

            // 绘制器要用句柄画那个**可写**的开关（相对范围只有序列化那一格，故它恒非 null）。
            state.Toggle = toggle;

            // 闭包只在这里分配一次（构建期）；绘制期每帧只是读一个 bool。
            property.State.ReadOnlyResolver = () => !read();
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
