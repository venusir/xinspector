using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 多态字段那一行（「当前类型名」按钮 + 候选菜单）——**两个消费者共用一枚实现**：
    /// 链上的 <see cref="PolymorphicDrawerSettingsDrawer"/>（未展开态）与末端
    /// <see cref="PolymorphicRowTerminalDrawer"/>（展开态）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 判断全部收在纯函数里（<see cref="Decide"/> / <see cref="TextFor"/> /
    /// <see cref="ShouldDisableRow"/> / <see cref="ShouldRewrite"/> / <see cref="DeclaredTypeOf"/>）——
    /// 绘制器与末端只做「按档分派 + 画」，本仓不测 IMGUI，决策必须放在可无头断言的地方。
    /// </para>
    /// <para>
    /// 菜单复用 <see cref="TypeSelectorOptions"/> 的构造与分层（与 <c>[TypeDrawerSettings]</c>
    /// 逐字同款），载荷用 <see cref="Selection"/> + <c>MenuFunction2</c>——不为每项建闭包。
    /// </para>
    /// </remarks>
    internal static class PolymorphicRow
    {
        #region Constants

        /// <summary>空槽位的行文本（本包自定，与 <c>[TypeDrawerSettings]</c> 同款）。</summary>
        internal const string NoTypeText = "（无）";

        #endregion

        #region Pure Logic

        /// <summary>
        /// 这一行归谁画——整张表收在这一个纯函数里。
        /// </summary>
        /// <param name="property">节点。</param>
        /// <param name="serializedProperty">它的序列化属性；可以为 <c>null</c>。</param>
        /// <param name="declaredType">**字段的声明类型**。</param>
        /// <returns>归谁画。</returns>
        /// <remarks>
        /// 判据与 <c>[TypeDrawerSettings]</c> 共用（<see cref="TypeSelectorTarget.IsTypeSlot"/>）；
        /// 「不是托管引用」与「多选」在这里**分两档**——那边合在 <c>CanSelect</c> 里够了，
        /// 这边两档的处置不同（一个告警、一个不告警）。
        /// </remarks>
        public static PolymorphicRowDisposition Decide(
            InspectorProperty property, SerializedProperty serializedProperty, Type declaredType)
        {
            if (property == null || serializedProperty == null ||
                serializedProperty.propertyType != SerializedPropertyType.ManagedReference)
            {
                return PolymorphicRowDisposition.FallbackNotBacked;
            }

            if (TypeSelectorTarget.IsTypeSlot(serializedProperty, declaredType))
            {
                return PolymorphicRowDisposition.FallbackTypeSlot;
            }

            if (serializedProperty.hasMultipleDifferentValues)
            {
                return PolymorphicRowDisposition.FallbackMultiSelect;
            }

            if (property.State.Get<PolymorphicLayerState>() != null)
            {
                // 已展开：行由末端画（链上这枚必须让路，否则两行并存）。
                return PolymorphicRowDisposition.TerminalOwnsRow;
            }

            var current = serializedProperty.managedReferenceValue;
            if (current == null)
            {
                // 空槽位：本包画——用户要能挑第一个类型。
                return PolymorphicRowDisposition.DrawRow;
            }

            // 有值但没展开：撞守卫（构建期已告警过）或类型用不到本包——都回退原生，
            // 只差一条告警。
            return PolymorphicReference.NestingBlock(property, current.GetType()) != null
                ? PolymorphicRowDisposition.FallbackGuarded
                : PolymorphicRowDisposition.FallbackNotTakenOver;
        }

        /// <summary>行文本：空槽位「（无）」；有值显示具体类型，开启开关时再带声明类型。</summary>
        /// <param name="currentType">当前的具体类型；空槽位为 <c>null</c>。</param>
        /// <param name="declaredType">字段的声明类型。</param>
        /// <param name="showBaseType">是否带基类型后缀。</param>
        /// <returns>行文本。</returns>
        /// <remarks>格式「具体类型 （基类型）」是本包自定，写进 README。</remarks>
        public static string TextFor(Type currentType, Type declaredType, bool showBaseType)
        {
            if (currentType == null)
            {
                return NoTypeText;
            }

            var name = TypeSelectorOptions.DisplayName(currentType);

            return showBaseType && declaredType != null && declaredType != currentType
                ? $"{name} （{TypeSelectorOptions.DisplayName(declaredType)}）"
                : name;
        }

        /// <summary>
        /// 这一行要不要禁用：状态只读恒禁；<c>ReadOnlyIfNotNullReference</c> 只在**有值**时禁。
        /// </summary>
        /// <param name="stateReadOnly">节点状态上的只读。</param>
        /// <param name="readOnlyIfNotNullReference">旋钮。</param>
        /// <param name="hasValue">槽位有没有值。</param>
        /// <returns>该禁用返回 <c>true</c>。</returns>
        /// <remarks>只锁**行**（改类型），不锁子字段——锁整棵子树是 <c>[ReadOnly]</c> 的事。</remarks>
        public static bool ShouldDisableRow(bool stateReadOnly, bool readOnlyIfNotNullReference, bool hasValue)
        {
            return stateReadOnly || (readOnlyIfNotNullReference && hasValue);
        }

        /// <summary>
        /// 这次选择要不要真的重写：**点当前类型 = 无操作**（不拿同类型的新实例换掉用户的值）。
        /// </summary>
        /// <param name="currentType">当前的具体类型；空槽位为 <c>null</c>。</param>
        /// <param name="selectedType">选中的类型。</param>
        /// <returns>该重写返回 <c>true</c>。</returns>
        public static bool ShouldRewrite(Type currentType, Type selectedType)
        {
            return selectedType != null && selectedType != currentType;
        }

        /// <summary>
        /// **字段的声明类型**——展开态的节点 <c>Type</c> 是具体类型，不能用。
        /// </summary>
        /// <param name="property">节点。</param>
        /// <returns>声明类型；取不到时为 <c>null</c>。</returns>
        public static Type DeclaredTypeOf(InspectorProperty property)
        {
            return (property?.Member as FieldInfo)?.FieldType ?? property?.Type;
        }

        #endregion

        #region Drawing

        /// <summary>
        /// 画那一行（替换型）：一行「像字段的下拉按钮」，点开是候选菜单。
        /// </summary>
        /// <param name="property">节点。</param>
        /// <param name="serializedProperty">它的序列化属性。</param>
        /// <param name="declaredType">字段的声明类型。</param>
        /// <param name="settings">特性；允许为 <c>null</c>（按默认值画——链装配错配也不该炸）。</param>
        /// <param name="label">标签。</param>
        public static void Draw(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            Type declaredType,
            PolymorphicDrawerSettingsAttribute settings,
            GUIContent label)
        {
            var current = serializedProperty.managedReferenceValue;
            var currentType = current?.GetType();
            var showBaseType = settings != null && settings.ShowBaseType;
            var readOnlyIfNotNull = settings != null && settings.ReadOnlyIfNotNullReference;
            var preference = settings?.NonDefaultConstructorPreference ??
                             NonDefaultConstructorPreference.ConstructIdeal;

            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginProperty(rect, label, serializedProperty);

            var content = label != null && label != GUIContent.none ? EditorGUI.PrefixLabel(rect, label) : rect;

            using (new EditorGUI.DisabledScope(
                       ShouldDisableRow(property.State.IsReadOnly, readOnlyIfNotNull, currentType != null)))
            {
                if (GUI.Button(content, TextFor(currentType, declaredType, showBaseType), EditorStyles.popup))
                {
                    ShowMenu(property, serializedProperty, declaredType, preference, currentType);
                }
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// 弹出候选菜单；选中则**造实例写回**（见 <see cref="PolymorphicSlotWrite"/>）。
        /// </summary>
        /// <param name="property">节点。</param>
        /// <param name="serializedProperty">它的序列化属性。</param>
        /// <param name="declaredType">字段的声明类型。</param>
        /// <param name="preference">非默认构造的处置档。</param>
        /// <param name="currentType">当前的具体类型；空槽位为 <c>null</c>。</param>
        private static void ShowMenu(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            Type declaredType,
            NonDefaultConstructorPreference preference,
            Type currentType)
        {
            var candidates = PolymorphicCandidateFilter.Candidates(
                declaredType, preference, out var duplicates, out var error);

            if (error != null)
            {
                DrawerWarnings.Once(
                    property,
                    nameof(PolymorphicRow) + ".scan",
                    $"[XInspector] 属性「{property.Path}」的多态选择器扫描失败：{error}");
                return;
            }

            if (candidates.Count == 0)
            {
                DrawerWarnings.Once(
                    property,
                    nameof(PolymorphicRow) + ".empty",
                    $"[XInspector] 属性「{property.Path}」的多态选择器没有候选" +
                    $"（声明类型 = {ReflectedAccessor.DescribeType(declaredType)}，" +
                    $"构造偏好 = {preference}）——检查那个类型下有没有**装得进、造得出**的实现。");
                return;
            }

            if (duplicates > 0)
            {
                DrawerWarnings.Once(
                    property,
                    nameof(PolymorphicRow) + ".duplicates",
                    $"[XInspector] 属性「{property.Path}」的多态选择器：有 {duplicates} 个候选与别的候选" +
                    "**全名相同**（同名类型落在不同程序集里）——只保留了其中一个。");
            }

            var options = TypeSelectorOptions.Build(candidates, currentType);
            var menu = new GenericMenu();
            var target = serializedProperty.Copy();
            var undoEnabled = property.Owner?.UndoEnabled ?? false;

            // 「（无）」清空项：**有值时才有**（空槽位点它什么都不发生）。
            if (currentType != null)
            {
                menu.AddItem(
                    new GUIContent(NoTypeText),
                    false,
                    OnSelected,
                    new Selection(target, null, declaredType, preference, undoEnabled));
                menu.AddSeparator(string.Empty);
            }

            for (var i = 0; i < options.Count; i++)
            {
                menu.AddItem(
                    new GUIContent(options[i].Path),
                    options[i].IsCurrent,
                    OnSelected,
                    new Selection(target, options[i].Type, declaredType, preference, undoEnabled));
            }

            menu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
        }

        /// <summary>菜单回调：写回选中的类型（<see cref="Selection.Value"/> 为 <c>null</c> 即清空）。</summary>
        /// <param name="userData"><see cref="Selection"/> 载荷。</param>
        private static void OnSelected(object userData)
        {
            var selection = (Selection)userData;

            if (selection.Value == null)
            {
                if (!PolymorphicSlotWrite.TryClear(
                        selection.Target, selection.DeclaredType, selection.UndoEnabled, out var clearReason))
                {
                    Debug.LogWarning($"[XInspector] [PolymorphicDrawerSettings] 清空不成功：{clearReason}");
                }

                return;
            }

            var current = selection.Target.managedReferenceValue?.GetType();

            if (!ShouldRewrite(current, selection.Value))
            {
                // 点当前类型 = 无操作：不拿同类型的新实例换掉用户已经填好的值。
                return;
            }

            if (!PolymorphicSlotWrite.TryWrite(
                    selection.Value,
                    selection.Preference,
                    selection.Target,
                    selection.DeclaredType,
                    selection.UndoEnabled,
                    out var reason))
            {
                Debug.LogWarning($"[XInspector] [PolymorphicDrawerSettings] 选中的类型写不进去：{reason}");
            }
        }

        #endregion

        /// <summary>
        /// 菜单回调的载荷。
        /// </summary>
        private readonly struct Selection
        {
            /// <summary>要写入的目标。</summary>
            public readonly SerializedProperty Target;

            /// <summary>选中的类型；<c>null</c> 表示清空。</summary>
            public readonly Type Value;

            /// <summary>字段的声明类型——写回判据用它，**不是**节点的 <c>Type</c>。</summary>
            public readonly Type DeclaredType;

            /// <summary>非默认构造的处置档。</summary>
            public readonly NonDefaultConstructorPreference Preference;

            /// <summary>这次提交要不要进撤销栈（窗口路径为假）。</summary>
            public readonly bool UndoEnabled;

            /// <summary>以五段构造。</summary>
            /// <param name="target">目标属性。</param>
            /// <param name="value">选中的类型；<c>null</c> 表示清空。</param>
            /// <param name="declaredType">字段的声明类型。</param>
            /// <param name="preference">非默认构造的处置档。</param>
            /// <param name="undoEnabled">要不要进撤销栈。</param>
            public Selection(
                SerializedProperty target,
                Type value,
                Type declaredType,
                NonDefaultConstructorPreference preference,
                bool undoEnabled)
            {
                Target = target;
                Value = value;
                DeclaredType = declaredType;
                Preference = preference;
                UndoEnabled = undoEnabled;
            }
        }
    }

    /// <summary>多态那一行归谁画。</summary>
    internal enum PolymorphicRowDisposition
    {
        /// <summary>空槽位：本包自己画这一行。</summary>
        DrawRow,

        /// <summary>已展开：行归末端画（链上这枚让路——不画、不告警）。</summary>
        TerminalOwnsRow,

        /// <summary>没有序列化后端 / 不是托管引用：告警一次 + 让路。</summary>
        FallbackNotBacked,

        /// <summary>是 <c>System.Type</c> 的类型槽位：告警一次 + 让路（那归 <c>[TypeDrawerSettings]</c>）。</summary>
        FallbackTypeSlot,

        /// <summary>多选值不一致：让路，不告警（常规状态）。</summary>
        FallbackMultiSelect,

        /// <summary>撞上多态守卫：让路，不告警（构建期已响过）。</summary>
        FallbackGuarded,

        /// <summary>有值但类型用不到本包：告警一次 + 让路。</summary>
        FallbackNotTakenOver,
    }

    /// <summary>
    /// 多态槽位的候选集：**声明类型的派生 ∩ 装得进 ∩ 造得出**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>「可实例化」这条收窄必须在过滤位之外另立</b>：<c>TypeInclusionFilter</c> 的匹配是
    /// 「求交非空」，而 <c>KindOf(List&lt;&gt;)</c> = 具体 + 泛型——传 <c>IncludeConcreteTypes</c>
    /// 照样收下开放泛型。过滤位**表达不了「开放泛型不要」**。
    /// </para>
    /// <para>
    /// <b>排除 <c>UnityEngine.Object</c> 一族</b>的理由是「造不出来 + 引用语义」：
    /// 本包的换类型是「造一个新实例」，而 <c>new</c> 一个 MonoBehaviour 是 Unity 明确报错的用法、
    /// ScriptableObject 也不该 <c>new</c>；Unity 对象有自己的生命周期与引用语义，
    /// 往托管引用槽位塞一份拷贝没有意义。（**不是**「Unity 不把它们序列化成托管引用」——
    /// 那句话要成立得先加探针，本批不加。）
    /// </para>
    /// <para>值类型照收：它们确实装得进槽位（<c>IsInstanceOfType</c> 照真）。</para>
    /// </remarks>
    internal static class PolymorphicCandidateFilter
    {
        /// <summary>装得进、造得出：非抽象、非接口、非开放泛型、非 <c>UnityEngine.Object</c> 一族。</summary>
        /// <param name="type">类型。</param>
        /// <returns>可以当候选返回 <c>true</c>。</returns>
        public static bool IsInstantiable(Type type)
        {
            return type != null &&
                   !type.IsAbstract &&
                   !type.IsInterface &&
                   !type.IsGenericTypeDefinition &&
                   !type.ContainsGenericParameters &&
                   !typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        /// <summary>有没有公开无参构造（值类型天然有）——<c>Exclude</c> 档的收窄判据。</summary>
        /// <param name="type">类型。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        public static bool HasParameterlessConstruction(Type type)
        {
            return type != null && (type.IsValueType || type.GetConstructor(Type.EmptyTypes) != null);
        }

        /// <summary>
        /// 扫出候选：声明类型的派生（<c>TypeCache</c>）∩ 可实例化 ∩ 构造偏好。
        /// </summary>
        /// <param name="declaredType">**字段的声明类型**（多态槽位的语义是「装得进这个槽位」）。</param>
        /// <param name="preference">非默认构造的处置档（<c>Exclude</c> 会剔掉无参构造缺失者）。</param>
        /// <param name="duplicates">被丢弃的「全名相同」候选个数。</param>
        /// <param name="error">扫描本身失败的原因；成功时为 <c>null</c>。</param>
        /// <returns>成品候选表（已排序、已去重）。</returns>
        public static List<Type> Candidates(
            Type declaredType,
            NonDefaultConstructorPreference preference,
            out int duplicates,
            out string error)
        {
            duplicates = 0;
            error = null;

            var result = new List<Type>();

            if (declaredType == null)
            {
                return result;
            }

            var raw = TypeCandidateQuery.Collect(
                declaredType, TypeInclusionFilter.IncludeConcreteTypes, out _, out error);

            if (error != null)
            {
                return result;
            }

            // **声明类型自己也得是候选**：`[SerializeReference] Circle c;` 这种声明成具体类的槽位，
            // 唯一合理的选项就是 `Circle`——而 `Collect` 的语义是「派生」，会把它剔掉
            //（不并回来的话菜单是空的）。
            var source = new List<Type>(raw.Count + 1);
            source.AddRange(raw);
            source.Add(declaredType);

            for (var i = 0; i < source.Count; i++)
            {
                var type = source[i];

                if (!IsInstantiable(type))
                {
                    continue;
                }

                if (preference == NonDefaultConstructorPreference.Exclude &&
                    !HasParameterlessConstruction(type))
                {
                    continue;
                }

                result.Add(type);
            }

            return TypeCandidateFilter.Apply(result, TypeInclusionFilter.IncludeConcreteTypes, out duplicates);
        }
    }
}
