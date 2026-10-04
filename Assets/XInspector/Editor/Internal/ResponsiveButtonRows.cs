namespace XInspector.Editor
{
    /// <summary>
    /// 响应式按钮带的**分行**：按每格宽度贪心装行。
    /// <para>
    /// 抽成纯函数是为了能无头测试——IMGUI 的分行结果量不了，但「哪一格在第几行」
    /// 是纯粹的算术，而这正是最容易写错的部分（首格的间距、超宽格独占一行、
    /// 等宽模式下用谁当基准）。
    /// </para>
    /// </summary>
    internal static class ResponsiveButtonRows
    {
        #region Public API

        /// <summary>
        /// 算出每格的行号，必要时把宽度统一。
        /// </summary>
        /// <param name="widths">逐格宽度（像素），**会被就地改写**：等宽模式下统一成最宽的那一格。</param>
        /// <param name="count">格子数。</param>
        /// <param name="gap">格间距（像素）。</param>
        /// <param name="available">本行可用宽度（像素）。</param>
        /// <param name="uniform">是否等宽。</param>
        /// <param name="rows">逐格行号的输出数组，长度不小于 <paramref name="count"/>。</param>
        /// <returns>行数（至少 1）。</returns>
        /// <remarks>
        /// 超宽的格子**独占一行**而不是被截断：截断会让按钮上的字被切掉，
        /// 那比换行难看得多，而且用户看不出是被布局还是被文案弄坏的。
        /// </remarks>
        public static int Resolve(float[] widths, int count, float gap, float available, bool uniform, int[] rows)
        {
            if (count <= 0)
            {
                return 1;
            }

            if (uniform)
            {
                var widest = 0f;
                for (var i = 0; i < count; i++)
                {
                    if (widths[i] > widest)
                    {
                        widest = widths[i];
                    }
                }

                for (var i = 0; i < count; i++)
                {
                    widths[i] = widest;
                }
            }

            var row = 0;
            var used = 0f;

            for (var i = 0; i < count; i++)
            {
                var width = widths[i];

                if (i > 0)
                {
                    if (used + gap + width > available)
                    {
                        row++;
                        used = 0f;
                    }
                    else
                    {
                        used += gap;
                    }
                }

                rows[i] = row;
                used += width;
            }

            return row + 1;
        }

        #endregion
    }
}
