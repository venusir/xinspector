using System;
using System.Collections.Generic;

namespace XInspector.Editor
{
    // 内嵌环境条件的处理器。每个都只做一件事：把「当前在不在内嵌编辑器里」
    // 变成一条每帧求值的委托，装进 PropertyState。
    //
    // 判据是绘制期的静态深度（InlineEditorDrawContext.Depth），与上面那组模式条件读
    // Application.isPlaying 完全同构——**解析在构建期、求值在绘制期**这条分工没有变：
    // 构建期只负责装委托，委托里读的值每帧现取。
    //
    // 它们**不绘制任何东西**——这正是处理器与绘制器的分界。

    /// <summary>内嵌环境条件共用的求值器。</summary>
    /// <remarks>
    /// 用缓存的静态委托而非每个属性各 new 一个闭包：这些求值器没有捕获，可以安全共享，
    /// 而条件是每帧要调的，省下的是每次建树时每个属性一个委托的分配
    /// （与 <see cref="ModeConditions"/> 同款）。
    /// </remarks>
    internal static class InlineEditorConditions
    {
        /// <summary>正在内嵌编辑器里绘制。</summary>
        public static readonly Func<bool> Inside = () => InlineEditorDrawContext.Depth > 0;

        /// <summary>不在内嵌编辑器里（即外层自己的 Inspector）。</summary>
        public static readonly Func<bool> NotInside = () => InlineEditorDrawContext.Depth <= 0;
    }

    /// <summary>
    /// 只在内嵌编辑器里显示。
    /// </summary>
    internal sealed class ShowInInlineEditorsProcessor : AttributeProcessor<ShowInInlineEditorsAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, ShowInInlineEditorsAttribute attribute, IList<Attribute> attributes)
        {
            property.State.VisibilityResolver = InlineEditorConditions.Inside;
        }
    }

    /// <summary>
    /// 在内嵌编辑器里隐藏。
    /// </summary>
    internal sealed class HideInInlineEditorsProcessor : AttributeProcessor<HideInInlineEditorsAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, HideInInlineEditorsAttribute attribute, IList<Attribute> attributes)
        {
            property.State.VisibilityResolver = InlineEditorConditions.NotInside;
        }
    }

    /// <summary>
    /// 在内嵌编辑器里只读。
    /// </summary>
    /// <remarks>
    /// 求值器的语义是「返回 true 表示只读」，所以这里直接装 <see cref="InlineEditorConditions.Inside"/>，
    /// 不需要取反——与 <c>[DisableIf]</c> 那边同向。
    /// </remarks>
    internal sealed class DisableInInlineEditorsProcessor : AttributeProcessor<DisableInInlineEditorsAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, DisableInInlineEditorsAttribute attribute, IList<Attribute> attributes)
        {
            property.State.ReadOnlyResolver = InlineEditorConditions.Inside;
        }
    }
}
