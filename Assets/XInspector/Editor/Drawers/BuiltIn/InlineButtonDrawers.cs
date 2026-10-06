using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="InlineButtonAttribute"/>：在字段**右侧**加一个小按钮。
    /// <para>
    /// 权重取 <c>-50</c>，理由是硬性的：它要包住字段本身，故必须在值绘制器（权重 <c>0</c>）
    /// **之外**；而 <c>[DisplayAsString]</c>、<c>[ProgressBar]</c>、<c>[ValueDropdown]</c> 这些
    /// 替换型值绘制器**画完不调下一个**，一旦它们在外侧，按钮就永远画不出来——
    /// 且现象是「按钮莫名其妙消失了」，极难归因。放在属性带（<c>-100</c>）之内则让
    /// <c>[LabelText]</c>、<c>[Indent]</c>、<c>[GUIColor]</c> 照常包在最外层。
    /// </para>
    /// </summary>
    [DrawerPriority(-50d)]
    internal sealed class InlineButtonDrawer : AttributeDrawer<InlineButtonAttribute>
    {
        #region Private Fields

        /// <summary>按钮与字段之间的间距（像素）。</summary>
        private const float Gap = 2f;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property,
            InlineButtonAttribute attribute,
            GUIContent label)
        {
            var state = property.State.Get<InlineButtonState>();
            var index = state == null ? -1 : InlineButtonProcessor.IndexOf(property, attribute);

            if (state == null || index < 0)
            {
                // 处理器没跑到：字段照画，只是没有按钮——比整行画不出来强。
                CallNextDrawer(property, label);
                return;
            }

            var content = state.Labels[index] ?? new GUIContent(attribute.MethodName);
            var width = ResolveWidth(state, index, content);
            var available = GUILayoutUtility.GetRect(1f, 0f, GUILayout.ExpandWidth(true)).width;

            using (new EditorGUILayout.HorizontalScope())
            {
                // 字段占满「本行减去按钮」的宽度：GUILayout 里排在后面的控件拿不到位置，
                // 故先把宽度算好给字段，按钮留在右侧。
                using (new EditorGUILayout.VerticalScope(
                           GUILayout.Width(Mathf.Max(1f, available - width - Gap))))
                {
                    CallNextDrawer(property, label);
                }

                var methods = state.Methods[index];
                var callable = methods != null && methods[0] != null;

                using (new EditorGUI.DisabledScope(!callable || property.State.IsReadOnly))
                {
                    if (GUILayout.Button(content, EditorStyles.miniButton, GUILayout.Width(width)))
                    {
                        Click(property, methods, state.Scopes, content.text);
                    }
                }
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>取按钮宽度：首帧量一次，之后复用。</summary>
        /// <param name="state">按钮状态。</param>
        /// <param name="index">按钮次序。</param>
        /// <param name="content">按钮内容。</param>
        /// <returns>宽度（像素）。</returns>
        /// <remarks>
        /// 量宽度要用 <see cref="EditorStyles"/>，而它只在 GUI 期间可用——故缓存到状态里
        /// （绘制器不得有可变字段，每属性状态一律进 <see cref="PropertyState"/>）。
        /// </remarks>
        private static float ResolveWidth(InlineButtonState state, int index, GUIContent content)
        {
            if (state.Widths[index] > 0f)
            {
                return state.Widths[index];
            }

            var width = EditorStyles.miniButton.CalcSize(content).x + 4f;
            state.Widths[index] = width;
            return width;
        }

        /// <summary>点击：对每个目标调用各自解析出来的那份方法。</summary>
        /// <param name="property">目标属性。</param>
        /// <param name="methods">逐目标解析出的方法。</param>
        /// <param name="scopes">逐目标的嵌套实例来源；顶层为 <c>null</c>。</param>
        /// <param name="undoLabel">撤销栈里显示的这一步的名字。</param>
        private static void Click(
            InspectorProperty property, MethodInfo[] methods, ReflectedAccessor[] scopes, string undoLabel)
        {
            var tree = property.Owner;

            MethodInvoker.Invoke(
                methods, tree?.Targets, scopes, null, tree?.UndoEnabled ?? false, undoLabel);
        }

        #endregion
    }
}
