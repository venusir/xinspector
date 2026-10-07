using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 展开过的多态容器的**自绘**末端（字段标了 <see cref="PolymorphicDrawerSettingsAttribute"/>）：
    /// 「那一行」换成 <see cref="PolymorphicRow"/> 的类型选择器，子节点那半与基类
    /// <see cref="ManagedReferenceTerminalDrawer"/> 共用。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 两个末端的差别**只有那一行**（<see cref="ManagedReferenceTerminalDrawer.DrawRow"/>）——
    /// 子节点那半含搜索框、只读罩、缩进与过滤四处判断，复制一份迟早漂，故走继承而不是并列。
    /// </para>
    /// <para>
    /// 它由 <c>PropertyTreeBuilder.TerminalFor</c> 在构建期按「有层状态 + 字段带特性」选定——
    /// 末端选型是这条分支**唯一能无头断言的地方**（本仓不测 IMGUI）。
    /// </para>
    /// </remarks>
    internal sealed class PolymorphicRowTerminalDrawer : ManagedReferenceTerminalDrawer
    {
        /// <inheritdoc/>
        protected override void DrawRow(
            InspectorProperty property, SerializedProperty serializedProperty, GUIContent label)
        {
            PolymorphicRow.Draw(
                property,
                serializedProperty,
                PolymorphicRow.DeclaredTypeOf(property),
                property.Attributes.Get<PolymorphicDrawerSettingsAttribute>(),
                label);
        }
    }
}
