using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// 按钮的每属性状态：调谁、带什么参数、能不能调。
    /// <para>
    /// 绘制器是共享单例，不得有可变字段，故这些都存在节点自己的
    /// <see cref="PropertyState"/> 里——包括参数值缓冲：它是**用户填的东西**，
    /// 归属性所有，不归绘制器所有。
    /// </para>
    /// </summary>
    internal sealed class ButtonState
    {
        #region Public Fields

        /// <summary>
        /// 逐目标解析出的可调用方法，与树的目标列表一一对应；某个目标解析不到时为 <c>null</c>。
        /// </summary>
        public MethodInfo[] Methods;

        /// <summary>方法参数。构建期解析一次。</summary>
        public ParameterInfo[] Parameters = Array.Empty<ParameterInfo>();

        /// <summary>参数值缓冲，与 <see cref="Parameters"/> 一一对应。</summary>
        public object[] Arguments = Array.Empty<object>();

        /// <summary>不可调用时的原因（会画在按钮下方）；可调用时为 <c>null</c>。</summary>
        public string Reason;

        /// <summary>参数区是否展开。默认收起，与 Odin 带参按钮的默认形态一致。</summary>
        public bool Expanded;

        /// <summary>
        /// 逐目标的嵌套实例来源；顶层为 <c>null</c>。
        /// </summary>
        /// <remarks>调用时**现读**实例——绑死的实例在父字段被重新赋值之后会静默陈旧。</remarks>
        public ReflectedAccessor[] Scopes;

        #endregion
    }

    /// <summary>
    /// <see cref="ButtonAttribute"/> 的处理器：构建期把「按名解析方法」这件事做掉，
    /// 绘制期就只剩读状态与调用。
    /// <para>
    /// 反射只在构建期发生，是本仓的硬约束；按钮又是每帧重绘的东西，绘制期碰反射等于每帧付一次。
    /// </para>
    /// </summary>
    internal sealed class ButtonProcessor : AttributeProcessor<ButtonAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        /// <remarks>
        /// 用 <c>property.Member</c>（收集期记下的那份 <see cref="MethodInfo"/>）作**线索**，
        /// 而不是直接拿它去调用：它可能是一个抽象声明或基类上的虚方法，
        /// 真正可调用的是目标对象类型上的那一份（覆写后的）。
        /// </remarks>
        protected override void ProcessSelf(
            InspectorProperty property,
            ButtonAttribute attribute,
            IList<Attribute> attributes)
        {
            var state = property.State.GetOrCreate<ButtonState>();

            if (!string.IsNullOrWhiteSpace(attribute.Name))
            {
                // 走标签通道而不是绘制器自己记住：这样 [GUIColor]、[Indent] 那些包裹型的
                // 绘制器看到的也是同一个文本，不会出现「按钮叫 X、旁边标注叫 Y」。
                property.State.LabelOverride = new GUIContent(attribute.Name);
            }

            Resolve(property, state);
        }

        #endregion

        #region Private Helpers

        /// <summary>解析方法、参数缓冲与不可调用的原因。</summary>
        /// <param name="property">方法节点。</param>
        /// <param name="state">该节点的状态。</param>
        private static void Resolve(InspectorProperty property, ButtonState state)
        {
            var targets = property.Owner?.Targets;

            if (targets == null || targets.Length == 0)
            {
                state.Reason = "取不到目标对象，无法调用方法。";
                return;
            }

            if (!(property.Member is MethodInfo discovery))
            {
                state.Reason = "方法节点没有记住要调用的方法。";
                return;
            }

            var parameters = discovery.GetParameters();
            state.Parameters = parameters;

            // 参数缓冲每次建树重建：用户上次填的值没有跨会话保留的理由，
            // 而树重建本来就意味着「换了个对象在看」。
            state.Arguments = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                state.Arguments[i] = ButtonParameters.DefaultFor(parameters[i].ParameterType);
            }

            state.Reason = FindBlockingReason(discovery, parameters);
            if (state.Reason != null)
            {
                return;
            }

            // 嵌套层（或元素层）：方法要在**同一个实例**的类型上按签名找，调用时也要在那个实例上——
            // 在根对象上找会拿到根上签名相同的那一个（比「找不到」难查得多）。
            // 值类型容器上的调用一律拒绝：判据与文案收在 NestedInstanceScope 一处。
            var blocked = NestedInstanceScope.ValueTypeContainerReason(property);

            if (blocked != null)
            {
                state.Reason = blocked;
                return;
            }

            var container = NestedInstanceScope.ContainerOf(property);
            state.Scopes = NestedInstanceScope.Compile(targets, container);

            state.Methods = new MethodInfo[targets.Length];
            var missing = 0;

            for (var i = 0; i < targets.Length; i++)
            {
                // 嵌套层按**实例的类型**找；某个目标算不出实例类型就算它找不到。
                // （末段是多态引用时 `ValueType` 只是声明类型——`InstanceTypeOf` 现读实例。）
                var type = state.Scopes == null
                    ? (TargetObjects.IsAlive(targets[i]) ? targets[i].GetType() : null)
                    : (i < state.Scopes.Length
                        ? NestedInstanceScope.InstanceTypeOf(state.Scopes[i], targets[i])
                        : null);

                state.Methods[i] = type == null ? null : MethodResolver.BySignature(type, discovery);

                if (state.Methods[i] == null)
                {
                    missing++;
                }
            }

            if (missing == targets.Length)
            {
                state.Reason = $"在目标对象上找不到方法 \"{discovery.Name}\"，无法调用。";
            }
            else if (missing > 0)
            {
                // 多选时选中了不完全相同的类型：能调的照调，调不了的跳过——但要留痕。
                Debug.LogWarning(
                    $"[XInspector] 按钮 \"{discovery.Name}\" 在 {missing} 个目标上找不到，这些目标将被跳过。");
            }
        }

        /// <summary>
        /// 判断方法整体上是不是**根本没法调**，返回原因；能调则返回 <c>null</c>。
        /// </summary>
        /// <param name="method">方法。</param>
        /// <param name="parameters">它的参数。</param>
        /// <returns>中文原因，或 <c>null</c>。</returns>
        /// <remarks>
        /// 只做与具体目标无关的判断：泛型、参数类型。目标相关的失败在解析阶段各自处理。
        /// </remarks>
        private static string FindBlockingReason(MethodInfo method, ParameterInfo[] parameters)
        {
            if (method.IsGenericMethodDefinition || method.ContainsGenericParameters)
            {
                return $"方法 \"{method.Name}\" 是泛型方法。泛型方法没有确定的类型实参，"
                       + "反射无法调用——请写一个具体的重载给它。";
            }

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                if (parameter.ParameterType.IsByRef)
                {
                    return $"参数 \"{parameter.Name}\" 是 ref/out 参数，本包暂不支持（值传不回来）。";
                }

                if (!ButtonParameters.IsSupported(parameter.ParameterType))
                {
                    return $"参数 \"{parameter.Name}\" 的类型 {parameter.ParameterType.Name} 不在支持集里。"
                           + "支持 bool／int／float／double／string／枚举／UnityEngine.Object 派生／"
                           + "Vector2／Vector3／Vector4／Color／Rect。";
                }
            }

            return null;
        }

        #endregion
    }
}
