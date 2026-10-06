using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 成员引用的查找范围。
    /// </summary>
    internal enum MemberScope
    {
        /// <summary>
        /// 就近查找：**先**在「最近的复合成员容器」里找（即同层的兄弟成员），
        /// **找不到再回落**到属性所属序列化对象上的绝对路径（支持 <c>a.b</c> 点分路径），
        /// 仍找不到才走反射那一级。
        /// </summary>
        /// <remarks>
        /// 两级顺序与条件族同款：嵌套层里写 <c>[ToggleGroup("flag")]</c> 指的是**同层的**
        /// <c>flag</c>，指错了「看的是一个对象、取的是另一个对象」——静默且极难归因。
        /// 顶层成员与顶层分组没有嵌套容器，两级恒等，故对既有行为零变化。
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
    internal enum MemberKind
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
    /// <see cref="MemberKind"/> 的两种说法：告警文案用的名字，以及「它对应哪个 CLR 类型」
    /// ——反射腿要拿这个类型去比对成员的类型。
    /// </summary>
    internal static class MemberKindNames
    {
        #region Public API

        /// <summary>
        /// 约束对应的 CLR 类型。
        /// </summary>
        /// <param name="kind">类型约束。</param>
        /// <returns>类型；<see cref="MemberKind.Array"/> 返回 <c>null</c>——它没有单一的 CLR 类型
        /// （判据是「是不是数组」，而不是某个具体的类型）。</returns>
        public static Type TypeOf(MemberKind kind)
        {
            switch (kind)
            {
                case MemberKind.Boolean:
                    return typeof(bool);
                case MemberKind.Float:
                    return typeof(float);
                case MemberKind.Vector2:
                    return typeof(Vector2);
                default:
                    return null;
            }
        }

        /// <summary>
        /// 约束的中文说法，用于告警文本。
        /// </summary>
        /// <param name="kind">类型约束。</param>
        /// <returns>可读文本。</returns>
        public static string Describe(MemberKind kind)
        {
            return kind == MemberKind.Array ? "数组或 List" : ReflectedAccessor.DescribeType(TypeOf(kind));
        }

        #endregion
    }

    /// <summary>
    /// 「按名找一个成员」——**本包唯一的一处**。条件族（含 <c>[InfoBox]</c> 的 <c>visibleIf</c>
    /// 与分组条件）、<c>[Toggle]</c>、<c>[ToggleGroup]</c>、<c>[MinMaxSlider]</c> 的动态边界
    /// 全走这里。
    /// <para>
    /// <b>阶梯四级，次序即契约：</b>
    /// </para>
    /// <list type="number">
    /// <item><description><b>嵌套同层（序列化）</b>：最近的复合成员容器里的兄弟成员
    /// （容器沿父链上溯并**跳过分组节点**，见
    /// <see cref="SerializedMemberResolver.FindNestedScopeNode"/>）。</description></item>
    /// <item><description><b>根绝对名（序列化）</b>：属性所属序列化对象上的绝对路径。</description></item>
    /// <item><description><b>反射</b>：容器实例（嵌套 / 元素层）或根目标上的**普通字段与属性**，
    /// 再是**无参方法**。字段与属性在 C# 里不可能与同名方法共存（同一个成员命名空间），
    /// 故这个顺序不会漏掉什么，只是把「找到的是哪一种」定死——顺序定了，失败信息才可解释。</description></item>
    /// </list>
    /// <para>
    /// <b>命中即止，且「找到了但类型不符」也停。</b> 类型不符时不再往下找：继续找会报第二次警，
    /// 而两条消息互相矛盾（一句说「找到了但类型不对」、一句说「找不到」）。
    /// </para>
    /// <para>
    /// <b>反射那一级找的是「同一个实例」。</b> 嵌套层与元素层里，成员名指的是**同层**的成员
    /// ——在根上找就会拿到根上的同名成员，「条件看错了对象」，静默且极难归因。实例由一条
    /// 构建期编译的字段链每帧现读（见 <see cref="ReflectedAccessor.TryCreatePath"/>），
    /// 父字段被重新赋值后跟着走。
    /// </para>
    /// <para>
    /// <b>解析在构建期做一次，求值在绘制期每帧做。</b> 返回的读取器是编译好的委托——
    /// 绘制路径上既不反射、也不装箱（<see cref="SerializedProperty"/> 那条走的是活句柄，
    /// 同一条纪律）。<c>[ToggleGroup]</c> 是唯一的例外：它的分组节点在处理器阶段还不存在，
    /// 故退到**首次绘制**解析一次、结果缓存在 <see cref="PropertyState"/> 上。
    /// </para>
    /// <para>
    /// <b>只管解析，不管告警。</b> 调用方的告警机制本就不同且各自正确：条件族与
    /// <c>[Toggle]</c> 在构建期直接 <c>Debug.LogWarning</c>，<c>[ToggleGroup]</c> 在绘制期用
    /// <see cref="DrawerWarnings.Once"/>（每属性只报一次）。统一告警会把其中一方改坏，
    /// 故只统一解析。
    /// </para>
    /// <para>
    /// <b>失败一律不抛，只给一句人能读的中文原因。</b> 一个拼错的成员名不该让整个 Inspector
    /// 白屏——那是使用方看到本插件的第一眼。放弃的后果由调用方决定（条件族是「条件不生效、
    /// 字段照常显示」，值绘制器是「退回普通绘制」），配合告警足以定位。
    /// </para>
    /// </summary>
    internal static class MemberReferenceResolver
    {
        #region Public API

        /// <summary>
        /// 解析一个 bool 成员引用。
        /// </summary>
        /// <param name="property">目标属性，用来定位序列化对象与容器。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="scope">查找范围。</param>
        /// <param name="read">每帧现读的读取器；失败时为 <c>null</c>。</param>
        /// <param name="serialized">
        /// 解析到的是**序列化**成员时给出它的活句柄（可写、可看混合态），否则为 <c>null</c>。
        /// 反射成员在本包一律只读，故那一格恒为 <c>null</c>。
        /// </param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// 实例缺失（嵌套实例为 <c>null</c>、元素为空）时读取器给 <c>false</c>——
        /// 与既有条件族「实例为空时条件算假」逐字一致。
        /// </remarks>
        public static bool TryResolveBoolean(
            InspectorProperty property,
            string memberName,
            MemberScope scope,
            out Func<bool> read,
            out SerializedProperty serialized,
            out string reason)
        {
            return TryResolveCore(
                property, memberName, scope, MemberKind.Boolean, false,
                member => () => member.boolValue,
                out read, out serialized, out reason);
        }

        /// <summary>
        /// 解析一个 float 成员引用（如 <c>[MinMaxSlider]</c> 的动态边界）。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="read">每帧现读的读取器；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// 实例缺失时读取器给 <see cref="float.NaN"/>——消费方（滑块边界）本来就把非有限值
        /// 判为「边界不可用、退回普通绘制」，故这里不需要第二套「取不到」的表达。
        /// </remarks>
        public static bool TryResolveFloat(
            InspectorProperty property,
            string memberName,
            out Func<float> read,
            out string reason)
        {
            return TryResolveCore(
                property, memberName, MemberScope.Object, MemberKind.Float, float.NaN,
                member => () => member.floatValue,
                out read, out _, out reason);
        }

        /// <summary>
        /// 解析一个 <c>Vector2</c> 成员引用（如 <c>[MinMaxSlider]</c> 同时给出上下界的那个成员）。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="read">每帧现读的读取器；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        /// <remarks>实例缺失时给 <c>(NaN, NaN)</c>——理由同 <see cref="TryResolveFloat"/>。</remarks>
        public static bool TryResolveVector2(
            InspectorProperty property,
            string memberName,
            out Func<Vector2> read,
            out string reason)
        {
            return TryResolveCore(
                property, memberName, MemberScope.Object, MemberKind.Vector2, new Vector2(float.NaN, float.NaN),
                member => () => member.vector2Value,
                out read, out _, out reason);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 阶梯本体：三种种类共用这一段，差别只在类型判定与「怎么把序列化句柄变成读取器」。
        /// </summary>
        /// <typeparam name="T">成员的值类型。</typeparam>
        /// <param name="property">目标属性。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="scope">查找范围。</param>
        /// <param name="kind">类型约束。</param>
        /// <param name="whenMissing">实例缺失（嵌套实例为空、元素为空）时读取器给什么值。</param>
        /// <param name="bindSerialized">把序列化句柄包成读取器（每条腿各一个活句柄字段）。</param>
        /// <param name="read">每帧现读的读取器。</param>
        /// <param name="serialized">解析到序列化成员时的活句柄；反射成员恒为 <c>null</c>。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        private static bool TryResolveCore<T>(
            InspectorProperty property,
            string memberName,
            MemberScope scope,
            MemberKind kind,
            T whenMissing,
            Func<SerializedProperty, Func<T>> bindSerialized,
            out Func<T> read,
            out SerializedProperty serialized,
            out string reason)
        {
            read = null;
            serialized = null;
            reason = null;

            // 第 0、1 级：序列化那两条。
            var member = SerializedMemberResolver.TryFind(property, memberName, scope, out var findReason);

            if (member != null)
            {
                if (!SerializedMemberResolver.IsKind(member, kind))
                {
                    // 找到了却类型不符：**不再往下找**（理由见类注释）。
                    reason = $"该成员不是 {MemberKindNames.Describe(kind)}（实为 {member.propertyType}）";
                    return false;
                }

                serialized = member;
                read = bindSerialized(member);
                return true;
            }

            if (scope == MemberScope.Relative)
            {
                // 第 2、3 级在值对象内部没有落点：那里只有一个 SerializedProperty，没有实例句柄。
                reason = findReason;
                return false;
            }

            var targets = property?.Owner?.Targets;
            var container = SerializedMemberResolver.FindNestedScopeNode(property);

            // 第 2 级：嵌套 / 元素容器上的反射成员与方法。找不到就**停在这里**——
            // 回落到根上找会「看错对象」（同层没有、根上恰好同名时尤其难查）。
            if (container != null)
            {
                return ReflectedMemberResolver.TryResolveNested(
                    container.Type, container.Path, targets, memberName, whenMissing, out read, out reason);
            }

            // 第 3 级：根目标上的反射成员与方法。
            return ReflectedMemberResolver.TryResolve(
                targets, memberName, whenMissing, out read, out reason);
        }

        #endregion
    }
}
