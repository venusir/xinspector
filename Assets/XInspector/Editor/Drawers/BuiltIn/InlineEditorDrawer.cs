using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="InlineEditorAttribute"/>：把对象引用字段画成内嵌编辑器。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 对象字段那一段**走链条后面的绘制器**（<c>CallNextDrawer</c> → 末端的原生
    /// <c>PropertyField</c>），所以拖拽赋值、类型限制、预制件覆盖一样不少；本类只加
    /// 「内嵌内容」与对象字段的外层外观（装箱 / 折叠 / 隐藏）。
    /// </para>
    /// <para>
    /// 内嵌的那一层由 Unity 自己的编辑器解析承担（<c>Editor.CreateEditor</c>）：
    /// 被引用对象的类型若接了本管线，画出来就是一棵 XInspector 树——递归天然成立，
    /// 本类不做任何「自己再画一棵树」的事。它只负责摆放、守卫深度、以及实例的生命周期。
    /// </para>
    /// <para>
    /// 摆放与降级决策全在纯函数 <see cref="InlineEditorLayout"/> 里，深度与环的守卫在
    /// <see cref="InlineEditorDrawContext"/> 里，故本类剩下的只有「画 + 调下一个」。
    /// </para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class InlineEditorDrawer : AttributeDrawer<InlineEditorAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, InlineEditorAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.ObjectReference)
            {
                DrawerWarnings.Once(property, nameof(InlineEditorDrawer),
                    DrawerWarnings.TypeMismatch(property, "[InlineEditor]", "对象引用（UnityEngine.Object）"));
                CallNextDrawer(property, label);
                return;
            }

            if (!HasAnyPanel(attribute))
            {
                DrawerWarnings.Once(property, nameof(InlineEditorDrawer) + ".no-panel",
                    $"[XInspector] 属性「{property.Path}」上的 [InlineEditor] 三面旗" +
                    "（DrawGUI / DrawHeader / DrawPreview）全是 false，什么都不会画，" +
                    "该特性已忽略、字段退回普通绘制。");
                CallNextDrawer(property, label);
                return;
            }

            var state = property.State.GetOrCreate<InlineEditorState>();
            var target = serializedProperty.objectReferenceValue;

            if (!InlineEditorLayout.CanInline(serializedProperty.hasMultipleDifferentValues, target != null))
            {
                // 空值或混合值：没有可内嵌的东西，按对象字段模式收尾。
                DrawObjectField(property, attribute, label, target);
                return;
            }

            // 先问能不能进，再动笔画：被拒时整段退回普通绘制，而不是「字段画完了才发现内嵌不了」
            // ——后者在 CompletelyHidden 下会留下一片空白。
            var entered = InlineEditorDrawContext.TryEnter(
                target, attribute.IncrementInlineEditorDrawerDepth, out var scope);

            if (entered != InlineEditorEnterResult.Entered)
            {
                ReportEnterFailure(property, entered);
                CallNextDrawer(property, label);
                return;
            }

            using (scope)
            {
                DrawObjectFieldAndPanels(property, attribute, label, state, target);
            }
        }

        #endregion

        #region 对象字段段

        /// <summary>
        /// 按对象字段模式画出字段（可能还有一行提示），**不画内嵌内容**。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="attribute">特性。</param>
        /// <param name="label">标签。</param>
        /// <param name="target">被引用对象；可能为 <c>null</c>。</param>
        /// <remarks>
        /// 折叠与装箱都是**为内嵌内容服务**的：没有内容时退化成普通字段，
        /// 免得出现「点开是空的折叠头」或「框里只有一行字段」。
        /// </remarks>
        private void DrawObjectField(InspectorProperty property, InlineEditorAttribute attribute, GUIContent label, Object target)
        {
            if (attribute.ObjectFieldMode == InlineEditorObjectFieldModes.Foldout ||
                attribute.ObjectFieldMode == InlineEditorObjectFieldModes.Boxed)
            {
                CallNextDrawer(property, label);
                return;
            }

            var plan = InlineEditorLayout.ResolveObjectField(target != null, attribute.ObjectFieldMode);

            if (plan.DrawField)
            {
                CallNextDrawer(property, label);
            }

            if (plan.ShowHint)
            {
                DrawHiddenHint();
            }
        }

        /// <summary>
        /// 画对象字段（按模式装箱 / 折叠 / 隐藏）+ 内嵌内容。
        /// </summary>
        private void DrawObjectFieldAndPanels(InspectorProperty property, InlineEditorAttribute attribute, GUIContent label, InlineEditorState state, Object target)
        {
            switch (attribute.ObjectFieldMode)
            {
                case InlineEditorObjectFieldModes.Boxed:
                    // 框把字段与内嵌内容一起罩住——它们是一件事。
                    using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                    {
                        CallNextDrawer(property, label);
                        DrawPanels(property, attribute, state, target);
                    }

                    return;

                case InlineEditorObjectFieldModes.Foldout:
                    EnsureFoldoutInitialized(state, attribute);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        state.Expanded = EditorGUILayout.Foldout(state.Expanded, GUIContent.none, true);
                        CallNextDrawer(property, label);
                    }

                    if (state.Expanded)
                    {
                        DrawPanels(property, attribute, state, target);
                    }

                    return;

                default:
                    // Hidden / CompletelyHidden：字段可能有、可能没有，内嵌内容照画。
                    DrawObjectField(property, attribute, label, target);
                    DrawPanels(property, attribute, state, target);
                    return;
            }
        }

        /// <summary>折叠头没有显式初值时默认展开——折叠着的内嵌编辑器等于没画。</summary>
        private static void EnsureFoldoutInitialized(InlineEditorState state, InlineEditorAttribute attribute)
        {
            if (state.Initialized)
            {
                return;
            }

            state.Initialized = true;
            state.Expanded = InlineEditorLayout.ResolveInitialExpanded(attribute.ExpandedHasValue, attribute.Expanded);
        }

        /// <summary>画「字段被隐藏」的提示——本包不接受静默空白。</summary>
        private static void DrawHiddenHint()
        {
            EditorGUILayout.LabelField("[InlineEditor] 隐藏了对象字段（CompletelyHidden）", EditorStyles.miniLabel);
        }

        #endregion

        #region 内嵌内容

        /// <summary>
        /// 取（或建）被引用对象的编辑器，按三面旗画出头 / 预览 / 界面。
        /// </summary>
        private void DrawPanels(InspectorProperty property, InlineEditorAttribute attribute, InlineEditorState state, Object target)
        {
            var editor = state.GetOrCreateEditor(target);

            if (editor == null)
            {
                DrawerWarnings.Once(property, nameof(InlineEditorDrawer) + ".no-editor",
                    $"[XInspector] 属性「{property.Path}」无法为引用的对象创建编辑器，" +
                    "[InlineEditor] 只画出了对象字段。");
                return;
            }

            if (attribute.DrawHeader)
            {
                editor.DrawHeader();
            }

            if (attribute.DrawGUI && attribute.DrawPreview)
            {
                DrawGuiAndPreview(property, editor, attribute, state, target);
                return;
            }

            if (attribute.DrawPreview)
            {
                // 单独画预览：用 PreviewHeight（不再是列宽）。
                DrawPreviewBox(editor, attribute, state, target);
                return;
            }

            DrawEditorGUI(property, editor, attribute, state, target);
        }

        /// <summary>界面与预览同画：左右并排或上下堆叠，谁在前由对齐方式定。</summary>
        private void DrawGuiAndPreview(InspectorProperty property, UnityEditor.Editor editor, InlineEditorAttribute attribute, InlineEditorState state, Object target)
        {
            var previewFirst = InlineEditorLayout.PreviewComesFirst(attribute.PreviewAlignment);

            if (InlineEditorLayout.IsSideBySide(attribute.PreviewAlignment))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (previewFirst)
                    {
                        DrawPreviewColumn(editor, attribute, state, target);
                        DrawEditorGUI(property, editor, attribute, state, target);
                    }
                    else
                    {
                        DrawEditorGUI(property, editor, attribute, state, target);
                        DrawPreviewColumn(editor, attribute, state, target);
                    }
                }

                return;
            }

            if (previewFirst)
            {
                DrawPreviewBox(editor, attribute, state, target);
                DrawEditorGUI(property, editor, attribute, state, target);
            }
            else
            {
                DrawEditorGUI(property, editor, attribute, state, target);
                DrawPreviewBox(editor, attribute, state, target);
            }
        }

        /// <summary>并排时的预览列：定宽，高度取预览高度。</summary>
        private static void DrawPreviewColumn(UnityEditor.Editor editor, InlineEditorAttribute attribute, InlineEditorState state, Object target)
        {
            var width = InlineEditorLayout.ResolvePreviewWidth(attribute.PreviewWidth, EditorGUIUtility.currentViewWidth);
            var height = InlineEditorLayout.ResolvePreviewHeight(attribute.PreviewHeight);

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width)))
            {
                var rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
                DrawPreviewInto(editor, state, target, rect);
            }
        }

        /// <summary>单独/堆叠时的预览：占一行的正方形，靠左。</summary>
        private static void DrawPreviewBox(UnityEditor.Editor editor, InlineEditorAttribute attribute, InlineEditorState state, Object target)
        {
            var height = InlineEditorLayout.ResolvePreviewHeight(attribute.PreviewHeight);
            var row = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));

            DrawPreviewInto(editor, state, target, PreviewFieldLayout.PreviewRect(row, height, ObjectFieldAlignment.Left));
        }

        /// <summary>
        /// 把一个预览画进矩形：优先用被嵌对象自己的预览，取不到就退回共用方块。
        /// </summary>
        /// <param name="editor">被嵌编辑器。</param>
        /// <param name="state">本属性的状态（贴图缓存挂在它上面）。</param>
        /// <param name="target">被引用对象。</param>
        /// <param name="rect">目标矩形。</param>
        /// <remarks>
        /// 第二支复用 <see cref="PreviewFieldGUI.DrawBox"/>——材质球、网格这类对象通常有
        /// 自己的预览，而脚本、纯数据资产没有，那时退回「贴图或对象名字」的方块，
        /// 不留白（与 <c>[PreviewField]</c> 同一条规矩）。
        /// </remarks>
        private static void DrawPreviewInto(UnityEditor.Editor editor, InlineEditorState state, Object target, Rect rect)
        {
            if (editor.HasPreviewGUI())
            {
                editor.DrawPreview(rect);
                return;
            }

            PreviewFieldGUI.DrawBox(rect, target, state.Preview);
        }

        /// <summary>
        /// 画出被嵌编辑器自己的界面。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="editor">被嵌编辑器。</param>
        /// <param name="attribute">特性。</param>
        /// <param name="state">本属性的状态（滚动位置挂在它上面）。</param>
        /// <param name="target">被引用对象。</param>
        /// <remarks>
        /// 缩进加一层表示从属关系；只读属性（<c>[ReadOnly]</c>/<c>[DisableIf]</c> 等）与
        /// 被版本控制锁定的资产一并置灰。
        /// </remarks>
        private void DrawEditorGUI(InspectorProperty property, UnityEditor.Editor editor, InlineEditorAttribute attribute, InlineEditorState state, Object target)
        {
            var locked = IsLockedByVcs(attribute, target);
            var previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;

            try
            {
                using (new EditorGUI.DisabledScope(property.State.IsReadOnly || locked))
                {
                    if (!InlineEditorLayout.UsesScrollView(attribute.MaxHeight))
                    {
                        DrawNestedEditor(editor);
                        return;
                    }

                    state.Scroll = EditorGUILayout.BeginScrollView(
                        state.Scroll, GUILayout.MaxHeight(attribute.MaxHeight));

                    try
                    {
                        DrawNestedEditor(editor);
                    }
                    finally
                    {
                        EditorGUILayout.EndScrollView();
                    }
                }
            }
            finally
            {
                EditorGUI.indentLevel = previousIndent;
            }
        }

        /// <summary>
        /// 调被嵌编辑器自己的绘制入口。
        /// </summary>
        /// <param name="editor">被嵌编辑器。</param>
        /// <remarks>
        /// 外面包一层 <c>Update</c> / <c>ApplyModifiedProperties</c>：不假设被嵌的那个编辑器
        /// 自己会做这件事——它做了，这层是空转；它没做，漏掉就会显示陈旧值或丢改动。
        /// 代价是内嵌里的编辑**会进 Undo**（Inspector 路径上外层本来就是这么做的）。
        /// </remarks>
        private static void DrawNestedEditor(UnityEditor.Editor editor)
        {
            editor.serializedObject.Update();
            editor.OnInspectorGUI();
            editor.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 内嵌内容是否要因版本控制而置灰。
        /// </summary>
        /// <param name="attribute">特性。</param>
        /// <param name="target">被引用对象。</param>
        /// <returns>该置灰返回 <c>true</c>。</returns>
        /// <remarks>
        /// 规则本身在 <see cref="InlineEditorVcs.IsLockedForEditing"/>（可无头测试）；
        /// 这里只负责把两个查询喂给它，且**只在必要时查**——每帧一次原生调用不该白付。
        /// </remarks>
        private static bool IsLockedByVcs(InlineEditorAttribute attribute, Object target)
        {
            if (!attribute.DisableGUIForVCSLockedAssets)
            {
                return false;
            }

            var isAsset = AssetDatabase.Contains(target);

            return InlineEditorVcs.IsLockedForEditing(
                isAsset,
                !isAsset || AssetDatabase.IsOpenForEdit(target, StatusQueryOptions.UseCachedIfPossible));
        }

        #endregion

        #region Private Helpers

        /// <summary>三面旗里有没有一面是开的。</summary>
        private static bool HasAnyPanel(InlineEditorAttribute attribute)
        {
            return attribute.DrawGUI || attribute.DrawHeader || attribute.DrawPreview;
        }

        /// <summary>报「进不去内嵌上下文」的原因，并说明已降级。</summary>
        private static void ReportEnterFailure(InspectorProperty property, InlineEditorEnterResult result)
        {
            var reason = result == InlineEditorEnterResult.Cycle
                ? "引用成了环（该对象已在嵌套链上）"
                : "嵌套深度已达上限";

            DrawerWarnings.Once(property, nameof(InlineEditorDrawer) + "." + result,
                $"[XInspector] 属性「{property.Path}」上的 [InlineEditor] 未内嵌：{reason}，已退回普通对象字段绘制。");
        }

        #endregion
    }

    /// <summary>
    /// <c>[InlineEditor]</c> 的每属性状态：被嵌编辑器的实例、折叠与滚动位置、预览贴图缓存。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 绘制器是共享单例，这些可变数据只能放这里。实现 <see cref="IDisposable"/> 是**必须**的：
    /// 持有的 <c>Editor</c> 实例是原生对象，没人释放就会随每次选中/关闭累积——
    /// 释放链见 <see cref="PropertyState.Reset"/> 与 <see cref="PropertyTree.Dispose"/>。
    /// </para>
    /// <para>
    /// 预览缓存**组合**一个 <see cref="PreviewFieldState"/> 而不是直接复用袋里的同类型实例：
    /// 袋按类型取，同一个字段上同时挂 <c>[PreviewField]</c> 与 <c>[InlineEditor]</c> 时会撞键。
    /// </para>
    /// </remarks>
    internal sealed class InlineEditorState : IDisposable
    {
        #region Public Fields

        /// <summary>折叠头是否已初始化（初值来自特性的 <c>Expanded</c>）。</summary>
        public bool Initialized;

        /// <summary>折叠头是否展开。</summary>
        public bool Expanded;

        /// <summary>内嵌滚动视图的当前位置。</summary>
        public Vector2 Scroll;

        /// <summary>预览贴图缓存。</summary>
        public readonly PreviewFieldState Preview = new PreviewFieldState();

        #endregion

        #region Private Fields

        private UnityEditor.Editor _editor;
        private Object _editorTarget;

        #endregion

        #region Public API

        /// <summary>
        /// 取被引用对象的编辑器，没有或目标换了就建一个。
        /// </summary>
        /// <param name="target">被引用对象。</param>
        /// <returns>编辑器实例；创建失败时为 <c>null</c>。</returns>
        /// <remarks>
        /// 目标比较走 Unity 的 <c>==</c>（已销毁对象判定为 <c>null</c>，正合语义），
        /// **不比对 ID**——本包刻意不用 <c>GetInstanceID()</c>（Unity 6.4 已弃用，
        /// 而替代品 <c>GetEntityId()</c> 在包声明的最低版本 6000.3 上还不存在）。
        /// </remarks>
        public UnityEditor.Editor GetOrCreateEditor(Object target)
        {
            if (_editor != null && _editorTarget == target)
            {
                return _editor;
            }

            DisposeEditor();

            _editorTarget = target;
            _editor = UnityEditor.Editor.CreateEditor(target);

            if (_editor != null)
            {
                // 要的只是「不序列化、不被 UnloadUnusedAssets 回收」两条，故取 DontSave。
                // 不取 HideAndDontSave：它捎带的 NotEditable / HideInHierarchy 是给资产看的，
                // 对 Editor 实例没有意义，而 NotEditable 的语义我们并不想请进来。
                _editor.hideFlags = HideFlags.DontSave;
            }

            return _editor;
        }

        /// <summary>销毁持有的编辑器实例；重复调用是空操作。</summary>
        public void Dispose()
        {
            DisposeEditor();
        }

        #endregion

        #region Private Helpers

        /// <summary>丢掉当前编辑器实例与它的目标记录。</summary>
        private void DisposeEditor()
        {
            if (_editor != null)
            {
                // 编辑模式下必须用 DestroyImmediate——Destroy 会报「may not be called from edit mode」。
                // 销毁会触发被销毁编辑器的 OnDisable，它自己那棵树顺着同一条链释放。
                Object.DestroyImmediate(_editor);
            }

            _editor = null;
            _editorTarget = null;
        }

        #endregion
    }
}
