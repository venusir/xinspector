using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="VerticalGroupAttribute"/>：只做竖直容器与内边距，不画框、不画标题。
    /// </summary>
    /// <remarks>
    /// 权重 <c>-110</c>：分组带里靠内的一档——它只是「内容的容器」，
    /// 不如标题（-150）与框（-130）那样表达结构。
    /// </remarks>
    [DrawerPriority(-110d)]
    internal sealed class VerticalGroupDrawer : AttributeDrawer<VerticalGroupAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, VerticalGroupAttribute attribute, GUIContent label)
        {
            DrawPadding(attribute.PaddingTop);

            // 用 Unity 自带的 VerticalScope 而不是自己写 Begin/End 配对：
            // IDisposable 保证 Dispose 一定被调用，「失衡」在结构上就不可能发生。
            using (new EditorGUILayout.VerticalScope())
            {
                CallNextDrawer(property, label);
            }

            DrawPadding(attribute.PaddingBottom);
        }

        #endregion

        #region Private Helpers

        /// <summary>画一段内边距，非正值跳过（Unity 的垂直布局不接受负间距）。</summary>
        /// <param name="pixels">内边距（像素）。</param>
        private static void DrawPadding(float pixels)
        {
            if (pixels > 0f)
            {
                EditorGUILayout.Space(pixels);
            }
        }

        #endregion
    }
}
