using System;
using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 多态引用容器的对账：让「子节点集合」与**槽位里装着的具体类型**在**每趟绘制之前**一致。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与 <see cref="CollectionElementSync"/> 是同型问题的同一种答案——那边对的是
    /// <c>arraySize</c>（长度会变），这边对的是**具体类型**（用户或代码换了实现）。
    /// 答案也一样：**不做增量，对不上就整棵子树重建**。于是「节点数与真实成员数不一致」
    /// 这个问题在本层同样**不存在**。
    /// </para>
    /// <para>
    /// 对账判据是**具体类型变了**，不是「实例变了」：换一个同类型的新实例，子节点集合
    /// 一模一样，重建只会白白把折叠状态抖掉。故比的是 <c>managedReferenceValue?.GetType()</c>
    /// （零分配，且空槽位天然表达为 <c>null</c>），而不是每帧分配字符串的类型名，
    /// 也不是 <c>managedReferenceId</c>（同类型换实例它也会变）。
    /// </para>
    /// <para>
    /// 放在 <see cref="PropertyTree.Draw"/> 里、元素层对账的**紧后面**：同一条理由——
    /// 「本趟内子节点有效」必须**先于所有消费者**（搜索会从任意一处惰性触发、整棵子树走树）。
    /// </para>
    /// </remarks>
    internal static class PolymorphicReferenceSync
    {
        #region Public API

        /// <summary>
        /// 对账整棵树上**登记过的**多态容器。
        /// </summary>
        /// <param name="tree">属性树。</param>
        /// <returns>这一趟重建过几个容器（测试与诊断用）。</returns>
        /// <remarks>
        /// 与元素层同款：**循环里每一轮重读 <c>Count</c>，不做快照**。重建会摘掉自己后代里
        /// 的旧登记、并把新登记追加到表尾，直读 <c>Count</c> 在这个模式下才是对的
        /// （完整论证见 <see cref="CollectionElementSync.ReconcileAll"/>）。
        /// </remarks>
        public static int ReconcileAll(PropertyTree tree)
        {
            if (tree == null)
            {
                return 0;
            }

            var containers = tree.PolymorphicContainers;
            var rebuilt = 0;

            for (var i = 0; i < containers.Count; i++)
            {
                if (Reconcile(containers[i]))
                {
                    rebuilt++;
                }
            }

            return rebuilt;
        }

        /// <summary>
        /// 对账一个多态容器；重建过返回 <c>true</c>。
        /// </summary>
        /// <param name="container">多态成员节点。</param>
        /// <returns>重建过返回 <c>true</c>。</returns>
        public static bool Reconcile(InspectorProperty container)
        {
            var layer = container?.State.Get<PolymorphicLayerState>();
            var property = container?.ValueEntry?.SerializedProperty;

            if (layer == null || property == null)
            {
                // 没展开过的多态成员（绝大多数）在这里一句话都不说。
                return false;
            }

            var concrete = PolymorphicReference.ResolveConcreteType(property);

            if (!layer.Dirty && layer.ConcreteType == concrete)
            {
                return false;
            }

            PropertyTreeBuilder.RebuildPolymorphicLayer(container);
            return true;
        }

        #endregion
    }
}
