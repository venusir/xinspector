using System;
using System.Collections.Generic;

namespace XInspector.Editor
{
    /// <summary>
    /// 为单个属性装配绘制器链。
    /// <para>
    /// 装配分三步：**配对**（哪些格子存在）→ **排序**（谁在外谁在内）→ **追加末端**。
    /// 三步都只做机械判断，因此整条链可以在没有 GUI 的情况下构造并断言——
    /// 「顺序对不对」这个最容易出错的环节因此是可单测的。
    /// </para>
    /// </summary>
    internal static class DrawerChainBuilder
    {
        #region Public API

        /// <summary>
        /// 装配一个属性的绘制器链。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="terminal">
        /// 末端绘制器。**由调用方显式传入而非从注册表发现**——末端是结构性的：
        /// 根与分组接子节点绘制器、成员接值绘制器。若把末端也交给注册表，
        /// 一个写错的匹配条件就能让某属性链为空，症状是「它静默地什么都不画」，
        /// 而那是最难归因的一类问题。显式追加让链条永不为空。
        /// </param>
        /// <returns>装配好的链。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="property"/> 或 <paramref name="terminal"/> 为 <c>null</c>。</exception>
        public static DrawerChain Build(InspectorProperty property, XInspectorDrawer terminal)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            if (terminal == null)
            {
                throw new ArgumentNullException(nameof(terminal));
            }

            var registered = DrawerTypeRegistry.Registered;
            var attributes = property.Attributes;
            var entries = new List<DrawerChainEntry>();

            // 第一步：把每个特性实例与「处理它的绘制器」配对。
            // 逐个特性实例来配，而不是逐个绘制器来问「你能画这个属性吗」——
            // 后者在属性挂多个同类型特性时会把同一个绘制器重复入链。
            for (var i = 0; i < attributes.Count; i++)
            {
                var attribute = attributes[i];

                for (var d = 0; d < registered.Length; d++)
                {
                    var handled = registered[d].HandledAttributeType;
                    if (handled == null || !handled.IsInstanceOfType(attribute))
                    {
                        continue;
                    }

                    entries.Add(new DrawerChainEntry(registered[d].Drawer, attribute, registered[d].Priority, i));
                }
            }

            // 第二步：非特性驱动的绘制器（自己实现 CanDraw）整属性问一次。
            for (var d = 0; d < registered.Length; d++)
            {
                if (registered[d].HandledAttributeType != null || !registered[d].Drawer.CanDraw(property))
                {
                    continue;
                }

                entries.Add(new DrawerChainEntry(
                    registered[d].Drawer,
                    null,
                    registered[d].Priority,
                    attributes.Count + d));
            }

            entries.Sort(CompareEntries);

            // 第三步：末端。序号取 int.MaxValue 只是为了自解释——它排在排序之后，
            // 不参与比较。真正的保证是「无论前面发生什么，最后总有东西会画」。
            entries.Add(new DrawerChainEntry(terminal, null, DrawerPriority.FallbackPriority, int.MaxValue));

            return new DrawerChain(entries.ToArray());
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 权重升序；同权重按序号。
        /// </summary>
        /// <param name="a">左操作数。</param>
        /// <param name="b">右操作数。</param>
        /// <returns>比较结果。</returns>
        /// <remarks>
        /// 序号兜底必不可少：<see cref="List{T}.Sort(Comparison{T})"/> 是不稳定排序，
        /// 只比权重的话，同权重的格子顺序会随元素个数变化——而链条顺序正是可组合性的全部依据。
        /// </remarks>
        private static int CompareEntries(DrawerChainEntry a, DrawerChainEntry b)
        {
            var byPriority = a.Priority.CompareTo(b.Priority);
            return byPriority != 0 ? byPriority : a.Sequence.CompareTo(b.Sequence);
        }

        #endregion
    }
}
