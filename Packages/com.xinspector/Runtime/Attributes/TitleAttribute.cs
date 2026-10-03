using System;

namespace XInspector
{
    /// <summary>
    /// 为成员或类型加上标题。
    /// <para>
    /// 用在成员上时，标题紧贴该字段上方绘制；用在类型上时，标题出现在整个 Inspector 的最上方。
    /// 两种用法走的是**不同的管线**：成员级由绘制器直接处理，类级则由特性处理器读出来、
    /// 合成到根属性上——所以类级标题的实现不在绘制器里，见 <c>ClassLevelAttributeProcessor</c>。
    /// </para>
    /// <para>
    /// 标题是绘制器链上的普通一环：它把标题画完之后**调用链上的下一个绘制器**，
    /// 因此可以与 <see cref="BoxGroupAttribute"/> 等分组特性任意嵌套，互不知晓对方存在。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [Title("玩家档案")]
    /// public class PlayerProfile : MonoBehaviour
    /// {
    ///     [Title("身份", Subtitle = "只读展示")]
    ///     public string playerName;
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class TitleAttribute : Attribute
    {
        #region Public API

        /// <summary>
        /// 构造标题特性。
        /// </summary>
        /// <param name="title">标题文本，不得为空白。</param>
        /// <exception cref="ArgumentException"><paramref name="title"/> 为 null、空串或仅含空白。</exception>
        /// <remarks>
        /// 空白标题判为错误而非静默忽略：<c>[Title("")]</c> 几乎必然是笔误，
        /// 而它的表现是「什么都没画」——那是最难归因的一类现象。宁可在构建期明确报错。
        /// </remarks>
        public TitleAttribute(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("标题不能为空。", nameof(title));
            }

            Title = title;
        }

        /// <summary>
        /// 标题文本。
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 副标题文本。为 null 或空白时只画标题。
        /// </summary>
        public string Subtitle { get; set; }

        #endregion
    }
}
