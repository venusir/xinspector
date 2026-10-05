using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 展开过的复合成员的末端：画「父标签 + 折叠三角」，再画子节点。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与 <see cref="UnityFallbackDrawer"/> 的分工：那个把整份交给 Unity 的
    /// <c>PropertyField(includeChildren: true)</c>；这个自己画父行、子节点走本包的树——
    /// 于是**嵌套层里的绘制器链、条件、标签、顺序第一次生效**。选哪一个由
    /// <c>PropertyTreeBuilder.TerminalFor</c> 按「有没有子节点」决定（子节点在收集期就位，
    /// 挂链时 <c>Children.Count</c> 可信）。
    /// </para>
    /// <para>
    /// <b>两件今天由 Unity 白送、节点化之后必须自己接的事：</b> 其一，父节点的只读要罩住
    /// 子节点（`[ReadOnly]` 标在复合字段上时整块变灰——Unity 的 <c>DisabledScope</c> 是
    /// 层级式的，子树里的 `[EnableGUI]` 也开不回来，与原生一致）；其二，子节点**缩进一级**。
    /// 标签与折叠三角留在只读罩外（与 <c>InlinePropertyDrawer</c> 的既有做法同款）。
    /// </para>
    /// <para>
    /// <b>折叠状态进 <see cref="PropertyState"/></b>（与 <c>[FoldoutGroup]</c> 等包内所有折叠
    /// 同一口径：不跨会话持久化）；初值取**收起**，与原生对嵌套 `[Serializable]` 字段的默认一致。
    /// </para>
    /// </remarks>
    internal sealed class CompositeMemberTerminalDrawer : XInspectorDrawer
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
                // 与 UnityFallbackDrawer 同款：明确画一条提示而不是静默跳过——
                // 静默跳过会表现为「这个字段不见了」，最难联想到构建期。
                EditorGUILayout.HelpBox(
                    $"属性 \"{property.Path}\" 没有 Unity 序列化后端，复合末端绘制器无法绘制它。",
                    MessageType.Warning);
                return;
            }

            var state = property.State.GetOrCreate<CompositeMemberState>();

            if (!state.FoldoutSuppressed)
            {
                state.Expanded = EditorGUILayout.Foldout(state.Expanded, label ?? GUIContent.none, true);

                if (!state.Expanded)
                {
                    return;
                }
            }

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                EditorGUI.indentLevel++;
                try
                {
                    property.DrawChildren();
                }
                finally
                {
                    EditorGUI.indentLevel--;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// 复合成员节点的每属性状态。
    /// </summary>
    /// <remarks>
    /// <see cref="FoldoutSuppressed"/> 由构建期定案（带 <c>[InlineProperty]</c> 的节点
    /// 不画折叠头——标签与折叠由那只绘制器说了算），绘制期只读。
    /// </remarks>
    internal sealed class CompositeMemberState
    {
        /// <summary>当前是否展开。初值 <c>false</c>——与原生对嵌套字段的默认一致。</summary>
        public bool Expanded;

        /// <summary>是否不画折叠头（带 <c>[InlineProperty]</c> 的节点为真）。构建期定案。</summary>
        public bool FoldoutSuppressed;
    }
}
