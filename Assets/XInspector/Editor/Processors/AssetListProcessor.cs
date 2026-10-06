using System;
using System.Collections.Generic;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="AssetListAttribute"/>：构建期定形态、建模型，并保证列表形态有绘制器接手。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>它必须是泛型处理器。</b> 自动接管的判据只认「<c>HandledAttributeType</c> 不为 null」的
    /// 处理器（非泛型的那个属性恒为 <c>null</c>）——用非泛型处理器的话判据看不见它，
    /// 症状是类型不被接管、特性静默失效（与 <see cref="TableListProcessor"/> 同一条理由）。
    /// </para>
    /// <para>
    /// <b>列表形态要注入一份 <see cref="ListDrawerSettingsAttribute"/></b>：列表的画法与增删由
    /// 那一格绘制器拥有（本特性只贡献「怎么画一行」）。注入的是新实例，绝不共享。
    /// 单元素形态**不注入**——它有自己那一格替换型绘制器。
    /// </para>
    /// <para>
    /// <b>与 <see cref="TableListAttribute"/> 同现时本特性让位</b>（只限列表形态）：
    /// 表格保持既有行为，这里告警一次。这是本包自定规则（官方未写明）——选「让位」而不是
    /// 「抢过来」，是为了不动既有表格字段的行为。
    /// </para>
    /// </remarks>
    internal sealed class AssetListProcessor : AttributeProcessor<AssetListAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property, AssetListAttribute attribute, IList<Attribute> attributes)
        {
            var array = property.ValueEntry?.SerializedProperty;
            var listForm = AssetListModelBuilder.IsListForm(array);

            if (listForm && property.Attributes.Has<TableListAttribute>())
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上同时标了 [AssetList] 与 [TableList]：" +
                    "[AssetList] 已让位，该字段按表格绘制（表格行为保持不变）。" +
                    "想要资产列表就去掉 [TableList]。");
                return;
            }

            var model = AssetListModelBuilder.TryBuild(array, property.Type, attribute, out var reason);

            if (model == null)
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上的 [AssetList] 无法生效：{reason}。" +
                    (listForm ? "该字段按普通列表绘制。" : string.Empty));

                // 是数组 / List 就让本包的集合容器接着画（照 TableListProcessor 的先例：
                // 特性不生效也要保证「有人画」）；标量字段没什么可注入的，原样放行。
                if (listForm)
                {
                    CollectionDrawerLayout.EnsureListSettings(attributes);
                }

                return;
            }

            if (model.Form == AssetListForm.List)
            {
                // 列表形态的绘制与增删都属于集合容器——不注入就没人画（静默失效）。
                CollectionDrawerLayout.EnsureListSettings(attributes);
            }

            var state = property.State.GetOrCreate<AssetListModel>();
            state.Form = model.Form;
            state.ElementType = model.ElementType;
            state.Folders = model.Folders;
            state.TypeFilter = model.TypeFilter;
            state.NamePrefix = model.NamePrefix;
        }

        #endregion
    }
}
