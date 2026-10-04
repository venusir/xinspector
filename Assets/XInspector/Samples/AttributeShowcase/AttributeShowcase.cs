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
    }
}
