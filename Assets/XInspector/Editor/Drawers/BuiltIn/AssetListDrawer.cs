using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="AssetListAttribute"/> 的**单元素形态**：预览块 + 原生对象字段 + 「选择」。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>替换型绘制器</b>（值绘制带，与 <c>[PreviewField]</c> 同档）：自己画完、不调下一个——
    /// 预览块不是原生控件画得出来的。代价是自己照应只读罩（<c>[ReadOnly]</c> 一族的绘制器
    /// 在更内侧，绕过去就照不到了）。
    /// </para>
    /// <para>
    /// <b>列表形态在这里静默放行。</b> <c>AttributeDrawer.CanDraw</c> 是 sealed 的「有特性即命中」，
    /// 所以这个绘制器也会挂到列表字段的链上——那种字段归集合容器画（见
    /// <see cref="AssetListLayout"/>），这里既不该画也不该告警。
    /// </para>
    /// <para>
    /// 与 <c>[PreviewField]</c> 的差别：多一个「选择」按钮（按类型过滤的资产菜单）；
    /// 少两个旋钮（本包用固定值：方块 64 像素、在左——与 <c>[PreviewField]</c> 的默认一致）。
    /// 窄到排不下时与它同款：方块一行、字段另起一行（此时不画「选择」——原生字段自己的
    /// 对象选择器照旧可用）。
    /// </para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class AssetListDrawer : AttributeDrawer<AssetListAttribute>
    {
        #region Private Fields

        /// <summary>对象字段的最小宽度（像素）——排不下就把字段挪到下一行。</summary>
        private const float MinFieldWidth = 120f;

        /// <summary>方块与字段之间的间距（像素）。</summary>
        private const float Gap = 4f;

        /// <summary>「选择」按钮的标签（静态复用——绘制路径不新建 <see cref="GUIContent"/>）。</summary>
        private static readonly GUIContent SelectLabel = new GUIContent("▼", "从按类型过滤的资产里选一个");

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property, AssetListAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            // 列表形态归集合容器画——静默放行（不是错误，不需要告警）。
            if (CollectionDrawerLayout.CanDraw(serializedProperty))
            {
                CallNextDrawer(property, label);
                return;
            }

            if (serializedProperty == null ||
                serializedProperty.propertyType != SerializedPropertyType.ObjectReference)
            {
                DrawerWarnings.Once(property, nameof(AssetListDrawer),
                    DrawerWarnings.TypeMismatch(property, "[AssetList]", "对象引用（UnityEngine.Object）"));
                CallNextDrawer(property, label);
                return;
            }

            var model = property.State.Get<AssetListModel>();
            var height = PreviewFieldLayout.DefaultHeight;
            var rowRect = EditorGUILayout.GetControlRect(true, height);

            EditorGUI.BeginProperty(rowRect, label, serializedProperty);

            var content = label != null && label != GUIContent.none
                ? EditorGUI.PrefixLabel(rowRect, label)
                : rowRect;
            var previewRect = PreviewFieldLayout.PreviewRect(content, height, ObjectFieldAlignment.Left);

            PreviewFieldGUI.DrawBox(
                previewRect, serializedProperty.objectReferenceValue,
                property.State.GetOrCreate<PreviewFieldState>());

            var hasRoom = PreviewFieldLayout.FitsBeside(content, height, MinFieldWidth, Gap);

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                if (hasRoom)
                {
                    var fieldRect = PreviewFieldLayout.FieldRectBeside(
                        content, previewRect, ObjectFieldAlignment.Left, Gap);

                    AssetListLayout.SplitFieldAndButton(fieldRect, out var input, out var button);

                    // SerializedProperty 重载：类型限制、预制体覆盖、右键菜单都由 Unity 自己处理。
                    EditorGUI.ObjectField(input, serializedProperty, GUIContent.none);

                    // 没有模型就不画按钮——画一个点了没反应的按钮比不画更糟。
                    if (model != null && GUI.Button(button, SelectLabel, EditorStyles.miniButton))
                    {
                        AssetListMenu.Show(property, model, serializedProperty, append: false);
                    }
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
}
