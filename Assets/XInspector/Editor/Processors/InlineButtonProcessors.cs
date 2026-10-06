using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 行内按钮的每属性状态。
    /// <para>
    /// 数组**按特性在节点的特性列表里出现的次序**铺开，绘制器靠
    /// <see cref="InlineButtonProcessor.IndexOf"/> 找到自己那一格。不用字典以特性实例为键：
    /// <see cref="Attribute"/> 重写了 <c>Equals</c>/<c>GetHashCode</c>（按字段值比较，内部走反射），
    /// 放在每帧的绘制路径上等于每帧做一次反射——而这条路径上连装箱都要避。
    /// </para>
    /// </summary>
    internal sealed class InlineButtonState
    {
        #region Public Fields

        /// <summary>各按钮的显示内容；解析失败时把原因写进 Tooltip。</summary>
        public GUIContent[] Labels = Array.Empty<GUIContent>();

        /// <summary>各按钮逐目标解析出的方法，与树的目标列表一一对应。</summary>
        public MethodInfo[][] Methods = Array.Empty<MethodInfo[]>();

        /// <summary>各按钮的宽度（像素）。首帧量一次就缓存下来。</summary>
        public float[] Widths = Array.Empty<float>();

        /// <summary>
        /// 逐目标的嵌套实例来源；顶层为 <c>null</c>。
        /// </summary>
        /// <remarks>
        /// 各按钮共用一份（它只取决于这个属性在树上的位置，与是哪一个按钮无关）。
        /// 调用时**现读**实例——绑死的实例在父字段被重新赋值之后会**静默地陈旧**。
        /// </remarks>
        public ReflectedAccessor[] Scopes;

        #endregion

        #region Public API

        /// <summary>按按钮个数铺开数组（长度不变时不重新分配）。</summary>
        /// <param name="count">按钮个数。</param>
        public void EnsureCapacity(int count)
        {
            if (Labels.Length == count)
            {
                return;
            }

            Labels = new GUIContent[count];
            Methods = new MethodInfo[count][];
            Widths = new float[count];
        }

        #endregion
    }

    /// <summary>
    /// <see cref="InlineButtonAttribute"/> 的处理器：构建期把方法名解析成可调用的方法。
    /// <para>
    /// 与 <see cref="ButtonProcessor"/> 同一套路子——反射只发生在构建期，
    /// 解析失败**告警而不是静默**，且字段本身照常绘制（按钮不画而已）。
    /// </para>
    /// </summary>
    internal sealed class InlineButtonProcessor : AttributeProcessor<InlineButtonAttribute>
    {
        #region Public API

        /// <summary>
        /// 找一个行内按钮特性在节点的特性列表里的**次序**（只数行内按钮）。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="attribute">要找的特性实例。</param>
        /// <returns>次序；不在列表里时返回 <c>-1</c>。</returns>
        /// <remarks>
        /// 用引用相等而不是 <c>Equals</c>：同一个字段上挂两个写法完全相同的 <c>[InlineButton]</c>
        /// 是合法的，按值比较会把它们当成同一个。
        /// </remarks>
        public static int IndexOf(InspectorProperty property, InlineButtonAttribute attribute)
        {
            var attributes = property.Attributes.Raw;
            var index = 0;

            for (var i = 0; i < attributes.Count; i++)
            {
                if (!(attributes[i] is InlineButtonAttribute candidate))
                {
                    continue;
                }

                if (ReferenceEquals(candidate, attribute))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            InlineButtonAttribute attribute,
            IList<Attribute> attributes)
        {
            var state = property.State.GetOrCreate<InlineButtonState>();
            state.EnsureCapacity(CountButtons(attributes));

            var index = IndexOf(property, attribute);
            if (index < 0)
            {
                return;
            }

            // 解析循环与 [OnValueChanged]/[CustomContextMenu] 共用一份实现——它连同
            // 「嵌套层在实例上找、值类型实例拒绝」一起收敛在 NamedMethodResolver 里。
            // 这里只负责把结果摊进逐按钮的状态与 Tooltip。
            var methods = NamedMethodResolver.Resolve(
                property, attribute.MethodName, "[InlineButton]", out var scopes, out var reason);

            if (methods == null)
            {
                // 一个都调不了：按钮照画但禁用，原因挂在 Tooltip 上（旁边的字段不受影响）。
                state.Labels[index] = new GUIContent(
                    DisplayName(attribute),
                    $"{reason ?? "找不到方法。"}（[InlineButton] 标在 \"{property.Path}\" 上）");
                return;
            }

            state.Labels[index] = new GUIContent(DisplayName(attribute));
            state.Methods[index] = methods;
            state.Scopes = scopes;
        }

        #endregion

        #region Private Helpers

        /// <summary>数一数列表里有几个行内按钮特性。</summary>
        /// <param name="attributes">特性列表。</param>
        /// <returns>个数。</returns>
        private static int CountButtons(IList<Attribute> attributes)
        {
            var count = 0;

            for (var i = 0; i < attributes.Count; i++)
            {
                if (attributes[i] is InlineButtonAttribute)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>按钮文本：特性给了就用它，否则用方法名。</summary>
        /// <param name="attribute">特性。</param>
        /// <returns>显示文本。</returns>
        private static string DisplayName(InlineButtonAttribute attribute)
        {
            return string.IsNullOrWhiteSpace(attribute.Label) ? attribute.MethodName : attribute.Label;
        }

        #endregion
    }
}
