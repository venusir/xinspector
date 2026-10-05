using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 嵌套 `[Serializable]` 类型的**按需展开**：什么时候把它的成员变成真节点，
    /// 以及按点分路径解析嵌套字段。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>按需展开是本轮的安全阀。</b> 只给「用到了本包」的嵌套类型展开——判据是
    /// <see cref="ShouldExpand"/>：嵌套成员里有任何一个带**本包支持的特性**。其余情形整份交回
    /// <c>UnityFallbackDrawer</c>——「没用到本包的类型外观不变」这条契约不能破。
    /// </para>
    /// <para>
    /// <b><c>[InlineProperty]</c> 那条触发留给下一批。</b> 它今天还是**替换型**绘制器
    /// （自己迭代 <c>SerializedProperty</c> 画子字段、不调下一个），此刻让节点展开就会
    /// 画两遍或一遍都不画——两件事必须同批做：绘制器改成「画标签 + 调下一个」、
    /// 判据再加一条「字段/声明类型标了 <see cref="InlinePropertyAttribute"/>」。
    /// </para>
    /// <para>
    /// <b>只做固定形状的那一半。</b> 数组与列表不展开（元素个数随时可变，与「树的形状在构建
    /// 结束后冻结」正面冲突，那是元素节点化的领域）；多态引用（<c>[SerializeReference]</c>）
    /// 也不展开——那是 L7 那条产品线。
    /// </para>
    /// </remarks>
    internal static class NestedMemberExpansion
    {
        #region Private Fields

        /// <summary>递归扫描的深度上限——挡住自引用类型（<c>class Node { Node next; }</c>）。</summary>
        /// <remarks>
        /// 与 <c>[InlineEditor]</c> 的递归上限同档取 <c>4</c>：本包自定值，写进文档。
        /// 序列化属性那条路 Unity 自己会截断，纯反射这条路不会，故必须自己挡。
        /// </remarks>
        private const int MaxDepth = 4;

        #endregion

        #region Public API

        /// <summary>
        /// 这个类型（作为字段的**声明类型**）会不会被按需展开——**自动接管的判据**用它。
        /// </summary>
        /// <param name="declaredType">字段的声明类型。</param>
        /// <returns>会被展开返回 <c>true</c>。</returns>
        /// <remarks>
        /// 与 <see cref="ShouldExpand"/> 的差别：这条**只看类型**（没有节点、没有序列化属性），
        /// 供 <c>XInspectorUsageDetection</c> 在「这个类型用没用到本插件」那一问里使用。
        /// 两条判据必须指向同一批类型——判据看不见的展开就是静默失效。
        /// </remarks>
        public static bool WouldExpand(Type declaredType)
        {
            if (declaredType == null || declaredType.IsArray || declaredType.IsPrimitive || declaredType.IsEnum)
            {
                return false;
            }

            if (declaredType == typeof(string) || declaredType == typeof(object))
            {
                return false;
            }

            return HasSupportedField(declaredType, new HashSet<Type>(), 0);
        }

        /// <summary>
        /// 这个成员该不该展开成子节点。
        /// </summary>
        /// <param name="member">成员节点（建树期刚建好、还没挂链）。</param>
        /// <param name="property">它的序列化属性。</param>
        /// <returns>该展开返回 <c>true</c>。</returns>
        public static bool ShouldExpand(InspectorProperty member, SerializedProperty property)
        {
            if (!IsCompositeCandidate(member, property))
            {
                return false;
            }

            // 嵌套成员里有本包支持的特性——递归判一遍（孙辈也带特性时，得连展开两层）。
            // **判据只看成员级特性，不看嵌套类型的类级特性**：后者本轮不生效（没有收集它的
            // 地方），算进来等于「为了一个不画东西的特性而展开」。
            return HasSupportedMember(property, member.Type, 0);
        }

        /// <summary>
        /// 该成员够不够格当「复合成员」：真的复合类型、非数组、非多态引用、有可见子字段。
        /// </summary>
        /// <param name="member">成员节点。</param>
        /// <param name="property">它的序列化属性。</param>
        /// <returns>够格返回 <c>true</c>。</returns>
        private static bool IsCompositeCandidate(InspectorProperty member, SerializedProperty property)
        {
            if (member == null || property == null || member.Type == null)
            {
                return false;
            }

            // [InlineProperty] 那条路留到下一批：它今天还是**替换型**绘制器（自己迭代
            // SerializedProperty 画子字段、不调下一个）。此刻让节点展开会得到一个
            // 「子节点存在但永不绘制」的半成品——两件事必须同批做。
            if (member.Attributes.Has<InlinePropertyAttribute>() ||
                member.Type.IsDefined(typeof(InlinePropertyAttribute), false))
            {
                return false;
            }

            // 要求 Generic：向量之类也有可见子级，但它们由原生控件整块画（本包不拆）。
            if (property.propertyType != SerializedPropertyType.Generic)
            {
                return false;
            }

            // 数组/列表是元素节点化的领域（动态形状），不在这里展开。
            if (property.isArray || !property.hasVisibleChildren)
            {
                return false;
            }

            // [SerializeReference] 的多态引用是 L7 那条产品线——别在这里开半扇门。
            return !member.Type.IsDefined(typeof(SerializeReference), true);
        }

        /// <summary>
        /// 按**点分路径**逐层解析字段（嵌套成员用；现成的那条只认顶层名）。
        /// </summary>
        /// <param name="rootType">起点类型（被检视对象的类型）。</param>
        /// <param name="path">序列化路径，如 <c>nested.field</c>。</param>
        /// <returns>字段；解析不到时返回 <c>null</c>。</returns>
        /// <remarks>
        /// 逐段下钻：每段在该层类型上按 <c>DeclaredOnly</c> 逐级上溯找同名字段，再进它的
        /// <see cref="FieldInfo.FieldType"/>。解析不到不抛——子节点照建，只是拿不到特性
        /// （与 <c>m_Script</c> 那类 Unity 注入成员的处置一致）。
        /// </remarks>
        public static FieldInfo ResolveField(Type rootType, string path)
        {
            if (rootType == null || string.IsNullOrEmpty(path))
            {
                return null;
            }

            var current = rootType;
            var field = (FieldInfo)null;
            var start = 0;

            while (start < path.Length)
            {
                var separator = path.IndexOf('.', start);
                var name = separator < 0 ? path.Substring(start) : path.Substring(start, separator - start);

                field = FindDeclaredField(current, name);
                if (field == null)
                {
                    return null;
                }

                current = field.FieldType;

                if (separator < 0)
                {
                    break;
                }

                start = separator + 1;
            }

            return field;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 序列化属性这一层（含更深层）里有没有**本包支持的特性**。
        /// </summary>
        /// <param name="property">复合成员的序列化属性。</param>
        /// <param name="declaringType">它的声明类型。</param>
        /// <param name="depth">当前深度（挡自引用类型）。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        /// <remarks>
        /// 逐个子字段用路径解析器拿 <see cref="FieldInfo"/>（拿不到就跳过——那条子路径不是
        /// 普通字段），再用 <see cref="XInspectorUsageDetection.HasSupportedAttribute"/> 判——
        /// 「本包支持的特性」只有那一个定义处。
        /// </remarks>
        private static bool HasSupportedMember(SerializedProperty property, Type declaringType, int depth)
        {
            if (declaringType == null || depth > MaxDepth)
            {
                return false;
            }

            var child = property.Copy();
            var childDepth = property.depth + 1;
            var next = child.NextVisible(true) && child.depth == childDepth;

            while (next)
            {
                var field = FindDeclaredField(declaringType, child.name);

                if (field != null)
                {
                    if (XInspectorUsageDetection.HasSupportedAttribute(field.GetCustomAttributes(true)))
                    {
                        return true;
                    }

                    // 再往下一层看：孙辈带特性时，这一层也得展开，否则它进不了树。
                    if (child.hasVisibleChildren && HasSupportedMember(child, field.FieldType, depth + 1))
                    {
                        return true;
                    }
                }

                next = child.NextVisible(false) && child.depth == childDepth;
            }

            return false;
        }

        /// <summary>
        /// 纯反射版：这个类型（含更深层）的**可序列化字段**里有没有本包支持的特性。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <param name="visited">已访问的类型（挡环形引用）。</param>
        /// <param name="depth">当前深度。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        private static bool HasSupportedField(Type type, HashSet<Type> visited, int depth)
        {
            if (type == null || depth > MaxDepth || !visited.Add(type))
            {
                return false;
            }

            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (var field in current.GetFields(Flags))
                {
                    if (!IsSerializableField(field))
                    {
                        continue;
                    }

                    if (XInspectorUsageDetection.HasSupportedAttribute(field.GetCustomAttributes(true)))
                    {
                        return true;
                    }

                    if (HasSupportedField(field.FieldType, visited, depth + 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>该字段算不算「会被 Unity 序列化」（展开只看这些字段的特性）。</summary>
        /// <param name="field">字段。</param>
        /// <returns>算返回 <c>true</c>。</returns>
        private static bool IsSerializableField(FieldInfo field)
        {
            if (field.IsStatic || field.IsNotSerialized || field.IsDefined(typeof(HideInInspector), true))
            {
                return false;
            }

            return field.IsPublic || field.IsDefined(typeof(SerializeField), true);
        }

        /// <summary>在该类型及其基类上找同名字段（<c>DeclaredOnly</c> 逐级上溯）。</summary>
        /// <param name="type">起始类型。</param>
        /// <param name="name">字段名。</param>
        /// <returns>字段；找不到返回 <c>null</c>。</returns>
        private static FieldInfo FindDeclaredField(Type type, string name)
        {
            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                var field = current.GetField(name, Flags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        #endregion
    }
}
