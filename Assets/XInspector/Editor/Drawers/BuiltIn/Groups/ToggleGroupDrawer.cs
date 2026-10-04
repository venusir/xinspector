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
    /// <b>解析发生在首次绘制</b>（结果缓存进 <see cref="PropertyState"/>）：
    /// 分组节点在构建期的处理器阶段还不存在，装不上去——这是本包唯一一处绘制期解析，
    /// 不是疏忽。`SerializedProperty` 是活句柄（跨 <c>Update()</c> 有效），
    /// 与条件族的用法一致，解析一次就够。
    /// </para>
    /// <para>
    /// 序列化对象从**第一个带值入口的后代**取，不能假定 <c>Children[0]</c>——
    /// 页签容器的第一个孩子是页节点，同样是分组、同样没有值入口。
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
                state.Title = new GUIContent(title);

                state.Flag = ToggleGroupFlag.Resolve(property, attribute.ToggleMemberName, out var reason);

                if (state.Flag == null)
                {
                    DrawerWarnings.Once(property, nameof(ToggleGroupDrawer) + "." + attribute.ToggleMemberName,
                        $"[XInspector] 分组「{property.Path}」上的 [ToggleGroup] 开关「{attribute.ToggleMemberName}」" +
                        $"无法解析：{reason}。开关已忽略，组内内容照常显示。");
                }
            }

            if (state.Flag == null)
            {
                // 解析失败：恒显示内容——拼错的名字不该让一整组字段消失。
                CallNextDrawer(property, label);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(ToggleWidth));
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

                EditorGUILayout.LabelField(state.Title, EditorStyles.boldLabel);
            }

            if (state.Flag.boolValue)
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
        /// <returns>开关的序列化属性；失败返回 <c>null</c>。</returns>
        public static SerializedProperty Resolve(InspectorProperty property, string memberName, out string reason)
        {
            reason = null;

            var serializedObject = FindSerializedObject(property);
            if (serializedObject == null)
            {
                reason = "这个分组下没有任何成员，取不到序列化对象";
                return null;
            }

            var flag = serializedObject.FindProperty(memberName);
            if (flag == null)
            {
                reason = "在同一个对象上找不到这个成员（名字拼错，或它不是序列化成员）";
                return null;
            }

            if (flag.propertyType != SerializedPropertyType.Boolean)
            {
                reason = $"该成员不是 bool（实为 {flag.propertyType}）";
                return null;
            }

            return flag;
        }

        #endregion

        #region Private Helpers

        /// <summary>递归找**第一个带值入口**的节点，取它的序列化对象。</summary>
        /// <param name="property">起点。</param>
        /// <returns>序列化对象；整棵子树都没有值入口时返回 <c>null</c>。</returns>
        private static SerializedObject FindSerializedObject(InspectorProperty property)
        {
            var entry = property.ValueEntry;
            if (entry?.SerializedProperty != null)
            {
                return entry.SerializedProperty.serializedObject;
            }

            for (var i = 0; i < property.Children.Count; i++)
            {
                var found = FindSerializedObject(property.Children[i]);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        #endregion
    }

    /// <summary>
    /// 开关分组的每属性状态，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    internal sealed class ToggleGroupState
    {
        /// <summary>是否已解析过开关（解析只做一次）。</summary>
        public bool Initialized;

        /// <summary>开关的序列化属性；解析失败为 <c>null</c>。</summary>
        public SerializedProperty Flag;

        /// <summary>缓存的标题内容。</summary>
        public GUIContent Title;
    }
}
