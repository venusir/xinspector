using System;
using NUnit.Framework;
using XInspector.Internal;

namespace XInspector.Tests
{
    /// <summary>
    /// <see cref="PropertyGroupPath"/> 的解析与规范化。
    /// <para>
    /// 这些用例守的是一条**身份**契约：路径规范化后必须唯一。构建期用分组路径做字典键
    /// 来归并同组成员，同一逻辑分组若能写出两种形式，归并就会静默失败——成员散落到两个
    /// 节点里，现象是「分组看起来少了一个字段」，极难归因。所以规整化的边界要钉死。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PropertyGroupPathTests
    {
        #region Normalize

        /// <summary>
        /// 常规路径原样返回。
        /// </summary>
        /// <param name="input">输入路径。</param>
        /// <param name="expected">期望的规范化结果。</param>
        [TestCase("Outer", "Outer")]
        [TestCase("Outer/Inner", "Outer/Inner")]
        [TestCase("A/B/C", "A/B/C")]
        public void Normalize_常规路径保持不变(string input, string expected)
        {
            Assert.That(PropertyGroupPath.Normalize(input), Is.EqualTo(expected));
        }

        /// <summary>
        /// 各段首尾空白被去除。
        /// </summary>
        /// <param name="input">输入路径。</param>
        /// <param name="expected">期望的规范化结果。</param>
        [TestCase(" Outer", "Outer")]
        [TestCase("Outer ", "Outer")]
        [TestCase(" Outer / Inner ", "Outer/Inner")]
        [TestCase("\tOuter\t/\tInner\t", "Outer/Inner")]
        public void Normalize_去除每段首尾空白(string input, string expected)
        {
            Assert.That(PropertyGroupPath.Normalize(input), Is.EqualTo(expected));
        }

        /// <summary>
        /// 空段被丢弃而非报错——这些手写笔误的意图没有歧义。
        /// </summary>
        /// <param name="input">输入路径。</param>
        /// <param name="expected">期望的规范化结果。</param>
        [TestCase("Outer//Inner", "Outer/Inner")]
        [TestCase("Outer/", "Outer")]
        [TestCase("/Outer", "Outer")]
        [TestCase("//Outer//Inner//", "Outer/Inner")]
        public void Normalize_丢弃空段(string input, string expected)
        {
            Assert.That(PropertyGroupPath.Normalize(input), Is.EqualTo(expected));
        }

        /// <summary>
        /// 不含任何有效段的路径属于笔误到无法猜测意图，必须报错而不是静默返回空串。
        /// </summary>
        /// <param name="input">输入路径。</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("/")]
        [TestCase("//")]
        [TestCase(" / / ")]
        public void Normalize_无效路径抛ArgumentException(string input)
        {
            Assert.Throws<ArgumentException>(() => PropertyGroupPath.Normalize(input));
        }

        /// <summary>
        /// 规范化是幂等的：对已规范化的路径再跑一次不应改变结果。
        /// <para>
        /// 这条保证构建期可以放心地对可能已规范化的路径再调一次 Normalize，
        /// 不必先判断来源。
        /// </para>
        /// </summary>
        /// <param name="input">输入路径。</param>
        [TestCase("Outer")]
        [TestCase(" Outer / Inner ")]
        [TestCase("Outer//Inner/")]
        public void Normalize_幂等(string input)
        {
            var once = PropertyGroupPath.Normalize(input);

            Assert.That(PropertyGroupPath.Normalize(once), Is.EqualTo(once));
        }

        #endregion

        #region GetLeafName

        /// <summary>
        /// 取末段作为分组显示名。
        /// </summary>
        /// <param name="path">已规范化的路径。</param>
        /// <param name="expected">期望的末段。</param>
        [TestCase("Outer", "Outer")]
        [TestCase("Outer/Inner", "Inner")]
        [TestCase("A/B/C", "C")]
        public void GetLeafName_取末段(string path, string expected)
        {
            Assert.That(PropertyGroupPath.GetLeafName(path), Is.EqualTo(expected));
        }

        #endregion

        #region GetParentPath

        /// <summary>
        /// 取父路径；顶层路径无父，返回 null。
        /// </summary>
        /// <param name="path">已规范化的路径。</param>
        /// <param name="expected">期望的父路径。</param>
        [TestCase("Outer", null)]
        [TestCase("Outer/Inner", "Outer")]
        [TestCase("A/B/C", "A/B")]
        public void GetParentPath_取父路径(string path, string expected)
        {
            Assert.That(PropertyGroupPath.GetParentPath(path), Is.EqualTo(expected));
        }

        /// <summary>
        /// 沿 <see cref="PropertyGroupPath.GetParentPath"/> 逐级上溯能在有限步内到达顶层。
        /// <para>
        /// 构建期正是靠这个循环来合成缺失的祖先节点。用例同时钉住「上溯必然终止」——
        /// 若 GetParentPath 哪天对顶层路径返回了自身，就成了死循环，而它发生在构建期，
        /// 表现是编辑器卡死。
        /// </para>
        /// </summary>
        [Test]
        public void GetParentPath_逐级上溯必然终止()
        {
            var path = "A/B/C/D";
            var steps = 0;

            while (path != null)
            {
                path = PropertyGroupPath.GetParentPath(path);
                steps++;

                Assert.That(steps, Is.LessThan(100), "上溯未在合理步数内终止，GetParentPath 可能对顶层路径返回了自身。");
            }

            Assert.That(steps, Is.EqualTo(4), "A/B/C/D 应在 4 步内上溯到顶层。");
        }

        #endregion
    }
}
