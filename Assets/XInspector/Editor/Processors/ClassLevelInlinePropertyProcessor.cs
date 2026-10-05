using System;
using System.Collections.Generic;
using System.Reflection;

namespace XInspector.Editor
{
    /// <summary>
    /// 把**标在字段声明类型上**的 <see cref="InlinePropertyAttribute"/> 注入到成员自己身上。
    /// <para>
    /// 官方语义：<c>[InlineProperty]</c> 标在类上时，该类型的字段一律内联。判据因此落在
    /// **字段的声明类型**（<c>field.FieldType</c>）上，而不是被检视类型——本包的类级通道
    /// （类型特性落在根节点上）管的是被检视类型本身，与这里不是一回事。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 非泛型处理器（照 <see cref="ClassLevelGroupProcessor"/> 的先例）：判据要读的是
    /// <see cref="MemberInfo"/> 上的类型信息，泛型基类按「父属性有没有该特性」判断，够不着。
    /// <see cref="AttributeProcessor.HandledAttributeType"/> 保持 <c>null</c>——本处理器不由
    /// 某个特性类型驱动（也正因此不会进第二趟：第二趟的判据是「派生自
    /// <c>PropertyGroupAttribute</c>」）。
    /// </para>
    /// <para>
    /// <b>注入的是新实例。</b> 类型上那一份会被该类型的**所有**字段共享，直接塞进去就是分组族
    /// 当年那个「改一处串一片」的隐患——<see cref="InlinePropertyAttribute"/> 不是分组特性、
    /// 没有 <c>CloneForPath</c>，在这里照它的可写属性复制一份。
    /// </para>
    /// </remarks>
    internal sealed class ClassLevelInlinePropertyProcessor : AttributeProcessor
    {
        #region Public API

        /// <summary>
        /// 仅当父节点是根、且该成员是**声明类型带 <see cref="InlinePropertyAttribute"/> 的字段**
        /// 时参与。
        /// </summary>
        /// <param name="parentProperty">父属性（类级特性经构建期放在根节点上）。</param>
        /// <param name="member">子成员。</param>
        /// <returns>需要处理返回 <c>true</c>。</returns>
        /// <remarks>
        /// 只看字段：属性没有「声明类型的内联」这回事（反射后端也没有子字段可摊平）。
        /// </remarks>
        public override bool CanProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member)
        {
            return parentProperty != null
                && parentProperty.Kind == InspectorPropertyKind.Root
                && member is FieldInfo field
                && FindDeclared(field.FieldType) != null;
        }

        /// <summary>
        /// 把声明类型上的那份复制到成员的特性列表里；成员自己已经标了就不重复注入。
        /// </summary>
        /// <param name="parentProperty">父属性。</param>
        /// <param name="member">子成员。</param>
        /// <param name="attributes">子成员的特性列表。</param>
        public override void ProcessChildMemberAttributes(
            InspectorProperty parentProperty,
            MemberInfo member,
            IList<Attribute> attributes)
        {
            if (!(member is FieldInfo field))
            {
                return;
            }

            for (var i = 0; i < attributes.Count; i++)
            {
                if (attributes[i] is InlinePropertyAttribute)
                {
                    // 自己标了：AllowMultiple = false，再注入一份会多配一格绘制器。
                    return;
                }
            }

            var source = FindDeclared(field.FieldType);
            if (source == null)
            {
                return;
            }

            attributes.Add(new InlinePropertyAttribute { LabelWidth = source.LabelWidth });
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 取声明类型上**自己那份** <see cref="InlinePropertyAttribute"/>。
        /// </summary>
        /// <param name="fieldType">字段的声明类型。</param>
        /// <returns>找到的特性；没有时返回 <c>null</c>。</returns>
        /// <remarks>
        /// 不上溯基类：官方的 <c>Inherited = false</c>——标记写在哪一层，哪一层才内联。
        /// </remarks>
        private static InlinePropertyAttribute FindDeclared(Type fieldType)
        {
            return fieldType?.GetCustomAttribute<InlinePropertyAttribute>(inherit: false);
        }

        #endregion
    }
}
