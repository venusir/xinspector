using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 属性树上的一个节点：一个字段、一个分组，或整棵树的根。
    /// <para>
    /// 它是**纯数据 + 一次派发**：自己不画任何东西，只把绘制工作交给
    /// <see cref="Chain"/>，由链上的绘制器决定画什么。所有关于「长什么样」的判断
    /// 都在绘制器里，本类只负责「我是谁、我有哪些子节点、我的链在哪」。
    /// </para>
    /// </summary>
    public sealed class InspectorProperty
    {
        #region Private Fields

        private readonly List<InspectorProperty> _children = new List<InspectorProperty>();
        private GUIContent _defaultLabel;

        #endregion

        #region Construction

        /// <summary>
        /// 构造节点。由构建期调用，第三方不应自行构造。
        /// </summary>
        /// <param name="name">显示名。</param>
        /// <param name="path">从根算起的完整路径，作为节点身份。</param>
        /// <param name="type">节点对应的类型。</param>
        /// <param name="kind">节点种类。</param>
        /// <param name="attributes">该节点携带的特性集合。</param>
        internal InspectorProperty(
            string name,
            string path,
            Type type,
            InspectorPropertyKind kind,
            PropertyAttributes attributes)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Type = type;
            Kind = kind;
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            State = new PropertyState();
        }

        #endregion

        #region Public API

        /// <summary>
        /// 显示名（字段名或分组末段名）。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 从根算起的完整路径，如 <c>"playerName"</c>、<c>"Outer/Inner/health"</c>。
        /// <para>
        /// 它同时充当节点的身份标识，因此树内唯一。
        /// </para>
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 节点对应的类型。分组节点为 <c>null</c>（分组不对应任何真实成员）。
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// 节点种类。
        /// </summary>
        public InspectorPropertyKind Kind { get; }

        /// <summary>
        /// 该节点携带的特性集合。
        /// </summary>
        public PropertyAttributes Attributes { get; }

        /// <summary>
        /// 该节点自己的可变状态。
        /// </summary>
        public PropertyState State { get; }

        /// <summary>
        /// 父节点。根节点为 <c>null</c>。
        /// </summary>
        public InspectorProperty Parent { get; private set; }

        /// <summary>
        /// 子节点，顺序即绘制顺序。
        /// </summary>
        public IReadOnlyList<InspectorProperty> Children => _children;

        /// <summary>
        /// 该节点的绘制器链。由构建期装配，装配后即冻结。
        /// </summary>
        public DrawerChain Chain { get; internal set; }

        /// <summary>
        /// 该节点的值入口。分组节点与根节点不对应真实成员，故为 <c>null</c>。
        /// </summary>
        public PropertyValueEntry ValueEntry { get; internal set; }

        /// <summary>
        /// 该成员对应的反射信息；非成员节点为 <c>null</c>。
        /// </summary>
        /// <remarks>
        /// 构建期存下来供特性处理器读取**成员自身**的特性——处理器拿到的是一个节点，
        /// 而它要判断的可能是「这个成员上有没有某个特性」，那件事只有 <see cref="MemberInfo"/> 知道。
        /// 用 <see cref="MemberInfo"/> 而非 <see cref="FieldInfo"/>：眼下成员都是字段，
        /// 但普通属性进来时不该再改一次签名。
        /// </remarks>
        internal MemberInfo Member { get; set; }

        /// <summary>
        /// 当前是否可见。等价于 <c>State.IsVisible</c>，为绘制器提供便利。
        /// </summary>
        public bool IsVisible => State.IsVisible;

        /// <summary>
        /// 绘制标签。优先用 <see cref="PropertyState.LabelOverride"/>，否则用 <see cref="Name"/>。
        /// </summary>
        /// <remarks>
        /// 默认标签缓存复用：Inspector 每帧重绘，每帧给每个字段新建一个
        /// <see cref="GUIContent"/> 是纯浪费。覆盖值不稳定（处理器可能每帧改），
        /// 故不做缓存。
        /// </remarks>
        public GUIContent Label => State.LabelOverride ?? (_defaultLabel ?? (_defaultLabel = new GUIContent(Name)));

        /// <summary>
        /// 绘制本节点。
        /// </summary>
        /// <exception cref="InvalidOperationException">节点尚未装配绘制器链。</exception>
        /// <remarks>
        /// 不可见时直接返回，不进入链条。<c>DrawChildren</c> 因此无需自己过滤子节点。
        /// </remarks>
        public void Draw()
        {
            if (!State.IsVisible)
            {
                return;
            }

            var chain = Chain;
            if (chain == null)
            {
                throw new InvalidOperationException(
                    $"属性 \"{Path}\" 没有绘制器链。构建期必须为每个节点装配链条——" +
                    "包括至少一个末端绘制器，否则该节点会静默地什么都不画。");
            }

            chain.Draw(this, Label);
        }

        /// <summary>
        /// 依次绘制所有子节点。
        /// </summary>
        public void DrawChildren()
        {
            // 手写 for 而非 foreach：绘制是每帧路径，foreach 在部分集合实现上仍会产生枚举器分配。
            for (var i = 0; i < _children.Count; i++)
            {
                _children[i].Draw();
            }
        }

        /// <summary>
        /// 是否存在指定类型的特性。
        /// </summary>
        /// <typeparam name="T">特性类型。</typeparam>
        /// <returns>存在返回 <c>true</c>。</returns>
        public bool HasAttribute<T>() where T : Attribute
        {
            return Attributes.Has<T>();
        }

        /// <summary>
        /// 取第一个指定类型的特性。
        /// </summary>
        /// <typeparam name="T">特性类型。</typeparam>
        /// <returns>找到的特性；不存在时返回 <c>null</c>。</returns>
        public T GetAttribute<T>() where T : Attribute
        {
            return Attributes.Get<T>();
        }

        /// <summary>
        /// 返回节点的可读形式。
        /// </summary>
        /// <returns>形如 <c>Member:health</c> 的字符串。</returns>
        public override string ToString()
        {
            return $"{Kind}:{Path}";
        }

        #endregion

        #region Internal

        /// <summary>
        /// 追加一个子节点，并回填其父引用。
        /// </summary>
        /// <param name="child">子节点。</param>
        internal void AddChild(InspectorProperty child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            child.Parent = this;
            _children.Add(child);
        }

        /// <summary>
        /// 子节点的底层列表，供构建期的分组装配做位置调整。
        /// </summary>
        /// <remarks>
        /// 只对构建期开放。对外仍是只读的 <see cref="Children"/>——
        /// 树的形状在构建结束后不应再变，否则每帧绘制的内容会不稳定。
        /// </remarks>
        internal List<InspectorProperty> RawChildren => _children;

        /// <summary>
        /// 按名字查找直接子节点。
        /// </summary>
        /// <param name="name">子节点名。</param>
        /// <returns>找到的子节点；不存在时返回 <c>null</c>。</returns>
        internal InspectorProperty FindChild(string name)
        {
            for (var i = 0; i < _children.Count; i++)
            {
                if (string.Equals(_children[i].Name, name, StringComparison.Ordinal))
                {
                    return _children[i];
                }
            }

            return null;
        }

        #endregion
    }
}
