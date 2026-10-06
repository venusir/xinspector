using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="ToggleGroupAttribute"/>：组标题前的复选框 + 关掉时不画组内内容。
    /// <para>
    /// 权重 <c>-180</c>：与折叠同档思路——**决定内容存在与否的恒最外**，
    /// 关掉时内侧的框、标题、行都该一起消失。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>解析发生在首次绘制</b>（结果缓存进 <see cref="PropertyState"/>）——这是本包唯一一处
    /// 绘制期解析，但理由与条件族不同：它的门必须让**开关控件本身保持可见**（关掉时消失的是
    /// 组内内容与框，开关那一行照画），「画者自己决定调不调下一个」本来就是绘制期的事。
    /// 构建期的第二趟处理器（分组装配之后）解决不了这个——它只能决定**整个**分组节点的
    /// 可见性，那会把开关也一起藏掉。
    /// <c>SerializedProperty</c> 是活句柄（跨 <c>Update()</c> 有效），与条件族的用法一致，
    /// 解析一次就够。
    /// </para>
    /// <para>
    /// 解析本身（含「序列化对象从**第一个带值入口的后代**取，不能假定 <c>Children[0]</c>——
    /// 页签容器的第一个孩子是页节点，同样是分组、同样没有值入口」这条）已收敛进
    /// <see cref="MemberReferenceResolver"/>，与条件族、<c>[Toggle]</c>、<c>[MinMaxSlider]</c>
    /// 共用同一层；本处是它唯一的绘制期调用方，告警机制（<see cref="DrawerWarnings.Once"/>）
    /// 仍是本处独有的。
    /// </para>
    /// <para>
    /// <b>开关可以是反射成员（非序列化属性 / 无参方法）</b>——那时复选框画成**禁用**、
    /// 标题上带一句说明，门控照常生效。不给写是因为本包对反射成员一律不给写
    /// （写进去既不可撤销也不会随存档保存），而画一个点了没反应的开关比画个禁用的更糟。
    /// </para>
    /// </remarks>
    [DrawerPriority(-180d)]
    internal sealed class ToggleGroupDrawer : AttributeDrawer<ToggleGroupAttribute>
    {
        #region Private Fields

        /// <summary>复选框列宽（像素）。</summary>
        private const float ToggleWidth = 16f;

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, ToggleGroupAttribute attribute, GUIContent label)
        {
            var state = property.State.GetOrCreate<ToggleGroupState>();

            if (!state.Initialized)
            {
                state.Initialized = true;

                var title = string.IsNullOrWhiteSpace(attribute.ToggleGroupTitle)
                    ? attribute.ToggleMemberName
                    : attribute.ToggleGroupTitle;

                var resolved = ToggleGroupFlag.Resolve(property, attribute.ToggleMemberName, out var reason);
                state.Read = resolved.Read;
                state.Flag = resolved.Serialized;

                // 反射开关画的是禁用复选框：标题上带一句说明，否则「点不动」看着像坏了。
                state.Title = state.Flag == null && state.Read != null
                    ? new GUIContent(title, ToggleGroupFlag.ReadOnlyTooltip(attribute.ToggleMemberName))
                    : new GUIContent(title);

                if (state.Read == null)
                {
                    DrawerWarnings.Once(property, nameof(ToggleGroupDrawer) + "." + attribute.ToggleMemberName,
                        $"[XInspector] 分组「{property.Path}」上的 [ToggleGroup] 开关「{attribute.ToggleMemberName}」" +
                        $"无法解析：{reason}。开关已忽略，组内内容照常显示。");
                }
            }

            if (state.Read == null)
            {
                // 解析失败：恒显示内容——拼错的名字不该让一整组字段消失。
                CallNextDrawer(property, label);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(ToggleWidth));

                if (state.Flag != null)
                {
                    var previousMixed = EditorGUI.showMixedValue;

                    try
                    {
                        // showMixedValue 是全局状态，必须还原——漏还原会让别的 Inspector 显示成混合态。
                        EditorGUI.showMixedValue = state.Flag.hasMultipleDifferentValues;
                        state.Flag.boolValue = EditorGUI.Toggle(rect, state.Flag.boolValue);
                    }
                    finally
                    {
                        EditorGUI.showMixedValue = previousMixed;
                    }
                }
                else
                {
                    // 反射开关：只显示不写。禁用而不是照画——点了没反应的控件比画成禁用的更糟。
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUI.Toggle(rect, state.Read());
                    }
                }

                EditorGUILayout.LabelField(state.Title, EditorStyles.boldLabel);
            }

            if (state.Read())
            {
                CallNextDrawer(property, label);
            }
        }

        #endregion
    }

    /// <summary>
    /// 开关成员的解析。静态纯函数（除取序列化对象外不碰 GUI），可无头测试。
    /// </summary>
    internal static class ToggleGroupFlag
    {
        #region Public API

        /// <summary>
        /// 在同一个对象上解析开关成员。
        /// </summary>
        /// <param name="property">分组节点。</param>
        /// <param name="memberName">成员路径。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>解析结果；失败时 <see cref="ToggleGroupFlagReference.Read"/> 为 <c>null</c>。</returns>
        /// <remarks>
        /// 解析本身（含「分组节点没有值入口、沿后代找」与那条四级阶梯）在
        /// <see cref="MemberReferenceResolver"/>；这里只是它在绘制期的一个调用方。
        /// </remarks>
        public static ToggleGroupFlagReference Resolve(
            InspectorProperty property, string memberName, out string reason)
        {
            if (!MemberReferenceResolver.TryResolveBoolean(
                    property, memberName, MemberScope.Object,
                    out var read, out var serialized, out reason))
            {
                read = null;
                serialized = null;
            }

            return new ToggleGroupFlagReference(read, serialized);
        }

        /// <summary>
        /// 反射开关的说明文本（挂在标题上）——解释那个复选框为什么点不动。
        /// </summary>
        /// <param name="memberName">开关成员名。</param>
        /// <returns>可读文本。</returns>
        public static string ReadOnlyTooltip(string memberName)
        {
            return $"开关「{memberName}」是反射成员（不在 Unity 的序列化里），这里只显示它的值：" +
                   "写进去既不可撤销，也不会随存档保存。";
        }

        #endregion
    }

    /// <summary>
    /// <c>[ToggleGroup]</c> 开关的解析结果。
    /// </summary>
    /// <remarks>
    /// <see cref="Serialized"/> 非 <c>null</c> ⇔ 开关是**序列化**成员，也就是「这个复选框可写」；
    /// 否则是个反射开关——<see cref="Read"/> 照常给值，绘制器据此画禁用的复选框。
    /// 解析失败时两者皆为 <c>null</c>。
    /// </remarks>
    internal readonly struct ToggleGroupFlagReference
    {
        #region Construction

        /// <summary>构造解析结果。</summary>
        /// <param name="read">每帧现读的读取器。</param>
        /// <param name="serialized">序列化开关的活句柄；反射开关为 <c>null</c>。</param>
        public ToggleGroupFlagReference(Func<bool> read, SerializedProperty serialized)
        {
            Read = read;
            Serialized = serialized;
        }

        #endregion

        #region Public API

        /// <summary>每帧现读的读取器；解析失败为 <c>null</c>。</summary>
        public Func<bool> Read { get; }

        /// <summary>序列化开关的活句柄（可写）；反射开关或解析失败为 <c>null</c>。</summary>
        public SerializedProperty Serialized { get; }

        #endregion
    }

    /// <summary>
    /// 开关分组的每属性状态，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    /// <remarks>
    /// 解析失败时 <see cref="Read"/> 为 <c>null</c>——绘制器据此决定不画开关，
    /// 但内侧照常往下传（字段不会因此消失）。
    /// </remarks>
    internal sealed class ToggleGroupState
    {
        /// <summary>是否已解析过开关（解析只做一次）。</summary>
        public bool Initialized;

        /// <summary>每帧现读的读取器；解析失败为 <c>null</c>。</summary>
        public Func<bool> Read;

        /// <summary>序列化开关的活句柄（可写）；反射开关为 <c>null</c>。</summary>
        public SerializedProperty Flag;

        /// <summary>缓存的标题内容（反射开关那句只读说明挂在它上面）。</summary>
        public GUIContent Title;
    }
}
