using System;

namespace XInspector
{
    /// <summary>
    /// 给这个字段（或它代表的类型）加一个搜索框，按输入**过滤它的子成员**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>匹配的是「标签」与「值」两样。</b> 子成员的字段名命中即显示；值命中同样显示
    /// （叶子按显示文本比，复合成员递归到它下面任意一层叶子）。大小写不敏感、按子串比，
    /// 空白查询等于不过滤。
    /// </para>
    /// <para>
    /// <b>容器命中有两种含义。</b> 分组自己的名字命中时**整组原样显示**（不然会得到一个空框）；
    /// 只有后代命中时，那一组自己画、组内成员逐个再筛。
    /// </para>
    /// <para>
    /// <b>标在数组/List 上时过滤的是「行」</b>：行的标签是 <c>Element 3</c> 这种索引名，
    /// 拿它当匹配素材等于搜不着东西，故那一侧只按**值**匹配（表格按任一单元格的值）。
    /// <c>−</c> 与索引标签用的仍是**真实下标**，<c>+</c> 永远追加到末尾。
    /// </para>
    /// <para>
    /// <b>过滤结果在查询变化时重算</b>（绘制路径只查表，不做反射与分配）。因此搜索框里
    /// 没动的时候改字段值，不会立刻让某一行冒出来——动一下查询即刷新。
    /// </para>
    /// <para>
    /// <b>副作用：</b> 字段上没有 <c>[ListDrawerSettings]</c> / <c>[TableList]</c> 时本包会**补一份**
    /// （否则整份列表仍由 Unity 原生绘制，搜索无从谈起）。代价是该字段的外观从原生变成本包自绘；
    /// 标在复合字段上时同理，它会从「整份交给 Unity」变成**本包的折叠头 + 缩进**——
    /// 也就是说这个类型会真正进管线，里面的特性随之生效。
    /// </para>
    /// <para>
    /// <b>本轮不做：</b> 标在**类型上**的形态（本包的类级特性只对被检视的最外层类型收集，
    /// 那是另一条通道）；<c>Recursive</c> 与 <c>FilterOptions</c> / <c>ISearchFilterable</c>
    /// （官网未核到，本包不声明）；集合的**元素节点化**（元素仍由原生绘制器画）。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Searchable]
    /// public List&lt;Item&gt; items = new List&lt;Item&gt;();
    ///
    /// [Searchable]
    /// public Stats stats = new Stats();
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class SearchableAttribute : Attribute
    {
    }
}
