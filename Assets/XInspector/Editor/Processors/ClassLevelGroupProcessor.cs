using System;
using System.Collections.Generic;
using System.Reflection;
using XInspector.Internal;

namespace XInspector.Editor
{
    /// <summary>
    /// 把**类型上**的分组特性分发到各成员。
    /// <para>
    /// 这是「父级注入」钩子的真正用途，也是本包此前已知限制里那条
    /// 「类级 <c>[BoxGroup]</c> 会把整个 Inspector 框起来、而不是让成员归属该分组」的修复。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>两个来源。</b> 被检视的最外层类型的类级特性经构建期收集，放在**根节点**上；
    /// 嵌套类型与元素类型自己带的（自 2026-10-06 起生效）没有那条收集通道，判据因此落到
    /// **容器的声明类型**（<see cref="InspectorProperty.Type"/>）上——与
    /// <see cref="ClassLevelInlinePropertyProcessor"/> 读 <c>field.FieldType</c> 同一条纪律。
    /// </para>
    /// <para>
    /// <b>语义。</b> 类级分组恒在最外层：
    /// </para>
    /// <list type="bullet">
    /// <item>成员**没有**自己的分组 → 直接归入类级分组；</item>
    /// <item>成员**有**自己的分组 → 把它的路径改写为「类级路径 / 原路径」，
    /// 于是它嵌在类级分组里面，而不是与之并列。</item>
    /// </list>
    /// <para>
    /// 嵌套层与元素层里，容器路径的前缀由**装配期**统一加（<c>AssembleNestedLevel</c>）——
    /// 本处理器只注入、不加前缀，两处都加会叠成 <c>stats/stats/组</c>。
    /// </para>
    /// <para>
    /// <b>只取第一个类级分组，其余忽略。</b> 一个类型上挂多个顶层分组没有明确语义
    /// （它们之间是并列还是嵌套？），与其发明一条规则，不如取声明顺序的第一个。
    /// </para>
    /// <para>
    /// 之所以继承非泛型的 <see cref="AttributeProcessor"/> 而不是
    /// <c>AttributeProcessor&lt;PropertyGroupAttribute&gt;</c>：泛型基类会**逐个特性实例**
    /// 回调，于是类型上挂两个分组就会注入两次；而这里的规则是「取第一个」，
    /// 需要一次看到全部。
    /// </para>
    /// </remarks>
    internal sealed class ClassLevelGroupProcessor : AttributeProcessor
    {
        #region Public API

        /// <summary>
        /// 仅当「这个父节点是某个类型的容器、且那个类型上带分组特性」时参与。
        /// </summary>
        /// <param name="parentProperty">父属性。</param>
        /// <param name="member">子成员。</param>
        /// <returns>需要处理返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// 两类容器：**根**（类级特性经构建期收集在根节点上，根就是「这个类型」），
        /// 以及**嵌套层与元素层的复合成员节点**（父字段的声明类型 / 元素类型才是「这个类型」）。
        /// 分组节点、方法节点与反射成员节点都没有成员子节点，天然不参与。
        /// </para>
        /// <para>
        /// 复合容器的类型**从 <see cref="InspectorProperty.Type"/> 读**，绝不读
        /// <c>parentProperty.Attributes</c>——那装的是**字段自己的**特性：字段上写一个
        /// <c>[BoxGroup("X")]</c> 会被误当成这个类型的类级分组、再分发给它的孩子。
        /// </para>
        /// </remarks>
        public override bool CanProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member)
        {
            return TryGetClassLevelGroup(parentProperty, out _);
        }

        /// <summary>
        /// 把类级分组注入（或嵌套）到子成员的特性列表里。
        /// </summary>
        /// <param name="parentProperty">容器节点（根，或嵌套 / 元素层的复合成员节点）。</param>
        /// <param name="member">子成员。</param>
        /// <param name="attributes">子成员的特性列表。</param>
        public override void ProcessChildMemberAttributes(
            InspectorProperty parentProperty,
            MemberInfo member,
            IList<Attribute> attributes)
        {
            if (!TryGetClassLevelGroup(parentProperty, out var classGroup))
            {
                return;
            }

            var hasOwnGroup = false;

            for (var i = 0; i < attributes.Count; i++)
            {
                if (!(attributes[i] is PropertyGroupAttribute own))
                {
                    continue;
                }

                // 成员自己声明了分组：把类级分组当作它的父路径。
                // 用 CloneForPath 而不是直接改 GroupID——克隆会一并保留子类字段
                // （ShowLabel、Label 之类），且不会动到别处共享的那个实例。
                //
                // 不加 return：成员身上**每个**分组特性都要加前缀。只加第一个的话，
                // 第二种会留在类级分组外面，跑出去（多类型改动之后这条才有意义）。
                attributes[i] = own.CloneForPath(
                    classGroup.GroupID + PropertyGroupPath.Separator + own.GroupID);
                hasOwnGroup = true;
            }

            if (hasOwnGroup)
            {
                return;
            }

            // 成员没有自己的分组：整份归入类级分组。
            // 同样要克隆：直接塞同一个实例的话，多个成员会共享它，
            // 后续任何一处改写（如在别处 Combine）都会串到所有人身上。
            attributes.Add(classGroup.CloneForPath(classGroup.GroupID));
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 取这个容器节点上「作为类级分组」生效的那份特性：根读节点特性，复合成员读声明类型。
        /// </summary>
        /// <param name="parentProperty">容器节点。</param>
        /// <param name="group">取到的类级分组；没有时返回 <c>null</c>。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        /// <remarks>
        /// 「只取第一个」在两个来源上都成立：根走 <c>PropertyAttributes.Get&lt;T&gt;</c>（首匹配），
        /// 类型走 <see cref="NestedMemberExpansion.FindClassLevelGroup"/>（反射顺序里的第一个）。
        /// <c>GetCustomAttributes</c> 的顺序不作承诺，因此只承诺「恰好一份」。
        /// </remarks>
        private static bool TryGetClassLevelGroup(InspectorProperty parentProperty, out PropertyGroupAttribute group)
        {
            group = null;

            if (parentProperty == null)
            {
                return false;
            }

            if (parentProperty.Kind == InspectorPropertyKind.Root)
            {
                group = parentProperty.Attributes.Get<PropertyGroupAttribute>();
                return group != null;
            }

            // 嵌套层与元素层的复合容器：类级特性在**容器的类型**上（2026-10-06 起生效）。
            if (parentProperty.Kind == InspectorPropertyKind.Member)
            {
                group = NestedMemberExpansion.FindClassLevelGroup(parentProperty.Type);
                return group != null;
            }

            return false;
        }

        #endregion
    }
}
