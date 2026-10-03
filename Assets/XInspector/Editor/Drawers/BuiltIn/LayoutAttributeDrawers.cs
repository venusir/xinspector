using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="GUIColorAttribute"/>：把内侧的一切染上颜色。
    /// <para>
    /// 权重 <c>-900</c>，在链上几乎最外层——染的不只是值控件，分组框、信息框、
    /// 替换型值绘制器都在它里面。
    /// </para>
    /// </summary>
    [DrawerPriority(-900d)]
    internal sealed class GUIColorDrawer : AttributeDrawer<GUIColorAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, GUIColorAttribute attribute, GUIContent label)
        {
            var previous = GUI.color;

            try
            {
                GUI.color = new Color(attribute.R, attribute.G, attribute.B, attribute.A);
                CallNextDrawer(property, label);
            }
            finally
            {
                // 必须还原：GUI.color 是全局状态，漏还原会让**别的 Inspector** 跟着变色，
                // 而现象出现在别处，几乎联想不到源头。
                GUI.color = previous;
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="IndentAttribute"/>：把内侧整体缩进若干级，返回时原样还原。
    /// </summary>
    [DrawerPriority(-880d)]
    internal sealed class IndentDrawer : AttributeDrawer<IndentAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, IndentAttribute attribute, GUIContent label)
        {
            var previous = EditorGUI.indentLevel;

            try
            {
                EditorGUI.indentLevel = previous + attribute.IndentLevel;
                CallNextDrawer(property, label);
            }
            finally
            {
                EditorGUI.indentLevel = previous;
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="PropertySpaceAttribute"/>：在字段前后各留一段间距。
    /// </summary>
    [DrawerPriority(-860d)]
    internal sealed class PropertySpaceDrawer : AttributeDrawer<PropertySpaceAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, PropertySpaceAttribute attribute, GUIContent label)
        {
            DrawSpace(attribute.SpaceBefore);
            CallNextDrawer(property, label);
            DrawSpace(attribute.SpaceAfter);
        }

        #endregion

        #region Private Helpers

        /// <summary>画一段间距，非正值跳过。</summary>
        /// <param name="pixels">间距（像素）。</param>
        /// <remarks>
        /// 负值跳过而不是画出来：Unity 的垂直布局不接受负间距，画不出来还会打乱后面的排布。
        /// 这一点写进了 <see cref="PropertySpaceAttribute"/> 的文档。
        /// </remarks>
        private static void DrawSpace(float pixels)
        {
            if (pixels > 0f)
            {
                EditorGUILayout.Space(pixels);
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="LabelWidthAttribute"/>：临时改标签宽度，返回时还原。
    /// </summary>
    [DrawerPriority(-500d)]
    internal sealed class LabelWidthDrawer : AttributeDrawer<LabelWidthAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, LabelWidthAttribute attribute, GUIContent label)
        {
            var previous = EditorGUIUtility.labelWidth;

            try
            {
                EditorGUIUtility.labelWidth = attribute.Width;
                CallNextDrawer(property, label);
            }
            finally
            {
                EditorGUIUtility.labelWidth = previous;
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="HideLabelAttribute"/>：把「无标签」传给下一个绘制器。
    /// <para>
    /// 不碰任何状态、不新建对象——<see cref="GUIContent.none"/> 是静态实例，
    /// 「撤掉标签」这件事在链上就只是换一个参数往下传。
    /// </para>
    /// </summary>
    [DrawerPriority(-480d)]
    internal sealed class HideLabelDrawer : AttributeDrawer<HideLabelAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, HideLabelAttribute attribute, GUIContent label)
        {
            CallNextDrawer(property, GUIContent.none);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="SuffixLabelAttribute"/>：在值控件右侧画一段只读后缀。
    /// <para>
    /// 三种画法：叠在控件上（<c>Overlay</c>）、占右侧一列（默认）、以及复合类型上的回退
    /// ——数组与嵌套结构不进水平布局（那会把展开的子控件挤成半宽），改为画在字段下方。
    /// </para>
    /// </summary>
    [DrawerPriority(-200d)]
    internal sealed class SuffixLabelDrawer : AttributeDrawer<SuffixLabelAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, SuffixLabelAttribute attribute, GUIContent label)
        {
            var content = ContentOf(property, attribute);

            if (IsCompound(property))
            {
                CallNextDrawer(property, label);
                DrawBelow(content);
                return;
            }

            if (attribute.Overlay)
            {
                CallNextDrawer(property, label);
                DrawOverlay(content);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                CallNextDrawer(property, label);
                EditorGUILayout.LabelField(content, EditorStyles.miniLabel, GUILayout.Width(WidthOf(content)));
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>取（并缓存）后缀文本的绘制内容。</summary>
        /// <param name="property">目标属性。</param>
        /// <param name="attribute">后缀特性。</param>
        /// <returns>可复用的绘制内容。</returns>
        /// <remarks>
        /// 缓存在 <see cref="PropertyState"/> 而不是绘制器字段里——绘制器是全工程共享的
        /// 无状态单例。文本构建后不再变，每帧新建 <see cref="GUIContent"/> 是纯浪费。
        /// </remarks>
        private static GUIContent ContentOf(InspectorProperty property, SuffixLabelAttribute attribute)
        {
            var state = property.State.GetOrCreate<SuffixLabelState>();
            if (state.Content == null)
            {
                state.Content = new GUIContent(attribute.Label);
            }

            return state.Content;
        }

        /// <summary>判断是否为复合类型（数组或带可见子级的类型）。</summary>
        /// <param name="property">目标属性。</param>
        /// <returns>是复合类型返回 <c>true</c>。</returns>
        private static bool IsCompound(InspectorProperty property)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;
            return serializedProperty != null &&
                   (serializedProperty.isArray || serializedProperty.hasVisibleChildren);
        }

        /// <summary>按内容算后缀需要的宽度。</summary>
        /// <param name="content">后缀内容。</param>
        /// <returns>宽度（像素）。</returns>
        private static float WidthOf(GUIContent content)
        {
            return EditorStyles.miniLabel.CalcSize(content).x + 4f;
        }

        /// <summary>把后缀叠在刚画完的控件右侧。</summary>
        /// <param name="content">后缀内容。</param>
        private static void DrawOverlay(GUIContent content)
        {
            var rect = GUILayoutUtility.GetLastRect();
            rect.xMin = rect.xMax - WidthOf(content);
            EditorGUI.LabelField(rect, content, EditorStyles.miniLabel);
        }

        /// <summary>把后缀画在字段下方（复合类型的回退画法）。</summary>
        /// <param name="content">后缀内容。</param>
        private static void DrawBelow(GUIContent content)
        {
            EditorGUILayout.LabelField(content, EditorStyles.miniLabel);
        }

        #endregion
    }

    /// <summary>
    /// 后缀文本的缓存内容，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    internal sealed class SuffixLabelState
    {
        /// <summary>缓存的绘制内容。</summary>
        public GUIContent Content;
    }
}
