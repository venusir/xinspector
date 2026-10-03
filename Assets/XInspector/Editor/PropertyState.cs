using System;
using System.Collections.Generic;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 单个属性的可变状态。
    /// <para>
    /// <b>为什么需要它。</b> 绘制器是全工程共享的无状态单例（每种类型只实例化一个，
    /// 供所有属性、所有 Inspector 复用）——这是内存上的硬要求：一个 500 字段的 Inspector
    /// 若每个字段每种绘制器各来一个实例，就是两万个对象。代价是绘制器**不得持有可变字段**，
    /// 因此凡是「这个属性自己的」可变数据都必须有个别处可放，那就是本类。
    /// </para>
    /// <para>
    /// 这条约束不是建议而是架构前提：绘制器里出现 <c>private bool _expanded;</c>
    /// 之类的字段，会让所有用到该绘制器的属性共享同一个展开状态——现象是「展开一个，
    /// 全都展开了」，而且很难联想到原因。
    /// </para>
    /// </summary>
    public sealed class PropertyState
    {
        #region Private Fields

        // 用缓存的静态委托而非闭包：SetVisible 会在构建期被处理器反复调用，
        // 每次 new 一个闭包是白白的分配。这两个委托无捕获，可安全共享。
        private static readonly Func<bool> AlwaysVisible = () => true;
        private static readonly Func<bool> AlwaysHidden = () => false;
        private static readonly Func<bool> AlwaysReadOnly = () => true;
        private static readonly Func<bool> AlwaysEditable = () => false;

        private readonly Dictionary<Type, object> _bag = new Dictionary<Type, object>();
        private Func<bool> _visibilityResolver = AlwaysVisible;
        private Func<bool> _readOnlyResolver = AlwaysEditable;

        #endregion

        #region Public API

        /// <summary>
        /// 当前是否可见。每次读取都重新求值 <see cref="VisibilityResolver"/>。
        /// </summary>
        /// <remarks>
        /// 每帧重新求值是有意为之：<c>[ShowIf]</c> 这类条件依赖其它字段的当前值，
        /// 缓存会让它在该变的时候不变。
        /// </remarks>
        public bool IsVisible => _visibilityResolver();

        /// <summary>
        /// 可见性求值器。置 <c>null</c> 等价于「恒可见」。
        /// </summary>
        /// <remarks>
        /// 处理器装一个 <see cref="Func{TResult}"/> 进来（如 <c>[ShowIf]</c> 解析出的条件），
        /// 绘制路径每帧调用它。这样「要不要显示」这个判断完全不碰 GUI，可以无头单测。
        /// </remarks>
        public Func<bool> VisibilityResolver
        {
            get => _visibilityResolver;
            set => _visibilityResolver = value ?? AlwaysVisible;
        }

        /// <summary>
        /// 强制设为可见或不可见，等价于装一个恒定求值器。
        /// </summary>
        /// <param name="visible">是否可见。</param>
        public void SetVisible(bool visible)
        {
            _visibilityResolver = visible ? AlwaysVisible : AlwaysHidden;
        }

        /// <summary>
        /// 当前是否只读。只读表示仍然绘制但不可编辑。每次读取都重新求值
        /// <see cref="ReadOnlyResolver"/>。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="IsVisible"/> 同构，理由也一样：<c>[EnableIf]</c>/<c>[DisableIf]</c>
        /// 这类条件依赖其它字段的当前值，缓存会让它在该变的时候不变。
        /// </remarks>
        public bool IsReadOnly => _readOnlyResolver();

        /// <summary>
        /// 只读状态求值器。置 <c>null</c> 等价于「恒可编辑」。
        /// </summary>
        /// <remarks>
        /// 处理器装一个 <see cref="Func{TResult}"/> 进来（如 <c>[EnableIf]</c> 解析出的条件），
        /// 绘制路径每帧调用它。这样「能不能改」这个判断完全不碰 GUI，可以无头单测。
        /// </remarks>
        public Func<bool> ReadOnlyResolver
        {
            get => _readOnlyResolver;
            set => _readOnlyResolver = value ?? AlwaysEditable;
        }

        /// <summary>
        /// 强制设为只读或可编辑，等价于装一个恒定求值器。
        /// </summary>
        /// <param name="readOnly">是否只读。</param>
        /// <remarks>
        /// 定值场景用这个，需要按条件变化的用 <see cref="ReadOnlyResolver"/>。
        /// </remarks>
        public void SetReadOnly(bool readOnly)
        {
            _readOnlyResolver = readOnly ? AlwaysReadOnly : AlwaysEditable;
        }

        /// <summary>
        /// 标签覆盖。为 <c>null</c> 时使用属性自身的名字。
        /// </summary>
        public GUIContent LabelOverride { get; set; }

        /// <summary>
        /// 取指定类型的附加状态，不存在则创建。
        /// </summary>
        /// <typeparam name="T">状态类型，须有无参构造函数。</typeparam>
        /// <returns>该属性自己的状态实例。</returns>
        /// <remarks>
        /// 绘制器把自己的每属性状态放这里，而不是放进绘制器实例的字段——
        /// 见类型级注释。状态实例按属性隔离，属性销毁即随之回收。
        /// </remarks>
        public T GetOrCreate<T>() where T : class, new()
        {
            if (_bag.TryGetValue(typeof(T), out var existing))
            {
                return (T)existing;
            }

            var created = new T();
            _bag[typeof(T)] = created;
            return created;
        }

        /// <summary>
        /// 取指定类型的附加状态。
        /// </summary>
        /// <typeparam name="T">状态类型。</typeparam>
        /// <returns>状态实例；尚未创建时返回 <c>null</c>。</returns>
        public T Get<T>() where T : class
        {
            return _bag.TryGetValue(typeof(T), out var existing) ? (T)existing : null;
        }

        /// <summary>
        /// 复位到初始状态：可见、可编辑、无标签覆盖、清空附加状态。
        /// </summary>
        /// <remarks>
        /// 构建期重建属性树时调用，使旧状态不会渗透到新一轮。
        /// </remarks>
        public void Reset()
        {
            _visibilityResolver = AlwaysVisible;
            _readOnlyResolver = AlwaysEditable;
            LabelOverride = null;
            _bag.Clear();
        }

        #endregion
    }
}
