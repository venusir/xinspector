using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="TabGroupAttribute"/>：容器画页签栏并只显示选中的那一页；页节点只把内容传下去。
    /// <para>
    /// 权重 <c>-170</c>：页签栏与「选哪一页」必须在页内容的一切装饰之外——
    /// 排在框内侧的话，框会跟着每一页各画一次。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 「只画选中页」不是自己驱动子节点，而是把选择装进 <see cref="GroupChildrenLayout"/>，
    /// 由末端 <see cref="ChildrenDrawer"/> 消费——见那个类的说明。
    /// </remarks>
    [DrawerPriority(-170d)]
    internal sealed class TabGroupDrawer : AttributeDrawer<TabGroupAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, TabGroupAttribute attribute, GUIContent label)
        {
            if (!attribute.IsContainer)
            {
                // 页节点：自己不画任何东西，内容由容器的选页策略决定画不画。
                CallNextDrawer(property, label);
                return;
            }

            var children = property.Children;
            var count = children.Count;

            if (count == 0)
            {
                CallNextDrawer(property, label);
                return;
            }

            var state = property.State.GetOrCreate<TabGroupState>();

            if (state.TabNames == null)
            {
                // 页名只在构建后取一次（树结构不随帧变化）；每帧新建 string[] 是纯浪费。
                state.TabNames = new string[count];
                for (var i = 0; i < count; i++)
                {
                    state.TabNames[i] = children[i].Name;
                }
            }

            // 选中页越界（例如子节点数变过）回退到第一页——不静默什么都不画。
            if (state.SelectedIndex < 0 || state.SelectedIndex >= count)
            {
                state.SelectedIndex = 0;
            }

            if (count > 1 || !attribute.HideTabGroupIfTabGroupOnlyHasOneTab)
            {
                state.SelectedIndex = GUILayout.Toolbar(state.SelectedIndex, state.TabNames);
            }

            var layout = property.State.GetOrCreate<GroupChildrenLayout>();
            layout.HasPolicy = true;
            layout.OnlyChildIndex = state.SelectedIndex;
            layout.CellGap = 0f;

            CallNextDrawer(property, label);
        }

        #endregion
    }

    /// <summary>
    /// 页签组的每属性状态，挂在 <see cref="PropertyState"/> 上。
    /// </summary>
    /// <remarks>选中页**不跨会话持久化**：域重载、重开 Inspector 都回到第一页。</remarks>
    internal sealed class TabGroupState
    {
        /// <summary>当前选中的页下标。</summary>
        public int SelectedIndex;

        /// <summary>页名缓存（子节点顺序固定，取一次即可）。</summary>
        public string[] TabNames;
    }
}
