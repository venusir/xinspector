using System;
using System.Collections.Generic;
using System.Reflection;

namespace XInspector.Editor
{
    /// <summary>
    /// 在**构建期**改写特性列表或属性状态、但不参与绘制的阶段。
    /// <para>
    /// 它补的是绘制器补不了的那一半。<see cref="XInspectorDrawer"/> 只能决定「这个属性怎么画」，
    /// 而下面这些事都发生在「画之前」：
    /// </para>
    /// <list type="bullet">
    /// <item><b>改状态</b>：<c>[ShowIf]</c> 要装一个可见性解析器，<c>[EnableIf]</c> 要装只读解析器。
    /// 这类判断不产出任何像素，塞进绘制器只会让绘制器变得既画东西又做决策。</item>
    /// <item><b>改别的节点的特性</b>：类级 <c>[BoxGroup]</c> 要分发到各成员。处理器是唯一能在
    /// 构建期够到「父级特性 → 子成员」这条路的角色。</item>
    /// </list>
    /// <para>
    /// <b>处理器不得绘制。</b> 一旦它能画东西，绘制器链的顺序语义就被绕过了——
    /// 「谁包住谁」将不再只由 <see cref="DrawerPriority"/> 决定。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 与绘制器一样，处理器是**全工程共享的无状态单例**：每种类型只实例化一个。
    /// 因此不得持有可变字段——需要跨调用保存的东西放进 <see cref="PropertyState"/>。
    /// </remarks>
    public abstract class AttributeProcessor
    {
        #region Public API

        /// <summary>
        /// 处理器的执行顺序，**越小越先跑**。默认 0。
        /// </summary>
        /// <remarks>
        /// 顺序有意义：一个处理器注入的特性可能被后一个处理器读到。同优先级时按类型全名排序，
        /// 保证跨平台、跨版本都确定。
        /// </remarks>
        public virtual float ProcessorPriority => 0f;

        /// <summary>
        /// 是否要处理**该属性自身**的特性。
        /// </summary>
        /// <param name="property">候选属性。</param>
        /// <returns>需要处理返回 <c>true</c>。</returns>
        public virtual bool CanProcessSelfAttributes(InspectorProperty property) => false;

        /// <summary>
        /// 处理该属性自身的特性列表。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="attributes">该属性的特性列表，**可直接增删改**。</param>
        public virtual void ProcessSelfAttributes(InspectorProperty property, IList<Attribute> attributes)
        {
        }

        /// <summary>
        /// 是否要处理**父属性下某个子成员**的特性。
        /// </summary>
        /// <param name="parentProperty">父属性（对类级特性来说就是根节点）。</param>
        /// <param name="member">子成员的反射信息。</param>
        /// <returns>需要处理返回 <c>true</c>。</returns>
        /// <remarks>
        /// 判据看的是**父属性**有没有相关特性，不是子成员——这正是类级 <c>[BoxGroup]</c>
        /// 得以分发到各成员的那条路。子成员自身的特性走
        /// <see cref="CanProcessSelfAttributes"/>。
        /// </remarks>
        public virtual bool CanProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member) => false;

        /// <summary>
        /// 处理某个子成员的特性列表。
        /// </summary>
        /// <param name="parentProperty">父属性。</param>
        /// <param name="member">子成员的反射信息。</param>
        /// <param name="attributes">**子成员的**特性列表，可直接增删改。</param>
        public virtual void ProcessChildMemberAttributes(
            InspectorProperty parentProperty,
            MemberInfo member,
            IList<Attribute> attributes)
        {
        }

        #endregion

        #region Internal

        /// <summary>
        /// 本处理器所处理的特性类型；非特性驱动的处理器返回 <c>null</c>。
        /// <para>
        /// 与 <see cref="XInspectorDrawer.HandledAttributeType"/> 完全对称，用途也一样：
        /// 回答「哪些特性有处理器」——自动接管的判据要用它，否则**处理器专有的特性**
        /// （条件族就是：只有处理器、没有绘制器）会被当成「没用到本插件」，
        /// 症状是类型不被接管、特性静默失效。
        /// </para>
        /// </summary>
        internal virtual Type HandledAttributeType => null;

        #endregion
    }

    /// <summary>
    /// 由某个特性驱动的处理器的基类。绝大多数处理器都应该继承它。
    /// </summary>
    /// <typeparam name="TAttribute">本处理器处理的特性类型。</typeparam>
    /// <example>
    /// <code>
    /// internal sealed class MyProcessor : AttributeProcessor&lt;MyAttribute&gt;
    /// {
    ///     protected override void ProcessSelf(
    ///         InspectorProperty property, MyAttribute attribute, IList&lt;Attribute&gt; attributes)
    ///     {
    ///         property.State.VisibilityResolver = () =&gt; /* … */ true;
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class AttributeProcessor<TAttribute> : AttributeProcessor
        where TAttribute : Attribute
    {
        #region Public API

        /// <summary>
        /// 属性携带 <typeparamref name="TAttribute"/> 即参与处理。
        /// </summary>
        /// <param name="property">候选属性。</param>
        /// <returns>携带该特性返回 <c>true</c>。</returns>
        public sealed override bool CanProcessSelfAttributes(InspectorProperty property)
        {
            return property != null && property.HasAttribute<TAttribute>();
        }

        /// <summary>
        /// **父属性**携带 <typeparamref name="TAttribute"/> 即参与处理。
        /// </summary>
        /// <param name="parentProperty">父属性。</param>
        /// <param name="member">子成员。</param>
        /// <returns>父属性携带该特性返回 <c>true</c>。</returns>
        public sealed override bool CanProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member)
        {
            return parentProperty != null && parentProperty.HasAttribute<TAttribute>();
        }

        /// <summary>
        /// 逐个实例回调子类，再把整个列表交回去供其改写。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="attributes">该属性的特性列表。</param>
        /// <remarks>
        /// 逐个实例而非只取第一个：一个成员上可以挂多个同类型特性（如两个 <c>[BoxGroup]</c>），
        /// 每个都该被处理。这一点与绘制器链的配对规则一致。
        /// </remarks>
        public sealed override void ProcessSelfAttributes(InspectorProperty property, IList<Attribute> attributes)
        {
            // 倒序遍历不必要——子类通过 attributes 增删时由它自己负责；
            // 这里只遍历传入时的快照长度，新增的条目不会被本次重复处理。
            var count = attributes.Count;
            for (var i = 0; i < count; i++)
            {
                if (attributes[i] is TAttribute typed)
                {
                    ProcessSelf(property, typed, attributes);
                }
            }
        }

        /// <summary>
        /// 把父属性上的特性逐个交回子类处理。
        /// </summary>
        /// <param name="parentProperty">父属性。</param>
        /// <param name="member">子成员。</param>
        /// <param name="attributes">子成员的特性列表。</param>
        public sealed override void ProcessChildMemberAttributes(
            InspectorProperty parentProperty,
            MemberInfo member,
            IList<Attribute> attributes)
        {
            if (parentProperty == null)
            {
                return;
            }

            // **枚举父属性的特性，不是子成员的。** 触发者与判据必须一致——
            // CanProcessChildMemberAttributes 判的是父级有没有，这里就该从父级取实例。
            // 从子成员取会让「类级分组分发」这类用法彻底失效：特性挂在类型上，
            // 而成员一个都不带它，于是预处理通过、处理却一次都不触发。
            var parentAttributes = parentProperty.Attributes;
            var count = parentAttributes.Count;

            for (var i = 0; i < count; i++)
            {
                if (parentAttributes[i] is TAttribute typed)
                {
                    ProcessChildMember(parentProperty, member, typed, attributes);
                }
            }
        }

        #endregion

        #region Protected API

        /// <summary>
        /// 处理属性自身的某个特性实例。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="attribute">特性实例。</param>
        /// <param name="attributes">该属性的特性列表。</param>
        protected virtual void ProcessSelf(InspectorProperty property, TAttribute attribute, IList<Attribute> attributes)
        {
        }

        /// <summary>
        /// 处理子成员的某个特性实例（触发者是**父属性**）。
        /// </summary>
        /// <param name="parentProperty">父属性。</param>
        /// <param name="member">子成员。</param>
        /// <param name="attribute">父属性上的特性实例。</param>
        /// <param name="attributes">子成员的特性列表。</param>
        protected virtual void ProcessChildMember(
            InspectorProperty parentProperty,
            MemberInfo member,
            TAttribute attribute,
            IList<Attribute> attributes)
        {
        }

        #endregion

        #region Internal

        /// <summary>
        /// 本处理器处理的特性类型。
        /// </summary>
        internal sealed override Type HandledAttributeType => typeof(TAttribute);

        #endregion
    }
}
