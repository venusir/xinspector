using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 注册表里的一条：绘制器实例 + 已解析的权重 + 它处理的特性类型。
    /// </summary>
    internal readonly struct RegisteredDrawer
    {
        /// <summary>绘制器实例，全工程共享。</summary>
        public readonly XInspectorDrawer Drawer;

        /// <summary>已解析的权重（<see cref="DrawerPriorityAttribute"/> 优先于虚属性）。</summary>
        public readonly DrawerPriority Priority;

        /// <summary>本绘制器处理的特性类型；非特性驱动者为 <c>null</c>。</summary>
        public readonly Type HandledAttributeType;

        /// <summary>构造一条注册记录。</summary>
        /// <param name="drawer">绘制器实例。</param>
        /// <param name="priority">已解析的权重。</param>
        /// <param name="handledAttributeType">处理的特性类型。</param>
        public RegisteredDrawer(XInspectorDrawer drawer, DrawerPriority priority, Type handledAttributeType)
        {
            Drawer = drawer;
            Priority = priority;
            HandledAttributeType = handledAttributeType;
        }
    }

    /// <summary>
    /// 绘制器发现与注册：扫描所有已加载的编辑器程序集，找出
    /// <see cref="XInspectorDrawer"/> 的派生类，各实例化一份共享单例。
    /// <para>
    /// <b>扫描全部程序集而非只扫本包</b>，这是刻意的：使用方在自己项目的编辑器程序集里
    /// 写一个 <c>AttributeDrawer&lt;MyAttribute&gt;</c>，无需任何注册调用就会被自动发现。
    /// 这正是「新增特性是加法」在扩展点上的体现。
    /// </para>
    /// <para>
    /// 扫描在首次使用时惰性发生，之后缓存；域重载会清空静态字段，下次使用时自动重扫。
    /// </para>
    /// </summary>
    public static class DrawerTypeRegistry
    {
        #region Private Fields

        private static RegisteredDrawer[] _registered;
        private static XInspectorDrawer[] _drawers;

        #endregion

        #region Public API

        /// <summary>
        /// 已发现的所有绘制器实例，顺序稳定（权重升序，同权重按类型名）。
        /// </summary>
        /// <remarks>
        /// 暴露出来主要是供测试与诊断查看「到底扫到了什么」——
        /// 绘制器没被发现的症状是「什么都没画」，光看现象无从判断是没扫到还是没匹配上。
        /// </remarks>
        public static IReadOnlyList<XInspectorDrawer> Drawers
        {
            get
            {
                EnsureInitialized();
                return _drawers;
            }
        }

        #endregion

        #region Internal

        /// <summary>
        /// 已注册的全部记录，供构建期装配链条。
        /// </summary>
        internal static RegisteredDrawer[] Registered
        {
            get
            {
                EnsureInitialized();
                return _registered;
            }
        }

        /// <summary>
        /// 判断是否存在能处理指定特性类型的绘制器。
        /// </summary>
        /// <param name="attributeType">特性类型。</param>
        /// <returns>存在处理它的绘制器返回 <c>true</c>。</returns>
        /// <remarks>
        /// 供自动接管程序集判断「这个类型用到了本插件吗」。用「有没有对应绘制器」
        /// 而不是「特性的命名空间是不是 XInspector」：后者会把使用方自己写的自定义特性
        /// 与绘制器一并漏掉，而前者恰好就是「画得出来吗」这个真正要问的问题。
        /// </remarks>
        internal static bool HasDrawerForAttribute(Type attributeType)
        {
            if (attributeType == null)
            {
                return false;
            }

            var registered = Registered;
            for (var i = 0; i < registered.Length; i++)
            {
                var handled = registered[i].HandledAttributeType;
                if (handled != null && handled.IsAssignableFrom(attributeType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 清空缓存，强制下次使用时重新扫描。
        /// </summary>
        /// <remarks>
        /// 供测试复位用。本仓约定：fixture 必须复位它触碰的静态门面——
        /// PlayMode 下所有用例共享一个 player 实例，不复位即互相污染。
        /// </remarks>
        internal static void Reset()
        {
            _registered = null;
            _drawers = null;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 首次使用时执行扫描并缓存。
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_registered != null)
            {
                return;
            }

            var found = new List<RegisteredDrawer>();

            // 扫描与「哪些类型可用」的判断交给 EditorTypeScanner——与特性处理器注册表共用同一份，
            // 免得两份各自演化后分叉。
            foreach (var type in EditorTypeScanner.CollectInstantiable<XInspectorDrawer>("绘制器"))
            {
                var drawer = (XInspectorDrawer)Activator.CreateInstance(type);
                var priorityAttribute = type.GetCustomAttribute<DrawerPriorityAttribute>();

                // 特性优先于虚属性：特性是编译期常量，注册表不必实例化后再问一次；
                // 虚属性保留给需要按状态计算的场合。
                var priority = priorityAttribute != null ? priorityAttribute.Priority : drawer.Priority;

                found.Add(new RegisteredDrawer(drawer, priority, drawer.HandledAttributeType));
            }

            found.Sort(CompareByPriorityThenName);

            _registered = found.ToArray();
            _drawers = new XInspectorDrawer[_registered.Length];
            for (var i = 0; i < _registered.Length; i++)
            {
                _drawers[i] = _registered[i].Drawer;
            }
        }

        /// <summary>
        /// 权重升序；同权重按类型全名排序。
        /// </summary>
        /// <param name="a">左操作数。</param>
        /// <param name="b">右操作数。</param>
        /// <returns>比较结果。</returns>
        /// <remarks>
        /// 同权重用类型名兜底是为了让注册顺序**确定**：扫描结果本身不保证跨平台或跨版本稳定，
        /// 若同权重时顺序随机，链条顺序就会随机，而链条顺序恰恰是可组合性的全部依据。
        /// </remarks>
        private static int CompareByPriorityThenName(RegisteredDrawer a, RegisteredDrawer b)
        {
            var byPriority = a.Priority.CompareTo(b.Priority);
            if (byPriority != 0)
            {
                return byPriority;
            }

            return string.CompareOrdinal(a.Drawer.GetType().FullName, b.Drawer.GetType().FullName);
        }

        #endregion
    }
}
