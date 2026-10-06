using System;

namespace XInspector
{
    /// <summary>
    /// 集合**在 Inspector 里被改动**时，调用指定的方法。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 两个方法名分别是「改动施加**之前**」与「施加**之后**」的回调，都可以传 <c>null</c>
    /// 表示不要那一次。两者夹住的是「**写进序列化数据**」那一步——这正是官方说的
    /// 「through the inspector」。
    /// </para>
    /// <para>
    /// <b>回调触发时目标对象上的托管集合仍是旧的。</b> 绘制路径只改 <c>SerializedObject</c>
    /// 的内存副本，落盘由宿主在这一帧绘制结束后统一做——本包没有「落盘之后」的挂点。
    /// 读 <see cref="CollectionChangeInfo"/> 可以知道刚发生了什么，但**不要**在这时去读那个集合本身。
    /// </para>
    /// <para>
    /// <b>只覆盖 Inspector 里的改动。</b> 代码里直接改集合不触发；窗口工具栏的
    /// 「重置为默认值」也不触发（它整属性复制，不经过集合绘制器）。
    /// </para>
    /// <para>
    /// <b>回调方法的两种形状</b>：无参的 <c>()</c>，或 <c>(<see cref="CollectionChangeInfo"/>, object)</c>
    /// ——后者第二个参数就是 <see cref="CollectionChangeInfo.Value"/>。方法必须在本类型上
    /// （含继承链），名字只认字面量，不认 <c>$</c>／<c>@</c> 表达式。
    /// </para>
    /// <para>
    /// <b>两个回调都配不出可调用的方法时特性不生效</b>（Console 有一条告警说明原因）；
    /// 只配一个方向时另一个方向不触发。
    /// </para>
    /// <para>
    /// <b>成对与否的判据是「集合长度真的变了」。</b> 长度不可变的数组（固定缓冲那种）删不掉／加不进时，
    /// 改动前那一次已经触发、改动后**不会**——可以拿这条判断改动有没有真的发生。
    /// </para>
    /// <para>
    /// <b>多选时每个目标各调一次</b>（方法按目标解析），而列表的修改只施加一次；
    /// 窗口路径不记 Undo，Inspector 路径记（与 <c>[OnValueChanged]</c> 一致）。
    /// </para>
    /// <para>
    /// <b>副作用：</b> 字段上没有 <c>[ListDrawerSettings]</c> 时本包会**补一份**——
    /// 增删只有集合绘制器有落点，不补就「写了没反应」。代价是该字段的外观从 Unity 原生
    /// 变成本包自绘（与 <c>[TableList]</c> 同款）。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异：</b>只支持数组与 <c>List&lt;T&gt;</c>（字典、HashSet、Stack 等本包没有
    /// 对应容器）；不声明 <c>CollectionChangeType</c> 里本包不产生的取值。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [OnCollectionChanged(nameof(BeforeChange), nameof(AfterChange))]
    /// [ListDrawerSettings]
    /// public List&lt;int&gt; scores = new List&lt;int&gt;();
    ///
    /// private void BeforeChange(CollectionChangeInfo info, object value) { }
    /// private void AfterChange(CollectionChangeInfo info, object value) { }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class OnCollectionChangedAttribute : Attribute
    {
        #region Public API

        /// <summary>
        /// 以「改动前」「改动后」两个方法名构造；两者都可以传 <c>null</c>。
        /// </summary>
        /// <param name="before">改动施加**之前**要调用的方法名。</param>
        /// <param name="after">改动施加**之后**要调用的方法名。</param>
        /// <exception cref="ArgumentException">两个方法名都是 null 或空白。</exception>
        public OnCollectionChangedAttribute(string before = null, string after = null)
        {
            if (string.IsNullOrWhiteSpace(before) && string.IsNullOrWhiteSpace(after))
            {
                throw new ArgumentException(
                    "改动前与改动后至少要给一个方法名——两个都不给，这个特性就什么都不做。");
            }

            Before = string.IsNullOrWhiteSpace(before) ? null : before.Trim();
            After = string.IsNullOrWhiteSpace(after) ? null : after.Trim();
        }

        /// <summary>改动施加**之前**要调用的方法名；没给时为 <c>null</c>。</summary>
        public string Before { get; }

        /// <summary>改动施加**之后**要调用的方法名；没给时为 <c>null</c>。</summary>
        public string After { get; }

        #endregion
    }
}
