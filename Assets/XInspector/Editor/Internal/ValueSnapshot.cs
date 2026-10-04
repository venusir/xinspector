using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// 一个序列化属性在某一瞬间的值快照，用来判断「这一趟里它变了没有」。
    /// <para>
    /// <b>按类型取值，不装箱。</b> <c>[OnValueChanged]</c> 每帧都要前后各取一次，
    /// 走 <c>boxedValue</c> 等于每帧给每个被监听的字段分配一个箱子——本包的绘制路径
    /// 连匿名函数都避，更不会接受这个。代价是只覆盖常见类型，其余类型由调用方告警说明。
    /// </para>
    /// </summary>
    internal readonly struct ValueSnapshot
    {
        #region Private Fields

        private readonly SerializedPropertyType _type;
        private readonly long _integer;
        private readonly double _number;
        private readonly string _text;
        private readonly Object _reference;
        private readonly Vector4 _vector;

        #endregion

        #region Construction

        /// <summary>取一份快照。</summary>
        /// <param name="property">序列化属性。</param>
        private ValueSnapshot(SerializedProperty property)
        {
            _type = property.propertyType;
            _integer = 0L;
            _number = 0d;
            _text = null;
            _reference = null;
            _vector = default;

            switch (_type)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                    _integer = property.longValue;
                    break;

                case SerializedPropertyType.Boolean:
                    _integer = property.boolValue ? 1L : 0L;
                    break;

                case SerializedPropertyType.Enum:
                    _integer = property.enumValueIndex;
                    break;

                case SerializedPropertyType.Float:
                    _number = property.doubleValue;
                    break;

                case SerializedPropertyType.String:
                    _text = property.stringValue;
                    break;

                case SerializedPropertyType.ObjectReference:
                case SerializedPropertyType.ExposedReference:
                    _reference = property.objectReferenceValue;
                    break;

                case SerializedPropertyType.Vector2:
                    _vector = property.vector2Value;
                    break;

                case SerializedPropertyType.Vector3:
                    _vector = property.vector3Value;
                    break;

                case SerializedPropertyType.Vector4:
                    _vector = property.vector4Value;
                    break;

                case SerializedPropertyType.Quaternion:
                    var quaternion = property.quaternionValue;
                    _vector = new Vector4(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
                    break;

                case SerializedPropertyType.Color:
                    _vector = property.colorValue;
                    break;

                case SerializedPropertyType.Rect:
                    var rectangle = property.rectValue;
                    _vector = new Vector4(rectangle.x, rectangle.y, rectangle.width, rectangle.height);
                    break;

                default:
                    // 没覆盖到的类型：快照恒等，于是「变了」永远为假。
                    // 调用方负责把这件事告警一次——见 ValueSnapshot.IsSupported。
                    break;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 这个类型支不支持前后比对。
        /// </summary>
        /// <param name="type">序列化属性类型。</param>
        /// <returns>支持返回 <c>true</c>。</returns>
        public static bool IsSupported(SerializedPropertyType type)
        {
            switch (type)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.String:
                case SerializedPropertyType.ObjectReference:
                case SerializedPropertyType.ExposedReference:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Quaternion:
                case SerializedPropertyType.Color:
                case SerializedPropertyType.Rect:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>取一份快照。</summary>
        /// <param name="property">序列化属性。</param>
        /// <returns>快照。</returns>
        public static ValueSnapshot Capture(SerializedProperty property)
        {
            return new ValueSnapshot(property);
        }

        /// <summary>
        /// 与另一份快照相比，值是否变了。
        /// </summary>
        /// <param name="other">后取的快照。</param>
        /// <returns>不同返回 <c>true</c>。</returns>
        /// <remarks>
        /// 类型都不同时直接算「变了」：序列化属性的类型在一趟里不会变，
        /// 真出现就说明节点被换过，宁可报一次也不要漏。
        /// </remarks>
        public bool DiffersFrom(in ValueSnapshot other)
        {
            if (_type != other._type)
            {
                return true;
            }

            if (!string.Equals(_text, other._text, StringComparison.Ordinal))
            {
                return true;
            }

            if (_reference != other._reference)
            {
                return true;
            }

            return _integer != other._integer || !_number.Equals(other._number) || _vector != other._vector;
        }

        #endregion
    }
}
