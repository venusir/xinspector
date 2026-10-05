namespace XInspector.Internal
{
    /// <summary>
    /// 标记「**不产生节点、也不配绘制器与处理器，由构建期直接消费**」的特性——
    /// 目前只有会改变成员排列的 <see cref="PropertyOrderAttribute"/>。
    /// <para>
    /// <b>它存在是为了让判据不漏。</b> 这类特性既没有绘制器也没有处理器，于是两处判据都会漏掉它：
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <c>XInspectorUsageDetection</c>——类型不被自动接管的后果是**整个特性静默不生效**
    /// （排序看着毫无反应）；
    /// </description></item>
    /// <item><description>
    /// 构建期的成员收集——排的是收集之后那份列表，收集环节不认识它。
    /// </description></item>
    /// </list>
    /// <para>
    /// 三个标记接口合起来覆盖全部「注册表之外」的特性：
    /// <see cref="ITreeLifecycleAttribute"/>（不产生节点）、
    /// <see cref="ITreeMembershipAttribute"/>（产生节点，但不画也不改别人）、
    /// 本接口（不产生节点，但改变节点的排列）。
    /// </para>
    /// </summary>
    internal interface ITreeOrderingAttribute
    {
    }
}
