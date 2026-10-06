using System;

namespace XInspector.Editor
{
    /// <summary>
    /// 「这个节点画的是**哪个对象**」——顶层就是树的目标对象本身，嵌套层是沿序列化路径走到的那个实例。
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
        /// 为一组根目标编译「走到 <paramref name="containerPath"/> 那个实例」的逐目标访问器。
        /// </summary>
        /// <param name="targets">根目标对象数组。</param>
        /// <param name="containerPath">嵌套实例相对根目标的序列化路径。</param>
        /// <returns>
        /// 逐目标的访问器，与 <paramref name="targets"/> 同长；**顶层返回 <c>null</c>**
        /// （调用方据此走「目标即实例」那条老路）。
        /// </returns>
        /// <remarks>
        /// 某个目标上编译不出来就是那一格 <c>null</c>——读取时算「取不到实例」，
        /// 与「某个目标上没有这个成员」同款处置（跳过它，而不是拿别人的值冒充）。
        /// </remarks>
        public static ReflectedAccessor[] Compile(object[] targets, string containerPath)
        {
            if (targets == null || string.IsNullOrEmpty(containerPath))
            {
                return null;
            }

            var scopes = new ReflectedAccessor[targets.Length];

            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    continue;
                }

                if (ReflectedAccessor.TryCreatePath(target.GetType(), containerPath, out var scope, out _))
                {
                    scopes[i] = scope;
                }
            }

            return scopes;
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
        /// 取节点所属的**复合成员容器**节点——沿父链上溯、跳过分组节点。
        /// </summary>
        /// <param name="node">节点。</param>
        /// <returns>容器节点；顶层节点返回 <c>null</c>（实例就是目标对象本身）。</returns>
        public static InspectorProperty ContainerOf(InspectorProperty node)
        {
            return SerializedMemberResolver.FindNestedScopeNode(node);
        }

        /// <summary>
        /// 节点所属的容器是不是**值类型**——是的话方法调用要拒绝。
        /// </summary>
        /// <param name="node">节点。</param>
        /// <returns>是值类型返回 <c>true</c>。</returns>
        /// <remarks>
        /// 值类型在链上会**装箱**，读到的是副本，方法调用改的也是副本——改动**静默丢弃**。
        /// 与其做一个「点了没反应」的按钮，不如明说。
        /// </remarks>
        public static bool IsValueTypeContainer(InspectorProperty node)
        {
            var type = ContainerOf(node)?.Type;
            return type != null && type.IsValueType;
        }

        #endregion
    }
}
