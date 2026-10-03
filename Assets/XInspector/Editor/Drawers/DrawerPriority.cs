using System;

namespace XInspector.Editor
{
    /// <summary>
    /// 绘制器在链上的位置权重。**值越小越靠外层**（越早被调用，能包住后面的绘制器）。
    /// <para>
    /// 为什么需要一个数值而不是「先来后到」：链条的可组合性完全建立在顺序可预测之上。
    /// <c>[BoxGroup]</c> 能包住 <c>[Title]</c>、<c>[Title]</c> 能包住字段，靠的就是
    /// 「分组绘制器排在标题绘制器之前」。顺序一旦依赖偶然因素，组合就会时对时错。
    /// </para>
    /// <para>
    /// 排序规则是**升序**（小 = 外层），同值时按特性声明顺序。后者是稳定排序的兜底，
    /// 让同一属性上两个同优先级的绘制器有确定的先后。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 用 <see cref="double"/> 而非 <see cref="int"/>，是为了让使用方能在两个内置档位之间
    /// 插值（如 -50 落在 SuperPriority 与 AttributePriority 之间）而不必重新编号既有档位。
    /// </remarks>
    public readonly struct DrawerPriority : IComparable<DrawerPriority>, IEquatable<DrawerPriority>
    {
        #region Public API

        /// <summary>
        /// 最外层档位。用于需要包住一切的绘制器（如整页背景、全局禁用遮罩）。
        /// </summary>
        public static readonly DrawerPriority SuperPriority = new DrawerPriority(-1000d);

        /// <summary>
        /// 普通特性绘制器的默认档位。<see cref="XInspectorDrawer"/> 的默认值。
        /// </summary>
        public static readonly DrawerPriority AttributePriority = new DrawerPriority(-100d);

        /// <summary>
        /// 值绘制器档位。用于绘制「值本身」的绘制器，排在所有特性绘制器内侧。
        /// </summary>
        public static readonly DrawerPriority ValuePriority = new DrawerPriority(0d);

        /// <summary>
        /// 兜底档位。用于必须最后执行的绘制器。
        /// </summary>
        public static readonly DrawerPriority FallbackPriority = new DrawerPriority(double.MaxValue);

        /// <summary>
        /// 构造指定权重的优先级。
        /// </summary>
        /// <param name="value">权重，越小越靠外层。</param>
        public DrawerPriority(double value)
        {
            Value = value;
        }

        /// <summary>
        /// 权重值，越小越靠外层。
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// 按权重升序比较。
        /// </summary>
        /// <param name="other">另一个优先级。</param>
        /// <returns>本实例小于、等于或大于 <paramref name="other"/> 时，分别返回负数、零或正数。</returns>
        public int CompareTo(DrawerPriority other)
        {
            return Value.CompareTo(other.Value);
        }

        /// <summary>
        /// 按权重判等。
        /// </summary>
        /// <param name="other">另一个优先级。</param>
        /// <returns>权重相等返回 <c>true</c>。</returns>
        public bool Equals(DrawerPriority other)
        {
            return Value.Equals(other.Value);
        }

        /// <summary>
        /// 按权重判等。
        /// </summary>
        /// <param name="obj">比较对象。</param>
        /// <returns>为同类型且权重相等时返回 <c>true</c>。</returns>
        public override bool Equals(object obj)
        {
            return obj is DrawerPriority other && Equals(other);
        }

        /// <summary>
        /// 取权重值的哈希码。
        /// </summary>
        /// <returns>哈希码。</returns>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        /// <summary>
        /// 返回权重的可读形式。
        /// </summary>
        /// <returns>形如 <c>DrawerPriority(-100)</c> 的字符串。</returns>
        public override string ToString()
        {
            return $"DrawerPriority({Value})";
        }

        #endregion
    }
}
