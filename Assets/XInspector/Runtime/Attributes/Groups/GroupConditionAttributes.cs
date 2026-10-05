using System;
using XInspector.Internal;

namespace XInspector
{
    /// <summary>
    /// 分组条件特性的公共基类：按一个条件开关**整个分组**。
    /// <para>
    /// 与 <see cref="ShowIfAttribute"/> 一族的根本差别在判据挂在**分组节点**上：条件为假时
    /// 整个组连同子成员都不出现（而不是逐个成员各判一次）。条件成员名默认取**分组路径的
    /// 末段**——组名兼条件名，<c>[ShowIfGroup("Box/Toggle")]</c> 的条件就是成员 <c>Toggle</c>。
    /// </para>
    /// <para>
    /// 本类及其子类**不画任何东西**：分组节点只做条件载体，想让组有框，用同一个路径再配一个
    /// <see cref="BoxGroupAttribute"/>（官方样例全是这么配的）。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>条件只挂在「声明的那一节」上。</b> 只声明深层路径也能工作（构建期会沿父路径上溯，
    /// 合成缺失的祖先节点），但祖先节点**不**继承条件——否则同祖先下的兄弟分组会被一起藏掉。
    /// 路径被类级分组改写（加前缀）时，条件跟着改写后的那一节走。
    /// </para>
    /// <para>
    /// <b>与 Odin 的三处偏差</b>（理由与 <c>ShowIf</c> 一族的收窄同款，见包 README 的已知限制）：
    /// 值比较（<c>Value</c>）不做——本包 <see cref="ShowIfAttribute"/> 也没有；
    /// <c>Animate</c> 不做——本包没有动画系统；<c>CombineValuesWith</c> 不做——同路径多次声明
    /// 走 <see cref="Combine"/>，先声明者优先。
    /// </para>
    /// </remarks>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class,
        AllowMultiple = true,
        Inherited = true)]
    public abstract class GroupConditionAttribute : PropertyGroupAttribute
    {
        #region Public API

        /// <summary>
        /// 构造分组条件特性。
        /// </summary>
        /// <param name="groupID">分组路径，如 <c>"Box/Toggle"</c>。构造时即规范化。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="groupID"/> 为 null、空白或不含有效段。</exception>
        protected GroupConditionAttribute(string groupID, float order = 0f)
            : base(groupID, order)
        {
            DeclaredPath = GroupID;
            Condition = PropertyGroupPath.GetLeafName(GroupID);
        }

        /// <summary>
        /// 条件成员名。默认是分组路径的末段（组名兼条件名），显式赋值可覆盖。
        /// </summary>
        /// <remarks>
        /// 名字与 <see cref="ShowIfAttribute"/> 的条件同款：可以是 <c>a/b</c> 这样的嵌套路径，
        /// 按「序列化成员 → 普通字段/属性 → 无参返回 bool 的方法」三级解析；
        /// 解析失败时**保持可见**并记一条告警，不抛异常。
        /// </remarks>
        public string Condition { get; set; }

        #endregion

        #region Internal

        /// <summary>
        /// 声明时的那条路径。<see cref="PropertyGroupAttribute.CloneForPath"/> 只在改写
        /// （类级分组加前缀）时跟着走，**祖先合成时不改**——见该方法的说明。
        /// </summary>
        internal string DeclaredPath { get; private set; }

        /// <summary>
        /// 条件是否挂在**本节点**上（节点路径与声明路径相等）。
        /// </summary>
        /// <remarks>
        /// 为假只说明「本节点是构建期合成的祖先」，不代表特性没用——判据要落到声明那一节上。
        /// </remarks>
        internal bool AppliesToOwnNode => string.Equals(DeclaredPath, GroupID, StringComparison.Ordinal);

        #endregion

        #region Clone and Merge

        /// <summary>
        /// 复制一份本特性，但把路径改写成指定路径；**祖先合成**时不把条件带过去。
        /// </summary>
        /// <param name="path">目标路径，须已规范化。</param>
        /// <returns>改写路径后的副本。</returns>
        /// <remarks>
        /// <para>
        /// 基类的语义是「祖先继承后代的呈现设定」（<c>ShowLabel</c> 之类随克隆上溯），
        /// 对条件不成立：<c>[ShowIfGroup("Box/Toggle")]</c> 会合成出 <c>Box</c> 与
        /// <c>Box/Toggle</c> 两个节点，若祖先也挂条件，同祖先下的兄弟分组
        /// （如 <c>[BoxGroup("Box/Other")]</c>）会被一起藏掉。
        /// </para>
        /// <para>
        /// 判据：目标路径是当前路径的**祖先**，即视为祖先合成（构建期合成祖先时只会沿路径
        /// 逐级上溯地调用本方法）——此时保留 <see cref="DeclaredPath"/> 不动，
        /// <see cref="AppliesToOwnNode"/> 于是恒为假；其余调用（包括类级分组把路径加前缀的
        /// 改写）都把新路径认作声明路径，条件跟着它走。
        /// </para>
        /// </remarks>
        protected internal override PropertyGroupAttribute CloneForPath(string path)
        {
            var clone = (GroupConditionAttribute)base.CloneForPath(path);

            if (!IsAncestorPath(path, GroupID))
            {
                clone.DeclaredPath = path;
            }

            return clone;
        }

        /// <summary>
        /// 并入同路径的另一份声明：**真正声明在本路径上的那份赢**。
        /// </summary>
        /// <param name="other">要并入的特性，与本实例路径相同。</param>
        /// <remarks>
        /// 基类的「先声明者优先」在这里不够用：节点可能先由更深声明的**祖先克隆**创建
        /// （那份不带生效的条件），后由真正声明在该路径上的成员归并——先到者恰恰是没条件的
        /// 那份。两份都生效（都声明在本路径）或都不生效时仍按基类规则取先到者。
        /// </remarks>
        protected internal override void Combine(PropertyGroupAttribute other)
        {
            base.Combine(other);

            if (other is GroupConditionAttribute incoming && !AppliesToOwnNode && incoming.AppliesToOwnNode)
            {
                DeclaredPath = incoming.DeclaredPath;
                Condition = incoming.Condition;
            }
        }

        /// <summary>
        /// <paramref name="candidate"/> 是否为 <paramref name="path"/> 的祖先（逐段上溯比较）。
        /// </summary>
        /// <param name="candidate">候选祖先路径。</param>
        /// <param name="path">起点路径，须已规范化。</param>
        /// <returns>是祖先返回 <c>true</c>。</returns>
        private static bool IsAncestorPath(string candidate, string path)
        {
            var current = PropertyGroupPath.GetParentPath(path);

            while (current != null)
            {
                if (string.Equals(current, candidate, StringComparison.Ordinal))
                {
                    return true;
                }

                current = PropertyGroupPath.GetParentPath(current);
            }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// 条件为真时才显示**整个分组**。
    /// </summary>
    /// <remarks>
    /// 条件名默认取分组路径的末段（<c>[ShowIfGroup("Box/Toggle")]</c> 判的是成员
    /// <c>Toggle</c>），可用 <c>Condition</c> 覆盖。条件成员名不存在或类型不对时，
    /// 整组**保持可见**并记一条告警。
    /// </remarks>
    /// <example>
    /// <code>
    /// public bool showDetails = true;
    ///
    /// [ShowIfGroup("showDetails")]
    /// [BoxGroup("showDetails/细节")]
    /// public int health;
    ///
    /// // 组名与条件名不同时，显式覆盖：
    /// [ShowIfGroup("战斗组", Condition = nameof(showDetails))]
    /// [BoxGroup("战斗组/属性")]
    /// public int attack;
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class ShowIfGroupAttribute : GroupConditionAttribute
    {
        /// <summary>
        /// 以分组路径构造。
        /// </summary>
        /// <param name="groupID">分组路径，如 <c>"Box/Toggle"</c>。条件名默认取其末段。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="groupID"/> 为 null、空白或不含有效段。</exception>
        public ShowIfGroupAttribute(string groupID, float order = 0f)
            : base(groupID, order)
        {
        }
    }

    /// <summary>
    /// 条件为真时隐藏**整个分组**。见 <see cref="ShowIfGroupAttribute"/> 的完整说明。
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class,
        AllowMultiple = true,
        Inherited = true)]
    public sealed class HideIfGroupAttribute : GroupConditionAttribute
    {
        /// <summary>
        /// 以分组路径构造。
        /// </summary>
        /// <param name="groupID">分组路径，如 <c>"Box/Debug"</c>。条件名默认取其末段。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="groupID"/> 为 null、空白或不含有效段。</exception>
        public HideIfGroupAttribute(string groupID, float order = 0f)
            : base(groupID, order)
        {
        }
    }
}
