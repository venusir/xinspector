using System;
using System.Collections.Generic;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <c>[TypeSelectorSettings].FilterTypesFunction</c> 的处理器：构建期把方法名解析成
    /// 候选过滤器，存进 <see cref="TypeSelectorFilterState"/>。
    /// </summary>
    /// <remarks>
    /// <b>解析失败只忽略过滤器、不失效特性</b>（一条告警，文案里说清影响范围）：候选不做这道收窄，
    /// 选择器照常可用。不写（或空白）表示不过滤——**不建状态也不告警**（空串是「没写」不是「写错」）。
    /// </remarks>
    internal sealed class TypeSelectorSettingsProcessor : AttributeProcessor<TypeSelectorSettingsAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            TypeSelectorSettingsAttribute attribute,
            IList<Attribute> attributes)
        {
            if (string.IsNullOrWhiteSpace(attribute.FilterTypesFunction))
            {
                return;
            }

            var state = property.State.GetOrCreate<TypeSelectorFilterState>();

            if (!TypeFunctionBinder.TryBindFilter(
                    property, attribute.FilterTypesFunction, out var filter, out var reason))
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上的 [TypeSelectorSettings] 过滤器" +
                    $"「{attribute.FilterTypesFunction}」无法解析：{reason}" +
                    "该过滤器已忽略——候选不做这一道收窄，类型选择器照常可用。" +
                    "过滤器必须是**单参 Type、返回 bool** 的实例或静态方法" +
                    $"（形参名官方约定为 \"{TypeSelectorSettingsAttribute.FILTER_TYPES_FUNCTION_NAMED_VALUE}\"；" +
                    "本包按位置调用、不校验名字）；嵌套层 / 元素层 / 多态层里找的是**那一层的实例**；" +
                    "Odin 的 $/@ 表达式语言本包不做。");
                return;
            }

            state.Filter = filter;
            state.Resolved = true;
        }
    }

    /// <summary>
    /// <c>[PolymorphicDrawerSettings].CreateInstanceFunction</c> 的处理器：构建期把方法名解析成
    /// 自定义造实例，存进 <see cref="PolymorphicCreateInstanceState"/>。
    /// </summary>
    /// <remarks>
    /// <b>解析失败回落内置工厂</b>（一条告警）：配置错不该让「换类型」整个不可用——与绘制期的
    /// 「函数返回 <c>null</c> 不回落」是两件事（那边是函数明确拒绝，这边是压根没解析出来）。
    /// </remarks>
    internal sealed class PolymorphicCreateInstanceProcessor
        : AttributeProcessor<PolymorphicDrawerSettingsAttribute>
    {
        /// <inheritdoc/>
        protected override void ProcessSelf(
            InspectorProperty property,
            PolymorphicDrawerSettingsAttribute attribute,
            IList<Attribute> attributes)
        {
            if (string.IsNullOrWhiteSpace(attribute.CreateInstanceFunction))
            {
                return;
            }

            var state = property.State.GetOrCreate<PolymorphicCreateInstanceState>();

            if (!TypeFunctionBinder.TryBindFactory(
                    property, attribute.CreateInstanceFunction, out var factory, out var reason))
            {
                Debug.LogWarning(
                    $"[XInspector] 属性「{property.Path}」上的 [PolymorphicDrawerSettings] 造实例函数" +
                    $"「{attribute.CreateInstanceFunction}」无法解析：{reason}" +
                    "该函数已忽略——换类型**退回内置的造实例**（NonDefaultConstructorPreference 那一套），" +
                    "候选收窄也照旧。函数必须是**单参 Type、返回实例**的实例或静态方法；" +
                    "返回 null 表示「这个类型不给实例」，届时写入会被拒绝、**不回落内置工厂**。");
                return;
            }

            state.CreateInstance = factory;
            state.Resolved = true;
        }
    }

    /// <summary>
    /// <c>[TypeSelectorSettings].FilterTypesFunction</c> 的解析结果，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    /// <remarks>没有本状态（或 <see cref="Resolved"/> 为假）都表示「不过滤」——绘制期静默退回，
    /// 构建期已各告警过（照 <c>[ValueDropdown]</c> 的分工）。</remarks>
    internal sealed class TypeSelectorFilterState
    {
        /// <summary>解析出的过滤器；不过滤时为 <c>null</c>。</summary>
        public Func<Type, bool> Filter;

        /// <summary>解析成功了吗。</summary>
        public bool Resolved;
    }

    /// <summary>
    /// <c>[PolymorphicDrawerSettings].CreateInstanceFunction</c> 的解析结果。
    /// </summary>
    internal sealed class PolymorphicCreateInstanceState
    {
        /// <summary>解析出的自定义造实例；用内置工厂时为 <c>null</c>。</summary>
        public Func<Type, object> CreateInstance;

        /// <summary>解析成功了吗。</summary>
        public bool Resolved;
    }

    /// <summary>
    /// 绘制期读 <c>[TypeSelectorSettings]</c> 的一次性视图：三个显示旋钮 + 过滤器。
    /// </summary>
    /// <remarks>
    /// <b>特性为 <c>null</c> 时三个旋钮全为 <c>true</c>、过滤器为 <c>null</c>——与基座既有行为逐字一致</b>
    /// （「不写特性就不改变菜单形状」这条契约的落点）。状态缺或未解析时过滤器为 <c>null</c>
    /// （构建期已告警过，这里不重复）。
    /// </remarks>
    internal readonly struct TypeSelectorSettingsView
    {
        /// <summary>候选过滤器；<c>null</c> 表示不过滤。</summary>
        public readonly Func<Type, bool> Filter;

        /// <summary>类别名取命名空间（<c>true</c>）还是程序集简单名（<c>false</c>）。</summary>
        public readonly bool PreferNamespaces;

        /// <summary>菜单分不分类别层。</summary>
        public readonly bool ShowCategories;

        /// <summary>有值时给不给「（无）」清空入口。</summary>
        public readonly bool ShowNoneItem;

        private TypeSelectorSettingsView(
            Func<Type, bool> filter, bool preferNamespaces, bool showCategories, bool showNoneItem)
        {
            Filter = filter;
            PreferNamespaces = preferNamespaces;
            ShowCategories = showCategories;
            ShowNoneItem = showNoneItem;
        }

        /// <summary>读一个节点的视图。</summary>
        /// <param name="property">节点。</param>
        /// <returns>视图。</returns>
        public static TypeSelectorSettingsView Of(InspectorProperty property)
        {
            var attribute = property?.Attributes.Get<TypeSelectorSettingsAttribute>();
            var filter = property?.State.Get<TypeSelectorFilterState>();

            return new TypeSelectorSettingsView(
                filter != null && filter.Resolved ? filter.Filter : null,
                attribute == null || attribute.PreferNamespaces,
                attribute == null || attribute.ShowCategories,
                attribute == null || attribute.ShowNoneItem);
        }
    }
}
