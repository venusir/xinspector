using System;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 根节点的末端绘制器：依次画出所有顶层子节点。
    /// <para>
    /// 由构建期**显式追加**到根属性的链尾，不经注册表匹配——
    /// 见 <see cref="DrawerChainBuilder"/> 对「末端是结构性的」的说明。
    /// </para>
    /// </summary>
    internal sealed class RootChildrenDrawer : XInspectorDrawer
    {
        #region XInspectorDrawer

        /// <summary>
        /// 恒为 <c>false</c>：末端绘制器由构建期显式追加，从不参与自动匹配。
        /// <para>
        /// 若这里返回 <c>true</c>，它会被自动加进每一条根属性的链，
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
