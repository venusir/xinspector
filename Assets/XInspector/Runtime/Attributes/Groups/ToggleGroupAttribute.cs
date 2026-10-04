using System;

namespace XInspector
{
    /// <summary>
    /// 把成员归入一个**开关分组**：组标题前有一个复选框，关掉时组内内容不画。
    /// <para>
    /// 开关指向**同一个对象上**的 bool 成员（相对路径，可写 <c>"a/b"</c> 这样的嵌套路径）；
    /// 那个成员名同时充当分组 ID——**同组成员必须写同一个 bool 成员名**，不支持 <c>static</c>。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与 <see cref="ToggleAttribute"/> 的差别：那个门控**单个字段**（变灰，仍可见），
    /// 本特性门控**整个分组**（关掉时内容不画）。
    /// </para>
    /// <para>
    /// 开关的解析发生在**首次绘制**时（存在属性状态里）：分组节点在构建期的处理器阶段
    /// 还不存在，装不上去。这是本包唯一一处「绘制期解析」的例外，理由写在绘制器的注释里。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [ToggleGroup("showAdvanced", groupTitle: "高级选项")]
    /// public int debugLevel;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class ToggleGroupAttribute : PropertyGroupAttribute
    {
        #region Public API

        /// <summary>
        /// 以开关成员名构造。
        /// </summary>
        /// <param name="toggleMemberName">开关成员名（同时充当分组 ID），不得为空白。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <param name="groupTitle">组标题；为 null 或空白时用开关成员名。</param>
        /// <exception cref="ArgumentException"><paramref name="toggleMemberName"/> 为 null、空白或不含有效段。</exception>
        public ToggleGroupAttribute(string toggleMemberName, float order = 0f, string groupTitle = null)
            : base(toggleMemberName, order)
        {
            ToggleGroupTitle = groupTitle;
        }

        /// <summary>
        /// 以开关成员名与组标题构造。
        /// </summary>
        /// <param name="toggleMemberName">开关成员名（同时充当分组 ID），不得为空白。</param>
        /// <param name="groupTitle">组标题；为 null 或空白时用开关成员名。</param>
        /// <exception cref="ArgumentException"><paramref name="toggleMemberName"/> 为 null、空白或不含有效段。</exception>
        public ToggleGroupAttribute(string toggleMemberName, string groupTitle)
            : this(toggleMemberName, 0f, groupTitle)
        {
        }

        /// <summary>
        /// 开关成员名。**它就是分组 ID**（<see cref="PropertyGroupAttribute.GroupID"/>）——
        /// 同组成员写同一个名字才会归进同一组，这是 Odin 的语义，照搬。
        /// </summary>
        public string ToggleMemberName => GroupID;

        /// <summary>组标题；为 <c>null</c> 或空白时绘制期用开关成员名。</summary>
        /// <remarks>私有 setter 供 <see cref="Combine"/> 用（只读自动属性没法在合并里改）。</remarks>
        public string ToggleGroupTitle { get; private set; }

        /// <summary>
        /// 展开一个时是否收起其它。**只留字段、不做行为**——跨组协调没有明确语义
        /// （「其它」指哪些分组？），保留它只是为了让照着 Odin 写的调用代码编译得过。
        /// </summary>
        public bool CollapseOthersOnExpand { get; set; }

        #endregion

        #region Internal

        /// <summary>
        /// 并入另一个同 ID 的声明：标题取**先出现的非空值**（与 BoxGroup 的 Label 同一规则）。
        /// </summary>
        /// <param name="other">要并入的特性。</param>
        protected internal override void Combine(PropertyGroupAttribute other)
        {
            base.Combine(other);

            if (other is ToggleGroupAttribute toggle && string.IsNullOrWhiteSpace(ToggleGroupTitle))
            {
                ToggleGroupTitle = toggle.ToggleGroupTitle;
            }
        }

        #endregion
    }
}
