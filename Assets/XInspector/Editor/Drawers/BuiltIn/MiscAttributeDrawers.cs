using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="TypeInfoBoxAttribute"/>：类级信息框。
    /// <para>
    /// 类级特性落在根节点，根节点的链照样「包住子节点」，于是「整页顶部一条信息框」
    /// 不需要任何特例代码——与 <c>[Title]</c> 用在类上同一机制。
    /// </para>
    /// </summary>
    [DrawerPriority(-700d)]
    internal sealed class TypeInfoBoxDrawer : AttributeDrawer<TypeInfoBoxAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, TypeInfoBoxAttribute attribute, GUIContent label)
        {
            EditorGUILayout.HelpBox(attribute.Message, MessageType.Info);
            CallNextDrawer(property, label);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="DrawWithUnityAttribute"/>：把属性交回 Unity 的绘制路径。
    /// <para>
    /// 权重 <c>SuperPriority</c>（-1000）：几乎最外。与末端绘制器同法画
    /// <c>PropertyField</c>，但**不调用下一个**——链上更内侧的一切（包括其它修饰特性）
    /// 因此都不运行。这正是「交给 Unity」的含义。
    /// </para>
    /// </summary>
    [DrawerPriority(-1000d)]
    internal sealed class DrawWithUnityDrawer : AttributeDrawer<DrawWithUnityAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, DrawWithUnityAttribute attribute, GUIContent label)
        {
            var entry = property.ValueEntry;

            if (entry == null || !entry.IsUnityBacked)
            {
                // 与 UnityFallbackDrawer 同款守卫：没有 Unity 后端就画不出东西，
                // 明确提示而不是静默跳过（静默跳过会表现为「这个字段不见了」）。
                EditorGUILayout.HelpBox(
                    $"属性 \"{property.Path}\" 没有 Unity 序列化后端，[DrawWithUnity] 无法绘制它。",
                    MessageType.Warning);
                return;
            }

            // 与 UnityFallbackDrawer 一样自己处理只读——本绘制器不调下一个，
            // 末端那层禁用罩不会执行。
            using (new EditorGUI.DisabledScope(property.State.IsReadOnly))
            {
                EditorGUILayout.PropertyField(entry.SerializedProperty, label, true);
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="ChildGameObjectsOnlyAttribute"/>：引用不是子物体时告警。
    /// </summary>
    [DrawerPriority(-580d)]
    internal sealed class ChildGameObjectsOnlyDrawer : AttributeDrawer<ChildGameObjectsOnlyAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, ChildGameObjectsOnlyAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.ObjectReference)
            {
                DrawerWarnings.Once(property, nameof(ChildGameObjectsOnlyDrawer),
                    DrawerWarnings.TypeMismatch(property, "[ChildGameObjectsOnly]", "对象引用"));
            }
            else if (ChildObjectValidator.TryDescribeViolation(
                         serializedProperty, attribute.IncludeInactive, attribute.IncludeSelf, out var message))
            {
                EditorGUILayout.HelpBox(message, MessageType.Warning);
            }

            CallNextDrawer(property, label);
        }

        #endregion
    }

    /// <summary>
    /// 「引用必须是同一物体下的子物体」的判定。可无头测试（判定本身不碰 GUI）。
    /// </summary>
    internal static class ChildObjectValidator
    {
        #region Public API

        /// <summary>
        /// 判定引用是否违反「子物体限定」，并给出告警文本。
        /// </summary>
        /// <param name="property">对象引用属性。</param>
        /// <param name="includeInactive">是否允许非激活的子物体。</param>
        /// <param name="includeSelf">是否允许引用所在物体自己。</param>
        /// <param name="message">违反时的告警文本；未违反为 <c>null</c>。</param>
        /// <returns>违反返回 <c>true</c>。</returns>
        /// <remarks>
        /// 空引用**不算违反**——那是 <c>[Required]</c> 的职责。多对象编辑取第一个目标所在的物体
        /// 作为「根」（与条件族的现状一致：校验器不假装能同时校验多个不同的根）。
        /// </remarks>
        public static bool TryDescribeViolation(
            SerializedProperty property,
            bool includeInactive,
            bool includeSelf,
            out string message)
        {
            message = null;

            var reference = property.objectReferenceValue;
            if (reference == null)
            {
                return false;
            }

            var referenceTransform = TransformOf(reference);
            if (referenceTransform == null)
            {
                message = $"「{reference.name}」不是 GameObject、组件或 Transform，[ChildGameObjectsOnly] 无法判定它的层级。";
                return true;
            }

            var rootTransform = TransformOf(property.serializedObject.targetObject);
            if (rootTransform == null)
            {
                message = "[ChildGameObjectsOnly] 需要被检视对象是场景里的组件或 GameObject（它有层级可言），当前不是。";
                return true;
            }

            if (referenceTransform == rootTransform)
            {
                if (includeSelf)
                {
                    return false;
                }

                message = $"「{reference.name}」就是所在物体自己。想允许它请加 IncludeSelf = true。";
                return true;
            }

            if (!referenceTransform.IsChildOf(rootTransform))
            {
                message = $"「{reference.name}」不在「{rootTransform.name}」的层级之下。";
                return true;
            }

            if (!includeInactive && !referenceTransform.gameObject.activeInHierarchy)
            {
                message = $"「{reference.name}」是非激活的子物体。想允许它请加 IncludeInactive = true。";
                return true;
            }

            return false;
        }

        #endregion

        #region Private Helpers

        /// <summary>取对象背后的 Transform；取不到返回 <c>null</c>。</summary>
        /// <param name="target">对象。</param>
        /// <returns>Transform；<c>null</c> 表示该对象没有层级可言。</returns>
        private static Transform TransformOf(UnityEngine.Object target)
        {
            switch (target)
            {
                case Transform transform:
                    return transform;
                case Component component:
                    return component.transform;
                case GameObject gameObject:
                    return gameObject.transform;
                default:
                    // ScriptableObject、被销毁的对象、以及一切没有 Transform 的东西。
                    return null;
            }
        }

        #endregion
    }
}
