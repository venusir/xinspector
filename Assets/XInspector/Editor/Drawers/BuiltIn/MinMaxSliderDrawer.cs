using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="MinMaxSliderAttribute"/>：<c>Vector2</c> 画成双滑块（x = 下界、y = 上界）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 替换型绘制器（自己画完、不调用下一个），故**必须自己套
    /// <see cref="EditorGUI.DisabledScope"/>**。
    /// </para>
    /// <para>
    /// 边界解析放在构建期的 <see cref="MinMaxSliderProcessor"/> 里，本类只负责
    /// 「读边界 → 画 → 必要时写回」；数学全在纯函数 <see cref="MinMaxRange"/> 里。
    /// </para>
    /// <para>
    /// 多对象值不一致时**退回普通绘制**：双滑块没有混合值形态，而 Unity 自己的控件会显示「—」，
    /// 那比用一个目标的值冒充好（与 <c>[PropertyRange]</c> 同一条规则）。
    /// </para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class MinMaxSliderDrawer : AttributeDrawer<MinMaxSliderAttribute>
    {
        #region Private Fields

        /// <summary>与滑块同排的数值框宽度（像素）。</summary>
        private const float FieldWidth = 48f;

        /// <summary>滑块与数值框之间的间距（像素）。</summary>
        private const float Gap = 2f;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, MinMaxSliderAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.Vector2)
            {
                DrawerWarnings.Once(property, nameof(MinMaxSliderDrawer),
                    DrawerWarnings.TypeMismatch(property, "[MinMaxSlider]", "Vector2"));
                CallNextDrawer(property, label);
                return;
            }

            if (serializedProperty.hasMultipleDifferentValues)
            {
                CallNextDrawer(property, label);
                return;
            }

            var state = property.State.Get<MinMaxSliderState>();
            if (state == null || !state.Resolved)
            {
                // 处理器已经报过一次构建期告警，这里不再重复刷屏。
                CallNextDrawer(property, label);
                return;
            }

            if (!MinMaxRange.TryReadBounds(attribute, state, out var lower, out var upper))
            {
                DrawerWarnings.Once(property, nameof(MinMaxSliderDrawer) + ".bounds",
                    $"[XInspector] 属性「{property.Path}」上的 [MinMaxSlider] 边界不是有限值（NaN 或无穷），" +
                    "滑块无法成形，该特性已忽略、字段退回普通绘制。");
                CallNextDrawer(property, label);
                return;
            }

            DrawSlider(property, serializedProperty, attribute, label, lower, upper);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 画整行：标签 +（可选）两个数值框 + 滑块。
        /// </summary>
        /// <remarks>
        /// 手工切矩形而不是用 <c>HorizontalScope</c>：滑块是 rect 形式、数值框要与它同排，
        /// 手工切出来的宽度才好推理。
        /// </remarks>
        private static void DrawSlider(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            MinMaxSliderAttribute attribute,
            GUIContent label,
            float lower,
            float upper)
        {
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginProperty(rect, label, serializedProperty);

            var content = label != null && label != GUIContent.none ? EditorGUI.PrefixLabel(rect, label) : rect;

            var value = serializedProperty.vector2Value;
            var min = value.x;
            var max = value.y;

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                EditorGUI.BeginChangeCheck();

                var sliderRect = content;
                if (attribute.ShowFields)
                {
                    var fieldWidth = Mathf.Min(FieldWidth, content.width * 0.3f);

                    var minRect = new Rect(content.x, content.y, fieldWidth, content.height);
                    var maxRect = new Rect(content.xMax - fieldWidth, content.y, fieldWidth, content.height);

                    min = EditorGUI.FloatField(minRect, min);
                    max = EditorGUI.FloatField(maxRect, max);

                    sliderRect = new Rect(
                        content.x + fieldWidth + Gap,
                        content.y,
                        Mathf.Max(0f, content.width - (fieldWidth * 2f) - (Gap * 2f)),
                        content.height);
                }

                EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, lower, upper);

                if (EditorGUI.EndChangeCheck())
                {
                    serializedProperty.vector2Value = MinMaxRange.Clamp(new Vector2(min, max), lower, upper);
                }
            }

            EditorGUI.EndProperty();
        }

        #endregion
    }

    /// <summary>
    /// <c>[MinMaxSlider]</c> 的取值与边界数学：全是纯函数，可无头测试。
    /// </summary>
    internal static class MinMaxRange
    {
        #region Public API

        /// <summary>
        /// 把值夹进 <c>[lower, upper]</c>，并保证 <c>x ≤ y</c>。
        /// </summary>
        /// <param name="value">待夹的值（x = 下界、y = 上界）。</param>
        /// <param name="lower">允许的最小值。</param>
        /// <param name="upper">允许的最大值。</param>
        /// <returns>夹好且有序的值。</returns>
        public static Vector2 Clamp(Vector2 value, float lower, float upper)
        {
            var low = Mathf.Clamp(value.x, lower, upper);
            var high = Mathf.Clamp(value.y, lower, upper);

            if (low > high)
            {
                // 两个把手交叉了：交换而不是取平均——用户的意图明显是「把这段区间倒过来」。
                var swap = low;
                low = high;
                high = swap;
            }

            return new Vector2(low, high);
        }

        /// <summary>
        /// 读取当前边界。
        /// </summary>
        /// <param name="attribute">特性（提供字面量形态的上下界）。</param>
        /// <param name="state">构建期解析出的成员句柄。</param>
        /// <param name="lower">下界。</param>
        /// <param name="upper">上界。</param>
        /// <returns>边界可用返回 <c>true</c>；包含非有限值或上下限倒置/相等时返回 <c>false</c>。</returns>
        /// <remarks>
        /// 与字面量形态的构造期校验不同，**动态边界没法在构造期验**（值要到绘制期才知道），
        /// 故这里统一兜底：不猜、不夹、直接判为不可用，由调用方退回普通绘制。
        /// </remarks>
        public static bool TryReadBounds(
            MinMaxSliderAttribute attribute, MinMaxSliderState state, out float lower, out float upper)
        {
            lower = 0f;
            upper = 0f;

            if (attribute.MinMaxValueGetter != null)
            {
                if (state.MinMaxGetter == null)
                {
                    return false;
                }

                var bounds = state.MinMaxGetter.vector2Value;
                lower = bounds.x;
                upper = bounds.y;
            }
            else
            {
                lower = state.MinGetter != null ? state.MinGetter.floatValue : attribute.MinValue;
                upper = state.MaxGetter != null ? state.MaxGetter.floatValue : attribute.MaxValue;
            }

            if (float.IsNaN(lower) || float.IsNaN(upper) ||
                float.IsInfinity(lower) || float.IsInfinity(upper))
            {
                return false;
            }

            if (upper < lower)
            {
                // 动态成员的值可能被别的代码改反：换过来比整条特性失效好。
                var swap = lower;
                lower = upper;
                upper = swap;
            }

            // 上下限相等时滑块无从拖动，但它也不是错的——照常画，Unity 会画成一根不动的条。
            return true;
        }

        #endregion
    }
}
