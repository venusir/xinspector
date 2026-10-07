using System;
using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 类型槽位的一步写回：<see cref="ReflectedValueCopier.TryAssign"/> 成功后**当场提交**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么必须当场提交：实测 <c>SerializedObject.Update()</c> 会丢掉未 <c>Apply</c> 的改动</b>
    /// （见 <c>SerializedReferenceProbeTests</c> 的「未提交的改动活不活得过Update」）。
    /// 类型选择器的写回发生在**菜单回调**里，而回调在绘制周期之外——宿主
    /// （Inspector 路径是 <c>Update → Draw → Apply</c>）管不到它。
    /// </para>
    /// <para>
    /// <b>提交必须用「设值的那个」<c>SerializedObject</c></b>（CLAUDE.md 的既有坑：
    /// 拿另一个句柄去 Apply，改动**静默**不落盘）。
    /// </para>
    /// <para>
    /// <b>刻意用 <c>WithoutUndo</c>：这一步不进撤销栈。</b> 实测（见
    /// <c>ReflectedValueCopierTests</c> 的「类型槽位写回刻意不进撤销栈」）：值是一只
    /// <c>System.Type</c> 的托管引用，**撤销恢复不回来**——撤一次之后槽位里留下的既不是旧值、
    /// 也不是新值，而是一只不洁的 <c>RuntimeType</c> 对象（连格式化它都能把编辑器搞崩）。
    /// 故这里宁可「Ctrl+Z 跳过这一步」，也不让一次撤销把槽位弄坏；
    /// 用户对象的多态引用没有这个问题（见 <c>SerializedReferenceProbeTests</c> 的撤销探针）。
    /// </para>
    /// <para>
    /// <b>单独成一个函数是为了可测</b>：藏在绘制器的私有回调里就永远测不到
    /// （本仓不测 IMGUI），独立之后「落盘 + 不进撤销栈」能无头断言。
    /// </para>
    /// </remarks>
    internal static class TypeSlotWrite
    {
        /// <summary>
        /// 把选中的类型写进类型槽位并立刻提交；失败时**什么都不写**。
        /// </summary>
        /// <param name="selected">选中的类型；<c>null</c> 表示清空槽位。</param>
        /// <param name="destination">类型槽位的序列化属性。</param>
        /// <param name="declaredType">**字段的声明类型**（<c>FieldInfo.FieldType</c>，不是节点的 <c>Type</c>）。</param>
        /// <param name="reason">拒绝的原因；成功时为 <c>null</c>。</param>
        /// <returns>写入并提交成功返回 <c>true</c>。</returns>
        public static bool TryWrite(
            Type selected, SerializedProperty destination, Type declaredType, out string reason)
        {
            if (!ReflectedValueCopier.TryAssign(selected, destination, declaredType, out reason))
            {
                return false;
            }

            // WithoutUndo 是**刻意的**（理由见 remarks）：带撤销的那条路在这类槽位上会把值弄坏。
            destination.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
