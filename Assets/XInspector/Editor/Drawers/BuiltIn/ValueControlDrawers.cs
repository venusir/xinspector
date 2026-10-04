using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="MultiLinePropertyAttribute"/>：字符串画成多行文本域。
    /// </summary>
    /// <remarks>
    /// 替换型绘制器：绕过末端那层只读禁用罩，故自己处理 <see cref="PropertyState.IsReadOnly"/>。
    /// 标签画在文本域**上方**（多行控件与标签并排会挤成半宽）。
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class MultiLinePropertyDrawer : AttributeDrawer<MultiLinePropertyAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, MultiLinePropertyAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.String)
            {
                DrawerWarnings.Once(property, nameof(MultiLinePropertyDrawer),
                    DrawerWarnings.TypeMismatch(property, "[MultiLineProperty]", "字符串"));
                CallNextDrawer(property, label);
                return;
            }

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                if (label != null && label != GUIContent.none)
                {
                    EditorGUILayout.LabelField(label);
                }

                var lines = Mathf.Max(1, attribute.Lines);
                var height = EditorGUIUtility.singleLineHeight * lines + EditorGUIUtility.standardVerticalSpacing * 2f;

                serializedProperty.stringValue = EditorGUILayout.TextArea(
                    serializedProperty.stringValue, EditorStyles.textArea, GUILayout.Height(height));
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="DelayedPropertyAttribute"/>：值只在回车或失焦时提交。
    /// </summary>
    /// <remarks>
    /// 支持 <c>int</c> / <c>float</c> / <c>double</c> / <c>string</c>。
    /// <c>long</c> 超出 <c>int</c> 范围时告警并退回普通绘制——延迟控件只有 <c>int</c> 版本，
    /// 硬用会把高位悄悄截掉（那是「看到的值不是真实的值」，比不做更糟）。
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class DelayedPropertyDrawer : AttributeDrawer<DelayedPropertyAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, DelayedPropertyAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;
            var valueType = property.ValueEntry?.ValueType;

            if (serializedProperty == null)
            {
                CallNextDrawer(property, label);
                return;
            }

            switch (serializedProperty.propertyType)
            {
                case SerializedPropertyType.String:
                    using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
                    {
                        serializedProperty.stringValue = EditorGUILayout.DelayedTextField(label, serializedProperty.stringValue);
                    }

                    return;

                case SerializedPropertyType.Float:
                    using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
                    {
                        if (valueType == typeof(double))
                        {
                            serializedProperty.doubleValue = EditorGUILayout.DelayedDoubleField(label, serializedProperty.doubleValue);
                        }
                        else
                        {
                            serializedProperty.floatValue = EditorGUILayout.DelayedFloatField(label, serializedProperty.floatValue);
                        }
                    }

                    return;

                case SerializedPropertyType.Integer when serializedProperty.longValue >= int.MinValue &&
                                                          serializedProperty.longValue <= int.MaxValue:
                    using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
                    {
                        serializedProperty.longValue = EditorGUILayout.DelayedIntField(label, (int)serializedProperty.longValue);
                    }

                    return;

                default:
                    DrawerWarnings.Once(property, nameof(DelayedPropertyDrawer),
                        $"[XInspector] 属性「{property.Path}」上的 [DelayedProperty] 只支持 int、float、double、string" +
                        $"（long 仅在值处于 int 范围内时可画），当前值不在支持范围内，该特性已忽略、字段退回普通绘制。");
                    CallNextDrawer(property, label);
                    return;
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="EnumPagingAttribute"/>：枚举下拉框 + 前后翻页按钮（末尾绕回开头）。
    /// </summary>
    /// <remarks>
    /// <c>[Flags]</c> 位标志没有「上一项/下一项」的顺序语义，遇flags告警并退回普通绘制。
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class EnumPagingDrawer : AttributeDrawer<EnumPagingAttribute>
    {
        #region Private Fields

        private static readonly GUIContent PreviousLabel = new GUIContent("◀", "上一项");
        private static readonly GUIContent NextLabel = new GUIContent("▶", "下一项");

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, EnumPagingAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.Enum)
            {
                DrawerWarnings.Once(property, nameof(EnumPagingDrawer),
                    DrawerWarnings.TypeMismatch(property, "[EnumPaging]", "枚举"));
                CallNextDrawer(property, label);
                return;
            }

            if (EnumSupport.IsFlags(property))
            {
                DrawerWarnings.Once(property, nameof(EnumPagingDrawer) + ".flags",
                    $"[XInspector] 属性「{property.Path}」上的 [EnumPaging] 不适用于 [Flags] 枚举" +
                    "（位标志没有「上一项/下一项」的顺序语义），该特性已忽略、字段退回普通绘制。");
                CallNextDrawer(property, label);
                return;
            }

            var names = serializedProperty.enumNames;
            if (names == null || names.Length == 0)
            {
                CallNextDrawer(property, label);
                return;
            }

            var index = Mathf.Clamp(serializedProperty.enumValueIndex, 0, names.Length - 1);

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (label != null && label != GUIContent.none)
                {
                    EditorGUILayout.PrefixLabel(label);
                }

                if (GUILayout.Button(PreviousLabel, EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                {
                    serializedProperty.enumValueIndex = (index + names.Length - 1) % names.Length;
                }

                var selected = EditorGUILayout.Popup(index, names);
                if (selected != index)
                {
                    serializedProperty.enumValueIndex = selected;
                }

                if (GUILayout.Button(NextLabel, EditorStyles.miniButtonRight, GUILayout.Width(22f)))
                {
                    serializedProperty.enumValueIndex = (index + 1) % names.Length;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="PropertyRangeAttribute"/>：数值画成滑块。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 替换型绘制器。写入走 <c>BeginChangeCheck</c>：只有用户真的拖动了才写回，
    /// 避免「每帧把 <c>float</c> 精度的值写回 <c>double</c> 字段」这类精度损耗。
    /// </para>
    /// <para>
    /// 多对象值不一致时**退回普通绘制**：滑块没有混合值形态，
    /// Unity 自己的控件会显示「—」，那比用一个目标的值冒充好。
    /// </para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class PropertyRangeDrawer : AttributeDrawer<PropertyRangeAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, PropertyRangeAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (!ValueClamper.IsClampableType(serializedProperty))
            {
                DrawerWarnings.Once(property, nameof(PropertyRangeDrawer),
                    DrawerWarnings.TypeMismatch(property, "[PropertyRange]", "数值类型（int、long、float、double）"));
                CallNextDrawer(property, label);
                return;
            }

            if (serializedProperty.hasMultipleDifferentValues)
            {
                // 滑块表达不了「各目标值不同」，交给原生控件显示「—」。
                CallNextDrawer(property, label);
                return;
            }

            var isInteger = serializedProperty.propertyType == SerializedPropertyType.Integer;
            if (isInteger &&
                (serializedProperty.longValue < int.MinValue || serializedProperty.longValue > int.MaxValue))
            {
                // 范围守卫放在 BeginChangeCheck 之外：任何早退路径若跳过 EndChangeCheck，
                // 会让别处的变更检测对着一个过期快照比较（症状是「改一个、另一个跟着变脏」）。
                DrawerWarnings.Once(property, nameof(PropertyRangeDrawer) + ".range",
                    $"[XInspector] 属性「{property.Path}」上的 [PropertyRange] 用 int 滑块绘制，" +
                    "当前值超出 int 范围，该特性已忽略、字段退回普通绘制。");
                CallNextDrawer(property, label);
                return;
            }

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                EditorGUI.BeginChangeCheck();

                if (isInteger)
                {
                    var intValue = EditorGUILayout.IntSlider(
                        label, (int)serializedProperty.longValue, (int)attribute.Min, (int)attribute.Max);

                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedProperty.longValue = intValue;
                    }
                }
                else
                {
                    var floatValue = EditorGUILayout.Slider(
                        label, (float)serializedProperty.doubleValue, (float)attribute.Min, (float)attribute.Max);

                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedProperty.doubleValue = floatValue;
                    }
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="WrapAttribute"/>：绘制后把值回绕到范围内。
    /// </summary>
    /// <remarks>
    /// 与钳制族同一时机、同一套跳过规则（多对象不一致、只读、非数值类型），
    /// 判定直接复用 <see cref="ValueClamper.CanClamp"/>——「不悄悄改数据」的前提两条特性一致。
    /// </remarks>
    [DrawerPriority(-300d)]
    internal sealed class WrapDrawer : AttributeDrawer<WrapAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, WrapAttribute attribute, GUIContent label)
        {
            CallNextDrawer(property, label);

            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (!ValueClamper.IsClampableType(serializedProperty))
            {
                DrawerWarnings.Once(property, nameof(WrapDrawer),
                    DrawerWarnings.TypeMismatch(property, "[Wrap]", "数值类型（int、long、float、double）"));
                return;
            }

            if (ValueClamper.CanClamp(serializedProperty, property.State))
            {
                WrapValues.TryWrap(serializedProperty, attribute.Min, attribute.Max);
            }
        }

        #endregion
    }

    /// <summary>
    /// 枚举属性信息的共享缓存（当前只有「是不是 <c>[Flags]</c>」）。
    /// </summary>
    /// <remarks>
    /// 反射只做一次：绘制路径每帧跑，逐帧 <c>Type.IsDefined</c> 违反「反射仅限构建期」
    /// 那条纪律的精神（这里的「构建期」对绘制器来说就是首次绘制）。
    /// </remarks>
    internal static class EnumSupport
    {
        #region Public API

        /// <summary>
        /// 该属性的枚举类型是否带 <c>[Flags]</c>。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <returns>是位标志返回 <c>true</c>。</returns>
        public static bool IsFlags(InspectorProperty property)
        {
            var state = property.State.GetOrCreate<EnumFlagsState>();

            if (!state.Initialized)
            {
                state.Initialized = true;

                var type = property.ValueEntry?.ValueType;
                state.IsFlags = type != null && type.IsEnum && type.IsDefined(typeof(FlagsAttribute), false);
            }

            return state.IsFlags;
        }

        #endregion
    }

    /// <summary>枚举 flags 判定的缓存，挂在 <see cref="PropertyState"/> 上。</summary>
    internal sealed class EnumFlagsState
    {
        /// <summary>是否已解析。</summary>
        public bool Initialized;

        /// <summary>解析结果。</summary>
        public bool IsFlags;
    }

    /// <summary>
    /// <see cref="WrapAttribute"/> 的回绕数学。纯函数，可无头测试。
    /// </summary>
    internal static class WrapValues
    {
        #region Public API

        /// <summary>
        /// 把值回绕到 <c>[min, max)</c>。
        /// </summary>
        /// <param name="value">当前值。</param>
        /// <param name="min">范围下端（含）。</param>
        /// <param name="max">范围上端（不含）。</param>
        /// <returns>回绕后的值；范围非法或值非有限时原样返回。</returns>
        public static double Wrap(double value, double min, double max)
        {
            var range = max - min;
            if (range <= 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                return value;
            }

            // C# 的 % 对负数返回负余数，先归一化到 [0, range) 再加回 min。
            var remainder = (value - min) % range;
            if (remainder < 0)
            {
                remainder += range;
            }

            return min + remainder;
        }

        /// <summary>
        /// 回绕序列化属性上的值。
        /// </summary>
        /// <param name="property">数值属性。</param>
        /// <param name="min">范围下端（含）。</param>
        /// <param name="max">范围上端（不含）。</param>
        /// <returns>改动了值返回 <c>true</c>。</returns>
        public static bool TryWrap(SerializedProperty property, double min, double max)
        {
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                var wrapped = (long)Math.Round(Wrap(property.longValue, min, max));
                if (wrapped == property.longValue)
                {
                    return false;
                }

                property.longValue = wrapped;
                return true;
            }

            var current = property.doubleValue;
            var result = Wrap(current, min, max);
            if (result == current)
            {
                return false;
            }

            property.doubleValue = result;
            return true;
        }

        #endregion
    }
}
