using System;
using XInspector.Internal;

namespace XInspector
{
    /// <summary>
    /// 把成员分进**页签**：同组不同页签的成员分别落在各自的页里，一次只显示一页。
    /// <para>
    /// <c>[TabGroup("Tabs", "Tab1")]</c> 的分组路径是 <c>"Tabs/Tab1"</c>——
    /// **点分路径本身就是子分组机制**，因此构建期一行都不用改。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与 Odin 的**实现**差异（行为等价）：官方靠 <c>ISubGroupProviderAttribute</c>
    /// 让每个页签派生子分组，本包用点分路径天然表达，没有那条缝。
    /// 页签的顺序即子分组节点的顺序（按 <c>Order</c>，同权重按声明先后）。
    /// </para>
    /// <para>
    /// 选中页**不跨会话持久化**（存在属性树上），域重载后回到第一页。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [TabGroup("设置", "基础")]
    /// public int health;
    ///
    /// [TabGroup("设置", "高级")]
    /// public int debugLevel;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class TabGroupAttribute : PropertyGroupAttribute
    {
        #region Public API

        /// <summary>
        /// 不指定组名时使用的默认组名（照 Odin 的取值）。
        /// </summary>
        public const string DEFAULT_NAME = "_DefaultTabGroup";

        /// <summary>
        /// 默认分组构造：只给页签名，归入 <see cref="DEFAULT_NAME"/> 这一组。
        /// </summary>
        /// <param name="tab">页签名。</param>
        /// <param name="useFixedHeight">保留参数：本包的高度由内容决定，该值不产生行为。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="tab"/> 为 null、空白或不含有效段。</exception>
        public TabGroupAttribute(string tab, bool useFixedHeight = false, float order = 0f)
            : this(DEFAULT_NAME, tab, useFixedHeight, order)
        {
        }

        /// <summary>
        /// 以组名与页签名构造。
        /// </summary>
        /// <param name="group">组名（充当容器节点的路径）。</param>
        /// <param name="tab">页签名（容器下的子分组）。</param>
        /// <param name="useFixedHeight">保留参数：本包的高度由内容决定，该值不产生行为。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="group"/> 或 <paramref name="tab"/> 为 null、空白或不含有效段。</exception>
        public TabGroupAttribute(string group, string tab, bool useFixedHeight = false, float order = 0f)
            : base(Compose(group, tab), order)
        {
            // 路径恒为「组/页」两段以上，故父路径必有值；拆出来存着是为了让
            // CloneForPath 改写 GroupID 之后仍能认出「这是容器还是页」——
            // MemberwiseClone 会把这个字段原样带过去，而 GroupID 被改成容器路径。
            TabsGroupID = PropertyGroupPath.GetParentPath(GroupID);
            TabName = PropertyGroupPath.GetLeafName(GroupID);
            UseFixedHeight = useFixedHeight;
        }

        /// <summary>容器节点的路径（即 <see cref="PropertyGroupAttribute.GroupID"/> 的父路径）。</summary>
        public string TabsGroupID { get; }

        /// <summary>页签名（<see cref="PropertyGroupAttribute.GroupID"/> 的末段）。</summary>
        public string TabName { get; }

        /// <summary>
        /// 保留参数：本包的高度由内容决定，该值不产生行为
        /// （官方的固定高度模式是为滚动内容准备的，本包还没有那套布局）。
        /// </summary>
        public bool UseFixedHeight { get; set; }

        /// <summary>
        /// 只有一个页签时是否隐藏页签栏。默认 <c>false</c>（照常显示）。
        /// </summary>
        public bool HideTabGroupIfTabGroupOnlyHasOneTab { get; set; }

        #endregion

        #region Internal

        /// <summary>
        /// 本特性所在节点是不是**容器**（而不是页）。
        /// </summary>
        /// <remarks>
        /// 判定依据：容器的特性由 <see cref="PropertyGroupAttribute.CloneForPath"/>
        /// 改写路径而来，<see cref="GroupID"/> 被改成了容器路径，而
        /// <see cref="TabsGroupID"/> 因 MemberwiseClone 保持原值——两者相等即容器。
        /// </remarks>
        internal bool IsContainer => string.Equals(GroupID, TabsGroupID, StringComparison.Ordinal);

        #endregion

        #region Private Helpers

        /// <summary>把组名与页签名拼成节点路径（两段都先规范化）。</summary>
        /// <param name="group">组名。</param>
        /// <param name="tab">页签名。</param>
        /// <returns>形如 <c>"设置/基础"</c> 的路径。</returns>
        /// <exception cref="ArgumentException">任一段为 null、空白或不含有效段。</exception>
        private static string Compose(string group, string tab)
        {
            return PropertyGroupPath.Normalize(group) + PropertyGroupPath.Separator + PropertyGroupPath.Normalize(tab);
        }

        #endregion
    }
}
