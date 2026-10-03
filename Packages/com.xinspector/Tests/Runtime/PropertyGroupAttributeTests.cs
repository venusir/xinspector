using System;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// <see cref="PropertyGroupAttribute"/> 及其子类 <see cref="BoxGroupAttribute"/> 的行为。
    /// <para>
    /// 重点是 <c>Combine</c>（同组声明归并）与 <c>CloneForPath</c>（祖先节点合成）这两条
    /// 构建期依赖的契约——它们的规则若不明确，表现是「分组偶尔合并、偶尔不合并」这类
    /// 难以复现的现象。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PropertyGroupAttributeTests
    {
        #region 构造与派生属性

        /// <summary>
        /// 构造时即规范化 <see cref="PropertyGroupAttribute.GroupID"/>。
        /// </summary>
        [Test]
        public void 构造时规范化GroupID()
        {
            var attribute = new BoxGroupAttribute(" Outer / Inner ");

            Assert.That(attribute.GroupID, Is.EqualTo("Outer/Inner"));
        }

        /// <summary>
        /// <see cref="PropertyGroupAttribute.GroupName"/> 恒为 GroupID 的末段。
        /// </summary>
        [Test]
        public void GroupName为GroupID的末段()
        {
            Assert.That(new BoxGroupAttribute("Outer").GroupName, Is.EqualTo("Outer"));
            Assert.That(new BoxGroupAttribute("Outer/Inner").GroupName, Is.EqualTo("Inner"));
            Assert.That(new BoxGroupAttribute("A/B/C").GroupName, Is.EqualTo("C"));
        }

        /// <summary>
        /// 无效分组路径在**构造时**就报错，而不是拖到绘制期。
        /// </summary>
        /// <param name="groupID">无效的分组路径。</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("/")]
        public void 无效GroupID在构造时报错(string groupID)
        {
            Assert.Throws<ArgumentException>(() => new BoxGroupAttribute(groupID));
        }

        /// <summary>
        /// <see cref="PropertyGroupAttribute.Order"/> 默认 0，可显式指定。
        /// </summary>
        [Test]
        public void Order默认为零且可指定()
        {
            Assert.That(new BoxGroupAttribute("A").Order, Is.EqualTo(0f));
            Assert.That(new BoxGroupAttribute("A", 5f).Order, Is.EqualTo(5f));
        }

        #endregion

        #region Combine

        /// <summary>
        /// 归并规则：Order 取**先出现的非零值**，而不是累加。
        /// <para>
        /// 累加会让「给分组多加一个字段」意外改变该分组的排序位置——一个很难归因的副作用。
        /// </para>
        /// </summary>
        [Test]
        public void Combine_Order取先声明的非零值()
        {
            var first = new BoxGroupAttribute("A");
            first.Combine(new BoxGroupAttribute("A", 5f));

            Assert.That(first.Order, Is.EqualTo(5f), "先声明者 Order 为 0，应取后者的 5。");

            var explicitFirst = new BoxGroupAttribute("A", 3f);
            explicitFirst.Combine(new BoxGroupAttribute("A", 5f));

            Assert.That(explicitFirst.Order, Is.EqualTo(3f), "先声明者已有非零 Order，不应被后者覆盖，也不应累加。");
        }

        /// <summary>
        /// 归并后 Order 不累加——单独钉住这一条，因为「累加」是很容易写错的方向。
        /// </summary>
        [Test]
        public void Combine_Order不累加()
        {
            var attribute = new BoxGroupAttribute("A", 2f);
            attribute.Combine(new BoxGroupAttribute("A", 3f));

            Assert.That(attribute.Order, Is.Not.EqualTo(5f));
        }

        /// <summary>
        /// <see cref="BoxGroupAttribute.Label"/> 取先出现的非空值。
        /// </summary>
        [Test]
        public void Combine_Label取先声明的非空值()
        {
            var attribute = new BoxGroupAttribute("A");
            attribute.Combine(new BoxGroupAttribute("A") { Label = "先行标签" });
            Assert.That(attribute.Label, Is.EqualTo("先行标签"), "先声明者 Label 为空，应取后者的。");

            var explicitFirst = new BoxGroupAttribute("A") { Label = "已有标签" };
            explicitFirst.Combine(new BoxGroupAttribute("A") { Label = "后者标签" });
            Assert.That(explicitFirst.Label, Is.EqualTo("已有标签"), "先声明者已有 Label，不应被覆盖。");
        }

        /// <summary>
        /// 归并 null 属于编程错误，必须报错而不是静默忽略。
        /// </summary>
        [Test]
        public void Combine_null抛异常()
        {
            var attribute = new BoxGroupAttribute("A");

            Assert.Throws<ArgumentNullException>(() => attribute.Combine(null));
        }

        #endregion

        #region CloneForPath

        /// <summary>
        /// 复制后 GroupID 变为目标路径，且不是同一个实例。
        /// </summary>
        [Test]
        public void CloneForPath_改写路径且为新实例()
        {
            var original = new BoxGroupAttribute("Outer/Inner");
            var clone = original.CloneForPath("Outer");

            Assert.That(clone, Is.Not.SameAs(original));
            Assert.That(clone.GroupID, Is.EqualTo("Outer"));
            Assert.That(original.GroupID, Is.EqualTo("Outer/Inner"), "原实例不应被改动。");
        }

        /// <summary>
        /// 复制会保留子类字段——这正是用 <c>MemberwiseClone</c> 而非重新构造的理由。
        /// <para>
        /// 若哪天有人把实现改成 <c>new BoxGroupAttribute(path)</c>，这条用例会挡住：
        /// 那样写会丢掉 <c>ShowLabel</c> 与 <c>Label</c>。
        /// </para>
        /// </summary>
        [Test]
        public void CloneForPath_保留子类字段()
        {
            var original = new BoxGroupAttribute("Outer/Inner")
            {
                Label = "自定义标题",
                ShowLabel = false,
                Order = 7f,
            };

            // CloneForPath 的返回类型是基类——构建期只以基类身份使用它，
            // 故这里显式向下转型：验证「子类字段是否被保留」本就是子类视角的断言。
            var clone = (BoxGroupAttribute)original.CloneForPath("Outer");

            Assert.That(clone.Label, Is.EqualTo("自定义标题"));
            Assert.That(clone.ShowLabel, Is.False);
            Assert.That(clone.Order, Is.EqualTo(7f));
        }

        /// <summary>
        /// <see cref="PropertyGroupAttribute.GroupName"/> 由 GroupID 推导，故复制后自动跟随新路径。
        /// </summary>
        [Test]
        public void CloneForPath_GroupName跟随新路径()
        {
            var clone = new BoxGroupAttribute("Outer/Inner").CloneForPath("Outer");

            Assert.That(clone.GroupName, Is.EqualTo("Outer"));
        }

        #endregion
    }
}
