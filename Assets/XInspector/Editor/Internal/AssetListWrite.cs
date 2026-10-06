using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// 资产列表的**写入**：落值前的最后一道类型校验，菜单选择与拖放共用。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么还要校验一遍。</b> 类型过滤串（<c>t:Xxx</c>）只是**启发式收窄**——抽象类型、
    /// 不常见的派生都可能命不中或命错。<b>正确性由这里保证</b>：不是元素类型的实例就拒掉
    /// 并告警，绝不写进去。
    /// </para>
    /// <para>
    /// 两条路都经这里：单元素形态写字段本身（<see cref="TryAssign"/>）；列表形态批量追加
    /// （<see cref="TryAppend"/> → <see cref="CollectionChangeInvoker.ApplyAddRange"/>，
    /// 回调与元素层的脏标记都在那一处）。
    /// </para>
    /// </remarks>
    internal static class AssetListWrite
    {
        #region Public API

        /// <summary>
        /// 把资产写进**单个对象字段**（单元素形态）。
        /// </summary>
        /// <param name="property">目标节点（告警去重要用）。</param>
        /// <param name="slot">字段的序列化属性。</param>
        /// <param name="elementType">要的类型。</param>
        /// <param name="asset">要写进去的资产。</param>
        /// <returns>写进去了返回 <c>true</c>。</returns>
        public static bool TryAssign(
            InspectorProperty property, SerializedProperty slot, Type elementType, Object asset)
        {
            if (slot == null || !Accepts(property, elementType, asset))
            {
                return false;
            }

            slot.objectReferenceValue = asset;
            return true;
        }

        /// <summary>
        /// 把资产**批量追加**进列表（列表形态：菜单选择与拖放共用）。
        /// </summary>
        /// <param name="property">集合节点。</param>
        /// <param name="array">集合的序列化属性。</param>
        /// <param name="elementType">元素类型。</param>
        /// <param name="assets">要追加的资产，顺序即写入顺序。</param>
        /// <returns>长度真的变了返回 <c>true</c>。</returns>
        /// <remarks>类型不符的会被剔除（每个类型告警一次）；**全部不合格就不产生任何改动**。</remarks>
        public static bool TryAppend(
            InspectorProperty property, SerializedProperty array, Type elementType, IList<Object> assets)
        {
            if (array == null || assets == null || assets.Count == 0)
            {
                return false;
            }

            List<Object> accepted = null;

            for (var i = 0; i < assets.Count; i++)
            {
                if (!Accepts(property, elementType, assets[i]))
                {
                    continue;
                }

                accepted ??= new List<Object>(assets.Count);
                accepted.Add(assets[i]);
            }

            return accepted != null &&
                   CollectionChangeInvoker.ApplyAddRange(property, array, accepted);
        }

        #endregion

        #region Private Helpers

        /// <summary>这道值能不能落进去；不能则告警一次（不静默）。</summary>
        /// <param name="property">目标节点。</param>
        /// <param name="elementType">要的类型。</param>
        /// <param name="asset">候选资产。</param>
        /// <returns>能返回 <c>true</c>。</returns>
        private static bool Accepts(InspectorProperty property, Type elementType, Object asset)
        {
            if (asset == null)
            {
                return false;
            }

            if (elementType != null && elementType.IsInstanceOfType(asset))
            {
                return true;
            }

            // 同一种错类型只报一次（键里带上类型名）：拖一堆同类错东西进来不会刷屏。
            DrawerWarnings.Once(property, nameof(AssetListWrite) + "." + asset.GetType().Name,
                $"[XInspector] 资产「{asset.name}」（{asset.GetType().Name}）不是属性「{property.Path}」" +
                $"要的类型（{elementType?.Name ?? "?"}），已忽略。");

            return false;
        }

        #endregion
    }
}
