using System;
using System.Collections.Generic;
using System.Reflection;

namespace XInspector.Editor
{
    /// <summary>
    /// **类型选择器一族**（<c>[TypeDrawerSettings]</c> 与 <c>[PolymorphicDrawerSettings]</c>）的
    /// 误用扫描：标了特性却**没有变成节点**的字段，构建期告警一次。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么不能在绘制器里：</b> 没加 <c>[SerializeReference]</c> 的引用字段**根本没有节点**
    /// （不进序列化数据 ⇒ 成员收集看不见它 ⇒ 绘制器永远跑不到）——放绘制器等于这条告警永远不响。
    /// </para>
    /// <para>
    /// <b>判据看结果、不看猜测</b>：问「这个字段有没有变成节点」。这一条自动罩住**所有**
    /// 不建节点的原因（没加 <c>[SerializeReference]</c>、非序列化字段、<c>[HideInInspector]</c>……），
    /// 不必逐条列举——列举的清单迟早会漏。
    /// </para>
    /// <para>
    /// <b>作用域只到最外层类型</b>（含基类链）：与「类级特性只在最外层类型上收集」同款口径；
    /// 嵌套层/元素层里的误用不报（那里面的字段本来就另有收集规则）。
    /// </para>
    /// <para>
    /// <b>「有节点但不是托管引用」（如标在 <c>int</c> 上）不在这里管</b>——那种字段绘制器跑得到，
    /// 由绘制器的判据档位告警（见 <c>PolymorphicRow.Disposition</c> 的 <c>FallbackNotBacked</c>）。
    /// </para>
    /// <para>
    /// <b>第三支与前两支的判据不同：<c>[TypeSelectorSettings]</c> 标了、而两个选择器特性一个都没有</b>
    /// ——它**与「有没有节点」无关**：没有选择器特性就没有任何本包选择器被渲染，有节点也一样是 no-op。
    /// 三支的 key 后缀各不相同（<c>:type</c> / <c>:poly</c> / <c>:settings</c>）。
    /// </para>
    /// </remarks>
    internal static class TypeSelectorMisuse
    {
        /// <summary>
        /// 扫最外层类型（含基类链）上标了这两个特性的字段，没节点的逐个告警。
        /// </summary>
        /// <param name="targetType">被检视的类型。</param>
        /// <param name="members">已收齐的顶层成员（序列化 + 反射 + 方法）。</param>
        /// <param name="root">根节点（告警的挂点，一次建树一条）。</param>
        public static void WarnAboutUnreachableFields(
            Type targetType, IReadOnlyList<InspectorProperty> members, InspectorProperty root)
        {
            for (var current = targetType; current != null && current != typeof(object); current = current.BaseType)
            {
                var fields = current.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                for (var i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    var typeSelector = field.IsDefined(typeof(TypeDrawerSettingsAttribute), true);
                    var polySelector = field.IsDefined(typeof(PolymorphicDrawerSettingsAttribute), true);

                    if (!typeSelector && !polySelector)
                    {
                        // **第三支**：设置标了、可它配的两个选择器一个都没有——判据与「有没有节点」
                        // 无关（没有选择器特性就没有任何本包选择器被渲染，有节点也一样是 no-op）。
                        if (field.IsDefined(typeof(TypeSelectorSettingsAttribute), true))
                        {
                            DrawerWarnings.Once(
                                root,
                                nameof(TypeSelectorMisuse) + ":" + current.Name + "." + field.Name + ":settings",
                                BuildSettingsText(current, field));
                        }

                        continue;
                    }

                    if (HasNode(members, field))
                    {
                        continue;
                    }

                    var key = nameof(TypeSelectorMisuse) + ":" + current.Name + "." + field.Name +
                              (typeSelector ? ":type" : ":poly");

                    DrawerWarnings.Once(root, key, BuildText(current, field, typeSelector));
                }
            }
        }

        /// <summary>这个字段有没有变成节点（按 <see cref="MemberInfo"/> 相等判）。</summary>
        /// <param name="members">顶层成员。</param>
        /// <param name="field">字段。</param>
        /// <returns>有节点返回 <c>true</c>。</returns>
        private static bool HasNode(IReadOnlyList<InspectorProperty> members, FieldInfo field)
        {
            for (var i = 0; i < members.Count; i++)
            {
                if (Equals(members[i].Member, field))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>告警文案：能点名「要加 <c>[SerializeReference]</c>」的那一支单独说。</summary>
        /// <param name="declaringType">声明类型。</param>
        /// <param name="field">字段。</param>
        /// <param name="typeSelector">是 <c>[TypeDrawerSettings]</c>（否则是 <c>[PolymorphicDrawerSettings]</c>）。</param>
        /// <returns>文案。</returns>
        private static string BuildText(Type declaringType, FieldInfo field, bool typeSelector)
        {
            var label = declaringType.Name + "." + field.Name;
            var attribute = typeSelector ? "TypeDrawerSettings" : "PolymorphicDrawerSettings";

            if (typeSelector)
            {
                if (field.FieldType == typeof(Type) && !NestedMemberExpansion.IsPolymorphicReference(field))
                {
                    return $"[XInspector] 字段「{label}」标了 [{attribute}]，但它没有进 Inspector：" +
                           "裸的 System.Type 字段不在序列化数据里——**要加 [SerializeReference]**" +
                           "（写成 `[SerializeReference] public System.Type …`），类型选择器才有落点。";
                }

                return $"[XInspector] 字段「{label}」标了 [{attribute}]，但它没有进序列化数据" +
                       "（也就不进 Inspector）——该特性不会生效。";
            }

            // 多态选择器管的是**多态引用**：引用类型字段 + [SerializeReference]。
            if (!field.FieldType.IsValueType &&
                !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
            {
                return $"[XInspector] 字段「{label}」标了 [{attribute}]，但它没有进 Inspector：" +
                       "多态引用要写成 `[SerializeReference]`——**要加 [SerializeReference]**" +
                       "（`[SerializeReference] public IShape …`），本包的选择器才有落点。";
            }

            return $"[XInspector] 字段「{label}」标了 [{attribute}]，但它没有进序列化数据" +
                   "（也就不进 Inspector）——该特性不会生效。";
        }

        /// <summary>第三支的文案：设置标了、可它配的选择器一个都没有。</summary>
        /// <param name="declaringType">声明类型。</param>
        /// <param name="field">字段。</param>
        /// <returns>文案。</returns>
        private static string BuildSettingsText(Type declaringType, FieldInfo field)
        {
            var label = declaringType.Name + "." + field.Name;

            return $"[XInspector] 字段「{label}」标了 [TypeSelectorSettings]，" +
                   "但它配的是**本包自绘的类型选择器**——这一格上既没有 [TypeDrawerSettings] 也没有 " +
                   "[PolymorphicDrawerSettings]，没有任何选择器由本包渲染，该特性不会生效。";
        }
    }
}
