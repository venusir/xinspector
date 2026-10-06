using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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
            try
            {
                return EnumIdentity.SameNames(left.enumNames, right.enumNames);
            }
            catch (System.Exception)
            {
                // enumNames 在极少数情况下会抛（属性已失效）。取不到就判为不一致，宁可不动。
                return false;
            }
        }

        #endregion
    }

    /// <summary>
    /// 「两个枚举算不算同一个」——**两条写回通道共用的那一份判据**。
    /// </summary>
    /// <remarks>
    /// 判据是「成员名与顺序逐字一致」：两个不同的枚举可以在同一序号上放着完全不同的东西，
    /// 只比 <c>propertyType</c>（两边都是 <c>Enum</c>）会让那种情况**静默写错**。
    /// 来源侧传 <c>Enum.GetNames</c>、目标侧传 <see cref="SerializedProperty.enumNames"/>，
    /// 两边问的是同一个问题，就该由同一个函数回答——各写一遍迟早漂。
    /// </remarks>
    internal static class EnumIdentity
    {
        #region Public API

        /// <summary>
        /// 两组枚举成员名是否**同长、逐字相同、顺序一致**。
        /// </summary>
        /// <param name="left">左（来源）。</param>
        /// <param name="right">右（目标）。</param>
        /// <returns>一致返回 <c>true</c>；任一为 <c>null</c> 时返回 <c>false</c>。</returns>
        public static bool SameNames(string[] left, string[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }

    /// <summary>
    /// 把**托管的选中值**写进序列化属性——<c>[ValueDropdown]</c> 反射源那条写回通道。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>与 <see cref="SerializedValueCopier"/> 是两条通道、同一份严格性。</b> 那边是
    /// 句柄 → 句柄（选项来自序列化数组），这边是托管值 → 句柄（选项来自反射源）。
    /// 两条都**先判完再写**，任何拒绝路径什么都不写；枚举的判据**物理上共用一份**
    /// （见 <see cref="EnumIdentity"/>）——同一份数据走两条路得到不同的接受结论，
    /// 是最难向使用者解释的一类不一致。
    /// </para>
    /// <para>
    /// <b>为什么需要 <paramref name="declaredType"/>：</b><see cref="SerializedProperty"/>
    /// 给得出 <c>propertyType</c>，却给不出对象引用字段的 **CLR 声明类型**
    /// （<c>Transform</c> 还是 <c>GameObject</c>？）。那个类型只能从树节点上取
    /// （<see cref="InspectorProperty.Type"/>）。传 <c>null</c> 或 <c>object</c> 时按「未知」处理，
    /// 跳过那一层校验。
    /// </para>
    /// <para>
    /// <b>拒绝面比那条通道宽得多，所以必须给出原因</b>：一条固定文案会把「它不是 Unity 对象」、
    /// 「声明类型不符」、「枚举成员名对不上」说成同一句话，而这三件事的修法完全不同。
    /// </para>
    /// </remarks>
    internal static class ReflectedValueCopier
    {
        #region Public API

        /// <summary>
        /// 尝试把托管值写进目标序列化属性。
        /// </summary>
        /// <param name="value">选中的值，可为 <c>null</c>。</param>
        /// <param name="destination">目标属性。</param>
        /// <param name="declaredType">目标的声明类型；未知时为 <c>null</c>。</param>
        /// <param name="reason">拒绝的原因；成功时为 <c>null</c>。</param>
        /// <returns>写入成功返回 <c>true</c>；拒绝返回 <c>false</c>（**什么都不写**）。</returns>
        public static bool TryAssign(
            object value, SerializedProperty destination, Type declaredType, out string reason)
        {
            reason = null;

            if (destination == null)
            {
                reason = "目标是空的（该属性没有序列化后端）";
                return false;
            }

            switch (destination.propertyType)
            {
                case SerializedPropertyType.Integer when IsIntegral(value):
                    destination.longValue = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    return true;

                case SerializedPropertyType.Boolean when value is bool flag:
                    destination.boolValue = flag;
                    return true;

                case SerializedPropertyType.Float when value is float || value is double:
                    destination.doubleValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return true;

                // 空字符串与空值都当「清空」——Unity 本来就把 null 存成空串。
                case SerializedPropertyType.String when value == null:
                    destination.stringValue = string.Empty;
                    return true;

                case SerializedPropertyType.String when value is string text:
                    destination.stringValue = text;
                    return true;

                case SerializedPropertyType.Color when value is Color color:
                    destination.colorValue = color;
                    return true;

                // Color32 → Color 是 Unity 定义的隐式转换，无损。
                case SerializedPropertyType.Color when value is Color32 color32:
                    destination.colorValue = color32;
                    return true;

                case SerializedPropertyType.Enum:
                    return TryAssignEnum(value, destination, out reason);

                case SerializedPropertyType.ObjectReference:
                    return TryAssignObject(value, destination, declaredType, out reason);

                case SerializedPropertyType.Vector2 when value is Vector2 vector2:
                    destination.vector2Value = vector2;
                    return true;

                case SerializedPropertyType.Vector3 when value is Vector3 vector3:
                    destination.vector3Value = vector3;
                    return true;

                case SerializedPropertyType.Vector4 when value is Vector4 vector4:
                    destination.vector4Value = vector4;
                    return true;

                case SerializedPropertyType.Rect when value is Rect rect:
                    destination.rectValue = rect;
                    return true;

                case SerializedPropertyType.Bounds when value is Bounds bounds:
                    destination.boundsValue = bounds;
                    return true;

                case SerializedPropertyType.Quaternion when value is Quaternion quaternion:
                    destination.quaternionValue = quaternion;
                    return true;
            }

            reason = Mismatch(destination, value);
            return false;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 枚举那一格：按**成员名**定位，且要求两边是同一个枚举。
        /// </summary>
        /// <param name="value">选中的值。</param>
        /// <param name="destination">目标属性。</param>
        /// <param name="reason">拒绝的原因。</param>
        /// <returns>写入成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// 判据与 <see cref="SerializedValueCopier"/> **同源**（见 <see cref="EnumIdentity"/>），
        /// 不更宽也不更严——更宽会把那条通道挡住的静默写错放回来，
        /// 更严则让同一份 <c>List&lt;MyEnum&gt;</c> 换个来源形态就得到不同的接受结论。
        /// </remarks>
        private static bool TryAssignEnum(object value, SerializedProperty destination, out string reason)
        {
            reason = null;

            if (!(value is Enum))
            {
                reason = Mismatch(destination, value);
                return false;
            }

            string[] targetNames;

            try
            {
                targetNames = destination.enumNames;
            }
            catch (Exception)
            {
                // enumNames 在极少数情况下会抛（属性已失效）。取不到就判为不一致，宁可不动。
                reason = "取不到目标枚举的成员名（属性可能已失效）";
                return false;
            }

            var sourceType = value.GetType();

            if (!EnumIdentity.SameNames(Enum.GetNames(sourceType), targetNames))
            {
                reason =
                    $"两个枚举的成员名或顺序不一致（来源是 {ReflectedAccessor.DescribeType(sourceType)}，" +
                    $"目标有 {targetNames?.Length ?? 0} 个成员）——无法确认它们是同一个枚举";
                return false;
            }

            var name = Enum.GetName(sourceType, value);

            if (name == null)
            {
                // [Flags] 的组合值没有单一成员名，按序号写会写错——宁可不动。
                reason = $"选中的枚举值是多个标志位的组合（{value}），没有单一的成员名，写不进去";
                return false;
            }

            var index = Array.IndexOf(targetNames, name);

            if (index < 0)
            {
                // 名字集合刚刚比对过，走到这里说明它中途变了；照样不写。
                reason = $"目标枚举里找不到名为「{name}」的成员";
                return false;
            }

            destination.enumValueIndex = index;
            return true;
        }

        /// <summary>
        /// 对象引用那一格：先看**是不是 Unity 对象**，再看**能不能赋给声明类型**。
        /// </summary>
        /// <param name="value">选中的值。</param>
        /// <param name="destination">目标属性。</param>
        /// <param name="declaredType">目标的声明类型；未知时为 <c>null</c>。</param>
        /// <param name="reason">拒绝的原因。</param>
        /// <returns>写入成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <b>两道校验缺一不可。</b> 只做第一道的话，<c>Transform</c> 字段会被塞进
        /// <c>GameObject</c>；只做第二道的话，普通托管对象会被塞进
        /// <c>objectReferenceValue</c>——那是 Unity 会报错的路径，而我们的纪律是**不写就明说**。
        /// </remarks>
        private static bool TryAssignObject(
            object value, SerializedProperty destination, Type declaredType, out string reason)
        {
            reason = null;

            // 这里刻意用**引用比较**判空：Unity 的「已销毁对象」引用不为 null、按 Unity 的语义却是空，
            // 而这两种情况在下面分开处置（真空与已销毁都写 null，非 Unity 对象要拒绝）。
            if (value == null)
            {
                destination.objectReferenceValue = null;
                return true;
            }

            if (!(value is Object unity))
            {
                reason =
                    $"目标是对象引用，而选中的值是 {ReflectedValueFormatter.TypeName(value.GetType())}" +
                    "——它不是 Unity 对象";
                return false;
            }

            // 已销毁的对象：它按 Unity 的语义就是空，写进去等于清空（用户看到的标签本来也是 None）。
            if (unity == null)
            {
                destination.objectReferenceValue = null;
                return true;
            }

            if (declaredType != null && declaredType != typeof(object) && !declaredType.IsInstanceOfType(unity))
            {
                reason =
                    $"目标的声明类型是 {ReflectedAccessor.DescribeType(declaredType)}，" +
                    $"而选中的值是 {ReflectedAccessor.DescribeType(unity.GetType())}";
                return false;
            }

            destination.objectReferenceValue = unity;
            return true;
        }

        /// <summary>
        /// 值是不是「能原样放进 <c>long</c>」的整型。
        /// </summary>
        /// <param name="value">值。</param>
        /// <returns>是返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>逐类型判，不用 <c>Type.GetTypeCode</c></b>：装箱枚举的 <c>TypeCode</c> 是它的底层
        /// 整数类型，用类型码会把「枚举值写进 int 字段」也放行——那正是本包点名要挡的静默错写。
        /// </para>
        /// <para>
        /// <c>ulong</c> 不在内：<c>long.MaxValue</c> 之外的范围表达不了，截断是错的，宁可拒绝。
        /// </para>
        /// </remarks>
        private static bool IsIntegral(object value)
        {
            return value is sbyte || value is byte || value is short || value is ushort ||
                   value is int || value is uint || value is long;
        }

        /// <summary>构造一句「类型对不上」的中文原因。</summary>
        /// <param name="destination">目标属性。</param>
        /// <param name="value">选中的值。</param>
        /// <returns>原因文本。</returns>
        private static string Mismatch(SerializedProperty destination, object value)
        {
            if (value == null)
            {
                return $"目标是 {destination.propertyType}，而选中的值是空值——只有字符串与对象引用收空值";
            }

            return $"目标是 {destination.propertyType}，而选中的值是 " +
                   $"{ReflectedValueFormatter.TypeName(value.GetType())}——两者不能互转";
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
