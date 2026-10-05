using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="RequiredListLengthAttribute"/>：元素个数不在范围内时画一条提示。
    /// </summary>
    /// <remarks>
    /// 权重 <c>-600</c>：与 <c>[Required]</c> 同档——在信息框之内、值控件之外，
    /// 与值控件是「同一个字段的两件事」。
    /// </remarks>
    [DrawerPriority(-600d)]
    internal sealed class RequiredListLengthDrawer : AttributeDrawer<RequiredListLengthAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property, RequiredListLengthAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || !CollectionDrawerLayout.CanDraw(serializedProperty))
            {
                DrawerWarnings.Once(property, nameof(RequiredListLengthDrawer),
                    DrawerWarnings.TypeMismatch(property, "[RequiredListLength]", "数组或 List"));
                CallNextDrawer(property, label);
                return;
            }

            if (serializedProperty.hasMultipleDifferentValues)
            {
                // 多选且各目标长度不一致：读到的不是任何一个目标的真值，报长度是错的。
                // 与 [MinValue]/[MaxValue] 那族「混合态不钳制」同一口径。
                CallNextDrawer(property, label);
                return;
            }

            var size = serializedProperty.arraySize;

            if (!ListLengthValidator.IsSatisfied(size, attribute.MinLength, attribute.MaxLength))
            {
                EditorGUILayout.HelpBox(
                    ListLengthValidator.Describe(size, attribute),
                    InfoMessageTypeMap.ToUnity(attribute.MessageType));
            }

            CallNextDrawer(property, label);
        }

        #endregion
    }

    /// <summary>
    /// 长度校验的**判定与文案**——纯函数，可无头测试。
    /// </summary>
    internal static class ListLengthValidator
    {
        #region Public API

        /// <summary>
        /// 长度是否满足要求。
        /// </summary>
        /// <param name="size">当前元素个数。</param>
        /// <param name="minLength">下限；<c>null</c> 表示不限。</param>
        /// <param name="maxLength">上限；<c>null</c> 表示不限。</param>
        /// <returns>满足返回 <c>true</c>。</returns>
        public static bool IsSatisfied(int size, int? minLength, int? maxLength)
        {
            if (minLength != null && size < minLength)
            {
                return false;
            }

            return maxLength == null || size <= maxLength;
        }

        /// <summary>
        /// 生成提示文案：给了 <see cref="RequiredListLengthAttribute.ErrorMessage"/> 就用它，
        /// 否则按上下限说清要求与现状。
        /// </summary>
        /// <param name="size">当前元素个数。</param>
        /// <param name="attribute">特性实例。</param>
        /// <returns>提示文本。</returns>
        public static string Describe(int size, RequiredListLengthAttribute attribute)
        {
            if (!string.IsNullOrWhiteSpace(attribute.ErrorMessage))
            {
                return attribute.ErrorMessage;
            }

            var min = attribute.MinLength;
            var max = attribute.MaxLength;

            if (min != null && max != null)
            {
                return min == max
                    ? $"此列表需要恰好 {min} 项（当前 {size} 项）。"
                    : $"此列表需要 {min}–{max} 项（当前 {size} 项）。";
            }

            return min != null
                ? $"此列表至少需要 {min} 项（当前 {size} 项）。"
                : $"此列表最多 {max} 项（当前 {size} 项）。";
        }

        #endregion
    }
}
