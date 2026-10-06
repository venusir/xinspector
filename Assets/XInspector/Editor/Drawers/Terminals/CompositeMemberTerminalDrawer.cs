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
    /// <para>
    /// <b>标了 <c>[Searchable]</c> 时这里多两件事</b>：画一行搜索框（在只读罩**之外**——
    /// 只读的复合块也要能搜），以及按命中集挑子节点画。过滤是**策略不是可见性**：
    /// 被筛掉的节点照旧可见，只是没被交给 <c>Draw()</c>——见 <see cref="SearchScope"/>。
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

            // 搜索框画在**只读罩之外**：只读的复合块也要能搜（输入框本身不是数据）。
            // 框由**宿主自己**画——状态就是在这里按需建出来的，子节点只往上找。
            if (property.Attributes.Has<SearchableAttribute>())
            {
                SearchBox.Draw(property);
            }

            var scope = SearchScope.Find(property);

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                EditorGUI.indentLevel++;
                try
                {
                    if (scope.IsActive)
                    {
                        DrawFiltered(property, scope);
                    }
                    else
                    {
                        property.DrawChildren();
                    }
                }
                finally
                {
                    EditorGUI.indentLevel--;
                }
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 只画命中的子节点；一个都没命中时留一条提示，而不是留一块空白。
        /// </summary>
        /// <param name="property">复合成员节点。</param>
        /// <param name="scope">生效中的搜索。</param>
        private static void DrawFiltered(InspectorProperty property, SearchScope scope)
        {
            var drawn = false;
            var children = property.Children;

            for (var i = 0; i < children.Count; i++)
            {
                if (!scope.ShouldDraw(children[i]))
                {
                    continue;
                }

                children[i].Draw();
                drawn = true;
            }

            if (!drawn)
            {
                SearchBox.DrawNoMatchHint();
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
