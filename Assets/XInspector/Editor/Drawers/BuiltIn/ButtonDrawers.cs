using System;
using UnityEditor;
using UnityEngine;
using XInspector.Internal;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="ButtonAttribute"/>：把方法节点画成一个按钮。
    /// <para>
    /// 它是**替换型**绘制器——画完不调下一个。方法节点没有值，链上那个方法末端只是结构性兜底，
    /// 调它只会画出一句「没有绘制器」的假告警。
    /// </para>
    /// <para>
    /// 权重取 <c>0</c>（与值绘制器同档）：这样 <c>[GUIColor]</c>／<c>[Indent]</c>／
    /// <c>[LabelText]</c> 那些属性带的绘制器都包在按钮外面，与它们作用于字段时行为一致。
    /// </para>
    /// </summary>
    [DrawerPriority(0d)]
    internal sealed class ButtonDrawer : AttributeDrawer<ButtonAttribute>
    {
        #region Private Fields

        /// <summary>参数折叠箭头的宽度（像素）。</summary>
        private const float FoldoutWidth = 14f;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property,
            ButtonAttribute attribute,
            GUIContent label)
        {
            var state = property.State.Get<ButtonState>();

            if (state == null)
            {
                // 处理器没跑到——属于构建期故障，明确说出来而不是画个不能点的按钮。
                EditorGUILayout.HelpBox(
                    $"按钮 \"{property.Path}\" 没有构建期状态，XInspector 无法绘制它。",
                    MessageType.Warning);
                return;
            }

            var height = ResolveHeight(property, attribute);
            var enabled = state.Reason == null && !property.State.IsReadOnly;

            if (state.Parameters.Length == 0)
            {
                using (new EditorGUI.DisabledScope(!enabled))
                {
                    if (GUILayout.Button(label, GUILayout.Height(height), GUILayout.ExpandWidth(true)))
                    {
                        Click(property, state);
                    }
                }
            }
            else
            {
                DrawParameterized(property, state, label, height, enabled);
            }

            if (state.Reason != null)
            {
                // 「画了个按钮但点了没反应」是本包最想避免的现象，故原因必须写在脸上。
                EditorGUILayout.HelpBox(state.Reason, MessageType.Warning);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>带参数的按钮：一行放折叠箭头与按钮，展开后在下方缩进画参数。</summary>
        /// <param name="property">方法节点。</param>
        /// <param name="state">按钮状态。</param>
        /// <param name="label">按钮文本。</param>
        /// <param name="height">按钮高度。</param>
        /// <param name="enabled">是否可点。</param>
        /// <remarks>
        /// 形态固定为 Odin 的 <c>CompactBox</c>——按钮与折叠箭头同一行。Odin 把它列为
        /// 「带参方法的默认样式」，另外两种样式（盒式、纯折叠）本包不提供，
        /// 因为三种只是同一件事的摆法差异。
        /// </remarks>
        private static void DrawParameterized(
            InspectorProperty property,
            ButtonState state,
            GUIContent label,
            float height,
            bool enabled)
        {
            var rect = EditorGUILayout.GetControlRect(false, height);
            var arrowRect = new Rect(rect.x, rect.y, FoldoutWidth, rect.height);
            rect.xMin += FoldoutWidth;

            state.Expanded = EditorGUI.Foldout(arrowRect, state.Expanded, GUIContent.none, true);

            using (new EditorGUI.DisabledScope(!enabled))
            {
                if (GUI.Button(rect, label))
                {
                    Click(property, state);
                }
            }

            if (!state.Expanded)
            {
                return;
            }

            EditorGUI.indentLevel++;

            try
            {
                for (var i = 0; i < state.Parameters.Length; i++)
                {
                    DrawArgument(state.Parameters[i].Name, state.Parameters[i].ParameterType, state, i);
                }
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>画一个参数输入框，**只在值真变了时**才写回缓冲。</summary>
        /// <param name="name">参数名。</param>
        /// <param name="type">参数类型。</param>
        /// <param name="state">按钮状态。</param>
        /// <param name="index">参数下标。</param>
        /// <remarks>
        /// <b>「只在变了才写回」不是优化而是纪律。</b> 缓冲是 <c>object[]</c>，每次写回都要装箱；
        /// 每帧无脑写回等于每帧给每个参数分配一个箱子。类型化读回、比较之后再写，
        /// 稳态下绘制路径一次分配都没有。
        /// </remarks>
        private static void DrawArgument(string name, Type type, ButtonState state, int index)
        {
            var current = state.Arguments[index];

            if (type == typeof(bool))
            {
                var value = (bool)current;
                var next = EditorGUILayout.Toggle(name, value);

                if (next != value)
                {
                    state.Arguments[index] = next;
                }

                return;
            }

            if (type == typeof(int))
            {
                var value = (int)current;
                var next = EditorGUILayout.IntField(name, value);

                if (next != value)
                {
                    state.Arguments[index] = next;
                }

                return;
            }

            if (type == typeof(float))
            {
                var value = (float)current;
                var next = EditorGUILayout.FloatField(name, value);

                if (!next.Equals(value))
                {
                    state.Arguments[index] = next;
                }

                return;
            }

            if (type == typeof(double))
            {
                var value = (double)current;
                var next = EditorGUILayout.DoubleField(name, value);

                if (!next.Equals(value))
                {
                    state.Arguments[index] = next;
                }

                return;
            }

            if (type == typeof(string))
            {
                var value = (string)current ?? string.Empty;
                var next = EditorGUILayout.TextField(name, value);

                if (!string.Equals(next, value, StringComparison.Ordinal))
                {
                    state.Arguments[index] = next;
                }

                return;
            }

            if (type.IsEnum)
            {
                var value = (Enum)current;
                var next = EditorGUILayout.EnumPopup(name, value);

                if (!Equals(next, value))
                {
                    state.Arguments[index] = next;
                }

                return;
            }

            if (typeof(Object).IsAssignableFrom(type))
            {
                var value = (Object)current;
                var next = EditorGUILayout.ObjectField(name, value, type, true);

                if (next != value)
                {
                    state.Arguments[index] = next;
                }

                return;
            }

            if (type == typeof(Vector2))
            {
                DrawVector(ref current, index, state, EditorGUILayout.Vector2Field(name, (Vector2)current));
                return;
            }

            if (type == typeof(Vector3))
            {
                DrawVector(ref current, index, state, EditorGUILayout.Vector3Field(name, (Vector3)current));
                return;
            }

            if (type == typeof(Vector4))
            {
                DrawVector(ref current, index, state, EditorGUILayout.Vector4Field(name, (Vector4)current));
                return;
            }

            if (type == typeof(Color))
            {
                var value = (Color)current;
                var next = EditorGUILayout.ColorField(name, value);

                if (next != value)
                {
                    state.Arguments[index] = next;
                }

                return;
            }

            if (type == typeof(Rect))
            {
                var value = (Rect)current;
                var next = EditorGUILayout.RectField(name, value);

                if (next != value)
                {
                    state.Arguments[index] = next;
                }
            }
        }

        /// <summary>把向量类的绘制结果写回缓冲。</summary>
        /// <typeparam name="T">向量类型。</typeparam>
        /// <param name="current">缓冲里的旧值。</param>
        /// <param name="index">参数下标。</param>
        /// <param name="state">按钮状态。</param>
        /// <param name="next">控件返回的新值。</param>
        private static void DrawVector<T>(ref object current, int index, ButtonState state, T next)
            where T : struct, IEquatable<T>
        {
            if (!next.Equals((T)current))
            {
                state.Arguments[index] = next;
            }
        }

        /// <summary>点击：对每个目标调用各自解析出来的那份方法。</summary>
        /// <param name="property">方法节点。</param>
        /// <param name="state">按钮状态。</param>
        private static void Click(InspectorProperty property, ButtonState state)
        {
            var tree = property.Owner;

            MethodInvoker.Invoke(
                state.Methods,
                tree?.Targets,
                state.Scopes,
                state.Arguments,
                tree?.UndoEnabled ?? false,
                property.Label.text);
        }

        /// <summary>
        /// 取按钮高度：**组上的设定只在按钮自己没表态时生效**。
        /// </summary>
        /// <param name="property">方法节点。</param>
        /// <param name="attribute">按钮特性。</param>
        /// <returns>高度（像素）。</returns>
        /// <remarks>
        /// 按钮组的设定挂在**分组节点**上，而按钮是它的子节点——故这里往上看一层。
        /// 嵌套分组（<c>"A/B"</c>）时最近的组赢，与分组的层叠语义一致。
        /// </remarks>
        internal static float ResolveHeight(InspectorProperty property, ButtonAttribute attribute)
        {
            var parent = property.Parent;

            // 组上的设定**只在按钮自己没表态时**才轮到——判据统一放最外层，
            // 免得后面每加一种组都得多写一次「按钮优先」。
            if (parent != null && !attribute.SizeHasValue)
            {
                var group = parent.GetAttribute<ButtonGroupAttribute>();

                if (group != null && group.ButtonHeight > 0)
                {
                    return group.ButtonHeight;
                }

                var responsive = parent.GetAttribute<ResponsiveButtonGroupAttribute>();

                if (responsive != null)
                {
                    return ButtonSizeMetrics.HeightOf(responsive.DefaultButtonSize);
                }
            }

            return ButtonSizeMetrics.HeightOf(attribute.Size);
        }

        #endregion
    }
}
