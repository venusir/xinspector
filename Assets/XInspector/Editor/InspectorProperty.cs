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
        /// <remarks>
        /// <para>
        /// <b>多态引用（<c>[SerializeReference]</c>）节点上它是<em>当前实例的具体类型</em></b>，
        /// 不是字段的声明类型——声明类型常常是接口或抽象类，按它解析会拿不到子成员的
        /// <see cref="FieldInfo"/>，特性与分组会一起静默消失。空引用与多选混合态下退回声明类型
        /// （那两种情况本包不展开）。
        /// </para>
        /// <para>
        /// <b>它是唯一一处会被改写的节点数据</b>：换具体类型之后整棵子树要重建，
        /// 而容器自己的类型也变了。改写入口只有一个——<c>PropertyTreeBuilder</c> 的重建路径，
        /// 见那里的注释。别处仍然当它是稳定的。
        /// </para>
        /// </remarks>
        public Type Type { get; internal set; }

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
        /// 用 <see cref="MemberInfo"/> 而非 <see cref="FieldInfo"/>：成员既有字段也有方法
        /// （<c>[Button]</c> 一族），按 <see cref="MemberInfo"/> 存就不必为后者再改一次签名。
        /// </remarks>
        internal MemberInfo Member { get; set; }

        /// <summary>
        /// 本节点所属的树。根节点之外都靠构建期回填，第三方不应读写。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>为什么需要它。</b> 绘制签名只有「属性 + 特性 + 标签」三样（给全部绘制器加一个上下文
        /// 已被否决），所以需要树级信息（目标对象列表、是否记 Undo）的绘制器只能沿节点往回找。
        /// </para>
        /// <para>
        /// <b>两个回填点，缺一不可。</b> 树在处理器之前就构造好（处理器需要目标对象才能工作），
        /// 彼时子树只含根与成员——<see cref="PropertyTree"/> 的构造递归回填它们；
        /// 分组节点是之后的分组装配期才挂进来的，靠 <see cref="AddChild"/> 从父节点继承。
        /// </para>
        /// </remarks>
        internal PropertyTree Owner { get; set; }

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
        /// 追加一个子节点，并回填其父引用与所属的树。
        /// </summary>
        /// <param name="child">子节点。</param>
        /// <remarks>
        /// 顺带回填 <see cref="Owner"/> 是为了分组装配：那些节点在树构造**之后**才挂进来，
        /// 递归回填那一步已经过去了。
        /// </remarks>
        internal void AddChild(InspectorProperty child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            child.Parent = this;
            child.Owner = Owner;
            _children.Add(child);
        }

        /// <summary>
        /// 子节点的底层列表，供构建期的分组装配与**元素层对账**做位置调整。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>形状只在两个时刻变</b>（2026-10-06 随元素节点化重新定义，全文见 Pipeline §十八）：
        /// 构建期一次；以及每次绘制之前的**元素层对账**——某个集合的元素节点数与
        /// <c>arraySize</c> 对不上时整层丢弃重建。其余任何时刻形状不变，
        /// 因此**一趟绘制之内**读到的树是稳定的。
        /// </para>
        /// <para>
        /// <b>推论：元素节点不跨结构变更。</b> <see cref="Path"/> 在每次同步之后于树内唯一，
        /// 它仍是节点身份；但元素节点的身份是**按位置**的投影（<c>items.Array.data[0]</c>），
        /// 长度一变整层作废——旧节点对象不得跨同步点持有（搜索命中集、重置名单这类
        /// 跨趟消费者一律把元素子树排除在外，见 <c>CollectionElementExpansion.IsElementNode</c>）。
        /// </para>
        /// <para>只对构建期与同步点开放。对外仍是只读的 <see cref="Children"/>。</para>
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
