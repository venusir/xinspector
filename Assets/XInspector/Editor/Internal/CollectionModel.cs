using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 表格的一列：元素类型里的一个成员。
    /// </summary>
    internal struct TableColumn
    {
        /// <summary>成员名——<c>FindPropertyRelative</c> 的相对路径。</summary>
        public string Name;

        /// <summary>列头标签（构建期建好，绘制期复用，不每帧新建）。</summary>
        public GUIContent Label;

        /// <summary>列宽（像素）；<c>0</c> 表示弹性（剩余宽度均分）。</summary>
        public float Width;
    }

    /// <summary>
    /// 表格模型：**构建期**建一次，绘制期只读。
    /// </summary>
    /// <remarks>
    /// 反射只发生在这里（本仓硬规则：绘制路径不反射）。模型挂在 <see cref="PropertyState"/> 上，
    /// 随树重建而重建。
    /// </remarks>
    internal sealed class TableModel
    {
        /// <summary>列，按元素类型的声明顺序（基类在前）。</summary>
        public TableColumn[] Columns;

        /// <summary>是否画最左的序号列。</summary>
        public bool ShowIndexLabels;

        /// <summary>是否恒展开（不画折叠头）。</summary>
        public bool AlwaysExpanded;
    }

    /// <summary>
    /// 表格模型的构建：从元素类型反射出列，并用第一个元素的**序列化顺序**校一遍。
    /// </summary>
    internal static class TableModelBuilder
    {
        #region Public API

        /// <summary>
        /// 尝试为表格建模型。
        /// </summary>
        /// <param name="field">列表字段（提供元素类型）。</param>
        /// <param name="array">列表的序列化属性（提供序列化顺序；空列表时传什么结果一样）。</param>
        /// <param name="attribute">表格特性。</param>
        /// <param name="reason">失败原因（中文，供构建期告警）。</param>
        /// <returns>模型；不成立时返回 <c>null</c>。</returns>
        /// <remarks>
        /// 「不成立」只有两种：元素类型不是数组 / <c>List&lt;T&gt;</c>，或元素类型没有可成列的
        /// 可序列化成员（<c>List&lt;int&gt;</c>、<c>string[]</c> 属于后者）。两者都退回普通列表绘制。
        /// </remarks>
        public static TableModel TryBuild(
            FieldInfo field, SerializedProperty array, TableListAttribute attribute, out string reason)
        {
            reason = null;

            var elementType = ElementTypeOf(field?.FieldType);
            if (elementType == null)
            {
                reason = "只对数组与 List<T> 有效";
                return null;
            }

            var fields = CollectColumns(elementType);
            if (fields.Count == 0)
            {
                reason = $"元素类型 {elementType.Name} 没有可成列的可序列化成员";
                return null;
            }

            OrderBySerialization(fields, array, elementType);

            var columns = new TableColumn[fields.Count];
            for (var i = 0; i < fields.Count; i++)
            {
                var width = fields[i].GetCustomAttribute<TableColumnWidthAttribute>(true);
                columns[i] = new TableColumn
                {
                    Name = fields[i].Name,
                    Label = new GUIContent(fields[i].Name),
                    Width = width?.Width ?? 0f,
                };
            }

            return new TableModel
            {
                Columns = columns,
                ShowIndexLabels = attribute.ShowIndexLabels,
                AlwaysExpanded = attribute.AlwaysExpanded,
            };
        }

        #endregion

        #region Private Helpers

        /// <summary>取集合的元素类型；不是数组 / <c>List&lt;T&gt;</c> 时返回 <c>null</c>。</summary>
        /// <param name="collectionType">列表字段的声明类型。</param>
        /// <returns>元素类型；不适用时 <c>null</c>。</returns>
        /// <remarks>
        /// 用反射看声明类型而不是序列化属性：Unity 在若干语境下把 <c>string</c> 也算作 <c>isArray</c>，
        /// 而元素类型只有声明类型说得清。
        /// </remarks>
        private static Type ElementTypeOf(Type collectionType)
        {
            if (collectionType == null)
            {
                return null;
            }

            if (collectionType.IsArray)
            {
                return collectionType.GetElementType();
            }

            if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return collectionType.GetGenericArguments()[0];
            }

            return null;
        }

        /// <summary>
        /// 收集可成列的成员：可序列化的字段，排除静态、<c>[HideInInspector]</c>、
        /// <c>[NonSerialized]</c> 与 <c>[HideInTables]</c>；逐层上溯、基类在前、层内按元数据令牌。
        /// </summary>
        /// <param name="elementType">元素类型。</param>
        /// <returns>字段列表。</returns>
        private static List<FieldInfo> CollectColumns(Type elementType)
        {
            // 逐层上溯：基类的列排在前（与 Unity 的序列化顺序一致）。
            var layers = new List<Type>();
            for (var current = elementType; current != null && current != typeof(object); current = current.BaseType)
            {
                layers.Add(current);
            }

            layers.Reverse();

            var fields = new List<FieldInfo>();

            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i].GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                Array.Sort(layer, CompareByMetadataToken);

                for (var f = 0; f < layer.Length; f++)
                {
                    if (IsColumn(layer[f]))
                    {
                        fields.Add(layer[f]);
                    }
                }
            }

            return fields;
        }

        /// <summary>该字段能不能成列。</summary>
        /// <param name="field">字段。</param>
        /// <returns>能成列返回 <c>true</c>。</returns>
        private static bool IsColumn(FieldInfo field)
        {
            if (field.IsStatic || field.IsNotSerialized || field.IsDefined(typeof(HideInInspector), true))
            {
                return false;
            }

            if (field.IsDefined(typeof(HideInTablesAttribute), true))
            {
                return false;
            }

            return field.IsPublic || field.IsDefined(typeof(SerializeField), true);
        }

        /// <summary>
        /// 按**序列化顺序**重排：以第一个元素的实际子属性为准，反射多出来的排在后面。
        /// </summary>
        /// <param name="fields">反射得到的字段。</param>
        /// <param name="array">列表的序列化属性（没有元素时跳过）。</param>
        /// <param name="elementType">元素类型（只用于注释与将来的兜底）。</param>
        /// <remarks>
        /// 顺序以序列化说了算——这与建树那条「顺序是契约」同款（反射顺序在多数类型上与它一致，
        /// 但 <c>[FormerlySerializedAs]</c> 这类会让两者分家）。空列表时无从核对，用反射顺序。
        /// </remarks>
        private static void OrderBySerialization(List<FieldInfo> fields, SerializedProperty array, Type elementType)
        {
            if (array == null || array.arraySize == 0)
            {
                return;
            }

            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            var element = array.GetArrayElementAtIndex(0);
            var child = element.Copy();
            var depth = element.depth + 1;
            var next = child.NextVisible(true) && child.depth == depth;
            var index = 0;

            while (next)
            {
                order[child.name] = index++;
                next = child.NextVisible(false) && child.depth == depth;
            }

            if (order.Count == 0)
            {
                return;
            }

            fields.Sort((a, b) =>
            {
                var hasA = order.TryGetValue(a.Name, out var indexA);
                var hasB = order.TryGetValue(b.Name, out var indexB);

                if (hasA && hasB)
                {
                    return indexA.CompareTo(indexB);
                }

                if (hasA != hasB)
                {
                    return hasA ? -1 : 1;
                }

                return CompareByMetadataToken(a, b);
            });
        }

        /// <summary>
        /// 同层内按元数据令牌排序——**同一张表内**的行号才是声明顺序（跨表比大小没有意义，
        /// 这条在 <c>MethodNodeTests</c> 里有用例钉着）。
        /// </summary>
        /// <param name="a">左。</param>
        /// <param name="b">右。</param>
        /// <returns>比较结果。</returns>
        private static int CompareByMetadataToken(FieldInfo a, FieldInfo b)
        {
            return a.MetadataToken.CompareTo(b.MetadataToken);
        }

        #endregion
    }
}
