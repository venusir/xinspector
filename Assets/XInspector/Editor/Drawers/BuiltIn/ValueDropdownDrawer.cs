using System;
using System.Collections;
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
    /// <b>数据源有两个形态</b>（见 <see cref="ValueDropdownState"/>）：序列化的数组 / List
    /// （句柄形态）与「声明类型实现 <see cref="IList"/> 的字段 / 属性 / 无参方法」（反射形态）。
    /// 两者的差别被收在两处：选项表怎么造、选中的值怎么写回——绘制那一段是共用的。
    /// </para>
    /// <para>
    /// 选项表（树形分层、排序、取值名）在纯函数 <see cref="ValueDropdownOptions"/> 里，
    /// 复制值在 <see cref="SerializedValueCopier"/> 与 <see cref="ReflectedValueCopier"/> 里；
    /// 本类只负责画与弹菜单。
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
                // 目标必须是序列化成员：反射成员在本包只读（写回无落点），
                // 而本特性存在的意义就是**写**一个值。以前这里静默退回，用户看不到任何解释。
                DrawerWarnings.Once(
                    property,
                    nameof(ValueDropdownDrawer) + ".backend",
                    DrawerWarnings.TypeMismatch(property, "[ValueDropdown]", "Unity 的序列化后端"));
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
        /// 弹出选项菜单；选中则写回。按来源形态分派到两套——选项表怎么造、值怎么写回不同，
        /// 其余（去重告警的键、菜单的构造方式、弹出位置）共用。
        /// </summary>
        /// <remarks>
        /// 用 <c>GenericMenu.MenuFunction2</c> + 一个载荷当 <c>userData</c>，
        /// **不为每个选项建闭包**——那样一个菜单就会分配 n 个委托。
        /// </remarks>
        private static void ShowMenu(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            ValueDropdownAttribute attribute,
            ValueDropdownState state)
        {
            if (state.Source != null)
            {
                ShowSerializedMenu(property, serializedProperty, attribute, state);
                return;
            }

            ShowReflectedMenu(property, serializedProperty, attribute, state);
        }

        /// <summary>序列化形态的菜单：来源是数组句柄，选项逐个取自它的元素。</summary>
        /// <param name="property">目标属性。</param>
        /// <param name="serializedProperty">目标的序列化属性。</param>
        /// <param name="attribute">特性实例。</param>
        /// <param name="state">解析结果。</param>
        private static void ShowSerializedMenu(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            ValueDropdownAttribute attribute,
            ValueDropdownState state)
        {
            var options = ValueDropdownOptions.Build(state.Source, attribute.FlattenTreeView, attribute.SortDropdownItems);

            if (options.Count == 0)
            {
                DrawerWarnings.Once(property, nameof(ValueDropdownDrawer) + ".empty", EmptySourceWarning(property));
                return;
            }

            var menu = new GenericMenu();
            var target = serializedProperty.Copy();
            var source = state.Source.Copy();

            for (var i = 0; i < options.Count; i++)
            {
                menu.AddItem(new GUIContent(options[i].Path), false, OnSerializedSelected, new Selection(source, i, target));
            }

            menu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
        }

        /// <summary>
        /// 反射形态的菜单：**此刻**才现读来源（点击之前一次都不读，用户的方法因此不会被每帧调用）。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="serializedProperty">目标的序列化属性。</param>
        /// <param name="attribute">特性实例。</param>
        /// <param name="state">解析结果。</param>
        /// <remarks>
        /// 「取不到」与「是空的」分开告警：前者是实例为空（嵌套实例被置空、方法给回 null），
        /// 后者是来源确实没有元素。两条都在**点击时**报——挪进绘制期就变成每帧调一次用户代码。
        /// </remarks>
        private static void ShowReflectedMenu(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            ValueDropdownAttribute attribute,
            ValueDropdownState state)
        {
            var source = state.SourceList();

            if (source == null)
            {
                DrawerWarnings.Once(property, nameof(ValueDropdownDrawer) + ".missing",
                    $"[XInspector] 属性「{property.Path}」上的 [ValueDropdown] 选项来源此刻取不到" +
                    "（嵌套实例为空，或来源成员给回了空引用）。字段本身照常可用。");
                return;
            }

            var options = ValueDropdownOptions.Build(
                source, state.ElementType, attribute.FlattenTreeView, attribute.SortDropdownItems);

            if (options.Count == 0)
            {
                DrawerWarnings.Once(property, nameof(ValueDropdownDrawer) + ".empty", EmptySourceWarning(property));
                return;
            }

            var menu = new GenericMenu();
            var target = serializedProperty.Copy();
            var declaredType = property.Type;

            for (var i = 0; i < options.Count; i++)
            {
                // 载荷里带的是**这一条的值本身**，不是「列表 + 下标」——理由见 Selection 的注释。
                menu.AddItem(
                    new GUIContent(options[i].Path),
                    false,
                    OnReflectedSelected,
                    new Selection(source[options[i].Index], target, declaredType));
            }

            menu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
        }

        /// <summary>「来源是空的」那条告警的文本（两个形态共用）。</summary>
        /// <param name="property">目标属性。</param>
        /// <returns>告警文本。</returns>
        private static string EmptySourceWarning(InspectorProperty property)
        {
            return $"[XInspector] 属性「{property.Path}」上的 [ValueDropdown] 选项来源是空的，" +
                   "下拉框里没有任何可选项（字段本身照常可用）。";
        }

        /// <summary>
        /// 序列化形态的菜单回调：把第 <c>index</c> 个选项复制进目标。
        /// </summary>
        /// <param name="userData">打包好的「来源 + 下标 + 目标」。</param>
        private static void OnSerializedSelected(object userData)
        {
            var selection = (Selection)userData;

            if (!SerializedValueCopier.TryCopy(selection.Source.GetArrayElementAtIndex(selection.Index), selection.Target))
            {
                Debug.LogWarning(
                    "[XInspector] [ValueDropdown] 选中的选项与字段的类型不一致，已忽略这次选择。" +
                    "来源数组的元素类型必须与字段类型相同——本包不做数值互转（枚举还要求成员名与顺序一致）。");
            }
        }

        /// <summary>
        /// 反射形态的菜单回调：把选中的托管值写进目标。
        /// </summary>
        /// <param name="userData">打包好的「值 + 目标 + 目标的声明类型」。</param>
        private static void OnReflectedSelected(object userData)
        {
            var selection = (Selection)userData;

            if (ReflectedValueCopier.TryAssign(
                    selection.Value, selection.Target, selection.DeclaredType, out var reason))
            {
                return;
            }

            Debug.LogWarning(
                $"[XInspector] [ValueDropdown] 属性「{selection.Target.propertyPath}」上的这次选择已忽略：" +
                $"{reason}。来源里那些值与字段类型不一致的选项写不进去——本包不做数值互转。");
        }

        #endregion

        /// <summary>
        /// 菜单回调的载荷：目标属性，外加**其中一个形态**的来源信息。
        /// </summary>
        /// <remarks>
        /// <b>两个形态的取法刻意不同，这不是疏漏：</b>
        /// <see cref="SerializedProperty"/> 是**活句柄**（跨帧有效），故序列化形态可以只记
        /// 「数组 + 下标」，回调时再取那一个元素；反射形态记的却是**菜单构造那一刻的值本身**——
        /// 回调发生在菜单关闭之后，中间可能隔好几帧，而方法源每调一次就新建一个列表，
        /// 那时再按下标去取，取到的可能已经不是用户看到的那一条了。
        /// 捕获值对象让「看到的标签」与「写进去的值」逐字是同一个。
        /// </remarks>
        private readonly struct Selection
        {
            /// <summary>序列化形态的选项来源（数组）；反射形态为 <c>null</c>。</summary>
            public readonly SerializedProperty Source;

            /// <summary>序列化形态选中的下标；反射形态不用。</summary>
            public readonly int Index;

            /// <summary>反射形态选中的值（**快照**）；序列化形态为 <c>null</c>。</summary>
            public readonly object Value;

            /// <summary>要写入的目标（活句柄，两个形态共用）。</summary>
            public readonly SerializedProperty Target;

            /// <summary>目标的声明类型（反射形态写回时要它校验对象引用）；序列化形态为 <c>null</c>。</summary>
            public readonly Type DeclaredType;

            /// <summary>以序列化形态的三段构造。</summary>
            /// <param name="source">选项来源。</param>
            /// <param name="index">选中的下标。</param>
            /// <param name="target">目标属性。</param>
            public Selection(SerializedProperty source, int index, SerializedProperty target)
            {
                Source = source;
                Index = index;
                Value = null;
                Target = target;
                DeclaredType = null;
            }

            /// <summary>以反射形态的三段构造。</summary>
            /// <param name="value">选中的值（快照）。</param>
            /// <param name="target">目标属性。</param>
            /// <param name="declaredType">目标的声明类型。</param>
            public Selection(object value, SerializedProperty target, Type declaredType)
            {
                Source = null;
                Index = 0;
                Value = value;
                Target = target;
                DeclaredType = declaredType;
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
    /// <b>为什么需要一个声明类型参数：</b><see cref="SerializedProperty"/>
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

                // 托管引用（[SerializeReference]）：**一条判据管两种槽位**——类型槽位（值是一只
                // System.Type）与多态槽位（值是一个实例）都问「声明类型装不装得下这个值」。
                case SerializedPropertyType.ManagedReference:
                    return TryAssignManagedReference(value, destination, declaredType, out reason);

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
        /// 托管引用那一格：**一条判据管两种槽位**——类型槽位（值是一只 <c>System.Type</c>）
        /// 与多态槽位（值是一个实例）。
        /// </summary>
        /// <param name="value">选中的值：一只 <c>Type</c>、一个实例，或清空用的 <c>null</c>。</param>
        /// <param name="destination">目标属性。</param>
        /// <param name="declaredType">
        /// 目标的声明类型。**调用方要传字段的声明类型**（<c>FieldInfo.FieldType</c>），
        /// 不能传节点的 <c>Type</c>——类型槽位有值时那个是 <c>RuntimeType</c>，
        /// 把「字段声明的是什么」这条信息丢了。
        /// </param>
        /// <param name="reason">拒绝的原因。</param>
        /// <returns>写入成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>判据只有一条：<c>declaredType.IsInstanceOfType(value)</c>。</b> 它对两种槽位同时成立：
        /// 类型槽位存的就是那只 <c>Type</c> 实例（它本身就是 <c>System.Type</c> 的实例），
        /// 多态槽位存的是实现类的实例。两条路各写一遍的话，同一个值走不同入口会得到不同的接受结论。
        /// </para>
        /// <para>
        /// <b>清空（<c>null</c>）两形态共用</b>：赋 <c>null</c> 与置 <c>RefIdNull</c> 两条路都通（实测），
        /// 这里用前者。
        /// </para>
        /// </remarks>
        private static bool TryAssignManagedReference(
            object value, SerializedProperty destination, Type declaredType, out string reason)
        {
            reason = null;

            if (value == null)
            {
                destination.managedReferenceValue = null;
                return true;
            }

            if (declaredType != null && declaredType != typeof(object) && !declaredType.IsInstanceOfType(value))
            {
                reason =
                    $"目标的声明类型是 {ReflectedAccessor.DescribeType(declaredType)}" +
                    $"——它装不下选中的值（{DescribeManagedValue(value)}）";
                return false;
            }

            destination.managedReferenceValue = value;
            return true;
        }

        /// <summary>把托管引用的候选值说成一个类型名——**类型槽位要说它指的那个类型**。</summary>
        /// <param name="value">非空的值。</param>
        /// <returns>描述文本。</returns>
        /// <remarks>
        /// 值是一只 <c>System.Type</c> 时不能拿 <c>value.GetType()</c>——那会打印出
        /// <c>RuntimeType</c>，是纯混淆；要说的是**它指的那个类型**。
        /// </remarks>
        private static string DescribeManagedValue(object value)
        {
            return value is Type type
                ? ReflectedAccessor.DescribeType(type)
                : ReflectedAccessor.DescribeType(value.GetType());
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
                return $"目标是 {destination.propertyType}，而选中的值是空值——" +
                       "只有字符串、对象引用，以及托管引用槽位（清空）收空值";
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
        /// **每次弹出菜单时**重建一次（选项随来源的值变化才算对），故这里**不缓存**——
        /// 缓存会让来源被改后菜单还显示旧选项。它不是每帧路径：
        /// <c>ShowMenu</c> 在点击时才调它。
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
        /// 由**反射来源**构造选项表——与 <see cref="Build(SerializedProperty, bool, bool)"/>
        /// 同一套分层、排序与「空文本 → （空）」的规矩，只是值来自托管对象而不是序列化句柄。
        /// </summary>
        /// <param name="source">选项来源（实现 <see cref="IList"/> 的集合）；可为 <c>null</c>。</param>
        /// <param name="elementType">
        /// 元素的**声明类型**；推不出来时给 <c>null</c>（那时退回运行时类型，见
        /// <see cref="DescribeValue(object, Type)"/>）。
        /// </param>
        /// <param name="flatten"><c>true</c> 时不分层（路径里的 <c>/</c> 原样显示）。</param>
        /// <param name="sort"><c>true</c> 时按名字排序。</param>
        /// <returns>选项表；来源为空或没有元素时返回空表。</returns>
        /// <remarks>
        /// 调用方在这一步**才**现读来源（<c>state.SourceList()</c>），故来源为 <c>null</c>
        /// 表示「此刻取不到实例」——那是与「有值但是空的」不同的另一种情况，
        /// 由调用方分开告警。
        /// </remarks>
        public static List<Option> Build(IList source, Type elementType, bool flatten, bool sort)
        {
            var result = new List<Option>();

            if (source == null || source.Count == 0)
            {
                return result;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var name = DescribeValue(source[i], elementType);

                if (string.IsNullOrEmpty(name))
                {
                    // 与序列化形态同款：空字符串在菜单里是一行看不见的东西，得明说。
                    name = "(空)";
                }

                result.Add(new Option(flatten ? Flatten(name) : name, i));
            }

            if (sort)
            {
                result.Sort(CompareByPath);
            }

            return result;
        }

        /// <summary>
        /// 把一个**托管值**描述成一行可读文本（反射源的选项标签）。
        /// </summary>
        /// <param name="value">值，可为 <c>null</c>。</param>
        /// <param name="declaredType">元素的声明类型；未知时为 <c>null</c>。</param>
        /// <returns>文本，不会为 <c>null</c>。</returns>
        /// <remarks>
        /// <b>转调 <see cref="ReflectedValueFormatter.Format"/>，不另写一套。</b>
        /// 那是本包回答「一个值显示成什么」的唯一入口——<c>[ShowInInspector]</c> 的只读展示
        /// 用的是同一个，两处答案不该有两个。
        /// 元素声明类型推不出来时用**运行时类型**兜底：不这么做，数字会落到跟随当前文化的
        /// <c>ToString()</c>（德语环境下小数点会变成逗号）。
        /// </remarks>
        public static string DescribeValue(object value, Type declaredType)
        {
            return ReflectedValueFormatter.Format(value, declaredType ?? value?.GetType());
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
