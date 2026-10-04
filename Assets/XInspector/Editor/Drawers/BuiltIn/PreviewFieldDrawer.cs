using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="PreviewFieldAttribute"/>：对象字段画成「预览方块 + 可编辑的对象字段」。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 替换型绘制器（自己画完、不调用下一个），故**必须自己套
    /// <see cref="EditorGUI.DisabledScope"/>**。
    /// </para>
    /// <para>
    /// 几何全在纯函数 <see cref="PreviewFieldLayout"/> 里，贴图选取在
    /// <see cref="PreviewFieldContent"/> 里，方块的画法在 <see cref="PreviewFieldGUI"/> 里
    /// （与内嵌编辑器的预览列共用）；本类只负责摆放与调用。
    /// </para>
    /// <para>
    /// 与 Odin 的差异见特性的类注释（方块是预览不是控件、默认高度与对齐由本包定、
    /// 两个 <c>FilterMode</c> 重载永久不做）。
    /// </para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class PreviewFieldDrawer : AttributeDrawer<PreviewFieldAttribute>
    {
        #region Private Fields

        /// <summary>预览方块与对象字段之间、以及标签与内容之间的间距（像素）。</summary>
        private const float Gap = 2f;

        /// <summary>同排时留给对象字段的最小宽度（像素）。窄于此就把字段排到下一行。</summary>
        private const float MinFieldWidth = 60f;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, PreviewFieldAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.ObjectReference)
            {
                DrawerWarnings.Once(property, nameof(PreviewFieldDrawer),
                    DrawerWarnings.TypeMismatch(property, "[PreviewField]", "对象引用（UnityEngine.Object）"));
                CallNextDrawer(property, label);
                return;
            }

            var height = PreviewFieldLayout.ResolveHeight(attribute.Height);

            var rowRect = EditorGUILayout.GetControlRect(true, height);

            EditorGUI.BeginProperty(rowRect, label, serializedProperty);

            var content = label != null && label != GUIContent.none ? EditorGUI.PrefixLabel(rowRect, label) : rowRect;
            var previewRect = PreviewFieldLayout.PreviewRect(content, height, attribute.Alignment);

            PreviewFieldGUI.DrawBox(previewRect, serializedProperty.objectReferenceValue,
                property.State.GetOrCreate<PreviewFieldState>());

            var hasRoom = PreviewFieldLayout.FitsBeside(content, height, MinFieldWidth, Gap);

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                if (hasRoom)
                {
                    // SerializedProperty 重载：类型限制、预制体覆盖、右键菜单都由 Unity 自己处理。
                    var fieldRect = PreviewFieldLayout.FieldRectBeside(content, previewRect, attribute.Alignment, Gap);
                    EditorGUI.ObjectField(fieldRect, serializedProperty, GUIContent.none);
                }
            }

            EditorGUI.EndProperty();

            if (!hasRoom)
            {
                // 窄：方块自己一行，对象字段另起一行——把字段挤成一条缝等于值改不了。
                using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
                {
                    EditorGUILayout.PropertyField(serializedProperty, label, true);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// 预览方块的画法：有贴图画贴图，没贴图画对象名字。**两处共用**——
    /// <see cref="PreviewFieldDrawer"/> 与内嵌编辑器的预览列。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 贴图取不到时**不能什么都不画**——一个空白的方块看不出「这里没东西」还是「画挂了」。
    /// </para>
    /// <para>
    /// 不在预览没生成好时主动请求重绘：<c>AssetPreview</c> 生成完会自己让 Inspector 重画，
    /// 而本包没有「请求重绘」这条现成通路（<see cref="InspectorProperty"/> 上就没有），
    /// 为此新开一条不值得。代价是那一帧画的是小图标，下一帧换成真预览。
    /// </para>
    /// </remarks>
    internal static class PreviewFieldGUI
    {
        #region Public API

        /// <summary>
        /// 把一个预览方块画进指定矩形。
        /// </summary>
        /// <param name="rect">方块区域。</param>
        /// <param name="target">方块代表的对象；为 <c>null</c> 时只画空框。</param>
        /// <param name="state">贴图缓存，由调用方提供（缓存归属随调用方）。</param>
        public static void DrawBox(Rect rect, Object target, PreviewFieldState state)
        {
            var texture = PreviewFieldContent.ResolveTexture(target, state);

            GUI.Box(rect, GUIContent.none);

            var inner = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);

            if (texture != null)
            {
                GUI.DrawTexture(inner, texture, ScaleMode.ScaleToFit, true);
                return;
            }

            if (target == null)
            {
                return;
            }

            var previousColor = GUI.color;
            try
            {
                GUI.color = Color.gray;
                GUI.Label(inner, new GUIContent(target.name), EditorStyles.centeredGreyMiniLabel);
            }
            finally
            {
                GUI.color = previousColor;
            }
        }

        #endregion
    }

    /// <summary>
    /// <c>[PreviewField]</c> 的几何：全是纯函数，可无头测试。
    /// </summary>
    internal static class PreviewFieldLayout
    {
        #region Public API

        /// <summary>未指定高度时用的边长（像素）。</summary>
        public const float DefaultHeight = 64f;

        /// <summary>
        /// 把特性上的高度换算成实际边长。
        /// </summary>
        /// <param name="height">特性上的高度；非正表示未指定。</param>
        /// <returns>实际边长。</returns>
        public static float ResolveHeight(float height)
        {
            return height > 0f ? height : DefaultHeight;
        }

        /// <summary>
        /// 按对齐方式在可用宽度里放下预览方块。
        /// </summary>
        /// <param name="content">可用区域（标签已被扣掉）。</param>
        /// <param name="height">方块边长。</param>
        /// <param name="alignment">对齐方式。</param>
        /// <returns>方块矩形（正方形，边长不超过可用宽度）。</returns>
        public static Rect PreviewRect(Rect content, float height, ObjectFieldAlignment alignment)
        {
            var side = Mathf.Min(height, content.width);

            switch (alignment)
            {
                case ObjectFieldAlignment.Center:
                    return new Rect(content.x + ((content.width - side) * 0.5f), content.y, side, content.height);
                case ObjectFieldAlignment.Right:
                    return new Rect(content.xMax - side, content.y, side, content.height);
                default:
                    return new Rect(content.x, content.y, side, content.height);
            }
        }

        /// <summary>
        /// 判断同排还放不放得下对象字段。
        /// </summary>
        /// <param name="content">可用区域。</param>
        /// <param name="height">方块边长。</param>
        /// <param name="minFieldWidth">对象字段的最小宽度。</param>
        /// <param name="gap">间距。</param>
        /// <returns>放得下返回 <c>true</c>。</returns>
        /// <remarks>
        /// 放不下时调用方把字段排到**下一行**，而不是把它挤成一条缝——
        /// 一条几像素宽的对象字段既看不清也点不着，等于把值改不了。
        /// </remarks>
        public static bool FitsBeside(Rect content, float height, float minFieldWidth, float gap)
        {
            var side = Mathf.Min(height, content.width);
            return content.width >= side + minFieldWidth + gap;
        }

        /// <summary>
        /// 方块在左时字段在右、方块在右时字段在左；居中时字段放右边。
        /// </summary>
        /// <param name="content">可用区域。</param>
        /// <param name="preview">方块矩形。</param>
        /// <param name="alignment">对齐方式。</param>
        /// <param name="gap">间距。</param>
        /// <returns>对象字段的矩形。</returns>
        public static Rect FieldRectBeside(Rect content, Rect preview, ObjectFieldAlignment alignment, float gap)
        {
            if (alignment == ObjectFieldAlignment.Right)
            {
                return new Rect(content.x, content.y, Mathf.Max(0f, preview.x - content.x - gap), content.height);
            }

            var x = preview.xMax + gap;
            return new Rect(x, content.y, Mathf.Max(0f, content.xMax - x), content.height);
        }

        #endregion
    }

    /// <summary>
    /// <c>[PreviewField]</c> 的贴图选取与缓存。
    /// </summary>
    /// <remarks>
    /// 资产预览是**异步**生成的：第一次问往往拿到 <c>null</c>，要几帧之后才有。
    /// 故这里缓存「上一次拿到的贴图 + 它是哪个对象的」，避免每帧重新问、
    /// 也避免对象换掉后还贴着旧图。
    /// </remarks>
    internal static class PreviewFieldContent
    {
        #region Public API

        /// <summary>
        /// 取要画的贴图，必要时更新缓存。
        /// </summary>
        /// <param name="target">对象引用。</param>
        /// <param name="state">该属性上的缓存。</param>
        /// <returns>贴图；没有可画的返回 <c>null</c>。</returns>
        public static Texture ResolveTexture(Object target, PreviewFieldState state)
        {
            if (target == null)
            {
                state.Clear();
                return null;
            }

            if (state.CachedFor == target && state.Cached != null)
            {
                return state.Cached;
            }

            var preview = AssetPreview.GetAssetPreview(target);
            if (preview != null)
            {
                state.Cached = preview;
                state.CachedFor = target;
                return preview;
            }

            // 还没有真预览（多半是还在生成）：先用小图标顶着，别让方块空着。
            // **不缓存**这个过渡态——缓存住就再也不会升级成真预览了。
            return AssetPreview.GetMiniThumbnail(target);
        }

        #endregion
    }

    /// <summary>
    /// <c>[PreviewField]</c> 的每属性状态，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    internal sealed class PreviewFieldState
    {
        /// <summary>缓存下来的真预览贴图。</summary>
        public Texture Cached;

        /// <summary>
        /// 缓存对应的对象。
        /// </summary>
        /// <remarks>
        /// 存**引用**而不是 <c>GetInstanceID()</c>：后者在 Unity 6.4 已标记弃用
        /// （提示改用 <c>GetEntityId()</c>），而 <c>GetEntityId()</c> 在 6000.3 上还不存在，
        /// 用了会撞破本包声明的最低版本。存引用两者都不欠，比较走 Unity 的 <c>==</c>
        /// （它会把已销毁的对象当 null，正合此处语义）。
        /// </remarks>
        public Object CachedFor;

        /// <summary>清空缓存（对象被清空时调）。</summary>
        public void Clear()
        {
            Cached = null;
            CachedFor = null;
        }
    }
}
