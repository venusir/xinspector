using System;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 由某个特性驱动的绘制器的基类。
    /// <para>
    /// 继承它即自动获得三件事：<see cref="CanDraw"/> 由特性类型决定（无需手写）、
    /// 链上与特性实例的配对由构建期完成、子类实现拿到的是**强类型**的特性而非
    /// <see cref="Attribute"/>。绝大多数绘制器都应该继承本类而不是
    /// <see cref="XInspectorDrawer"/>。
    /// </para>
    /// </summary>
    /// <typeparam name="TAttribute">本绘制器处理的特性类型。</typeparam>
    /// <example>
    /// <code>
    /// public sealed class TitleAttributeDrawer : AttributeDrawer&lt;TitleAttribute&gt;
    /// {
    ///     protected override void DrawPropertyLayout(
    ///         InspectorProperty property, TitleAttribute attribute, GUIContent label)
    ///     {
    ///         EditorGUILayout.LabelField(attribute.Title, EditorStyles.boldLabel);
    ///         CallNextDrawer(property, label);   // 包住内侧
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class AttributeDrawer<TAttribute> : XInspectorDrawer
        where TAttribute : Attribute
    {
        #region Public API

        /// <summary>
        /// 属性携带 <typeparamref name="TAttribute"/> 即参与绘制。
        /// </summary>
        /// <param name="property">候选属性。</param>
        /// <returns>携带该特性返回 <c>true</c>。</returns>
        public sealed override bool CanDraw(InspectorProperty property)
        {
            return property != null && property.HasAttribute<TAttribute>();
        }

        /// <summary>
        /// 取出强类型特性后转交子类实现。
        /// </summary>
        /// <param name="property">被绘制的属性。</param>
        /// <param name="attribute">触发本格的特性实例。</param>
        /// <param name="label">绘制标签。</param>
        /// <exception cref="InvalidOperationException">
        /// 传入的特性不是 <typeparamref name="TAttribute"/>——说明链装配时把特性与绘制器配错了。
        /// </exception>
        public sealed override void DrawPropertyLayout(InspectorProperty property, Attribute attribute, GUIContent label)
        {
            if (!(attribute is TAttribute typed))
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} 期望特性 {typeof(TAttribute).Name}，" +
                    $"实得 {(attribute == null ? "null" : attribute.GetType().Name)}。" +
                    "这属于链装配错误，不是使用方的用法问题。");
            }

            DrawPropertyLayout(property, typed, label);
        }

        #endregion

        #region Protected API

        /// <summary>
        /// 绘制属性。
        /// </summary>
        /// <param name="property">被绘制的属性。</param>
        /// <param name="attribute">触发本格的强类型特性实例。</param>
        /// <param name="label">绘制标签。</param>
        protected abstract void DrawPropertyLayout(InspectorProperty property, TAttribute attribute, GUIContent label);

        #endregion

        #region Internal

        /// <summary>
        /// 供构建期把特性实例与本绘制器配对。
        /// </summary>
        internal sealed override Type HandledAttributeType => typeof(TAttribute);

        #endregion
    }
}
