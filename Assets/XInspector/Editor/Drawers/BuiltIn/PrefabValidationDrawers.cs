using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    // 两个预制体上下文校验特性的绘制器。它们与四个条件族的分工是同一条纪律：
    // **只读门控由处理器装**（见 PrefabValidationProcessors），
    // **要画东西的那一半在这里**，而「画不画」的判定收在纯函数里（PrefabModificationRules），
    // 于是决策可以无头测试，绘制器只剩「画 + 调下一个」。

    /// <summary>
    /// <see cref="DisallowModificationsInAttribute"/> 的覆盖判定规则。纯函数，可无头测试。
    /// </summary>
    internal static class PrefabModificationRules
    {
        /// <summary>
        /// 现在该不该报「这个值在加上本特性之前就已经被改过」。
        /// </summary>
        /// <param name="targetCount">树的目标个数。</param>
        /// <param name="hasValueEntry">这个成员有没有值入口。</param>
        /// <param name="prefabOverride">值相对预制体是不是覆盖状态。</param>
        /// <returns>该报返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>多目标时不报。</b> <c>SerializedProperty.prefabOverride</c> 在多选下反映的是谁、
        /// 官方没有写明（每个目标的覆盖状态本来就可能不同），照它报会给出一个无法解释的结论。
        /// 少报一句的代价，小于报一句查不出所以然的话。
        /// </para>
        /// <para>
        /// <b>没有值入口时不报</b>：<c>[ShowInInspector]</c> 的反射成员没有序列化属性，
        /// 谈不上「覆盖」。这一侧由绘制器单独告警一次（措辞要说清只读那一半仍然生效）。
        /// </para>
        /// </remarks>
        public static bool ShouldReportOverride(int targetCount, bool hasValueEntry, bool prefabOverride)
        {
            return targetCount == 1 && hasValueEntry && prefabOverride;
        }
    }

    /// <summary>
    /// <see cref="RequiredInAttribute"/>：**只在指定上下文里**为空时画一条提示框。
    /// </summary>
    /// <remarks>
    /// 判空规则整条复用 <see cref="RequiredValidator"/>——「空」的定义只能有一份，
    /// 这个特性多的只是「先看上下文对不对」这道门。层级固定为错误（官方没有级别参数）。
    /// 权重 <c>-590</c>：紧挨着 <see cref="RequiredDrawer"/>（<c>-600</c>）之内。
    /// </remarks>
    [DrawerPriority(-590d)]
    internal sealed class RequiredInDrawer : AttributeDrawer<RequiredInAttribute>
    {
        #region Public API

        /// <summary>未给自定义消息时使用的文本。</summary>
        public const string DefaultMessage = "此字段在当前的预制体上下文里为必填。";

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, RequiredInAttribute attribute, GUIContent label)
        {
            if (!PrefabContextProbe.MatchesAll(property.Owner?.Targets, attribute.PrefabKind))
            {
                CallNextDrawer(property, label);
                return;
            }

            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || !RequiredValidator.CanBeEmpty(serializedProperty))
            {
                DrawerWarnings.Once(property, nameof(RequiredInDrawer),
                    $"[XInspector] 属性「{property.Path}」上的 [RequiredIn] 只对可能为空的类型有意义" +
                    "（字符串、对象引用、数组与列表）；数值与 bool 恒视为有值，该特性已忽略。");
            }
            else if (RequiredValidator.IsEmpty(serializedProperty))
            {
                EditorGUILayout.HelpBox(attribute.ErrorMessage ?? DefaultMessage, MessageType.Error);
            }

            CallNextDrawer(property, label);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="DisallowModificationsInAttribute"/>：「加上本特性之前就已经被改过」的那条提示。
    /// </summary>
    /// <remarks>
    /// 这个特性有两件事，这里是其中一件：<b>禁用</b>那一半由处理器装只读解析器
    /// （处理器不得绘制）。判据是 <c>SerializedProperty.prefabOverride</c>——
    /// 它只在预制体实例上才有意义。权重 <c>-585</c>：与 <see cref="RequiredInDrawer"/> 同族。
    /// </remarks>
    [DrawerPriority(-585d)]
    internal sealed class DisallowModificationsInDrawer : AttributeDrawer<DisallowModificationsInAttribute>
    {
        #region Public API

        /// <summary>提示文本。</summary>
        public const string OverrideMessage =
            "这个值在本特性生效之前就已经改过了（相对预制体是覆盖状态）。" +
            "该改的请改到预制体上，或把这条特性去掉。";

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(
            InspectorProperty property,
            DisallowModificationsInAttribute attribute,
            GUIContent label)
        {
            var targets = property.Owner?.Targets;

            if (PrefabContextProbe.MatchesAll(targets, attribute.PrefabKind))
            {
                var serializedProperty = property.ValueEntry?.SerializedProperty;

                if (serializedProperty == null)
                {
                    DrawerWarnings.Once(property, nameof(DisallowModificationsInDrawer),
                        $"[XInspector] 属性「{property.Path}」上的 [DisallowModificationsIn] 需要值入口才能看出" +
                        "「有没有被改过」，而这是一个 [ShowInInspector] 的只读成员，该提示对它无效" +
                        "——只读那一半照常生效。");
                }
                else if (PrefabModificationRules.ShouldReportOverride(
                             targets.Length, true, serializedProperty.prefabOverride))
                {
                    EditorGUILayout.HelpBox(OverrideMessage, MessageType.Error);
                }
            }

            CallNextDrawer(property, label);
        }

        #endregion
    }
}
