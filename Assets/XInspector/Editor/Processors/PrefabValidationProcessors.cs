using System;
using System.Collections.Generic;

namespace XInspector.Editor
{
    // [DisallowModificationsIn] 有两件事，分居两侧：
    // **禁用**在这里（处理器不得绘制），**「已经改过」的提示**在绘制器里
    // （PrefabValidationDrawers，判据需要看序列化属性，而处理器不该去画东西）。
    // 判据与四个条件族共用同一块探测，故这里直接用 PrefabConditions。

    /// <summary>
    /// <see cref="DisallowModificationsInAttribute"/> 的只读门控：处在该上下文里就变灰。
    /// </summary>
    /// <remarks>
    /// 求值器语义是「返回 true 表示只读」，与 <c>[DisableIn]</c> 同向，不需要取反。
    /// </remarks>
    internal sealed class DisallowModificationsInProcessor : AttributeProcessor<DisallowModificationsInAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            DisallowModificationsInAttribute attribute,
            IList<Attribute> attributes)
        {
            property.State.ReadOnlyResolver =
                PrefabConditions.For(property.Owner?.Targets, attribute.PrefabKind, invert: false);
        }
    }
}
