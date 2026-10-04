using System;
using System.Linq.Expressions;
using System.Reflection;

namespace XInspector.Editor
{
    /// <summary>
    /// 把一个字段或属性编译成「读一个目标对象、得到它的当前值」的委托。
    /// <para>
    /// <b>它存在的唯一理由是「反射仅限构建期」这条硬规则。</b>
    /// <c>FieldInfo.GetValue</c> / <c>PropertyInfo.GetValue</c> 每调一次都要走一遍反射通路，
    /// 而反射成员的取值发生在**绘制路径**上——每帧、每个目标各一次。
    /// 于是在建树时把成员编译成一个委托，之后每帧只剩一次委托调用加一次装箱。
    /// </para>
    /// <para>
    /// <b>它只管读。</b> 反射成员在本包里一律只读（见 <c>[ShowInInspector]</c> 的说明），
    /// 所以这里没有对应的写入口——写路径拿不到 Undo、标脏与预制体覆盖，
    /// 与其做一个会静默丢失的写，不如不做。
    /// </para>
    /// <para>
    /// 编译失败时 <see cref="TryCreate"/> 返回 <c>false</c> 并给出可读的原因，
    /// 由调用方告警并**跳过该成员**。<b>刻意不设「每帧反射」的兜底</b>：
    /// 那会让「反射不进绘制路径」这条不变量出现例外，而一处例外就足以让整条规则再也说不清。
    /// </para>
    /// </summary>
    internal sealed class ReflectedAccessor
    {
        #region Private Fields

        /// <summary>编译好的取值委托。</summary>
        private readonly Func<object, object> _reader;

        #endregion

        #region Construction

        /// <summary>
        /// 构造取值访问器。由 <see cref="TryCreate"/> 在编译成功后调用。
        /// </summary>
        /// <param name="member">被读取的字段或属性。</param>
        /// <param name="reader">编译好的取值委托。</param>
        /// <param name="valueType">成员的值类型。</param>
        /// <param name="isStatic">是否为静态成员。</param>
        private ReflectedAccessor(MemberInfo member, Func<object, object> reader, Type valueType, bool isStatic)
        {
            Member = member;
            ValueType = valueType;
            IsStatic = isStatic;
            _reader = reader;
        }

        #endregion

        #region Public API

        /// <summary>
        /// 被读取的成员。
        /// </summary>
        public MemberInfo Member { get; }

        /// <summary>
        /// 成员的值类型（字段的 <c>FieldType</c> / 属性的 <c>PropertyType</c>）。
        /// </summary>
        public Type ValueType { get; }

        /// <summary>
        /// 是否为静态成员。
        /// </summary>
        /// <remarks>
        /// 静态成员不参与「多目标值是否一致」的比较——它跨目标天然一致，
        /// 拿来比只会把「所有目标看到同一个值」误报成一致。
        /// </remarks>
        public bool IsStatic { get; }

        /// <summary>
        /// 编译成功后返回访问器；失败返回 <c>false</c> 并给出原因。
        /// </summary>
        /// <param name="member">字段或属性。</param>
        /// <param name="accessor">编译出的访问器；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因（中文，可直接拼进告警）；成功时为 <c>null</c>。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// 拒绝的三种成员：索引器（没有「一个目标对应一个值」的语义）、只写属性（读不到值）、
        /// 以及既非字段也非属性的成员（方法、事件、构造器）。
        /// 表达式树编译本身也可能失败，那种情况的原因取自异常消息。
        /// </remarks>
        public static bool TryCreate(MemberInfo member, out ReflectedAccessor accessor, out string reason)
        {
            accessor = null;

            if (!TryBuild(member, out var body, out var instance, out var valueType, out var isStatic, out reason))
            {
                return false;
            }

            try
            {
                var lambda = Expression.Lambda<Func<object, object>>(
                    Expression.Convert(body, typeof(object)), instance);

                accessor = new ReflectedAccessor(member, lambda.Compile(), valueType, isStatic);
                reason = null;
                return true;
            }
            catch (Exception exception)
            {
                reason = Describe(exception);
                return false;
            }
        }

        /// <summary>
        /// 编译一个**强类型**的 bool 读取器。
        /// </summary>
        /// <param name="member">字段或属性，必须是 <c>bool</c>。</param>
        /// <param name="reader">编译出的读取器；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <b>条件的求值发生在绘制路径上、每帧一次</b>，而 <see cref="Read"/> 的返回值要装箱。
        /// 那条路能省一次分配就省一次——这与 <c>ValueSnapshot</c> 为 <c>[OnValueChanged]</c>
        /// 按类型取值而不走 <c>boxedValue</c> 是同一条理由。
        /// 别的调用方（只读展示）仍走 <see cref="Read"/>：那里每帧本来就要拼一个字符串，
        /// 再多一个箱子没有意义。
        /// </remarks>
        public static bool TryCreateBooleanReader(MemberInfo member, out Func<object, bool> reader, out string reason)
        {
            reader = null;

            if (!TryBuild(member, out var body, out var instance, out var valueType, out _, out reason))
            {
                return false;
            }

            if (valueType != typeof(bool))
            {
                reason = $"它是 {valueType.Name}，不是 bool";
                return false;
            }

            try
            {
                reader = Expression.Lambda<Func<object, bool>>(body, instance).Compile();
                reason = null;
                return true;
            }
            catch (Exception exception)
            {
                reason = Describe(exception);
                return false;
            }
        }

        /// <summary>
        /// 读一次值。
        /// </summary>
        /// <param name="target">目标对象；静态成员忽略它，可以传 <c>null</c>。</param>
        /// <returns>当前值，装箱返回。</returns>
        public object Read(object target)
        {
            return _reader(target);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 校验成员形状并搭出访问表达式的骨架。
        /// </summary>
        /// <param name="member">字段或属性。</param>
        /// <param name="body">取值表达式的本体。</param>
        /// <param name="instance">目标形参。</param>
        /// <param name="valueType">成员的值类型。</param>
        /// <param name="isStatic">是否为静态成员。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>形状可用返回 <c>true</c>。</returns>
        /// <remarks>
        /// 两个入口（装箱版与强类型版）共用这一段，是为了让「哪些成员读得了」
        /// 只有一个答案——分成两份的话，早晚会出现「展示读得到、条件读不到」这种怪事。
        /// </remarks>
        private static bool TryBuild(
            MemberInfo member,
            out Expression body,
            out ParameterExpression instance,
            out Type valueType,
            out bool isStatic,
            out string reason)
        {
            body = null;
            instance = Expression.Parameter(typeof(object), "target");
            valueType = null;
            isStatic = false;
            reason = null;

            if (member == null)
            {
                reason = "成员为 null";
                return false;
            }

            if (member is FieldInfo field)
            {
                valueType = field.FieldType;
                isStatic = field.IsStatic;
                body = Expression.Field(isStatic ? null : Convert(instance, field.DeclaringType), field);
                return true;
            }

            if (member is PropertyInfo property)
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    reason = "它是索引器（带参数），没有「一个目标对应一个值」的语义";
                    return false;
                }

                var getter = property.GetGetMethod(true);
                if (getter == null)
                {
                    reason = "它是只写属性，读不到值";
                    return false;
                }

                valueType = property.PropertyType;
                isStatic = getter.IsStatic;
                body = Expression.Property(isStatic ? null : Convert(instance, property.DeclaringType), property);
                return true;
            }

            reason = $"它既不是字段也不是属性（{member.MemberType}）";
            return false;
        }

        /// <summary>把编译异常转成一句可拼进告警的中文原因。</summary>
        /// <param name="exception">异常。</param>
        /// <returns>原因文本。</returns>
        private static string Describe(Exception exception)
        {
            return $"无法为它编译取值委托：{exception.Message}";
        }

        /// <summary>
        /// 为实例成员把 <c>object</c> 形参转成成员的声明类型。
        /// </summary>
        /// <param name="instance">形参表达式。</param>
        /// <param name="declaringType">成员的声明类型。</param>
        /// <returns>转换表达式。</returns>
        /// <remarks>
        /// 值类型的声明类型走的是<b>拆箱</b>而不是引用转换，但 <see cref="Expression.Convert(Expression, Type)"/>
        /// 两种情况都认，不必分支。声明类型为 <c>null</c> 时退回 <c>object</c>——
        /// 那只会出现在全局字段上，而全局字段必然是静态的，走不到这里。
        /// </remarks>
        private static Expression Convert(ParameterExpression instance, Type declaringType)
        {
            return Expression.Convert(instance, declaringType ?? typeof(object));
        }

        #endregion
    }
}
