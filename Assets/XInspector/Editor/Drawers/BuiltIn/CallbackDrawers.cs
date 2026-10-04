using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="OnInspectorGUIAttribute"/>：调用目标方法，由它自己画一段界面。
    /// <para>
    /// 权重 <c>0</c>：它与按钮同档，于是属性带的绘制器（<c>[Indent]</c> 之类）照常包在外面。
    /// 画完**不调下一个**——方法节点没有值，链尾那个末端只是结构性兜底。
    /// </para>
    /// </summary>
    [DrawerPriority(0d)]
    internal sealed class OnInspectorGUIDrawer : AttributeDrawer<OnInspectorGUIAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        /// <remarks>
        /// <b>只执行一次，且用第一个目标。</b> 多选时按目标各画一遍只会把同一段界面叠 N 次
        /// ——绘制是「一次」，而按钮那种「调用」才是「每目标一次」。
        /// 与 <c>[ChildGameObjectsOnly]</c>「多选时以第一个目标为准」是同一条处置。
        /// </remarks>
        protected override void DrawPropertyLayout(
            InspectorProperty property,
            OnInspectorGUIAttribute attribute,
            GUIContent label)
        {
            var state = property.State.Get<CustomGuiState>();

            if (state?.SingleMethod == null || state.SingleTarget == null)
            {
                EditorGUILayout.HelpBox(
                    state?.Reason ?? $"方法节点 \"{property.Path}\" 没有构建期状态，XInspector 无法绘制它。",
                    MessageType.Warning);
                return;
            }

            // 用构建期备好的单元素数组：绘制是每帧路径，这里连一个单元素数组都不该现建。
            MethodInvoker.Invoke(state.SingleMethod, state.SingleTarget, null, false, property.Path);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="CustomContextMenuAttribute"/>：在本字段上右键时弹出菜单。
    /// <para>
    /// 权重 <c>-50</c>：既要在值绘制器之外（否则替换型值绘制器会让它拿不到矩形），
    /// 又要在属性带之内。
    /// </para>
    /// </summary>
    [DrawerPriority(-50d)]
    internal sealed class CustomContextMenuDrawer : AttributeDrawer<CustomContextMenuAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property,
            CustomContextMenuAttribute attribute,
            GUIContent label)
        {
            CallNextDrawer(property, label);

            var state = property.State.Get<ContextMenuState>();

            // 一个字段挂多项时，每项各有一格绘制器，但右键只该弹一次菜单——
            // 由第一项当处理者，其余只当数据。见 ContextMenuState.Owner。
            if (state == null || state.Entries.Count == 0 || !ReferenceEquals(state.Owner, attribute))
            {
                return;
            }

            var current = Event.current;

            if (current == null || current.type != EventType.ContextClick)
            {
                return;
            }

            // 刚画完的就是这个字段的矩形——不落在里面就不是冲它来的。
            var rect = GUILayoutUtility.GetLastRect();

            if (!rect.Contains(current.mousePosition))
            {
                return;
            }

            current.Use();
            ShowMenu(property, state, rect);
        }

        #endregion

        #region Private Helpers

        /// <summary>弹出菜单。</summary>
        /// <param name="property">目标属性。</param>
        /// <param name="state">菜单状态。</param>
        /// <param name="rect">菜单的锚点矩形。</param>
        private static void ShowMenu(InspectorProperty property, ContextMenuState state, Rect rect)
        {
            var entries = state.Entries;
            var options = new GUIContent[entries.Count];
            var methods = new MethodInfo[entries.Count][];
            var names = new string[entries.Count];
            var callable = 0;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var enabled = entry.Methods != null;

                options[i] = new GUIContent(entry.MenuItem);
                methods[i] = entry.Methods;
                names[i] = entry.MenuItem;

                if (enabled)
                {
                    callable++;
                }
            }

            if (callable == 0)
            {
                // 一项都调不了：菜单照样弹，但全是灰的——比「右键没反应」好归因。
                for (var i = 0; i < options.Length; i++)
                {
                    options[i].tooltip = "这个菜单项要调用的方法没解析到，见 Console 里的告警。";
                }
            }

            var tree = property.Owner;

            var payload = new MenuPayload
            {
                Methods = methods,
                Names = names,
                Targets = tree?.Targets,
                UndoEnabled = tree?.UndoEnabled ?? false,
            };

            EditorUtility.DisplayCustomMenu(rect, options, -1, OnMenuSelected, payload);
        }

        /// <summary>菜单选中回调。</summary>
        /// <param name="userData">载荷。</param>
        /// <param name="options">选项文本。</param>
        /// <param name="selected">选中的下标。</param>
        /// <remarks>
        /// 静态方法 + 载荷对象，而不是匿名函数：委托只在点击时创建一次，
        /// 但静态方法连那一次都不需要。
        /// </remarks>
        private static void OnMenuSelected(object userData, string[] options, int selected)
        {
            if (!(userData is MenuPayload payload) || selected < 0 || selected >= payload.Methods.Length)
            {
                return;
            }

            MethodInvoker.Invoke(
                payload.Methods[selected],
                payload.Targets,
                null,
                payload.UndoEnabled,
                payload.Names[selected]);
        }

        /// <summary>菜单回调的载荷。</summary>
        private sealed class MenuPayload
        {
            /// <summary>逐菜单项、逐目标的方法。</summary>
            public MethodInfo[][] Methods;

            /// <summary>逐菜单项的文本，用作撤销栈里的名字。</summary>
            public string[] Names;

            /// <summary>目标对象。</summary>
            public object[] Targets;

            /// <summary>是否记 Undo。</summary>
            public bool UndoEnabled;
        }

        #endregion
    }

    /// <summary>
    /// <see cref="OnValueChangedAttribute"/>：值变了就调用指定的方法。
    /// <para>
    /// 判据是**绘制这个字段的那一趟里值前后不一致**，因此不需要跨帧记旧值，
    /// 也就没有「第一帧误报一次」的问题。取快照走类型化路径，不装箱——
    /// 这一段是每帧路径。
    /// </para>
    /// </summary>
    [DrawerPriority(-50d)]
    internal sealed class OnValueChangedDrawer : AttributeDrawer<OnValueChangedAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property,
            OnValueChangedAttribute attribute,
            GUIContent label)
        {
            var state = property.State.Get<ValueChangedState>();
            var entry = state == null ? null : FindEntry(state, attribute);
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (entry?.Methods == null)
            {
                // 「没解析到方法」由处理器负责告警（它才知道找没找到、为什么没找到）。
                CallNextDrawer(property, label);
                return;
            }

            if (serializedProperty == null)
            {
                // 这条曾经是静默的：标在 [ShowInInspector] 只读成员上的 [OnValueChanged]
                // 既不触发也没有任何提示。「改了但没反应」比「压根没监听」难查得多。
                DrawerWarnings.Once(property, nameof(OnValueChangedDrawer),
                    DrawerWarnings.TypeMismatch(property, "[OnValueChanged]", "会变化的值"));
                CallNextDrawer(property, label);
                return;
            }

            if (!ValueSnapshot.IsSupported(serializedProperty.propertyType))
            {
                // 不支持就明说，别假装监听着——「改了但没反应」比「压根没监听」难查得多。
                DrawerWarnings.Once(
                    property,
                    "OnValueChanged." + serializedProperty.propertyType,
                    $"[XInspector] 属性「{property.Path}」的值类型（{serializedProperty.propertyType}）"
                    + "不支持变化检测，[OnValueChanged] 不会触发。");

                CallNextDrawer(property, label);
                return;
            }

            var before = ValueSnapshot.Capture(serializedProperty);

            CallNextDrawer(property, label);

            var after = ValueSnapshot.Capture(serializedProperty);

            if (!before.DiffersFrom(after))
            {
                return;
            }

            var tree = property.Owner;

            MethodInvoker.Invoke(
                entry.Methods,
                tree?.Targets,
                null,
                tree?.UndoEnabled ?? false,
                label.text);
        }

        #endregion

        #region Private Helpers

        /// <summary>按引用找出这一格绘制器对应的那项监听。</summary>
        /// <param name="state">状态。</param>
        /// <param name="attribute">当前的特性实例。</param>
        /// <returns>对应的项；找不到时返回 <c>null</c>。</returns>
        private static ValueChangedEntry FindEntry(ValueChangedState state, OnValueChangedAttribute attribute)
        {
            var entries = state.Entries;

            for (var i = 0; i < entries.Count; i++)
            {
                if (ReferenceEquals(entries[i].Attribute, attribute))
                {
                    return entries[i];
                }
            }

            return null;
        }

        #endregion
    }
}
