using System;

namespace XInspector.Internal
{
    /// <summary>
    /// 水平分组的**分数 → 像素**换算。
    /// <para>
    /// 纯 float 运算、零 Unity 依赖，因此这条最容易出错的规则跑在离线的
    /// <c>Tests.Native</c> 通道里（约 20 毫秒），不必起编辑器。
    /// </para>
    /// <para>
    /// 本类型为 <c>internal</c>；编辑器程序集经 <c>InternalsVisibleTo</c> 使用。
    /// </para>
    /// </summary>
    internal static class HorizontalGroupWeights
    {
        #region Public API

        /// <summary>
        /// 把每个格子的宽度分数换算成像素宽度。
        /// </summary>
        /// <param name="fractions">逐格的宽度分数（0–1）；非正或 NaN 视为「未指定」。</param>
        /// <param name="minWidths">逐格最小宽度（像素）；非正不限制。可为 <c>null</c>。</param>
        /// <param name="maxWidths">逐格最大宽度（像素）；非正不限制。可为 <c>null</c>。</param>
        /// <param name="availableWidth">整行可用宽度（像素，调用方扣好外边距与缩进）。</param>
        /// <param name="gap">格子之间的间距（像素）。</param>
        /// <returns>与 <paramref name="fractions"/> 等长的像素宽度；**0 表示不限制**（交给 GUILayout 伸展）。</returns>
        /// <remarks>
        /// <para>
        /// 规则：显式分数之和 ≤ 1 时，未指定的格子均分剩余；之和大于 1 时按总和等比缩放
        /// （确定性、不溢出）。可用宽度扣掉间距后不足时整行返回全 0——那时限制宽度只会更糟。
        /// </para>
        /// <para>
        /// 返回 0 而不是某个最小值是有意的：0 让调用方走「不加宽度约束」那条路，
        /// 于是窗口极窄时行为退化为普通的自动排布，而不是挤成一团。
        /// </para>
        /// </remarks>
        public static float[] Resolve(
            float[] fractions,
            float[] minWidths,
            float[] maxWidths,
            float availableWidth,
            float gap)
        {
            var count = fractions?.Length ?? 0;
            var widths = new float[count];

            if (count == 0)
            {
                return widths;
            }

            var totalGap = gap > 0f && count > 1 ? gap * (count - 1) : 0f;
            var usable = availableWidth - totalGap;

            if (usable <= 0f)
            {
                return widths;
            }

            var explicitSum = 0f;
            var implicitCount = 0;

            for (var i = 0; i < count; i++)
            {
                var fraction = fractions[i];
                if (IsExplicit(fraction))
                {
                    explicitSum += fraction;
                }
                else
                {
                    implicitCount++;
                }
            }

            for (var i = 0; i < count; i++)
            {
                var fraction = fractions[i];
                float width;

                if (IsExplicit(fraction))
                {
                    width = explicitSum > 1f
                        ? usable * (fraction / explicitSum)
                        : usable * fraction;
                }
                else if (implicitCount == 0 || explicitSum >= 1f)
                {
                    // 没有未指定的格子，或显式分数已占满：分不到宽度，交给 GUILayout。
                    width = 0f;
                }
                else
                {
                    width = usable * (1f - explicitSum) / implicitCount;
                }

                widths[i] = Clamp(width, minWidths, maxWidths, i);
            }

            return widths;
        }

        #endregion

        #region Private Helpers

        /// <summary>该分数是否算「显式指定」。</summary>
        /// <param name="fraction">宽度分数。</param>
        /// <returns>显式返回 <c>true</c>。</returns>
        private static bool IsExplicit(float fraction)
        {
            return fraction > 0f && !float.IsNaN(fraction);
        }

        /// <summary>按上下限夹一下某格的宽度。</summary>
        /// <param name="width">算出的宽度。</param>
        /// <param name="minWidths">逐格最小宽度。</param>
        /// <param name="maxWidths">逐格最大宽度。</param>
        /// <param name="index">格子下标。</param>
        /// <returns>夹过的宽度。</returns>
        private static float Clamp(float width, float[] minWidths, float[] maxWidths, int index)
        {
            if (minWidths != null && index < minWidths.Length && minWidths[index] > 0f)
            {
                width = Math.Max(width, minWidths[index]);
            }

            if (maxWidths != null && index < maxWidths.Length && maxWidths[index] > 0f)
            {
                width = Math.Min(width, maxWidths[index]);
            }

            return width;
        }

        #endregion
    }
}
