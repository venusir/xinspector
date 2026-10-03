using System;
using System.Collections;
using System.Collections.Generic;

namespace XInspector.Editor
{
    /// <summary>
    /// 一个属性上携带的特性集合，附按类型查找的辅助方法。
    /// <para>
    /// 每个属性上的特性列表都是**独立副本**（构建期克隆而来）。这一点是刻意的：
    /// 一个类级 <c>[BoxGroup]</c> 实例会被几十个成员共享，若处理器直接改它，
    /// 改动就会串到所有成员上——那是朴素实现里的真实缺陷。
    /// </para>
    /// </summary>
    public sealed class PropertyAttributes : IReadOnlyList<Attribute>
    {
        #region Private Fields

        private readonly List<Attribute> _attributes;

        #endregion

        #region Construction

        /// <summary>
        /// 以既有列表构造。列表**不被复制**，调用方须保证此后不再从外部改动它。
        /// </summary>
        /// <param name="attributes">特性列表。</param>
        internal PropertyAttributes(List<Attribute> attributes)
        {
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        }

        #endregion

        #region Public API

        /// <summary>
        /// 特性个数。
        /// </summary>
        public int Count => _attributes.Count;

        /// <summary>
        /// 按下标取特性，顺序即声明顺序（类级合成的特性排在最后）。
        /// </summary>
        /// <param name="index">下标。</param>
        /// <returns>该位置的特性。</returns>
        public Attribute this[int index] => _attributes[index];

        /// <summary>
        /// 是否存在指定类型的特性。
        /// </summary>
        /// <typeparam name="T">特性类型。</typeparam>
        /// <returns>存在返回 <c>true</c>。</returns>
        public bool Has<T>() where T : Attribute
        {
            return Get<T>() != null;
        }

        /// <summary>
        /// 取第一个指定类型的特性。
        /// </summary>
        /// <typeparam name="T">特性类型。</typeparam>
        /// <returns>找到的特性；不存在时返回 <c>null</c>。</returns>
        /// <remarks>
        /// 不抛异常而是返回 <c>null</c>：绘制器通常写成
        /// <c>var attr = property.Attributes.Get&lt;MyAttribute&gt;(); if (attr == null) return;</c>，
        /// 返回 null 让这条路径自然成立。需要「必须有」的场合请先调 <see cref="Has{T}"/>。
        /// </remarks>
        public T Get<T>() where T : Attribute
        {
            for (var i = 0; i < _attributes.Count; i++)
            {
                if (_attributes[i] is T match)
                {
                    return match;
                }
            }

            return null;
        }

        /// <summary>
        /// 取第一个指定类型的特性在列表中的下标。
        /// </summary>
        /// <typeparam name="T">特性类型。</typeparam>
        /// <returns>下标；不存在时返回 -1。</returns>
        public int IndexOf<T>() where T : Attribute
        {
            for (var i = 0; i < _attributes.Count; i++)
            {
                if (_attributes[i] is T)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 顺序遍历所有特性。
        /// </summary>
        /// <returns>特性枚举器。</returns>
        public IEnumerator<Attribute> GetEnumerator()
        {
            return _attributes.GetEnumerator();
        }

        /// <summary>
        /// 顺序遍历所有特性。
        /// </summary>
        /// <returns>非泛型枚举器。</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region Internal

        /// <summary>
        /// 底层列表，供构建期与处理器增删特性。
        /// </summary>
        internal List<Attribute> Raw => _attributes;

        #endregion
    }
}
