using NUnit.Framework;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[AssetList]</c> 的纯逻辑：目录归一化、类型过滤串、名字前缀、行几何。
    /// <para>
    /// 不建树、不碰 GUI——照 <c>TableListTests</c> 里列宽分配纯函数的测法。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AssetListLayoutTests
    {
        #region 目录归一化

        /// <summary>竖线分隔、去空白、去尾部斜杠。</summary>
        [Test]
        public void 目录_竖线拆分与去空白()
        {
            CollectionAssert.AreEqual(
                new[] { "Assets/A", "Assets/B" },
                AssetListPaths.Normalize(" Assets/A | Assets/B/ "));
        }

        /// <summary>
        /// **兼容官方样例的前导斜杠**：<c>"/Plugins/Sirenix/"</c> 按 <c>Assets/</c> 相对理解
        /// ——这是本包对那句样例的解释，写进了特性注释与 README。
        /// </summary>
        [Test]
        public void 目录_前导斜杠按Assets相对()
        {
            CollectionAssert.AreEqual(
                new[] { "Assets/Plugins/Sirenix" },
                AssetListPaths.Normalize("/Plugins/Sirenix/"));

            CollectionAssert.AreEqual(
                new[] { "Packages/com.x.y" },
                AssetListPaths.Normalize("Packages/com.x.y"),
                "Packages 开头的原样保留（它不是 Assets 相对路径）。");

            CollectionAssert.AreEqual(
                new[] { "Assets/Art" },
                AssetListPaths.Normalize("Assets/Art"),
                "本来就是工程相对的原样通过。");
        }

        /// <summary>空白与「/」都表示整个工程（空数组）。</summary>
        [Test]
        public void 目录_空与根斜杠表示整个工程()
        {
            Assert.That(AssetListPaths.Normalize(null), Is.Empty);
            Assert.That(AssetListPaths.Normalize("   "), Is.Empty);
            Assert.That(AssetListPaths.Normalize("/"), Is.Empty);
        }

        #endregion

        #region 过滤

        /// <summary>类型过滤串是 <c>t:类型名</c>——只是启发式收窄，正确性由落值校验保证。</summary>
        [Test]
        public void 类型过滤串()
        {
            Assert.That(AssetListFilter.TypeFilter(typeof(Material)), Is.EqualTo("t:Material"));
            Assert.That(AssetListFilter.TypeFilter(null), Is.Null);
        }

        /// <summary>名字前缀：比**不含扩展名**的文件名、大小写不敏感、空白不过滤。</summary>
        [Test]
        public void 名字前缀()
        {
            Assert.That(AssetListFilter.MatchesNamePrefix("Assets/Art/Rock_01.png", "rock"), Is.True);
            Assert.That(AssetListFilter.MatchesNamePrefix("Assets/Art/Rock_01.png", "stone"), Is.False);
            Assert.That(AssetListFilter.MatchesNamePrefix("Assets/Art/Rock.png", null), Is.True);
            Assert.That(AssetListFilter.MatchesNamePrefix("Assets/Art/Rock.png", "  "), Is.True, "空白不过滤。");
            Assert.That(
                AssetListFilter.MatchesNamePrefix("Assets/Art/Rock.png", "Rock.png"),
                Is.False,
                "比的是不含扩展名的文件名——写成带扩展名的不该命中。");
        }

        #endregion

        #region 行几何

        /// <summary>三块矩形依次排开：缩略图 16、垂直居中，字段夹在中间，「−」贴右端。</summary>
        [Test]
        public void 行几何_三块依次排开()
        {
            var row = new Rect(10f, 5f, 300f, 18f);
            var rects = AssetListLayout.Allocate(row, showRemove: true);

            Assert.That(rects.Thumbnail.x, Is.EqualTo(10f));
            Assert.That(rects.Thumbnail.width, Is.EqualTo(16f));
            Assert.That(rects.Thumbnail.height, Is.EqualTo(16f));
            Assert.That(rects.Thumbnail.y, Is.EqualTo(6f), "18 的行高里 16 的块垂直居中。");

            Assert.That(rects.Field.x, Is.EqualTo(10f + 16f + 2f));
            Assert.That(rects.Remove.x, Is.EqualTo(310f - 20f));
            Assert.That(rects.Remove.width, Is.EqualTo(20f));
            Assert.That(rects.Field.xMax, Is.LessThanOrEqualTo(rects.Remove.x - 2f), "字段不压到「−」上。");
        }

        /// <summary>不画「−」时字段一直铺到行右端。</summary>
        [Test]
        public void 行几何_无删除键时铺满()
        {
            var rects = AssetListLayout.Allocate(new Rect(0f, 0f, 200f, 18f), showRemove: false);

            Assert.That(rects.Remove.width, Is.EqualTo(0f));
            Assert.That(rects.Field.xMax, Is.EqualTo(200f));
        }

        /// <summary>宽度不够时**不缩成负数**（照 <c>TableLayout.AllocateWidths</c> 的纪律）。</summary>
        [Test]
        public void 行几何_宽度不足不为负()
        {
            var rects = AssetListLayout.Allocate(new Rect(0f, 0f, 10f, 18f), showRemove: true);

            Assert.That(rects.Field.width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(rects.Remove.width, Is.GreaterThanOrEqualTo(0f));
        }

        #endregion
    }
}
