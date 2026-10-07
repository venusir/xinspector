using System;
using System.Collections.Generic;
using System.Reflection;

namespace XInspector.Editor
{
    /// <summary>
    /// 「这个节点画的是**哪个对象**」——顶层就是树的目标对象本身，嵌套层、集合元素层与
    /// **多态容器**是沿序列化路径走到的那个实例。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>存在的理由：默认取根对象会静默调错对象。</b> 嵌套字段上的 <c>[OnValueChanged]</c>、
    /// <c>[CustomContextMenu]</c>、<c>[InlineButton]</c> 都按名找方法——在**根类型**上按名找，
    /// 根上恰好有同名方法时就会被调走，按钮点下去有反应，只是反应发生在另一个对象上。
    /// 比「找不到」难查得多。
    /// </para>
    /// <para>
    /// <b>产出的是「每帧现读」的访问器，不是实例。</b> 用户把父字段重新赋值
    /// （<c>nested = new …</c>、Undo、预制体 revert）之后必须跟着走；绑死的实例会**静默地陈旧**。
    /// 这也是延迟回调（右键菜单还在屏幕上时）必须存访问器而不是实例的原因。
    /// </para>
    /// <para>
    /// 容器一律经 <see cref="SerializedMemberResolver.FindNestedScopeNode"/> 取——它会沿父链
    /// **跳过分组节点**。直接看 <c>Parent</c> 会踩错：嵌套层装配分组之后，方法节点的父节点
    /// 可能是个分组节点（<c>Type</c> 为 <c>null</c>）。
    /// </para>
    /// </remarks>
    internal static class NestedInstanceScope
    {
        #region Public API

        /// <summary>
        /// 为一组根目标编译「走到 <paramref name="container"/> 那个实例」的逐目标访问器。
        /// </summary>
        /// <param name="targets">根目标对象数组。</param>
        /// <param name="container">
        /// 容器节点（嵌套层的复合成员、集合元素节点、或**多态容器**——三种都是
        /// <see cref="InspectorPropertyKind.Member"/>）。路径里要**穿过**的多态引用段
        /// 由 <see cref="PolymorphicTypesFor"/> 从祖先链取具体类型。
        /// </param>
        /// <returns>
        /// 逐目标的访问器，与 <paramref name="targets"/> 同长；**顶层返回 <c>null</c>**
        /// （调用方据此走「目标即实例」那条老路）。
        /// </returns>
        /// <remarks>
        /// 某个目标上编译不出来就是那一格 <c>null</c>——读取时算「取不到实例」，
        /// 与「某个目标上没有这个成员」同款处置（跳过它，而不是拿别人的值冒充）。
        /// </remarks>
        public static ReflectedAccessor[] Compile(object[] targets, InspectorProperty container)
        {
            if (targets == null || container == null || string.IsNullOrEmpty(container.Path))
            {
                return null;
            }

            var types = PolymorphicTypesFor(container);
            var scopes = new ReflectedAccessor[targets.Length];

            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    continue;
                }

                if (ReflectedAccessor.TryCreatePath(target.GetType(), container.Path, types, out var scope, out _))
                {
                    scopes[i] = scope;
                }
            }

            return scopes;
        }

        /// <summary>
        /// 取容器路径上各**非末段**多态引用段的具体类型（按路径出现顺序，浅 → 深）。
        /// </summary>
        /// <param name="container">容器节点。</param>
        /// <returns>具体类型数组；路径上没有要穿过的多态段时返回 <c>null</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>不含容器自身</b>：容器节点必然是路径的末段，而末段多态引用不需要换基
        /// （读到实例即可）——于是「从 <c>Parent</c> 往上扫」恰好就是要穿过的那些段。
        /// </para>
        /// <para>
        /// 只认 <see cref="InspectorPropertyKind.Member"/> 且成员是 <c>[SerializeReference]</c>
        /// 字段的节点（多态容器的判据，与展开、守卫同源）；分组节点与元素节点
        /// （<c>Member == null</c>）天然被挡掉。
        /// </para>
        /// </remarks>
        internal static Type[] PolymorphicTypesFor(InspectorProperty container)
        {
            List<Type> types = null;

            for (var current = container?.Parent; current != null; current = current.Parent)
            {
                if (current.Kind != InspectorPropertyKind.Member)
                {
                    continue;
                }

                if (!NestedMemberExpansion.IsPolymorphicReference(current.Member as FieldInfo))
                {
                    continue;
                }

                (types ??= new List<Type>()).Add(current.Type);
            }

            if (types == null)
            {
                return null;
            }

            types.Reverse(); // 上溯是深 → 浅；路径出现顺序是浅 → 深。
            return types.ToArray();
        }

        /// <summary>
        /// 取第 <paramref name="index"/> 个目标上的实例。
        /// </summary>
        /// <param name="scopes">逐目标的访问器；<c>null</c> 表示顶层。</param>
        /// <param name="targets">根目标对象数组。</param>
        /// <param name="index">目标下标。</param>
        /// <returns>实例；取不到（顶层目标为空、或父字段为空）时返回 <c>null</c>。</returns>
        public static object Read(ReflectedAccessor[] scopes, object[] targets, int index)
        {
            var target = targets != null && index < targets.Length ? targets[index] : null;

            if (scopes == null)
            {
                return target;
            }

            var scope = index < scopes.Length ? scopes[index] : null;
            return scope?.Read(target);
        }

        /// <summary>
        /// 取「这条路径末端那个**实例**」的类型——按名找成员/方法的那三处（反射成员重解析、
        /// 按钮、按名回调）要的就是它。
        /// </summary>
        /// <param name="scope">该目标的路径访问器。</param>
        /// <param name="target">根目标对象。</param>
        /// <returns>实例类型；取不到时回落 <paramref name="scope"/> 的声明类型 / <c>null</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>与 <see cref="ReflectedAccessor.ValueType"/> 的差别只在一处</b>：末段是多态引用时
        /// <c>ValueType</c> 是**声明类型**（常常是接口/抽象类），而这里现读一次实例给**具体类型**
        /// （空槽位回落声明类型，与「取不到实例」同款）。其余情形逐字返回 <c>ValueType</c>，
        /// 零行为变化。
        /// </para>
        /// <para>
        /// 会真的读一次用户实例，**只发生在构建 / 重建期**（与元素层、多态层的重建同一条
        /// 「反射仅限构建期」豁免），不进每帧路径。
        /// </para>
        /// </remarks>
        public static Type InstanceTypeOf(ReflectedAccessor scope, object target)
        {
            if (scope == null)
            {
                return null;
            }

            if (scope.Member is FieldInfo field && NestedMemberExpansion.IsPolymorphicReference(field))
            {
                var instance = scope.Read(target);
                if (instance != null)
                {
                    return instance.GetType();
                }
            }

            return scope.ValueType;
        }

        /// <summary>
        /// 取节点所属的**复合成员容器**节点——沿父链上溯、跳过分组节点。
        /// </summary>
        /// <param name="node">节点。</param>
        /// <returns>容器节点；顶层节点返回 <c>null</c>（实例就是目标对象本身）。</returns>
        public static InspectorProperty ContainerOf(InspectorProperty node)
        {
            return SerializedMemberResolver.FindNestedScopeNode(node);
        }

        /// <summary>
        /// 容器是**值类型**时，方法调用一律被拒的原因；不是值类型时返回 <c>null</c>。
        /// </summary>
        /// <param name="node">发出调用的节点。</param>
        /// <returns>拒绝原因（中文，可直接画在脸上或拼进告警）；可以调用时返回 <c>null</c>。</returns>
        /// <remarks>
        /// <para>
        /// 值类型在链上会**装箱**，读到的是副本，方法调用改的也是副本——改动**静默丢弃**。
        /// 与其做一个「点了没反应」的按钮，不如明说。
        /// </para>
        /// <para>
        /// <b>判据与文案只此一份</b>：按钮（<c>ButtonProcessors</c>）与按名回调
        /// （<c>NamedMethodResolver</c>）两处此前各写了一遍，逐字相同——元素层让
        /// <c>List&lt;结构体&gt;</c> 变得常见之后，这种「各写一遍」迟早会漂。
        /// 调用方只负责把原因放到该放的地方（HelpBox / Tooltip / 告警前缀）。
        /// </para>
        /// </remarks>
        public static string ValueTypeContainerReason(InspectorProperty node)
        {
            var type = ContainerOf(node)?.Type;

            return type != null && type.IsValueType
                ? $"「{type.Name}」是值类型（struct）：方法调用改的是装箱副本，改动会丢，因此不在它上面调用方法。"
                : null;
        }

        #endregion
    }
}
