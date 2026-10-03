namespace XInspector
{
    /// <summary>
    /// 信息框的样式，对应 Unity 的 <c>MessageType</c>。
    /// <para>
    /// <b>为什么自己造一个：</b>Runtime 程序集不能用 <c>UnityEditor.MessageType</c>——
    /// 它零 Unity 依赖（由 <c>Tests.Native</c> 用纯 .NET 编译整份 Runtime 强制）。
    /// Odin 也是同样的理由自己造了一个，故本包与它同名。
    /// </para>
    /// </summary>
    public enum InfoMessageType
    {
        /// <summary>无图标、无底色。</summary>
        None = 0,

        /// <summary>信息（灰色）。</summary>
        Info = 1,

        /// <summary>警告（黄色）。</summary>
        Warning = 2,

        /// <summary>错误（红色）。</summary>
        Error = 3,
    }
}
