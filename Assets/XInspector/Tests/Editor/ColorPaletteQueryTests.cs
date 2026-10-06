using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 调色板资产的查找：按名、取全部、失效后重扫。
    /// <para>
    /// 夹具往盘上写**真的资产**（扫描走 <see cref="AssetDatabase"/>，没有它测不出什么），
    /// 一次性目录 + 逐用例清理 + 收尾删目录，与预制体那套同款。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ColorPaletteQueryTests
    {
        #region Setup / Teardown

        private const string FolderName = "__XInspectorPaletteTests__";
        private const string TempFolder = "Assets/" + FolderName;

        private readonly List<string> _assetPaths = new List<string>();

        /// <summary>建临时资产目录（先清残留）。</summary>
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            DeleteTempFolder();
            AssetDatabase.CreateFolder("Assets", FolderName);
        }

        /// <summary>删掉整个临时目录，并复位查找的静态表。</summary>
        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            DeleteTempFolder();
            ColorPaletteQuery.Invalidate();
        }

        /// <summary>逐用例清理：本用例产生的资产 + 复位静态表（静态门面必须复位）。</summary>
        [TearDown]
        public void TearDown()
        {
            for (var i = _assetPaths.Count - 1; i >= 0; i--)
            {
                AssetDatabase.DeleteAsset(_assetPaths[i]);
            }

            _assetPaths.Clear();
            ColorPaletteQuery.Invalidate();
        }

        #endregion

        #region 查找

        /// <summary>按**资产名**找得到（名字就是工程窗口里显示的那个）。</summary>
        [Test]
        public void 按名找得到调色板()
        {
            var created = CreatePalette("UiColors");

            Assert.That(ColorPaletteQuery.Find("UiColors"), Is.SameAs(created));
        }

        /// <summary>名字大小写不敏感（本包自定语义，与 <c>[AssetList]</c> 的前缀比法同口径）。</summary>
        [Test]
        public void 名字不区分大小写()
        {
            var created = CreatePalette("UiColors");

            Assert.That(ColorPaletteQuery.Find("uicolors"), Is.SameAs(created));
        }

        /// <summary>找不到时给 <c>null</c>，不抛（拼错名字是常事）。</summary>
        [Test]
        public void 找不到时返回空()
        {
            CreatePalette("UiColors");

            Assert.That(ColorPaletteQuery.Find("不存在"), Is.Null);
            Assert.That(ColorPaletteQuery.Find(null), Is.Null);
            Assert.That(ColorPaletteQuery.Find(string.Empty), Is.Null);
        }

        /// <summary>
        /// 全部按名字排序——告警里要列候选，顺序得稳定。
        /// </summary>
        /// <remarks>
        /// 断的是**这三个之间的相对次序**，不是绝对条数：扫描是全工程的，展示台里那份示例
        /// 调色板也会出现在表里（而且它本来就应该在）。
        /// </remarks>
        [Test]
        public void 全部按名字排序()
        {
            CreatePalette("Zeta");
            CreatePalette("Alpha");
            CreatePalette("Beta");

            var all = ColorPaletteQuery.All();
            var ours = new List<string>();

            foreach (var palette in all)
            {
                if (palette.name == "Alpha" || palette.name == "Beta" || palette.name == "Zeta")
                {
                    ours.Add(palette.name);
                }
            }

            Assert.That(ours, Is.EqualTo(new[] { "Alpha", "Beta", "Zeta" }));
        }

        /// <summary>
        /// 查找是**惰性缓存**：没有资产变动时重复查找给同一份表；失效之后重扫。
        /// </summary>
        /// <remarks>
        /// 比的是**同一实例**而不是条数：建资产本身会触发 <c>projectChanged</c>（那正是失效的
        /// 来源），所以「盘上动过资产之后还要求给旧表」是测不出来的——而「没重扫」由同一份即可
        /// 证明。两级节流（事件只置脏、重扫推给下一次查找）也靠这条守。
        /// </remarks>
        [Test]
        public void 查找是惰性缓存且失效后重扫()
        {
            var alpha = CreatePalette("Alpha");

            var first = ColorPaletteQuery.All();
            Assert.That(ColorPaletteQuery.All(), Is.SameAs(first), "没有资产变动 ⇒ 不重扫。");

            CreatePalette("Beta");

            ColorPaletteQuery.Invalidate();

            var rescanned = ColorPaletteQuery.All();
            Assert.That(rescanned, Is.Not.SameAs(first), "失效之后重扫，换一份新表。");
            Assert.That(rescanned, Does.Contain(alpha), "重扫之后仍找得到原来那份。");
            Assert.That(ColorPaletteQuery.Find("Beta"), Is.Not.Null, "新资产也进表了。");
        }

        /// <summary>
        /// 展示台里那份示例调色板找得到。
        /// </summary>
        /// <remarks>
        /// 它是 <c>[ColorPalette("ShowcasePalette")]</c> 那一格的落点——资产坏掉或丢了的话，
        /// 展示台会**静默**变成兜底演示（告警 + 退回普通绘制），看的人只会以为特性没生效。
        /// 这条守卫与沙盒场景对齐检查同一个理由：仓库内的一致性也是门禁的事。
        /// </remarks>
        [Test]
        public void 展示台的示例调色板找得到()
        {
            Assert.That(
                ColorPaletteQuery.Find("ShowcasePalette"),
                Is.Not.Null,
                "装好的包里应当带着 Samples/AttributeShowcase/ShowcasePalette.asset。");
        }

        /// <summary>调色板里的颜色是**只读视图**，且空数组不会漏出 <c>null</c>。</summary>
        [Test]
        public void 颜色是只读视图且不为空引用()
        {
            var palette = CreatePalette("Alpha");

            Assert.That(palette.Colors, Is.Not.Null);
            Assert.That(palette.Colors.Count, Is.GreaterThan(0), "新建的资产带一份默认色。");

            palette.SetColors(null);
            Assert.That(palette.Colors, Is.Empty, "清空之后给空表，不是 null。");
        }

        #endregion

        #region Private Helpers

        /// <summary>在临时目录里建一份调色板资产。</summary>
        /// <param name="fileName">文件名（不含扩展名）——它就是调色板名。</param>
        /// <returns>建出来的资产。</returns>
        /// <remarks>
        /// **刻意不失效缓存**：那一步由调用方按需做（<see cref="TearDown"/> 已复位过一次）。
        /// 在这里顺手失效的话，「没失效就照旧给缓存」那条断言会变成恒真。
        /// </remarks>
        private XInspectorColorPalette CreatePalette(string fileName)
        {
            var palette = ScriptableObject.CreateInstance<XInspectorColorPalette>();
            var path = $"{TempFolder}/{fileName}.asset";

            AssetDatabase.CreateAsset(palette, path);
            _assetPaths.Add(path);

            return AssetDatabase.LoadAssetAtPath<XInspectorColorPalette>(path);
        }

        /// <summary>删掉临时目录（幂等）。</summary>
        private static void DeleteTempFolder()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        #endregion
    }
}
