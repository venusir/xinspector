using System;

namespace XInspector.Editor
{
    /// <summary>
    /// 分组节点交给**末端**（<see cref="ChildrenDrawer"/>）的子节点排布策略。
    /// <para>
    /// 为什么要有它：有些分组（水平分组、将来的页签）不是「画点东西、再调下一个」就完事，
    /// 它们要**改变内侧怎么画**——分行、只画选中页。若不改末端而在分组绘制器里自己驱动
    /// 子节点，就会跳过同节点上更内层的绘制器（多类型并存之后那是常规场景），
    /// 还会把「末端必画」这条结构性保证降级成分支逻辑。
    /// 于是改成一个显式的策略对象：**分组绘制器装它、末端读它**，
    /// 决策（分数换算、选页解析）仍留在可无头测试的纯函数里。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 存在分组节点的 <see cref="PropertyState"/> 上。数组**就地复用**（每帧只覆盖不新建，
    /// 绘制路径禁分配）；<see cref="HasPolicy"/> 由分组绘制器每帧重设，
    /// 因此不会出现「上一帧的策略残留」。
    /// </remarks>
    internal sealed class GroupChildrenLayout
    {
        #region Public API

        /// <summary>「不筛选子节点」的 <see cref="OnlyChildIndex"/> 取值。</summary>
        public const int AllChildren = -1;

        /// <summary>本帧是否装了策略；为 <c>false</c> 时末端走原路径（画全部）。</summary>
        public bool HasPolicy;

        /// <summary>只画这一个子节点（页签用）；<see cref="AllChildren"/> 表示不筛选。</summary>
        public int OnlyChildIndex = AllChildren;

        /// <summary>格子之间的间距（像素）。非正值不画。</summary>
        public float CellGap;

        /// <summary>逐格宽度（像素）；0 表示不限制（交给 GUILayout 伸展）。可为 <c>null</c>。</summary>
        public float[] CellWidths;

        /// <summary>逐格标签宽度（像素）；0 表示不调整。可为 <c>null</c>。</summary>
        public float[] CellLabelWidths;

        /// <summary>逐格行号；仅当 <see cref="RowsManagedByTerminal"/> 为真时有效。可为 <c>null</c>。</summary>
        public int[] CellRows;

        /// <summary>行数；仅当 <see cref="RowsManagedByTerminal"/> 为真时有效。</summary>
        public int RowCount;

        /// <summary>
        /// 行作用域是否**由末端自己开关**（多行排布用）。
        /// </summary>
        /// <remarks>
        /// 单行的分组（水平分组、按钮组）由分组绘制器开一个作用域包住全部子节点就够了；
        /// 多行则必须在画各格之间换作用域，而只有末端能插进那个位置——于是这面旗
        /// 把「谁来开作用域」的约定表达出来。分组绘制器每帧重设它。
        /// </remarks>
        public bool RowsManagedByTerminal;

        /// <summary>分数换算的暂存：逐格分数。就地复用，避免每帧分配。</summary>
        public float[] ScratchFractions;

        /// <summary>分数换算的暂存：逐格最小宽度。</summary>
        public float[] ScratchMinWidths;

        /// <summary>分数换算的暂存：逐格最大宽度。</summary>
        public float[] ScratchMaxWidths;

        /// <summary>
        /// 确保各数组长度与子节点数一致（就地复用，长度够时不重新分配）。
        /// </summary>
        /// <param name="count">子节点数。</param>
        public void EnsureCapacity(int count)
        {
            if (CellWidths == null || CellWidths.Length != count)
            {
                CellWidths = new float[count];
                CellLabelWidths = new float[count];
                CellRows = new int[count];
                ScratchFractions = new float[count];
                ScratchMinWidths = new float[count];
                ScratchMaxWidths = new float[count];
            }
        }

        #endregion
    }
}
