using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="InfoBoxAttribute"/>：在字段上方画一条信息框。
    /// <para>
    /// 权重 <c>-700</c>：在颜色/缩进/间距之内、标签修饰与值控件之外——框要跟着字段走，
    /// 但不该被字段的标签宽度之类影响。
    /// </para>
    /// </summary>
    [DrawerPriority(-700d)]
    internal sealed class InfoBoxDrawer : AttributeDrawer<InfoBoxAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, InfoBoxAttribute attribute, GUIContent label)
        {
            // 无条件时画；有条件时按条件决定画不画。**无论画不画，字段照常往下传**——
            // visibleIf 关的是这条框，不是那个字段。
            if (InfoBoxConditions.IsVisible(property, attribute))
            {
                // 字符串重载内部走 EditorGUIUtility.TempContent（复用的临时内容对象），
                // 不产生每帧分配——不必自己缓存 GUIContent。
                EditorGUILayout.HelpBox(attribute.Message, InfoMessageTypeMap.ToUnity(attribute.InfoMessageType));
            }

            CallNextDrawer(property, label);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="DetailedInfoBoxAttribute"/>：摘要一行 + 可展开的详情。
    /// </summary>
    [DrawerPriority(-690d)]
    internal sealed class DetailedInfoBoxDrawer : AttributeDrawer<DetailedInfoBoxAttribute>
    {
        #region Private Fields

        // 常量标签只建一次：静态只读字段对所有属性共享，且不可变，符合绘制器的无状态约束。
        private static readonly GUIContent DetailsLabel = new GUIContent("详情");

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, DetailedInfoBoxAttribute attribute, GUIContent label)
        {
            if (InfoBoxConditions.IsVisible(property, attribute))
            {
                EditorGUILayout.HelpBox(attribute.Message, InfoMessageTypeMap.ToUnity(attribute.InfoMessageType));

                if (!string.IsNullOrWhiteSpace(attribute.Details))
                {
                    // 展开状态按属性隔离；域重载后回到折叠——本包不做跨会话持久化。
                    var state = property.State.GetOrCreate<DetailedInfoBoxState>();
                    state.Expanded = EditorGUILayout.Foldout(state.Expanded, DetailsLabel, true);

                    if (state.Expanded)
                    {
                        EditorGUILayout.HelpBox(attribute.Details, MessageType.None);
                    }
                }
            }

            CallNextDrawer(property, label);
        }

        #endregion
    }

    /// <summary>
    /// 详情框的展开状态，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    internal sealed class DetailedInfoBoxState
    {
        /// <summary>是否已展开。</summary>
        public bool Expanded;
    }

    /// <summary>
    /// <see cref="InfoMessageType"/> 到 Unity <c>MessageType</c> 的映射。
    /// </summary>
    /// <remarks>
    /// Runtime 侧不能引用 <c>UnityEditor.MessageType</c>（零 Unity 依赖），
    /// 故映射只能落在编辑器侧的绘制路径上——这也是 <see cref="InfoMessageType"/> 存在的理由。
    /// </remarks>
    internal static class InfoMessageTypeMap
    {
        /// <summary>
        /// 转换为 Unity 的消息类型。
        /// </summary>
        /// <param name="type">本包的消息类型。</param>
        /// <returns>对应的 Unity 消息类型。</returns>
        public static MessageType ToUnity(InfoMessageType type)
        {
            switch (type)
            {
                case InfoMessageType.None:
                    return MessageType.None;
                case InfoMessageType.Warning:
                    return MessageType.Warning;
                case InfoMessageType.Error:
                    return MessageType.Error;
                default:
                    return MessageType.Info;
            }
        }
    }
}
