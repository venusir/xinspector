using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 集合的结构性改动**连同它的回调**——<c>[OnCollectionChanged]</c> 的 before / after 只有
    /// 放在这里才名副其实。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么不另设一格绘制器。</b> 增删只有集合绘制器有落点（<see cref="CollectionMutation"/> 的
    /// 两个调用点），而它是替换型绘制器——外档只能在它**返回之后**动，那时改动早已施加，
    /// 「改动前」会名不副实。故两个回调在这里、**紧挨着**写进序列化数据那一步触发。
    /// </para>
    /// <para>
    /// <b>成对与否的判据是「长度真的变了」。</b> 改动前照触发（它是「记下改动前的样子」的钩子，
    /// 多调一次无害），改动**没落地**时不触发改动后（它是「改动发生了，去做后续」的钩子，
    /// 多调一次有害）。长度不可变的数组因此只会看到改动前那一次，外加一条告警。
    /// </para>
    /// <para>
    /// <b>回调触发时目标对象上的托管集合仍是旧的。</b> 这里只改 <c>SerializedObject</c> 的内存副本，
    /// 落盘由宿主在这一帧绘制结束后统一做——本包没有「落盘之后」的挂点。
    /// </para>
    /// <para>
    /// <b>两个方法都是绘制器调的那一个。</b> 抽成可无头调用的形式，是为了让「回调真的跑了、
    /// 拿到的信息对不对」能被用例直接断言——IMGUI 那一半本仓不测。
    /// </para>
    /// </remarks>
    internal static class CollectionChangeInvoker
    {
        #region Public API

        /// <summary>
        /// 追加一个元素，并在前后配对触发回调。
        /// </summary>
        /// <param name="property">集合节点。</param>
        /// <param name="array">集合的序列化属性。</param>
        /// <returns>长度真的变了返回 <c>true</c>。</returns>
        public static bool ApplyAdd(InspectorProperty property, SerializedProperty array)
        {
            var state = property.State.Get<CollectionChangedState>();

            // 追加时**不报新元素的值**：新元素是上一个的副本，报它等于报旧值（误导）。
            var info = new CollectionChangeInfo(CollectionChangeType.Add, array.arraySize, null);

            Fire(property, state?.Before, info);

            var size = array.arraySize;
            CollectionMutation.Add(array);
            var applied = array.arraySize != size;

            if (applied)
            {
                CollectionElementExpansion.MarkLayerDirty(property);
                Fire(property, state?.After, info);
            }

            return applied;
        }

        /// <summary>
        /// **批量追加**若干个对象引用，并在前后配对触发回调。
        /// </summary>
        /// <param name="property">集合节点。</param>
        /// <param name="array">集合的序列化属性。</param>
        /// <param name="values">要写进新槽的资产，**顺序即写入顺序**。</param>
        /// <returns>长度真的变了返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>一次拖入 / 一次多选 = 一对回调</b>（改动前一次、改动后一次），与单元素的
        /// <see cref="ApplyAdd"/> 同一口径：<c>CollectionChangeInfo.Type</c> 仍是
        /// <see cref="CollectionChangeType.Add"/>、<c>Index</c> 是**第一个**新元素的下标、
        /// <c>Value</c> 为 <c>null</c>（追加本来就不报新元素的值）。
        /// </para>
        /// <para>
        /// <b>先长槽、后写值、再触发改动后。</b> 槽由 <see cref="CollectionMutation.AddRange"/>
        /// 按 Unity 的「副本」语义长出，紧接着被逐个覆盖——改动后那一次回调看到的是**写完值
        /// 之后**的序列化数据（托管集合仍是旧的，见类注释）。
        /// </para>
        /// <para>空入参**不产生任何改动**、也不触发回调（「长度真的变了」这条判据不成立）。</para>
        /// </remarks>
        public static bool ApplyAddRange(
            InspectorProperty property, SerializedProperty array, IList<Object> values)
        {
            if (values == null || values.Count == 0)
            {
                return false;
            }

            var state = property.State.Get<CollectionChangedState>();
            var first = array.arraySize;
            var info = new CollectionChangeInfo(CollectionChangeType.Add, first, null);

            Fire(property, state?.Before, info);

            if (!CollectionMutation.AddRange(array, values.Count))
            {
                return false;
            }

            for (var i = 0; i < values.Count; i++)
            {
                array.GetArrayElementAtIndex(first + i).objectReferenceValue = values[i];
            }

            CollectionElementExpansion.MarkLayerDirty(property);
            Fire(property, state?.After, info);

            return true;
        }

        /// <summary>
        /// 删掉下标处的元素，并在前后配对触发回调。
        /// </summary>
        /// <param name="property">集合节点。</param>
        /// <param name="array">集合的序列化属性。</param>
        /// <param name="index">要删的下标。</param>
        /// <returns>长度真的变了返回 <c>true</c>。</returns>
        public static bool ApplyRemove(InspectorProperty property, SerializedProperty array, int index)
        {
            var state = property.State.Get<CollectionChangedState>();
            var value = ReadRemovedValue(property, array, index, state);
            var info = new CollectionChangeInfo(CollectionChangeType.RemoveAt, index, value);

            Fire(property, state?.Before, info);

            var applied = CollectionMutation.TryRemove(array, index);

            if (applied)
            {
                CollectionElementExpansion.MarkLayerDirty(property);
                Fire(property, state?.After, info);
            }

            return applied;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 在删除之前读出这个元素的值。
        /// </summary>
        /// <param name="property">集合节点。</param>
        /// <param name="array">集合的序列化属性。</param>
        /// <param name="index">要删的下标。</param>
        /// <param name="state">回调状态；没配回调时为 <c>null</c>。</param>
        /// <returns>读出的值；读不出来时为 <c>null</c>。</returns>
        /// <remarks>
        /// 删完就读不到了，故只能在这里读。没有回调要看这个值时**不读**：白读一次复合元素的
        /// 子树不提，还会为一个谁都不关心的值报一条告警。
        /// </remarks>
        private static object ReadRemovedValue(
            InspectorProperty property, SerializedProperty array, int index, CollectionChangedState state)
        {
            if (state == null || (state.Before == null && state.After == null))
            {
                return null;
            }

            var element = array.GetArrayElementAtIndex(index);
            var elementType = CollectionElement.TypeOf(property.Type);

            if (SerializedValueReader.TryRead(element, elementType, out var value, out var reason))
            {
                return value;
            }

            // 读不出来不是「没发生改动」：值报 null，回调照常触发，但要说清为什么。
            DrawerWarnings.Once(property, nameof(CollectionChangeInvoker) + ".值读不出来",
                $"[XInspector] 属性「{property.Path}」的集合回调读不出被移除元素的值（{reason}），"
                + "CollectionChangeInfo.Value 为 null；回调照常触发。");

            return null;
        }

        /// <summary>调一次回调（没配或解析不到时什么都不做）。</summary>
        /// <param name="property">集合节点。</param>
        /// <param name="callback">回调条目；<c>null</c> 表示这个方向没配。</param>
        /// <param name="info">这次改动的描述。</param>
        /// <remarks>
        /// Undo / 异常吞掉 / 逐目标调用 / 静态方法只调一次，全部交给
        /// <see cref="MethodInvoker"/>——与 <c>[OnValueChanged]</c> 同一套语义，不另造一套。
        /// </remarks>
        private static void Fire(InspectorProperty property, CollectionCallback callback, CollectionChangeInfo info)
        {
            if (callback?.Methods == null)
            {
                return;
            }

            var arguments = callback.TakesInfo ? new object[] { info, info.Value } : null;
            var tree = property.Owner;

            MethodInvoker.Invoke(
                callback.Methods,
                tree?.Targets,
                callback.Scopes,
                arguments,
                tree?.UndoEnabled ?? false,
                property.Label.text);
        }

        #endregion
    }
}
