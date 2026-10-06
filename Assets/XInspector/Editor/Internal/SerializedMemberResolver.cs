using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 成员查找的范围。
    /// </summary>
    internal enum SerializedMemberScope
    {
        /// <summary>
        /// 就近查找：**先**在「最近的复合成员容器」里找（即同层的兄弟成员），
        /// **找不到再回落**到属性所属序列化对象上的绝对路径（支持 <c>a.b</c> 点分路径）。
        /// </summary>
        /// <remarks>
        /// 两级顺序与条件族同款（见 <c>ConditionResolver</c>）：嵌套层里写 <c>[ToggleGroup("flag")]</c>
        /// 指的是**同层的** <c>flag</c>，指错了「看的是一个对象、取的是另一个对象」——
        /// 静默且极难归因。顶层成员与顶层分组没有嵌套容器，两级恒等，故对既有行为零变化。
        /// </remarks>
        Object = 0,

        /// <summary>在属性自己的值对象内部查找（如 <c>[Toggle]</c> 的开关字段）。</summary>
        Relative = 1,
    }

    /// <summary>
    /// 成员必须满足的类型约束。
    /// </summary>
    /// <remarks>
    /// 只列**有调用方**的几种。加一种就意味着一条新的校验分支与一句新的告警文本，
    /// 等真有特性需要时再加。
    /// </remarks>
    internal enum SerializedMemberKind
    {
        /// <summary>bool。</summary>
        Boolean = 0,

        /// <summary>float。</summary>
        Float = 1,

        /// <summary>Vector2。</summary>
        Vector2 = 2,

        /// <summary>数组或 List（判定依据是 <see cref="SerializedProperty.isArray"/>）。</summary>
        Array = 3,
    }

    /// <summary>
    /// 「按名找一个序列化成员」的公共实现。
    /// <para>
    /// 本包有多处需要这件事：条件族、<c>[Toggle]</c>、<c>[ToggleGroup]</c>，以及靠
    /// <b>成员引用</b>取参数的值绘制器（<c>[MinMaxSlider]</c> 的动态边界、
    /// <c>[ValueDropdown]</c> 的数据源）。它们找的位置（同一个对象上 / 值对象内部）、
    /// 要的类型、失败的后果都不同，但「找不到 / 类型不符 → 给一句人能读的中文原因」
    /// 是同一件事，收敛在这里。
    /// </para>
    /// <para>
    /// <b>只管解析，不管告警。</b> 调用方的告警机制本就不同且各自正确：
    /// 条件族与 <c>[Toggle]</c> 在**构建期**直接 <c>Debug.LogWarning</c>（每次建树重报一次），
    /// <c>[ToggleGroup]</c> 在**绘制期**用 <see cref="DrawerWarnings.Once"/>（每属性只报一次）。
    /// 统一告警会把其中一方改坏，故只统一解析。
    /// </para>
    /// <para>
    /// <b>失败一律不抛。</b> 一个拼错的成员名不该让整个 Inspector 白屏——那是使用方
    /// 看到本插件的第一眼。放弃的后果由调用方决定（条件族是「条件不生效、字段照常显示」，
    /// 值绘制器是「退回普通绘制」），配合告警足以定位。
    /// </para>
    /// </summary>
    internal static class SerializedMemberResolver
    {
        #region Public API

        /// <summary>
        /// 取属性所属的序列化对象。分组与根节点没有值入口，沿后代找**第一个**带值入口的节点。
        /// </summary>
        /// <param name="property">起点。</param>
        /// <returns>序列化对象；整棵子树都没有值入口时返回 <c>null</c>。</returns>
        /// <remarks>
        /// <para>
        /// 不能假定 <c>Children[0]</c>：页签容器的第一个孩子是页节点，同样是分组、
        /// 同样没有值入口。
        /// </para>
        /// <para>
        /// <b>先走 <see cref="InspectorProperty.Owner"/>。</b> 方法节点（<c>[Button]</c> 一族）
        /// 既没有值入口也没有子节点，只靠向下找会一路 null，症状是「按钮上的条件静默失效」
        /// 加上每次建树一条「取不到序列化对象」的告警。
        /// </para>
        /// </remarks>
        public static SerializedObject FindSerializedObject(InspectorProperty property)
        {
            if (property == null)
            {
                return null;
            }

            // 树是权威来源：它直接持有构造它的那个序列化对象。
            var owner = property.Owner;
            if (owner != null)
            {
                return owner.SerializedObject;
            }

            var entry = property.ValueEntry;
            if (entry?.SerializedProperty != null)
            {
                return entry.SerializedProperty.serializedObject;
            }

            for (var i = 0; i < property.Children.Count; i++)
            {
                var found = FindSerializedObject(property.Children[i]);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// 取「最近的复合成员容器」的序列化属性——沿父链上溯、**跳过分组节点**。
        /// </summary>
        /// <param name="property">起点。</param>
        /// <returns>容器的序列化属性；没有则返回 <c>null</c>。</returns>
        /// <remarks>
        /// <para>
        /// 用途是「嵌套层里的成员引用优先解析到同层」：嵌套层里写 <c>[ShowIf("flag")]</c>
        /// 或 <c>[ToggleGroup("flag")]</c> 指的是**同层的** <c>flag</c>。
        /// </para>
        /// <para>
        /// <b>必须跳过分组节点。</b> 分组节点把成员包在中间（嵌套层里尤其如此：
        /// <c>stats/组/条件组</c> 的父节点就是分组节点），只看直接父节点会返回 <c>null</c>，
        /// 于是回落到「根上的绝对名」——**静默地看错了对象**。
        /// </para>
        /// <para>
        /// 上溯到根就停：顶层成员与顶层分组都没有嵌套容器，返回 <c>null</c> 即「按绝对名走」，
        /// 与从前逐字一致。
        /// </para>
        /// </remarks>
        public static SerializedProperty FindNestedScope(InspectorProperty property)
        {
            return FindNestedScopeNode(property)?.ValueEntry?.SerializedProperty;
        }

        /// <summary>
        /// 取「最近的复合成员容器」**节点**——沿父链上溯、跳过分组节点。
        /// </summary>
        /// <param name="property">起点。</param>
        /// <returns>容器节点；没有则返回 <c>null</c>。</returns>
        /// <remarks>
        /// 与 <see cref="FindNestedScope"/> 是同一件事的两种出口：那个给序列化属性（按名找成员用），
        /// 这个给节点本身——嵌套层的**读路径**要拿它的 <see cref="InspectorProperty.Type"/> 与
        /// <see cref="InspectorProperty.Path"/> 才能编译出访问器。
        /// </remarks>
        public static InspectorProperty FindNestedScopeNode(InspectorProperty property)
        {
            var parent = property?.Parent;

            while (parent != null)
            {
                if (parent.Kind == InspectorPropertyKind.Member)
                {
                    return parent;
                }

                if (parent.Kind == InspectorPropertyKind.Root)
                {
                    return null;
                }

                parent = parent.Parent;
            }

            return null;
        }

        /// <summary>
        /// 按名解析成员并校验类型。失败不抛，只给出中文原因。
        /// </summary>
        /// <param name="property">目标属性，用来定位序列化对象。</param>
        /// <param name="memberName">
        /// 成员名。<see cref="SerializedMemberScope.Relative"/> 时是值对象内部的相对路径。
        /// </param>
        /// <param name="scope">查找范围。</param>
        /// <param name="kind">类型约束。</param>
        /// <param name="member">解析出的序列化属性；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        public static bool TryResolve(
            InspectorProperty property,
            string memberName,
            SerializedMemberScope scope,
            SerializedMemberKind kind,
            out SerializedProperty member,
            out string reason)
        {
            member = null;
            reason = null;

            var found = Find(property, memberName, scope, out reason);
            if (found == null)
            {
                return false;
            }

            if (!IsKind(found, kind))
            {
                reason = $"该成员不是 {DescribeKind(kind)}（实为 {found.propertyType}）";
                return false;
            }

            member = found;
            return true;
        }

        /// <summary>
        /// 判定一个序列化属性是否满足类型约束。
        /// </summary>
        /// <param name="property">待判定的属性。</param>
        /// <param name="kind">类型约束。</param>
        /// <returns>满足返回 <c>true</c>；<paramref name="property"/> 为 <c>null</c> 时返回 <c>false</c>。</returns>
        public static bool IsKind(SerializedProperty property, SerializedMemberKind kind)
        {
            if (property == null)
            {
                return false;
            }

            switch (kind)
            {
                case SerializedMemberKind.Boolean:
                    return property.propertyType == SerializedPropertyType.Boolean;
                case SerializedMemberKind.Float:
                    return property.propertyType == SerializedPropertyType.Float;
                case SerializedMemberKind.Vector2:
                    return property.propertyType == SerializedPropertyType.Vector2;
                case SerializedMemberKind.Array:
                    return property.isArray;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 类型约束的中文说法，用于告警文本。
        /// </summary>
        /// <param name="kind">类型约束。</param>
        /// <returns>可读文本。</returns>
        public static string DescribeKind(SerializedMemberKind kind)
        {
            switch (kind)
            {
                case SerializedMemberKind.Boolean:
                    return "bool";
                case SerializedMemberKind.Float:
                    return "float";
                case SerializedMemberKind.Vector2:
                    return "Vector2";
                case SerializedMemberKind.Array:
                    return "数组或 List";
                default:
                    return kind.ToString();
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 按范围取成员；失败时填 <paramref name="reason"/>。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="scope">查找范围。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>序列化属性；失败返回 <c>null</c>。</returns>
        private static SerializedProperty Find(
            InspectorProperty property,
            string memberName,
            SerializedMemberScope scope,
            out string reason)
        {
            reason = null;

            if (scope == SerializedMemberScope.Relative)
            {
                var owner = property?.ValueEntry?.SerializedProperty;
                if (owner == null)
                {
                    reason = "该属性没有序列化后端";
                    return null;
                }

                var relative = owner.FindPropertyRelative(memberName);
                if (relative == null)
                {
                    reason = "在值对象内部找不到这个成员（名字拼错，或它不在序列化范围内）";
                }

                return relative;
            }

            // 第 0 级（**只在嵌套层生效**）：最近的复合成员容器里的同层成员。
            // 找到了就用——哪怕类型不符也不继续往下找，与条件族同款：继续找会报第二次警，
            // 而两条消息互相矛盾（一句说「找到了但类型不对」、一句说「找不到」）。
            var container = FindNestedScope(property);
            if (container != null)
            {
                var sibling = container.FindPropertyRelative(memberName);
                if (sibling != null)
                {
                    return sibling;
                }
            }

            var serializedObject = FindSerializedObject(property);
            if (serializedObject == null)
            {
                reason = "取不到序列化对象（这个节点下没有任何带值入口的成员）";
                return null;
            }

            var absolute = serializedObject.FindProperty(memberName);
            if (absolute == null)
            {
                reason = "在同一个对象上找不到这个成员（名字拼错，或它不是序列化成员）";
            }

            return absolute;
        }

        #endregion
    }
}
