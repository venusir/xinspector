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

            var type = target.GetType();

            for (var current = type; current != null; current = current.BaseType)
            {
                var candidates = current.GetMember(memberName, Flags);
                if (candidates.Length > 0)
                {
                    return TryBind(candidates, target, memberName, out condition, out reason);
                }
            }

            reason = $"目标对象上找不到名为「{memberName}」的字段、属性或方法";
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
        /// <param name="target">目标对象。</param>
        /// <param name="memberName">成员名，用于告警文本。</param>
        /// <param name="condition">绑定出的求值器。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>绑定成功返回 <c>true</c>。</returns>
        private static bool TryBind(
            MemberInfo[] candidates,
            object target,
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
                    return TryBindField(field, target, memberName, out condition, out reason);
                }

                if (candidates[i] is PropertyInfo property)
                {
                    return TryBindProperty(property, target, memberName, out condition, out reason);
                }
            }

            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is MethodInfo method)
                {
                    return TryBindMethod(method, target, memberName, out condition, out reason);
                }
            }

            reason = $"「{memberName}」既不是字段、属性，也不是方法";
            return false;
        }

        /// <summary>绑定一个 bool 字段。</summary>
        /// <param name="field">字段。</param>
        /// <param name="target">目标对象。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="condition">绑定出的求值器。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryBindField(
            FieldInfo field,
            object target,
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

            condition = () => reader(target);
            return true;
        }

        /// <summary>绑定一个 bool 属性。</summary>
        /// <param name="property">属性。</param>
        /// <param name="target">目标对象。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="condition">绑定出的求值器。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryBindProperty(
            PropertyInfo property,
            object target,
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

            condition = () => reader(target);
            return true;
        }

        /// <summary>
        /// 绑定一个无参、非泛型、返回 bool 的方法。
        /// </summary>
        /// <param name="method">方法。</param>
        /// <param name="target">目标对象。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="condition">绑定出的求值器。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <b>绑定成委托而不是每帧 <c>Invoke</c>。</b> 条件每帧求值一次，
        /// 而 <c>MethodInfo.Invoke</c> 是反射调用——「反射仅限构建期」这条规则对它同样成立。
        /// 实例方法绑到主目标上（闭包里的那个），静态方法不绑目标。
        /// </remarks>
        private static bool TryBindMethod(
            MethodInfo method,
            object target,
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

            try
            {
                condition = method.IsStatic
                    ? (Func<bool>)method.CreateDelegate(typeof(Func<bool>))
                    : (Func<bool>)method.CreateDelegate(typeof(Func<bool>), target);
                return true;
            }
            catch (Exception exception)
            {
                reason = $"无法为方法「{memberName}」绑定委托：{exception.Message}";
                return false;
            }
        }

        #endregion
    }
}
