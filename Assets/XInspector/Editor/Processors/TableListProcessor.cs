using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="TableListAttribute"/>：构建期为表格建列模型，并保证有绘制器接手。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>它必须是泛型处理器。</b> 自动接管的判据只认「<c>HandledAttributeType</c> 不为 null」的
    /// 处理器（非泛型处理器的那个属性恒为 <c>null</c>），而 <c>[TableList]</c> **没有**自己的
    /// 绘制器——用非泛型处理器的话判据看不见它，症状是类型不被接管、表格静默失效。
    /// </para>
    /// <para>
    /// <b>为什么需要注入 <see cref="ListDrawerSettingsAttribute"/></b>：官方样例显示
    /// <c>[TableList]</c> 是**单独**用在字段上的，而集合绘制由后者那一格绘制器拥有——
    /// 不注入就没人画这张表（静默失效）。注入的是一份**新实例**（绝不共享，与分组族同一纪律）。
    /// </para>
    /// </remarks>
    internal sealed class TableListProcessor : AttributeProcessor<TableListAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property, TableListAttribute attribute, IList<Attribute> attributes)
        {
            var field = property.Member as FieldInfo;
            var model = TableModelBuilder.TryBuild(
                field, property.ValueEntry?.SerializedProperty, attribute, out var reason);

            if (model == null)
            {
                // 构建期说清楚为什么，并仍然保证有绘制器——否则这个字段会因为「没人画」而消失
                // 或退回完全无关的形态，那样告警就等于没说。
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上的 [TableList] 无法生效：{reason}。" +
                    "该字段按普通列表绘制。");
                CollectionDrawerLayout.EnsureListSettings(attributes);
                return;
            }

            var state = property.State.GetOrCreate<TableModel>();
            state.Columns = model.Columns;
            state.ShowIndexLabels = model.ShowIndexLabels;
            state.AlwaysExpanded = model.AlwaysExpanded;

            CollectionDrawerLayout.EnsureListSettings(attributes);
        }

        #endregion
    }
}
