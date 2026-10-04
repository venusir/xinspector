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
    /// <b>语义。</b> 类级分组恒在最外层：
    /// </para>
    /// <list type="bullet">
    /// <item>成员**没有**自己的分组 → 直接归入类级分组；</item>
    /// <item>成员**有**自己的分组 → 把它的路径改写为「类级路径 / 原路径」，
    /// 于是它嵌在类级分组里面，而不是与之并列。</item>
    /// </list>
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
        /// 仅当父节点是**根**、且根上带分组特性时参与。
        /// </summary>
        /// <param name="parentProperty">父属性。</param>
        /// <param name="member">子成员。</param>
        /// <returns>需要处理返回 <c>true</c>。</returns>
        /// <remarks>
        /// 限定为根是有意的：类级特性经构建期放在根节点上，而根就是「这个类型」。
        /// 分组节点不会带类型特性，故不必考虑更深的情况。
        /// </remarks>
        public override bool CanProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member)
        {
            return parentProperty != null
                && parentProperty.Kind == InspectorPropertyKind.Root
                && parentProperty.Attributes.Has<PropertyGroupAttribute>();
        }

        /// <summary>
        /// 把类级分组注入（或嵌套）到子成员的特性列表里。
        /// </summary>
        /// <param name="parentProperty">根属性。</param>
        /// <param name="member">子成员。</param>
        /// <param name="attributes">子成员的特性列表。</param>
        public override void ProcessChildMemberAttributes(
            InspectorProperty parentProperty,
            MemberInfo member,
            IList<Attribute> attributes)
        {
            var classGroup = parentProperty.Attributes.Get<PropertyGroupAttribute>();
            if (classGroup == null)
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
    }
}
