using System;
using System.Text;

namespace XInspector.Internal
{
    /// <summary>
    /// 分组路径（GroupID）的解析与规范化。
    /// <para>
    /// 点分路径是 XInspector 表达分组嵌套的唯一手段：<c>"Outer/Inner"</c> 表示
    /// <c>Inner</c> 是 <c>Outer</c> 的子分组。本类是这套惯例的**唯一真相来源**——
    /// 分组特性、构建期分组装配、绘制器都只经它来理解路径，不各自实现一遍解析。
    /// </para>
    /// <para>
    /// 路径一旦规范化就不应再变：构建期会用它做字典键来归并同组分成员，
    /// 若同一逻辑分组能产生两种写法，归并就会失败。
    /// </para>
    /// <para>
    /// 本类型为 <c>internal</c>；编辑器程序集经 <c>InternalsVisibleTo</c> 使用。
    /// </para>
    /// </summary>
    internal static class PropertyGroupPath
    {
        #region Public API

        /// <summary>
        /// 路径分隔符。
        /// </summary>
        public const char Separator = '/';

        /// <summary>
        /// 规范化分组路径：逐段去除首尾空白、丢弃空段、以 <see cref="Separator"/> 重新连接。
        /// </summary>
        /// <param name="path">原始路径，如 <c>" Outer / Inner "</c>。</param>
        /// <returns>规范化后的路径，如 <c>"Outer/Inner"</c>。</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="path"/> 为 null、空白，或不含任何有效段（如 <c>""</c>、<c>"/"</c>、<c>"//"</c>）。
        /// </exception>
        /// <remarks>
        /// 空段被丢弃而非报错，是为了容忍 <c>"Outer//Inner"</c>、<c>"Outer/"</c> 这类手写笔误——
        /// 它们的意图没有歧义。但**整条路径不含有效段**属于笔误到无法猜测意图，故报错而不是静默返回空串。
        /// </remarks>
        public static string Normalize(string path)
        {
            if (path == null)
            {
                throw new ArgumentException("分组路径不能为 null。", nameof(path));
            }

            var segments = path.Split(Separator);
            var builder = new StringBuilder(path.Length);

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i].Trim();
                if (segment.Length == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(Separator);
                }

                builder.Append(segment);
            }

            if (builder.Length == 0)
            {
                throw new ArgumentException($"分组路径 \"{path}\" 不含任何有效段。", nameof(path));
            }

            return builder.ToString();
        }

        /// <summary>
        /// 取路径的末段，即分组的显示名。
        /// </summary>
        /// <param name="path">**已规范化**的路径。</param>
        /// <returns>末段，如 <c>"Outer/Inner"</c> 返回 <c>"Inner"</c>。</returns>
        public static string GetLeafName(string path)
        {
            var index = path.LastIndexOf(Separator);
            return index < 0 ? path : path.Substring(index + 1);
        }

        /// <summary>
        /// 取父路径。
        /// </summary>
        /// <param name="path">**已规范化**的路径。</param>
        /// <returns>
        /// 父路径，如 <c>"Outer/Inner"</c> 返回 <c>"Outer"</c>；
        /// 顶层路径（如 <c>"Outer"</c>）无父，返回 <c>null</c>。
        /// </returns>
        /// <remarks>
        /// 构建期沿本方法逐级上溯即可合成缺失的祖先分组节点，
        /// 因此无需另提供「取全部前缀」的 API——那会为每条路径多分配一个数组。
        /// </remarks>
        public static string GetParentPath(string path)
        {
            var index = path.LastIndexOf(Separator);
            return index < 0 ? null : path.Substring(0, index);
        }

        #endregion
    }
}
