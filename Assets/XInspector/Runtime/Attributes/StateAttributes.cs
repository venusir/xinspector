using System;

namespace XInspector
{
    /// <summary>
    /// 把成员画成**只读**：照常显示、照常占据位置，但不可编辑。
    /// <para>
    /// 与 <see cref="DisableIfAttribute"/> 的差别只在「要不要条件」：本特性恒只读，
    /// 后者按另一个成员的值决定。两者同时出现时**本特性赢**——恒只读比条件只读更具体，
    /// 处理器层为此显式排在条件族之后。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [ReadOnly]
    /// public int computedScore;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ReadOnlyAttribute : Attribute
    {
    }

    /// <summary>
    /// 用指定文本替换字段的默认标签。
    /// <para>
    /// 默认标签是字段名，写给人看时往往不够——<c>[LabelText("玩家生命")]</c> 比
    /// <c>health</c> 清楚。标签是构建期算好的，绘制期不做任何额外工作。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 与 <c>[HideLabel]</c> 同时出现时后者赢：它把标签整个撤掉，
    /// 换什么文本都不再有意义。
    /// </remarks>
    /// <example>
    /// <code>
    /// [LabelText("玩家生命")]
    /// public int health = 100;
    ///
    /// [LabelText("playerScore", NicifyText = true)]   // → "Player Score"
    /// public int playerScore;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class LabelTextAttribute : Attribute
    {
        /// <summary>
        /// 以标签文本构造。
        /// </summary>
        /// <param name="text">标签文本，不得为空白。</param>
        /// <exception cref="ArgumentException"><paramref name="text"/> 为 null、空串或仅含空白。</exception>
        /// <remarks>
        /// 空白文本判为错误而非静默忽略：<c>[LabelText("")]</c> 几乎必然是笔误，
        /// 而它的表现是「标签不见了」——那是最难归因的一类现象。想撤掉标签请用 <c>[HideLabel]</c>。
        /// </remarks>
        public LabelTextAttribute(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("标签文本不能为空。想撤掉标签请用 [HideLabel]。", nameof(text));
            }

            Text = text;
        }

        /// <summary>
        /// 以标签文本构造，并指定是否做「可读化」处理。
        /// </summary>
        /// <param name="text">标签文本，不得为空白。</param>
        /// <param name="nicifyText">是否把 <c>playerScore</c> 这样的名字转成 <c>Player Score</c>。</param>
        /// <exception cref="ArgumentException"><paramref name="text"/> 为 null、空串或仅含空白。</exception>
        public LabelTextAttribute(string text, bool nicifyText)
            : this(text)
        {
            NicifyText = nicifyText;
        }

        /// <summary>
        /// 标签文本。
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// 是否对文本做「可读化」处理（<c>playerScore</c> → <c>Player Score</c>），默认 <c>false</c>。
        /// </summary>
        public bool NicifyText { get; }
    }

    /// <summary>
    /// 给成员挂一条悬停提示。
    /// <para>
    /// 提示挂在**标签**上：鼠标停在字段名上时显示。这决定了它与另外两个标签类特性的关系——
    /// 提示跟着标签走，<see cref="LabelTextAttribute"/> 改文本、本特性加提示，两者可以共存；
    /// 而 <c>[HideLabel]</c> 把标签撤掉，提示也就无处可挂。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [PropertyTooltip("每秒恢复的生命值")]
    /// public float regenRate = 1f;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class PropertyTooltipAttribute : Attribute
    {
        /// <summary>
        /// 以提示文本构造。
        /// </summary>
        /// <param name="tooltip">提示文本，不得为空白。</param>
        /// <exception cref="ArgumentException"><paramref name="tooltip"/> 为 null、空串或仅含空白。</exception>
        public PropertyTooltipAttribute(string tooltip)
        {
            if (string.IsNullOrWhiteSpace(tooltip))
            {
                throw new ArgumentException("提示文本不能为空。", nameof(tooltip));
            }

            Tooltip = tooltip;
        }

        /// <summary>
        /// 提示文本。
        /// </summary>
        public string Tooltip { get; }
    }
}
