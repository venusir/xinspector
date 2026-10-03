using System;

namespace XInspector
{
    /// <summary>
    /// 在字段上方画一条信息框。
    /// <para>
    /// 用在类上时出现在整个 Inspector 的最上方——与 <see cref="TitleAttribute"/> 同理，
    /// 类级特性由构建期放到根节点上，绘制器链照样包住子节点，不是特例代码。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <c>visibleIfMemberName</c> 指的是**序列化成员名**（public 字段或
    /// <c>[SerializeField]</c> 私有字段），解析出来须为 <see cref="bool"/>。
    /// 它与条件族一样：名字无效时**保持显示**并记一条告警，不抛异常。
    /// <b>类级 + 条件不受支持</b>——根节点没有值入口，无从解析，此时告警并按无条件处理。
    /// </remarks>
    /// <example>
    /// <code>
    /// [InfoBox("生命值低于 20 会进入濒死状态", InfoMessageType.Warning)]
    /// public int health = 100;
    ///
    /// [InfoBox("仅在开启调试时显示", InfoMessageType.Info, nameof(debugMode))]
    /// public int debugValue;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class InfoBoxAttribute : Attribute
    {
        /// <summary>
        /// 以消息与样式构造。
        /// </summary>
        /// <param name="message">消息文本，不得为空白。</param>
        /// <param name="infoMessageType">样式，默认 <see cref="InfoMessageType.Info"/>。</param>
        /// <param name="visibleIfMemberName">可选的可见性条件成员名；为 null 表示始终显示。</param>
        /// <exception cref="ArgumentException"><paramref name="message"/> 为 null、空串或仅含空白。</exception>
        public InfoBoxAttribute(
            string message,
            InfoMessageType infoMessageType = InfoMessageType.Info,
            string visibleIfMemberName = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("信息框的内容不能为空。", nameof(message));
            }

            Message = message;
            InfoMessageType = infoMessageType;
            VisibleIfMemberName = visibleIfMemberName;
        }

        /// <summary>
        /// 以消息与可见性条件构造（样式取默认的 <see cref="InfoMessageType.Info"/>）。
        /// </summary>
        /// <param name="message">消息文本，不得为空白。</param>
        /// <param name="visibleIfMemberName">可见性条件成员名，须为 <see cref="bool"/>。</param>
        /// <exception cref="ArgumentException"><paramref name="message"/> 为 null、空串或仅含空白。</exception>
        public InfoBoxAttribute(string message, string visibleIfMemberName)
            : this(message, InfoMessageType.Info, visibleIfMemberName)
        {
        }

        /// <summary>消息文本。</summary>
        public string Message { get; }

        /// <summary>信息框样式。</summary>
        public InfoMessageType InfoMessageType { get; }

        /// <summary>可见性条件成员名；为 <c>null</c> 表示始终显示。</summary>
        public string VisibleIfMemberName { get; }
    }

    /// <summary>
    /// 在字段上方画一条**可展开**的信息框：摘要一行，详情折起来。
    /// <para>
    /// 与 <see cref="InfoBoxAttribute"/> 的差别只在详情那一块——它适合
    /// 「一句话说不完，但平时又不想占地方」的说明。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>展开状态不跨会话持久化</b>：它存在属性的运行时状态里，域重载或重开 Unity 后
    /// 回到折叠状态。这是本包一贯的边界，理由见 README 的「已知限制」。
    /// 详情为空时只画摘要。
    /// </remarks>
    /// <example>
    /// <code>
    /// [DetailedInfoBox("伤害计算公式", "基础伤害 × (1 + 力量加成) × 暴击系数", InfoMessageType.None)]
    /// public float damage;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class DetailedInfoBoxAttribute : Attribute
    {
        /// <summary>
        /// 以摘要与详情构造。
        /// </summary>
        /// <param name="message">摘要文本，不得为空白。</param>
        /// <param name="details">详情文本；为空白时只画摘要。</param>
        /// <param name="infoMessageType">样式，默认 <see cref="InfoMessageType.Info"/>。</param>
        /// <param name="visibleIf">可选的可见性条件成员名；为 null 表示始终显示。</param>
        /// <exception cref="ArgumentException"><paramref name="message"/> 为 null、空串或仅含空白。</exception>
        public DetailedInfoBoxAttribute(
            string message,
            string details,
            InfoMessageType infoMessageType = InfoMessageType.Info,
            string visibleIf = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("信息框的摘要不能为空。", nameof(message));
            }

            Message = message;
            Details = details;
            InfoMessageType = infoMessageType;
            VisibleIf = visibleIf;
        }

        /// <summary>摘要文本。</summary>
        public string Message { get; }

        /// <summary>详情文本；为空白时只画摘要。</summary>
        public string Details { get; }

        /// <summary>信息框样式。</summary>
        public InfoMessageType InfoMessageType { get; }

        /// <summary>可见性条件成员名；为 <c>null</c> 表示始终显示。</summary>
        public string VisibleIf { get; }
    }
}
