using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="ToggleAttribute"/>：在字段左侧画开关，并把内侧包在同一行里。
    /// <para>
    /// 权重 <c>-250</c>：在值控件之外、着色/缩进/间距之内——开关属于「这个字段」，
    /// 不属于某一格修饰。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>开关永远可点</b>：它不在禁用罩里。否则一关掉就再也开不回来——
    /// 那是把「门控」做成了「单向闸门」。
    /// </remarks>
    [DrawerPriority(-250d)]
    internal sealed class ToggleDrawer : AttributeDrawer<ToggleAttribute>
    {
        #region Private Fields

        /// <summary>开关列宽（像素）。</summary>
        private const float ToggleWidth = 16f;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, ToggleAttribute attribute, GUIContent label)
        {
            var state = property.State.Get<ToggleState>();

            if (state?.Toggle == null)
            {
                // 解析失败：构建期的处理器已经告警过一次，这里只把内侧原样画出来——
                // 字段不会因为一个拼错的开关名而消失。
                CallNextDrawer(property, label);
                return;
            }

            var toggle = state.Toggle;

            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(ToggleWidth));
                toggle.boolValue = EditorGUI.Toggle(rect, toggle.boolValue);

                CallNextDrawer(property, label);
            }
        }

        #endregion
    }
}
