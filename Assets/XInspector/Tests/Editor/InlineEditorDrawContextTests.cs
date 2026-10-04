using System;
using NUnit.Framework;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 内嵌编辑器深度上下文：进出、环检测、深度上限与异常安全。
    /// <para>
    /// 全部无头——这个上下文是纯计数与引用比较，不碰 GUI。它是内嵌编辑器一族里
    /// 唯一「错了会很难归因」的部分：栈没弹干净的症状是「过一会儿莫名不再内嵌」，
    /// 深度漏算的症状是三个条件族行为反向。
    /// </para>
    /// </summary>
    [TestFixture]
    public class InlineEditorDrawContextTests
    {
        #region Setup / Teardown

        private ScriptableObject _a;
        private ScriptableObject _b;
        private ScriptableObject _c;
        private ScriptableObject _d;

        /// <summary>
        /// 建几个互不相同的目标对象；顺带复位上下文这个静态门面。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            InlineEditorDrawContext.Reset();
            _a = ScriptableObject.CreateInstance<ScriptableObject>();
            _b = ScriptableObject.CreateInstance<ScriptableObject>();
            _c = ScriptableObject.CreateInstance<ScriptableObject>();
            _d = ScriptableObject.CreateInstance<ScriptableObject>();
        }

        /// <summary>
        /// 再复位一次，确保本 fixture 的痕迹不渗给后面的 fixture——
        /// 上下文是静态的，漏一层就会让后续所有内嵌绘制都少一层深度。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            InlineEditorDrawContext.Reset();

            Destroy(ref _a);
            Destroy(ref _b);
            Destroy(ref _c);
            Destroy(ref _d);
        }

        private static void Destroy(ref ScriptableObject target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
                target = null;
            }
        }

        /// <summary>进入一层，断言成功并交出作用域。</summary>
        private static void Enter(Object target, bool incrementDepth, out InlineEditorDrawContext.Scope scope)
        {
            var result = InlineEditorDrawContext.TryEnter(target, incrementDepth, out scope);
            Assert.That(result, Is.EqualTo(InlineEditorEnterResult.Entered), "这一步应当进入成功。");
        }

        #endregion

        #region 进出

        /// <summary>没进过任何一层时，深度是零。</summary>
        [Test]
        public void 未进入时深度为零()
        {
            Assert.That(InlineEditorDrawContext.Depth, Is.Zero);
        }

        /// <summary>进入递增、释放复原。</summary>
        [Test]
        public void 进入与退出复原深度()
        {
            Enter(_a, true, out var scope);
            Assert.That(InlineEditorDrawContext.Depth, Is.EqualTo(1));

            scope.Dispose();
            Assert.That(InlineEditorDrawContext.Depth, Is.Zero);
        }

        /// <summary>嵌套进入逐层递增，逐层释放逐层递减。</summary>
        [Test]
        public void 嵌套逐层递增()
        {
            Enter(_a, true, out var outer);
            Enter(_b, true, out var inner);

            Assert.That(InlineEditorDrawContext.Depth, Is.EqualTo(2), "两层都算深度。");

            inner.Dispose();
            Assert.That(InlineEditorDrawContext.Depth, Is.EqualTo(1));

            outer.Dispose();
            Assert.That(InlineEditorDrawContext.Depth, Is.Zero);
        }

        /// <summary>
        /// 不递增深度时语义深度不变，但这一层照样占着栈——
        /// 这正是 <c>IncrementInlineEditorDrawerDepth = false</c> 不会因此漏掉环的依据。
        /// </summary>
        [Test]
        public void 不递增深度时仍占栈()
        {
            Enter(_a, false, out var scope);

            Assert.That(InlineEditorDrawContext.Depth, Is.Zero, "语义深度不涨。");
            Assert.That(
                InlineEditorDrawContext.TryEnter(_a, true, out _),
                Is.EqualTo(InlineEditorEnterResult.Cycle),
                "但目标确实在链上，自引用仍被挡下。");

            scope.Dispose();
            Assert.That(
                InlineEditorDrawContext.TryEnter(_a, true, out _),
                Is.EqualTo(InlineEditorEnterResult.Entered),
                "释放后同一个目标又可以进了。");
        }

        #endregion

        #region 环与上限

        /// <summary>同一个对象在链上时再进被拒，判定为成环。</summary>
        [Test]
        public void 同一目标重复进入被拒()
        {
            Enter(_a, true, out var scope);

            Assert.That(
                InlineEditorDrawContext.TryEnter(_a, true, out _),
                Is.EqualTo(InlineEditorEnterResult.Cycle));

            scope.Dispose();
        }

        /// <summary>不同对象可以一直进到上限；第 5 个才被拒。</summary>
        [Test]
        public void 超过上限被拒()
        {
            var extra = ScriptableObject.CreateInstance<ScriptableObject>();

            try
            {
                Enter(_a, true, out var scopeA);
                Enter(_b, true, out var scopeB);
                Enter(_c, true, out var scopeC);
                Enter(_d, true, out var scopeD);

                Assert.That(InlineEditorDrawContext.Depth, Is.EqualTo(InlineEditorDrawContext.MaxDepth));

                Assert.That(
                    InlineEditorDrawContext.TryEnter(extra, true, out _),
                    Is.EqualTo(InlineEditorEnterResult.DepthLimit),
                    "换一个新对象也一样——被挡下的原因是深度，不是成环。");

                scopeD.Dispose();
                scopeC.Dispose();
                scopeB.Dispose();
                scopeA.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(extra);
            }
        }

        /// <summary>栈满又碰上重复目标时，报成环而不是超限——成环是更具体的那条诊断。</summary>
        [Test]
        public void 栈满时重复目标报成环()
        {
            Enter(_a, true, out var scopeA);
            Enter(_b, true, out var scopeB);
            Enter(_c, true, out var scopeC);
            Enter(_d, true, out var scopeD);

            Assert.That(
                InlineEditorDrawContext.TryEnter(_b, true, out _),
                Is.EqualTo(InlineEditorEnterResult.Cycle));

            scopeD.Dispose();
            scopeC.Dispose();
            scopeB.Dispose();
            scopeA.Dispose();
        }

        /// <summary>空目标无从内嵌，直接拒。</summary>
        [Test]
        public void 空目标不可进入()
        {
            Assert.That(
                InlineEditorDrawContext.TryEnter(null, true, out _),
                Is.EqualTo(InlineEditorEnterResult.NoTarget));

            Assert.That(InlineEditorDrawContext.Depth, Is.Zero);
        }

        #endregion

        #region 异常与误用

        /// <summary>内嵌绘制抛异常时，<c>using</c> 也要把栈弹干净。</summary>
        [Test]
        public void 异常时也复原()
        {
            Enter(_a, true, out var outer);

            Assert.Throws<InvalidOperationException>(() =>
            {
                Enter(_b, true, out var scope);
                using (scope)
                {
                    throw new InvalidOperationException("模拟绘制期异常。");
                }
            });

            Assert.That(InlineEditorDrawContext.Depth, Is.EqualTo(1), "只弹掉内层。");

            outer.Dispose();
            Assert.That(InlineEditorDrawContext.Depth, Is.Zero);
        }

        /// <summary>重复释放是空操作，不会把计数减过头。</summary>
        [Test]
        public void 重复释放是幂等的()
        {
            Enter(_a, true, out var scope);

            scope.Dispose();
            scope.Dispose();

            Assert.That(InlineEditorDrawContext.Depth, Is.Zero);
            Assert.That(
                InlineEditorDrawContext.TryEnter(_a, true, out _),
                Is.EqualTo(InlineEditorEnterResult.Entered),
                "栈已经空了，同一个目标还能再进。");
        }

        /// <summary>
        /// 先释放外层（乱序）是空操作：只有栈顶那层能弹。
        /// 正确用法（<c>using</c> 配对）不会走到这里，本条守的是误用时不把栈减错。
        /// </summary>
        [Test]
        public void 乱序释放不改变栈()
        {
            Enter(_a, true, out var outer);
            Enter(_b, true, out var inner);

            outer.Dispose();
            Assert.That(InlineEditorDrawContext.Depth, Is.EqualTo(2), "非栈顶的释放不生效。");

            inner.Dispose();
            Assert.That(InlineEditorDrawContext.Depth, Is.EqualTo(1), "栈顶那层正常弹出。");

            Assert.That(
                InlineEditorDrawContext.TryEnter(_b, true, out _),
                Is.EqualTo(InlineEditorEnterResult.Entered),
                "b 已下链，可以再进。");
            Assert.That(
                InlineEditorDrawContext.TryEnter(_a, true, out _),
                Is.EqualTo(InlineEditorEnterResult.Cycle),
                "a 还在链上——外层那次释放确实是空操作。");
        }

        #endregion
    }
}
