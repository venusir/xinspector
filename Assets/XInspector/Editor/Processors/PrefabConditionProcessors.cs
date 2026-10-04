using System;
using System.Collections.Generic;

namespace XInspector.Editor
{
    // 预制体上下文条件族的处理器。每个都只做一件事：把「目标处在哪种预制体上下文」
    // 变成一条每帧求值的委托，装进 PropertyState。
    //
    // 与那四个模式条件（判据是 Application.isPlaying）、三个内嵌环境条件（判据是绘制期深度）
    // 完全同构：**解析在构建期、求值在绘制期**。判据每帧现取，故进出隔离编辑模式、
    // 实例化、断开连接都会在下一帧反映出来，不必重建属性树。
    //
    // 唯一的差别：这里的判据要用到**树的目标列表**，所以委托必须捕获它，
    // 不能像那两个那样共享静态的无捕获委托。捕获发生在构建期（每属性一个），
    // 绘制期只剩一次委托调用——不违反每帧零分配那条约定。
    //
    // 它们**不绘制任何东西**——这正是处理器与绘制器的分界。

    /// <summary>预制体上下文条件共用的求值器工厂。</summary>
    /// <remarks>
    /// 做成工厂而不是静态委托字段：无捕获的委托可以共享（见 <c>ModeConditions</c>），
    /// 而这里的判据依赖「这棵树在看哪些对象」，只能每个属性各捕获一份。
    /// 目标列表在树的存活期内不变（选中项一变就重建整棵树），故捕获它是安全的。
    /// </remarks>
    internal static class PrefabConditions
    {
        /// <summary>构造「全部目标是否都落在指定上下文里」的求值器。</summary>
        /// <param name="targets">树的目标列表。</param>
        /// <param name="requested">要求的上下文。</param>
        /// <param name="invert">是否取反（隐藏/可编辑那两族用反）。</param>
        /// <returns>每帧可调用的求值器。</returns>
        public static Func<bool> For(object[] targets, PrefabKind requested, bool invert)
        {
            if (invert)
            {
                return () => !PrefabContextProbe.MatchesAll(targets, requested);
            }

            return () => PrefabContextProbe.MatchesAll(targets, requested);
        }
    }

    /// <summary>
    /// 只在指定上下文里显示。
    /// </summary>
    internal sealed class ShowInProcessor : AttributeProcessor<ShowInAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, ShowInAttribute attribute, IList<Attribute> attributes)
        {
            property.State.VisibilityResolver =
                PrefabConditions.For(property.Owner?.Targets, attribute.PrefabKind, invert: false);
        }
    }

    /// <summary>
    /// 在指定上下文里隐藏。
    /// </summary>
    internal sealed class HideInProcessor : AttributeProcessor<HideInAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, HideInAttribute attribute, IList<Attribute> attributes)
        {
            property.State.VisibilityResolver =
                PrefabConditions.For(property.Owner?.Targets, attribute.PrefabKind, invert: true);
        }
    }

    /// <summary>
    /// 只在指定上下文里可编辑。
    /// </summary>
    /// <remarks>
    /// 求值器的语义是「返回 true 表示只读」，所以「只在某上下文里可编辑」要取反
    /// ——与 <c>[EnableIf]</c> 那边同款。
    /// </remarks>
    internal sealed class EnableInProcessor : AttributeProcessor<EnableInAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, EnableInAttribute attribute, IList<Attribute> attributes)
        {
            property.State.ReadOnlyResolver =
                PrefabConditions.For(property.Owner?.Targets, attribute.PrefabKind, invert: true);
        }
    }

    /// <summary>
    /// 在指定上下文里只读。
    /// </summary>
    /// <remarks>
    /// 语义与只读求值器同向（true 即只读），不需要取反——与 <c>[DisableIf]</c> 同款。
    /// </remarks>
    internal sealed class DisableInProcessor : AttributeProcessor<DisableInAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(InspectorProperty property, DisableInAttribute attribute, IList<Attribute> attributes)
        {
            property.State.ReadOnlyResolver =
                PrefabConditions.For(property.Owner?.Targets, attribute.PrefabKind, invert: false);
        }
    }
}
