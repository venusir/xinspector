using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// 拖放的**判定**：这次拖来的东西里哪些能进列表、拒了几个、为什么。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 纯逻辑——照「决策挪出去、事件留在绘制器」的纪律：<c>DragUpdated</c> / <c>DragPerform</c>
    /// 的接线在 <c>AssetListLayout</c>，这里只回答「接受哪些」。
    /// </para>
    /// <para>
    /// <b>三条本包自定规则</b>（写进包 README 的差异清单）：**去重**（已在列表里的不再加）、
    /// **只收工程资产**（场景对象不是本特性的语义——判据 <see cref="EditorUtility.IsPersistent"/>）、
    /// 载荷内**重复只收一个**。被拒的要说得出来路（告警带计数），不静默。
    /// </para>
    /// <para>
    /// <b>不做</b> GameObject → 组件的降级查找：官方没写这种语义，本包不发明。
    /// </para>
    /// </remarks>
    internal static class AssetListDrop
    {
        #region Public Types

        /// <summary>
        /// 一次拖放的判定结果。
        /// </summary>
        public readonly struct Plan
        {
            /// <summary>以四段计数构造。</summary>
            /// <param name="accepted">接受的个数。</param>
            /// <param name="rejectedByType">类型不符被拒的个数。</param>
            /// <param name="rejectedDuplicate">重复（已在列表 / 载荷内重复）被拒的个数。</param>
            /// <param name="rejectedNotAsset">不是工程资产被拒的个数。</param>
            public Plan(int accepted, int rejectedByType, int rejectedDuplicate, int rejectedNotAsset)
            {
                Accepted = accepted;
                RejectedByType = rejectedByType;
                RejectedDuplicate = rejectedDuplicate;
                RejectedNotAsset = rejectedNotAsset;
            }

            /// <summary>接受的个数。</summary>
            public int Accepted { get; }

            /// <summary>类型不符被拒的个数。</summary>
            public int RejectedByType { get; }

            /// <summary>重复（已在列表 / 载荷内重复）被拒的个数。</summary>
            public int RejectedDuplicate { get; }

            /// <summary>不是工程资产被拒的个数。</summary>
            public int RejectedNotAsset { get; }

            /// <summary>有东西能收下。</summary>
            public bool HasAny => Accepted > 0;

            /// <summary>被拒的总数。</summary>
            public int TotalRejected => RejectedByType + RejectedDuplicate + RejectedNotAsset;
        }

        #endregion

        #region Public API

        /// <summary>
        /// 判这次拖放。
        /// </summary>
        /// <param name="dragged">拖拽载荷（<c>DragAndDrop.objectReferences</c>）。</param>
        /// <param name="elementType">列表元素类型。</param>
        /// <param name="array">列表的序列化属性（用来去重）。</param>
        /// <param name="acceptedOut">接受的写进这里（**先清空**；按拖入顺序）。</param>
        /// <returns>判定结果。</returns>
        public static Plan Build(
            Object[] dragged, Type elementType, SerializedProperty array, List<Object> acceptedOut)
        {
            acceptedOut.Clear();

            if (dragged == null || dragged.Length == 0)
            {
                return default;
            }

            var byType = 0;
            var duplicate = 0;
            var notAsset = 0;

            for (var i = 0; i < dragged.Length; i++)
            {
                var candidate = dragged[i];

                if (candidate == null)
                {
                    continue;
                }

                if (elementType == null || !elementType.IsInstanceOfType(candidate))
                {
                    byType++;
                    continue;
                }

                if (!EditorUtility.IsPersistent(candidate))
                {
                    notAsset++;
                    continue;
                }

                if (Contains(array, candidate) || Contains(acceptedOut, candidate))
                {
                    duplicate++;
                    continue;
                }

                acceptedOut.Add(candidate);
            }

            return new Plan(acceptedOut.Count, byType, duplicate, notAsset);
        }

        /// <summary>
        /// 把拒绝的原因说成一句人话（告警文案用）。
        /// </summary>
        /// <param name="plan">判定结果。</param>
        /// <returns>形如「类型不符 1 个、重复 2 个」的描述。</returns>
        public static string DescribeRejections(Plan plan)
        {
            var parts = new List<string>(3);

            if (plan.RejectedByType > 0)
            {
                parts.Add($"类型不符 {plan.RejectedByType} 个");
            }

            if (plan.RejectedDuplicate > 0)
            {
                parts.Add($"已在列表或本次载荷里重复 {plan.RejectedDuplicate} 个");
            }

            if (plan.RejectedNotAsset > 0)
            {
                parts.Add($"不是工程资产（场景对象？）{plan.RejectedNotAsset} 个");
            }

            return string.Join("、", parts);
        }

        #endregion

        #region Private Helpers

        /// <summary>这个对象是不是已经在列表里。</summary>
        /// <param name="array">列表的序列化属性。</param>
        /// <param name="candidate">候选对象。</param>
        /// <returns>在返回 <c>true</c>。</returns>
        /// <remarks>只按引用比——这本来只是「别把同一个资产加两遍」，不是集合语义。</remarks>
        private static bool Contains(SerializedProperty array, Object candidate)
        {
            var count = array.arraySize;

            for (var i = 0; i < count; i++)
            {
                if (ReferenceEquals(array.GetArrayElementAtIndex(i).objectReferenceValue, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>这个对象是不是已经在本次载荷的收受列表里。</summary>
        /// <param name="accepted">收受列表。</param>
        /// <param name="candidate">候选对象。</param>
        /// <returns>在返回 <c>true</c>。</returns>
        private static bool Contains(List<Object> accepted, Object candidate)
        {
            for (var i = 0; i < accepted.Count; i++)
            {
                if (ReferenceEquals(accepted[i], candidate))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
