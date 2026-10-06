using System;
using System.Globalization;
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
        /// <remarks>
        /// <b>路径以索引段收尾时为 <c>null</c></b>（如元素节点的 <c>items.Array.data[0]</c>）：
        /// 末段不是任何成员，没有单一 <see cref="MemberInfo"/> 可记——此时看
        /// <see cref="ValueType"/>（它是元素类型）。
        /// </remarks>
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
        /// 编译一个**强类型**的读取器——「实例 → 当前值」。
        /// </summary>
        /// <typeparam name="T">
        /// 读取器要返回的类型。本包用到的是 <c>bool</c>、<c>float</c>、<c>Vector2</c>，
        /// 以及 <c>[ValueDropdown]</c> 数据源那一格的 <c>IList</c>。
        /// </typeparam>
        /// <param name="member">字段或属性。</param>
        /// <param name="reader">编译出的读取器；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>求值发生在绘制路径上、每帧一次</b>，而 <see cref="Read"/> 的返回值要装箱。
        /// 那条路能省一次分配就省一次——这与 <c>ValueSnapshot</c> 为 <c>[OnValueChanged]</c>
        /// 按类型取值而不走 <c>boxedValue</c> 是同一条理由。
        /// 别的调用方（只读展示）仍走 <see cref="Read"/>：那里每帧本来就要拼一个字符串，
        /// 再多一个箱子没有意义。
        /// </para>
        /// <para>
        /// <b>判定是「可赋值给 <c>T</c>」而不是「恰好是 <c>T</c>」</b>：列表那一格的
        /// <c>T</c> 是 <see cref="System.Collections.IList"/>，而成员的声明类型是
        /// <c>List&lt;string&gt;</c> 或某个数组——精确相等会把它们全拒掉。
        /// 值类型没有隐式变体，故 <c>bool</c>/<c>float</c>/<c>Vector2</c> 三格的行为逐字不变。
        /// </para>
        /// <para>
        /// <b>只在需要时才插 <c>Convert</c>，且只插一次（编译期）</b>：<c>body</c> 的类型是成员的
        /// 声明类型，lambda 的返回类型是 <c>T</c>，两者不同时才补一次引用转换。
        /// <c>T</c> 是值类型时 <c>body.Type == typeof(T)</c> 恒成立，于是**不插转换、不新增装箱**
        /// ——读取器进的是每帧路径（条件求值、<c>[MinMaxSlider]</c> 的边界），这条不是微优化。
        /// </para>
        /// </remarks>
        public static bool TryCreateReader<T>(MemberInfo member, out Func<object, T> reader, out string reason)
        {
            reader = null;

            if (!TryBuild(member, out var body, out var instance, out var valueType, out _, out reason))
            {
                return false;
            }

            if (!typeof(T).IsAssignableFrom(valueType))
            {
                reason = $"它是 {DescribeType(valueType)}，不是 {DescribeType(typeof(T))}";
                return false;
            }

            try
            {
                reader = Expression.Lambda<Func<object, T>>(Widen(body, typeof(T)), instance).Compile();
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
        /// <remarks>就是 <see cref="TryCreateReader{T}"/> 的 <c>bool</c> 那一格。留一个专门的入口，
        /// 是因为条件族与 <c>[Toggle]</c> 一族全都只要 bool，读作 bool 比读作 <c>T</c> 直白。</remarks>
        public static bool TryCreateBooleanReader(MemberInfo member, out Func<object, bool> reader, out string reason)
        {
            return TryCreateReader(member, out reader, out reason);
        }

        /// <summary>
        /// 成员值类型的惯用说法，供各处告警文案共用。
        /// </summary>
        /// <param name="type">类型；可以为 <c>null</c>。</param>
        /// <returns>
        /// <c>bool</c> 与 <c>float</c> 用 C# 关键字那套小写，其余用类型名
        /// （<c>Int32</c>、<c>Vector2</c>、泛型的 <c>List&lt;Int32&gt;</c>）。
        /// </returns>
        /// <remarks>
        /// <para>
        /// 单独一个入口是为了让「同一个类型在两处被告警成同一个词」——
        /// 各写一遍的话，一边说 <c>Boolean</c>、一边说 <c>bool</c> 只是时间问题。
        /// </para>
        /// <para>
        /// 泛型交给 <see cref="ReflectedValueFormatter.TypeName"/>：直接取 <c>Type.Name</c> 会得到
        /// <c>List`1</c> 这种带反引号元数的名字，出现在告警里像是坏了。
        /// 两处**共用同一份拼法**，不各写一遍。
        /// </para>
        /// </remarks>
        public static string DescribeType(Type type)
        {
            if (type == null)
            {
                return "null";
            }

            if (type == typeof(bool))
            {
                return "bool";
            }

            return type == typeof(float) ? "float" : ReflectedValueFormatter.TypeName(type);
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
        /// <b>字段段必须是实例字段。</b> 本工厂不认属性、不认静态成员——嵌套层与元素层的成员
        /// 本来就全是字段（它们来自 Unity 的序列化迭代器）。要认属性与静态成员就用
        /// <see cref="TryCreate"/>（单段版）。两条路对「哪些段认得」必须一致，
        /// 故段解析共用 <c>NestedMemberExpansion.FindDeclaredField</c>。
        /// </para>
        /// <para>
        /// <b>索引段只认 Unity 的 <c>Array</c> + <c>data[i]</c> 这一对</b>，判据落在
        /// **前置类型**上（当前类型是数组或 <c>List&lt;T&gt;</c> 时才是索引对——字段真叫
        /// <c>Array</c> 时它就是普通字段）。**可以出现多组**（元素层深度 &gt; 1 起，
        /// <c>items.Array.data[0].inner.Array.data[1].hp</c> 每层一对）：判据在每轮循环里
        /// 按当前前置类型重算，没有「只允许一组 / 只允许在末尾」的假设。非法形态
        /// （<c>Array.size</c>、下标非数字、多维数组……）**响亮拒绝**并说清为什么。
        /// 取元素带 null 与越界守卫（<c>AndAlso</c> 短路），与逐段空传播同一条纪律：
        /// 链在绘制路径上每帧求值，**绝不抛**。
        /// 越界/空集合的落点分两档：**末段索引**给 <c>null</c>（消费者据此显示「—」/跳过目标），
        /// **中间段**给 <c>default(元素类型)</c>。值类型元素取出的是副本，只读语义下无差别。
        /// </para>
        /// <para>
        /// <b>链只在末端转一次 <c>object</c></b>，中间段不逐个装箱
        /// （末段索引例外：它直接产出 <c>object</c> 好让越界表达成 <c>null</c>）。
        /// </para>
        /// <para>
        /// <b>深度只数字段段</b>（索引对不占 <c>MaxDepth</c> 预算）：预算挡的是自引用类型，
        /// 而索引段是**路径转义**（Unity 的属性路径形态），不是类型下钻。
        /// </para>
        /// <para>
        /// <b>表达式树的增长账</b>（记在这里，免得日后有人量到耗时先怀疑别处）：索引对的
        /// 前置子链会被引用三次——<c>BuildInRangeGuard</c> 里的非空判定与长度判定各一次、
        /// <c>BuildElementAccess</c> 一次，且 <c>Condition</c> 是**内联展开**而非 Block 临时变量，
        /// 于是每多一对索引，前缀在树里出现次数约 ×3（两对 ≈ 9×，预算 4 层内可接受）。
        /// 这不是正确性问题，是「层数 × 元素个数」之外的第二笔成本。
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
            var lastValueType = (Type)null;
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

                // **索引对**（Unity 的 `Array` + `data[i]`）。判据落在**前置类型**上而不是段名上：
                // 字段真叫 `Array` 时它就是普通字段（`Array.Array.data[0]` 靠这条正确解析）。
                var collectionType = CollectionElement.TypeOf(current);

                if (collectionType != null && string.Equals(name, "Array", StringComparison.Ordinal))
                {
                    if (separator < 0)
                    {
                        reason = "路径以「Array」结尾——它后面必须跟 data[i]（只认这一种形态）";
                        return false;
                    }

                    var indexStart = separator + 1;
                    var indexSeparator = path.IndexOf('.', indexStart);
                    var indexName = indexSeparator < 0
                        ? path.Substring(indexStart)
                        : path.Substring(indexStart, indexSeparator - indexStart);

                    if (!TryParseElementIndex(indexName, out var index, out reason))
                    {
                        return false;
                    }

                    // 多维数组不做：序列化系统本来就看不见它们（元素层的节点也不会走这条路径），
                    // 真放行的话 Expression.ArrayIndex 会抛，兜底出来的原因答非所问。
                    if (current.IsArray && current.GetArrayRank() != 1)
                    {
                        reason = $"路径段「{name}.{indexName}」落在多维数组上——只支持一维数组与 List<T>";
                        return false;
                    }

                    var collection = body ?? Convert(instance, current);
                    var inRange = BuildInRangeGuard(collection, current, index);
                    var indexed = BuildElementAccess(collection, current, index);

                    // **末段索引**（元素节点路径全是这种）：越界 / 空集合在 object 层给 null，
                    // 而不是 default(元素类型)——多选下各目标长度不一致时，值类型元素给默认值
                    // 会表现为「显示 0」，那是静默错值的近亲；给 null 则三处消费者都落到既有的
                    // 「取不到实例」语义（读值「—」、方法跳过该目标、条件算假）。
                    // **中间段**保持 default(元素类型)：后面还有字段段，null 与默认值在
                    // 「再往下读一个字段」这条路上是同一件事（引用类型给 null、值类型给默认）。
                    var isLast = indexSeparator < 0;

                    body = isLast
                        ? Expression.Condition(
                            inRange,
                            Expression.Convert(indexed, typeof(object)),
                            Expression.Constant(null, typeof(object)))
                        : Expression.Condition(inRange, indexed, Expression.Default(collectionType));

                    current = collectionType;
                    last = null;
                    lastValueType = collectionType;

                    if (isLast)
                    {
                        break;
                    }

                    start = indexSeparator + 1;
                    continue;
                }

                if (name.IndexOf('[') >= 0)
                {
                    reason =
                        $"路径段「{name}」只认得 Unity 的 `Array.data[i]` 形态——索引段必须成对出现，" +
                        "且前面那一段的字段是数组或 List<T>";
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

                if (NestedMemberExpansion.IsPolymorphicReference(field))
                {
                    reason =
                        $"路径段「{name}」是多态引用（[SerializeReference]）——展开判据不给它开半扇门" +
                        "（那是 L7 那条产品线），路径也就不该穿过它";
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
                lastValueType = field.FieldType;

                if (separator < 0)
                {
                    break;
                }

                start = separator + 1;
            }

            if (lastValueType == null)
            {
                reason = "路径不含任何有效段";
                return false;
            }

            try
            {
                var lambda = Expression.Lambda<Func<object, object>>(
                    Expression.Convert(body, typeof(object)), instance);

                // 恒为 false：段解析只看实例字段（静态成员够不着，见 FindDeclaredField 的说明），
                // 故整条链一定依赖目标对象。`last` 为 null 表示路径以**索引段**收尾
                // （元素节点路径），此时没有单一成员可记——`ValueType` 给元素类型。
                accessor = new ReflectedAccessor(last, lambda.Compile(), lastValueType, false);
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
        /// 把「无参、返回 <typeparamref name="T"/> 的**方法**」编译成 <c>实例 → 返回值</c> 的委托。
        /// </summary>
        /// <typeparam name="T">方法必须返回的类型。**返回值类型由调用方校验**——
        /// 这里只管编译，不判断「这个方法该不该被选中」（那是找成员那一层的判据）。</typeparam>
        /// <param name="method">方法。</param>
        /// <param name="invoker">编译出的调用委托；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>编译成「开实例」的调用，而不是把方法绑到某个实例上</b>
        /// （<c>MethodInfo.CreateDelegate(typeof(Func&lt;bool&gt;), target)</c> 那种）。
        /// 绑死的实例在嵌套层会**过期**：用户把父字段重新赋值（<c>stats = new …</c>、Undo、
        /// 预制体 revert）之后，条件读的还是旧对象——静默且极难归因。
        /// 开实例的委托每次调用都吃**当时**那个实例。
        /// </para>
        /// <para>
        /// 静态方法忽略入参，所以同一个委托签名两种情况都能用。
        /// </para>
        /// <para>
        /// 调用表达式产出的已经是 <typeparamref name="T"/>，不插 <c>Convert</c>——
        /// 与 <see cref="TryCreateReader{T}"/> 同一条理由（每帧路径上的装箱要省）。
        /// </para>
        /// </remarks>
        public static bool TryCreateInvoker<T>(MethodInfo method, out Func<object, T> invoker, out string reason)
        {
            invoker = null;

            if (method == null)
            {
                reason = "方法为 null";
                return false;
            }

            try
            {
                var instance = Expression.Parameter(typeof(object), "target");
                var call = method.IsStatic
                    ? Expression.Call(method)
                    : Expression.Call(Convert(instance, method.DeclaringType), method);

                invoker = Expression.Lambda<Func<object, T>>(Widen(call, typeof(T)), instance).Compile();
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
        /// 把「无参、返回 bool 的**方法**」编译成 <c>实例 → bool</c> 的委托。
        /// </summary>
        /// <param name="method">方法。</param>
        /// <param name="invoker">编译出的调用委托；失败时为 <c>null</c>。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>就是 <see cref="TryCreateInvoker{T}"/> 的 <c>bool</c> 那一格——理由同
        /// <see cref="TryCreateBooleanReader"/>。</remarks>
        public static bool TryCreateBooleanInvoker(
            MethodInfo method, out Func<object, bool> invoker, out string reason)
        {
            return TryCreateInvoker(method, out invoker, out reason);
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

        /// <summary>
        /// 解析 Unity 的 <c>data[i]</c> 索引段。
        /// </summary>
        /// <param name="name">段名。</param>
        /// <param name="index">解析出的下标。</param>
        /// <param name="reason">失败原因（中文，可直接拼进告警）。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// 只认 <c>data[非负整数]</c> 这一种形态：别的写法（<c>size</c>、负号、空格、空括号）
        /// 一律**响亮拒绝**并说清为什么——失败原因是告警文案的主体，
        /// 「在 X 上找不到名为 Y 的字段」那种答非所问的原因比不说更糟。
        /// </remarks>
        private static bool TryParseElementIndex(string name, out int index, out string reason)
        {
            index = 0;
            reason = null;

            const string Prefix = "data[";

            if (name.Length < Prefix.Length + 1 ||
                !name.StartsWith(Prefix, StringComparison.Ordinal) ||
                name[name.Length - 1] != ']')
            {
                reason = $"路径段「{name}」不是索引段——只认得 Unity 的 `data[i]` 形态";
                return false;
            }

            var digits = name.Substring(Prefix.Length, name.Length - Prefix.Length - 1);

            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out index))
            {
                reason = $"路径段「{name}」的下标不是非负整数";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 越界守卫：集合非空**且**下标在范围内。
        /// </summary>
        /// <param name="collection">集合表达式（一维数组或 <c>List&lt;T&gt;</c>）。</param>
        /// <param name="collectionType">集合类型。</param>
        /// <param name="index">下标。</param>
        /// <returns>布尔表达式。</returns>
        /// <remarks>
        /// <b><c>AndAlso</c> 的顺序就是守卫的全部</b>：集合为 <c>null</c> 时
        /// <c>Length</c> / <c>Count</c> 都会抛，短路保证右侧不执行。空集合与越界是真实存在的
        /// ——多选时各目标的长度可以不一致，而「每帧不抛」与逐段空传播是同一条纪律。
        /// </remarks>
        private static Expression BuildInRangeGuard(Expression collection, Type collectionType, int index)
        {
            var notNull = Expression.ReferenceNotEqual(collection, Expression.Constant(null, collectionType));
            var length = collectionType.IsArray
                ? (Expression)Expression.ArrayLength(collection)
                : Expression.Property(collection, "Count");

            return Expression.AndAlso(notNull, Expression.LessThan(Expression.Constant(index), length));
        }

        /// <summary>
        /// 按索引取元素的表达式（不含守卫）。
        /// </summary>
        /// <param name="collection">集合表达式。</param>
        /// <param name="collectionType">集合类型。</param>
        /// <param name="index">下标。</param>
        /// <returns>取值表达式，类型是元素类型。</returns>
        /// <remarks>
        /// 数组走 <see cref="Expression.ArrayIndex(Expression, Expression)"/>，
        /// <c>List&lt;T&gt;</c> 走它的索引器（<c>Item</c>）——<c>List</c> 的索引器越界会抛，
        /// 所以调用方必须先接 <see cref="BuildInRangeGuard"/>。值类型元素取出的是**副本**，
        /// 只读语义下无差别（本包不给反射成员写路径）。
        /// </remarks>
        private static Expression BuildElementAccess(Expression collection, Type collectionType, int index)
        {
            var constant = Expression.Constant(index);

            return collectionType.IsArray
                ? Expression.ArrayIndex(collection, constant)
                : Expression.Property(collection, "Item", constant);
        }

        /// <summary>
        /// 表达式本体的类型不是 <paramref name="target"/> 时补一次引用转换。
        /// </summary>
        /// <param name="body">表达式本体（类型是成员的声明类型或方法的返回类型）。</param>
        /// <param name="target">lambda 要返回的类型。</param>
        /// <returns>可直接当 lambda 本体的表达式。</returns>
        /// <remarks>
        /// <b>类型相同时原样返回</b>——那两个强类型工厂的「值类型不装箱」约定全靠这一条
        /// （<c>bool</c> / <c>float</c> / <c>Vector2</c> 三格永远走到这里就返回）。
        /// 需要转换的只有引用那一侧（<c>List&lt;string&gt;</c> → <c>IList</c>），
        /// 编译成一条 <c>castclass</c>，且在**编译期**只插一次。
        /// </remarks>
        private static Expression Widen(Expression body, Type target)
        {
            return body.Type == target ? body : Expression.Convert(body, target);
        }

        /// <summary>把编译异常转成一句可拼进告警的中文原因。</summary>
        /// <param name="exception">异常。</param>
        /// <returns>原因文本。</returns>
        private static string Describe(Exception exception)
        {
            return $"无法为它编译访问委托：{exception.Message}";
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
