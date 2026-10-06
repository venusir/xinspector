using System;
using System.Collections.Generic;
using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 一个节点在这次搜索下的三种去处。
    /// </summary>
    /// <remarks>
    /// 分三档而不是两档，是为了让**分组名命中时整组照常显示**：只有两档的话，
    /// 搜「基础」会得到一个空框（分组自己画了、组成员全被筛掉）。
    /// </remarks>
    internal enum SearchVisibility
    {
        /// <summary>自己不命中、后代也没有命中的——不画。</summary>
        Hidden = 0,

        /// <summary>靠后代命中活下来——自己画，子节点逐个再筛。</summary>
        Filtered = 1,

        /// <summary>自己命中——整棵子树原样保留。</summary>
        KeepAll = 2,
    }

    /// <summary>
    /// 搜索的**判定**：命中与否、以及拿什么当匹配素材。
    /// <para>
    /// 全是纯函数（GUI 那一半本仓不测，判定这一半必须能无头测）。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>值文本来自 <see cref="SerializedValueReader.TryFormat"/>。</b>
    /// 「一个值该显示成什么」与只读展示、与回调报的值共用同一份实现——
    /// 三处各写一套的话，搜索能搜着的东西与眼睛看见的东西迟早对不上。
    /// </remarks>
    internal static class SearchMatcher
    {
        #region Private Fields

        /// <summary>值匹配往下钻的深度上限——与嵌套展开同档（自引用类型挡在这里）。</summary>
        private const int MaxDepth = 4;

        #endregion

        #region Public API

        /// <summary>查询是不是「在生效」——空白串等于没搜。</summary>
        /// <param name="query">输入框里的原文。</param>
        /// <returns>生效返回 <c>true</c>。</returns>
        public static bool IsActive(string query)
        {
            return !string.IsNullOrWhiteSpace(query);
        }

        /// <summary>文本命中：大小写不敏感的子串。</summary>
        /// <param name="query">查询（已 trim）。</param>
        /// <param name="text">被比的文本。</param>
        /// <returns>命中返回 <c>true</c>；空查询恒真。</returns>
        public static bool TextMatches(string query, string text)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            return !string.IsNullOrEmpty(text)
                && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 节点**自己**命中没有：标签或它自己的值。
        /// </summary>
        /// <param name="node">节点。</param>
        /// <param name="query">查询（已 trim）。</param>
        /// <returns>命中返回 <c>true</c>。</returns>
        /// <remarks>
        /// 只看节点自己，**不看后代**——「靠后代命中」是调用方的事（它要记得那一档）。
        /// </remarks>
        public static bool NodeMatchesSelf(InspectorProperty node, string query)
        {
            if (TextMatches(query, node.Label.text))
            {
                return true;
            }

            var entry = node.ValueEntry;
            return entry?.SerializedProperty != null && PropertyMatches(entry.SerializedProperty, query, 0);
        }

        /// <summary>
        /// 一个序列化属性（连同它下面任意一层的子属性）里有没有命中的**值**。
        /// </summary>
        /// <param name="property">序列化属性。</param>
        /// <param name="query">查询（已 trim）。</param>
        /// <param name="depth">当前深度。</param>
        /// <returns>命中返回 <c>true</c>。</returns>
        /// <remarks>
        /// 复合元素（<c>List&lt;MyStruct&gt;</c> 的元素）读不出值，但它的子字段读得出，
        /// 故往下钻一层再比——「搜字段的值」在列表与表格上就是靠这条成立的。
        /// </remarks>
        public static bool PropertyMatches(SerializedProperty property, string query, int depth)
        {
            if (property == null || string.IsNullOrEmpty(query))
            {
                return false;
            }

            if (SerializedValueReader.TryFormat(property, null, out var text, out _) && TextMatches(query, text))
            {
                return true;
            }

            if (depth >= MaxDepth || !property.hasVisibleChildren)
            {
                return false;
            }

            var child = property.Copy();
            var childDepth = property.depth + 1;
            var next = child.NextVisible(true) && child.depth == childDepth;

            while (next)
            {
                if (PropertyMatches(child, query, depth + 1))
                {
                    return true;
                }

                next = child.NextVisible(false) && child.depth == childDepth;
            }

            return false;
        }

        /// <summary>
        /// 表格的一行命中没有：**任一可见列**的值命中即算。
        /// </summary>
        /// <param name="element">本行的元素。</param>
        /// <param name="columns">表格的列（序号列不在其中）。</param>
        /// <param name="query">查询（已 trim）。</param>
        /// <returns>命中返回 <c>true</c>。</returns>
        /// <remarks>
        /// 列之外的字段不参与：用户搜的是**看得见的东西**，拿一份没显示的字段把行拉出来，
        /// 会得到「这一行为什么在这里」的困惑。
        /// </remarks>
        public static bool TableRowMatches(SerializedProperty element, TableColumn[] columns, string query)
        {
            for (var i = 0; i < columns.Length; i++)
            {
                var cell = element.FindPropertyRelative(columns[i].Name);

                // 单元格自身不再往下钻：复合单元格在表里本来就画成占位。
                if (cell != null && PropertyMatches(cell, query, MaxDepth))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>这一批里有没有命中的行。</summary>
        /// <param name="rows">行掩码；<c>null</c> 表示不过滤。</param>
        /// <returns>有命中的返回 <c>true</c>。</returns>
        public static bool HasAnyRow(bool[] rows)
        {
            if (rows == null)
            {
                return true;
            }

            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i])
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// 一个 <c>[Searchable]</c> 宿主的搜索状态，挂在它的 <see cref="PropertyState"/> 上。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>过滤结果在查询（或数组长度）变化时重算一次</b>，绘制路径只做集合查表：
    /// 没有反射、没有 LINQ、没有每帧分配。代价是「搜索框没动时改了值」不会立刻刷新命中集，
    /// 动一下查询即刷新——这条写进了包 README 的已知限制。
    /// </para>
    /// <para>
    /// <b>查询内容不跨会话持久化</b>（域重载、重开 Inspector 都清空），与折叠状态同一口径。
    /// </para>
    /// </remarks>
    internal sealed class SearchFilterState
    {
        #region Private Fields

        private readonly HashSet<InspectorProperty> _keepAll = new HashSet<InspectorProperty>();
        private readonly HashSet<InspectorProperty> _filtered = new HashSet<InspectorProperty>();
        private string _nodeQuery;
        private bool _nodesComputed;

        private bool[] _listRows;
        private string _listQuery;
        private int _listSize = -1;

        private bool[] _tableRows;
        private string _tableQuery;
        private int _tableSize = -1;

        #endregion

        #region Public API

        /// <summary>输入框里的原文；<c>null</c> 与空白都表示不过滤。</summary>
        public string Query;

        /// <summary>这次搜索在不在生效（空白查询不算）。</summary>
        public bool IsActive => SearchMatcher.IsActive(Query);

        /// <summary>
        /// 算一遍（必要时）「这棵子树里谁能画」。
        /// </summary>
        /// <param name="host">挂着 <c>[Searchable]</c> 的那个节点；它的**标签不参与**匹配。</param>
        /// <remarks>
        /// 宿主自己的标签不参与是有意的：搜「items」把整个 <c>items</c> 字段全显示出来，
        /// 等于没搜——用户已经在看这个字段了。
        /// </remarks>
        public void EnsureNodes(InspectorProperty host)
        {
            var query = Query?.Trim();

            if (_nodesComputed && string.Equals(_nodeQuery, query, StringComparison.Ordinal))
            {
                return;
            }

            _nodeQuery = query;
            _nodesComputed = true;
            _keepAll.Clear();
            _filtered.Clear();

            Collect(host, query);
        }

        /// <summary>这个节点在这次搜索下该怎么画。</summary>
        /// <param name="node">节点。</param>
        /// <returns>三档去处。</returns>
        public SearchVisibility VisibilityOf(InspectorProperty node)
        {
            if (_keepAll.Contains(node))
            {
                return SearchVisibility.KeepAll;
            }

            return _filtered.Contains(node) ? SearchVisibility.Filtered : SearchVisibility.Hidden;
        }

        /// <summary>
        /// 算一遍（必要时）列表的行掩码。
        /// </summary>
        /// <param name="array">列表的序列化属性。</param>
        /// <returns>逐行的掩码；不过滤时为 <c>null</c>。</returns>
        /// <remarks>
        /// **行只按值匹配**：元素标签是 <c>Element 3</c> 这种索引名，让它参与的话，
        /// 任何含 <c>element</c> 的查询都会全中。
        /// </remarks>
        public bool[] EnsureListRows(SerializedProperty array)
        {
            var query = Query?.Trim();

            if (!SearchMatcher.IsActive(query))
            {
                return null;
            }

            if (_listRows != null && _listSize == array.arraySize && string.Equals(_listQuery, query, StringComparison.Ordinal))
            {
                return _listRows;
            }

            _listQuery = query;
            _listSize = array.arraySize;

            if (_listRows == null || _listRows.Length != array.arraySize)
            {
                _listRows = new bool[array.arraySize];
            }

            for (var i = 0; i < _listRows.Length; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                _listRows[i] = SearchMatcher.PropertyMatches(element, query, 0);
            }

            return _listRows;
        }

        /// <summary>
        /// 算一遍（必要时）表格的行掩码。
        /// </summary>
        /// <param name="array">表格的序列化属性。</param>
        /// <param name="columns">列。</param>
        /// <returns>逐行的掩码；不过滤时为 <c>null</c>。</returns>
        public bool[] EnsureTableRows(SerializedProperty array, TableColumn[] columns)
        {
            var query = Query?.Trim();

            if (!SearchMatcher.IsActive(query))
            {
                return null;
            }

            if (_tableRows != null && _tableSize == array.arraySize && string.Equals(_tableQuery, query, StringComparison.Ordinal))
            {
                return _tableRows;
            }

            _tableQuery = query;
            _tableSize = array.arraySize;

            if (_tableRows == null || _tableRows.Length != array.arraySize)
            {
                _tableRows = new bool[array.arraySize];
            }

            for (var i = 0; i < _tableRows.Length; i++)
            {
                _tableRows[i] = SearchMatcher.TableRowMatches(array.GetArrayElementAtIndex(i), columns, query);
            }

            return _tableRows;
        }

        #endregion

        #region Private Helpers

        /// <summary>自底向上标一遍：谁自己命中（连同它的子树），谁靠后代命中。</summary>
        /// <param name="node">当前节点。</param>
        /// <param name="query">查询（已 trim）。</param>
        /// <returns>这棵子树的**后代**里有没有命中。</returns>
        /// <remarks>
        /// 自己命中的那一支不再往下递归地判：整棵子树原样保留就够了，
        /// 再判一遍只是白花工夫（而且会把「整组显示」重新拆成逐项判定）。
        /// </remarks>
        private bool Collect(InspectorProperty node, string query)
        {
            var matched = false;
            var children = node.Children;

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];

                if (SearchMatcher.NodeMatchesSelf(child, query))
                {
                    MarkKeepAll(child);
                    matched = true;
                    continue;
                }

                if (Collect(child, query))
                {
                    _filtered.Add(child);
                    matched = true;
                }
            }

            return matched;
        }

        /// <summary>把一棵子树整个标成「保留」。</summary>
        /// <param name="node">子树的根。</param>
        private void MarkKeepAll(InspectorProperty node)
        {
            _keepAll.Add(node);

            var children = node.Children;
            for (var i = 0; i < children.Count; i++)
            {
                MarkKeepAll(children[i]);
            }
        }

        #endregion
    }

    /// <summary>
    /// 「谁在过滤」的答案：往上找一个**生效中**的 <c>[Searchable]</c> 宿主。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 渲染子节点的绘制器（根与分组的 <c>ChildrenDrawer</c>、复合成员的末端、集合绘制器）
    /// 都靠它拿策略。**策略不是可见性**：被筛掉的节点照旧 <c>IsVisible == true</c>，
    /// 只是没被交给 <c>Draw()</c>——<c>[ShowIf]</c> 装在可见性槽上的闭包因此原封不动。
    /// </para>
    /// <para>
    /// <b>跳过没有生效的内层宿主</b>：内层搜索框空着就等于内层没在过滤，外层的条件继续适用。
    /// </para>
    /// </remarks>
    internal readonly struct SearchScope
    {
        #region Private Fields

        private readonly SearchFilterState _state;
        private readonly InspectorProperty _host;

        #endregion

        #region Construction

        /// <summary>构造一个作用域。只有 <see cref="Find"/> 会造它。</summary>
        /// <param name="state">搜索状态。</param>
        /// <param name="host">挂着 <c>[Searchable]</c> 的那个节点。</param>
        private SearchScope(SearchFilterState state, InspectorProperty host)
        {
            _state = state;
            _host = host;
        }

        #endregion

        #region Public API

        /// <summary>从某个节点往上找最近的、**生效中**的搜索。</summary>
        /// <param name="property">起点节点。</param>
        /// <returns>作用域；没有生效中的搜索时返回一个 <see cref="IsActive"/> 为假的值。</returns>
        public static SearchScope Find(InspectorProperty property)
        {
            for (var node = property; node != null; node = node.Parent)
            {
                var state = node.State.Get<SearchFilterState>();

                if (state != null && state.IsActive)
                {
                    return new SearchScope(state, node);
                }
            }

            return default;
        }

        /// <summary>有没有在过滤。</summary>
        public bool IsActive => _state != null;

        /// <summary>搜索状态（宿主的那一份）。</summary>
        public SearchFilterState State => _state;

        /// <summary>
        /// 这个子节点该不该画。
        /// </summary>
        /// <param name="node">子节点。</param>
        /// <returns>该画返回 <c>true</c>。</returns>
        public bool ShouldDraw(InspectorProperty node)
        {
            if (_state == null)
            {
                return true;
            }

            _state.EnsureNodes(_host);
            return _state.VisibilityOf(node) != SearchVisibility.Hidden;
        }

        #endregion
    }
}
