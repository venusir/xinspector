using System;

namespace XInspector
{
    /// <summary>
    /// 校验成员「不为空」：空引用、空串、空集合都会在字段上方画一条提示框。
    /// <para>
    /// 只画提示、**不拦保存**——Unity 的序列化层没有「拒绝写入」这个位置，
    /// 硬拦只会变成悄悄改数据。要拦请在业务层校验。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>「空」的判定是本包自定的</b>（官方只说校验器是 <c>RequiredValidator&lt;T&gt; where T : class</c>，
    /// 即只覆盖引用类型）：null、空串、空集合算空；**纯空白串按非空**。
    /// 数值与 bool 这类值类型无从为空，标在它们上会告警一次。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Required]
    /// public string playerId;
    ///
    /// [Required("必须指定一个目标", InfoMessageType.Warning)]
    /// public Transform target;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class RequiredAttribute : Attribute
    {
        /// <summary>
        /// 以默认设置构造：错误级别，消息用本包的默认文本。
        /// </summary>
        public RequiredAttribute()
        {
            MessageType = InfoMessageType.Error;
        }

        /// <summary>
        /// 以消息级别构造，消息用默认文本。
        /// </summary>
        /// <param name="messageType">提示框的级别。</param>
        public RequiredAttribute(InfoMessageType messageType)
            : this()
        {
            MessageType = messageType;
        }

        /// <summary>
        /// 以自定义消息构造，级别为错误。
        /// </summary>
        /// <param name="errorMessage">自定义消息；<c>null</c> 表示用默认文本。</param>
        /// <exception cref="ArgumentException"><paramref name="errorMessage"/> 是空白串。</exception>
        public RequiredAttribute(string errorMessage)
            : this()
        {
            ErrorMessage = Validate(errorMessage);
        }

        /// <summary>
        /// 以自定义消息与级别构造。
        /// </summary>
        /// <param name="errorMessage">自定义消息；<c>null</c> 表示用默认文本。</param>
        /// <param name="messageType">提示框的级别。</param>
        /// <exception cref="ArgumentException"><paramref name="errorMessage"/> 是空白串。</exception>
        public RequiredAttribute(string errorMessage, InfoMessageType messageType)
            : this(messageType)
        {
            ErrorMessage = Validate(errorMessage);
        }

        /// <summary>
        /// 自定义消息；为 <c>null</c> 时使用本包的默认文本（「此字段为必填。」）。
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 提示框的级别，默认 <see cref="InfoMessageType.Error"/>。
        /// </summary>
        public InfoMessageType MessageType { get; }

        /// <summary>
        /// 校验自定义消息：空白串判为笔误（想用默认文本请传 null 或用无参构造）。
        /// </summary>
        /// <param name="errorMessage">自定义消息。</param>
        /// <returns>原样返回的消息。</returns>
        /// <exception cref="ArgumentException"><paramref name="errorMessage"/> 是空白串。</exception>
        private static string Validate(string errorMessage)
        {
            if (errorMessage != null && string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException(
                    "错误消息不能是空白串；想使用默认文本请传 null 或改用无参构造。", nameof(errorMessage));
            }

            return errorMessage;
        }
    }

    /// <summary>
    /// 把数值**钳制**到不小于给定值。
    /// <para>
    /// 钳制发生在**每次绘制之后**：控件里填了越界值，下一帧就会被拉回范围内。
    /// 官方只确认了「钳制」这件事（并注明脚本改的值不会被钳），**时机未说明**，本条是本包自定的。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 三种情形**跳过钳制**，理由都是「不悄悄改数据」：多对象编辑且各目标值不一致（没有单一
    /// 当前值）、字段当前只读（只读意味着这个值不归你改）、非数值类型（告警一次）。
    /// </remarks>
    /// <example>
    /// <code>
    /// [MinValue(0)]
    /// public int health;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class MinValueAttribute : Attribute
    {
        /// <summary>
        /// 以最小值构造。
        /// </summary>
        /// <param name="minValue">允许的最小值。</param>
        public MinValueAttribute(double minValue)
        {
            MinValue = minValue;
        }

        /// <summary>允许的最小值。</summary>
        public double MinValue { get; }
    }

    /// <summary>
    /// 把数值**钳制**到不大于给定值。语义与注意事项同 <see cref="MinValueAttribute"/>。
    /// </summary>
    /// <example>
    /// <code>
    /// [MaxValue(100f)]
    /// public float heat;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class MaxValueAttribute : Attribute
    {
        /// <summary>
        /// 以最大值构造。
        /// </summary>
        /// <param name="maxValue">允许的最大值。</param>
        public MaxValueAttribute(double maxValue)
        {
            MaxValue = maxValue;
        }

        /// <summary>允许的最大值。</summary>
        public double MaxValue { get; }
    }

    /// <summary>
    /// 校验对象引用指向**工程资产**（而不是场景对象）。
    /// <para>
    /// 指向场景对象时在字段上方画一条警告框。**只提示、不拦赋值**——
    /// 对象选择器不归本包管，拦住也无从谈起。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [AssetsOnly]
    /// public GameObject prefab;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class AssetsOnlyAttribute : Attribute
    {
    }

    /// <summary>
    /// 校验对象引用指向**场景对象**（而不是工程资产）。
    /// <para>
    /// 指向资产时在字段上方画一条警告框。语义与边界同 <see cref="AssetsOnlyAttribute"/>。
    /// 注意**预制体实例算场景对象**（它属于场景），拖预制体资产进来才会告警。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [SceneObjectsOnly]
    /// public Transform target;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class SceneObjectsOnlyAttribute : Attribute
    {
    }
}
