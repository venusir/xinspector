using System;
using System.Collections.Generic;

namespace XInspector.Editor
{
    /// <summary>
    /// 特性处理器的发现与注册：扫描所有已加载的编辑器程序集，各实例化一份共享单例。
    /// <para>
    /// 与 <see cref="DrawerTypeRegistry"/> 同构——**扫全部程序集而非只扫本包**，
    /// 使用方在自己项目的编辑器程序集里写一个 <c>AttributeProcessor&lt;MyAttribute&gt;</c>
    /// 无需任何注册调用就会被发现。
    /// </para>
    /// <para>
    /// 本类型是 <c>internal</c> 的，与公开的 <see cref="DrawerTypeRegistry"/> 不同：
    /// 后者对外暴露 <c>Drawers</c> 是给使用方诊断「我的绘制器到底被扫到了没」用的，
    /// 而处理器目前没有这样的诉求。若日后需要，把可见性改成 <c>public</c> 是纯新增。
    /// </para>
    /// </summary>
    internal static class AttributeProcessorRegistry
    {
        #region Private Fields

        private static AttributeProcessor[] _processors;

        #endregion

        #region Public API

        /// <summary>
        /// 已发现的所有处理器，顺序稳定（优先级升序，同优先级按类型名）。
        /// </summary>
        public static AttributeProcessor[] Processors
        {
            get
            {
                EnsureInitialized();
                return _processors;
            }
        }

        /// <summary>
        /// 清空缓存，强制下次使用时重新扫描。
        /// </summary>
        /// <remarks>
        /// 供测试复位用。本仓约定：fixture 必须复位它触碰的静态门面——
        /// PlayMode 下所有用例共享一个 player 实例，不复位即互相污染。
        /// </remarks>
        public static void Reset()
        {
            _processors = null;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 首次使用时执行扫描并缓存。
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_processors != null)
            {
                return;
            }

            var found = new List<AttributeProcessor>();

            foreach (var type in EditorTypeScanner.CollectInstantiable<AttributeProcessor>("特性处理器"))
            {
                found.Add((AttributeProcessor)Activator.CreateInstance(type));
            }

            found.Sort(CompareByPriorityThenName);
            _processors = found.ToArray();
        }

        /// <summary>
        /// 优先级升序；同优先级按类型全名排序。
        /// </summary>
        /// <param name="a">左操作数。</param>
        /// <param name="b">右操作数。</param>
        /// <returns>比较结果。</returns>
        /// <remarks>
        /// 同优先级用类型名兜底是为了让顺序**确定**：扫描结果本身不保证跨平台或跨版本稳定，
        /// 而处理器的顺序是契约——一个处理器注入的特性可能被后一个读到。
        /// </remarks>
        private static int CompareByPriorityThenName(AttributeProcessor a, AttributeProcessor b)
        {
            var byPriority = a.ProcessorPriority.CompareTo(b.ProcessorPriority);
            if (byPriority != 0)
            {
                return byPriority;
            }

            return string.CompareOrdinal(a.GetType().FullName, b.GetType().FullName);
        }

        #endregion
    }
}
