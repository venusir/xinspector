using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 画出 <see cref="TitleAttribute"/> 的标题与副标题。
    /// <para>
    /// 它是最简单的一种「包装型」绘制器：画完自己的东西后调用下一个，
    /// 内侧内容因此落在标题下方。类级与成员级的标题走的是**同一条路径**——
    /// 构建期把类型上的特性直接放到了根节点，于是根节点的链上自然出现了本绘制器。
    /// 换句话说，「类级标题」不是特例，而是同一种特性出现在了另一个节点上。
    /// </para>
    /// </summary>
    internal sealed class TitleAttributeDrawer : AttributeDrawer<TitleAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, TitleAttribute attribute, GUIContent label)
        {
            EditorGUILayout.LabelField(attribute.Title, EditorStyles.boldLabel);

            if (!string.IsNullOrWhiteSpace(attribute.Subtitle))
            {
                EditorGUILayout.LabelField(attribute.Subtitle, EditorStyles.miniLabel);
            }

            CallNextDrawer(property, label);
        }

        #endregion
    }
}
