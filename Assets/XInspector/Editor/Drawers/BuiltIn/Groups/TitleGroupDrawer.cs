using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="TitleGroupAttribute"/>：画标题（可加副标题、分隔线），再画组内内容。
    /// <para>
    /// 权重 <c>-150</c>：标题属于「这块叫什么」，框属于「这块的范围」——标题在外、框在内。
    /// </para>
    /// </summary>
    [DrawerPriority(-150d)]
    internal sealed class TitleGroupDrawer : AttributeDrawer<TitleGroupAttribute>
    {
        #region Private Fields

        /// <summary>
        /// 按「加粗 + 对齐」缓存的对齐样式。
        /// <para>
        /// 惰性创建而非静态初始化：静态构造在批处理（无 GUI 上下文）下也会被触发
        /// （注册表扫描会实例化绘制器），那时创建样式不安全。
        /// </para>
        /// </summary>
        private static readonly GUIStyle[] CachedStyles = new GUIStyle[8];

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, TitleGroupAttribute attribute, GUIContent label)
        {
            var state = property.State.GetOrCreate<TitleGroupState>();

            if (!state.Initialized)
            {
                // 文本内容缓存在状态里：每帧新建 GUIContent 是纯浪费（标签同理，见 InspectorProperty）。
                state.Initialized = true;
                state.Title = new GUIContent(attribute.GroupName);
                state.Subtitle = string.IsNullOrEmpty(attribute.Subtitle) ? null : new GUIContent(attribute.Subtitle);
            }

            var previousIndent = EditorGUI.indentLevel;

            try
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawHeader(attribute, state);

                    if (attribute.Indent)
                    {
                        EditorGUI.indentLevel = previousIndent + 1;
                    }

                    CallNextDrawer(property, label);
                }
            }
            finally
            {
                // 缩进是全局状态，必须还原——漏还原会让**别的 Inspector** 跟着缩进。
                EditorGUI.indentLevel = previousIndent;
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>画标题、副标题与分隔线。</summary>
        /// <param name="attribute">标题组特性。</param>
        /// <param name="state">缓存的文本内容。</param>
        private static void DrawHeader(TitleGroupAttribute attribute, TitleGroupState state)
        {
            if (attribute.Alignment == TitleAlignments.Split && state.Subtitle != null)
            {
                // 分列两端：标题在左、副标题在右。
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(state.Title, StyleOf(attribute.BoldTitle, TitleAlignments.Left));
                    EditorGUILayout.LabelField(
                        state.Subtitle,
                        EditorStyles.miniLabel,
                        GUILayout.Width(EditorStyles.miniLabel.CalcSize(state.Subtitle).x));
                }
            }
            else
            {
                EditorGUILayout.LabelField(state.Title, StyleOf(attribute.BoldTitle, attribute.Alignment));

                if (state.Subtitle != null)
                {
                    EditorGUILayout.LabelField(state.Subtitle, StyleOf(false, attribute.Alignment));
                }
            }

            if (attribute.HorizontalLine)
            {
                DrawHorizontalLine();
            }
        }

        /// <summary>画一条分隔线（一个 1 像素高的矩形）。</summary>
        private static void DrawHorizontalLine()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1f);
            var color = EditorGUIUtility.isProSkin
                ? new Color(0.35f, 0.35f, 0.35f)
                : new Color(0.65f, 0.65f, 0.65f);

            EditorGUI.DrawRect(rect, color);
        }

        /// <summary>取（并按需创建）对齐样式。</summary>
        /// <param name="bold">是否加粗。</param>
        /// <param name="alignment">对齐方式（Split 的标题按左对齐处理）。</param>
        /// <returns>可复用的样式。</returns>
        private static GUIStyle StyleOf(bool bold, TitleAlignments alignment)
        {
            var index = (int)alignment + (bold ? 4 : 0);
            var style = CachedStyles[index];

            if (style == null)
            {
                var anchor = alignment == TitleAlignments.Centered
                    ? TextAnchor.MiddleCenter
                    : alignment == TitleAlignments.Right
                        ? TextAnchor.MiddleRight
                        : TextAnchor.MiddleLeft;

                style = new GUIStyle(bold ? EditorStyles.boldLabel : EditorStyles.label) { alignment = anchor };
                CachedStyles[index] = style;
            }

            return style;
        }

        #endregion
    }

    /// <summary>
    /// 标题组的每属性状态，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    internal sealed class TitleGroupState
    {
        /// <summary>是否已缓存过文本内容。</summary>
        public bool Initialized;

        /// <summary>缓存的标题内容。</summary>
        public GUIContent Title;

        /// <summary>缓存的副标题内容；无副标题为 <c>null</c>。</summary>
        public GUIContent Subtitle;
    }
}
