using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// <c>[AssetList]</c> 列表形态的绘制：缩略图行 + 原生对象字段 + 「−」，以及「选择」菜单。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与表格形态走同一条分流（<c>ListDrawerSettingsDrawer.DrawRows</c>）：容器画标题、折叠与
    /// 增删的**落点**，这里只回答「一行长什么样」。**行里的对象字段仍用 Unity 原生的**
    /// （<c>EditorGUI.ObjectField</c> 的序列化属性重载）——类型限制、拖拽赋值、预制体覆盖
    /// 一样不少，与 <c>[PreviewField]</c> 同一立场。
    /// </para>
    /// <para>
    /// 缩略图**自绘**（<c>GUI.Box</c> + <c>GUI.DrawTexture</c>），不走
    /// <see cref="PreviewFieldGUI.DrawBox"/>：那条路在「真预览与小图标都取不到」时会
    /// <c>new GUIContent(name)</c> 画占位文字——逐行画会破「绘制路径零分配」。
    /// </para>
    /// </remarks>
    internal static class AssetListLayout
    {
        #region Private Fields

        /// <summary>缩略图边长（**本包自定值**，写进包 README）。</summary>
        public const float ThumbnailSize = 16f;

        /// <summary>元件之间的间距（像素）。</summary>
        private const float Spacing = 2f;

        /// <summary>行尾「−」的宽度（像素）。</summary>
        private const float ButtonWidth = 20f;

        /// <summary>空列表的提示（静态复用——绘制路径不新建 <see cref="GUIContent"/>）。</summary>
        private static readonly GUIContent EmptyLabel =
            new GUIContent("（空：把资产拖进来，或用标题行的「选择」）");

        #endregion

        #region 行几何

        /// <summary>
        /// 一行的三块矩形。
        /// </summary>
        public readonly struct RowRects
        {
            /// <summary>以三块矩形构造。</summary>
            /// <param name="thumbnail">缩略图。</param>
            /// <param name="field">对象字段。</param>
            /// <param name="remove">行尾的「−」。</param>
            public RowRects(Rect thumbnail, Rect field, Rect remove)
            {
                Thumbnail = thumbnail;
                Field = field;
                Remove = remove;
            }

            /// <summary>缩略图矩形（垂直居中）。</summary>
            public Rect Thumbnail { get; }

            /// <summary>对象字段矩形。</summary>
            public Rect Field { get; }

            /// <summary>行尾「−」矩形；宽度为 0 表示不画。</summary>
            public Rect Remove { get; }
        }

        /// <summary>
        /// 按可用宽度切出一行的三块矩形。
        /// </summary>
        /// <param name="row">整行的矩形。</param>
        /// <param name="showRemove">是否给「−」留位。</param>
        /// <returns>三块矩形。</returns>
        /// <remarks>
        /// 纯函数（照 <c>TableLayout.AllocateWidths</c> 的纪律）：宽度不够时**不缩成负数**。
        /// </remarks>
        public static RowRects Allocate(Rect row, bool showRemove)
        {
            var remove = showRemove
                ? new Rect(row.xMax - ButtonWidth, row.y, ButtonWidth, row.height)
                : default;

            var right = showRemove ? row.xMax - ButtonWidth - Spacing : row.xMax;

            var size = Mathf.Min(ThumbnailSize, row.height);
            var thumbnail = new Rect(row.x, row.y + (row.height - size) * 0.5f, size, size);
            var fieldX = thumbnail.xMax + Spacing;
            var field = new Rect(fieldX, row.y, Mathf.Max(0f, right - fieldX), row.height);

            return new RowRects(thumbnail, field, remove);
        }

        #endregion

        #region 绘制

        /// <summary>
        /// 逐行画元素；只**记录**删除意图（下标），不在循环里改结构。
        /// </summary>
        /// <param name="property">集合节点。</param>
        /// <param name="array">集合的序列化属性。</param>
        /// <param name="attribute">列表设置（行尾按钮的可见性取自它）。</param>
        /// <param name="model">构建期模型（拖放接线要用）。</param>
        /// <param name="canResize">此刻允许增删吗。</param>
        /// <param name="rows">搜索的行掩码；<c>null</c> 表示不过滤。</param>
        /// <param name="removeIndex">请求删除的下标（原地更新，趟末施加）。</param>
        /// <returns>请求删除的下标；没有请求时为 <c>-1</c>。</returns>
        public static int DrawRows(
            InspectorProperty property,
            SerializedProperty array,
            ListDrawerSettingsAttribute attribute,
            AssetListModel model,
            bool canResize,
            bool[] rows,
            ref int removeIndex)
        {
            var state = property.State.GetOrCreate<AssetListState>();
            var disableContent = CollectionDrawerLayout.DisableRowContent(property);
            var showRemove = attribute.HideRemoveButton == false;
            var count = array.arraySize;

            // 拖放区 = 画出来的这些行（空列表时是那行提示）的并集；事件在行循环**之后**处理
            // ——「记意图、趟末施加」。
            var body = new Rect(0f, 0f, 0f, 0f);

            if (count == 0)
            {
                // 留一行提示而不是留白——空白的归因成本比一句话高得多。
                var hint = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(hint, EmptyLabel, EditorStyles.miniLabel);
                body = hint;
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    if (rows != null && !rows[i])
                    {
                        continue;
                    }

                    var element = array.GetArrayElementAtIndex(i);
                    var row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    var rects = Allocate(row, showRemove);

                    using (new EditorGUI.DisabledScope(disableContent))
                    {
                        DrawThumbnail(rects.Thumbnail, element.objectReferenceValue, state.PreviewOf(i));
                        EditorGUI.ObjectField(rects.Field, element, GUIContent.none);
                    }

                    if (showRemove)
                    {
                        using (new EditorGUI.DisabledScope(!canResize))
                        {
                            if (GUI.Button(rects.Remove, "−", EditorStyles.miniButton))
                            {
                                removeIndex = i;
                            }
                        }
                    }

                    body = body.width <= 0f ? row : Union(body, row);
                }
            }

            HandleDrag(property, array, model, canResize, body);

            return removeIndex;
        }

        /// <summary>两块矩形的并集（逐行累积拖放区）。</summary>
        /// <param name="left">已有区域。</param>
        /// <param name="right">新行。</param>
        /// <returns>并集。</returns>
        private static Rect Union(Rect left, Rect right)
        {
            var xMin = Mathf.Min(left.xMin, right.xMin);
            var yMin = Mathf.Min(left.yMin, right.yMin);
            var xMax = Mathf.Max(left.xMax, right.xMax);
            var yMax = Mathf.Max(left.yMax, right.yMax);

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        /// <summary>
        /// 处理落在列表身上的拖放：判定（<see cref="AssetListDrop"/>）→ 高亮 → 接受后施加。
        /// </summary>
        /// <param name="property">集合节点。</param>
        /// <param name="array">集合的序列化属性。</param>
        /// <param name="model">构建期模型。</param>
        /// <param name="canResize">此刻允许增删吗。</param>
        /// <param name="body">拖放区。</param>
        /// <remarks>
        /// <para>
        /// <b>只在这一处碰事件</b>（决策在 <see cref="AssetListDrop"/> 里，可无头测）。
        /// </para>
        /// <para>
        /// <b>行内的对象字段优先。</b> 指针落在某一行的字段上时，Unity 的 <c>ObjectField</c>
        /// 自己会处理拖放（原生替换那一行的值）——本包不跟它抢：事件被它用掉之后类型会变成
        /// <see cref="EventType.Used"/>，上面那道「只认 DragUpdated / DragPerform」的判据
        /// 天然让开，不会出现「替换 + 追加」的双重动作。
        /// </para>
        /// </remarks>
        private static void HandleDrag(
            InspectorProperty property,
            SerializedProperty array,
            AssetListModel model,
            bool canResize,
            Rect body)
        {
            var current = Event.current;

            // 已被别人（行的对象字段等）用掉的事件类型会变成 EventType.Used，故这里天然让开。
            if (current == null || body.width <= 0f ||
                (current.type != EventType.DragUpdated && current.type != EventType.DragPerform) ||
                !body.Contains(current.mousePosition))
            {
                return;
            }

            if (!canResize)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                current.Use();
                return;
            }

            var state = property.State.GetOrCreate<AssetListState>();
            var plan = AssetListDrop.Build(
                DragAndDrop.objectReferences, model.ElementType, array, state.Accepted);

            if (current.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = plan.HasAny
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
                current.Use();
                return;
            }

            // DragPerform：接受，并在**行循环之外**施加（列表因此长出新行——下一趟才画得出来）。
            if (plan.HasAny)
            {
                DragAndDrop.AcceptDrag();
                AssetListWrite.TryAppend(property, array, model.ElementType, state.Accepted);
            }

            if (plan.TotalRejected > 0)
            {
                // 拖放是**用户动作**（不是每帧），故直接报一条带计数的告警，不走 Once——
                // 每次被拒都要说得出来路。
                Debug.LogWarning(
                    $"[XInspector] 拖到「{property.Path}」的资产里有 {plan.TotalRejected} 个被忽略：" +
                    $"{AssetListDrop.DescribeRejections(plan)}。");
            }

            current.Use();
        }

        /// <summary>
        /// 画一行的缩略图：空框 + 贴图（取不到时只剩空框，**不画占位文字**——那会每帧分配）。
        /// </summary>
        /// <param name="rect">缩略图矩形。</param>
        /// <param name="target">这一行的对象。</param>
        /// <param name="state">这一行的缩略图缓存。</param>
        private static void DrawThumbnail(Rect rect, Object target, PreviewFieldState state)
        {
            GUI.Box(rect, GUIContent.none);

            var texture = PreviewFieldContent.ResolveTexture(target, state);
            if (texture == null)
            {
                return;
            }

            var inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            GUI.DrawTexture(inner, texture, ScaleMode.ScaleToFit, true);
        }

        #endregion
    }

    /// <summary>
    /// 「选择」菜单：按模型过滤出的资产列表弹一个编辑器自带菜单。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与 <c>[AssetSelector]</c> 同一手法：<c>GenericMenu</c> + <c>MenuFunction2</c> +
    /// 路径当 <c>userData</c>（**不为每个选项建闭包**）。弹出的是编辑器自带菜单，不是自建窗口
    /// ——带搜索框的弹出层本包不做（与 <c>[AssetSelector]</c> 同一条取舍）。
    /// </para>
    /// <para>
    /// <b>全工程搜索只在这一刻发生</b>（事件路径）——见 <see cref="AssetListQuery"/> 的纪律。
    /// </para>
    /// </remarks>
    internal static class AssetListMenu
    {
        #region Public API

        /// <summary>
        /// 弹出「选择」菜单。
        /// </summary>
        /// <param name="property">集合节点（写回与告警去重要用）。</param>
        /// <param name="model">构建期模型。</param>
        /// <param name="array">集合的序列化属性。</param>
        public static void Show(InspectorProperty property, AssetListModel model, SerializedProperty array)
        {
            if (model == null || array == null)
            {
                return;
            }

            var paths = AssetListQuery.Find(model);

            if (paths.Count == 0)
            {
                // 不弹空菜单：一条说清过滤条件的告警，比一个什么都没有的菜单有用。
                DrawerWarnings.Once(property, nameof(AssetListMenu) + ".空",
                    $"[XInspector] 属性「{property.Path}」的 [AssetList] 没搜到可选的资产" +
                    $"（类型过滤「{model.TypeFilter}」，目录 {Describe(model.Folders)}）。" +
                    "抽象类型等命不中属于正常的收窄；要放宽就把元素类型改具体些。");
                return;
            }

            var options = AssetSelectorOptions.Build(paths, flatten: false);
            var menu = new GenericMenu();

            for (var i = 0; i < options.Count; i++)
            {
                menu.AddItem(
                    new GUIContent(options[i].Path),
                    false,
                    OnSelected,
                    new Payload(property, array.Copy(), model.ElementType, options[i].AssetPath));
            }

            menu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
        }

        #endregion

        #region Private Helpers

        /// <summary>把目录列表说成人话（告警文案用）。</summary>
        /// <param name="folders">归一化后的目录。</param>
        /// <returns>描述。</returns>
        private static string Describe(string[] folders)
        {
            if (folders == null || folders.Length == 0)
            {
                return "整个工程";
            }

            return string.Join("|", folders);
        }

        /// <summary>菜单回调的载荷。</summary>
        private readonly struct Payload
        {
            /// <summary>集合节点。</summary>
            public readonly InspectorProperty Property;

            /// <summary>集合的序列化属性（菜单打开期间的副本）。</summary>
            public readonly SerializedProperty Array;

            /// <summary>元素类型。</summary>
            public readonly Type ElementType;

            /// <summary>选中的资产路径。</summary>
            public readonly string AssetPath;

            /// <summary>以四段构造。</summary>
            /// <param name="property">集合节点。</param>
            /// <param name="array">集合的序列化属性。</param>
            /// <param name="elementType">元素类型。</param>
            /// <param name="assetPath">资产路径。</param>
            public Payload(
                InspectorProperty property, SerializedProperty array, Type elementType, string assetPath)
            {
                Property = property;
                Array = array;
                ElementType = elementType;
                AssetPath = assetPath;
            }
        }

        /// <summary>菜单项被点中：取资产、写入。</summary>
        /// <param name="payload">载荷。</param>
        private static void OnSelected(object payload)
        {
            var data = (Payload)payload;
            var asset = AssetListQuery.Load(data.AssetPath, data.ElementType);

            if (asset == null)
            {
                Debug.LogWarning(
                    $"[XInspector] 资产「{data.AssetPath}」里没有「{data.ElementType.Name}」类型的对象" +
                    "（子资产也没找到），已忽略。");
                return;
            }

            AssetListWrite.TryAppend(data.Property, data.Array, data.ElementType, new[] { asset });
        }

        #endregion
    }

    /// <summary>
    /// <c>[AssetList]</c>（列表形态）的每属性状态。
    /// </summary>
    /// <remarks>
    /// <b>每行一份缩略图缓存</b>：<see cref="PreviewFieldState"/> 只记一个「当前对象」，
    /// 列表 N 行共用一份会互相顶掉（每帧重取）。数组式增长照
    /// <c>CollectionDrawerState.RowLabel</c> 的手法（起步 16、按需翻倍式扩容）。
    /// </remarks>
    internal sealed class AssetListState
    {
        #region Private Fields

        private PreviewFieldState[] _previews;

        #endregion

        #region Public API

        /// <summary>
        /// 拖放的收受缓冲：每次判定前被 <see cref="AssetListDrop.Build"/> 清空、随后复用
        /// ——事件路径上不产生垃圾。
        /// </summary>
        public readonly List<Object> Accepted = new List<Object>();

        /// <summary>
        /// 取第 <paramref name="index"/> 行的缩略图缓存。
        /// </summary>
        /// <param name="index">行下标。</param>
        /// <returns>该行自己的缓存。</returns>
        public PreviewFieldState PreviewOf(int index)
        {
            if (_previews == null || index >= _previews.Length)
            {
                var grown = new PreviewFieldState[Mathf.Max(16, index + 1)];

                for (var i = 0; i < grown.Length; i++)
                {
                    grown[i] = new PreviewFieldState();
                }

                if (_previews != null)
                {
                    Array.Copy(_previews, grown, _previews.Length);
                }

                _previews = grown;
            }

            return _previews[index];
        }

        #endregion
    }
}
