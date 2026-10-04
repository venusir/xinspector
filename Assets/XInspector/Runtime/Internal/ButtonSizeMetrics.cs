namespace XInspector.Internal
{
    /// <summary>
    /// <see cref="ButtonSizes"/> 到像素高度的换算。
    /// <para>
    /// <b>像素值以这里为准。</b> <see cref="ButtonSizes"/> 的文档注释里也写了数，但那只是给人看的
    /// ——真正的真相在本类的 <see cref="HeightOf"/>，两者不一致时改的是注释。放在 Runtime 侧是因为
    /// 这些数就是 <see cref="ButtonSizes"/> 的语义本身（换算是纯算术，不涉及任何 Unity 类型），
    /// 于是它能被离线的 <c>Tests.Native</c> 通道覆盖到。
    /// </para>
    /// </summary>
    internal static class ButtonSizeMetrics
    {
        #region Public API

        /// <summary>
        /// 取档位对应的像素高度。
        /// </summary>
        /// <param name="size">高度档位。</param>
        /// <returns>像素高度。</returns>
        public static float HeightOf(ButtonSizes size)
        {
            switch (size)
            {
                case ButtonSizes.Small:
                    return 20f;
                case ButtonSizes.Large:
                    return 30f;
                case ButtonSizes.Gigantic:
                    // 官方对它的描述是「两倍于 Large」，故 60 而不是 40。
                    return 60f;
                default:
                    return 25f;
            }
        }

        #endregion
    }
}
