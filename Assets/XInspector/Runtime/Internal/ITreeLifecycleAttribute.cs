namespace XInspector.Internal
{
    /// <summary>
    /// 标记「由属性树在特定时机调用」的特性（建树、释放、每次布局）。
    /// <para>
    /// <b>它存在是为了让判据不漏。</b> 这类特性不产生属性树节点、也不配绘制器，
    /// 于是两处按「有没有绘制器或处理器」判断的地方都会漏掉它们：
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <c>XInspectorUsageDetection</c>——类型不被自动接管的后果是**整个特性静默不生效**；
    /// </description></item>
    /// <item><description>
    /// 构建期的节点收集——漏了就不会被调用。
    /// </description></item>
    /// </list>
    /// <para>
    /// 用一个标记接口而不是在两个地方各维护一份类型清单：新增一个生命周期特性时，
    /// 「它算不算生命周期」这件事由它自己声明，不必回头改别处的名单。
    /// </para>
    /// </summary>
    internal interface ITreeLifecycleAttribute
    {
    }
}
