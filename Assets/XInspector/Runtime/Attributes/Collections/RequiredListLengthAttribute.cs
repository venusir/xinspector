using System;

namespace XInspector
{
    /// <summary>
    /// 限制集合的元素个数：少于下限或多于上限时在字段上方画一条提示。
    /// <para>
    /// 只读 <c>arraySize</c>——它不需要自绘列表，因此对数组与 <c>List&lt;T&gt;</c> 都成立。
    /// 多选且各目标的长度不一致时**跳过**（读到的不是任何一个目标的真值，报长度是错的）。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>与官方的差异：</b>官方还有四个用 <c>string</c> 表达式取上下限的构造（<c>"@this.SomeNumber"</c>），
    /// 本包不做——resolved string 是本包一贯砍掉的那一类；写了它们的代码会**编译不过**。
    /// 官方的 <c>PrefabKind</c> 属性同样不声明。
    /// </remarks>
    /// <example>
    /// <code>
    /// [RequiredListLength(3)]          // 恰好 3 项
    /// public string[] team;
    ///
    /// [RequiredListLength(1, 8)]       // 1 到 8 项
    /// public int[] upgrades;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class RequiredListLengthAttribute : Attribute
    {
        #region Public API

        /// <summary>
        /// 限定为**恰好**这么多项。
        /// </summary>
        /// <param name="length">要求的元素个数，不得为负。</param>
        /// <exception cref="ArgumentException"><paramref name="length"/> 为负。</exception>
        public RequiredListLengthAttribute(int length)
            : this(length, length)
        {
        }

        /// <summary>
        /// 限定上下限；两者都传 <c>null</c> 是笔误。
        /// </summary>
        /// <param name="minLength">下限；<c>null</c> 表示不限。</param>
        /// <param name="maxLength">上限；<c>null</c> 表示不限。</param>
        /// <exception cref="ArgumentException">
        /// 两者都为 <c>null</c>、下限为负、或上限小于下限——笔误应当当场炸，
        /// 而不是变成一个恒假的检查静默留在那里（与 <c>[Title("")]</c> 同一判据）。
        /// </exception>
        public RequiredListLengthAttribute(int? minLength, int? maxLength)
        {
            if (minLength == null && maxLength == null)
            {
                throw new ArgumentException("下限与上限不能都为 null——那样这条校验没有任何约束。");
            }

            if (minLength < 0)
            {
                throw new ArgumentException("下限不能为负。", nameof(minLength));
            }

            if (maxLength < 0)
            {
                throw new ArgumentException("上限不能为负。", nameof(maxLength));
            }

            if (minLength != null && maxLength != null && maxLength < minLength)
            {
                throw new ArgumentException("上限不能小于下限。", nameof(maxLength));
            }

            MinLength = minLength;
            MaxLength = maxLength;
        }

        /// <summary>元素个数下限；<c>null</c> 表示不限。</summary>
        public int? MinLength { get; }

        /// <summary>元素个数上限；<c>null</c> 表示不限。</summary>
        public int? MaxLength { get; }

        /// <summary>自定义消息文本。为 null 或空白时用本包生成的默认文案。</summary>
        public string ErrorMessage { get; set; }

        /// <summary>提示级别，默认 <see cref="InfoMessageType.Error"/>（与 <c>[Required]</c> 一致）。</summary>
        public InfoMessageType MessageType { get; set; } = InfoMessageType.Error;

        #endregion
    }
}
