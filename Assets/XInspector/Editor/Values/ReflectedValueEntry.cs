using System;
using UnityEditor;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// 第二套值后端：按反射读取普通属性、非序列化字段与静态成员。
    /// <para>
    /// <b>它是只读的，这是刻意的。</b> 反射成员按定义不在 Unity 的序列化里，因此
    /// <see cref="SerializedObject"/> 白送的那五件事（Undo/Redo、预制体覆盖、场景标脏、
    /// 多对象编辑、域重载后取值）一件也拿不到：写进去既不可撤销，也会在下次域重载或存档时
    /// 无声消失。Odin 自己的文档也写着「<c>[ShowInInspector]</c> 不序列化任何东西，
    /// 改动不会随之保存」——本包把这句话落实成「干脆不给写」，
    /// 而不是做一个看起来能改、改完就丢的控件。
    /// </para>
    /// <para>
    /// <b>取值走构建期编译好的委托</b>（见 <see cref="ReflectedAccessor"/>），绘制路径上没有反射。
    /// </para>
    /// </summary>
    internal sealed class ReflectedValueEntry : PropertyValueEntry
    {
        #region Private Fields

        /// <summary>目标对象，与 <see cref="_accessors"/> 一一对应。</summary>
        private readonly object[] _targets;

        /// <summary>逐目标的取值访问器；某个目标上没有这个成员时该格为 <c>null</c>。</summary>
        private readonly ReflectedAccessor[] _accessors;

        /// <summary>
        /// 逐目标的**嵌套实例**来源；顶层成员为 <c>null</c>。
        /// </summary>
        /// <remarks>
        /// 非 null 时，取值是 <c>访问器(实例(目标))</c> 而不是 <c>访问器(目标)</c>——
        /// 实例由一条构建期编译的字段链每帧现读（见 <see cref="ReflectedAccessor.TryCreatePath"/>），
        /// 因此父字段被重新赋值之后跟着走。
        /// </remarks>
        private readonly ReflectedAccessor[] _scopes;

        /// <summary>成员的值类型。</summary>
        private readonly Type _valueType;

        /// <summary>是否为静态成员。</summary>
        private readonly bool _isStatic;

        #endregion

        #region Construction

        /// <summary>
        /// 构造反射值入口。
        /// </summary>
        /// <param name="targets">目标对象数组。</param>
        /// <param name="accessors">逐目标的取值访问器，与 <paramref name="targets"/> 同长。</param>
        /// <param name="valueType">成员的值类型。</param>
        /// <param name="scopes">
        /// 逐目标的**嵌套实例**来源，与 <paramref name="targets"/> 同长；顶层成员传 <c>null</c>。
        /// </param>
        /// <remarks>
        /// <b>逐目标各一个访问器</b>而不是共用一个：多选下的目标未必是同一个类型，
        /// 而同一个成员名在不同类型上可能是不同的 <c>MemberInfo</c>。
        /// 某个目标解析不到就是 <c>null</c>，读取时算「不一致」——我们确实不知道它的值。
        /// </remarks>
        public ReflectedValueEntry(
            object[] targets,
            ReflectedAccessor[] accessors,
            Type valueType,
            ReflectedAccessor[] scopes = null)
        {
            _targets = targets ?? Array.Empty<object>();
            _accessors = accessors ?? Array.Empty<ReflectedAccessor>();
            _scopes = scopes;
            _valueType = valueType ?? typeof(object);

            for (var i = 0; i < _accessors.Length; i++)
            {
                if (_accessors[i] != null)
                {
                    _isStatic = _accessors[i].IsStatic;
                    break;
                }
            }
        }

        #endregion

        #region PropertyValueEntry

        /// <summary>
        /// 成员的值类型。
        /// </summary>
        public override Type ValueType => _valueType;

        /// <summary>
        /// 恒为 <c>false</c>——本后端的值不来自 Unity 的序列化系统。
        /// </summary>
        public override bool IsUnityBacked => false;

        /// <summary>
        /// 恒为 <c>null</c>——没有序列化属性可给。
        /// </summary>
        /// <remarks>
        /// 依赖它的绘制器（绝大多数值绘制器）会据此退让，由反射成员的末端绘制器接管。
        /// </remarks>
        public override SerializedProperty SerializedProperty => null;

        /// <summary>
        /// 取第 <paramref name="index"/> 个目标上的**取值对象**——顶层是目标本身，
        /// 嵌套层是沿字段链现读到的那个实例。
        /// </summary>
        /// <param name="index">目标下标。</param>
        /// <returns>取值对象；嵌套实例取不到时返回 <c>null</c>。</returns>
        private object ResolveInstance(int index)
        {
            var target = index < _targets.Length ? _targets[index] : null;

            if (_scopes == null)
            {
                return target;
            }

            var scope = index < _scopes.Length ? _scopes[index] : null;
            return scope?.Read(target);
        }

        /// <summary>
        /// 多目标编辑时各目标的值是否不一致。
        /// </summary>
        /// <remarks>
        /// <b>绘制路径不要用这个属性。</b> 它会真的去读值；终端应当走
        /// <see cref="TryGetDisplayValue"/> 一次读到底——先问这个再取值会把用户的 getter
        /// 每帧读上两遍。
        /// </remarks>
        public override bool HasMultipleDifferentValues
        {
            get
            {
                TryGetDisplayValue(out _, out var mixed, out _);
                return mixed;
            }
        }

        /// <summary>
        /// 读取当前值。
        /// </summary>
        /// <returns>当前值，装箱返回。</returns>
        /// <exception cref="NotSupportedException">多目标值不一致，或取值时用户代码抛了异常。</exception>
        /// <remarks>
        /// 多目标不一致时抛异常，与 <see cref="SerializedPropertyValueEntry"/> 同款语义——
        /// 那种情况下本就没有「单一的值」这回事。能给出「不一致」这个结论的是
        /// <see cref="TryGetDisplayValue"/>。
        /// </remarks>
        public override object GetValue()
        {
            if (TryGetDisplayValue(out var value, out _, out var error))
            {
                return value;
            }

            throw new NotSupportedException(
                error != null
                    ? $"读取该反射成员的值时出错：{error}"
                    : "多个目标的值不一致，没有单一的值可取。");
        }

        /// <summary>
        /// 恒抛——反射后端是只读的。
        /// </summary>
        /// <param name="value">新值；不会被使用。</param>
        /// <exception cref="NotSupportedException">总是抛出。</exception>
        public override void SetValue(object value)
        {
            throw new NotSupportedException(
                "反射后端是只读的：反射成员不在 Unity 的序列化里，" +
                "写进去既没法撤销，也不会随存档保存。");
        }

        #endregion

        #region Public API

        /// <summary>
        /// 读一次值，并给出「是否不一致」「是否出错」。
        /// </summary>
        /// <param name="value">第一个目标的值；不一致或出错时为 <c>null</c>。</param>
        /// <param name="mixed">各目标的值是否不一致。</param>
        /// <param name="error">取值时用户代码抛出的异常消息；没有出错时为 <c>null</c>。</param>
        /// <returns>拿到了一个可用于显示的值返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>绘制路径唯一的取值入口</b>，另有一个非绘制路径的消费者：
        /// 搜索匹配（查询变化时调一次，见 <see cref="SearchMatcher.NodeMatchesSelf"/>）。
        /// 一次调用最多把每个目标的 getter 各读一遍，
        /// 不像「先问 <see cref="HasMultipleDifferentValues"/> 再问 <see cref="GetValue"/>」
        /// 那样读两轮——用户的 getter 可能有开销，也可能有副作用。
        /// </para>
        /// <para>
        /// 一旦发现不一致就立刻停下，不读完剩下的目标：结论已经定了，多读只是多跑几遍用户代码。
        /// </para>
        /// <para>
        /// 用户的 getter 抛异常时**不外传**，转成 <paramref name="error"/>——
        /// 一个写坏的属性不该让整个 Inspector 白屏，但也绝不能静默。
        /// </para>
        /// </remarks>
        public bool TryGetDisplayValue(out object value, out bool mixed, out string error)
        {
            value = null;
            mixed = false;
            error = null;

            if (_accessors.Length == 0)
            {
                // 一个目标都解析不到这个成员。对显示而言与「不一致」是同一件事：
                // 我们给不出一个诚实的值。
                mixed = true;
                return false;
            }

            // 静态成员跨目标天然一致：只读一次，也不参与一致性比较。
            if (_isStatic)
            {
                var staticAccessor = FirstAccessor();
                if (staticAccessor == null)
                {
                    mixed = true;
                    return false;
                }

                return TryRead(staticAccessor, null, out value, ref error);
            }

            var haveFirst = false;

            for (var i = 0; i < _accessors.Length; i++)
            {
                var accessor = _accessors[i];
                if (accessor == null)
                {
                    mixed = true;
                    value = null;
                    return false;
                }

                var target = ResolveInstance(i);

                // 嵌套实例取不到（父字段为空）：给不出一个诚实的值，按「不一致」处置——
                // 与「某个目标上没有这个成员」同款，终端会画「—」。
                if (_scopes != null && target == null)
                {
                    mixed = true;
                    value = null;
                    return false;
                }

                if (!TryRead(accessor, target, out var current, ref error))
                {
                    value = null;
                    return false;
                }

                if (!haveFirst)
                {
                    value = current;
                    haveFirst = true;
                }
                else if (!SameValue(value, current))
                {
                    mixed = true;
                    value = null;
                    return false;
                }
            }

            return haveFirst;
        }

        /// <summary>
        /// 读一次值并直接给出**显示文本**——「读 + 格式化」收成一处。
        /// </summary>
        /// <param name="text">显示文本；没拿到可用值时（不一致或出错）为 <c>null</c>。</param>
        /// <param name="mixed">各目标的值不一致（或实例取不到、某个目标上没有这个成员）。</param>
        /// <param name="error">取值时用户代码抛出的异常消息；没有出错时为 <c>null</c>。</param>
        /// <returns>拿到了可用于显示的文本返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>两个消费者，一份实现</b>：只读展示的末端绘制器，与**搜索匹配**。
        /// 它们问的是同一个问题（「这个成员现在显示成什么」），答案不该有两个——
        /// 各写一遍的话，「搜得到的东西」与「眼睛看见的东西」迟早对不上。
        /// </para>
        /// <para>
        /// <b>它会真的把用户的 getter 读一遍。</b> 绘制路径每帧一次（那是展示的成本），
        /// 搜索路径在**查询变化时**一次（见 <see cref="SearchMatcher"/> 的重算时机）。
        /// </para>
        /// </remarks>
        public bool TryGetDisplayText(out string text, out bool mixed, out string error)
        {
            text = null;

            if (!TryGetDisplayValue(out var value, out mixed, out error))
            {
                return false;
            }

            text = ReflectedValueFormatter.Format(value, ValueType);
            return true;
        }

        #endregion

        #region Private Helpers

        /// <summary>取第一个非空的访问器。</summary>
        /// <returns>访问器；全为空时返回 <c>null</c>。</returns>
        private ReflectedAccessor FirstAccessor()
        {
            for (var i = 0; i < _accessors.Length; i++)
            {
                if (_accessors[i] != null)
                {
                    return _accessors[i];
                }
            }

            return null;
        }

        /// <summary>读一次，异常转成错误消息。</summary>
        /// <param name="accessor">访问器。</param>
        /// <param name="target">目标对象。</param>
        /// <param name="value">读到的值。</param>
        /// <param name="error">错误消息。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryRead(ReflectedAccessor accessor, object target, out object value, ref string error)
        {
            try
            {
                value = accessor.Read(target);
                return true;
            }
            catch (Exception exception)
            {
                value = null;
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 两个目标的值算不算相同。
        /// </summary>
        /// <param name="left">前一个值。</param>
        /// <param name="right">后一个值。</param>
        /// <returns>相同返回 <c>true</c>。</returns>
        /// <remarks>
        /// <b>判据取自成员的声明类型，而不是值的运行时类型。</b> 声明类型是 Unity 对象时用它自己的
        /// <c>==</c> 判等——那个运算符把「已销毁的对象」也算作空，于是「一个目标是空、
        /// 另一个是被销毁的对象」不会被误报成不一致。这个信息只能来自声明类型：
        /// 只看值的话，<c>null</c> 与「已销毁的对象」是两种完全不同的东西。
        /// </remarks>
        private bool SameValue(object left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (typeof(Object).IsAssignableFrom(_valueType))
            {
                return left as Object == right as Object;
            }

            return Equals(left, right);
        }

        #endregion
    }
}
