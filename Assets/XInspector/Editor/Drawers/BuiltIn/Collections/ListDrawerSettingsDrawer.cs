using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="ListDrawerSettingsAttribute"/>：自绘数组与列表的容器、行与增删按钮。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>替换型绘制器</b>（值绘制带，与 <see cref="DisplayAsStringDrawer"/> 同档同款）：
    /// 自己画完、不调下一个。因此它绕过了末端那层禁用罩——<c>[ReadOnly]</c> / <c>[DisableIf]</c> /
    /// <c>[DisableIn]</c> 都要在这里自己照应：按钮进 <see cref="EditorGUI.DisabledScope"/>，
    /// **灰而不隐**（隐不隐由旋钮说了算，禁不禁用由只读说了算）。
    /// </para>
    /// <para>
    /// <b>元素仍由原生绘制器逐个画</b>——本包不建元素节点，因为树的形状在构建结束后不可变，
    /// 而数组长度随时可变（见 <see cref="InspectorProperty.RawChildren"/> 的契约）。
    /// 元素级特性因此仍不生效（它们进不了树），这条边界写进了包 README。
    /// </para>
    /// <para>
    /// <b>结构性增删的纪律：</b>循环里只**记录意图**，趟末统一施加。两个理由：元素句柄在增删后
    /// 不再可信；同一个 Layout 与随后的事件之间元素个数必须一致（中途改会让同帧后续事件看到
    /// 不同的行数，症状是 GUILayout 的「control 位置」异常）。
    /// </para>
    /// <para>
    /// <b>不自己记 Undo</b>：只改内存里的 <see cref="SerializedProperty"/>，写回与撤销由宿主统一做
    /// （Inspector 的 <c>ApplyModifiedProperties</c> 登记 Undo，窗口路径不登记——本包对窗口的一贯约定）。
    /// </para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class ListDrawerSettingsDrawer : AttributeDrawer<ListDrawerSettingsAttribute>
    {
        #region Private Fields

        /// <summary>行尾小按钮的宽度（像素）。</summary>
        private const float ButtonWidth = 20f;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property, ListDrawerSettingsAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (!CollectionDrawerLayout.CanDraw(serializedProperty))
            {
                DrawerWarnings.Once(property, nameof(ListDrawerSettingsDrawer),
                    DrawerWarnings.TypeMismatch(property, "[ListDrawerSettings]", "数组或 List"));
                CallNextDrawer(property, label);
                return;
            }

            var state = property.State.GetOrCreate<CollectionDrawerState>();
            if (!state.Initialized)
            {
                state.Initialized = true;
                state.Expanded = attribute.ShowFoldout;
            }

            // 只读来自两处：特性自己的旋钮，与链上更外层装的只读解析器（[ReadOnly]/[DisableIf]）。
            var readOnly = attribute.IsReadOnly || property.State.IsReadOnly;

            // 多选且各目标的列表不一致时**禁止增删**：arraySize 的改动会把主目标的整份列表
            // 铺到所有目标上——那是静默改数据，与本包最忌讳的现象同类。元素内容的混合态
            // 由 Unity 的 PropertyField 免费显示为「—」，不受影响。
            var canResize = !readOnly && !serializedProperty.hasMultipleDifferentValues;

            var append = DrawHeader(property, attribute, state, canResize, label);

            if (!state.Expanded)
            {
                // 收起时没有增删入口——避免「看不见的修改」。
                return;
            }

            // 结构性修改**攒到这里、趟末统一施加**：循环里改会让同帧后续事件看到不同的行数。
            var removeIndex = -1;

            EditorGUI.indentLevel++;
            try
            {
                removeIndex = DrawRows(property, serializedProperty, attribute, state, canResize);

                if (removeIndex >= 0)
                {
                    // 增删连同 [OnCollectionChanged] 的回调一起走（见 CollectionChangeInvoker）：
                    // 两个回调夹住的正是「写进序列化数据」这一步。
                    if (!CollectionChangeInvoker.ApplyRemove(property, serializedProperty, removeIndex))
                    {
                        // 删不掉：长度不可变的数组（固定缓冲那种）。告警而不是「再删一次」——
                        // 多删一个元素是静默改数据，不做。
                        DrawerWarnings.Once(property, nameof(ListDrawerSettingsDrawer) + ".fixed",
                            $"[XInspector] 属性「{property.Path}」的集合删不掉元素（长度不可变的数组？），" +
                            "该次删除已忽略。");
                    }
                }
                else if (append)
                {
                    if (!CollectionChangeInvoker.ApplyAdd(property, serializedProperty))
                    {
                        // 加不进去与删不掉同款：告警一次，绝不「再试一次」。
                        DrawerWarnings.Once(property, nameof(ListDrawerSettingsDrawer) + ".fixedAdd",
                            $"[XInspector] 属性「{property.Path}」的集合加不了元素（长度不可变的数组？），" +
                            "该次追加已忽略。");
                    }
                }
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>标题行：折叠头（或普通标签）+ 右端的「+」。</summary>
        /// <param name="property">集合节点（表格模型的「恒展开」取自它的状态）。</param>
        /// <param name="attribute">特性实例。</param>
        /// <param name="state">每属性状态（折叠状态存这里）。</param>
        /// <param name="canResize">此刻允许增删吗。</param>
        /// <param name="label">链上传下来的标签（可能被 <c>[HideLabel]</c> 撤掉）。</param>
        /// <returns>本趟是否请求了追加。</returns>
        private static bool DrawHeader(
            InspectorProperty property,
            ListDrawerSettingsAttribute attribute,
            CollectionDrawerState state,
            bool canResize,
            GUIContent label)
        {
            var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            var model = property.State.Get<TableModel>();
            var showFoldout = attribute.ShowFoldout && (model == null || !model.AlwaysExpanded);
            var showAdd = attribute.HideAddButton == false;
            var addRect = row;

            if (showAdd)
            {
                addRect = new Rect(row.xMax - ButtonWidth, row.y, ButtonWidth, row.height);
                row.xMax = addRect.xMin - 2f;
            }

            if (showFoldout)
            {
                state.Expanded = EditorGUI.Foldout(row, state.Expanded, label ?? GUIContent.none, true);
            }
            else
            {
                state.Expanded = true;

                if (label != null && label != GUIContent.none)
                {
                    EditorGUI.LabelField(row, label);
                }
            }

            if (!showAdd || !state.Expanded)
            {
                return false;
            }

            using (new EditorGUI.DisabledScope(!canResize))
            {
                return GUI.Button(addRect, "+", EditorStyles.miniButton);
            }
        }

        /// <summary>逐行画元素；只**记录**删除意图（下标），不在循环里改结构。</summary>
        /// <param name="property">集合节点（表格形态要用它的状态与告警）。</param>
        /// <param name="array">集合的序列化属性。</param>
        /// <param name="attribute">特性实例。</param>
        /// <param name="state">每属性状态（行标签缓存）。</param>
        /// <param name="canResize">此刻允许增删吗。</param>
        /// <returns>请求删除的下标；没有请求时为 <c>-1</c>。</returns>
        private static int DrawRows(
            InspectorProperty property,
            SerializedProperty array,
            ListDrawerSettingsAttribute attribute,
            CollectionDrawerState state,
            bool canResize)
        {
            var removeIndex = -1;
            var model = property.State.Get<TableModel>();

            if (model != null)
            {
                // 表格形态：模型是构建期建好的（见 TableListProcessor），绘制路径不反射。
                TableLayout.DrawRows(property, array, attribute, model, canResize, ref removeIndex);
                return removeIndex;
            }

            var count = array.arraySize;

            for (var i = 0; i < count; i++)
            {
                DrawRow(array.GetArrayElementAtIndex(i), attribute, state, i, canResize, ref removeIndex);
            }

            return removeIndex;
        }

        /// <summary>画一行：复合元素自己画折叠头、单值元素交给原生控件，行尾是可选的「−」。</summary>
        /// <param name="element">本行的元素。</param>
        /// <param name="attribute">特性实例。</param>
        /// <param name="state">每属性状态（行标签缓存）。</param>
        /// <param name="index">元素下标。</param>
        /// <param name="canResize">此刻允许增删吗。</param>
        /// <param name="removeIndex">请求删除的下标（原地更新）。</param>
        /// <remarks>
        /// <b>不用水平布局。</b> 复合元素展开时子字段会被水平组挤成半宽——本仓在
        /// <c>[SuffixLabel]</c> 那里已经踩过并记下了这条。这里用 <c>GetControlRect</c> 切矩形：
        /// 行内容占满整行，行尾的按钮单独切一块。
        /// </remarks>
        private static void DrawRow(
            SerializedProperty element,
            ListDrawerSettingsAttribute attribute,
            CollectionDrawerState state,
            int index,
            bool canResize,
            ref int removeIndex)
        {
            var isCompound = element.propertyType == SerializedPropertyType.Generic && element.hasVisibleChildren;
            var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            var showRemove = attribute.HideRemoveButton == false;
            var removeRect = row;

            if (showRemove)
            {
                removeRect = new Rect(row.xMax - ButtonWidth, row.y, ButtonWidth, row.height);
                row.xMax = removeRect.xMin - 2f;
            }

            var label = state.RowLabel(index, attribute.ShowIndexLabels, element.displayName);

            if (isCompound)
            {
                // 复合元素：折叠头由我们画，展开的子字段在行**外**缩进逐个画。
                element.isExpanded = EditorGUI.Foldout(row, element.isExpanded, label, true);
            }
            else
            {
                // 单值元素：原生控件占满剩下的行宽。判据要有 Generic——向量也有可见子级，
                // 只看 hasVisibleChildren 会把 Vector3 画成「折叠头 + x/y/z 三行」，与原生不一致。
                EditorGUI.PropertyField(row, element, label, false);
            }

            if (showRemove)
            {
                using (new EditorGUI.DisabledScope(!canResize))
                {
                    if (GUI.Button(removeRect, "−", EditorStyles.miniButton))
                    {
                        removeIndex = index;
                    }
                }
            }

            if (!isCompound || !element.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            try
            {
                CollectionRows.DrawChildren(element);
            }
            finally
            {
                EditorGUI.indentLevel--;
            }
        }

        #endregion
    }

    /// <summary>
    /// 集合绘制的**判定**与**行画法**的公共部分——与绘制分开，判定可无头测试（本仓不测 IMGUI）。
    /// </summary>
    internal static class CollectionDrawerLayout
    {
        #region Public API

        /// <summary>
        /// 能否自绘：需要 Unity 的序列化后端，且是真的数组 / List。
        /// </summary>
        /// <param name="serializedProperty">值的序列化属性；反射成员没有它（传 <c>null</c>）。</param>
        /// <returns>可以自绘返回 <c>true</c>。</returns>
        /// <remarks>
        /// 字符串要单独排除：Unity 在若干语境下把 <c>string</c> 也算作 <c>isArray</c>，
        /// 只看那个属性会把文本框当成集合。
        /// </remarks>
        public static bool CanDraw(SerializedProperty serializedProperty)
        {
            return serializedProperty != null
                && serializedProperty.isArray
                && serializedProperty.propertyType != SerializedPropertyType.String;
        }

        /// <summary>
        /// 特性列表里没有列表设置时补一份。
        /// </summary>
        /// <param name="attributes">节点当前的特性列表（构建期，可写）。</param>
        /// <remarks>
        /// <para>
        /// <c>[TableList]</c> 与 <c>[OnCollectionChanged]</c> 都要它：前者自己不会画行、
        /// 后者要一个增删的落点，而增删**只有集合绘制器有**。少了这一步，
        /// 特性就「写了没反应」——本包最忌讳的静默。
        /// </para>
        /// <para>
        /// 幂等：已经有 <c>[ListDrawerSettings]</c> 就原样返回，故几个处理器各调一次也只会补一份。
        /// </para>
        /// </remarks>
        public static void EnsureListSettings(IList<Attribute> attributes)
        {
            for (var i = 0; i < attributes.Count; i++)
            {
                if (attributes[i] is ListDrawerSettingsAttribute)
                {
                    return;
                }
            }

            attributes.Add(new ListDrawerSettingsAttribute());
        }

        #endregion
    }

    /// <summary>集合行的公共画法。</summary>
    internal static class CollectionRows
    {
        #region Public API

        /// <summary>
        /// 画元素的**直接**子字段（孙辈仍交给 Unity 的 <c>includeChildren: true</c>）。
        /// </summary>
        /// <param name="element">复合类型的元素。</param>
        /// <remarks>
        /// 深度检查是必需的：没有子字段时 <c>NextVisible(true)</c> 会直接走到本属性之后。
        /// 与 <c>InlinePropertyDrawer.DrawChildren</c> 同一套迭代器模板。
        /// </remarks>
        public static void DrawChildren(SerializedProperty element)
        {
            var child = element.Copy();
            var depth = element.depth + 1;
            var next = child.NextVisible(true) && child.depth == depth;

            while (next)
            {
                EditorGUILayout.PropertyField(child, true);
                next = child.NextVisible(false) && child.depth == depth;
            }
        }

        #endregion
    }

    /// <summary>
    /// 集合的结构性修改：两处 Unity 经典坑的单一落点。
    /// </summary>
    /// <remarks>
    /// 只改内存里的 <see cref="SerializedProperty"/>，写回与 Undo 交给宿主的每帧 apply
    /// （Inspector 带 Undo、窗口不带）——绘制器自己**不**记撤销步。
    /// </remarks>
    internal static class CollectionMutation
    {
        #region Public API

        /// <summary>
        /// 在末尾追加一个元素。
        /// </summary>
        /// <param name="array">集合的序列化属性。</param>
        /// <remarks>
        /// <b>新元素是上一个元素的副本</b>（空列表则为默认值）——这是 Unity 自己的语义
        /// （官方手册：新增元素会复用前一个元素的值），原生「+」按钮亦然，本包保持逐字一致。
        /// Odin 有一个「不复制」的旋钮（<c>AddCopiesLastElement</c>），本包不做——要它就得给任意
        /// 元素类型造一台默认值写入器（<c>boxedValue</c> 对复合类型不通用），代价大于收益；
        /// 这条写进已知限制。
        /// </remarks>
        public static void Add(SerializedProperty array)
        {
            array.arraySize++;
        }

        /// <summary>
        /// 删掉下标处的元素。
        /// </summary>
        /// <param name="array">集合的序列化属性。</param>
        /// <param name="index">要删的下标。</param>
        /// <returns>长度确实减了返回 <c>true</c>；删不掉返回 <c>false</c>（由调用方告警）。</returns>
        /// <remarks>
        /// <b>只删一次。</b> 老文档里「引用类型要删两次」的做法在 Unity 2021.2 之后已不适用
        /// （本包下限 6000.3），照抄会**多删一个元素**。删不掉的返回 <c>false</c> 让调用方告警，
        /// 而不是猜着再删一下——多删是静默改数据，不做。
        /// </remarks>
        public static bool TryRemove(SerializedProperty array, int index)
        {
            var size = array.arraySize;
            array.DeleteArrayElementAtIndex(index);
            return array.arraySize != size;
        }

        #endregion
    }

    /// <summary>
    /// 集合容器的每属性状态，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    /// <remarks>
    /// 折叠状态**不跨会话持久化**：域重载、重开 Inspector 都回到 <c>ShowFoldout</c> 的初值
    /// （与 <c>[FoldoutGroup]</c> 的落点一致，见 Roadmap 的未决项）。
    /// </remarks>
    internal sealed class CollectionDrawerState
    {
        #region Private Fields

        /// <summary>行标签缓存：默认是「Element i」，开了索引标签则是「i」。</summary>
        private GUIContent[] _rowLabels;

        /// <summary>缓存里那一批标签用的是「索引」形态吗——两种形态不能混用同一份缓存。</summary>
        private bool _indexed;

        #endregion

        #region Public API

        /// <summary>是否已按特性初始化过初值。</summary>
        public bool Initialized;

        /// <summary>当前是否展开。</summary>
        public bool Expanded;

        /// <summary>
        /// 取某行的标签（缓存复用——绘制路径上不新建 <see cref="GUIContent"/>，也不做字符串转换）。
        /// </summary>
        /// <param name="index">元素下标。</param>
        /// <param name="showIndex">是否用「下标」形态（否则用元素自己的显示名）。</param>
        /// <param name="displayName">元素自己的显示名（原生是 <c>Element i</c>）。</param>
        /// <returns>该行的标签。</returns>
        public GUIContent RowLabel(int index, bool showIndex, string displayName)
        {
            if (_rowLabels == null || _indexed != showIndex || index >= _rowLabels.Length)
            {
                _rowLabels = new GUIContent[Mathf.Max(16, index + 1)];
                _indexed = showIndex;
            }

            var label = _rowLabels[index];
            if (label == null)
            {
                label = new GUIContent(showIndex ? index.ToString() : displayName);
                _rowLabels[index] = label;
            }

            return label;
        }

        #endregion
    }
}
