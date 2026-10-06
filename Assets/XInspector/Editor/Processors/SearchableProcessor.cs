using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="SearchableAttribute"/>：构建期校验 + 保证特性不写了个寂寞。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>它必须是泛型处理器。</b> 自动接管的判据只认「<c>HandledAttributeType</c> 不为 null」的
    /// 处理器，而 <c>[Searchable]</c> **没有**自己的绘制器（搜索框由宿主绘制器顺带画）——
    /// 用非泛型处理器的话判据看不见它，症状是类型不被接管、搜索静默失效。
    /// </para>
    /// <para>
    /// 三种落点是分开处理的：**集合**要补列表设置（否则整份列表仍由 Unity 画，搜索无从谈起）；
    /// **复合成员**靠 <c>NestedMemberExpansion.ShouldExpand</c> 的特例展开，这里只确认它有子节点；
    /// 其余一律告警——标量、多态引用、反射成员都没有可筛选的东西。
    /// </para>
    /// </remarks>
    internal sealed class SearchableProcessor : AttributeProcessor<SearchableAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property, SearchableAttribute attribute, IList<Attribute> attributes)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null)
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上的 [Searchable] 需要 Unity 的序列化后端，"
                    + "而这是一个 [ShowInInspector] 的只读成员（它不展开子成员），该特性对它无效。");
                return;
            }

            if (CollectionDrawerLayout.CanDraw(serializedProperty))
            {
                // 行级过滤落在集合绘制器里，而那个绘制器由 [ListDrawerSettings] 带出来。
                CollectionDrawerLayout.EnsureListSettings(attributes);
                return;
            }

            if (property.Children.Count > 0)
            {
                return;
            }

            Debug.LogWarning(
                $"[XInspector] 属性「{property.Path}」上的 [Searchable] 只对复合成员与数组/List 有效"
                + $"（{Reason(property)}），该特性已忽略。");
        }

        #endregion

        #region Private Helpers

        /// <summary>说清「为什么没有可筛选的子成员」——两种成因要做的事不一样。</summary>
        /// <param name="property">属性。</param>
        /// <returns>原因文本。</returns>
        /// <remarks>
        /// 多态引用走**与展开判据同一处**的判据（<see cref="NestedMemberExpansion.IsPolymorphicReference"/>）：
        /// 两处各写一遍的话，告警说的与真正发生的事迟早对不上。
        /// </remarks>
        private static string Reason(InspectorProperty property)
        {
            if (NestedMemberExpansion.IsPolymorphicReference(property.Member as FieldInfo))
            {
                return "多态引用（[SerializeReference]）本轮不展开，它的成员进不了树";
            }

            return $"当前是 {property.Type?.Name ?? "未知类型"}，没有可筛选的子成员";
        }

        #endregion
    }
}
