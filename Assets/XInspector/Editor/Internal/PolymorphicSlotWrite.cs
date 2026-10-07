using System;
using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 多态槽位的一步写回：造实例 → 写值 → **当场提交**（默认**带撤销**）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>与 <see cref="TypeSlotWrite"/> 是姊妹件，而撤销处置刻意相反</b>：
    /// 类型槽位（值是 <c>System.Type</c>）的托管引用**撤销恢复不出来**——实测撤一次之后
    /// 槽位里留下的既不是旧值也不是新值（会留下不可用的对象），故那条恒 <c>WithoutUndo</c>；
    /// 这条装的是普通用户对象，写回**能撤销**（<c>SerializedReferenceProbeTests.
    /// 多态引用的写回进撤销栈</c> 实测），故默认带撤销、只有窗口路径
    /// （<c>PropertyTree.UndoEnabled</c> 为假）才关。
    /// </para>
    /// <para>
    /// <b>为什么必须当场提交：实测 <c>SerializedObject.Update()</c> 会丢掉未 <c>Apply</c> 的改动。</b>
    /// 菜单回调发生在绘制周期之外——宿主（Inspector 路径是 <c>Update → Draw → Apply</c>）
    /// 管不到它。提交必须用「设值的那个」<c>SerializedObject</c>（CLAUDE.md 的既有坑）。
    /// </para>
    /// <para>
    /// <b>单独成一个函数是为了可测</b>：藏在绘制器的私有回调里就永远测不到（本仓不测 IMGUI），
    /// 独立之后「造实例 + 落盘 + 撤销」能无头断言。
    /// </para>
    /// </remarks>
    internal static class PolymorphicSlotWrite
    {
        /// <summary>
        /// 造一个新实例写进多态槽位并立刻提交；失败时**什么都不写**。
        /// </summary>
        /// <param name="selected">选中的类型（要造它的实例）。</param>
        /// <param name="preference">非默认构造的处置档。</param>
        /// <param name="destination">多态槽位的序列化属性。</param>
        /// <param name="declaredType">**字段的声明类型**（<c>FieldInfo.FieldType</c>，不是节点的 <c>Type</c>）。</param>
        /// <param name="undoEnabled">这次提交要不要进撤销栈（窗口路径传 <c>false</c>）。</param>
        /// <param name="reason">失败原因（中文，可直接拼进告警）；成功时为 <c>null</c>。</param>
        /// <returns>造实例、写入并提交都成功返回 <c>true</c>。</returns>
        public static bool TryWrite(
            Type selected,
            NonDefaultConstructorPreference preference,
            SerializedProperty destination,
            Type declaredType,
            bool undoEnabled,
            out string reason)
        {
            if (!PolymorphicInstanceFactory.TryCreate(selected, preference, out var instance, out reason))
            {
                return false;
            }

            if (!ReflectedValueCopier.TryAssign(instance, destination, declaredType, out reason))
            {
                return false;
            }

            if (undoEnabled)
            {
                destination.serializedObject.ApplyModifiedProperties();
            }
            else
            {
                destination.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            return true;
        }

        /// <summary>
        /// 清空多态槽位（菜单里的「（无）」项）：写 <c>null</c> 并**当场提交**；失败时什么都不写。
        /// </summary>
        /// <param name="destination">多态槽位的序列化属性。</param>
        /// <param name="declaredType">**字段的声明类型**。</param>
        /// <param name="undoEnabled">这次提交要不要进撤销栈（窗口路径传 <c>false</c>）。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>清空并提交成功返回 <c>true</c>。</returns>
        /// <remarks>清空**不需要造实例**，故不经过 <see cref="PolymorphicInstanceFactory"/>。</remarks>
        public static bool TryClear(
            SerializedProperty destination, Type declaredType, bool undoEnabled, out string reason)
        {
            if (!ReflectedValueCopier.TryAssign(null, destination, declaredType, out reason))
            {
                return false;
            }

            if (undoEnabled)
            {
                destination.serializedObject.ApplyModifiedProperties();
            }
            else
            {
                destination.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            return true;
        }
    }
}
