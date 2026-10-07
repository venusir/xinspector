using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 展开过的**多态引用**（<c>[SerializeReference]</c>）的末端：原生那一行照画，子节点走本包的树。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么不能复用 <see cref="CompositeMemberTerminalDrawer"/>。</b> 那个只画「折叠头 + 子节点」，
    /// 不画值——对嵌套 <c>[Serializable]</c> 类型没问题（值就是那些子字段，画全了），
    /// 但多态引用的**值是一份引用**：不画原生那一行，用户就看不见也换不了具体类型，
    /// 等于把一个能用的字段变成只读展示。那与本包「不给改就不假装能改」的取舍方向相反——
    /// 这里恰恰是**能改，只是多包了一层**。
    /// </para>
    /// <para>
    /// <b><c>includeChildren: false</c> 是这条的关键。</b> 原生那一行只画「类型名 + 赋值槽位」，
    /// 子字段由本包画——那正是这一批的意义（里面的条件、分组、顺序随之生效）。
    /// 给 <c>true</c> 的话子字段会画两遍。
    /// </para>
    /// <para>
    /// <b>本包不再加第二个折叠三角。</b> 原生那一行自己带一个（<c>ManagedReference</c> 的
    /// 原生展开器），再加一个就是两个并排的三角。代价是展开的多态成员**始终摊开**——
    /// 与 <c>[InlineProperty]</c> 的取舍同款（那边也是「摊平子字段、不画折叠头」）。
    /// </para>
    /// <para>
    /// <b>渲染未经目视确认。</b> 本仓不测 IMGUI——原生那一行到底画出什么（有没有三角、
    /// 类型名是不是下拉），只有人眼能答；这条与 L0 的验证记录同一类，写进了包 README。
    /// </para>
    /// </remarks>
    internal sealed class ManagedReferenceTerminalDrawer : XInspectorDrawer
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
                // 与另外两个末端同款：明确画一条提示而不是静默跳过。
                EditorGUILayout.HelpBox(
                    $"属性 \"{property.Path}\" 没有 Unity 序列化后端，多态引用末端绘制器无法绘制它。",
                    MessageType.Warning);
                return;
            }

            // 原生那一行：只读罩与它自己的展开器照旧（值仍由 Unity 读写、Undo 由它记）。
            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                EditorGUILayout.PropertyField(entry.SerializedProperty, label, false);
            }

            // 搜索框与复合末端同款：画在只读罩**之外**（输入框本身不是数据）。
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

        /// <summary>只画命中的子节点；一个都没命中时留一条提示。</summary>
        /// <param name="property">多态成员节点。</param>
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
}
