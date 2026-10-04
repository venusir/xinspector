using System;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="RequiredAttribute"/>：为空时在字段上方画一条提示框。
    /// <para>
    /// 权重 <c>-600</c>：在信息框之内、值控件之外——它与值控件是「同一个字段的两件事」，
    /// 不该跑得比信息框还远。
    /// </para>
    /// </summary>
    [DrawerPriority(-600d)]
    internal sealed class RequiredDrawer : AttributeDrawer<RequiredAttribute>
    {
        #region Public API

        /// <summary>未给自定义消息时使用的文本。</summary>
        public const string DefaultMessage = "此字段为必填。";

        #endregion

        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, RequiredAttribute attribute, GUIContent label)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || !RequiredValidator.CanBeEmpty(serializedProperty))
            {
                DrawerWarnings.Once(property, nameof(RequiredDrawer),
                    $"[XInspector] 属性「{property.Path}」上的 [Required] 只对可能为空的类型有意义" +
                    "（字符串、对象引用、数组与列表）；数值与 bool 恒视为有值，该特性已忽略。");
                CallNextDrawer(property, label);
                return;
            }

            if (RequiredValidator.IsEmpty(serializedProperty))
            {
                EditorGUILayout.HelpBox(
                    attribute.ErrorMessage ?? DefaultMessage,
                    InfoMessageTypeMap.ToUnity(attribute.MessageType));
            }

            CallNextDrawer(property, label);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="AssetsOnlyAttribute"/>：引用了场景对象时告警。
    /// </summary>
    [DrawerPriority(-580d)]
    internal sealed class AssetsOnlyDrawer : AttributeDrawer<AssetsOnlyAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, AssetsOnlyAttribute attribute, GUIContent label)
        {
            ObjectReferenceWarning.DrawWarning(property, true, nameof(AssetsOnlyDrawer), "[AssetsOnly]");
            CallNextDrawer(property, label);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="SceneObjectsOnlyAttribute"/>：引用了工程资产时告警。
    /// </summary>
    [DrawerPriority(-570d)]
    internal sealed class SceneObjectsOnlyDrawer : AttributeDrawer<SceneObjectsOnlyAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, SceneObjectsOnlyAttribute attribute, GUIContent label)
        {
            ObjectReferenceWarning.DrawWarning(property, false, nameof(SceneObjectsOnlyDrawer), "[SceneObjectsOnly]");
            CallNextDrawer(property, label);
        }

        #endregion
    }

    /// <summary>
    /// <see cref="MinValueAttribute"/>：绘制后把值钳到不小于下限。
    /// </summary>
    /// <remarks>
    /// 钳制在 <c>CallNextDrawer</c> **之后**：先让值控件把用户输入写进序列化属性，
    /// 再钳——顺序反过来钳的是上一个已提交的值，用户会看到「填了越界值却没反应」。
    /// 本帧钳好的值由宿主的 <c>ApplyModifiedProperties</c> 统一提交（不自己 Apply）。
    /// </remarks>
    [DrawerPriority(-320d)]
    internal sealed class MinValueDrawer : AttributeDrawer<MinValueAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, MinValueAttribute attribute, GUIContent label)
        {
            CallNextDrawer(property, label);

            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (!ValueClamper.IsClampableType(serializedProperty))
            {
                DrawerWarnings.Once(property, nameof(MinValueDrawer),
                    DrawerWarnings.TypeMismatch(property, "[MinValue]", "数值类型（int、long、float、double）"));
                return;
            }

            if (ValueClamper.CanClamp(serializedProperty, property.State))
            {
                ValueClamper.TryClampMin(serializedProperty, attribute.MinValue);
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="MaxValueAttribute"/>：绘制后把值钳到不大于上限。语义同 <see cref="MinValueDrawer"/>。
    /// </summary>
    [DrawerPriority(-310d)]
    internal sealed class MaxValueDrawer : AttributeDrawer<MaxValueAttribute>
    {
        #region Protected API

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, MaxValueAttribute attribute, GUIContent label)
        {
            CallNextDrawer(property, label);

            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (!ValueClamper.IsClampableType(serializedProperty))
            {
                DrawerWarnings.Once(property, nameof(MaxValueDrawer),
                    DrawerWarnings.TypeMismatch(property, "[MaxValue]", "数值类型（int、long、float、double）"));
                return;
            }

            if (ValueClamper.CanClamp(serializedProperty, property.State))
            {
                ValueClamper.TryClampMax(serializedProperty, attribute.MaxValue);
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="RequiredAttribute"/> 的判空规则。纯函数，可无头测试。
    /// </summary>
    internal static class RequiredValidator
    {
        #region Public API

        /// <summary>
        /// 该属性是否**可能为空**。
        /// </summary>
        /// <param name="property">候选属性。</param>
        /// <returns>可能为空返回 <c>true</c>。</returns>
        /// <remarks>
        /// 数值、bool、嵌套结构在 Unity 序列化里恒有值，故 <c>[Required]</c> 对它们没有意义
        /// （官方的校验器也只覆盖引用类型）——绘制器据此告警一次。
        /// </remarks>
        public static bool CanBeEmpty(SerializedProperty property)
        {
            if (property == null)
            {
                return false;
            }

            return property.propertyType == SerializedPropertyType.String ||
                   property.propertyType == SerializedPropertyType.ObjectReference ||
                   property.isArray;
        }

        /// <summary>
        /// 当前值是否为空。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <returns>空返回 <c>true</c>。</returns>
        /// <remarks>
        /// null 引用、空串、空集合算空；**纯空白串按非空**（本包自定的语义，见特性的文档）。
        /// </remarks>
        public static bool IsEmpty(SerializedProperty property)
        {
            if (property.isArray)
            {
                return property.arraySize == 0;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue == null;
                case SerializedPropertyType.String:
                    return string.IsNullOrEmpty(property.stringValue);
                default:
                    return false;
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="AssetsOnlyAttribute"/> 与 <see cref="SceneObjectsOnlyAttribute"/> 的判定与绘制。
    /// </summary>
    internal static class ObjectReferenceWarning
    {
        #region Public API

        /// <summary>
        /// 画一条引用校验的告警（若有）。**不调用下一个绘制器**——调用方画完自己再调，
        /// 这样「不管告警与否，字段照常往下传」这条纪律落在绘制器那一侧，不会漏。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="requireAsset"><c>true</c> 要求工程资产，<c>false</c> 要求场景对象。</param>
        /// <param name="warningKey">告警去重键。</param>
        /// <param name="attributeName">特性名（用于告警文本）。</param>
        public static void DrawWarning(
            InspectorProperty property,
            bool requireAsset,
            string warningKey,
            string attributeName)
        {
            var serializedProperty = property.ValueEntry?.SerializedProperty;

            if (serializedProperty == null || serializedProperty.propertyType != SerializedPropertyType.ObjectReference)
            {
                DrawerWarnings.Once(property, warningKey,
                    DrawerWarnings.TypeMismatch(property, attributeName, "对象引用"));
                return;
            }

            if (TryDescribeViolation(serializedProperty, requireAsset, out var message))
            {
                EditorGUILayout.HelpBox(message, MessageType.Warning);
            }
        }

        /// <summary>
        /// 判定引用是否违反要求，并给出告警文本。
        /// </summary>
        /// <param name="property">对象引用属性。</param>
        /// <param name="requireAsset"><c>true</c> 要求工程资产，<c>false</c> 要求场景对象。</param>
        /// <param name="message">违反时的告警文本；未违反为 <c>null</c>。</param>
        /// <returns>违反返回 <c>true</c>。</returns>
        /// <remarks>
        /// 空引用**不算违反**——那是 <see cref="RequiredAttribute"/> 的职责，两个特性各管一段。
        /// </remarks>
        public static bool TryDescribeViolation(SerializedProperty property, bool requireAsset, out string message)
        {
            message = null;

            var target = property.objectReferenceValue;
            if (target == null)
            {
                return false;
            }

            // IsPersistent 为 true 即「工程资产」；场景对象（含预制体实例）为 false。
            var isAsset = EditorUtility.IsPersistent(target);
            if (requireAsset == isAsset)
            {
                return false;
            }

            message = requireAsset
                ? $"「{target.name}」不是工程资产。这里需要 Assets 下的资源；只想引用场景对象的话请改用 [SceneObjectsOnly]。"
                : $"「{target.name}」是工程资产。这里需要场景里的对象；想引用资源的话请改用 [AssetsOnly]。";
            return true;
        }

        #endregion
    }

    /// <summary>
    /// <see cref="MinValueAttribute"/> 与 <see cref="MaxValueAttribute"/> 的钳制逻辑。纯函数，可无头测试。
    /// </summary>
    internal static class ValueClamper
    {
        #region Public API

        /// <summary>
        /// 该属性是否是钳制支持的类型（整数与浮点）。
        /// </summary>
        /// <param name="property">候选属性。</param>
        /// <returns>支持返回 <c>true</c>。</returns>
        public static bool IsClampableType(SerializedProperty property)
        {
            return property != null &&
                   (property.propertyType == SerializedPropertyType.Integer ||
                    property.propertyType == SerializedPropertyType.Float);
        }

        /// <summary>
        /// 现在该不该钳。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="state">属性的可变状态。</param>
        /// <returns>可以钳返回 <c>true</c>。</returns>
        /// <remarks>
        /// 两种情形跳过，理由都是「**不悄悄改数据**」：
        /// 多对象编辑且各目标值不一致（读到的不是任何目标的真实值，写回会把它们统一），
        /// 以及字段当前只读（只读意味着这个值不归你改——它可能正被游戏逻辑持有）。
        /// </remarks>
        public static bool CanClamp(SerializedProperty property, PropertyState state)
        {
            return IsClampableType(property) &&
                   !property.hasMultipleDifferentValues &&
                   (state == null || !state.IsReadOnly);
        }

        /// <summary>
        /// 把值抬到不小于 <paramref name="min"/>。
        /// </summary>
        /// <param name="property">数值属性。</param>
        /// <param name="min">下限。</param>
        /// <returns>改动了值返回 <c>true</c>。</returns>
        /// <remarks>
        /// 整数类型对下限**向上取整**：<c>[MinValue(2.5)]</c> 的最小合法整数是 3。
        /// 下限超出整数可表达的范围（或为 NaN）时不动数据——钳到一个表达不出来的值是错的。
        /// </remarks>
        public static bool TryClampMin(SerializedProperty property, double min)
        {
            if (double.IsNaN(min))
            {
                return false;
            }

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                if (min > long.MaxValue)
                {
                    return false;
                }

                var bound = (long)Math.Ceiling(min);
                if (property.longValue >= bound)
                {
                    return false;
                }

                property.longValue = bound;
                return true;
            }

            if (property.doubleValue >= min)
            {
                return false;
            }

            property.doubleValue = min;
            return true;
        }

        /// <summary>
        /// 把值压到不大于 <paramref name="max"/>。
        /// </summary>
        /// <param name="property">数值属性。</param>
        /// <param name="max">上限。</param>
        /// <returns>改动了值返回 <c>true</c>。</returns>
        /// <remarks>整数类型对上限**向下取整**：<c>[MaxValue(2.5)]</c> 的最大合法整数是 2。</remarks>
        public static bool TryClampMax(SerializedProperty property, double max)
        {
            if (double.IsNaN(max))
            {
                return false;
            }

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                if (max < long.MinValue)
                {
                    return false;
                }

                var bound = (long)Math.Floor(max);
                if (property.longValue <= bound)
                {
                    return false;
                }

                property.longValue = bound;
                return true;
            }

            if (property.doubleValue <= max)
            {
                return false;
            }

            property.doubleValue = max;
            return true;
        }

        #endregion
    }
}
