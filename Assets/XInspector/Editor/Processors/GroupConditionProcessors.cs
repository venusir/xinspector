using System;
using System.Collections.Generic;
using XInspector.Internal;

namespace XInspector.Editor
{
    /// <summary>
    /// 分组条件处理器的公共部分。
    /// <para>
    /// 与条件族其余处理器不同，它**不是**「节点带该特性才跑」：类级声明可能整份到不了节点
    /// （见 <see cref="GroupConditionSource"/>），而那种情形恰恰要照样生效。因此这里继承
    /// 非泛型的 <see cref="AttributeProcessor"/>，判据自己写——只看分组节点。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 在**第二趟**（分组装配之后）跑：分组节点到第一趟时还不存在，判据没有可以挂上去的节点。
    /// 装配只装一个每帧求值的可见性解析器，绘制路径零改动——<see cref="InspectorProperty.Draw"/>
    /// 在进链之前查 <see cref="PropertyState.IsVisible"/>，于是整组连同子成员一起消失。
    /// </remarks>
    internal abstract class GroupConditionProcessorBase : AttributeProcessor
    {
        /// <summary>条件为真时是否隐藏（<c>[ShowIfGroup]</c> 用 <c>false</c>）。</summary>
        protected abstract bool Invert { get; }

        /// <summary>只看分组节点——条件挂在节点上，与成员的自身特性无关。</summary>
        /// <param name="property">候选属性。</param>
        /// <returns>是分组节点返回 <c>true</c>。</returns>
        public sealed override bool CanProcessSelfAttributes(InspectorProperty property)
        {
            return property != null && property.Kind == InspectorPropertyKind.Group;
        }

        /// <summary>找本节点上生效的那份声明，装一个可见性求值器。</summary>
        /// <param name="property">分组节点。</param>
        /// <param name="attributes">该节点的特性列表。</param>
        public sealed override void ProcessSelfAttributes(InspectorProperty property, IList<Attribute> attributes)
        {
            var condition = GroupConditionSource.Resolve(property, attributes, HandledAttributeType);
            if (condition == null)
            {
                return;
            }

            ConditionResolver.InstallVisibility(property, condition, Invert);
        }
    }

    /// <summary>
    /// <see cref="ShowIfGroupAttribute"/>：条件为真时整个分组可见。
    /// </summary>
    internal sealed class ShowIfGroupProcessor : GroupConditionProcessorBase
    {
        /// <inheritdoc/>
        internal override Type HandledAttributeType => typeof(ShowIfGroupAttribute);

        /// <inheritdoc/>
        protected override bool Invert => false;
    }

    /// <summary>
    /// <see cref="HideIfGroupAttribute"/>：条件为真时整个分组隐藏。
    /// </summary>
    internal sealed class HideIfGroupProcessor : GroupConditionProcessorBase
    {
        /// <inheritdoc/>
        internal override Type HandledAttributeType => typeof(HideIfGroupAttribute);

        /// <inheritdoc/>
        protected override bool Invert => true;
    }

    /// <summary>
    /// 找「这一节上真正生效」的那份分组条件声明，返回它的条件成员名。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 两条来源：**节点自己带的**（常规——声明处会随分组装配克隆到节点上），以及**容器类型上的
    /// 类级声明**。后者是必需的：类级分组分发（<c>ClassLevelGroupProcessor</c>）在成员**有**
    /// 自有分组时只把自有分组的路径加前缀，类级那份特性本身到不了任何节点，条件作为数据会被
    /// 静默丢掉——症状是「类上写了 `[ShowIfGroup]`，一个成员带自己的分组就整条失效」。
    /// 嵌套层与元素层同款，只是容器从「根」换成了那个复合成员节点（2026-10-06 起）。
    /// </para>
    /// <para>
    /// 两条来源都只认「正常声明在节点所在路径上」的那一份：祖先合成出来的克隆<b>不</b>生效
    /// （否则同祖先下的兄弟分组会被一起藏掉），判据在 <see cref="GroupConditionAttribute"/>。
    /// 匹配按**具体类型**（而非基类）——多类型并存时，A 的条件不该被 B 的处理器取走。
    /// </para>
    /// <para>
    /// 来源二沿**父链就近到远**找：嵌套层自己的声明压过根上同名的声明（与条件名
    /// 「先同级、后根」的次序观一致）。判据写成「容器路径 + 声明路径 == 本节点路径」——
    /// 根的空路径让它退化成最初的 <c>GroupID == property.Path</c>，根上的行为逐字不变。
    /// </para>
    /// </remarks>
    internal static class GroupConditionSource
    {
        /// <summary>
        /// 取本节点上指定类型真正生效的条件名。
        /// </summary>
        /// <param name="property">分组节点。</param>
        /// <param name="attributes">该节点的特性列表。</param>
        /// <param name="attributeType">分组条件特性的**具体**类型。</param>
        /// <returns>条件成员名；本节点上不该生效时返回 <c>null</c>。</returns>
        public static string Resolve(InspectorProperty property, IList<Attribute> attributes, Type attributeType)
        {
            if (attributes != null)
            {
                for (var i = 0; i < attributes.Count; i++)
                {
                    if (attributes[i].GetType() == attributeType
                        && ((GroupConditionAttribute)attributes[i]).AppliesToOwnNode)
                    {
                        return ((GroupConditionAttribute)attributes[i]).Condition;
                    }
                }
            }

            // 来源二：**容器类型上的类级声明**。根读节点自身的特性列表——处理器可能改写过它，
            // 改成反射会漏掉那类注入；嵌套 / 元素容器读类型自己的声明（它们没有「收集到根节点上」
            // 那条通道）。分组节点的 Type 为 null——跳过、继续上溯。
            for (var container = property?.Parent; container != null; container = container.Parent)
            {
                if (container.Kind == InspectorPropertyKind.Root)
                {
                    var fromRoot = MatchDeclared(
                        container.Attributes.Raw, container.Path, property.Path, attributeType);
                    if (fromRoot != null)
                    {
                        return fromRoot;
                    }
                }
                else if (container.Type != null)
                {
                    var fromType = MatchDeclared(
                        container.Type.GetCustomAttributes(typeof(GroupConditionAttribute), true),
                        container.Path,
                        property.Path,
                        attributeType);
                    if (fromType != null)
                    {
                        return fromType;
                    }
                }
            }

            return null;
        }

        /// <summary>在某个容器声明的特性里找「恰好落在本节点路径上」的那份条件。</summary>
        /// <param name="declared">容器声明的特性（根是节点特性列表，嵌套 / 元素容器是类型特性）。</param>
        /// <param name="containerPath">容器路径（根为空串）。</param>
        /// <param name="nodePath">本节点路径。</param>
        /// <param name="attributeType">分组条件特性的**具体**类型。</param>
        /// <returns>条件成员名；没有返回 <c>null</c>。</returns>
        private static string MatchDeclared(
            System.Collections.IEnumerable declared, string containerPath, string nodePath, Type attributeType)
        {
            foreach (var item in declared)
            {
                if (!(item is GroupConditionAttribute group) || item.GetType() != attributeType)
                {
                    continue;
                }

                var expected = containerPath.Length == 0
                    ? group.GroupID
                    : containerPath + PropertyGroupPath.Separator + group.GroupID;

                if (string.Equals(expected, nodePath, StringComparison.Ordinal))
                {
                    return group.Condition;
                }
            }

            return null;
        }
    }
}
