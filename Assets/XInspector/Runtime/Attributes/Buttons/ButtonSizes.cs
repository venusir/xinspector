namespace XInspector
{
    /// <summary>
    /// 按钮的高度档位。成员名取自 Odin 的 <c>ButtonSizes</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>像素值是本包自定值。</b> Odin 的数值只存在它的偏好设置里，官网核不到；
    /// 本包按「与属性行同高 → 逐级加高」定档，比例关系照官方对 <c>Gigantic</c> 的描述
    /// （「两倍于 Large」）。
    /// </para>
    /// <para>
    /// <b>默认成员排 0</b>（<see cref="Medium"/>）——本包的默认是它，
    /// 这样「没显式指定」与 <c>default</c> 是同一个值。
    /// </para>
    /// </remarks>
    public enum ButtonSizes
    {
        /// <summary>中号（25 像素）。本包默认。</summary>
        Medium = 0,

        /// <summary>小号（20 像素），与普通属性行同高。</summary>
        Small = 1,

        /// <summary>大号（30 像素）。</summary>
        Large = 2,

        /// <summary>特大号（60 像素）——官方说它「两倍于 <see cref="Large"/>」。</summary>
        Gigantic = 3,
    }
}
