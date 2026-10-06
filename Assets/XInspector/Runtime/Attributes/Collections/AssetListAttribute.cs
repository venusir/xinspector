using System;

namespace XInspector
{
    /// <summary>
    /// 把数组 / 列表（**或单个 Unity 对象字段**）画成**资产列表**：每个元素一行，带缩略图、
    /// 原生对象字段与「−」；标题行有「+」与一个按类型过滤的「选择」按钮，整块接受**拖放**。
    /// <para>
    /// <b>两个形态都做</b>（官方原文：used on lists and arrays **and single elements of unity types**）：
    /// 列表 / 数组上是「替换默认的列表绘制器」；单个对象字段上是「预览 + 对象字段 + 选择按钮」。
    /// 官方明说这两者**行为不同**——只做单元素那半会得到一个语义随目标类型而变的半成品，
    /// 故本包两半一起做。
    /// </para>
    /// <para>
    /// 过滤是三种：元素 / 字段的**类型**（自动）、<see cref="Path"/>（目录）、
    /// <see cref="AssetNamePrefix"/>（资产名前缀）。类型那一道是**启发式收窄**——
    /// 正确性由落值前的类型校验保证（见下）。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>本包不做</b>（旋钮也相应地不声明——写了会**编译不过**，而不是静默无效）：
    /// <c>AutoPopulate</c>（官方语义是「被检视时填充列表」——那是绘制即搜工程 + 绘制即改数据，
    /// 与本包「<c>AssetDatabase</c> 只在事件路径」「绘制器只画、改动来自用户意图」两条硬规矩相悖）、
    /// <c>Tags</c> / <c>LayerNames</c>（它们说的是 GameObject 的标签与层，而 <c>AssetDatabase</c>
    /// 的 <c>l:</c> 是**资产标签**——没有忠实的对应物，用 <c>l:</c> 顶替等于换了个语义穿同一件
    /// 名字）、<c>CustomFilterMethod</c>（resolved string：本包只认序列化成员名，方法一律不做）。
    /// </para>
    /// <para>
    /// <b>三处本包自定语义</b>（见包 README 的差异清单）：拖放**去重**（已在列表里的不再加，
    /// 被拒会告警而不是静默）；**只收工程资产**（场景对象拖进来不是本特性的语义）；
    /// 类型过滤串只是**启发式收窄**——抽象类等可能一个都搜不到（菜单会空并告警），
    /// **真正保证正确性的是落值前那一道类型校验**。
    /// </para>
    /// <para>
    /// 与 <see cref="TableListAttribute"/> 同时标在**列表**上时本特性**让位**（构建期告警一次，
    /// 表格保持既有行为）；标在**单元素**上时两者互不影响（表格在那种字段上本来就只告警）。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [AssetList]
    /// public List&lt;Material&gt; materials;
    ///
    /// [AssetList(Path = "Assets/Art", AssetNamePrefix = "Rock")]
    /// public Texture2D[] rocks;
    ///
    /// [AssetList]
    /// public Texture2D single;   // 单元素形态：预览 + 对象字段 + 选择按钮
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class AssetListAttribute : Attribute
    {
        #region Public API

        /// <summary>
        /// 限定搜索的目录：<c>|</c> 分隔、**工程相对**（<c>Assets/</c> 开头）；空白表示整个工程。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="AssetSelectorAttribute.Paths"/> 同语义。**额外兼容官方样例的前导斜杠写法**
        /// （<c>Path = "/Plugins/Sirenix/"</c>）：逐段去掉开头的 <c>/</c>，若剩余部分不以
        /// <c>Assets/</c> 或 <c>Packages/</c> 开头则补 <c>Assets/</c>——这是本包对那句样例的解释，
        /// 写进了 README。
        /// </remarks>
        public string Path { get; set; }

        /// <summary>
        /// 只收**文件名**以它开头的资产（不含扩展名，大小写不敏感）；空白表示不过滤。
        /// </summary>
        /// <remarks>
        /// 本包自定语义：<c>AssetDatabase</c> 的搜索语法里没有名字前缀过滤器，
        /// 故这是取回路径后的一次纯函数过滤（只在菜单弹出 / 拖放这类**事件路径**上跑）。
        /// </remarks>
        public string AssetNamePrefix { get; set; }

        #endregion
    }
}
