using System;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 容器节点的末端绘制器：依次画出所有子节点。
    /// <para>
    /// 根节点与分组节点共用同一实现——两者在这个位置上做的事完全一样（画子节点），
    /// 差别只在**兄弟链上还挂了什么**：分组节点多一个分组绘制器负责画框，
    /// 根节点则通常只有类级特性合成出来的绘制器。差别在链的其他格子里，
    /// 不在这里，所以没有必要为它们各写一个类。
    /// </para>
    /// <para>
    /// 由构建期**显式追加**到链尾，不经注册表匹配——
    /// 见 <see cref="DrawerChainBuilder"/> 对「末端是结构性的」的说明。
    /// </para>
    /// </summary>
    internal sealed class ChildrenDrawer : XInspectorDrawer
    {
        #region XInspectorDrawer

        /// <summary>
        /// 恒为 <c>false</c>：末端绘制器由构建期显式追加，从不参与自动匹配。
        /// <para>
        /// 若这里返回 <c>true</c>，它会被自动加进每一条容器属性的链，
        /// 于是与显式追加的那一格重复，子节点会被画两遍。
        /// </para>
        /// </summary>
        /// <param name="property">候选属性。</param>
        /// <returns>恒为 <c>false</c>。</returns>
        public override bool CanDraw(InspectorProperty property)
        {
            return false;
        }

        /// <inheritdoc/>
        public override void DrawPropertyLayout(InspectorProperty property, Attribute attribute, GUIContent label)
        {
            property.DrawChildren();
        }

        #endregion
    }
}
