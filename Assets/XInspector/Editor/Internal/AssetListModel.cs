using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 资产列表的两种形态——判定在**构建期**做一次，绘制期只读。
    /// </summary>
    /// <remarks>
    /// 官方对这个特性说明了两种用法（列表 / 数组，与单个 Unity 对象字段），且明说**行为不同**；
    /// 两种形态各自有绘制器（列表形态由集合容器分流，单元素形态是替换型绘制器）。
    /// </remarks>
    internal enum AssetListForm
    {
        /// <summary>单个 Unity 对象字段：预览块 + 原生对象字段 + 选择按钮。</summary>
        Single,

        /// <summary>数组 / <c>List&lt;T&gt;</c>：缩略图行 + 增删 + 「选择」+ 拖放。</summary>
        List,
    }

    /// <summary>
    /// <c>[AssetList]</c> 的构建期模型：形态、元素类型与过滤条件。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="TableModel"/> 同款：**构建期建一次，绘制期只读**。字符串（拆好并归一化的
    /// 目录、预先拼好的类型过滤串）都在这里算完——绘制路径禁字符串拼接。
    /// </remarks>
    internal sealed class AssetListModel
    {
        /// <summary>形态。</summary>
        public AssetListForm Form;

        /// <summary>元素类型（单元素形态就是字段自己的类型）。</summary>
        public Type ElementType;

        /// <summary>拆好并归一化的目录；空数组表示整个工程。</summary>
        public string[] Folders;

        /// <summary>预先拼好的类型过滤串（<c>t:Xxx</c>）；<c>null</c> 表示不加。</summary>
        public string TypeFilter;

        /// <summary>资产名前缀；空白表示不过滤。</summary>
        public string NamePrefix;
    }

    /// <summary>
    /// <c>[AssetList]</c> 的形态判定与模型构建——构建期的唯一入口。
    /// </summary>
    internal static class AssetListModelBuilder
    {
        #region Public API

        /// <summary>
        /// 这个字段够不够格用资产列表。
        /// </summary>
        /// <param name="property">它的序列化属性；反射成员传 <c>null</c>。</param>
        /// <returns>是数组 / <c>List&lt;T&gt;</c> 返回 <c>true</c>（单元素形态在 <see cref="TryBuild"/> 里另判）。</returns>
        /// <remarks>与集合绘制器的降级判据**同源**（<c>CollectionDrawerLayout.CanDraw</c>），不另写一份。</remarks>
        public static bool IsListForm(SerializedProperty property)
        {
            return CollectionDrawerLayout.CanDraw(property);
        }

        /// <summary>
        /// 尝试为 <c>[AssetList]</c> 建模型。
        /// </summary>
        /// <param name="property">字段的序列化属性（反射成员传 <c>null</c>）。</param>
        /// <param name="declaredType">字段的声明类型。</param>
        /// <param name="attribute">特性实例。</param>
        /// <param name="reason">不成立的原因（中文，供构建期告警）；成立时为 <c>null</c>。</param>
        /// <returns>模型；不成立时返回 <c>null</c>。</returns>
        /// <remarks>
        /// <para>
        /// 「不成立」有五种：没有序列化后端、既不是对象引用也不是数组 / <c>List</c>、
        /// 取不到元素类型、元素类型是**接口**（接口没有「可拖入的资产」这回事）、
        /// 元素 / 字段类型不是 <see cref="UnityEngine.Object"/> 派生。
        /// 抽象类**放行**——但 <c>t:</c> 收窄可能一个都搜不到，菜单会空并告警（见 <c>AssetListQuery</c>）。
        /// </para>
        /// <para>
        /// 目录与过滤串在这里算完（构建期）：<see cref="AssetListPaths.Normalize"/> 负责
        /// 「<c>|</c> 分隔 + 前导斜杠兼容」，<see cref="AssetListFilter.TypeFilter"/> 负责
        /// 「<c>t:Xxx</c>」。
        /// </para>
        /// </remarks>
        public static AssetListModel TryBuild(
            SerializedProperty property, Type declaredType, AssetListAttribute attribute, out string reason)
        {
            reason = null;

            if (declaredType == null)
            {
                reason = "取不到字段类型";
                return null;
            }

            if (property == null)
            {
                reason = "它没有 Unity 的序列化后端";
                return null;
            }

            AssetListForm form;
            Type elementType;

            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                form = AssetListForm.Single;
                elementType = declaredType;
            }
            else if (CollectionDrawerLayout.CanDraw(property))
            {
                form = AssetListForm.List;
                elementType = CollectionElement.TypeOf(declaredType);

                if (elementType == null)
                {
                    reason = "取不到元素类型（只认数组与 List<T>）";
                    return null;
                }

                if (elementType.IsInterface)
                {
                    reason = $"元素类型「{elementType.Name}」是接口——接口没有「可拖入的资产」这回事";
                    return null;
                }
            }
            else
            {
                reason = "只对数组 / List<T> 或单个 Unity 对象字段有效";
                return null;
            }

            if (!typeof(UnityEngine.Object).IsAssignableFrom(elementType))
            {
                reason = form == AssetListForm.Single
                    ? $"字段类型「{elementType.Name}」不是 UnityEngine.Object 派生"
                    : $"元素类型「{elementType.Name}」不是 UnityEngine.Object 派生";
                return null;
            }

            return new AssetListModel
            {
                Form = form,
                ElementType = elementType,
                Folders = AssetListPaths.Normalize(attribute.Path),
                TypeFilter = AssetListFilter.TypeFilter(elementType),
                NamePrefix = attribute.AssetNamePrefix,
            };
        }

        #endregion
    }
}
