using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="InlinePropertyAttribute"/>：把复合类型的子字段**提到本层**画——不画折叠头。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>替换型绘制器</b>：有可内联的子字段时自己画完、不调下一个（与
    /// <see cref="DisplayAsStringDrawer"/> 同档同款）。因此它绕过了末端绘制器那层禁用罩，
    /// **必须自己处理只读**——<c>[ReadOnly]</c> / <c>[DisableIf]</c> 要在这类字段上照常生效。
    /// </para>
    /// <para>
    /// <b>观感派：子字段不进本包管线。</b> 它们仍由 Unity 的原生 <c>PropertyField</c> 逐个绘制，
    /// 不建子节点、不换末端——嵌套字段身上的本包特性照旧不生效，这与「嵌套类型交给 Unity」
    /// 的既有边界一致（见 <c>Pipeline</c> §二第 7 条的推迟记录）。
    /// </para>
    /// <para>
    /// 画法（官方只有一句「contents next to the label」，此为兜底并写进文档）：
    /// **父标签照常画在标签列（宽度受 <see cref="InlinePropertyAttribute.LabelWidth"/> 控制），
    /// 子字段缩进一级逐个画在下面**——「不画折叠头」的字面实现，父字段名不丢，
    /// 叠一个 <c>[HideLabel]</c> 还能把父标签整个撤掉。
    /// </para>
    /// <para>
    /// 与同档（值绘制带）的其它替换型绘制器（<c>[DisplayAsString]</c> 等）谁生效，
    /// 由**声明先后**决定——同一格子上不会两条都跑。
    /// </para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class InlinePropertyDrawer : AttributeDrawer<InlinePropertyAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, InlinePropertyAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty != null && serializedProperty.isArray)
            {
                // 数组/列表单独措辞：那不是「类型不合适」，而是「展开集合是另一件事」（L6）。
                DrawerWarnings.Once(property, nameof(InlinePropertyDrawer) + ".array",
                    $"[XInspector] 属性「{property.Path}」上的 [InlineProperty] 不内联数组与列表" +
                    "（展开集合不是本特性的职责），该字段退回普通绘制。");
                CallNextDrawer(property, label);
                return;
            }

            if (!InlinePropertyLayout.CanInline(serializedProperty))
            {
                DrawerWarnings.Once(property, nameof(InlinePropertyDrawer),
                    DrawerWarnings.TypeMismatch(property, "[InlineProperty]", "带可见子字段的复合类型"));
                CallNextDrawer(property, label);
                return;
            }

            var previousLabelWidth = EditorGUIUtility.labelWidth;

            try
            {
                var width = InlinePropertyLayout.ResolveLabelWidth(attribute.LabelWidth);
                if (width > 0f)
                {
                    EditorGUIUtility.labelWidth = width;
                }

                // 标签在禁用罩之外：禁用的是可编辑性，不是标题。
                if (label != null && label != GUIContent.none)
                {
                    EditorGUILayout.PrefixLabel(label);
                }

                using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
                {
                    EditorGUI.indentLevel++;
                    try
                    {
                        DrawChildren(serializedProperty);
                    }
                    finally
                    {
                        EditorGUI.indentLevel--;
                    }
                }
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 按深度优先迭代画**直接**子字段；孙辈仍交给 Unity（<c>includeChildren: true</c>）。
        /// </summary>
        /// <param name="parent">复合类型的序列化属性。</param>
        /// <remarks>
        /// 深度检查是必需的：没有子字段时 <c>NextVisible(true)</c> 会直接走到本属性之后，
        /// 少了它就会把**别的字段**画进这一段。
        /// </remarks>
        private static void DrawChildren(SerializedProperty parent)
        {
            var child = parent.Copy();
            var depth = parent.depth + 1;
            var next = child.NextVisible(true) && child.depth == depth;

            while (next)
            {
                EditorGUILayout.PropertyField(child, true);
                next = child.NextVisible(false) && child.depth == depth;
            }
        }

        #endregion
    }

    /// <summary>
    /// 内联的**判定**——与绘制分开，因此可以无头测试（本仓不测 IMGUI）。
    /// </summary>
    internal static class InlinePropertyLayout
    {
        #region Public API

        /// <summary>
        /// 能否内联：需要 Unity 的序列化后端、带可见子字段，且不是数组 / 列表。
        /// </summary>
        /// <param name="serializedProperty">值的序列化属性；反射成员没有它（传 <c>null</c>）。</param>
        /// <returns>可以内联返回 <c>true</c>。</returns>
        /// <remarks>
        /// 向量（<c>Vector2</c> / <c>Vector2Int</c> 等）与任何带子字段的 <c>[Serializable]</c>
        /// 类型都会返回 <c>true</c>——Unity 的序列化属性本来就为向量暴露了 x/y/z 子属性。
        /// </remarks>
        public static bool CanInline(SerializedProperty serializedProperty)
        {
            return serializedProperty != null
                && !serializedProperty.isArray
                && serializedProperty.hasVisibleChildren;
        }

        /// <summary>
        /// 子字段绘制期间要设的标签宽度。
        /// </summary>
        /// <param name="attributeLabelWidth">特性上的 <see cref="InlinePropertyAttribute.LabelWidth"/>。</param>
        /// <returns>正值原样返回；非正值返回 <c>-1</c>，表示**不改**全局值。</returns>
        public static float ResolveLabelWidth(int attributeLabelWidth)
        {
            return attributeLabelWidth > 0 ? attributeLabelWidth : -1f;
        }

        #endregion
    }
}
