using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="TypeDrawerSettingsAttribute"/>：<c>System.Type</c> 字段画成**类型选择器**——
    /// 一行「当前类型名」按钮，点开是本包自绘的候选菜单。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>替换型</b>：画完不调下一个（原生那行不画）。故只读要自己套
    /// <see cref="EditorGUI.DisabledScope"/>；多选混合时**退回原生那一行**
    /// （本包的选择器表达不了「各目标类型不同」，而原生多态行本来就支持多目标赋值——
    /// 退回不是能力倒退，与 <c>[PropertyRange]</c> 在多选下的处置同款，且**不告警**）。
    /// </para>
    /// <para>
    /// <b>弹出层是 <see cref="GenericMenu"/>、不自建窗口</b>：与 <c>[AssetSelector]</c> /
    /// <c>[ValueDropdown]</c> 同款。候选**只在点击那一刻现算**（<c>TypeCache</c> 自己就是缓存，
    /// 本包不再加一层——多一个失效点不划算）。
    /// </para>
    /// <para>
    /// 判定与构造全是纯函数（<see cref="TypeSelectorTarget"/>、<see cref="TypeSelectorOptions"/>、
    /// <see cref="TypeCandidateFilter"/>）；<c>TypeCache</c> 只在 <see cref="TypeCandidateQuery"/>
    /// 里出现一次——与 <c>AssetSelectorQuery</c> 收口 <c>AssetDatabase</c> 的先例同理。
    /// </para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class TypeDrawerSettingsDrawer : AttributeDrawer<TypeDrawerSettingsAttribute>
    {
        #region Private Fields

        /// <summary>空槽位的按钮文本（本包自定，见 README）。</summary>
        private static readonly GUIContent NoTypeLabel = new GUIContent("（无）");

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property, TypeDrawerSettingsAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null ||
                serializedProperty.propertyType != SerializedPropertyType.ManagedReference)
            {
                DrawerWarnings.Once(
                    property,
                    nameof(TypeDrawerSettingsDrawer) + ".backend",
                    DrawerWarnings.TypeMismatch(property, "[TypeDrawerSettings]", "Unity 的序列化后端（托管引用）"));
                CallNextDrawer(property, label);
                return;
            }

            // **字段的声明类型**，不是节点的 Type——槽位有值时后者是 RuntimeType（写回判据要用前者）。
            var declaredType = (property.Member as FieldInfo)?.FieldType ?? property.Type;

            if (!TypeSelectorTarget.IsTypeSlot(serializedProperty, declaredType))
            {
                DrawerWarnings.Once(
                    property,
                    nameof(TypeDrawerSettingsDrawer) + ".target",
                    $"[XInspector] 属性「{property.Path}」上的 [TypeDrawerSettings] 只作用在 System.Type 字段上" +
                    $"（这一格的声明类型是 {ReflectedAccessor.DescribeType(declaredType)}）——原样退回。");
                CallNextDrawer(property, label);
                return;
            }

            if (serializedProperty.hasMultipleDifferentValues)
            {
                // 多选值不一致：拿一个目标冒充全体是明令禁止的，退回原生那一行（它能多目标赋值）。
                CallNextDrawer(property, label);
                return;
            }

            DrawWide(property, serializedProperty, attribute, declaredType, label);
        }

        #endregion

        #region Private Helpers

        /// <summary>替换形态：整行一个「像字段的下拉按钮」，显示当前选中类型的名字。</summary>
        private static void DrawWide(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            TypeDrawerSettingsAttribute attribute,
            Type declaredType,
            GUIContent label)
        {
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginProperty(rect, label, serializedProperty);

            var content = label != null && label != GUIContent.none ? EditorGUI.PrefixLabel(rect, label) : rect;

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                var current = serializedProperty.managedReferenceValue as Type;
                var text = current == null ? NoTypeLabel : new GUIContent(TypeSelectorOptions.DisplayName(current));

                if (GUI.Button(content, text, EditorStyles.popup))
                {
                    ShowMenu(property, serializedProperty, attribute, declaredType);
                }
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// 弹出候选菜单；选中则写回并**当场提交**（见 <see cref="TypeSlotWrite"/>）。
        /// </summary>
        /// <remarks>
        /// 载荷用 <see cref="Selection"/> + <c>MenuFunction2</c>，**不为每个选项建闭包**
        /// （与既有两个菜单同款）。
        /// </remarks>
        private static void ShowMenu(
            InspectorProperty property,
            SerializedProperty serializedProperty,
            TypeDrawerSettingsAttribute attribute,
            Type declaredType)
        {
            var candidates = TypeCandidateQuery.Collect(
                attribute.BaseType, attribute.Filter, out var duplicates, out var error);

            if (error != null)
            {
                DrawerWarnings.Once(
                    property,
                    nameof(TypeDrawerSettingsDrawer) + ".scan",
                    $"[XInspector] 属性「{property.Path}」的类型选择器扫描失败：{error}");
                return;
            }

            if (candidates.Count == 0)
            {
                DrawerWarnings.Once(
                    property,
                    nameof(TypeDrawerSettingsDrawer) + ".empty",
                    $"[XInspector] 属性「{property.Path}」的类型选择器没有候选" +
                    $"（BaseType = {DescribeBaseType(attribute.BaseType)}，Filter = {attribute.Filter}）——" +
                    "检查基类型与过滤位。");
                return;
            }

            if (duplicates > 0)
            {
                DrawerWarnings.Once(
                    property,
                    nameof(TypeDrawerSettingsDrawer) + ".duplicates",
                    $"[XInspector] 属性「{property.Path}」的类型选择器：有 {duplicates} 个候选与别的候选**全名相同**" +
                    "（同名类型落在不同程序集里）——只保留了其中一个。");
            }

            var current = serializedProperty.managedReferenceValue as Type;
            var options = TypeSelectorOptions.Build(candidates, current);

            var menu = new GenericMenu();
            var target = serializedProperty.Copy();

            // 「（无）」清空项：**有值时才有**——空槽位点它什么都不发生，
            // 与「只声明有真行为的选项」冲突；而按钮那时本来写着「（无）」。
            if (current != null)
            {
                menu.AddItem(NoTypeLabel, false, OnSelected, new Selection(target, null, declaredType));
                menu.AddSeparator(string.Empty);
            }

            for (var i = 0; i < options.Count; i++)
            {
                menu.AddItem(
                    new GUIContent(options[i].Path),
                    options[i].IsCurrent,
                    OnSelected,
                    new Selection(target, options[i].Type, declaredType));
            }

            menu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
        }

        /// <summary>菜单回调：写回选中的类型（<see cref="Selection.Value"/> 为 <c>null</c> 即清空）。</summary>
        /// <param name="userData"><see cref="Selection"/> 载荷。</param>
        private static void OnSelected(object userData)
        {
            var selection = (Selection)userData;

            if (TypeSlotWrite.TryWrite(selection.Value, selection.Target, selection.DeclaredType, out var reason))
            {
                return;
            }

            Debug.LogWarning($"[XInspector] [TypeDrawerSettings] 选中的类型写不进去：{reason}");
        }

        /// <summary>把基类型说成一句人能读的话（<c>null</c> 时说明它的实际语义）。</summary>
        /// <param name="baseType">特性上写的基类型。</param>
        /// <returns>描述文本。</returns>
        private static string DescribeBaseType(Type baseType)
        {
            return baseType == null ? "（不额外约束，按 object 收；接口不在其列）" : ReflectedAccessor.DescribeType(baseType);
        }

        #endregion

        /// <summary>
        /// 菜单回调的载荷。
        /// </summary>
        private readonly struct Selection
        {
            /// <summary>要写入的目标（类型槽位）。</summary>
            public readonly SerializedProperty Target;

            /// <summary>选中的类型；<c>null</c> 表示清空。</summary>
            public readonly Type Value;

            /// <summary>字段的声明类型——写回判据用它，**不是**节点的 <c>Type</c>。</summary>
            public readonly Type DeclaredType;

            /// <summary>以三段构造。</summary>
            /// <param name="target">目标属性。</param>
            /// <param name="value">选中的类型；<c>null</c> 表示清空。</param>
            /// <param name="declaredType">字段的声明类型。</param>
            public Selection(SerializedProperty target, Type value, Type declaredType)
            {
                Target = target;
                Value = value;
                DeclaredType = declaredType;
            }
        }
    }

    /// <summary>
    /// 类型槽位的判据：托管引用、且声明类型装得下 <c>System.Type</c>。
    /// </summary>
    /// <remarks>纯判据，可无头测试——绘制器只负责按结论分派。</remarks>
    internal static class TypeSelectorTarget
    {
        /// <summary>
        /// 这格是不是「类型槽位」。
        /// </summary>
        /// <param name="property">序列化属性。</param>
        /// <param name="declaredFieldType">**字段的声明类型**（<c>FieldInfo.FieldType</c>）。</param>
        /// <returns>是返回 <c>true</c>。</returns>
        public static bool IsTypeSlot(SerializedProperty property, Type declaredFieldType)
        {
            return property != null &&
                   property.propertyType == SerializedPropertyType.ManagedReference &&
                   declaredFieldType != null &&
                   typeof(Type).IsAssignableFrom(declaredFieldType);
        }

        /// <summary>
        /// 这一格能不能弹本包的选择器：是托管引用、且不是多选混合态。
        /// </summary>
        /// <param name="property">序列化属性。</param>
        /// <returns>能返回 <c>true</c>。</returns>
        public static bool CanSelect(SerializedProperty property)
        {
            return property != null &&
                   property.propertyType == SerializedPropertyType.ManagedReference &&
                   !property.hasMultipleDifferentValues;
        }
    }

    /// <summary>
    /// 候选的**种类过滤**：把一个类型归到「抽象 / 具体 / 泛型 / 接口」各位，与过滤器求交。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 判据是**求交非空**（<c>TypeInclusionFilter</c> 的说明）；一个类型可以同时命中多位：
    /// **泛型接口**同时命中「泛型」与「接口」，**静态类**归「抽象」（IL 里是 <c>abstract sealed</c>）。
    /// </para>
    /// <para>
    /// **编译器生成的类型无条件剔除**（闭包、匿名类型……）：它们不在过滤位的描述范围里，
    /// 出现在菜单里是纯噪音。
    /// </para>
    /// </remarks>
    internal static class TypeCandidateFilter
    {
        /// <summary>按全名排序的比较器（序数比较，与既有选项表同款）。</summary>
        private static readonly Comparison<Type> ByFullName =
            (a, b) => string.CompareOrdinal(a.FullName, b.FullName);

        /// <summary>把一个类型归到哪几位。</summary>
        /// <param name="type">类型。</param>
        /// <returns>种类掩码（可能命中多位）。</returns>
        public static TypeInclusionFilter KindOf(Type type)
        {
            var kind = TypeInclusionFilter.None;

            if (type.IsInterface)
            {
                kind |= TypeInclusionFilter.IncludeInterfaces;
            }
            else if (type.IsAbstract)
            {
                // 静态类走这里：IL 里它就是 abstract sealed，不特判（自定边界，用例钉住）。
                kind |= TypeInclusionFilter.IncludeAbstracts;
            }
            else
            {
                kind |= TypeInclusionFilter.IncludeConcreteTypes;
            }

            if (type.IsGenericTypeDefinition || type.ContainsGenericParameters)
            {
                kind |= TypeInclusionFilter.IncludeGenerics;
            }

            return kind;
        }

        /// <summary>这个类型过不过滤。</summary>
        /// <param name="type">类型。</param>
        /// <param name="filter">过滤位。</param>
        /// <returns>收返回 <c>true</c>。</returns>
        public static bool IsIncluded(Type type, TypeInclusionFilter filter)
        {
            return (filter & KindOf(type)) != 0;
        }

        /// <summary>
        /// 是不是「编译器生成的噪音」（闭包显示类、匿名类型、泛型参数……）——无条件剔除。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <returns>是噪音返回 <c>true</c>。</returns>
        public static bool IsNoise(Type type)
        {
            if (type == null || type.FullName == null || type.IsGenericParameter)
            {
                return true;
            }

            if (type.Name.Length > 0 && type.Name[0] == '<')
            {
                return true;
            }

            return type.IsDefined(typeof(CompilerGeneratedAttribute), false);
        }

        /// <summary>
        /// 过滤 + 去重 + 排序，一步到位（只在点击那一刻跑）。
        /// </summary>
        /// <param name="source">原始候选。</param>
        /// <param name="filter">过滤位。</param>
        /// <param name="duplicateCount">被丢弃的「全名相同」候选个数。</param>
        /// <returns>成品候选表。</returns>
        public static List<Type> Apply(IEnumerable<Type> source, TypeInclusionFilter filter, out int duplicateCount)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<Type>();
            duplicateCount = 0;

            foreach (var type in source)
            {
                if (IsNoise(type) || !IsIncluded(type, filter))
                {
                    continue;
                }

                if (!seen.Add(type.FullName))
                {
                    // 跨程序集撞名真会发生（vendored 副本）。保留第一个并计数——
                    // 绘制器会告警一次（不静默）；**不做**「拼程序集名」那种补救
                    //（菜单立刻难看，而两个 Type 值写回去都成立）。
                    duplicateCount++;
                    continue;
                }

                result.Add(type);
            }

            result.Sort(ByFullName);
            return result;
        }
    }

    /// <summary>
    /// 候选的来源：<c>TypeCache</c> 在本包的**唯一出现处**（照 <c>AssetSelectorQuery</c>
    /// 收口 <c>AssetDatabase</c> 的先例）。
    /// </summary>
    internal static class TypeCandidateQuery
    {
        /// <summary>
        /// 扫出候选：<paramref name="baseType"/> 的派生，剔除它自己，再走种类过滤。
        /// </summary>
        /// <param name="baseType">基类型；<c>null</c> 表示不额外约束（按 <c>object</c> 收，**不含接口**）。</param>
        /// <param name="filter">种类过滤位。</param>
        /// <param name="duplicateCount">被丢弃的「全名相同」候选个数。</param>
        /// <param name="error">扫描本身失败的原因；成功时为 <c>null</c>。</param>
        /// <returns>成品候选表（失败时为空表，原因在 <paramref name="error"/> 里，**不静默**）。</returns>
        /// <remarks>
        /// 开放泛型实测**不抛**（见 <c>TypeCacheProbeTests</c>），<c>try/catch</c> 是保险：
        /// 真抛了也要给「空候选 + 一句原因」而不是把编辑器炸掉——外部 API 的行为不猜。
        /// </remarks>
        public static List<Type> Collect(
            Type baseType, TypeInclusionFilter filter, out int duplicateCount, out string error)
        {
            duplicateCount = 0;
            error = null;

            var effective = baseType ?? typeof(object);

            try
            {
                var raw = new List<Type>(TypeCache.GetTypesDerivedFrom(effective));
                raw.RemoveAll(type => type == effective); // 基类型自身不是候选（语义是「派生」）。
                return TypeCandidateFilter.Apply(raw, filter, out duplicateCount);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return new List<Type>();
            }
        }
    }

    /// <summary>
    /// 选项表的构造：菜单路径的分层与显示名。纯逻辑，可无头测试。
    /// </summary>
    /// <remarks>
    /// <b>默认按命名空间分层</b>：本包没有搜索框（<c>GenericMenu</c> 的限制），几百项的平铺菜单
    /// 不可用，分层是唯一可用的导航。Odin 的 <c>PreferNamespaces</c> / <c>ShowCategories</c> 是
    /// <c>[TypeSelectorSettings]</c> 的旋钮、**本批不声明**——这里写的是本批的默认行为。
    /// </remarks>
    internal static class TypeSelectorOptions
    {
        /// <summary>
        /// 选项表里的一项。
        /// </summary>
        public readonly struct Option
        {
            /// <summary>以三段构造。</summary>
            /// <param name="path">菜单路径（命名空间用 <c>/</c> 分层）。</param>
            /// <param name="type">这一项的类型。</param>
            /// <param name="isCurrent">是不是当前选中的那个。</param>
            public Option(string path, Type type, bool isCurrent)
            {
                Path = path;
                Type = type;
                IsCurrent = isCurrent;
            }

            /// <summary>菜单路径。</summary>
            public string Path { get; }

            /// <summary>类型。</summary>
            public Type Type { get; }

            /// <summary>是不是当前值（菜单里带勾）。</summary>
            public bool IsCurrent { get; }
        }

        /// <summary>建选项表：菜单路径 + 当前值标勾。</summary>
        /// <param name="candidates">候选（已排序）。</param>
        /// <param name="current">当前选中的类型；可以为 <c>null</c>。</param>
        /// <returns>选项表。</returns>
        public static List<Option> Build(IList<Type> candidates, Type current)
        {
            var options = new List<Option>(candidates.Count);

            for (var i = 0; i < candidates.Count; i++)
            {
                var type = candidates[i];
                options.Add(new Option(MenuPath(type), type, type == current));
            }

            return options;
        }

        /// <summary>
        /// 菜单路径 = 命名空间各段（<c>/</c> 分层）+ 显示名。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <returns>菜单路径。</returns>
        /// <remarks>
        /// <c>/</c> **不需要转义**：C# 标识符与 <c>FullName</c> 里不可能有它。
        /// 全局命名空间的类型没有前缀，直接落在根上。
        /// </remarks>
        public static string MenuPath(Type type)
        {
            var display = DisplayName(type);
            var ns = type.Namespace;

            return string.IsNullOrEmpty(ns) ? display : ns.Replace('.', '/') + "/" + display;
        }

        /// <summary>
        /// 显示名（菜单叶名 = 按钮文本，同一个函数）：去命名空间、嵌套的 <c>+</c> 换 <c>.</c>、
        /// 元数记号 <c>`2</c> 换 <c>&lt;&gt;</c>。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <returns>显示名（如 <c>List&lt;&gt;.Entry&lt;&gt;</c>）。</returns>
        public static string DisplayName(Type type)
        {
            var name = type.FullName ?? type.Name;
            var ns = type.Namespace;

            if (!string.IsNullOrEmpty(ns) && name.StartsWith(ns + ".", StringComparison.Ordinal))
            {
                name = name.Substring(ns.Length + 1);
            }

            name = name.Replace('+', '.');
            return name.IndexOf('`') < 0 ? name : ReplaceArity(name);
        }

        /// <summary>把 <c>`N</c> 这样的元数记号换成 <c>&lt;&gt;</c>。</summary>
        /// <param name="name">名字。</param>
        /// <returns>替换后的名字。</returns>
        private static string ReplaceArity(string name)
        {
            var builder = new StringBuilder(name.Length + 2);

            for (var i = 0; i < name.Length; i++)
            {
                if (name[i] != '`')
                {
                    builder.Append(name[i]);
                    continue;
                }

                builder.Append("<>");

                while (i + 1 < name.Length && char.IsDigit(name[i + 1]))
                {
                    i++;
                }
            }

            return builder.ToString();
        }
    }
}
