using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="ValueDropdownAttribute"/>：把字段画成选项来自另一个成员的下拉框。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 默认是替换型绘制器（下拉框占满整行、不调用下一个）；开了
    /// <see cref="ValueDropdownAttribute.AppendNextDrawer"/> 则画成一个小按钮，
    /// **照常调用下一个**（「下拉选 + 手填」并存），与 Odin 的语义一致。
    /// </para>
    /// <para>
    /// 替换形态必须自己套 <see cref="EditorGUI.DisabledScope"/>——末端绘制器那层只读保护被绕过了。
    /// </para>
    /// <para>
    /// 选项表（树形分层、排序、取值名）在纯函数 <see cref="ValueDropdownOptions"/> 里，
    /// 复制值在 <see cref="SerializedValueCopier"/> 里；本类只负责画与弹菜单。
    /// </para>
    /// <para>
    /// 弹出层用 <see cref="GenericMenu"/>，**不自建窗口**：官方那个带搜索框、图标、
    /// 双击确认的弹出层本包不做（那几个选项因此**不声明**，写了会编译不过——见特性注释）。
    /// </para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class ValueDropdownDrawer : AttributeDrawer<ValueDropdownAttribute>
    {
        #region Private Fields

        /// <summary>小按钮的宽度（像素）。</summary>
        private const float MiniButtonWidth = 20f;

        /// <summary>小按钮的文本。用实心三角，不引入图标系统。</summary>
        private static readonly GUIContent MiniLabel = new GUIContent("▼", "从列表中选择");

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, ValueDropdownAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;
            if (serializedProperty == null)
            {
                CallNextDrawer(property, label);
                return;
            }

            if (!ValueDropdownTarget.IsSupported(serializedProperty))
            {
                // 数组/集合目标以前会走到「像字段的下拉按钮」那条路：显示成空白、点开选择后报
                // 类型不一致。「改了但没反应」比「压根没装上」难查得多，故这里明说并退回。
                DrawerWarnings.Once(property, nameof(ValueDropdownDrawer) + ".target",
                    DrawerWarnings.TypeMismatch(property, "[ValueDropdown]", "单值成员（数组形态未做）"));
                CallNextDrawer(property, label);
                return;
            }

            var state = property.State.Get<ValueDropdownState>();
            if (state == null || !state.Resolved)
            {
                // 处理器已经报过一次构建期告警，这里不再重复刷屏。
                CallNextDrawer(property, label);
                return;
            }

            if (attribute.AppendNextDrawer)
            {
                DrawAppended(property, serializedProperty, attribute, label, state);
                return;
            }

            DrawWide(property, serializedProperty, attribute, label, state);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 替换形态：整行一个「像字段的下拉按钮」，显示当前值。
        /// </summary>
        /// <remarks>
        /// 按钮文本取**当前值本身**而不是去来源数组里找回选项名——后者是每帧 O(n) 的查找，
        /// 而本包支持的来源就是普通数组，选项名与值本来就是同一个东西。
        /// </remarks>
        private static void DrawWide(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            ValueDropdownAttribute attribute,
            GUIContent label,
            ValueDropdownState state)
        {
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginProperty(rect, label, serializedProperty);

            var content = label != null && label != GUIContent.none ? EditorGUI.PrefixLabel(rect, label) : rect;

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                var current = ValueDropdownOptions.DescribeValue(serializedProperty);
                if (GUI.Button(content, current, EditorStyles.popup))
                {
                    ShowMenu(property, serializedProperty, attribute, state);
                }
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// 附加形态：小按钮 + 照常画内侧控件。
        /// </summary>
        /// <remarks>非静态：<c>CallNextDrawer</c> 是基类的**受保护实例方法**。</remarks>
        private void DrawAppended(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            ValueDropdownAttribute attribute,
            GUIContent label,
            ValueDropdownState state)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
                {
                    if (GUILayout.Button(MiniLabel, EditorStyles.miniButton, GUILayout.Width(MiniButtonWidth)))
                    {
                        ShowMenu(property, serializedProperty, attribute, state);
                    }
                }

                using (new EditorGUI.DisabledScope(attribute.DisableGUIInAppendedDrawer))
                {
                    CallNextDrawer(property, label);
                }
            }
        }

        /// <summary>
        /// 弹出选项菜单；选中则复制值。
        /// </summary>
        /// <remarks>
        /// 用 <c>GenericMenu.MenuFunction2</c> + 「选项下标」当 <c>userData</c>，
        /// **不为每个选项建闭包**——那样一个菜单就会分配 n 个委托。
        /// </remarks>
        private static void ShowMenu(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            ValueDropdownAttribute attribute,
            ValueDropdownState state)
        {
            var options = ValueDropdownOptions.Build(state.Source, attribute.FlattenTreeView, attribute.SortDropdownItems);

            if (options.Count == 0)
            {
                DrawerWarnings.Once(property, nameof(ValueDropdownDrawer) + ".empty",
                    $"[XInspector] 属性「{property.Path}」上的 [ValueDropdown] 选项来源是空的，" +
                    "下拉框里没有任何可选项（字段本身照常可用）。");
                return;
            }

            var menu = new GenericMenu();
            var target = serializedProperty.Copy();
            var source = state.Source.Copy();

            for (var i = 0; i < options.Count; i++)
            {
                menu.AddItem(new GUIContent(options[i].Path), false, OnSelected, new Selection(source, i, target));
            }

            menu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
        }

        /// <summary>
        /// 菜单回调：把第 <c>index</c> 个选项复制进目标。
        /// </summary>
        /// <param name="userData">打包好的「来源 + 下标 + 目标」。</param>
        private static void OnSelected(object userData)
        {
            var selection = (Selection)userData;

            if (!SerializedValueCopier.TryCopy(selection.Source.GetArrayElementAtIndex(selection.Index), selection.Target))
            {
                Debug.LogWarning(
                    "[XInspector] [ValueDropdown] 选中的选项与字段的类型不一致，已忽略这次选择。" +
                    "来源数组的元素类型必须与字段类型相同——本包不做数值互转（枚举还要求成员名与顺序一致）。");
            }
        }

        #endregion

        /// <summary>
        /// 菜单回调的载荷：来源数组、选中的下标、目标属性。
        /// </summary>
        /// <remarks>
        /// <see cref="SerializedProperty"/> 是**活句柄**，跨帧有效，故可以捕获进菜单回调；
        /// 用 <c>Copy()</c> 另取一份是为了不干扰绘制路径上正在用的那一个。
        /// </remarks>
        private readonly struct Selection
        {
            /// <summary>选项来源（数组）。</summary>
            public readonly SerializedProperty Source;

            /// <summary>选中的下标。</summary>
            public readonly int Index;

            /// <summary>要写入的目标。</summary>
            public readonly SerializedProperty Target;

            /// <summary>以三段构造。</summary>
            /// <param name="source">选项来源。</param>
            /// <param name="index">选中的下标。</param>
            /// <param name="target">目标属性。</param>
            public Selection(SerializedProperty source, int index, SerializedProperty target)
            {
                Source = source;
                Index = index;
                Target = target;
            }
        }
    }

    /// <summary>
    /// 把一个序列化属性的值复制到另一个。判定与复制都是纯逻辑（不碰 GUI），可无头测试。
    /// </summary>
    internal static class SerializedValueCopier
    {
        #region Public API

        /// <summary>
        /// 尝试把 <paramref name="source"/> 的值复制进 <paramref name="destination"/>。
        /// </summary>
        /// <param name="source">来源。</param>
        /// <param name="destination">目标。</param>
        /// <returns>复制成功返回 <c>true</c>；类型对不上返回 <c>false</c>（**什么都不写**）。</returns>
        /// <remarks>
        /// 类型判定是**两道**：<see cref="SerializedProperty.propertyType"/> 必须相同；
        /// 枚举还要求成员名与顺序完全一致——两个不同的枚举可以在同一序号上放着完全不同的东西，
        /// 只比 <c>propertyType</c> 会让那种情况静默写错。
        /// </remarks>
        public static bool TryCopy(SerializedProperty source, SerializedProperty destination)
        {
            if (source == null || destination == null)
            {
                return false;
            }

            if (source.propertyType != destination.propertyType)
            {
                return false;
            }

            if (source.propertyType == SerializedPropertyType.Enum && !SameEnum(source, destination))
            {
                return false;
            }

            switch (source.propertyType)
            {
                case SerializedPropertyType.Integer:
                    destination.longValue = source.longValue;
                    return true;
                case SerializedPropertyType.Boolean:
                    destination.boolValue = source.boolValue;
                    return true;
                case SerializedPropertyType.Float:
                    destination.doubleValue = source.doubleValue;
                    return true;
                case SerializedPropertyType.String:
                    destination.stringValue = source.stringValue;
                    return true;
                case SerializedPropertyType.Color:
                    destination.colorValue = source.colorValue;
                    return true;
                case SerializedPropertyType.ObjectReference:
                    destination.objectReferenceValue = source.objectReferenceValue;
                    return true;
                case SerializedPropertyType.Enum:
                    destination.enumValueIndex = source.enumValueIndex;
                    return true;
                case SerializedPropertyType.Vector2:
                    destination.vector2Value = source.vector2Value;
                    return true;
                case SerializedPropertyType.Vector3:
                    destination.vector3Value = source.vector3Value;
                    return true;
                case SerializedPropertyType.Vector4:
                    destination.vector4Value = source.vector4Value;
                    return true;
                case SerializedPropertyType.Rect:
                    destination.rectValue = source.rectValue;
                    return true;
                case SerializedPropertyType.Bounds:
                    destination.boundsValue = source.boundsValue;
                    return true;
                case SerializedPropertyType.Quaternion:
                    destination.quaternionValue = source.quaternionValue;
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 两个枚举属性的成员名与顺序是否完全一致。
        /// </summary>
        /// <param name="left">左。</param>
        /// <param name="right">右。</param>
        /// <returns>一致返回 <c>true</c>。</returns>
        private static bool SameEnum(SerializedProperty left, SerializedProperty right)
        {
            string[] leftNames;
            string[] rightNames;

            try
            {
                leftNames = left.enumNames;
                rightNames = right.enumNames;
            }
            catch (System.Exception)
            {
                // enumNames 在极少数情况下会抛（属性已失效）。取不到就判为不一致，宁可不动。
                return false;
            }

            if (leftNames == null || rightNames == null || leftNames.Length != rightNames.Length)
            {
                return false;
            }

            for (var i = 0; i < leftNames.Length; i++)
            {
                if (!string.Equals(leftNames[i], rightNames[i], System.StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }

    /// <summary>
    /// 选项表的构造：从哪里来、怎么分层、怎么排序。纯逻辑，可无头测试。
    /// </summary>
    internal static class ValueDropdownOptions
    {
        #region Public API

        /// <summary>
        /// 选项表里的一项。
        /// </summary>
        public readonly struct Option
        {
            /// <summary>以菜单路径构造。</summary>
            /// <param name="path">菜单路径（树形时用 <c>/</c> 分层）。</param>
            /// <param name="index">在来源数组里的下标。</param>
            public Option(string path, int index)
            {
                Path = path;
                Index = index;
            }

            /// <summary>菜单路径。</summary>
            public string Path { get; }

            /// <summary>在来源数组里的下标。</summary>
            public int Index { get; }
        }

        /// <summary>
        /// 由来源数组构造选项表。
        /// </summary>
        /// <param name="source">选项来源（数组或 List）。</param>
        /// <param name="flatten"><c>true</c> 时不分层（路径里的 <c>/</c> 原样显示）。</param>
        /// <param name="sort"><c>true</c> 时按名字排序。</param>
        /// <returns>选项表；来源为空或无数组元素时返回空表。</returns>
        /// <remarks>
        /// 每帧重建（选项随来源的值变化才算对），故这里**不缓存**——
        /// 缓存会让来源被改后菜单还显示旧选项。
        /// </remarks>
        public static List<Option> Build(SerializedProperty source, bool flatten, bool sort)
        {
            var result = new List<Option>();

            if (source == null || !source.isArray || source.arraySize == 0)
            {
                return result;
            }

            for (var i = 0; i < source.arraySize; i++)
            {
                var element = source.GetArrayElementAtIndex(i);
                var name = DescribeValue(element);

                if (string.IsNullOrEmpty(name))
                {
                    name = "(空)";
                }

                // 树形 = 把 "/" 原样交给 GenericMenu：它自己会切成子菜单。
                result.Add(new Option(flatten ? Flatten(name) : name, i));
            }

            if (sort)
            {
                result.Sort(CompareByPath);
            }

            return result;
        }

        /// <summary>
        /// 把一个序列化属性描述成一行可读文本（菜单项与下拉按钮都用它）。
        /// </summary>
        /// <param name="property">属性。</param>
        /// <returns>文本；认不出的类型返回类型名。</returns>
        public static string DescribeValue(SerializedProperty property)
        {
            if (property == null)
            {
                return string.Empty;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.longValue.ToString();
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "True" : "False";
                case SerializedPropertyType.Float:
                    return property.doubleValue.ToString("0.###");
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Enum:
                    var names = property.enumNames;
                    var index = property.enumValueIndex;
                    return names != null && index >= 0 && index < names.Length ? names[index] : index.ToString();
                case SerializedPropertyType.ObjectReference:
                    var reference = property.objectReferenceValue;
                    return reference != null ? reference.name : "(None)";
                case SerializedPropertyType.Color:
                    return property.colorValue.ToString();
                case SerializedPropertyType.Vector2:
                    return property.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return property.vector3Value.ToString();
                default:
                    return property.propertyType.ToString();
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 把树形路径拍平：斜杠换成中点，避免 GenericMenu 把它切成子菜单。
        /// </summary>
        /// <param name="path">原路径。</param>
        /// <returns>拍平后的名字。</returns>
        private static string Flatten(string path)
        {
            return path.IndexOf('/') < 0 ? path : path.Replace('/', '›');
        }

        /// <summary>
        /// 按路径排序（序数比较，保证跨平台稳定）。
        /// </summary>
        /// <param name="left">左。</param>
        /// <param name="right">右。</param>
        /// <returns>比较结果。</returns>
        private static int CompareByPath(Option left, Option right)
        {
            return string.CompareOrdinal(left.Path, right.Path);
        }

        #endregion
    }

    /// <summary>
    /// <c>[ValueDropdown]</c> 的**目标判定**——纯函数，可无头测试。
    /// </summary>
    internal static class ValueDropdownTarget
    {
        #region Public API

        /// <summary>
        /// 目标字段能不能挂这个下拉：需要 Unity 的序列化后端，且是**单值**成员。
        /// </summary>
        /// <param name="serializedProperty">目标字段的序列化属性。</param>
        /// <returns>支持返回 <c>true</c>。</returns>
        /// <remarks>
        /// 字符串要放行：它在若干语境下被 Unity 算作 <c>isArray</c>，而 <c>[ValueDropdown]</c>
        /// 恰恰常用在字符串上。数组与 List 才是这里要挡的（按元素画属集合自绘那一层）。
        /// </remarks>
        public static bool IsSupported(SerializedProperty serializedProperty)
        {
            return serializedProperty != null
                && (serializedProperty.propertyType == SerializedPropertyType.String || !serializedProperty.isArray);
        }

        #endregion
    }
}
