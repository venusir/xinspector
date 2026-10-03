using System;
using System.Collections.Generic;
using UnityEngine;

namespace XInspector.Editor
{
    // 条件族的处理器。每个都只做一件事：把「这个属性该不该显示/可不可编辑」变成一条
    // 每帧求值的委托，装进 PropertyState。
    //
    // 它们**不绘制任何东西**——这正是处理器与绘制器的分界。判断不产出像素，
    // 塞进绘制器只会让绘制器变得既画东西又做决策。

    /// <summary>
    /// 条件为真时显示。
    /// </summary>
    internal sealed class ShowIfProcessor : AttributeProcessor<ShowIfAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, ShowIfAttribute attribute, IList<Attribute> attributes)
        {
            ConditionResolver.InstallVisibility(property, attribute.Condition, invert: false);
        }
    }

    /// <summary>
    /// 条件为真时隐藏。
    /// </summary>
    internal sealed class HideIfProcessor : AttributeProcessor<HideIfAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, HideIfAttribute attribute, IList<Attribute> attributes)
        {
            ConditionResolver.InstallVisibility(property, attribute.Condition, invert: true);
        }
    }

    /// <summary>
    /// 条件为真时可编辑。
    /// </summary>
    /// <remarks>
    /// 求值器的语义是「返回 true 表示只读」，所以这里要取反：
    /// 「条件为真即可编辑」等价于「条件为假才只读」。
    /// </remarks>
    internal sealed class EnableIfProcessor : AttributeProcessor<EnableIfAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, EnableIfAttribute attribute, IList<Attribute> attributes)
        {
            ConditionResolver.InstallReadOnly(property, attribute.Condition, invert: true);
        }
    }

    /// <summary>
    /// 条件为真时只读。
    /// </summary>
    internal sealed class DisableIfProcessor : AttributeProcessor<DisableIfAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, DisableIfAttribute attribute, IList<Attribute> attributes)
        {
            ConditionResolver.InstallReadOnly(property, attribute.Condition, invert: false);
        }
    }

    // 模式条件：判据是「当前在不在播放模式」，与字段值无关。
    //
    // 用缓存的静态委托而非每个属性各 new 一个闭包：这些求值器没有捕获，可以安全共享，
    // 而条件是每帧要调的，省下的是每次建树时每个属性一个委托的分配。
    //
    // 注意 here 读的是 UnityEngine.Application——Runtime 侧的属性本身**碰不到** Unity
    // （那条契约由 Tests.Native 编译期强制），所以判据只能落在编辑器侧。

    /// <summary>模式条件共用的求值器。</summary>
    internal static class ModeConditions
    {
        /// <summary>正在播放。</summary>
        public static readonly Func<bool> IsPlaying = () => Application.isPlaying;

        /// <summary>不在播放（即编辑模式）。</summary>
        public static readonly Func<bool> IsNotPlaying = () => !Application.isPlaying;
    }

    /// <summary>
    /// 编辑模式下隐藏。
    /// </summary>
    internal sealed class HideInEditorModeProcessor : AttributeProcessor<HideInEditorModeAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, HideInEditorModeAttribute attribute, IList<Attribute> attributes)
        {
            property.State.VisibilityResolver = ModeConditions.IsPlaying;
        }
    }

    /// <summary>
    /// 播放模式下隐藏。
    /// </summary>
    internal sealed class HideInPlayModeProcessor : AttributeProcessor<HideInPlayModeAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, HideInPlayModeAttribute attribute, IList<Attribute> attributes)
        {
            property.State.VisibilityResolver = ModeConditions.IsNotPlaying;
        }
    }

    /// <summary>
    /// 编辑模式下禁用。
    /// </summary>
    internal sealed class DisableInEditorModeProcessor : AttributeProcessor<DisableInEditorModeAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, DisableInEditorModeAttribute attribute, IList<Attribute> attributes)
        {
            property.State.ReadOnlyResolver = ModeConditions.IsNotPlaying;
        }
    }

    /// <summary>
    /// 播放模式下禁用。
    /// </summary>
    internal sealed class DisableInPlayModeProcessor : AttributeProcessor<DisableInPlayModeAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, DisableInPlayModeAttribute attribute, IList<Attribute> attributes)
        {
            property.State.ReadOnlyResolver = ModeConditions.IsPlaying;
        }
    }
}
