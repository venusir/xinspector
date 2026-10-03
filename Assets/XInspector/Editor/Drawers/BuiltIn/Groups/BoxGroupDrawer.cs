using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 把分组画成一个带边框的盒子。
    /// <para>
    /// 它是「链条可以层层包裹」最直观的证据：本绘制器画完框之后**调用下一个绘制器**，
    /// 于是子节点绘制器在框内执行；框的闭合发生在调用返回之后。整个实现里没有任何一处
    /// 提到子节点的存在——包裹是链条顺序的自然结果，不是特例代码。
    /// </para>
    /// </summary>
    internal sealed class BoxGroupDrawer : AttributeDrawer<BoxGroupAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, BoxGroupAttribute attribute, GUIContent label)
        {
            // 用 Unity 自带的 VerticalScope 而不是自己写 BeginVertical/EndVertical 配对：
            // 它是一个 IDisposable，编译器保证 Dispose 一定被调用，因此
            // 「Begin 了却因为提前 return 或异常而没 End」这类失衡在结构上就不可能发生。
            // 失衡的后果是 GUILayout 报 Mismatched LayoutGroup，且症状会蔓延到别的 Inspector。
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                if (attribute.ShowLabel)
                {
                    var text = string.IsNullOrWhiteSpace(attribute.Label) ? attribute.GroupName : attribute.Label;
                    EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
                }

                CallNextDrawer(property, label);
            }
        }

        #endregion
    }
}
