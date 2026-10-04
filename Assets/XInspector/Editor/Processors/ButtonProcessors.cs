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

            state.Methods = new MethodInfo[targets.Length];
            var missing = 0;

            for (var i = 0; i < targets.Length; i++)
            {
                state.Methods[i] = targets[i] == null
                    ? null
                    : FindMethod(targets[i].GetType(), discovery, parameters);

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

        /// <summary>
        /// 在目标类型上找出与线索**同名同参**的方法。
        /// </summary>
        /// <param name="type">目标对象的运行时类型。</param>
        /// <param name="discovery">收集期记下的那份方法。</param>
        /// <param name="parameters">线索方法的参数。</param>
        /// <returns>可调用的方法；找不到时返回 <c>null</c>。</returns>
        /// <remarks>
        /// 逐层 <c>DeclaredOnly</c> 上溯：<see cref="Type.GetMethods(BindingFlags)"/> 不带
        /// <c>DeclaredOnly</c> 时拿不到基类的私有方法，而按钮方法多半就是私有的。
        /// </remarks>
        private static MethodInfo FindMethod(Type type, MethodInfo discovery, ParameterInfo[] parameters)
        {
            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            for (var current = type; current != null; current = current.BaseType)
            {
                var candidates = current.GetMethods(Flags);

                for (var i = 0; i < candidates.Length; i++)
                {
                    if (candidates[i].Name != discovery.Name)
                    {
                        continue;
                    }

                    if (SignatureMatches(candidates[i], parameters))
                    {
                        return candidates[i];
                    }
                }
            }

            return null;
        }

        /// <summary>候选方法的参数表是否与线索方法逐项同型。</summary>
        /// <param name="candidate">候选方法。</param>
        /// <param name="parameters">线索方法的参数。</param>
        /// <returns>同型返回 <c>true</c>。</returns>
        private static bool SignatureMatches(MethodInfo candidate, ParameterInfo[] parameters)
        {
            var candidateParameters = candidate.GetParameters();

            if (candidateParameters.Length != parameters.Length)
            {
                return false;
            }

            for (var i = 0; i < parameters.Length; i++)
            {
                if (candidateParameters[i].ParameterType != parameters[i].ParameterType)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
