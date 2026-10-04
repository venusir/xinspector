using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 值的文本化。纯函数，所以这里能逐行断言——绘制那一半测不了，这一半必须测干净。
    /// </summary>
    [TestFixture]
    public class ReflectedValueFormatterTests
    {
        #region 空值

        /// <summary>普通的空显示成 <c>null</c>，不是一行空白——空白看起来像 bug。</summary>
        [Test]
        public void 普通的空显示为null()
        {
            Assert.That(ReflectedValueFormatter.Format(null, typeof(string)), Is.EqualTo("null"));
            Assert.That(ReflectedValueFormatter.Format(null, typeof(int)), Is.EqualTo("null"));
        }

        /// <summary>Unity 对象的空按 Unity 的惯例显示成 <c>None</c>。</summary>
        [Test]
        public void Unity对象的空显示为None()
        {
            Assert.That(ReflectedValueFormatter.Format(null, typeof(Object)), Is.EqualTo("None"));
            Assert.That(ReflectedValueFormatter.Format(null, typeof(Material)), Is.EqualTo("None"));
        }

        #endregion

        #region 简单值

        /// <summary>布尔用 C# 的写法，与既有的 <c>[DisplayAsString]</c> 一致。</summary>
        [Test]
        public void 布尔用CSharp写法()
        {
            Assert.That(ReflectedValueFormatter.Format(true, typeof(bool)), Is.EqualTo("True"));
            Assert.That(ReflectedValueFormatter.Format(false, typeof(bool)), Is.EqualTo("False"));
        }

        /// <summary>整数按不变文化输出。</summary>
        [Test]
        public void 整数用不变文化()
        {
            Assert.That(ReflectedValueFormatter.Format(42, typeof(int)), Is.EqualTo("42"));
            Assert.That(ReflectedValueFormatter.Format(-5L, typeof(long)), Is.EqualTo("-5"));
            Assert.That(ReflectedValueFormatter.Format((byte)7, typeof(byte)), Is.EqualTo("7"));
        }

        /// <summary>
        /// 浮点去掉拖尾。
        /// <para>
        /// <c>0.1f</c> 转 <c>double</c> 是 0.10000000149011612，默认格式会把它原样显示出来。
        /// </para>
        /// </summary>
        [Test]
        public void 浮点去掉拖尾()
        {
            Assert.That(ReflectedValueFormatter.Format(0.1f, typeof(float)), Is.EqualTo("0.1"));
            Assert.That(ReflectedValueFormatter.Format(1f / 3f, typeof(float)), Is.EqualTo("0.333333"));
            Assert.That(ReflectedValueFormatter.Format(2.5, typeof(double)), Is.EqualTo("2.5"));
        }

        /// <summary>字符串原样显示。</summary>
        [Test]
        public void 字符串原样显示()
        {
            Assert.That(ReflectedValueFormatter.Format("你好", typeof(string)), Is.EqualTo("你好"));
            Assert.That(ReflectedValueFormatter.Format(string.Empty, typeof(string)), Is.EqualTo(string.Empty));
        }

        /// <summary>过长的字符串被截断并留一个记号——「很长」与「正常」要一眼分得开。</summary>
        [Test]
        public void 过长字符串被截断()
        {
            var text = new string('x', ReflectedValueFormatter.MaxTextLength + 88);

            var formatted = ReflectedValueFormatter.Format(text, typeof(string));

            Assert.That(formatted.Length, Is.EqualTo(ReflectedValueFormatter.MaxTextLength + 1));
            Assert.That(formatted, Does.EndWith("…"));
        }

        #endregion

        #region 枚举

        /// <summary>枚举显示名字而不是下标。</summary>
        [Test]
        public void 枚举用名字()
        {
            Assert.That(
                ReflectedValueFormatter.Format(FormatterSample.Second, typeof(FormatterSample)),
                Is.EqualTo("Second"));
        }

        /// <summary>位标志把所有置位的名字都列出来。</summary>
        [Test]
        public void 位标志列出各位()
        {
            Assert.That(
                ReflectedValueFormatter.Format(FormatterFlags.A | FormatterFlags.B, typeof(FormatterFlags)),
                Is.EqualTo("A, B"));
        }

        #endregion

        #region Unity 值类型

        /// <summary>
        /// 向量按不变文化拼出来。
        /// <para>
        /// 不用类型自带的 <c>ToString</c>：那个按当前文化格式化，德语环境下小数点会变成逗号。
        /// </para>
        /// </summary>
        [Test]
        public void 向量按不变文化拼()
        {
            Assert.That(
                ReflectedValueFormatter.Format(new Vector3(1f, 2.5f, -3f), typeof(Vector3)),
                Is.EqualTo("(1, 2.5, -3)"));
        }

        /// <summary>颜色按 RGBA 四分量显示。</summary>
        [Test]
        public void 颜色按RGBA显示()
        {
            Assert.That(
                ReflectedValueFormatter.Format(new Color(1f, 0f, 0f, 1f), typeof(Color)),
                Is.EqualTo("RGBA(1, 0, 0, 1)"));
        }

        /// <summary>Unity 对象显示名字；已销毁的显示 <c>None</c>。</summary>
        [Test]
        public void Unity对象显示名字()
        {
            var asset = ScriptableObject.CreateInstance<ScriptableObject>();
            asset.name = "样本";

            try
            {
                Assert.That(ReflectedValueFormatter.Format(asset, typeof(Object)), Is.EqualTo("样本"));

                var destroyed = asset;
                Object.DestroyImmediate(asset);

                Assert.That(
                    ReflectedValueFormatter.Format(destroyed, typeof(Object)),
                    Is.EqualTo("None"),
                    "已销毁的对象按 Unity 的语义就是空。");
            }
            finally
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }
        }

        #endregion

        #region 集合

        /// <summary>集合只报类型与项数，不展开。</summary>
        [Test]
        public void 集合只报项数()
        {
            Assert.That(
                ReflectedValueFormatter.Format(new List<int> { 1, 2, 3 }, typeof(List<int>)),
                Is.EqualTo("List<Int32>（3 项）"));

            Assert.That(
                ReflectedValueFormatter.Format(new byte[] { 1, 2 }, typeof(byte[])),
                Is.EqualTo("Byte[]（2 项）"));
        }

        /// <summary>
        /// 惰性序列**不会被枚举**——枚举一个 <see cref="IEnumerable"/> 每帧都可能无界，
        /// 那不是显示该付的代价。
        /// <para>
        /// 夹具的枚举器一旦被调用就抛异常，所以这条用例同时也是它自己的控制项：
        /// 实现若改成「枚举一遍数一数」，这里立刻红。
        /// </para>
        /// </summary>
        [Test]
        public void 惰性序列不被枚举()
        {
            var sequence = new ExplodingSequence();

            Assert.DoesNotThrow(() =>
            {
                var formatted = ReflectedValueFormatter.Format(sequence, typeof(ExplodingSequence));
                Assert.That(formatted, Is.EqualTo("ExplodingSequence（无法计数）"));
            });
        }

        #endregion

        #region 兜底

        /// <summary>没法归类的类型走它自己的 <c>ToString</c>。</summary>
        [Test]
        public void 自定义类型用自带的ToString()
        {
            Assert.That(
                ReflectedValueFormatter.Format(new CustomText(), typeof(CustomText)),
                Is.EqualTo("自定义文本"));
        }

        /// <summary>没有重写 <c>ToString</c> 的类型显示类型全名，不是空白。</summary>
        [Test]
        public void 未重写ToString的类型不显示空白()
        {
            var formatted = ReflectedValueFormatter.Format(new PlainValue(), typeof(PlainValue));

            Assert.That(formatted, Is.Not.Null.And.Not.Empty);
        }

        #endregion
    }

    /// <summary>枚举样本。</summary>
    internal enum FormatterSample
    {
        /// <summary>第一项。</summary>
        First,

        /// <summary>第二项。</summary>
        Second,
    }

    /// <summary>位标志样本。</summary>
    [Flags]
    internal enum FormatterFlags
    {
        /// <summary>空。</summary>
        None = 0,

        /// <summary>第一位。</summary>
        A = 1,

        /// <summary>第二位。</summary>
        B = 2,
    }

    /// <summary>一被枚举就抛异常的序列——用来钉住「不枚举」这条不变量。</summary>
    internal sealed class ExplodingSequence : IEnumerable
    {
        /// <summary>总是抛异常。</summary>
        /// <returns>不会返回。</returns>
        public IEnumerator GetEnumerator()
        {
            throw new InvalidOperationException("这个序列不该被枚举。");
        }
    }

    /// <summary>自带 <c>ToString</c> 的类型。</summary>
    internal sealed class CustomText
    {
        /// <summary>返回固定文本。</summary>
        /// <returns>固定文本。</returns>
        public override string ToString()
        {
            return "自定义文本";
        }
    }

    /// <summary>没重写 <c>ToString</c> 的类型。</summary>
    internal sealed class PlainValue
    {
    }
}
