using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 成员引用阶梯的**序列化那条腿**：在同层兄弟成员、根绝对名这些位置上找序列化成员。
    /// <para>
    /// <b>它是腿，不是入口。</b> 入口是 <see cref="MemberReferenceResolver"/>——那层负责
    /// 「序列化找不到时再走反射」的次序与种类判定；本类只管「按范围把一个
    /// <see cref="SerializedProperty"/> 取出来」。<c>[ValueDropdown]</c> 的数据源是唯一
    /// 仍直接调用本类的消费者：它要的是**一个数组**而不是一个值，而消费侧（选项表与值复制）
    /// 目前只吃 <see cref="SerializedProperty"/>——给它接反射源要另建一套形态与写回通道，
    /// 与「解析」不是同一件事。
    /// </para>
    /// <para>
    /// 台词（<see cref="MemberScope"/> / <see cref="MemberKind"/>）定义在
    /// <see cref="MemberReferenceResolver"/> 那一份文件里——那是这一层共用的词汇。
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
        /// 成员名。<see cref="MemberScope.Relative"/> 时是值对象内部的相对路径。
        /// </param>
        /// <param name="scope">查找范围。</param>
        /// <param name="kind">类型约束。</param>
        /// <param name="member">解析出的序列化属性；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        public static bool TryResolve(
            InspectorProperty property,
            string memberName,
            MemberScope scope,
            MemberKind kind,
            out SerializedProperty member,
            out string reason)
        {
            member = null;

            var found = TryFind(property, memberName, scope, out reason);
            if (found == null)
            {
                return false;
            }

            if (!IsKind(found, kind))
            {
                reason = $"该成员不是 {MemberKindNames.Describe(kind)}（实为 {found.propertyType}）";
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
        public static bool IsKind(SerializedProperty property, MemberKind kind)
        {
            if (property == null)
            {
                return false;
            }

            switch (kind)
            {
                case MemberKind.Boolean:
                    return property.propertyType == SerializedPropertyType.Boolean;
                case MemberKind.Float:
                    return property.propertyType == SerializedPropertyType.Float;
                case MemberKind.Vector2:
                    return property.propertyType == SerializedPropertyType.Vector2;
                case MemberKind.Array:
                    // **字符串要挡掉**：Unity 在若干语境把它算作 isArray（见 ValueDropdownTarget
                    // 的同款说明），而它当选项来源只会得到一张空表——「解析成功却什么都没有」
                    // 是本包最忌讳的静默形态。`[ValueDropdown]` 标在字符串**字段**上另说，
                    // 那是目标不是来源。
                    return property.isArray && property.propertyType != SerializedPropertyType.String;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 按范围取成员——**不做类型判定**，找到就返回。失败时填 <paramref name="reason"/>。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="scope">查找范围。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>序列化属性；失败返回 <c>null</c>。</returns>
        /// <remarks>
        /// 与 <see cref="TryResolve"/> 分成两个入口，是因为「找到了但类型不符」与「根本没找到」
        /// 的**后果不同**：前者要停住（不再往下走反射那两级），后者才轮到反射。
        /// 只给一个布尔返回的话，调用方分不出这两种，只能靠解析原因字符串——
        /// 那正是「判据靠文案」这类坏味道的开端。
        /// </remarks>
        public static SerializedProperty TryFind(
            InspectorProperty property,
            string memberName,
            MemberScope scope,
            out string reason)
        {
            reason = null;

            if (scope == MemberScope.Relative)
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

            // 第 0 级（**只在嵌套层生效**）：最近的复合成员容器里的同层成员。找到了就返回
            // ——哪怕类型不符也是。**不在这里往下找**：那两步（继续找根绝对名 / 走反射）
            // 是入口那一层的事，判定「找到了该不该停」也需要它手上的类型约束。
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
