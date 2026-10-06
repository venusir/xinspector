using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// <c>[PreviewField]</c> **预览方块**的落点判定：这次拖来的东西能进这个字段吗。
    /// <para>
    /// 纯函数、无 GUI 依赖——本仓不测 IMGUI，但「收不收」必须能无头断言。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>与 <c>[AssetList]</c> 的拖放规则刻意不同，别合并。</b> 那边是**资产列表**，
    /// 故只收工程资产（拖进来的场景对象会被拒）；这里是**对象字段**，场景对象本来就是合法的值
    /// ——对齐 <c>[AssetList]</c> 那条规则会让「拖一个场景里的 GameObject 上去」失效，
    /// 而那是完全正当的操作。
    /// </para>
    /// <para>
    /// 裁掉一半的参数类型也是白送的：入参是 <see cref="Object"/> 数组（<c>DragAndDrop.objectReferences</c>
    /// 就是它），故「拖来的不是 Unity 对象」这种情况**根本进不来**——不必再判一次。
    /// </para>
    /// </remarks>
    internal static class PreviewFieldDrop
    {
        #region Public API

        /// <summary>
        /// 从拖放的载荷里挑一个能赋给这个字段的对象。
        /// </summary>
        /// <param name="dragged">载荷（<c>DragAndDrop.objectReferences</c>）。</param>
        /// <param name="declaredType">
        /// 字段的**声明类型**；未知时传 <c>null</c> 或 <c>object</c>（那时只要不是空就收）。
        /// </param>
        /// <param name="accepted">接受的对象；拒绝时为 <c>null</c>。</param>
        /// <param name="reason">拒绝的原因（中文，可直接拼进告警）；接受时为 <c>null</c>。</param>
        /// <returns>可以落值返回 <c>true</c>。</returns>
        public static bool TryAccept(
            Object[] dragged, Type declaredType, out Object accepted, out string reason)
        {
            accepted = null;
            reason = null;

            if (dragged == null || dragged.Length == 0)
            {
                reason = "拖来的载荷里没有对象";
                return false;
            }

            for (var i = 0; i < dragged.Length; i++)
            {
                // 已销毁的对象按 Unity 的语义就是空，跳过它继续看下一个。
                if (dragged[i] == null)
                {
                    continue;
                }

                if (declaredType == null || declaredType == typeof(object) || declaredType.IsInstanceOfType(dragged[i]))
                {
                    accepted = dragged[i];
                    return true;
                }
            }

            reason =
                $"拖来的 {dragged.Length} 个对象里没有一个能赋给 {ReflectedAccessor.DescribeType(declaredType)} 字段";

            return false;
        }

        #endregion
    }
}
