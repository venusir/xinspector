using System;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// 内嵌编辑器绘制期的上下文：当前嵌了几层、正在嵌的是哪些对象。
    /// <para>
    /// <b>两个计数各管一件事，不要合并。</b> 栈（<see cref="MaxDepth"/> 长的引用数组）管
    /// 「成环」与「超限」；<see cref="Depth"/> 管语义——「当前是不是正在画内嵌编辑器」，
    /// 三个 <c>[XxxInInlineEditors]</c> 条件族读的正是它。合并两者会漏掉一类环：
    /// <c>[InlineEditor]</c> 的 <c>IncrementInlineEditorDrawerDepth</c> 为 <c>false</c> 时
    /// 语义深度不涨，但栈照涨，于是自引用仍会被挡下——那正是这个选项能被安全实现的依据。
    /// </para>
    /// <para>
    /// <b>为什么上下文是静态的。</b> 深度是「绘制路径此刻的状态」，不是某个属性的状态：
    /// 嵌套那一层由 Unity 自己创建的编辑器画出来（它不在我们的属性树里），而绘制签名
    /// 只有「属性 + 特性 + 标签」三样，没有可传递上下文的地方。求值仍然只在绘制期发生，
    /// 条件族照旧走处理器层装 resolver——与 <see cref="ModeConditions"/> 读
    /// <c>Application.isPlaying</c> 完全同款，不是「构建期做决策」。
    /// </para>
    /// </summary>
    internal static class InlineEditorDrawContext
    {
        #region Constants

        /// <summary>
        /// 嵌套深度上限。
        /// </summary>
        /// <remarks>
        /// <b>本包自定值</b>——Odin 的对应上限未从官方文档核实到，故不冒充它的数值。
        /// 取 4 的理由是成本与可读性：每嵌一层就是一棵独立的属性树（嵌套对象的
        /// <c>XInspectorEditor</c> 会在 <c>OnEnable</c> 里建树），而内嵌超过三四层在
        /// 界面上已无可读性。它同时是自引用之外的第二道防线：A 引用 B、B 又引用 A 时，
        /// 环检测先挡下；对象图很深时由上限兜底。
        /// </remarks>
        public const int MaxDepth = 4;

        #endregion

        #region Private Fields

        // 引用数组当栈用，进出都只动 _count，不分配。长度就是上限——
        // 超过 MaxDepth 的进入在 TryEnter 里就被拒了，这里不必扩容。
        private static readonly Object[] Stack = new Object[MaxDepth];

        private static int _count;
        private static int _depth;

        #endregion

        #region Public API

        /// <summary>
        /// 语义深度：当前处于第几层内嵌编辑器里。<c>0</c> 表示不在内嵌编辑器里。
        /// </summary>
        /// <remarks>
        /// 只受 <c>[InlineEditor]</c> 的 <c>IncrementInlineEditorDrawerDepth</c> 为
        /// <c>true</c> 的进入影响——它回答的是「条件族该怎么表现」，不是「栈里压了几个」。
        /// </remarks>
        public static int Depth => _depth;

        /// <summary>
        /// 尝试进入一层内嵌编辑器。
        /// </summary>
        /// <param name="target">即将被内嵌绘制的对象。</param>
        /// <param name="incrementDepth">是否同时递增语义深度（对应 Odin 的同名选项）。</param>
        /// <param name="scope">成功时产出的作用域；失败时是不可用的默认值，不应释放。</param>
        /// <returns>进入的结果；只有 <see cref="InlineEditorEnterResult.Entered"/> 表示成功。</returns>
        /// <remarks>
        /// 用「一次调用给出结果」而不是 <c>CanEnter</c> + <c>Enter</c> 两步：两步写法允许
        /// 调用方忘了先问，而这里必须由调用方按结果决定后续（被拒时退回普通绘制并告警）。
        /// 进出必须配对，用 <c>using</c> 包住内嵌绘制，异常时也能弹栈。
        /// </remarks>
        public static InlineEditorEnterResult TryEnter(Object target, bool incrementDepth, out Scope scope)
        {
            scope = default;

            if (target == null)
            {
                return InlineEditorEnterResult.NoTarget;
            }

            if (Contains(target))
            {
                return InlineEditorEnterResult.Cycle;
            }

            if (_count >= MaxDepth)
            {
                return InlineEditorEnterResult.DepthLimit;
            }

            Stack[_count] = target;
            _count++;

            if (incrementDepth)
            {
                _depth++;
            }

            scope = new Scope(_count - 1, incrementDepth);
            return InlineEditorEnterResult.Entered;
        }

        /// <summary>
        /// 把上下文清回初始状态。
        /// </summary>
        /// <remarks>
        /// 供测试在 fixture 复位静态门面时调用（与 <see cref="DrawerTypeRegistry.Reset"/> 同款）。
        /// 正常路径不该走到这里——每个作用域都有自己的 <c>using</c>。
        /// </remarks>
        public static void Reset()
        {
            Array.Clear(Stack, 0, Stack.Length);
            _count = 0;
            _depth = 0;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 目标是否已在当前嵌套链上（即再嵌一层会成环）。
        /// </summary>
        /// <param name="target">待查对象。</param>
        /// <returns>在链上返回 <c>true</c>。</returns>
        /// <remarks>
        /// 按<b>引用</b>比较而不是 Unity 的 <c>==</c>：后者会把已销毁对象当成 <c>null</c>，
        /// 而这里要回答的是「是不是同一个对象」，与它存活与否无关。
        /// </remarks>
        private static bool Contains(Object target)
        {
            for (var i = 0; i < _count; i++)
            {
                if (ReferenceEquals(Stack[i], target))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// 一次成功进入的作用域，释放即弹栈。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 释放靠「<see cref="_index"/> 是否还是栈顶」判定：重复释放、或先释放内层再释放外层之外的
        /// 乱序释放，都只会静默地什么都不做，而不会把栈或深度减错。乱序释放在正确使用
        /// （<c>using</c> 配对）下不会发生，这条守卫是给误用兜底的。
        /// </para>
        /// <para>
        /// 字段全 <c>readonly</c>，故可声明为 <c>readonly struct</c>——<c>using</c> 语句在这种
        /// 类型上可能产生一份副本用于释放，而释放改的是静态计数、副本之间无差别，正因此安全。
        /// </para>
        /// </remarks>
        public readonly struct Scope : IDisposable
        {
            private readonly int _index;
            private readonly bool _incremented;

            internal Scope(int index, bool incremented)
            {
                _index = index;
                _incremented = incremented;
            }

            /// <summary>
            /// 弹栈；已释放或不是栈顶时是空操作。
            /// </summary>
            public void Dispose()
            {
                if (_count != _index + 1)
                {
                    return;
                }

                _count = _index;
                Stack[_index] = null;

                if (_incremented)
                {
                    _depth--;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="InlineEditorDrawContext.TryEnter"/> 的结果。
    /// </summary>
    internal enum InlineEditorEnterResult
    {
        /// <summary>进入成功。</summary>
        Entered,

        /// <summary>没有目标对象（值为空），无从内嵌。</summary>
        NoTarget,

        /// <summary>目标已在当前嵌套链上，再嵌会成环。</summary>
        Cycle,

        /// <summary>已达嵌套深度上限。</summary>
        DepthLimit,
    }
}
