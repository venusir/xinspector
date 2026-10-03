using System;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 绘制器链上的一格：一个绘制器实例，外加**触发它的那个特性实例**。
    /// <para>
    /// 为什么要带特性实例，而不是只带绘制器类型：绘制器是共享单例，
    /// 但同一个属性上可以挂多个同类型特性（如两个 <c>[BoxGroup]</c>，声明为
    /// <c>AllowMultiple = true</c>）。若链上只有绘制器，它就无从知道
    /// 「我这一格该处理哪一个」——把特性一并放进格子里，这个歧义就不存在了。
    /// </para>
    /// <para>
    /// 末端绘制器没有对应的特性，其 <see cref="Attribute"/> 为 <c>null</c>。
    /// </para>
    /// </summary>
    internal readonly struct DrawerChainEntry
    {
        #region Public API

        /// <summary>
        /// 本格要执行的绘制器。
        /// </summary>
        public readonly XInspectorDrawer Drawer;

        /// <summary>
        /// 触发本格绘制的特性实例；末端绘制器为 <c>null</c>。
        /// </summary>
        public readonly Attribute Attribute;

        /// <summary>
        /// 本格的排序权重，取自注册表（已计入 <see cref="DrawerPriorityAttribute"/> 的覆盖）。
        /// <para>
        /// 存在格子里而不是排序时现查：排序比较器会被调用 O(n log n) 次，
        /// 每次去注册表查一遍纯属浪费；而且格子自带权重也让它更自解释。
        /// </para>
        /// </summary>
        public readonly DrawerPriority Priority;

        /// <summary>
        /// 同权重时的先后序号。特性驱动的格子取特性声明下标；
        /// 非特性驱动的格子取「特性总数 + 注册顺序」，即同权重时排在特性格子之后。
        /// </summary>
        public readonly int Sequence;

        /// <summary>
        /// 构造一格。
        /// </summary>
        /// <param name="drawer">绘制器实例。</param>
        /// <param name="attribute">触发它的特性实例，可为 <c>null</c>。</param>
        /// <param name="priority">排序权重。</param>
        /// <param name="sequence">同权重时的序号。</param>
        public DrawerChainEntry(XInspectorDrawer drawer, Attribute attribute, DrawerPriority priority, int sequence)
        {
            Drawer = drawer ?? throw new ArgumentNullException(nameof(drawer));
            Attribute = attribute;
            Priority = priority;
            Sequence = sequence;
        }

        /// <summary>
        /// 执行本格的绘制器。
        /// </summary>
        /// <param name="property">被绘制的属性。</param>
        /// <param name="label">绘制标签。</param>
        public void Draw(InspectorProperty property, GUIContent label)
        {
            Drawer.DrawPropertyLayout(property, Attribute, label);
        }

        #endregion
    }
}
