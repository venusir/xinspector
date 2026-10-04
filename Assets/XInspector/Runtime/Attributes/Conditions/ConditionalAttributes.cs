using System;

namespace XInspector
{
    /// <summary>
    /// 按另一个成员的值决定本成员**是否可见**。
    /// <para>
    /// 条件名是**序列化成员的名字**（可以是 <c>a/b</c> 这样的嵌套路径），解析出来的值须为
    /// <see cref="bool"/>。名字不存在时**保持可见并记一条告警**，不抛异常——
    /// 一个拼错的名字不该让整个 Inspector 白屏。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>条件对象必须是序列化成员。</b> 本包的值后端是 <c>SerializedObject</c>
    /// （UnityEditor 的类型，Runtime 侧只能当名字提，不能写 cref），
    /// 因此普通属性、方法、静态成员都读不到——那需要一套反射后端，尚未实现。
    /// </para>
    /// <para>
    /// 条件**每帧重新求值**，所以被条件的字段可以随时跟着切换，不必重新构建属性树。
    /// </para>
    /// <para>
    /// <b>本文件四个特性都允许标在方法上</b>（与 <see cref="ButtonAttribute"/> 配合用），
    /// 这是本包少见的「放宽到方法」——判据在于方法**会**产生属性树节点，
    /// 而普通属性不会。标在既无 <see cref="ButtonAttribute"/> 又不产生节点的普通方法上，
    /// 它仍然什么都不做。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public bool isAlive = true;
    ///
    /// [ShowIf(nameof(isAlive))]
    /// public int health = 100;
    ///
    /// [Button, DisableIf(nameof(isAlive))]
    /// private void Finish() { }
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class ShowIfAttribute : Attribute
    {
        /// <summary>
        /// 以条件成员名构造。
        /// </summary>
        /// <param name="condition">序列化成员名，须为 <see cref="bool"/> 类型。</param>
        /// <exception cref="ArgumentException"><paramref name="condition"/> 为 null 或空白。</exception>
        public ShowIfAttribute(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new ArgumentException("条件成员名不能为空。", nameof(condition));
            }

            Condition = condition;
        }

        /// <summary>
        /// 条件成员名。
        /// </summary>
        public string Condition { get; }
    }

    /// <summary>
    /// 按另一个成员的值决定本成员**是否隐藏**。见 <see cref="ShowIfAttribute"/> 的完整说明。
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class HideIfAttribute : Attribute
    {
        /// <summary>
        /// 以条件成员名构造。
        /// </summary>
        /// <param name="condition">序列化成员名，须为 <see cref="bool"/> 类型。</param>
        /// <exception cref="ArgumentException"><paramref name="condition"/> 为 null 或空白。</exception>
        public HideIfAttribute(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new ArgumentException("条件成员名不能为空。", nameof(condition));
            }

            Condition = condition;
        }

        /// <summary>
        /// 条件成员名。
        /// </summary>
        public string Condition { get; }
    }

    /// <summary>
    /// 按另一个成员的值决定本成员**是否可编辑**（不可编辑时变灰但仍可见）。
    /// 见 <see cref="ShowIfAttribute"/> 的完整说明。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="ShowIfAttribute"/> 的差别在于「隐藏」与「禁用」：
    /// 禁用保留了「这个字段存在、只是现在不能改」的信息，通常比直接藏掉更有用。
    /// </remarks>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class EnableIfAttribute : Attribute
    {
        /// <summary>
        /// 以条件成员名构造。
        /// </summary>
        /// <param name="condition">序列化成员名，须为 <see cref="bool"/> 类型。</param>
        /// <exception cref="ArgumentException"><paramref name="condition"/> 为 null 或空白。</exception>
        public EnableIfAttribute(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new ArgumentException("条件成员名不能为空。", nameof(condition));
            }

            Condition = condition;
        }

        /// <summary>
        /// 条件成员名。
        /// </summary>
        public string Condition { get; }
    }

    /// <summary>
    /// 按另一个成员的值决定本成员**是否被禁用**。见 <see cref="EnableIfAttribute"/>。
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class DisableIfAttribute : Attribute
    {
        /// <summary>
        /// 以条件成员名构造。
        /// </summary>
        /// <param name="condition">序列化成员名，须为 <see cref="bool"/> 类型。</param>
        /// <exception cref="ArgumentException"><paramref name="condition"/> 为 null 或空白。</exception>
        public DisableIfAttribute(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new ArgumentException("条件成员名不能为空。", nameof(condition));
            }

            Condition = condition;
        }

        /// <summary>
        /// 条件成员名。
        /// </summary>
        public string Condition { get; }
    }
}
