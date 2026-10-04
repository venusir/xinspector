using System;
using XInspector.Internal;

namespace XInspector
{
    // 这三个是**树的生命周期钩子**：方法本身不产生节点、不画任何东西，
    // 只是被属性树在特定时机调用一次（或每趟一次）。判据在编辑器侧的 TreeLifecycle 里，
    // 数据（特性）与判据（时机）分居两侧，与条件族、按钮族同一个套路。
    //
    // 三个都实现 ITreeLifecycleAttribute：判据与节点收集都靠它识别，
    // 否则「只挂这类特性的类型」不会被接管，特性会静默地一次都不生效。

    /// <summary>
    /// 属性树**建好时**调用一次。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 时机是「这个 Inspector 初始化完成」——相当于 Unity 的 <c>OnEnable</c>，
    /// 但发生在**每次建树**时（切换选中目标、域重载之后都会重来）。
    /// 多选时对每个目标各调一次；不记 Undo（它是初始化，不是用户的编辑）。
    /// </para>
    /// <para>
    /// 方法必须**无参且非泛型**，否则构建期告警并跳过。返回值会被忽略。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异：</b>Odin 还有一个 <c>[OnInspectorInit("方法名")]</c> 形式
    /// （resolved string）。本包不做——那套表达式语法本包一律不支持，
    /// 而裸标在方法上已经表达了同一件事，少一层间接。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [OnInspectorInit]
    /// private void Prepare()
    /// {
    ///     // 每次开 Inspector 时把派生数据显示刷新一遍
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class OnInspectorInitAttribute : Attribute, ITreeLifecycleAttribute
    {
    }

    /// <summary>
    /// 属性树**释放时**调用一次。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 时机是「这个 Inspector 关闭 / 切走 / 域重载之前」，早于节点状态的复位。
    /// 多选时对每个目标各调一次；不记 Undo。
    /// </para>
    /// <para>
    /// <b>不要拿它当资产的生命周期用</b>——它只说明「没人再看这个对象了」，
    /// 与对象本身是否被销毁毫无关系。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异：</b>同上，不做 <c>(string action)</c> 形式。
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class OnInspectorDisposeAttribute : Attribute, ITreeLifecycleAttribute
    {
    }

    /// <summary>
    /// **每趟 GUI 布局**调用一次。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用途是「画之前先把派生数据刷新一遍」——例如根据几个字段算出一个只读的展示值。
    /// 时机是 IMGUI 的 <c>Layout</c> 趟，故**每帧恰好一次**（选 <c>Repaint</c> 会漏掉纯布局趟，
    /// 选「每次 <c>Draw</c>」则一趟布局 + 一趟重绘要跑两遍）。
    /// </para>
    /// <para>
    /// <b>这是本包自定的语义，Odin 没有对应的时机。</b> Odin 的 <c>[OnStateUpdate]</c> 接一个
    /// resolved string 指向要调的方法，跑在它自己的 state update 循环里；本包没有那个循环，
    /// 故改成裸标在方法上、判据换成「每趟 GUI 布局」。这样做还有个好处——
    /// 方法名不必再写一遍字符串。
    /// </para>
    /// <para>
    /// 它每趟都会跑，故**别在里面做重活**：Inspector 是交互驱动的，用户每动一下都会触发一趟。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [OnStateUpdate]
    /// private void RefreshDerived()
    /// {
    ///     total = attack + defense;
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class OnStateUpdateAttribute : Attribute, ITreeLifecycleAttribute
    {
    }
}
