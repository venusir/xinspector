using NUnit.Framework;
using XInspector.Internal;

namespace XInspector.Tests
{
    /// <summary>
    /// 水平分组的分数 → 像素换算。纯 float 运算，跑在离线的 <c>Tests.Native</c> 通道里。
    /// <para>
    /// 这条规则是水平分组最容易出错的地方（谁分到多少、挤不下时怎么办），
    /// 所以它被特意放在 Runtime 侧、不碰 Unity，好让每次改动都能被秒级回归。
    /// </para>
    /// </summary>
    [TestFixture]
    public class HorizontalGroupWeightsTests
    {
        #region Private Fields

        private const float Epsilon = 1e-3f;

        #endregion

        #region 分配规则

        /// <summary>全部未指定 → 均分。</summary>
        [Test]
        public void 全未指定则均分()
        {
            var widths = HorizontalGroupWeights.Resolve(new[] { 0f, 0f, 0f }, null, null, 300f, 0f);

            Assert.That(widths[0], Is.EqualTo(100f).Within(Epsilon));
            Assert.That(widths[1], Is.EqualTo(100f).Within(Epsilon));
            Assert.That(widths[2], Is.EqualTo(100f).Within(Epsilon));
        }

        /// <summary>全显式且和为 1 → 各取自己的份额。</summary>
        [Test]
        public void 显式分数之和为一()
        {
            var widths = HorizontalGroupWeights.Resolve(new[] { 0.7f, 0.3f }, null, null, 200f, 0f);

            Assert.That(widths[0], Is.EqualTo(140f).Within(Epsilon));
            Assert.That(widths[1], Is.EqualTo(60f).Within(Epsilon));
        }

        /// <summary>部分显式：未指定者均分剩余。</summary>
        [Test]
        public void 未指定者均分剩余()
        {
            var widths = HorizontalGroupWeights.Resolve(new[] { 0.5f, 0f, 0f }, null, null, 200f, 0f);

            Assert.That(widths[0], Is.EqualTo(100f).Within(Epsilon));
            Assert.That(widths[1], Is.EqualTo(50f).Within(Epsilon), "剩余 100 分给两个未指定者。");
            Assert.That(widths[2], Is.EqualTo(50f).Within(Epsilon));
        }

        /// <summary>显式和大于 1 → 等比缩放（确定性、不溢出）。</summary>
        [Test]
        public void 和大于一按总和缩放()
        {
            var widths = HorizontalGroupWeights.Resolve(new[] { 2f, 2f }, null, null, 200f, 0f);

            Assert.That(widths[0], Is.EqualTo(100f).Within(Epsilon));
            Assert.That(widths[1], Is.EqualTo(100f).Within(Epsilon));
        }

        /// <summary>显式已占满且还有未指定者 → 未指定者分不到（返回 0 表示不限制）。</summary>
        [Test]
        public void 显式占满时未指定者分不到()
        {
            var widths = HorizontalGroupWeights.Resolve(new[] { 1f, 0f }, null, null, 200f, 0f);

            Assert.That(widths[0], Is.EqualTo(200f).Within(Epsilon));
            Assert.That(widths[1], Is.EqualTo(0f));
        }

        /// <summary>间距从可用宽度里扣，未指定者均分的是扣完的剩余。</summary>
        [Test]
        public void 间距从可用宽度里扣()
        {
            var widths = HorizontalGroupWeights.Resolve(new[] { 0f, 0f }, null, null, 202f, 2f);

            Assert.That(widths[0], Is.EqualTo(100f).Within(Epsilon), "202 − 2（间距）= 200，再均分。");
            Assert.That(widths[1], Is.EqualTo(100f).Within(Epsilon));
        }

        /// <summary>非正、NaN 的分数都算「未指定」。</summary>
        [Test]
        public void 非正与NaN视为未指定()
        {
            var widths = HorizontalGroupWeights.Resolve(new[] { -1f, float.NaN, 0.5f }, null, null, 200f, 0f);

            Assert.That(widths[2], Is.EqualTo(100f).Within(Epsilon));
            Assert.That(widths[0], Is.EqualTo(50f).Within(Epsilon), "两个「未指定」均分剩下的 100。");
            Assert.That(widths[1], Is.EqualTo(50f).Within(Epsilon));
        }

        #endregion

        #region 边界

        /// <summary>换不下（可用宽度不足）时整行返回全 0——那时再加宽度限制只会更糟。</summary>
        [Test]
        public void 可用宽度不足时全零()
        {
            var widths = HorizontalGroupWeights.Resolve(new[] { 0.5f, 0.5f }, null, null, 2f, 10f);

            Assert.That(widths[0], Is.EqualTo(0f));
            Assert.That(widths[1], Is.EqualTo(0f));
        }

        /// <summary>上下限夹取。</summary>
        [Test]
        public void 上下限夹取()
        {
            var widths = HorizontalGroupWeights.Resolve(
                new[] { 0.5f, 0.5f }, new[] { 120f, 0f }, new[] { 0f, 40f }, 200f, 0f);

            Assert.That(widths[0], Is.EqualTo(120f).Within(Epsilon), "算出 100，被下限抬到 120。");
            Assert.That(widths[1], Is.EqualTo(40f).Within(Epsilon), "算出 100，被上限压到 40。");
        }

        /// <summary>空输入与 null 不抛。</summary>
        [Test]
        public void 空输入返回空数组()
        {
            Assert.That(HorizontalGroupWeights.Resolve(new float[0], null, null, 100f, 0f), Is.Empty);
            Assert.That(HorizontalGroupWeights.Resolve(null, null, null, 100f, 0f), Is.Empty);
        }

        /// <summary>单格且未指定 → 占满整行。</summary>
        [Test]
        public void 单格未指定占满整行()
        {
            var widths = HorizontalGroupWeights.Resolve(new[] { 0f }, null, null, 150f, 4f);

            Assert.That(widths[0], Is.EqualTo(150f).Within(Epsilon), "只有一格时不该扣间距。");
        }

        #endregion
    }
}
