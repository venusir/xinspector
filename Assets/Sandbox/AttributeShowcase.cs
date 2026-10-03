using UnityEngine;
using XInspector;

namespace XInspector.Sandbox
{
    /// <summary>
    /// L1a 特性展示台：本轮补的每个特性都在这里放一个，便于在场景里逐条目视。
    /// <para>
    /// **属于工程壳，不随包发布。**
    /// </para>
    /// <para>
    /// 与 <see cref="AttributeDemo"/> 的分工：那个演示**分组与标题**（架构的垂直切片），
    /// 这个演示**逐个特性本身**，字段按提交批次成组，一组一个 <c>#region</c>。
    /// 新增特性时在这里加一行——这是 CLAUDE.md「如何新增一个特性」的第 5 步。
    /// </para>
    /// </summary>
    [Title("L1a 特性展示", Subtitle = "逐个特性，按批次分组")]
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
    }
}
