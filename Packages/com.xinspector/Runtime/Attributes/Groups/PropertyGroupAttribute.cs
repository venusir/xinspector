using System;
using XInspector.Internal;

namespace XInspector
{
    /// <summary>
    /// 所有分组特性的公共基类。
    /// <para>
    /// 分组特性与普通特性的根本差别：普通特性作用于**它标注的那个成员**，
    /// 而分组特性会把它标注的成员**搬进一个分组节点**里。多个成员写上同一个
    /// <see cref="GroupID"/> 就会归并到同一个分组，于是「声明式的分组」不需要任何
    /// 集中登记——每个字段各自声明自己属于谁，构建期负责装配。
    /// </para>
    /// <para>
    /// <b>路径即嵌套。</b> <c>"Outer/Inner"</c> 表示 <c>Inner</c> 是 <c>Outer</c> 的子分组。
    /// 只声明深层路径也能工作：构建期会沿父路径上溯，把缺失的祖先节点合成出来。
    /// </para>
    /// <para>
    /// 本类及其子类**不参与绘制**。绘制是分组绘制器（如 <c>BoxGroupDrawer</c>）的职责；
    /// 特性只携带数据。这样同一套分组语义可以换不同的视觉呈现而互不影响。
    /// </para>
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class,
        AllowMultiple = true,
        Inherited = true)]
    public abstract class PropertyGroupAttribute : Attribute
    {
        #region Public API

        /// <summary>
        /// 构造分组特性。
        /// </summary>
        /// <param name="groupID">分组路径，如 <c>"Outer/Inner"</c>。构造时即规范化。</param>
        /// <param name="order">同层分组的排序权重，越小越靠前。</param>
        /// <exception cref="ArgumentException"><paramref name="groupID"/> 为 null、空白或不含有效段。</exception>
        protected PropertyGroupAttribute(string groupID, float order = 0f)
        {
            GroupID = PropertyGroupPath.Normalize(groupID);
            Order = order;
        }

        /// <summary>
        /// 分组路径，规范化后的完整形式（如 <c>"Outer/Inner"</c>）。
        /// <para>
        /// 这是分组的**身份**：构建期以它归并成员。规范化的意义就在于此——
        /// 同一逻辑分组若能写出两种形式，归并就会静默失败。
        /// </para>
        /// </summary>
        public string GroupID { get; protected set; }

        /// <summary>
        /// 分组显示名，即 <see cref="GroupID"/> 的末段。
        /// </summary>
        /// <remarks>
        /// 每次都从 <see cref="GroupID"/> 推导而非单独存储：这样它不可能与 GroupID 不一致。
        /// （若存成字段，<c>CloneForPath</c> 改写路径时就多一处要同步的地方。）
        /// </remarks>
        public string GroupName => PropertyGroupPath.GetLeafName(GroupID);

        /// <summary>
        /// 同层分组的排序权重，越小越靠前。默认 0。
        /// </summary>
        public float Order { get; set; }

        #endregion

        #region Internal

        /// <summary>
        /// 把另一个同 ID 的分组特性并入本实例。
        /// </summary>
        /// <param name="other">要并入的特性，与本实例 <see cref="GroupID"/> 相同。</param>
        /// <remarks>
        /// <para>
        /// 何时需要合并：同一个分组会被多个字段各声明一次，构建期归并成一个节点时
        /// 要把这些声明压成一份。本方法定义压的规则。
        /// </para>
        /// <para>
        /// <b>规则：先声明者优先。</b> <see cref="Order"/> 取先出现的非零值——
        /// 而不是累加。累加会让「给分组多加一个字段」意外改变该分组的排序位置，
        /// 那是个很难归因的副作用。
        /// </para>
        /// <para>
        /// 子类若新增可合并的字段，覆写本方法并先调用 <c>base.Combine</c>。
        /// 注意 C# 的限制：跨程序集覆写 <c>protected internal</c> 成员时，
        /// 覆写方必须声明为 <c>protected</c>（不能再写 <c>internal</c>）。
        /// </para>
        /// </remarks>
        protected internal virtual void Combine(PropertyGroupAttribute other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (Order == 0f)
            {
                Order = other.Order;
            }
        }

        /// <summary>
        /// 复制一份本特性，但把 <see cref="GroupID"/> 改写成指定路径。
        /// </summary>
        /// <param name="path">目标路径，须已规范化。</param>
        /// <returns>改写路径后的副本。</returns>
        /// <remarks>
        /// <para>
        /// 用途是**合成祖先节点**：某字段写了 <c>[BoxGroup("Outer/Inner")]</c> 却没有谁写
        /// <c>"Outer"</c>，构建期仍需造出一个 <c>Outer</c> 节点。它从最深那层的特性复制而来，
        /// 于是 <c>ShowLabel</c> 这类子类字段会一并保留——语义上「祖先继承后代的呈现设定」，
        /// 比凭空造一个默认特性的观感更一致。
        /// </para>
        /// <para>
        /// 用 <c>MemberwiseClone</c> 而非重新构造，正是为了不丢失子类字段——
        /// 基类无从知道自己有哪些子类字段。覆写者若要维护额外的引用型状态，
        /// 记得一并深拷贝。
        /// </para>
        /// </remarks>
        protected internal virtual PropertyGroupAttribute CloneForPath(string path)
        {
            var clone = (PropertyGroupAttribute)MemberwiseClone();
            clone.GroupID = path;
            return clone;
        }

        #endregion
    }
}
