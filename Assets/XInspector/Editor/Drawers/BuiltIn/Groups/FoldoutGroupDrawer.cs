using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="FoldoutGroupAttribute"/>：画折叠三角，收起时不画组内内容。
    /// <para>
    /// 权重 <c>-190</c>：分组带里最外的一档。折叠要把内侧一切（框、标题、行）**一起**收掉，
    /// 否则收起后会留下一个残缺的框或标题。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 「收起时不算内容」用的正是链条本来就支持的能力——**不调用下一个绘制器**
    /// 等于把内侧藏起来，不需要任何特例机制。
    /// </remarks>
    [DrawerPriority(-190d)]
    internal sealed class FoldoutGroupDrawer : AttributeDrawer<FoldoutGroupAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, FoldoutGroupAttribute attribute, GUIContent label)
        {
            var state = property.State.GetOrCreate<FoldoutGroupState>();

            if (!state.Initialized)
            {
                // PropertyState 没有 Set<T>（只能 GetOrCreate 出默认实例），带初值的状态
                // 一律用「标志位 + 首次初始化」这个模式。
                state.Initialized = true;
                state.Expanded = attribute.Expanded;
                state.Title = new GUIContent(attribute.GroupName);
            }

            state.Expanded = EditorGUILayout.Foldout(state.Expanded, state.Title, true);

            if (state.Expanded)
            {
                CallNextDrawer(property, label);
            }
        }

        #endregion
    }

    /// <summary>
    /// 折叠组的每属性状态，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    /// <remarks>
    /// 展开状态**不跨会话持久化**：域重载、重开 Inspector 都回到特性的 <c>Expanded</c> 初值。
    /// 存哪（<c>EditorPrefs</c> / <c>SessionState</c> / 场景）仍是未决项，见 Roadmap。
    /// </remarks>
    internal sealed class FoldoutGroupState
    {
        /// <summary>是否已按特性初始化过初值。</summary>
        public bool Initialized;

        /// <summary>当前是否展开。</summary>
        public bool Expanded;

        /// <summary>折叠标题的内容（缓存的 <see cref="GUIContent"/>，避免每帧新建）。</summary>
        public GUIContent Title;
    }
}
