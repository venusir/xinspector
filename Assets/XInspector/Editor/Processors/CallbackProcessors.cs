using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary><see cref="OnInspectorGUIAttribute"/> 的每属性状态。</summary>
    internal sealed class CustomGuiState
    {
        #region Public Fields

        /// <summary>逐目标解析出的绘制方法；第 0 项即第一个目标那一份。</summary>
        public MethodInfo[] Methods;

        /// <summary>不可调用时的原因；可调用时为 <c>null</c>。</summary>
        public string Reason;

        /// <summary>
        /// 只含第一个目标的单元素数组，供调用时使用。
        /// </summary>
        /// <remarks>
        /// 自定义绘制**只执行一次**（见绘制器的说明），故这里预先把单目标数组备好——
        /// 每帧现建一个单元素数组是要计入绘制路径的分配的。
        /// </remarks>
        public object[] SingleTarget;

        /// <summary>与 <see cref="SingleTarget"/> 配套的单元素方法数组。</summary>
        public MethodInfo[] SingleMethod;

        #endregion
    }

    /// <summary><see cref="CustomContextMenuAttribute"/> 的一项菜单。</summary>
    internal sealed class ContextMenuEntry
    {
        /// <summary>挂着它的那个特性实例，用来让绘制器按引用认领。</summary>
        public CustomContextMenuAttribute Attribute;

        /// <summary>菜单项文本。</summary>
        public string MenuItem;

        /// <summary>逐目标解析出的方法。</summary>
        public MethodInfo[] Methods;

        /// <summary>逐目标的嵌套实例来源；顶层为 <c>null</c>（见 <see cref="NestedInstanceScope"/>）。</summary>
        public ReflectedAccessor[] Scopes;
    }

    /// <summary><see cref="CustomContextMenuAttribute"/> 的每属性状态。</summary>
    internal sealed class ContextMenuState
    {
        /// <summary>本字段上的全部菜单项，按声明顺序。</summary>
        public readonly List<ContextMenuEntry> Entries = new List<ContextMenuEntry>();

        /// <summary>
        /// 由哪一个特性实例负责处理右键。
        /// </summary>
        /// <remarks>
        /// 一个字段挂多项时，每一项在链上各有一格绘制器，而**右键只该弹一次**菜单
        /// （菜单里本来就列着全部项）。故指定第一项当处理者，其余只当数据。
        /// </remarks>
        public CustomContextMenuAttribute Owner;
    }

    /// <summary><see cref="OnValueChangedAttribute"/> 的一项监听。</summary>
    internal sealed class ValueChangedEntry
    {
        /// <summary>挂着它的那个特性实例。</summary>
        public OnValueChangedAttribute Attribute;

        /// <summary>方法名。</summary>
        public string MethodName;

        /// <summary>逐目标解析出的方法；解析失败时为 <c>null</c>。</summary>
        public MethodInfo[] Methods;

        /// <summary>逐目标的嵌套实例来源；顶层为 <c>null</c>（见 <see cref="NestedInstanceScope"/>）。</summary>
        public ReflectedAccessor[] Scopes;
    }

    /// <summary><see cref="OnValueChangedAttribute"/> 的每属性状态。</summary>
    internal sealed class ValueChangedState
    {
        /// <summary>本字段上的全部监听，按声明顺序。</summary>
        public readonly List<ValueChangedEntry> Entries = new List<ValueChangedEntry>();
    }

    /// <summary><see cref="OnCollectionChangedAttribute"/> 的一个方向的回调。</summary>
    internal sealed class CollectionCallback
    {
        /// <summary>方法名（特性里写的那个）。</summary>
        public string Name;

        /// <summary>逐目标解析出的方法；解析失败时为 <c>null</c>。</summary>
        public MethodInfo[] Methods;

        /// <summary>逐目标的嵌套实例来源；顶层为 <c>null</c>（见 <see cref="NestedInstanceScope"/>）。</summary>
        public ReflectedAccessor[] Scopes;

        /// <summary>
        /// 这个方法收不收 <c>(CollectionChangeInfo, object)</c> 那两个参数。
        /// </summary>
        /// <remarks>
        /// 构建期定案。绘制期照它决定要不要备实参数组——按 <see cref="Methods"/> 现推
        /// 会把反射读放进每帧路径（虽然只在增删那一帧，但本仓的口径是能定案就定案）。
        /// </remarks>
        public bool TakesInfo;
    }

    /// <summary><see cref="OnCollectionChangedAttribute"/> 的每属性状态。</summary>
    /// <remarks>
    /// 变更的**时机**由集合绘制器在施加点前后触发（见 <c>CollectionChangeInvoker</c>），
    /// 这里只存「到时候该调谁」。
    /// </remarks>
    internal sealed class CollectionChangedState
    {
        /// <summary>改动施加**之前**的回调；没配时为 <c>null</c>。</summary>
        public CollectionCallback Before;

        /// <summary>改动施加**之后**的回调；没配时为 <c>null</c>。</summary>
        public CollectionCallback After;
    }

    /// <summary>
    /// <see cref="OnInspectorGUIAttribute"/> 的处理器：构建期解析出要调用的绘制方法。
    /// </summary>
    internal sealed class CustomGuiProcessor : AttributeProcessor<OnInspectorGUIAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            OnInspectorGUIAttribute attribute,
            IList<Attribute> attributes)
        {
            var state = property.State.GetOrCreate<CustomGuiState>();

            if (!(property.Member is MethodInfo discovery))
            {
                state.Reason = "方法节点没有记住要调用的方法。";
                return;
            }

            if (discovery.GetParameters().Length > 0 || discovery.ContainsGenericParameters)
            {
                state.Reason = $"[OnInspectorGUI] 只能标在**无参、非泛型**的方法上，"
                               + $"而 \"{discovery.Name}\" 不是。";
                Debug.LogWarning($"[XInspector] 方法「{discovery.Name}」{state.Reason}");
                return;
            }

            var targets = property.Owner?.Targets;

            if (targets == null || targets.Length == 0)
            {
                state.Reason = "取不到目标对象，无法调用绘制方法。";
                return;
            }

            var methods = new MethodInfo[targets.Length];

            for (var i = 0; i < targets.Length; i++)
            {
                methods[i] = TargetObjects.IsAlive(targets[i])
                    ? MethodResolver.BySignature(targets[i].GetType(), discovery)
                    : null;
            }

            if (methods[0] == null)
            {
                state.Reason = $"在目标对象上找不到方法 \"{discovery.Name}\"，无法绘制。";
                return;
            }

            state.Methods = methods;
            state.SingleTarget = new[] { targets[0] };
            state.SingleMethod = new[] { methods[0] };
        }
    }

    /// <summary>
    /// <see cref="CustomContextMenuAttribute"/> 的处理器：构建期解析出各菜单项要调用的方法。
    /// </summary>
    internal sealed class ContextMenuProcessor : AttributeProcessor<CustomContextMenuAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            CustomContextMenuAttribute attribute,
            IList<Attribute> attributes)
        {
            var state = property.State.GetOrCreate<ContextMenuState>();

            var entry = new ContextMenuEntry
            {
                Attribute = attribute,
                MenuItem = attribute.MenuItem,
                Methods = NamedMethodResolver.Resolve(
                    property, attribute.MethodName, "[CustomContextMenu]", out var scopes, out _),
                Scopes = scopes,
            };

            state.Entries.Add(entry);

            if (state.Owner == null)
            {
                state.Owner = attribute;
            }
        }
    }

    /// <summary>
    /// <see cref="OnValueChangedAttribute"/> 的处理器：构建期解析出各监听要调用的方法。
    /// </summary>
    internal sealed class ValueChangedProcessor : AttributeProcessor<OnValueChangedAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            OnValueChangedAttribute attribute,
            IList<Attribute> attributes)
        {
            var state = property.State.GetOrCreate<ValueChangedState>();

            state.Entries.Add(new ValueChangedEntry
            {
                Attribute = attribute,
                MethodName = attribute.MethodName,
                Methods = NamedMethodResolver.Resolve(
                    property, attribute.MethodName, "[OnValueChanged]", out var scopes, out _),
                Scopes = scopes,
            });
        }
    }

    /// <summary>
    /// <see cref="OnCollectionChangedAttribute"/> 的处理器：构建期解析两个方向的回调，
    /// 并保证这个特性**不会写了个寂寞**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>没标 <c>[ListDrawerSettings]</c> 时注入一份。</b> 集合的增删只有集合绘制器有落点，
    /// 不注入就会「写了没反应」——本包最忌讳的静默。代价是该字段的外观从原生变为自绘
    /// （与 <c>[TableList]</c> 的既有先例同款，写进包 README）。
    /// </para>
    /// <para>
    /// 标在非集合上时**告警并忽略**：那时既没有可触发的改动，注入也没有意义。
    /// </para>
    /// </remarks>
    internal sealed class CollectionChangedProcessor : AttributeProcessor<OnCollectionChangedAttribute>
    {
        #region Private Fields

        /// <summary>两个回调都收的两种形状，**按顺序试**。</summary>
        private static readonly Type[][] Shapes =
        {
            Type.EmptyTypes,
            new[] { typeof(CollectionChangeInfo), typeof(object) },
        };

        #endregion

        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            OnCollectionChangedAttribute attribute,
            IList<Attribute> attributes)
        {
            if (!CollectionDrawerLayout.CanDraw(property.ValueEntry?.SerializedProperty))
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上的 [OnCollectionChanged] 只支持数组或 List，"
                    + "该特性已忽略（没有增删就无从触发）。");
                return;
            }

            CollectionDrawerLayout.EnsureListSettings(attributes);

            var state = property.State.GetOrCreate<CollectionChangedState>();
            state.Before = Resolve(property, attribute.Before, "改动前回调");
            state.After = Resolve(property, attribute.After, "改动后回调");
        }

        #region Private Helpers

        /// <summary>解析一个方向的方法名；没配这个方向时返回 <c>null</c>。</summary>
        /// <param name="property">属性。</param>
        /// <param name="methodName">方法名；<c>null</c> 表示这个方向不要。</param>
        /// <param name="direction">方向的中文名（告警文案用）。</param>
        /// <returns>回调条目；解析不到时返回 <c>null</c>。</returns>
        private static CollectionCallback Resolve(InspectorProperty property, string methodName, string direction)
        {
            if (methodName == null)
            {
                return null;
            }

            var methods = NamedMethodResolver.Resolve(
                property, methodName, $"[OnCollectionChanged] 的{direction}", Shapes, out var scopes, out _);

            if (methods == null)
            {
                return null;
            }

            return new CollectionCallback
            {
                Name = methodName,
                Methods = methods,
                Scopes = scopes,
                TakesInfo = TakesInfo(methods),
            };
        }

        /// <summary>
        /// 这批方法收不收那两个参数。
        /// </summary>
        /// <param name="methods">逐目标解析出的方法。</param>
        /// <returns>收参数返回 <c>true</c>。</returns>
        /// <remarks>
        /// 取**第一个解析成功**的那一份：<c>MethodInvoker</c> 遇到 <c>methods[0] == null</c> 直接返回，
        /// 而各目标的形状已被解析器强制统一，故任取一份非空的都一样。
        /// </remarks>
        private static bool TakesInfo(MethodInfo[] methods)
        {
            for (var i = 0; i < methods.Length; i++)
            {
                if (methods[i] != null)
                {
                    return methods[i].GetParameters().Length > 0;
                }
            }

            return false;
        }

        #endregion
    }
}
