using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 成员节点的末端绘制器：把值交给 Unity 原生的 <c>PropertyField</c> 画。
    /// <para>
    /// 这是整条链最内侧的一格，也是「XInspector 不重造轮子」的体现——
    /// 各种值类型（int、枚举、对象引用、嵌套结构……）的绘制全部复用 Unity 已有的实现，
    /// 于是不会出现「我们的 Vector3 画得和 Unity 不一样」这类问题。
    /// </para>
    /// <para>
    /// 由构建期显式追加，不经注册表匹配。
    /// </para>
    /// </summary>
    internal sealed class UnityFallbackDrawer : XInspectorDrawer
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
            var entry = property.ValueEntry;

            if (entry == null || !entry.IsUnityBacked)
            {
                // 走到这里说明构建期为该成员装了错误的末端绘制器。
                // 明确画一条提示而不是静默跳过：静默跳过会表现为「这个字段不见了」，
                // 而那正是最难联想到构建期的问题现象。
                EditorGUILayout.HelpBox(
                    $"属性 \"{property.Path}\" 没有 Unity 序列化后端，UnityFallbackDrawer 无法绘制它。",
                    MessageType.Warning);
                return;
            }

            // includeChildren: true —— 嵌套类型交给 Unity 自己展开。
            // v0 不实现自己的可展开复合绘制器，正因如此本提交的渲染结果才能与
            // Unity 默认 Inspector 逐像素一致，从而把「值管道是否正确」单独隔离出来验证。
            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                EditorGUILayout.PropertyField(entry.SerializedProperty, label, true);
            }
        }

        #endregion
    }
}
