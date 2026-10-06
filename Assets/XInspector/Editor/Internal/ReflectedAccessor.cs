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
        /// 把一条**点分序列化路径**编译成「读一个根目标、得到路径末端当前值」的委托。
        /// </summary>
        /// <param name="rootType">根目标的类型（被检视对象的类型）。</param>
        /// <param name="path">点分路径，如 <c>stats.hp</c>。</param>
        /// <param name="accessor">编译出的访问器；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因（中文，可直接拼进告警）；成功时为 <c>null</c>。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>它是「嵌套层的读路径」。</b> Unity 没有公开 API 从 <c>SerializedProperty</c> 拿到
        /// 嵌套托管实例——<c>boxedValue</c> 只是**序列化数据的装箱快照**，读不到非序列化成员
        /// （而那正是 <c>[ShowInInspector]</c> 存在的理由），也给不出可调用的活实例。
        /// 于是唯一可行的路是「根目标 + 按路径逐段下钻」，编译成一条委托链。
        /// 嵌套成员节点的 <c>Path</c> 恰好就是绝对序列化路径，不必另拼。
        /// </para>
        /// <para>
        /// <b>每一段都必须是实例字段。</b> 本工厂不认属性、不认静态成员——嵌套层的成员本来就
        /// 全是字段（它们来自 Unity 的序列化迭代器）。要认属性与静态成员就用
        /// <see cref="TryCreate"/>（单段版）。两条路对「哪些段认得」必须一致，
        /// 故段解析共用 <c>NestedMemberExpansion.FindDeclaredField</c>。
        /// </para>
        /// <para>
        /// <b>链只在末端转一次 <c>object</c></b>，中间段不逐个装箱。
        /// </para>
        /// <para>
        /// <b>数组与多态段响亮拒绝，不静默错读。</b> 展开判据本来就不展开数组与
        /// <c>[SerializeReference]</c>，所以今天走不到那里；拒绝是给元素节点化那天留的接口。
        /// </para>
        /// </remarks>
        public static bool TryCreatePath(
            Type rootType, string path, out ReflectedAccessor accessor, out string reason)
        {
            accessor = null;

            if (rootType == null || string.IsNullOrEmpty(path))
            {
                reason = "起点类型或路径为空";
                return false;
            }

            var instance = Expression.Parameter(typeof(object), "target");
            Expression body = null;
            var current = rootType;
            var last = (FieldInfo)null;
            var depth = 0;
            var start = 0;

            while (start < path.Length)
            {
                var separator = path.IndexOf('.', start);
                var name = separator < 0 ? path.Substring(start) : path.Substring(start, separator - start);

                if (name.Length == 0)
                {
                    reason = "路径里有空段";
                    return false;
                }

                if (string.Equals(name, "Array", StringComparison.Ordinal) || name.IndexOf('[') >= 0)
                {
                    reason =
                        $"路径段「{name}」是数组/列表的段——按元素取实例是「元素节点化」的领域，" +
                        "本包还没有那条路";
                    return false;
                }

                if (++depth > NestedMemberExpansion.MaxDepth)
                {
                    reason = $"路径超过 {NestedMemberExpansion.MaxDepth} 层";
                    return false;
                }

                var field = NestedMemberExpansion.FindDeclaredField(current, name);
                if (field == null)
                {
                    reason = $"在 {current.Name} 上找不到名为「{name}」的实例字段";
                    return false;
                }

                var source = body ?? Convert(instance, field.DeclaringType);

                // **空传播**：中间段为 null 时整条链给 null，而不是每帧抛 NullReferenceException。
                // 托管对象与序列化数据不同——`public Inner inner;` 可以真的是 null
                // （序列化那条路上 Unity 总会补出一个实例，托管这条不会）。
                // 值类型的段没有「null」可言，直接取字段。
                body = source.Type.IsValueType
                    ? Expression.Field(source, field)
                    : Expression.Condition(
                        Expression.ReferenceEqual(source, Expression.Constant(null, source.Type)),
                        Expression.Default(field.FieldType),
                        Expression.Field(source, field));

                current = field.FieldType;
                last = field;

                if (separator < 0)
                {
                    break;
                }

                start = separator + 1;
            }

            if (last == null)
            {
                reason = "路径不含任何有效段";
                return false;
            }

            try
            {
                var lambda = Expression.Lambda<Func<object, object>>(
                    Expression.Convert(body, typeof(object)), instance);

                // 恒为 false：段解析只看实例字段（静态成员够不着，见 FindDeclaredField 的说明），
                // 故整条链一定依赖目标对象。
                accessor = new ReflectedAccessor(last, lambda.Compile(), last.FieldType, false);
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
