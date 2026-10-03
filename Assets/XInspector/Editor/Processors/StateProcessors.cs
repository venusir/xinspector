using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="ReadOnlyAttribute"/>：把属性设为**恒只读**。
    /// <para>
    /// 走处理器而非绘制器：只读是**状态**，不是像素。状态只有一份真相
    /// （<see cref="PropertyState.IsReadOnly"/>），绘制路径末端已按它上禁用罩，
    /// 一个绘制器再套一层 <c>DisabledScope</c> 只会让两处各说各话。
    /// </para>
    /// </summary>
    internal sealed class ReadOnlyProcessor : AttributeProcessor<ReadOnlyAttribute>
    {
        #region Public API

        /// <summary>
        /// 排在条件族（默认 0）**之后**：「恒只读」比「条件只读」更具体，并存时应当本特性赢。
        /// </summary>
        /// <remarks>
        /// 用显式的优先级而不是依赖「同优先级按类型名排序」——后者只是确定性的兜底，
        /// 拿它来表达语义会让「谁赢」变成一次改名就能改掉的事。
        /// </remarks>
        public override float ProcessorPriority => 100f;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            ReadOnlyAttribute attribute,
            IList<Attribute> attributes)
        {
            property.State.SetReadOnly(true);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="LabelTextAttribute"/>：把标签文本算好放进
    /// <see cref="PropertyState.LabelOverride"/>。
    /// <para>
    /// 在构建期算一次，绘制期零成本——<see cref="InspectorProperty.Label"/> 本来就优先读那个槽。
    /// 若改成绘制器，就得每帧新建 <see cref="GUIContent"/>，那违反本包的每帧零分配约定。
    /// </para>
    /// </summary>
    internal sealed class LabelTextProcessor : AttributeProcessor<LabelTextAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            LabelTextAttribute attribute,
            IList<Attribute> attributes)
        {
            var text = attribute.NicifyText
                ? ObjectNames.NicifyVariableName(attribute.Text)
                : attribute.Text;

            property.State.LabelOverride = new GUIContent(text);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="PropertyTooltipAttribute"/>：把提示挂到标签上。
    /// <para>
    /// 与 <see cref="LabelTextProcessor"/> 共用 <see cref="PropertyState.LabelOverride"/>，
    /// 因此**必须排在它之后**（优先级 10 &gt; 0）：先有标签文本，再往上面挂提示。
    /// 读的是 <see cref="InspectorProperty.Label"/> 而不是 <see cref="InspectorProperty.Name"/>——
    /// 后者会绕过 <c>[LabelText]</c> 刚设好的文本，把标签打回字段名。
    /// </para>
    /// </summary>
    internal sealed class PropertyTooltipProcessor : AttributeProcessor<PropertyTooltipAttribute>
    {
        #region Public API

        /// <summary>
        /// 排在标签文本之后：提示挂在标签上，得先有标签。
        /// </summary>
        public override float ProcessorPriority => 10f;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            PropertyTooltipAttribute attribute,
            IList<Attribute> attributes)
        {
            var existing = property.State.LabelOverride;
            if (existing != null)
            {
                // 就地改提示而非新建：标签文本（可能来自 [LabelText]）必须原样保住。
                existing.tooltip = attribute.Tooltip;
                return;
            }

            property.State.LabelOverride = new GUIContent(property.Label.text, attribute.Tooltip);
        }

        #endregion
    }
}
