using System;

namespace XInspector
{
    /// <summary>
    /// 把成员归入一个带边框的分组。
    /// <para>
    /// 路径即嵌套：<c>[BoxGroup("Outer/Inner")]</c> 画出「盒子里的盒子」。
    /// 只声明深层路径也够——<c>Outer</c> 节点会被自动合成，不必再写一遍 <c>[BoxGroup("Outer")]</c>。
    /// </para>
    /// <para>
    /// 分组节点落在**其首个成员出现的位置**，因此夹在分组字段之间的未分组字段会留在原地，
    /// 而不是被挤到 Inspector 末尾。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [BoxGroup("基础")]
    /// public string playerName;
    ///
    /// [BoxGroup("基础/属性")]
    /// public int health;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class BoxGroupAttribute : PropertyGroupAttribute
    {
        #region Public API

        /// <summary>
        /// 构造分组特性。
        /// </summary>
        /// <param name="groupID">分组路径，如 <c>"Outer/Inner"</c>。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="groupID"/> 为 null、空白或不含有效段。</exception>
        public BoxGroupAttribute(string groupID, float order = 0f)
            : base(groupID, order)
        {
        }

        /// <summary>
        /// 分组标题的显示文本。为 null 或空白时使用 <see cref="PropertyGroupAttribute.GroupName"/>。
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// 是否绘制分组标题。默认 <c>true</c>；置 <c>false</c> 则只画边框不画标题。
        /// </summary>
        public bool ShowLabel { get; set; } = true;

        #endregion

        #region Internal

        /// <summary>
        /// 并入另一个同 ID 的分组声明。
        /// </summary>
        /// <param name="other">要并入的特性。</param>
        /// <remarks>
        /// <see cref="Label"/> 取先出现的非空值，<see cref="ShowLabel"/> 取先声明者的值——
        /// 与基类 <see cref="PropertyGroupAttribute.Order"/> 同一条规则：**先声明者优先**。
        /// 布尔量无法区分「未设置」与「显式设为默认值」，故不做「假值优先」之类的猜测。
        /// </remarks>
        protected internal override void Combine(PropertyGroupAttribute other)
        {
            base.Combine(other);

            if (other is BoxGroupAttribute box && string.IsNullOrWhiteSpace(Label))
            {
                Label = box.Label;
            }
        }

        #endregion
    }
}
