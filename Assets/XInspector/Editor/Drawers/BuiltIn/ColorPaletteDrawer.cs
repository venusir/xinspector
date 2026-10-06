using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="ColorPaletteAttribute"/>：在字段**上方**画一行调色板色块，点一下填进字段。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>透传型</b>：画完色块**照常调用下一个绘制器**，原生颜色字段仍在下面。
    /// 理由与 <c>[AssetSelector]</c> 同款——调色板是**快捷入口**，不是值控件：
    /// 精确值、alpha、吸管都在原生控件里，替换掉它是能力倒退。
    /// 顺带的便宜：只读罩与多选混合态由末端那层照常处理，这里不必自己再造一遍。
    /// </para>
    /// <para>
    /// <b>多选值不一致时整行色块不画</b>（只留原生字段）：点一下会把主目标的颜色铺到全部
    /// 目标上，那是静默改数据——与集合绘制器在多选下禁用增删按钮同一条理由。
    /// </para>
    /// <para>几何全在纯函数 <see cref="ColorPaletteLayout"/> 里，本类只负责画与落值。</para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class ColorPaletteDrawer : AttributeDrawer<ColorPaletteAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property, ColorPaletteAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null ||
                serializedProperty.propertyType != SerializedPropertyType.Color ||
                serializedProperty.isArray)
            {
                // 反射成员（没有序列化后端）与不是 Color 的字段都走这里。文案由
                // DrawerWarnings 给——它对 [ShowInInspector] 的只读成员有专门的措辞。
                DrawerWarnings.Once(
                    property,
                    nameof(ColorPaletteDrawer) + ".target",
                    DrawerWarnings.TypeMismatch(property, "[ColorPalette]", "Color 单值成员"));
                CallNextDrawer(property, label);
                return;
            }

            var palette = ResolvePalette(property, attribute);

            if (palette == null)
            {
                CallNextDrawer(property, label);
                return;
            }

            var colors = palette.Colors;

            if (colors.Count == 0)
            {
                DrawerWarnings.Once(
                    property,
                    nameof(ColorPaletteDrawer) + ".empty",
                    $"[XInspector] 属性「{property.Path}」上的 [ColorPalette] 指向的调色板「{palette.name}」是空的，" +
                    "色块行不画（字段本身照常可用）。");
                CallNextDrawer(property, label);
                return;
            }

            DrawSwatches(property, serializedProperty, colors);
            CallNextDrawer(property, label);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 找到要用的调色板；找不到就告警并给 <c>null</c>（调用方退回普通绘制）。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="attribute">特性实例。</param>
        /// <returns>调色板；找不到为 <c>null</c>。</returns>
        private static XInspectorColorPalette ResolvePalette(
            InspectorProperty property, ColorPaletteAttribute attribute)
        {
            if (!string.IsNullOrEmpty(attribute.PaletteName))
            {
                var byName = ColorPaletteQuery.Find(attribute.PaletteName);

                if (byName == null)
                {
                    DrawerWarnings.Once(
                        property,
                        nameof(ColorPaletteDrawer) + ".missing",
                        $"[XInspector] 属性「{property.Path}」上的 [ColorPalette] 找不到名为「{attribute.PaletteName}」" +
                        "的调色板（找的是工程里的 XInspectorColorPalette 资产，**资产文件名就是调色板名**）。" +
                        "该特性已忽略，字段退回普通绘制。");
                }

                return byName;
            }

            var all = ColorPaletteQuery.All();

            if (all.Count == 1)
            {
                return all[0];
            }

            DrawerWarnings.Once(
                property,
                nameof(ColorPaletteDrawer) + ".single",
                all.Count == 0
                    ? $"[XInspector] 属性「{property.Path}」上的 [ColorPalette]（无参形态）要求工程里恰好有一份" +
                      "调色板资产，现在**一份都没有**（建一份：右键 Create/XInspector/Color Palette）。" +
                      "该特性已忽略，字段退回普通绘制。"
                    : $"[XInspector] 属性「{property.Path}」上的 [ColorPalette]（无参形态）要求工程里恰好有一份" +
                      $"调色板资产，现在有 {all.Count} 份（{NamesOf(all)}）——要么只留一份，" +
                      "要么改成具名形态 [ColorPalette(\"名字\")]。该特性已忽略，字段退回普通绘制。");

            return null;
        }

        /// <summary>把候选的调色板名列成一句人话（告警里用）。</summary>
        /// <param name="palettes">全部调色板。</param>
        /// <returns>顿号分隔的名字。</returns>
        private static string NamesOf(IReadOnlyList<XInspectorColorPalette> palettes)
        {
            var names = new string[palettes.Count];

            for (var i = 0; i < palettes.Count; i++)
            {
                names[i] = palettes[i].name;
            }

            return string.Join("、", names);
        }

        /// <summary>
        /// 画色块网格：每格一个透明按钮，点中就把颜色写进字段。
        /// </summary>
        /// <param name="property">目标属性（只读取自它）。</param>
        /// <param name="serializedProperty">目标的序列化属性（写值）。</param>
        /// <param name="colors">调色板里的颜色。</param>
        private static void DrawSwatches(
            InspectorProperty property, SerializedProperty serializedProperty, IReadOnlyList<Color> colors)
        {
            if (serializedProperty.hasMultipleDifferentValues)
            {
                // 多选值不一致：点一下会把主目标的颜色铺到全部目标上，静默改数据。整行不画。
                return;
            }

            var width = EditorGUIUtility.currentViewWidth - 40f; // 扣掉大约的行首缩进
            var block = EditorGUILayout.GetControlRect(false, ColorPaletteLayout.BlockHeightOf(colors.Count, width));

            var columns = ColorPaletteLayout.ColumnsOf(block.width);
            var current = serializedProperty.colorValue;

            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                for (var i = 0; i < colors.Count; i++)
                {
                    var rect = ColorPaletteLayout.SwatchRect(block, i, columns);

                    EditorGUI.DrawRect(rect, colors[i]);

                    // 当前值命中某一格时描一圈——用户一眼看得出「现在用的是这一格」。
                    if (ColorPaletteLayout.SameColor(current, colors[i]))
                    {
                        EditorGUI.DrawRect(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, 1f), Color.white);
                        EditorGUI.DrawRect(new Rect(rect.x - 1f, rect.yMax, rect.width + 2f, 1f), Color.white);
                        EditorGUI.DrawRect(new Rect(rect.x - 1f, rect.y - 1f, 1f, rect.height + 2f), Color.white);
                        EditorGUI.DrawRect(new Rect(rect.xMax, rect.y - 1f, 1f, rect.height + 2f), Color.white);
                    }

                    // 透明按钮当命中区：不画任何东西，只吃点击（与手工判 Event.current 相比，
                    // 它自动处理悬停/按下/禁用，也不必自己分配控制 ID）。
                    if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                    {
                        serializedProperty.colorValue = colors[i];
                    }
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// <c>[ColorPalette]</c> 色块行的**几何**——全是纯函数，可无头测试。
    /// <para>
    /// 本仓不测 IMGUI，但「几行几列、每格在哪」是画之前就该对的账，故它必须能算。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 两个尺寸都是**本包自定值**（Odin 的对应旋钮没核到）：格子边长 16 像素、间距 2 像素——
    /// 与 <c>[AssetList]</c> 的缩略图同档。放不下就**折行**，不缩格子：缩到几像素等于点不着。
    /// </remarks>
    internal static class ColorPaletteLayout
    {
        #region Public API

        /// <summary>每格边长（像素）。</summary>
        public const float SwatchSize = 16f;

        /// <summary>格与格之间的间距（像素）。</summary>
        public const float SwatchGap = 2f;

        /// <summary>一行放得下几格。</summary>
        /// <param name="width">可用宽度。</param>
        /// <returns>列数，**至少 1**（宽度再窄也得画点什么）。</returns>
        public static int ColumnsOf(float width)
        {
            var columns = Mathf.FloorToInt((width + SwatchGap) / (SwatchSize + SwatchGap));
            return Mathf.Max(1, columns);
        }

        /// <summary>需要几行。</summary>
        /// <param name="colorCount">颜色个数。</param>
        /// <param name="columns">列数。</param>
        /// <returns>行数；没有颜色时为 0。</returns>
        public static int RowCountOf(int colorCount, int columns)
        {
            return columns <= 0 || colorCount <= 0 ? 0 : (colorCount + columns - 1) / columns;
        }

        /// <summary>整块色块区的高度。</summary>
        /// <param name="colorCount">颜色个数。</param>
        /// <param name="width">可用宽度。</param>
        /// <returns>像素高度；没有颜色时为 0（不占位）。</returns>
        public static float BlockHeightOf(int colorCount, float width)
        {
            var rows = RowCountOf(colorCount, ColumnsOf(width));
            return rows == 0 ? 0f : (rows * SwatchSize) + ((rows - 1) * SwatchGap);
        }

        /// <summary>第 <paramref name="index"/> 格的矩形。</summary>
        /// <param name="block">整块色块区。</param>
        /// <param name="index">下标（按行优先排布）。</param>
        /// <param name="columns">列数。</param>
        /// <returns>这一格的矩形。</returns>
        public static Rect SwatchRect(Rect block, int index, int columns)
        {
            var column = columns <= 0 ? 0 : index % columns;
            var row = columns <= 0 ? 0 : index / columns;

            return new Rect(
                block.x + (column * (SwatchSize + SwatchGap)),
                block.y + (row * (SwatchSize + SwatchGap)),
                SwatchSize,
                SwatchSize);
        }

        /// <summary>
        /// 两个颜色算不算同一个（描边高亮用）。
        /// </summary>
        /// <param name="left">左。</param>
        /// <param name="right">右。</param>
        /// <returns>同一个返回 <c>true</c>。</returns>
        /// <remarks>
        /// 按 1/255 的容差比：颜色字段经原生取色器来回一趟之后，float 不必逐位相同，
        /// 而「明明选的就是这一格、高亮却不亮」比不高亮更让人困惑。
        /// </remarks>
        public static bool SameColor(Color left, Color right)
        {
            const float Tolerance = 1f / 255f;

            return Mathf.Abs(left.r - right.r) <= Tolerance
                && Mathf.Abs(left.g - right.g) <= Tolerance
                && Mathf.Abs(left.b - right.b) <= Tolerance
                && Mathf.Abs(left.a - right.a) <= Tolerance;
        }

        #endregion
    }
}
