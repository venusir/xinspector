using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="DisplayAsStringAttribute"/>：把值画成只读文本。
    /// <para>
    /// 它是「替换型值绘制器」的第一个实例：画完自己就结束，**不调用下一个绘制器**。
    /// 这正是绘制器链有意支持的能力——「不调用下一个」等于把内侧藏起来，
    /// 于是「值控件换成文本」不需要任何特殊机制。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 不处理 <see cref="PropertyState.IsReadOnly"/>：它本来就画不出可编辑的东西，
    /// 只读与可编辑的外观一致。这一点与另外三个替换型绘制器不同（它们必须自己上禁用罩，
    /// 因为末端绘制器那层罩被绕过了）。
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class DisplayAsStringDrawer : AttributeDrawer<DisplayAsStringAttribute>
    {
        #region Private Fields

        private static GUIStyle _wrappedLabel;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, DisplayAsStringAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (!ValueTextFormatter.IsSupported(serializedProperty))
            {
                DrawerWarnings.Once(property, nameof(DisplayAsStringDrawer),
                    DrawerWarnings.TypeMismatch(property, "[DisplayAsString]",
                        "简单类型（数值、字符串、bool、枚举、对象引用）"));
                CallNextDrawer(property, label);
                return;
            }

            var text = ValueTextFormatter.Format(serializedProperty);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (label != null && label != GUIContent.none)
                {
                    EditorGUILayout.PrefixLabel(label);
                }

                if (attribute.FontSize <= 0 && !attribute.EnableRichText)
                {
                    // 没提字号与富文本：**逐字沿用从前的两条路**（共享静态样式、零额外分配）。
                    if (attribute.Overflow)
                    {
                        EditorGUILayout.LabelField(new GUIContent(text), WrappedLabel);
                    }
                    else
                    {
                        // 可选中复制；高度限定为一行，长文本被裁切（而不是撑开排布）。
                        EditorGUILayout.SelectableLabel(
                            text, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    }

                    return;
                }

                var style = StyleFor(property, attribute);

                if (attribute.Overflow)
                {
                    EditorGUILayout.LabelField(new GUIContent(text), style);
                }
                else
                {
                    EditorGUILayout.SelectableLabel(text, style, GUILayout.Height(RowHeightOf(style)));
                }
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>允许折行的标签样式（<see cref="EditorStyles.label"/> 默认不折行）。</summary>
        /// <remarks>
        /// 惰性创建而非静态初始化：静态构造在批处理（无 GUI 上下文）下也会被触发
        /// （注册表扫描会实例化绘制器），那时创建样式不安全。
        /// </remarks>
        private static GUIStyle WrappedLabel => _wrappedLabel ??= new GUIStyle(EditorStyles.label) { wordWrap = true };

        /// <summary>
        /// 取这一行该用的样式（带字号 / 富文本的那两条路）。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="attribute">特性实例。</param>
        /// <returns>样式。</returns>
        /// <remarks>
        /// <b>样式缓存在 <see cref="PropertyState"/> 上，不放绘制器字段</b>——绘制器是无状态共享
        /// 单例（本包硬约束），放字段的症状是「改一个字段的字号、别的字段跟着变」。
        /// 字号/富文本与缓存不符时重建：同一属性上换了特性参数（重建树）要跟着走。
        /// </remarks>
        private static GUIStyle StyleFor(InspectorProperty property, DisplayAsStringAttribute attribute)
        {
            var state = property.State.GetOrCreate<DisplayAsStringState>();

            if (state.Style == null || state.FontSize != attribute.FontSize ||
                state.RichText != attribute.EnableRichText || state.Wrapped != attribute.Overflow)
            {
                state.Style = new GUIStyle(EditorStyles.label)
                {
                    fontSize = attribute.FontSize, // 0 ＝ 用编辑器默认字号，正是 GUIStyle 的语义
                    richText = attribute.EnableRichText,
                    wordWrap = attribute.Overflow,
                };
                state.FontSize = attribute.FontSize;
                state.RichText = attribute.EnableRichText;
                state.Wrapped = attribute.Overflow;
            }

            return state.Style;
        }

        /// <summary>这一行要多高：字号放大时行高跟着长，但**不小于**默认单行高。</summary>
        /// <param name="style">这一行用的样式。</param>
        /// <returns>像素高度。</returns>
        private static float RowHeightOf(GUIStyle style)
        {
            return style.fontSize > 0
                ? Mathf.Max(EditorGUIUtility.singleLineHeight, style.fontSize * 1.4f)
                : EditorGUIUtility.singleLineHeight;
        }

        #endregion
    }

    /// <summary>
    /// <c>[DisplayAsString]</c> 的每属性状态：按字号 / 富文本缓存的标签样式。
    /// </summary>
    /// <remarks>
    /// 默认路径（不指定字号也不开富文本）**不走这里**——那时用的是共享静态样式，
    /// 与从前逐字一致；这个状态只为「提了要求」的那些属性而建。
    /// </remarks>
    internal sealed class DisplayAsStringState
    {
        /// <summary>缓存的样式；尚未建过为 <c>null</c>。</summary>
        public GUIStyle Style;

        /// <summary>建这份样式时的字号。</summary>
        public int FontSize;

        /// <summary>建这份样式时的富文本开关。</summary>
        public bool RichText;

        /// <summary>建这份样式时的折行开关。</summary>
        public bool Wrapped;
    }

    /// <summary>
    /// <see cref="ToggleLeftAttribute"/>：Bool 画成「开关在左、标签在右」。
    /// </summary>
    /// <remarks>
    /// 替换型绘制器，绕过末端的那层禁用罩，故**必须自己处理只读**
    /// （<c>[ReadOnly]</c> / <c>[DisableIf]</c> 都要在它上面照常生效）。
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class ToggleLeftDrawer : AttributeDrawer<ToggleLeftAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, ToggleLeftAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.Boolean)
            {
                DrawerWarnings.Once(property, nameof(ToggleLeftDrawer),
                    DrawerWarnings.TypeMismatch(property, "[ToggleLeft]", "bool"));
                CallNextDrawer(property, label);
                return;
            }

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                var previousMixed = EditorGUI.showMixedValue;
                try
                {
                    EditorGUI.showMixedValue = serializedProperty.hasMultipleDifferentValues;
                    serializedProperty.boolValue = EditorGUILayout.ToggleLeft(label, serializedProperty.boolValue);
                }
                finally
                {
                    // showMixedValue 是全局状态，必须还原——漏还原会让**别的 Inspector** 显示成混合态。
                    EditorGUI.showMixedValue = previousMixed;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="ProgressBarAttribute"/>：数值画成可拖动的进度条。
    /// <para>
    /// 数学（取值、归一化、鼠标位置换算）全在纯函数 <see cref="ProgressBarValues"/> 里，
    /// 本类只做「读事件 → 写值 → 画」三件事。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 与 Odin 的一处刻意差异：**不钳制值**，只把条子画到端点为止。
    /// 想连数据一起钳制请配 <c>[MinValue]</c>/<c>[MaxValue]</c>——
    /// 绘制器悄悄改数据是另一类特性（钳制族）的职责，混在一起会让「谁改了我的值」无从追查。
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class ProgressBarDrawer : AttributeDrawer<ProgressBarAttribute>
    {
        #region Private Fields

        private static GUIStyle _centeredLabel;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, ProgressBarAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (!ProgressBarValues.IsSupported(serializedProperty))
            {
                DrawerWarnings.Once(property, nameof(ProgressBarDrawer),
                    DrawerWarnings.TypeMismatch(property, "[ProgressBar]", "数值类型（int、long、float、double）"));
                CallNextDrawer(property, label);
                return;
            }

            var height = attribute.Height > 0f ? attribute.Height : EditorGUIUtility.singleLineHeight;
            var rect = EditorGUILayout.GetControlRect(true, height);

            EditorGUI.BeginProperty(rect, label, serializedProperty);

            if (label != null && label != GUIContent.none)
            {
                rect = EditorGUI.PrefixLabel(rect, label);
            }

            // 控件 ID 无条件分配：GetControlID 是**有状态**的调用，条件调用会让后续控件的
            // ID 随本属性的只读状态漂移，而那正是热点控件（hotControl）串线的来源。
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var interactive = !property.State.IsReadOnly && !serializedProperty.hasMultipleDifferentValues;

            if (interactive && ProgressBarValues.TryHandleMouse(rect, controlId, out var dragged))
            {
                ProgressBarValues.Write(serializedProperty, ProgressBarValues.FromNormalized(dragged, attribute.Min, attribute.Max));
            }

            var value = ProgressBarValues.Read(serializedProperty);
            DrawBar(rect, ProgressBarValues.Normalize(value, attribute.Min, attribute.Max), value, serializedProperty, attribute);

            EditorGUI.EndProperty();
        }

        #endregion

        #region Private Helpers

        /// <summary>画条本体：底色、填充、分段线、数值文本。</summary>
        /// <param name="rect">条占用的矩形。</param>
        /// <param name="normalized">归一化后的填充比例（0–1）。</param>
        /// <param name="value">当前值（用于显示文本）。</param>
        /// <param name="serializedProperty">底层序列化属性（判断整数还是浮点）。</param>
        /// <param name="attribute">进度条特性。</param>
        /// <remarks>
        /// 不用 <c>EditorGUI.ProgressBar</c>：它固定用皮肤里的蓝色，吃不到构造参数给的颜色。
        /// 自绘只有几行，且深浅色皮肤各取一个中性底色。
        /// </remarks>
        private static void DrawBar(
            Rect rect,
            float normalized,
            double value,
            SerializedProperty serializedProperty,
            ProgressBarAttribute attribute)
        {
            var background = EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.22f, 0.22f)
                : new Color(0.75f, 0.75f, 0.75f);

            EditorGUI.DrawRect(rect, background);

            if (normalized > 0f)
            {
                EditorGUI.DrawRect(
                    new Rect(rect.x, rect.y, rect.width * normalized, rect.height),
                    new Color(attribute.R, attribute.G, attribute.B));
            }

            if (attribute.Segmented)
            {
                var separator = EditorGUIUtility.isProSkin
                    ? new Color(0f, 0f, 0f, 0.35f)
                    : new Color(1f, 1f, 1f, 0.55f);

                for (var i = 1; i < 4; i++)
                {
                    EditorGUI.DrawRect(new Rect(rect.x + rect.width * (i * 0.25f), rect.y, 1f, rect.height), separator);
                }
            }

            if (attribute.DrawValueLabel)
            {
                var isInteger = serializedProperty.propertyType == SerializedPropertyType.Integer;
                EditorGUI.LabelField(rect, ProgressBarValues.FormatValue(value, isInteger), CenteredLabel);
            }
        }

        /// <summary>居中的白字标签样式。</summary>
        private static GUIStyle CenteredLabel => _centeredLabel ??= new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
        };

        #endregion
    }

    /// <summary>
    /// <see cref="EnumToggleButtonsAttribute"/>：枚举画成一排按钮。
    /// <para>
    /// 普通枚举用工具栏（单选）；<c>[Flags]</c> 枚举逐位多选——工具栏是单选控件，
    /// 表达不了位组合，故退化为一行 <c>ToggleLeft</c>。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 反射（<c>Enum.GetValues</c>）只在**首次绘制**时做一次，结果缓存在
    /// <see cref="EnumToggleButtonsState"/>；之后每帧只读几个数组。
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class EnumToggleButtonsDrawer : AttributeDrawer<EnumToggleButtonsAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, EnumToggleButtonsAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.Enum)
            {
                DrawerWarnings.Once(property, nameof(EnumToggleButtonsDrawer),
                    DrawerWarnings.TypeMismatch(property, "[EnumToggleButtons]", "枚举"));
                CallNextDrawer(property, label);
                return;
            }

            var state = EnumToggleButtonsState.For(property, serializedProperty);
            if (state == null)
            {
                DrawerWarnings.Once(property, nameof(EnumToggleButtonsDrawer) + ".命名",
                    $"[XInspector] 属性「{property.Path}」上的 [EnumToggleButtons] 无法解析枚举成员名，" +
                    "该特性已忽略、字段退回普通绘制。");
                CallNextDrawer(property, label);
                return;
            }

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (label != null && label != GUIContent.none)
                {
                    EditorGUILayout.PrefixLabel(label);
                }

                if (state.IsFlags)
                {
                    DrawFlagToggles(serializedProperty, state);
                }
                else
                {
                    var selected = GUILayout.Toolbar(serializedProperty.enumValueIndex, state.Names);
                    if (selected != serializedProperty.enumValueIndex)
                    {
                        serializedProperty.enumValueIndex = selected;
                    }
                }
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>逐位多选：每个成员一行 <c>ToggleLeft</c>，按位翻转。</summary>
        /// <param name="serializedProperty">枚举属性。</param>
        /// <param name="state">枚举的解析结果。</param>
        private static void DrawFlagToggles(SerializedProperty serializedProperty, EnumToggleButtonsState state)
        {
            var value = serializedProperty.longValue;

            for (var i = 0; i < state.Names.Length; i++)
            {
                var mask = state.Masks[i];
                var selected = mask != 0L && (value & mask) == mask;

                if (EditorGUILayout.ToggleLeft(state.Names[i], selected, GUILayout.Width(state.Widths[i])) != selected)
                {
                    value = selected ? value & ~mask : value | mask;
                }
            }

            if (value != serializedProperty.longValue)
            {
                serializedProperty.longValue = value;
            }
        }

        #endregion
    }

    /// <summary>
    /// 值的文本化：把 <see cref="SerializedProperty"/> 转成显示用字符串。
    /// <para>
    /// 不走 <c>GetValue()</c> 装箱：那条路对枚举只给下标、对多对象不一致会抛，
    /// 而这里要的恰好是「枚举名」与「不一致时显示 —」。
    /// </para>
    /// </summary>
    internal static class ValueTextFormatter
    {
        #region Public API

        /// <summary>多对象编辑下各目标值不一致时显示的占位符（与 Unity 自身一致）。</summary>
        public const string MixedValues = "—";

        /// <summary>
        /// 该属性能否被文本化。
        /// </summary>
        /// <param name="property">候选属性。</param>
        /// <returns>是简单类型返回 <c>true</c>。</returns>
        public static bool IsSupported(SerializedProperty property)
        {
            if (property == null)
            {
                return false;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.String:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.ObjectReference:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 把属性当前值转成显示文本。
        /// </summary>
        /// <param name="property">目标属性，须是 <see cref="IsSupported"/> 认可的类型。</param>
        /// <returns>显示文本。</returns>
        public static string Format(SerializedProperty property)
        {
            if (property.hasMultipleDifferentValues)
            {
                return MixedValues;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.longValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "True" : "False";
                case SerializedPropertyType.Float:
                    // "0.######" 而不是默认格式：float 转 double 会拖出一串尾巴（0.1 → 0.10000000149…）。
                    return property.doubleValue.ToString("0.######", CultureInfo.InvariantCulture);
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Enum:
                    return FormatEnum(property);
                case SerializedPropertyType.ObjectReference:
                    var target = property.objectReferenceValue;
                    return target == null ? "None" : target.name;
                default:
                    return string.Empty;
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>枚举名；组合位（无对应名字）退回原始数值。</summary>
        /// <param name="property">枚举属性。</param>
        /// <returns>显示文本。</returns>
        private static string FormatEnum(SerializedProperty property)
        {
            var names = property.enumNames;
            var index = property.enumValueIndex;

            return index >= 0 && index < names.Length
                ? names[index]
                : property.longValue.ToString(CultureInfo.InvariantCulture);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="ProgressBarAttribute"/> 的取值与鼠标换算。全是纯函数，可无头测试。
    /// </summary>
    internal static class ProgressBarValues
    {
        #region Public API

        /// <summary>
        /// 该属性能否画成进度条。
        /// </summary>
        /// <param name="property">候选属性。</param>
        /// <returns>数值类型返回 <c>true</c>。</returns>
        public static bool IsSupported(SerializedProperty property)
        {
            return property != null &&
                   (property.propertyType == SerializedPropertyType.Integer ||
                    property.propertyType == SerializedPropertyType.Float);
        }

        /// <summary>读当前值（整数与浮点统一成 double 参与运算）。</summary>
        /// <param name="property">数值属性。</param>
        /// <returns>当前值。</returns>
        public static double Read(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Integer
                ? property.longValue
                : property.doubleValue;
        }

        /// <summary>写回值；整数类型四舍五入到整数。</summary>
        /// <param name="property">数值属性。</param>
        /// <param name="value">要写入的值。</param>
        public static void Write(SerializedProperty property, double value)
        {
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                property.longValue = (long)Math.Round(value);
            }
            else
            {
                property.doubleValue = value;
            }
        }

        /// <summary>
        /// 把值归一化到 0–1（范围外夹到端点）。
        /// </summary>
        /// <param name="value">当前值。</param>
        /// <param name="min">范围下端。</param>
        /// <param name="max">范围上端。</param>
        /// <returns>填充比例。</returns>
        /// <remarks>
        /// 不钳制数据本身，只钳制「条画多满」——见 <see cref="ProgressBarDrawer"/> 的说明。
        /// </remarks>
        public static float Normalize(double value, double min, double max)
        {
            if (max <= min)
            {
                return 0f;
            }

            var normalized = (value - min) / (max - min);
            return (float)Math.Min(1.0, Math.Max(0.0, normalized));
        }

        /// <summary>
        /// 由归一化比例反算值。
        /// </summary>
        /// <param name="normalized">填充比例（会被夹到 0–1）。</param>
        /// <param name="min">范围下端。</param>
        /// <param name="max">范围上端。</param>
        /// <returns>对应的值。</returns>
        public static double FromNormalized(float normalized, double min, double max)
        {
            var clamped = Math.Min(1f, Math.Max(0f, normalized));
            return min + clamped * (max - min);
        }

        /// <summary>
        /// 按数值类型格式化显示文本。
        /// </summary>
        /// <param name="value">当前值。</param>
        /// <param name="isInteger">是否为整数类型。</param>
        /// <returns>显示文本。</returns>
        public static string FormatValue(double value, bool isInteger)
        {
            return isInteger
                ? ((long)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 鼠标横坐标在条内的归一化位置。
        /// </summary>
        /// <param name="rect">条的矩形。</param>
        /// <param name="mousePosition">鼠标位置。</param>
        /// <returns>0–1 的比例。</returns>
        public static float NormalizedFromMouse(Rect rect, Vector2 mousePosition)
        {
            return rect.width <= 0f ? 0f : Mathf.Clamp01((mousePosition.x - rect.x) / rect.width);
        }

        /// <summary>
        /// 处理条上的按下与拖动，返回本帧是否产生了新的比例。
        /// </summary>
        /// <param name="rect">条的矩形。</param>
        /// <param name="controlId">本控件的 ID（调用方用 <c>GUIUtility.GetControlID</c> 取）。</param>
        /// <param name="normalized">新的填充比例，仅在返回 <c>true</c> 时有效。</param>
        /// <returns>是否改了值。</returns>
        /// <remarks>
        /// 手写事件处理而不用隐形滑块：隐形滑块的命中区域依赖皮肤尺寸，
        /// 点条子空白处未必响应；这里命中区域就是条子本身，行为可预期。
        /// </remarks>
        public static bool TryHandleMouse(Rect rect, int controlId, out float normalized)
        {
            normalized = 0f;
            var current = Event.current;
            if (current == null)
            {
                return false;
            }

            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (current.button != 0 || !rect.Contains(current.mousePosition))
                    {
                        return false;
                    }

                    GUIUtility.hotControl = controlId;
                    normalized = NormalizedFromMouse(rect, current.mousePosition);
                    current.Use();
                    return true;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId)
                    {
                        return false;
                    }

                    normalized = NormalizedFromMouse(rect, current.mousePosition);
                    current.Use();
                    return true;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }

                    return false;

                default:
                    return false;
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="EnumToggleButtonsAttribute"/> 的每属性解析结果。
    /// <para>
    /// 反射只在首次绘制做一次：枚举是不是 <c>[Flags]</c>、每个成员的名字、掩码与按钮宽度
    /// （宽度在这里算好，避免每帧新建 <see cref="GUIContent"/> 去量文本）。
    /// </para>
    /// </summary>
    internal sealed class EnumToggleButtonsState
    {
        #region Private Fields

        private bool _initialized;
        private bool _valid;

        #endregion

        #region Public API

        /// <summary>是否为 <c>[Flags]</c> 枚举。</summary>
        public bool IsFlags;

        /// <summary>成员显示名（取自 <c>SerializedProperty.enumNames</c>，含 <c>[InspectorName]</c> 的效果）。</summary>
        public string[] Names;

        /// <summary>与 <see cref="Names"/> 对齐的位掩码。</summary>
        public long[] Masks;

        /// <summary>与 <see cref="Names"/> 对齐的按钮宽度（像素）。</summary>
        public float[] Widths;

        /// <summary>
        /// 取（并在首次访问时解析）本属性的枚举信息。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="serializedProperty">枚举序列化属性。</param>
        /// <returns>解析结果；无法解析（成员数与名字数对不上）时返回 <c>null</c>。</returns>
        public static EnumToggleButtonsState For(InspectorProperty property, SerializedProperty serializedProperty)
        {
            var state = property.State.GetOrCreate<EnumToggleButtonsState>();

            if (!state._initialized)
            {
                state._initialized = true;
                state._valid = state.Resolve(property, serializedProperty);
            }

            return state._valid ? state : null;
        }

        #endregion

        #region Private Helpers

        /// <summary>解析枚举：名字、掩码、宽度、是否 flags。</summary>
        /// <param name="property">目标属性。</param>
        /// <param name="serializedProperty">枚举序列化属性。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        private bool Resolve(InspectorProperty property, SerializedProperty serializedProperty)
        {
            var enumType = property.ValueEntry?.ValueType;
            if (enumType == null || !enumType.IsEnum)
            {
                return false;
            }

            var names = serializedProperty.enumNames;
            var values = Enum.GetValues(enumType);

            // 两者都是「枚举成员的声明顺序」，长度对不上说明前提不成立——
            // 此时按名字画按钮会把值与名字错位，宁可退回普通绘制。
            if (names == null || values.Length != names.Length)
            {
                return false;
            }

            // flags 判定与 EnumPaging 共用一份缓存（EnumSupport），避免两处各判一次。
            IsFlags = EnumSupport.IsFlags(property);
            Names = names;
            Masks = new long[names.Length];
            Widths = new float[names.Length];

            for (var i = 0; i < names.Length; i++)
            {
                Masks[i] = Convert.ToInt64(values.GetValue(i));
                Widths[i] = EditorStyles.miniLabel.CalcSize(new GUIContent(names[i])).x + 26f;
            }

            return true;
        }

        #endregion
    }
}
