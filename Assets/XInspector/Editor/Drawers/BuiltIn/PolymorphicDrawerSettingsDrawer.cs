using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="PolymorphicDrawerSettingsAttribute"/>：**未展开**的多态槽位那条链上的绘制器。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>替换型</b>：空槽位时画完不调下一个（那一行由本包画完，原生就没得画了）。
    /// 其余各档一律 <c>CallNextDrawer</c> 退回——尤其是**多选**（本包的选择器表达不了
    /// 「各目标类型不同」，而原生多态行本来就支持多目标赋值）。
    /// </para>
    /// <para>
    /// <b>为什么还需要这一枚：</b>展开态的末端（<see cref="PolymorphicRowTerminalDrawer"/>）
    /// 只管得到**有层状态**的节点；空槽位、撞守卫、类型用不到本包这几格**没有层状态**，
    /// 末端是原生兜底——那一行只能由链上的绘制器接管。两处共用
    /// <see cref="PolymorphicRow"/> 的那一枚实现。
    /// </para>
    /// </remarks>
    [DrawerPriority(0d)]
    internal sealed class PolymorphicDrawerSettingsDrawer : AttributeDrawer<PolymorphicDrawerSettingsAttribute>
    {
        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property, PolymorphicDrawerSettingsAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;
            var declaredType = PolymorphicRow.DeclaredTypeOf(property);

            switch (PolymorphicRow.Decide(property, serializedProperty, declaredType))
            {
                case PolymorphicRowDisposition.DrawRow:
                    PolymorphicRow.Draw(property, serializedProperty, declaredType, attribute, label);
                    return;

                case PolymorphicRowDisposition.FallbackNotBacked:
                    DrawerWarnings.Once(
                        property,
                        nameof(PolymorphicDrawerSettingsDrawer) + ".backend",
                        DrawerWarnings.TypeMismatch(
                            property, "[PolymorphicDrawerSettings]", "Unity 的序列化后端（托管引用）"));
                    break;

                case PolymorphicRowDisposition.FallbackTypeSlot:
                    DrawerWarnings.Once(
                        property,
                        nameof(PolymorphicDrawerSettingsDrawer) + ".typeslot",
                        $"[XInspector] 属性「{property.Path}」是 System.Type 的**类型槽位**——" +
                        "[PolymorphicDrawerSettings] 管的是多态引用；这一行归 [TypeDrawerSettings]。");
                    break;

                case PolymorphicRowDisposition.FallbackNotTakenOver:
                    DrawerWarnings.Once(
                        property,
                        nameof(PolymorphicDrawerSettingsDrawer) + ".not-taken-over",
                        $"[XInspector] 属性「{property.Path}」里的类型没有用到本包（或它的成员里没有本包特性）——" +
                        "这一行退回 Unity 原生，[PolymorphicDrawerSettings] 的旋钮此刻不生效。" +
                        "给那个类型加一个本包特性（如 [BoxGroup]）即可让本包接管这一行。");
                    break;

                // TerminalOwnsRow / FallbackMultiSelect / FallbackGuarded：让路，不告警。
            }

            CallNextDrawer(property, label);
        }
    }
}
