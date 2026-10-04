using System;

namespace XInspector
{
    /// <summary>
    /// 在字段**右侧**加一个小按钮，点击即调用指定的方法。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 方法必须**无参**，且写在 Inspector 正在检视的那个对象上（含继承链）——
    /// 与 <see cref="ButtonAttribute"/> 同一条边界：嵌套 <c>[Serializable]</c> 类里的方法不生效。
    /// 解析不到、同名有多个能匹配的方法时**构建期告警**，字段本身照常绘制（按钮不画）。
    /// </para>
    /// <para>
    /// 同一个字段可以标多次，按钮按声明顺序依次排在右侧——每次各调各的方法。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异：</b>不声明图标重载（随图标一族否决）、<c>ShowIf</c>（本包的条件要写成
    /// <c>[ShowIf]</c>，不藏在按钮特性里）、<c>ButtonColor</c>／<c>TextColor</c>（本包没有样式系统）。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [InlineButton("Randomize", "随机")]
    /// public int value;
    ///
    /// private void Randomize()
    /// {
    ///     value = UnityEngine.Random.Range(0, 100);
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class InlineButtonAttribute : Attribute
    {
        #region Public API

        /// <summary>
        /// 构造行内按钮。
        /// </summary>
        /// <param name="methodName">要点调用的方法名，须是无参方法。</param>
        /// <param name="label">按钮文本；<c>null</c> 或空白表示沿用方法名。</param>
        /// <exception cref="ArgumentException"><paramref name="methodName"/> 为 null 或空白。</exception>
        public InlineButtonAttribute(string methodName, string label = null)
        {
            if (string.IsNullOrWhiteSpace(methodName))
            {
                throw new ArgumentException("行内按钮要调用的方法名不能为空。", nameof(methodName));
            }

            MethodName = methodName.Trim();
            Label = label;
        }

        /// <summary>
        /// 要点调用的方法名，构造时已去除首尾空白。
        /// </summary>
        /// <remarks>
        /// Odin 把这个成员叫 <c>Action</c>；本包改名是因为它**只接受方法名**，
        /// 不接受 Odin 那套 <c>$</c>／<c>@</c> 表达式，叫 <c>Action</c> 会让人以为接受表达式。
        /// </remarks>
        public string MethodName { get; }

        /// <summary>
        /// 按钮文本；<c>null</c> 或空白表示沿用方法名。
        /// </summary>
        public string Label { get; }

        #endregion
    }
}
