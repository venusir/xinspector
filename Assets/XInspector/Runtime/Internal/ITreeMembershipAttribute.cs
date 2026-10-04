namespace XInspector.Internal
{
    /// <summary>
    /// 标记「**会产生属性树节点，但既不配绘制器也不配处理器**」的特性。
    /// <para>
    /// 前面两类特性各有一条现成的判据：绘制器驱动的、处理器驱动的。这一类两样都不是——
    /// 它的作用发生在**成员收集**那一步（<c>PropertyTreeBuilder</c> 决定要不要为这个成员建节点），
    /// 而收集是按具体特性类型查的，注册表里查不到它。
    /// </para>
    /// <para>
    /// <b>它存在是为了让判据不漏。</b> 没有它，<c>XInspectorUsageDetection</c> 的两张表都命中不了，
    /// 后果不是「少画了点东西」，而是**类型不被自动接管、特性一次都不生效、且没有任何告警**
    /// ——与当年条件族、生命周期钩子踩过的是同一个坑。
    /// </para>
    /// <para>
    /// 与 <see cref="ITreeLifecycleAttribute"/> 的区别：那个是「不产生节点」，
    /// 这个是「产生节点，但不画也不改别人的」。两者都不进注册表，理由相同。
    /// </para>
    /// </summary>
    internal interface ITreeMembershipAttribute
    {
    }
}
