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
    /// <b><c>CreateInstanceFunction</c> 的两种失败是两件事</b>（<paramref name="createInstance"/>）：
    /// 它**解析失败**（构建期）时调用方回落内置工厂（配置错不该让换类型整个不可用）；
    /// 而它**点击时返回 <c>null</c> 或抛异常**时这里**拒绝且不回落**——函数说「不给」，
    /// 拿内置档造一个用户明确没给的东西是静默换值的近亲。
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
        /// <param name="createInstance">
        /// 自定义造实例（<c>CreateInstanceFunction</c> 解析出来的）；<c>null</c> 表示走内置工厂。
        /// </param>
        /// <param name="destination">多态槽位的序列化属性。</param>
        /// <param name="declaredType">**字段的声明类型**（<c>FieldInfo.FieldType</c>，不是节点的 <c>Type</c>）。</param>
        /// <param name="undoEnabled">这次提交要不要进撤销栈（窗口路径传 <c>false</c>）。</param>
        /// <param name="reason">失败原因（中文，可直接拼进告警）；成功时为 <c>null</c>。</param>
        /// <returns>造实例、写入并提交都成功返回 <c>true</c>。</returns>
        public static bool TryWrite(
            Type selected,
            NonDefaultConstructorPreference preference,
            Func<Type, object> createInstance,
            SerializedProperty destination,
            Type declaredType,
            bool undoEnabled,
            out string reason)
        {
            object instance;

            if (createInstance != null)
            {
                if (!TryUseCustomFactory(selected, createInstance, out instance, out reason))
                {
                    return false;
                }
            }
            else if (!PolymorphicInstanceFactory.TryCreate(selected, preference, out instance, out reason))
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
        /// 自定义造实例那一格：调它、校验结果——**失败一律不回落内置工厂**。
        /// </summary>
        /// <param name="selected">选中的类型。</param>
        /// <param name="createInstance">自定义造实例。</param>
        /// <param name="instance">造出的实例。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>成功返回 <c>true</c>。</returns>
        private static bool TryUseCustomFactory(
            Type selected, Func<Type, object> createInstance, out object instance, out string reason)
        {
            instance = null;
            reason = null;

            try
            {
                instance = createInstance(selected);
            }
            catch (Exception exception)
            {
                reason = $"自定义造实例函数抛了异常：{exception.Message}";
                return false;
            }

            if (instance == null)
            {
                reason =
                    "自定义造实例函数返回了 null——本包**不回落内置工厂**" +
                    "（那会造一个函数明确没给的东西），槽位未改动。";
                return false;
            }

            if (!selected.IsInstanceOfType(instance))
            {
                reason =
                    $"自定义造实例函数返回的是 {ReflectedAccessor.DescribeType(instance.GetType())}，" +
                    $"不是选中的 {ReflectedAccessor.DescribeType(selected)}——槽位未改动。";
                return false;
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
