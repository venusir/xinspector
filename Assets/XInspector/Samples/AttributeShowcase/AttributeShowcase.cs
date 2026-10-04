using System;
using UnityEngine;
using XInspector;

namespace XInspector.Samples
{
    /// <summary>
    /// 逐特性展示台：每个特性在这里放一个，便于逐条目视。
    /// <para>
    /// 与 <see cref="OverviewComponent"/> 的分工：那个演示**最小可用形态**
    /// （类级标题 + 嵌套分组，架构的垂直切片），这个演示**逐个特性本身**，
    /// 字段按批次成组，一组一个 <c>#region</c>。
    /// </para>
    /// <para>
    /// 新增特性时在这里加一行——这是 CLAUDE.md「如何新增一个特性」的第 5 步。
    /// </para>
    /// </summary>
    [Title("XInspector 特性展示", Subtitle = "逐个特性，按批次分组")]
    [TypeInfoBox("这条信息框来自类级 [TypeInfoBox]——与 [Title] 同一机制：类级特性落在根节点，根节点的链照样包住子节点。")]
    [HideMonoScript]
    public class AttributeShowcase : MonoBehaviour
    {
        #region 状态与标签

        /// <summary>恒只读：照常显示，不可编辑。</summary>
        [ReadOnly]
        public int computedScore = 42;

        /// <summary>标签文本被替换成中文。</summary>
        [LabelText("玩家生命")]
        public int health = 100;

        /// <summary>标签文本做可读化：<c>playerScore</c> → <c>Player Score</c>。</summary>
        [LabelText("playerScore", true)]
        public int playerScore = 7;

        /// <summary>悬停标签时显示提示。</summary>
        [PropertyTooltip("每秒恢复的生命值")]
        public float regenRate = 1f;

        /// <summary>文本与提示并存——两者各管一段，互不覆盖。</summary>
        [LabelText("法力")]
        [PropertyTooltip("施放技能消耗的值")]
        public float mana = 10f;

        #endregion

        #region 布局与外观

        /// <summary>给整块染一层暖色。（用在类上会染整页——同一个绘制器落在根节点上而已。）</summary>
        [GUIColor(1f, 0.85f, 0.6f)]
        public int tinted = 1;

        /// <summary>缩进一级。</summary>
        [Indent]
        public int indented = 2;

        /// <summary>前置 12 像素的间距。</summary>
        [PropertySpace(12f)]
        public int spaced = 3;

        /// <summary>标签占 200 像素宽。</summary>
        [LabelWidth(200f)]
        public string wideLabel = "宽标签的值";

        /// <summary>撤掉标签，值占满整行。</summary>
        [HideLabel]
        public string noLabel = "撤掉标签后值会占满整行";

        /// <summary>值控件右侧画后缀。</summary>
        [SuffixLabel("秒")]
        public float duration = 1.5f;

        /// <summary>后缀叠在控件上。</summary>
        [SuffixLabel("×100%", true)]
        public float ratio = 0.75f;

        #endregion

        #region 值绘制

        /// <summary>值画成只读文本（可选中复制），不带可编辑控件。</summary>
        [DisplayAsString]
        public int computedId = 20261004;

        /// <summary>文本允许溢出：长文本折行显示全，而不是裁成一行。</summary>
        [DisplayAsString(true)]
        public string longDescription = "这一行刻意写得比较长，用来对比「裁到一行」与「折行显示全」两种模式在展示台里的差别。";

        /// <summary>bool 画成「开关在左、标签在右」——比 Unity 默认的排布更贴近一列复选框的读法。</summary>
        [ToggleLeft]
        public bool enableTracing = true;

        /// <summary>数值画成可拖动的进度条（点击/拖动条子改值）。</summary>
        [ProgressBar(0, 100)]
        public float stamina = 65f;

        /// <summary>进度条：自定义填充色 + 四段刻度 + 显示数值文本。</summary>
        [ProgressBar(0f, 1f, 0.9f, 0.45f, 0.2f, Segmented = true)]
        public float charge = 0.5f;

        /// <summary>枚举画成一排按钮（单选），而不是下拉框。</summary>
        [EnumToggleButtons]
        public DamageType damageType = DamageType.Fire;

        /// <summary><c>[Flags]</c> 枚举逐位多选。</summary>
        [EnumToggleButtons]
        public StatusFlags status = StatusFlags.Poisoned;

        /// <summary>5 行文本域（行数可调，标签画在文本域上方）。</summary>
        [MultiLineProperty(5)]
        public string notes = "多行文本示例：\n第二行。";

        /// <summary>延迟提交：输入过程中不写回，回车或失焦才提交。</summary>
        [DelayedProperty]
        public string searchFilter = "";

        /// <summary>枚举下拉框 + 前后翻页按钮（末尾自动绕回开头）。</summary>
        [EnumPaging]
        public DamageType pagedType = DamageType.Physical;

        /// <summary>滑块——只换控件，**不钳数据**（要钳请配 [MinValue]/[MaxValue]）。</summary>
        [PropertyRange(0, 100)]
        public float rangedValue = 30f;

        /// <summary>回绕：初始的 400 在绘制后被绕成 40（区间按半开处理）。</summary>
        [Wrap(0f, 360f)]
        public float angle = 400f;

        #endregion

        #region 校验与钳制

        /// <summary>空串 → 字段上方出现一条错误框（默认文本）。</summary>
        [Required]
        public string playerId = "";

        /// <summary>自定义消息与级别。</summary>
        [Required("必须填一个昵称", InfoMessageType.Warning)]
        public string nickname = "";

        /// <summary>纯空白串**不算**空——与上一条对比就是那条语义。</summary>
        [Required]
        public string spacesOnly = "   ";

        /// <summary>绘制后被抬到 0：初始值故意给了 -5（打开 Inspector 就会看到它被改掉）。</summary>
        [MinValue(0)]
        public int level = -5;

        /// <summary>绘制后被压到 100：初始值故意给了 150。</summary>
        [MaxValue(100f)]
        public float heat = 150f;

        /// <summary>拖一个**场景**物体进来就会看到警告（这里需要工程资产）。</summary>
        [AssetsOnly]
        public GameObject assetRef;

        /// <summary>拖一个**工程资产**进来就会看到警告（这里需要场景对象）。</summary>
        [SceneObjectsOnly]
        public GameObject sceneRef;

        #endregion

        #region 信息框

        /// <summary>恒显示的信息框。</summary>
        [InfoBox("这条信息框始终显示。", InfoMessageType.Info)]
        public int withInfoBox = 1;

        /// <summary>只有勾上开关才显示的信息框——字段本身照常绘制。</summary>
        [InfoBox("勾上开关才看得到这条警告。", InfoMessageType.Warning, nameof(showWarning))]
        public int conditionalInfoBox = 2;

        /// <summary>上一条信息框的显示条件。</summary>
        public bool showWarning;

        /// <summary>摘要一行，详情折起来。</summary>
        [DetailedInfoBox("伤害计算公式", "基础伤害 × (1 + 力量加成) × 暴击系数", InfoMessageType.None)]
        public float damage = 10f;

        #endregion

        #region 分组族

        /// <summary>竖直分组：只有容器与内边距，不画框。</summary>
        [VerticalGroup("竖列")]
        public int verticalA = 1;

        /// <summary>同分组的第二个成员——两者合进同一个容器。</summary>
        [VerticalGroup("竖列")]
        public int verticalB = 2;

        /// <summary>标题组：加粗标题 + 分隔线 + 副标题。</summary>
        [TitleGroup("标题组", "副标题画在标题下方")]
        public int titledValue = 3;

        /// <summary>折叠组：点三角收起；收起时组内内容**不画**（不是变灰）。</summary>
        [FoldoutGroup("折叠组", true)]
        public int foldoutValue = 4;

        /// <summary>折叠组的第二个成员（同组）。</summary>
        [FoldoutGroup("折叠组", true)]
        public int foldoutValue2 = 5;

        /// <summary>同路径两种分组：折叠在外、框在内——档位不是审美而是功能。</summary>
        [FoldoutGroup("折叠与框")]
        [BoxGroup("折叠与框")]
        public int foldoutOnBox = 6;

        /// <summary>标题组 + 框同路径：标题在框之外。</summary>
        [TitleGroup("标题与框")]
        [BoxGroup("标题与框")]
        public int titleOnBox = 7;

        #endregion

        #region 结构与门控

        /// <summary>只读 + 强制可编辑：EnableGUI 排在只读之后，它赢。</summary>
        [ReadOnly]
        [EnableGUI]
        public int forcedEditable = 3;

        /// <summary>交回 Unity 绘制：内侧的 [Indent] **不会运行**（本条更外且不调下一个）。</summary>
        [DrawWithUnity]
        [Indent]
        public int unityDrawn = 7;

        /// <summary>只允许引用本物体之下的子物体——拖别的物体进来会看到警告。</summary>
        [ChildGameObjectsOnly]
        public GameObject childRef;

        /// <summary>字段前的开关门控它能否编辑：关掉就变灰；开关本身**永远可点**。</summary>
        [Toggle("Enabled")]
        public ToggleableSettings toggleable;

        #endregion

        #region 调试

        /// <summary>把本字段的绘制器链摊开成一张表——展开后第 0 格就是它自己。</summary>
        [ShowDrawerChain]
        [Indent]
        [DisplayAsString]
        public int traced = 42;

        #endregion
    }

    /// <summary>展示 <c>[Toggle]</c> 用的设置块——开关在值对象内部。</summary>
    [Serializable]
    public struct ToggleableSettings
    {
        /// <summary>开关本身。</summary>
        public bool Enabled;

        /// <summary>被门控的值。</summary>
        public int value;
    }

    /// <summary>展示 <c>[EnumToggleButtons]</c> 用的普通枚举。</summary>
    public enum DamageType
    {
        /// <summary>物理。</summary>
        Physical,

        /// <summary>火焰。</summary>
        Fire,

        /// <summary>冰霜。</summary>
        Ice,
    }

    /// <summary>展示 <c>[EnumToggleButtons]</c> 用的位标志枚举。</summary>
    [Flags]
    public enum StatusFlags
    {
        /// <summary>无状态。</summary>
        None = 0,

        /// <summary>中毒。</summary>
        Poisoned = 1,

        /// <summary>燃烧。</summary>
        Burning = 2,

        /// <summary>冰冻。</summary>
        Frozen = 4,
    }
}
