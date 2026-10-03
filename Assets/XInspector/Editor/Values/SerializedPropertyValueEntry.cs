using System;
using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// <see cref="SerializedObject"/> 支撑的值入口。v0 唯一的后端实现。
    /// </summary>
    internal sealed class SerializedPropertyValueEntry : PropertyValueEntry
    {
        #region Private Fields

        private readonly SerializedProperty _property;
        private readonly Type _valueType;

        #endregion

        #region Construction

        /// <summary>
        /// 包装一个序列化属性。
        /// </summary>
        /// <param name="property">底层序列化属性。</param>
        /// <param name="valueType">值的类型。由构建期解析后传入——从
        /// <see cref="SerializedProperty"/> 反推类型需要一张不完整的映射表，
        /// 而构建期本来就有 <see cref="System.Reflection.FieldInfo"/>，直接取更可靠。</param>
        public SerializedPropertyValueEntry(SerializedProperty property, Type valueType)
        {
            _property = property ?? throw new ArgumentNullException(nameof(property));
            _valueType = valueType;
        }

        #endregion

        #region PropertyValueEntry

        /// <inheritdoc/>
        public override Type ValueType => _valueType;

        /// <inheritdoc/>
        public override bool IsUnityBacked => true;

        /// <inheritdoc/>
        public override SerializedProperty SerializedProperty => _property;

        /// <inheritdoc/>
        public override bool HasMultipleDifferentValues => _property.hasMultipleDifferentValues;

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">
        /// 多对象编辑且各目标值不一致时读取。此时「当前值」没有定义——
        /// Unity 自身在这种情形下显示为「—」，而不是挑一个目标的值冒充。
        /// </exception>
        public override object GetValue()
        {
            if (_property.hasMultipleDifferentValues)
            {
                throw new NotSupportedException(
                    $"属性 \"{_property.propertyPath}\" 在多对象编辑下取值不一致，没有单一当前值可读。" +
                    "需要这种场景请改用 SerializedProperty 逐目标判断。");
            }

            switch (_property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return _property.longValue;
                case SerializedPropertyType.Boolean:
                    return _property.boolValue;
                case SerializedPropertyType.Float:
                    return _property.doubleValue;
                case SerializedPropertyType.String:
                    return _property.stringValue;
                case SerializedPropertyType.Color:
                    return _property.colorValue;
                case SerializedPropertyType.ObjectReference:
                    return _property.objectReferenceValue;
                case SerializedPropertyType.Enum:
                    return _property.enumValueIndex;
                case SerializedPropertyType.Vector2:
                    return _property.vector2Value;
                case SerializedPropertyType.Vector3:
                    return _property.vector3Value;
                case SerializedPropertyType.Vector4:
                    return _property.vector4Value;
                case SerializedPropertyType.Rect:
                    return _property.rectValue;
                case SerializedPropertyType.Bounds:
                    return _property.boundsValue;
                case SerializedPropertyType.Quaternion:
                    return _property.quaternionValue;
                default:
                    throw new NotSupportedException(
                        $"尚未支持读取 {_property.propertyType} 类型的值（属性 \"{_property.propertyPath}\"）。" +
                        "请直接用 SerializedProperty 读取，或为该类型补上分支。");
            }
        }

        /// <inheritdoc/>
        /// <exception cref="NotSupportedException">类型尚未支持。</exception>
        public override void SetValue(object value)
        {
            switch (_property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    _property.longValue = Convert.ToInt64(value);
                    break;
                case SerializedPropertyType.Boolean:
                    _property.boolValue = Convert.ToBoolean(value);
                    break;
                case SerializedPropertyType.Float:
                    _property.doubleValue = Convert.ToDouble(value);
                    break;
                case SerializedPropertyType.String:
                    _property.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Color:
                    _property.colorValue = (UnityEngine.Color)value;
                    break;
                case SerializedPropertyType.ObjectReference:
                    _property.objectReferenceValue = (UnityEngine.Object)value;
                    break;
                case SerializedPropertyType.Enum:
                    _property.enumValueIndex = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Vector2:
                    _property.vector2Value = (UnityEngine.Vector2)value;
                    break;
                case SerializedPropertyType.Vector3:
                    _property.vector3Value = (UnityEngine.Vector3)value;
                    break;
                case SerializedPropertyType.Vector4:
                    _property.vector4Value = (UnityEngine.Vector4)value;
                    break;
                case SerializedPropertyType.Rect:
                    _property.rectValue = (UnityEngine.Rect)value;
                    break;
                case SerializedPropertyType.Bounds:
                    _property.boundsValue = (UnityEngine.Bounds)value;
                    break;
                case SerializedPropertyType.Quaternion:
                    _property.quaternionValue = (UnityEngine.Quaternion)value;
                    break;
                default:
                    throw new NotSupportedException(
                        $"尚未支持写入 {_property.propertyType} 类型的值（属性 \"{_property.propertyPath}\"）。");
            }
        }

        #endregion
    }
}
