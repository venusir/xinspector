using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 反射成员节点的末端绘制器：把值格式化成**只读文本**画出来。
    /// <para>
    /// 它是反射成员那一支的末端，与 <see cref="UnityFallbackDrawer"/> 并列——
    /// 两者的分工就是「值从哪来」：那个把值交给 Unity 的 <c>PropertyField</c>，
    /// 这个自己格式化。之所以不能复用前者：反射成员没有 <c>SerializedProperty</c>，
    /// 而 <c>PropertyField</c> 只认序列化属性。
    /// </para>
    /// <para>
    /// <b>只读是后端决定的，不是绘制器偷懒。</b> 反射成员不在 Unity 的序列化里，
    /// 写进去既不可撤销也不会保存——见 <see cref="ShowInInspectorAttribute"/>。
    /// </para>
    /// <para>
    /// 由构建期显式追加，不经注册表匹配。
    /// </para>
    /// </summary>
    internal sealed class ReflectedMemberTerminalDrawer : XInspectorDrawer
    {
        #region XInspectorDrawer

        /// <summary>
        /// 恒为 <c>false</c>：末端绘制器由构建期显式追加，从不参与自动匹配。
        /// </summary>
        /// <param name="property">候选属性。</param>
        /// <returns>恒为 <c>false</c>。</returns>
        public override bool CanDraw(InspectorProperty property)
        {
            return false;
        }

        /// <inheritdoc/>
        public override void DrawPropertyLayout(InspectorProperty property, Attribute attribute, GUIContent label)
        {
            var entry = property.ValueEntry as ReflectedValueEntry;

            if (entry == null)
            {
                // 走到这里说明构建期为该成员装了错误的末端绘制器。
                // 明确画一条提示而不是静默跳过：静默跳过会表现为「这个成员不见了」，
                // 而那正是最难联想到构建期的问题现象。
                EditorGUILayout.HelpBox(
                    $"属性 \"{property.Path}\" 没有反射值入口，ReflectedMemberTerminalDrawer 无法绘制它。",
                    MessageType.Warning);
                return;
            }

            if (!entry.TryGetDisplayValue(out var value, out var mixed, out var error))
            {
                if (error != null)
                {
                    ReportReadError(property, error);
                    EditorGUILayout.HelpBox($"读取「{property.Path}」的值时出错：{error}", MessageType.Error);
                    return;
                }

                // 不一致：与 Unity 自身一样显示占位符，不拿某个目标的值冒充。
                DrawText(label, ValueTextFormatter.MixedValues);
                return;
            }

            DrawText(label, ReflectedValueFormatter.Format(value, entry.ValueType));
        }

        #endregion

        #region Private Helpers

        /// <summary>画一行「标签 + 只读文本」。</summary>
        /// <param name="label">标签。</param>
        /// <param name="text">显示文本。</param>
        private static void DrawText(GUIContent label, string text)
        {
            if (label == null || label == GUIContent.none)
            {
                EditorGUILayout.LabelField(text);
                return;
            }

            EditorGUILayout.LabelField(label, new GUIContent(text));
        }

        /// <summary>
        /// 记一条取值失败的告警，同一属性只记一次。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="error">错误消息。</param>
        /// <remarks>
        /// 行内已经有 HelpBox 了，这里还要记一条 Console：行内提示只在该成员可见时才看得到，
        /// 而「这个成员的 getter 抛异常」通常正是要拿去搜的线索。
        /// </remarks>
        private static void ReportReadError(InspectorProperty property, string error)
        {
            DrawerWarnings.Once(
                property,
                nameof(ReflectedMemberTerminalDrawer) + ".read",
                $"[XInspector] 读取属性「{property.Path}」的值时出错：{error}");
        }

        #endregion
    }
}
