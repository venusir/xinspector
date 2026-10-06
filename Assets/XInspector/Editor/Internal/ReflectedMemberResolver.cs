using System;
using System.Reflection;

namespace XInspector.Editor
{
    /// <summary>
    /// 把「另一个成员的当前值」变成条件时，**反射那一半**的解析。
    /// <para>
    /// 与 <see cref="SerializedMemberResolver"/> 分工对称：那个管序列化成员，
    /// 这个管 Unity 不序列化的字段、属性，以及无参返回 <c>bool</c> 的方法。
    /// 条件族先问序列化那一半，问不到再来这里。
    /// </para>
    /// <para>
    /// <b>解析在构建期做一次，求值在绘制期每帧做。</b> 这条分工与序列化条件完全相同，
    /// 也是「不碰 GUI 就能测」的前提。故这里返回的是一个被绑定的委托，
    /// 绘制期既不反射、也不装箱——见 <see cref="ReflectedAccessor.TryCreateBooleanReader"/>。
    /// </para>
    /// <para>
    /// <b>失败一律不抛，只给一句中文原因。</b> 一个拼错的条件名不该让整个 Inspector 白屏，
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
        /// 解析一个基于反射成员的条件。
        /// </summary>
        /// <param name="targets">目标对象列表。</param>
        /// <param name="memberName">条件成员名。</param>
        /// <param name="condition">解析出的每帧求值器；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>只看第一个目标。</b> 条件决定的是「这条属性画不画 / 能不能改」，而多选下各目标的
        /// 条件值未必一致——序列化那条路也是这样（<c>SerializedProperty.boolValue</c>
        /// 在多选时读的同样是主目标）。这里不扩大那条语义。
        /// </para>
        /// <para>
        /// <b>名字先按字段/属性找，再按方法找</b>，且逐层上溯时任取最先遇到的那一层。
        /// 字段与属性在 C# 里不可能与同名方法共存（同一个成员命名空间），
        /// 故这个顺序不会漏掉什么，只是把「找到的是哪一种」定死——顺序定了，失败信息才可解释。
        /// </para>
        /// </remarks>
        public static bool TryResolveBooleanCondition(
            object[] targets,
            string memberName,
            out Func<bool> condition,
            out string reason)
        {
            condition = null;
            reason = null;

            var target = FirstAlive(targets);
            if (target == null)
            {
                reason = "取不到目标对象";
                return false;
            }

            return TryResolveOn(target.GetType(), () => target, memberName, "目标对象", out condition, out reason);
        }

        /// <summary>
        /// 解析一个基于**嵌套实例**上反射成员的条件。
        /// </summary>
        /// <param name="nestedType">嵌套实例的声明类型。</param>
        /// <param name="containerPath">嵌套实例相对根目标的序列化路径（如 <c>stats</c>）。</param>
        /// <param name="targets">根目标对象列表。</param>
        /// <param name="memberName">条件成员名。</param>
        /// <param name="condition">解析出的每帧求值器；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>解析成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// 与根层那条的差别只有一处：**在哪一个对象上找成员**。根层找目标对象本身，
        /// 这里找「沿 <paramref name="containerPath"/> 走到的那个嵌套实例」——
        /// 不这么做就会拿到根上的同名成员，「条件看错了对象」，静默且极难归因。
        /// </para>
        /// <para>
        /// 实例**每帧现读**（编译出的字段链），不是绑死的：用户把父字段重新赋值之后
        /// （<c>stats = new …</c>、Undo、预制体 revert）条件要跟着走。
        /// <b>实例为空时条件算假</b>——它在那时确实没有值可言。
        /// </para>
        /// </remarks>
        public static bool TryResolveNestedBooleanCondition(
            Type nestedType,
            string containerPath,
            object[] targets,
            string memberName,
            out Func<bool> condition,
            out string reason)
        {
            condition = null;
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
                out condition,
                out reason);
        }

        /// <summary>
        /// 在指定类型上按名找成员并绑定——根层与嵌套层共用这一段。
        /// </summary>
        /// <param name="type">在哪一个类型上找。</param>
        /// <param name="instance">取当前实例（每帧现读）；根层是恒等，嵌套层是沿路径下钻。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="scopeName">失败信息里的范围名。</param>
        /// <param name="condition">绑定出的求值器。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryResolveOn(
            Type type,
            Func<object> instance,
            string memberName,
            string scopeName,
            out Func<bool> condition,
            out string reason)
        {
            condition = null;

            for (var current = type; current != null; current = current.BaseType)
            {
                var candidates = current.GetMember(memberName, Flags);
                if (candidates.Length > 0)
                {
                    return TryBind(candidates, instance, memberName, out condition, out reason);
                }
            }

            reason = $"{scopeName}上找不到名为「{memberName}」的字段、属性或无参方法";
            return false;
        }

        #endregion

        #region Private Helpers

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
        /// <param name="candidates">同名成员。</param>
        /// <param name="instance">取当前实例。</param>
        /// <param name="memberName">成员名，用于告警文本。</param>
        /// <param name="condition">绑定出的求值器。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>绑定成功返回 <c>true</c>。</returns>
        private static bool TryBind(
            MemberInfo[] candidates,
            Func<object> instance,
            string memberName,
            out Func<bool> condition,
            out string reason)
        {
            condition = null;
            reason = null;

            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is FieldInfo field)
                {
                    return TryBindField(field, instance, memberName, out condition, out reason);
                }

                if (candidates[i] is PropertyInfo property)
                {
                    return TryBindProperty(property, instance, memberName, out condition, out reason);
                }
            }

            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is MethodInfo method)
                {
                    return TryBindMethod(method, instance, memberName, out condition, out reason);
                }
            }

            reason = $"「{memberName}」既不是字段、属性，也不是方法";
            return false;
        }

        /// <summary>绑定一个 bool 字段。</summary>
        /// <param name="field">字段。</param>
        /// <param name="instance">取当前实例。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="condition">绑定出的求值器。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryBindField(
            FieldInfo field,
            Func<object> instance,
            string memberName,
            out Func<bool> condition,
            out string reason)
        {
            condition = null;

            if (field.FieldType != typeof(bool))
            {
                reason = $"找到的「{memberName}」是 {field.FieldType.Name} 字段，条件必须是 bool";
                return false;
            }

            if (!ReflectedAccessor.TryCreateBooleanReader(field, out var reader, out reason))
            {
                return false;
            }

            condition = () =>
            {
                var target = instance();
                return target != null && reader(target);
            };

            return true;
        }

        /// <summary>绑定一个 bool 属性。</summary>
        /// <param name="property">属性。</param>
        /// <param name="instance">取当前实例。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="condition">绑定出的求值器。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryBindProperty(
            PropertyInfo property,
            Func<object> instance,
            string memberName,
            out Func<bool> condition,
            out string reason)
        {
            condition = null;

            if (property.GetIndexParameters().Length > 0)
            {
                reason = $"找到的「{memberName}」是索引器，条件必须是普通字段或属性";
                return false;
            }

            if (property.GetGetMethod(true) == null)
            {
                reason = $"找到的「{memberName}」是只写属性，读不到值";
                return false;
            }

            if (property.PropertyType != typeof(bool))
            {
                reason = $"找到的「{memberName}」属性是 {property.PropertyType.Name}，条件必须是 bool";
                return false;
            }

            if (!ReflectedAccessor.TryCreateBooleanReader(property, out var reader, out reason))
            {
                return false;
            }

            condition = () =>
            {
                var target = instance();
                return target != null && reader(target);
            };

            return true;
        }

        /// <summary>
        /// 绑定一个无参、非泛型、返回 bool 的方法。
        /// </summary>
        /// <param name="method">方法。</param>
        /// <param name="instance">取当前实例。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="condition">绑定出的求值器。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <b>编译成委托而不是每帧 <c>Invoke</c>。</b> 条件每帧求值一次，
        /// 而 <c>MethodInfo.Invoke</c> 是反射调用——「反射仅限构建期」这条规则对它同样成立。
        /// 实例方法走**开实例**调用（每次吃当时的实例），理由见
        /// <see cref="ReflectedAccessor.TryCreateBooleanInvoker"/>。
        /// </remarks>
        private static bool TryBindMethod(
            MethodInfo method,
            Func<object> instance,
            string memberName,
            out Func<bool> condition,
            out string reason)
        {
            condition = null;
            reason = null;

            if (method.GetParameters().Length > 0)
            {
                reason = $"找到的「{memberName}」方法带参数，条件方法必须无参";
                return false;
            }

            if (method.ContainsGenericParameters)
            {
                reason = $"找到的「{memberName}」是泛型方法，条件方法不能是泛型";
                return false;
            }

            if (method.ReturnType != typeof(bool))
            {
                reason = $"找到的「{memberName}」方法返回 {method.ReturnType.Name}，条件方法必须返回 bool";
                return false;
            }

            if (!ReflectedAccessor.TryCreateBooleanInvoker(method, out var invoker, out reason))
            {
                return false;
            }

            if (method.IsStatic)
            {
                condition = () => invoker(null);
                return true;
            }

            condition = () =>
            {
                var target = instance();
                return target != null && invoker(target);
            };

            return true;
        }

        #endregion
    }
}
