using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[ColorPalette]</c> 的链装配与几何。
    /// <para>
    /// <b>测不了的</b>：色块行本身（画法与点击是 IMGUI，本仓不测），以及三条兜底告警——
    /// 它们发生在绘制期。故这里测「绘制器在不在链上」与「几行几列、每格在哪」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ColorPaletteDrawerTests
    {
        #region Setup / Teardown

        private ColorPaletteFixture _target;

        /// <summary>建临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ColorPaletteFixture>();
        }

        /// <summary>销毁临时资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 链装配

        /// <summary>绘制器在链上，且**排在末端之前**（它是透传型，末端才是原生颜色字段）。</summary>
        [Test]
        public void 绘制器在链上且排在末端之前()
        {
            using (var tree = PropertyTree.Create(new SerializedObject(_target)))
            {
                AssertDrawer(tree.Root, "named");
                AssertDrawer(tree.Root, "single");

                // 目标不是 Color 时它也照旧在链上（判据在绘制期，构建期不该悄悄摘掉它——
                // 摘掉的话告警就没地方说，症状变成「什么都不发生」）。
                AssertDrawer(tree.Root, "notAColor");
            }
        }

        #endregion

        #region 几何

        /// <summary>列数按可用宽度折算——含间距，且**至少 1 列**。</summary>
        [Test]
        public void 几何按行宽折算列数()
        {
            // (16 + 2) × n - 2 ≤ 宽度 ⇒ n = floor((宽度 + 2) / 18)
            Assert.That(ColorPaletteLayout.ColumnsOf(16f), Is.EqualTo(1));
            Assert.That(ColorPaletteLayout.ColumnsOf(17f), Is.EqualTo(1));
            Assert.That(ColorPaletteLayout.ColumnsOf(18f + 16f), Is.EqualTo(2));
            Assert.That(ColorPaletteLayout.ColumnsOf(0f), Is.EqualTo(1), "窄到没有也得画点什么。");
        }

        /// <summary>行数 = 向上取整；没有颜色时一行都不占。</summary>
        [Test]
        public void 几何折行后行数正确()
        {
            Assert.That(ColorPaletteLayout.RowCountOf(0, 4), Is.EqualTo(0));
            Assert.That(ColorPaletteLayout.RowCountOf(4, 4), Is.EqualTo(1));
            Assert.That(ColorPaletteLayout.RowCountOf(5, 4), Is.EqualTo(2));
            Assert.That(ColorPaletteLayout.RowCountOf(8, 4), Is.EqualTo(2));
        }

        /// <summary>整块高度：行数 × 边长 + 行间间距；空调色板不占位。</summary>
        [Test]
        public void 整块高度按行数折算()
        {
            Assert.That(ColorPaletteLayout.BlockHeightOf(0, 200f), Is.EqualTo(0f), "没有颜色就不占位。");
            Assert.That(ColorPaletteLayout.BlockHeightOf(1, 200f), Is.EqualTo(16f));
            Assert.That(ColorPaletteLayout.BlockHeightOf(2, 200f), Is.EqualTo(16f), "同一行还是那么高。");

            // 一行放 4 格：第 5 个颜色折到第二行，高度 = 16 + 2 + 16。
            var oneRow = ColorPaletteLayout.ColumnsOf(4f * 18f);
            Assert.That(ColorPaletteLayout.BlockHeightOf(oneRow + 1, 4f * 18f), Is.EqualTo(34f));
        }

        /// <summary>每格按**行优先**排布（先填满一行再换行）。</summary>
        [Test]
        public void 每格按行列排布()
        {
            var block = new Rect(10f, 20f, 100f, 34f);

            Assert.That(ColorPaletteLayout.SwatchRect(block, 0, 3), Is.EqualTo(new Rect(10f, 20f, 16f, 16f)));
            Assert.That(ColorPaletteLayout.SwatchRect(block, 1, 3), Is.EqualTo(new Rect(10f + 18f, 20f, 16f, 16f)));
            Assert.That(
                ColorPaletteLayout.SwatchRect(block, 3, 3),
                Is.EqualTo(new Rect(10f, 20f + 18f, 16f, 16f)),
                "第四个回到第二行行首。");
        }

        /// <summary>高亮判等按 1/255 的容差——取色器来回一趟之后 float 不必逐位相同。</summary>
        [Test]
        public void 同一个颜色按容差判定()
        {
            var red = new Color(1f, 0f, 0f, 1f);

            Assert.That(ColorPaletteLayout.SameColor(red, new Color(1f, 0f, 0f, 1f)), Is.True);
            Assert.That(
                ColorPaletteLayout.SameColor(red, new Color(1f - (0.5f / 255f), 0f, 0f, 1f)),
                Is.True,
                "差半格色阶算同一个。");
            Assert.That(ColorPaletteLayout.SameColor(red, new Color(1f, 0f, 0f, 0.5f)), Is.False, "alpha 不同就是不同。");
            Assert.That(ColorPaletteLayout.SameColor(red, Color.blue), Is.False);
        }

        #endregion

        #region Private Helpers

        /// <summary>断言某个成员节点的链上有这个绘制器，且它后面还有绘制器（不是末端）。</summary>
        /// <param name="root">树的根。</param>
        /// <param name="path">成员路径。</param>
        private static void AssertDrawer(InspectorProperty root, string path)
        {
            var node = Find(root, path);
            var entries = node.Chain.Entries;
            var index = -1;

            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Drawer is ColorPaletteDrawer)
                {
                    index = i;
                    break;
                }
            }

            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"{path} 的链上没有 ColorPaletteDrawer。");
            Assert.That(index, Is.LessThan(entries.Length - 1), "透传型：后面还要有末端绘制器。");
        }

        /// <summary>按路径查找直接子节点。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；不存在时断言失败。</returns>
        private static InspectorProperty Find(InspectorProperty parent, string path)
        {
            foreach (var child in parent.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            Assert.Fail($"找不到节点 {path}。");
            return null;
        }

        #endregion
    }

    /// <summary><c>[ColorPalette]</c> 的夹具：具名、无参与「目标不是 Color」三格。</summary>
    internal sealed class ColorPaletteFixture : ScriptableObject
    {
        /// <summary>具名形态的目标。</summary>
        [ColorPalette("Showcase")]
        public Color named = Color.white;

        /// <summary>无参形态的目标。</summary>
        [ColorPalette]
        public Color single = Color.black;

        /// <summary>目标不是 <c>Color</c>——绘制期告警并退回（链上照旧有它）。</summary>
        [ColorPalette("Showcase")]
        public int notAColor = 7;
    }
}
