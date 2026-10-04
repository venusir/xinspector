using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 方法节点（<see cref="InspectorPropertyKind.Method"/>）的末端绘制器。
    /// <para>
    /// 它**正常情况下永不可达**：方法节点能存在，就说明它身上带着 <c>[Button]</c> 一类特性，
    /// 而那个特性必然配了一格绘制器；那格绘制器自己画完就结束，不调下一个。
    /// 这里画一条明确的提示，是给「构建期配错了末端」这种结构性故障兜底的
    /// ——静默跳过会表现为「这个方法节点不见了」，而那是最难归因的一类现象。
    /// </para>
    /// <para>
    /// 由构建期显式追加，不经注册表匹配。
    /// </para>
    /// </summary>
    internal sealed class MethodTerminalDrawer : XInspectorDrawer
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
            // 走到这里说明这个方法节点身上没有任何能画它的绘制器。
            EditorGUILayout.HelpBox(
                $"方法 \"{property.Path}\" 没有对应的绘制器，XInspector 无法绘制它。"
                + "这通常意味着构建期给它配的特性没有被任何绘制器认领。",
                MessageType.Warning);
        }

        #endregion
    }
}
