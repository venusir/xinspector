using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 成员查找的范围。
    /// </summary>
    internal enum SerializedMemberScope
    {
        /// <summary>在属性所属的序列化对象上按属性路径查找（支持 <c>a.b</c> 点分路径）。</summary>
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
