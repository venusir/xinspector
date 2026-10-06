namespace XInspector.Editor
{
    /// <summary>
    /// 元素层的对账：让「元素节点数」与 <c>arraySize</c> 在**每趟绘制之前**一致。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>先决问题的答案。</b> 「节点数与真实元素数不一致时怎么办」的答案是
    /// **让不一致不存在**：元素层是数组的同步投影，对不上就整层丢弃重建，不做增量节点。
    /// 三条理由（都在代码里）：其一，<see cref="InspectorProperty.Path"/> 是不可变身份，
    /// 删中间元素后所有下标位移 = 所有路径必须改写 = 节点必须换对象，增量方案连「改名」
    /// 都表达不出来；其二，消费者按**引用**记账（搜索命中集是节点的 <c>HashSet</c>、
    /// 状态袋挂在节点上），换对象天然与它们相容，而增量重排会让它们静默指向错位的节点；
    /// 其三，仓内先例就是分组装配的「快照 → 清空 → 重挂」。
    /// </para>
    /// <para>
    /// <b>为什么落在树级绘制入口，而不是集合绘制器入口。</b> 搜索过滤
    /// （<c>SearchFilterState.EnsureNodes</c>）会从任意一个 <c>ShouldDraw</c> 惰性触发、
    /// **整棵子树**走一遍——它可能先于集合绘制器读到元素节点。对账放在
    /// <see cref="PropertyTree.Draw"/> 的最前面，「元素层在本趟内有效」才是先于**所有**
    /// 消费者的前置条件；顺带地，Inspector 路径与窗口路径共用这一个入口。
    /// </para>
    /// <para>
    /// <b>为什么只比长度就够（外加一个脏标记）。</b> 元素路径是位置的投影
    /// （<c>items.Array.data[i]</c>），长度不变时「路径 → 元素」的对应不变，
    /// 而 <c>SerializedProperty</c> 是按路径的活句柄——改值、换序都不需要重建。
    /// 只有长度变了（或结构刚被改过、旧句柄不再可信，见
    /// <see cref="CollectionElementLayerState.Dirty"/>）才重建。
    /// </para>
    /// <para>
    /// <b>一次性的动作，不是每帧的动作。</b> 重建只在长度对不上时发生，绘制路径上
    /// 只有「比较」；反射与处理器都发生在重建那一下（与构建期同级）。
    /// </para>
    /// </remarks>
    internal static class CollectionElementSync
    {
        #region Public API

        /// <summary>
        /// 对账整棵树上**登记过的**集合。
        /// </summary>
        /// <param name="tree">属性树。</param>
        /// <returns>这一趟重建过几个层（测试与诊断用）。</returns>
        /// <remarks>
        /// <para>
        /// <b>循环里每一轮重读 <c>Count</c>，不做快照、不提前缓存——这是契约。</b>
        /// 元素层可以递归之后再叠一层：外层集合重建时会**摘掉**它子树里旧的内层登记、
        /// 并把新的内层登记**追加到表尾**。这个「摘下 + 追加」在直读 <c>Count</c> 的
        /// <c>for</c> 下是正确的，靠三条不变量：
        /// </para>
        /// <list type="number">
        /// <item>登记是 **DFS 先序**：祖先的登记下标恒小于其后代（见
        /// <c>PropertyTree.AddElementCollection</c> / <c>UnregisterElementLayersIn</c>）；</item>
        /// <item>重建摘掉的恒是**自己的后代**且恒为死条目（其祖先子树刚被释放），下标恒 &gt; 当前
        /// <c>i</c>——不会有活条目被跳过；</item>
        /// <item>追加到表尾的新内层条目恒**新鲜**（<c>Dirty=false</c>、长度相等），同趟后续
        /// <c>Reconcile</c> 是廉价 no-op。</item>
        /// </list>
        /// <para>
        /// 被误改成快照或提前缓存 <c>Count</c> 的症状是「偶尔漏对账一个集合」——难归因，
        /// 故这里写成注释而不是只靠测试。快照还违反「每帧路径禁分配」（本方法在每个
        /// GUI 事件跑一次）。
        /// </para>
        /// </remarks>
        public static int ReconcileAll(PropertyTree tree)
        {
            if (tree == null)
            {
                return 0;
            }

            var collections = tree.ElementCollections;
            var rebuilt = 0;

            for (var i = 0; i < collections.Count; i++)
            {
                if (Reconcile(collections[i]))
                {
                    rebuilt++;
                }
            }

            return rebuilt;
        }

        /// <summary>
        /// 对账一个集合；重建过返回 <c>true</c>。
        /// </summary>
        /// <param name="collection">集合节点。</param>
        /// <returns>重建过返回 <c>true</c>。</returns>
        public static bool Reconcile(InspectorProperty collection)
        {
            var layer = collection?.State.Get<CollectionElementLayerState>();
            var array = collection?.ValueEntry?.SerializedProperty;

            if (layer == null || array == null)
            {
                // 没有元素层的集合（绝大多数）在这里一句话都不说。
                return false;
            }

            if (!layer.Dirty && layer.Nodes.Count == array.arraySize)
            {
                return false;
            }

            PropertyTreeBuilder.RebuildElementLayer(collection);
            return true;
        }

        #endregion
    }
}
