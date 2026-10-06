namespace XInspector
{
    /// <summary>
    /// 集合发生了一次什么样的改动。
    /// </summary>
    /// <remarks>
    /// <b>只声明本包真会产生的那几种。</b> Odin 的同名枚举本包**没有核到**成员表
    /// （官网文档页正文取不到），而「多声明一个永远不出现的取值」正是本包最忌讳的
    /// 「编译得过但什么都不发生」。日后真做了移动/排序再加。
    /// </remarks>
    public enum CollectionChangeType
    {
        /// <summary>末尾追加了一个元素。</summary>
        Add = 0,

        /// <summary>按下标移除了一个元素。</summary>
        RemoveAt = 1,
    }

    /// <summary>
    /// 一次集合改动的描述——连同 <c>[OnCollectionChanged]</c> 的回调一起交给你。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>它描述的是「序列化数据上的改动」。</b> 回调触发时，目标对象上的**托管集合仍是旧的**
    /// ——绘制路径只改 <c>SerializedObject</c> 的内存副本，落盘由宿主在这一帧绘制结束后统一做。
    /// 也就是说：读 <c>info</c> 可以知道刚发生了什么，但**不要**在这时去读那个集合本身。
    /// </para>
    /// <para>
    /// <b>只有 Inspector 里的改动才会产生它。</b> 代码里直接改集合不会触发回调——
    /// 这与官方那句「through the inspector」一致。窗口工具栏的「重置为默认值」也不触发
    /// （它整属性复制，不经过集合绘制器）。
    /// </para>
    /// </remarks>
    public readonly struct CollectionChangeInfo
    {
        #region Public API

        /// <summary>
        /// 构造一次变更描述。
        /// </summary>
        /// <param name="type">改动类型。</param>
        /// <param name="index">涉及的下标；<see cref="CollectionChangeType.Add"/> 时是**新元素的下标**。</param>
        /// <param name="value">
        /// 涉及的元素值。移除时是被移除的那个（**在删除之前读到**，删完就读不到了）；
        /// 追加时为 <c>null</c>——新元素是上一个元素的副本，报它的值等于报旧值，误导。
        /// </param>
        public CollectionChangeInfo(CollectionChangeType type, int index, object value)
        {
            Type = type;
            Index = index;
            Value = value;
        }

        /// <summary>改动类型。</summary>
        public CollectionChangeType Type { get; }

        /// <summary>
        /// 涉及的下标。
        /// </summary>
        /// <remarks>
        /// <see cref="CollectionChangeType.Add"/> 时是**新元素的下标**（即改动前的长度；
        /// 一次批量追加——如 <c>[AssetList]</c> 拖入多个资产——是**第一个**新元素的下标，
        /// 其余新元素紧跟在它后面），<see cref="CollectionChangeType.RemoveAt"/> 时是
        /// 被移除元素原来的下标。
        /// </remarks>
        public int Index { get; }

        /// <summary>
        /// 涉及的元素值；追加时为 <c>null</c>（理由见构造函数的说明）。
        /// </summary>
        /// <remarks>
        /// <b>装箱类型 = 元素的声明类型。</b> <c>List&lt;int&gt;</c> 的元素给 <c>int</c>、
        /// <c>List&lt;MyEnum&gt;</c> 给**枚举值本身**（不是下标，也不是底层数值）、
        /// <c>List&lt;GameObject&gt;</c> 给那个对象、向量与颜色给各自的值类型——
        /// 于是回调里一句 <c>(int)value</c> / <c>(MyEnum)value</c> 拿到的就是你以为的那个东西。
        /// 复合类型的元素读不出值（<c>null</c>），这时 Console 里有一条告警说明。
        /// </remarks>
        public object Value { get; }

        #endregion
    }
}
