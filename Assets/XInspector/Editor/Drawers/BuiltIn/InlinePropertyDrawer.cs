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
    /// <b>两种形态，按构建期判据分派：</b>
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>真节点形态</b>：复合成员已在构建期展开成子节点（<c>NestedMemberExpansion</c>）——
    /// 本绘制器只画标签与标签宽度，子节点交给复合末端。它因此从**替换型变成了穿过型**：
    /// 调 <c>CallNextDrawer</c>。副产品是同档的替换型绘制器（<c>[DisplayAsString]</c> 等）
    /// 与它同挂时**不再是非此即彼**，两者都会跑（不支持的类型各自告警一次后放行）。
    /// </description></item>
    /// <item><description>
    /// <b>观感派形态</b>：向量这类**原生复合类型**不展开真节点（它们的子属性由原生控件整块画），
    /// 本绘制器照旧自己迭代 <c>SerializedProperty</c> 画出来——行为与本轮之前逐字一致。
    /// </description></item>
    /// </list>
    /// <para>
    /// 降级（数组/列表、没有可见子字段、反射成员）两支保留：告警一次 + 退回末端。
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

                if (property.Children.Count > 0)
                {
                    // **真节点形态**：这个复合成员已经在构建期展开了（见 NestedMemberExpansion），
                    // 子节点由复合末端统一画（缩进、只读罩、折叠头都在那边）。
                    // 本绘制器只负责标签与标签宽度——它从替换型变成了**穿过型**：调下一个。
                    CallNextDrawer(property, label);
                    return;
                }

                // 观感派形态：向量这类**原生复合类型**不展开真节点（它们的子属性由原生控件整块画），
                // 这里保持既有的「自己迭代子属性画出来」——行为与本轮之前逐字一致。
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
