using System;
using System.Collections;
using System.Reflection;

namespace XInspector.Editor
{
    /// <summary>
    /// 成员引用阶梯的**反射那条腿**：Unity 不序列化的字段、属性，以及无参方法。
    /// <para>
    /// 与 <see cref="SerializedMemberResolver"/> 分工对称：那个管序列化成员，这个管其余。
    /// 入口是 <see cref="MemberReferenceResolver"/>——它先问序列化那一半，问不到才来这里。
    /// </para>
    /// <para>
    /// <b>解析在构建期做一次，求值在绘制期每帧做。</b> 这条分工与序列化那条完全相同，
    /// 也是「不碰 GUI 就能测」的前提。故这里返回的是一个被绑定的委托，
    /// 绘制期既不反射、也不装箱——见 <see cref="ReflectedAccessor.TryCreateReader{T}"/>。
    /// </para>
    /// <para>
    /// <b>只看第一个存活目标。</b> 多选下各目标的同名成员值未必一致，而这条腿服务的判据
    /// （条件、开关、边界）读的都是「这一个值」；序列化那条路也是这样
    /// （<c>SerializedProperty.boolValue</c> 在多选时读的同样是主目标）。
    /// 这里不扩大那条语义。
    /// </para>
    /// <para>
    /// <b>失败一律不抛，只给一句中文原因。</b> 一个拼错的成员名不该让整个 Inspector 白屏，
    /// 那是使用方看到本插件的第一眼。
    /// </para>
    /// </summary>
    internal static class ReflectedMemberResolver
    {
        #region Private Fields

        /// <summary>逐层上溯地找成员——私有成员各自声明在自己的类里。</summary>
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        #endregion

        #region Public API

        /// <summary>
        /// 在**根目标对象**上解析一个成员引用。
        /// </summary>
        /// <typeparam name="T">读取器的类型（通常是成员的值类型；列表那一格是 <see cref="IList"/>）。</typeparam>
        /// <param name="targets">目标对象列表。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="requirement">对成员**声明类型**的要求。</param>
        /// <param name="whenMissing">取不到目标对象、或嵌套实例为空时，读取器给什么值。</param>
        /// <param name="read">解析出的每帧求值器；失败时为 <c>null</c>。</param>
        /// <param name="declaredType">命中成员的声明类型；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        public static bool TryResolve<T>(
            object[] targets,
            string memberName,
            MemberTypeRequirement requirement,
            T whenMissing,
            out Func<T> read,
            out Type declaredType,
            out string reason)
        {
            read = null;
            declaredType = null;
            reason = null;

            var target = FirstAlive(targets);
            if (target == null)
            {
                reason = "取不到目标对象";
                return false;
            }

            return TryResolveOn(
                target.GetType(), () => target, memberName, "目标对象", requirement, whenMissing,
                out read, out declaredType, out reason);
        }

        /// <summary>
        /// 在**嵌套实例（或集合元素）**上解析一个成员引用。
        /// </summary>
        /// <typeparam name="T">读取器的类型（通常是成员的值类型；列表那一格是 <see cref="IList"/>）。</typeparam>
        /// <param name="nestedType">嵌套实例的声明类型。</param>
        /// <param name="containerPath">嵌套实例相对根目标的序列化路径（如 <c>stats</c>）。</param>
        /// <param name="targets">根目标对象列表。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="requirement">对成员**声明类型**的要求。</param>
        /// <param name="whenMissing">取不到目标对象、或嵌套实例为空时，读取器给什么值。</param>
        /// <param name="read">解析出的每帧求值器；失败时为 <c>null</c>。</param>
        /// <param name="declaredType">命中成员的声明类型；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// 与根层那条的差别只有一处：**在哪一个对象上找成员**。根层找目标对象本身，
        /// 这里找「沿 <paramref name="containerPath"/> 走到的那个嵌套实例」——
        /// 不这么做就会拿到根上的同名成员，「看错了对象」，静默且极难归因。
        /// </para>
        /// <para>
        /// 实例**每帧现读**（编译出的字段链），不是绑死的：用户把父字段重新赋值之后
        /// （<c>stats = new …</c>、Undo、预制体 revert）读的值要跟着走。
        /// <b>实例为空时读取器给 <paramref name="whenMissing"/></b>——它在那时确实没有值可言。
        /// </para>
        /// </remarks>
        public static bool TryResolveNested<T>(
            Type nestedType,
            string containerPath,
            object[] targets,
            string memberName,
            MemberTypeRequirement requirement,
            T whenMissing,
            out Func<T> read,
            out Type declaredType,
            out string reason)
        {
            read = null;
            declaredType = null;
            reason = null;

            var target = FirstAlive(targets);
            if (target == null)
            {
                reason = "取不到目标对象";
                return false;
            }

            if (nestedType == null)
            {
                reason = "取不到嵌套实例的类型";
                return false;
            }

            if (!ReflectedAccessor.TryCreatePath(
                    target.GetType(), containerPath, out var scope, out reason))
            {
                return false;
            }

            return TryResolveOn(
                nestedType,
                () => scope.Read(target),
                memberName,
                $"嵌套实例（{nestedType.Name}）",
                requirement,
                whenMissing,
                out read,
                out declaredType,
                out reason);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 在指定类型上按名找成员并绑定——根层与容器层共用这一段。
        /// </summary>
        /// <typeparam name="T">读取器的类型。</typeparam>
        /// <param name="type">在哪一个类型上找。</param>
        /// <param name="instance">取当前实例（每帧现读）；根层是恒等，容器层是沿路径下钻。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="scopeName">失败信息里的范围名。</param>
        /// <param name="requirement">对成员声明类型的要求。</param>
        /// <param name="whenMissing">实例为空时读取器给什么值。</param>
        /// <param name="read">绑定出的求值器。</param>
        /// <param name="declaredType">命中成员的声明类型；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryResolveOn<T>(
            Type type,
            Func<object> instance,
            string memberName,
            string scopeName,
            MemberTypeRequirement requirement,
            T whenMissing,
            out Func<T> read,
            out Type declaredType,
            out string reason)
        {
            read = null;
            declaredType = null;

            for (var current = type; current != null; current = current.BaseType)
            {
                var candidates = current.GetMember(memberName, Flags);
                if (candidates.Length > 0)
                {
                    return TryBind(
                        candidates, instance, memberName, requirement, whenMissing,
                        out read, out declaredType, out reason);
                }
            }

            reason = $"{scopeName}上找不到名为「{memberName}」的字段、属性或无参方法";
            return false;
        }

        /// <summary>取第一个还活着的目标。</summary>
        /// <param name="targets">目标对象列表。</param>
        /// <returns>目标；一个都没有时返回 <c>null</c>。</returns>
        private static object FirstAlive(object[] targets)
        {
            if (targets == null)
            {
                return null;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                if (TargetObjects.IsAlive(targets[i]))
                {
                    return targets[i];
                }
            }

            return null;
        }

        /// <summary>在一层里挑出可用的那一个成员并绑定。</summary>
        /// <typeparam name="T">读取器的类型。</typeparam>
        /// <param name="candidates">同名成员。</param>
        /// <param name="instance">取当前实例。</param>
        /// <param name="memberName">成员名，用于告警文本。</param>
        /// <param name="requirement">对成员声明类型的要求。</param>
        /// <param name="whenMissing">实例为空时读取器给什么值。</param>
        /// <param name="read">绑定出的求值器。</param>
        /// <param name="declaredType">命中成员的声明类型；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>绑定成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <b>字段/属性优先于方法。</b> 字段与属性在 C# 里不可能与同名方法共存（同一个成员
        /// 命名空间），故这个顺序不会漏掉什么，只是把「找到的是哪一种」定死——
        /// 顺序定了，失败信息才可解释。
        /// </remarks>
        private static bool TryBind<T>(
            MemberInfo[] candidates,
            Func<object> instance,
            string memberName,
            MemberTypeRequirement requirement,
            T whenMissing,
            out Func<T> read,
            out Type declaredType,
            out string reason)
        {
            read = null;
            declaredType = null;
            reason = null;

            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is FieldInfo field)
                {
                    return TryBindField(
                        field, instance, memberName, requirement, whenMissing,
                        out read, out declaredType, out reason);
                }

                if (candidates[i] is PropertyInfo property)
                {
                    return TryBindProperty(
                        property, instance, memberName, requirement, whenMissing,
                        out read, out declaredType, out reason);
                }
            }

            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is MethodInfo method)
                {
                    return TryBindMethod(
                        method, instance, memberName, requirement, whenMissing,
                        out read, out declaredType, out reason);
                }
            }

            reason = $"「{memberName}」既不是字段、属性，也不是方法";
            return false;
        }

        /// <summary>绑定一个字段。</summary>
        /// <typeparam name="T">要求的成员类型。</typeparam>
        /// <param name="field">字段。</param>
        /// <param name="instance">取当前实例。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="whenMissing">实例为空时读取器给什么值。</param>
        /// <param name="read">绑定出的求值器。</param>
        /// <param name="requirement">对字段声明类型的要求。</param>
        /// <param name="declaredType">命中字段的声明类型；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryBindField<T>(
            FieldInfo field,
            Func<object> instance,
            string memberName,
            MemberTypeRequirement requirement,
            T whenMissing,
            out Func<T> read,
            out Type declaredType,
            out string reason)
        {
            read = null;
            declaredType = null;

            if (!requirement.Accepts(field.FieldType))
            {
                reason =
                    $"找到的「{memberName}」是 {ReflectedAccessor.DescribeType(field.FieldType)} 字段，" +
                    $"必须是 {requirement.Description}";
                return false;
            }

            if (!ReflectedAccessor.TryCreateReader<T>(field, out var reader, out reason))
            {
                return false;
            }

            declaredType = field.FieldType;
            read = () =>
            {
                var target = instance();
                return target != null ? reader(target) : whenMissing;
            };

            return true;
        }

        /// <summary>绑定一个属性。</summary>
        /// <typeparam name="T">读取器的类型。</typeparam>
        /// <param name="property">属性。</param>
        /// <param name="instance">取当前实例。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="requirement">对属性声明类型的要求。</param>
        /// <param name="whenMissing">实例为空时读取器给什么值。</param>
        /// <param name="read">绑定出的求值器。</param>
        /// <param name="declaredType">命中属性的声明类型；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryBindProperty<T>(
            PropertyInfo property,
            Func<object> instance,
            string memberName,
            MemberTypeRequirement requirement,
            T whenMissing,
            out Func<T> read,
            out Type declaredType,
            out string reason)
        {
            read = null;
            declaredType = null;

            if (property.GetIndexParameters().Length > 0)
            {
                reason = $"找到的「{memberName}」是索引器（带参数），读不到单一的值";
                return false;
            }

            if (property.GetGetMethod(true) == null)
            {
                reason = $"找到的「{memberName}」是只写属性，读不到值";
                return false;
            }

            if (!requirement.Accepts(property.PropertyType))
            {
                reason =
                    $"找到的「{memberName}」属性是 {ReflectedAccessor.DescribeType(property.PropertyType)}，" +
                    $"必须是 {requirement.Description}";
                return false;
            }

            if (!ReflectedAccessor.TryCreateReader<T>(property, out var reader, out reason))
            {
                return false;
            }

            declaredType = property.PropertyType;
            read = () =>
            {
                var target = instance();
                return target != null ? reader(target) : whenMissing;
            };

            return true;
        }

        /// <summary>
        /// 绑定一个无参、非泛型、返回值满足要求的方法。
        /// </summary>
        /// <typeparam name="T">读取器的类型。</typeparam>
        /// <param name="method">方法。</param>
        /// <param name="instance">取当前实例。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="requirement">对方法返回类型的要求。</param>
        /// <param name="whenMissing">实例为空时读取器给什么值。</param>
        /// <param name="read">绑定出的求值器。</param>
        /// <param name="declaredType">命中方法的返回类型；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <b>编译成委托而不是每帧 <c>Invoke</c>。</b> 求值每帧一次，
        /// 而 <c>MethodInfo.Invoke</c> 是反射调用——「反射仅限构建期」这条规则对它同样成立。
        /// 实例方法走**开实例**调用（每次吃当时的实例），理由见
        /// <see cref="ReflectedAccessor.TryCreateInvoker{T}"/>。
        /// </remarks>
        private static bool TryBindMethod<T>(
            MethodInfo method,
            Func<object> instance,
            string memberName,
            MemberTypeRequirement requirement,
            T whenMissing,
            out Func<T> read,
            out Type declaredType,
            out string reason)
        {
            read = null;
            declaredType = null;

            if (method.GetParameters().Length > 0)
            {
                reason = $"找到的「{memberName}」方法带参数，必须无参";
                return false;
            }

            if (method.ContainsGenericParameters)
            {
                reason = $"找到的「{memberName}」是泛型方法，不能当成员引用";
                return false;
            }

            if (!requirement.Accepts(method.ReturnType))
            {
                reason =
                    $"找到的「{memberName}」方法返回 {ReflectedAccessor.DescribeType(method.ReturnType)}，" +
                    $"必须返回 {requirement.Description}";
                return false;
            }

            if (!ReflectedAccessor.TryCreateInvoker<T>(method, out var invoker, out reason))
            {
                return false;
            }

            declaredType = method.ReturnType;

            if (method.IsStatic)
            {
                read = () => invoker(null);
                return true;
            }

            read = () =>
            {
                var target = instance();
                return target != null ? invoker(target) : whenMissing;
            };

            return true;
        }

        #endregion
    }
}
