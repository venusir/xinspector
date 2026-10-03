using System;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 所有绘制器的基类。
    /// <para>
    /// <b>与 Unity 原生 <c>PropertyDrawer</c> 的根本差别：</b>原生模型是「首个匹配者胜出」——
    /// 一个属性最多被一个绘制器画，那个绘制器必须独自处理标签、字段、修饰、分组的一切。
    /// 本框架改成**链**：所有匹配的绘制器依次叠加，每个都可以「做点事，然后调用下一个」。
    /// 于是「<c>[BoxGroup]</c> 包住 <c>[Title]</c>、<c>[Title]</c> 再包住字段」是链条的自然结果，
    /// 而不是需要专门写一个「同时懂分组和标题」的绘制器。新增特性因此是纯加法。
    /// </para>
    /// <para>
    /// <b>实现者必须无状态。</b> 每种绘制器全工程只实例化一个，供所有属性复用
    /// （500 字段的 Inspector 不会因此产生两万个对象）。代价是不得持有可变字段——
    /// 每属性的可变数据放 <see cref="PropertyState"/>，见 <see cref="InspectorProperty.State"/>。
    /// </para>
    /// </summary>
    public abstract class XInspectorDrawer
    {
        #region Public API

        /// <summary>
        /// 本绘制器在链上的位置权重，默认 <see cref="DrawerPriority.AttributePriority"/>。
        /// 值越小越靠外层。可由 <see cref="DrawerPriorityAttribute"/> 覆盖。
        /// </summary>
        public virtual DrawerPriority Priority => DrawerPriority.AttributePriority;

        /// <summary>
        /// 本绘制器是否要参与指定属性的绘制。
        /// </summary>
        /// <param name="property">候选属性。</param>
        /// <returns>需要参与返回 <c>true</c>。</returns>
        public abstract bool CanDraw(InspectorProperty property);

        /// <summary>
        /// 绘制属性。
        /// </summary>
        /// <param name="property">被绘制的属性。</param>
        /// <param name="attribute">触发本格的特性实例；末端绘制器收到的为 <c>null</c>。</param>
        /// <param name="label">绘制标签。</param>
        /// <remarks>
        /// 典型实现是「画点东西 → <see cref="CallNextDrawer"/>」，从而包住后面的绘制器。
        /// 若要有条件地**不画**内侧内容（如隐藏子节点），直接不调用即可。
        /// </remarks>
        public abstract void DrawPropertyLayout(InspectorProperty property, Attribute attribute, GUIContent label);

        #endregion

        #region Protected API

        /// <summary>
        /// 调用链上的下一个绘制器。
        /// </summary>
        /// <param name="property">被绘制的属性，须与当前正在绘制的属性为同一个。</param>
        /// <param name="label">绘制标签。</param>
        /// <exception cref="InvalidOperationException">
        /// 已在链尾仍调用（说明构建期漏装了末端绘制器），或传入的属性不属于当前链。
        /// </exception>
        protected void CallNextDrawer(InspectorProperty property, GUIContent label)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            var chain = property.Chain;
            if (chain == null)
            {
                throw new InvalidOperationException(
                    $"属性 \"{property.Path}\" 没有绘制器链，无法调用下一个绘制器。");
            }

            chain.CallNext(property, label);
        }

        #endregion

        #region Internal

        /// <summary>
        /// 本绘制器所处理的特性类型；非特性驱动的绘制器返回 <c>null</c>。
        /// <para>
        /// 构建期用它把「特性实例」与「绘制器」配对。不由第三方覆写——
        /// 特性驱动的绘制器请改继承 <see cref="AttributeDrawer{TAttribute}"/>。
        /// </para>
        /// </summary>
        internal virtual Type HandledAttributeType => null;

        #endregion
    }
}
