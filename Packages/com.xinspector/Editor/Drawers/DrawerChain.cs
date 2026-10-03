using System;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 一个属性的绘制器链。构建期装配一次，之后冻结。
    /// <para>
    /// 执行模型是**递归下降 + 游标**：第 0 格先执行，它若调用
    /// <see cref="CallNext"/> 则游标前进一格，如此类推。每个绘制器都在
    /// 「调用下一个」的前后各有一段自己的代码，于是自然形成层层包裹。
    /// </para>
    /// <para>
    /// <b>游标是每属性（每链）自己的状态</b>，不是全局的——这正是重入安全的原因：
    /// 某个绘制器在绘制过程中又去画一次同一个属性（如内联预览），
    /// <see cref="Draw"/> 会保存并恢复游标，外层不受影响。
    /// </para>
    /// </summary>
    public sealed class DrawerChain
    {
        #region Private Fields

        private readonly DrawerChainEntry[] _entries;
        private int _cursor;

        #endregion

        #region Construction

        /// <summary>
        /// 以既有格子序列构造。由构建期调用。
        /// </summary>
        /// <param name="entries">按执行顺序排好的格子，**末格必须是末端绘制器**。</param>
        /// <exception cref="ArgumentException"><paramref name="entries"/> 为空。</exception>
        internal DrawerChain(DrawerChainEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                throw new ArgumentException(
                    "绘制器链不能为空。链条末端的绘制器由构建期显式追加，" +
                    "空链意味着该属性会静默地什么都不画。",
                    nameof(entries));
            }

            _entries = entries;
        }

        #endregion

        #region Public API

        /// <summary>
        /// 链上的格子数（含末端绘制器）。
        /// </summary>
        public int Count => _entries.Length;

        /// <summary>
        /// 绘制整个链。
        /// </summary>
        /// <param name="property">被绘制的属性。</param>
        /// <param name="label">绘制标签。</param>
        public void Draw(InspectorProperty property, GUIContent label)
        {
            var saved = _cursor;
            _cursor = 0;

            // try/finally 而非顺序执行：某个绘制器抛异常时游标若不恢复，
            // 这条链此后的行为就全乱了，而异常本身的信息会把注意力引向别处。
            try
            {
                _entries[0].Draw(property, label);
            }
            finally
            {
                _cursor = saved;
            }
        }

        #endregion

        #region Internal

        /// <summary>
        /// 游标前进一格并执行。
        /// </summary>
        /// <param name="property">被绘制的属性。</param>
        /// <param name="label">绘制标签。</param>
        /// <exception cref="InvalidOperationException">已在链尾，或传入的属性不属于本链。</exception>
        internal void CallNext(InspectorProperty property, GUIContent label)
        {
            // 传入别的属性是很容易犯且症状隐蔽的错误：游标会在这条链上乱走，
            // 表现为绘制顺序莫名错乱。引用比较极廉价，值得挡在这里。
            if (!ReferenceEquals(property.Chain, this))
            {
                throw new InvalidOperationException(
                    $"属性 \"{property.Path}\" 不属于本绘制器链。" +
                    "CallNextDrawer 必须传入当前正在绘制的那个属性。");
            }

            if (_cursor + 1 >= _entries.Length)
            {
                throw new InvalidOperationException(
                    $"属性 \"{property.Path}\" 绘制器链已到末端（第 {_cursor + 1}/{_entries.Length} 格），" +
                    "仍有人调用 CallNextDrawer。末端绘制器由构建期追加，出现本异常说明构建期漏装了它。");
            }

            _cursor++;
            _entries[_cursor].Draw(property, label);
        }

        /// <summary>
        /// 链上的格子序列，供测试断言顺序。
        /// </summary>
        internal DrawerChainEntry[] Entries => _entries;

        #endregion
    }
}
