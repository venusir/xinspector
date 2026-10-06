using System;
using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 把一个 <see cref="SerializedProperty"/> 的当前值**装箱读出来**，或转成显示用的文本。
    /// <para>
    /// 两处消费者：<c>[OnCollectionChanged]</c> 要报「被改动的那个元素是什么」，
    /// <c>[Searchable]</c> 要拿值当搜索的素材。两处问的是同一个问题，答案不该有两个。
    /// </para>
    /// <para>
    /// <b>按类型取值，不用 <c>boxedValue</c>。</b> 那个属性在复合类型上的行为没有权威依据，
    /// 而整型给的是 <c>long</c>、枚举给的是**下标**（<c>enumValueIndex</c>）——
    /// 用户回调里一句 <c>(int)value</c> 或 <c>(MyEnum)value</c> 就会抛异常并被吞掉。
    /// 这里改走 Unity 的类型化取值器（与 <c>SerializedPropertyValueEntry</c> 同款），
    /// 再把结果**对齐到声明类型**：是 <c>int</c> 的元素就给 <c>int</c>，是枚举就给枚举值本身。
    /// </para>
    /// <para>
    /// <b>它是纯读取，不进每帧路径。</b> 调用点是「用户点了增删」与「搜索词变了」，
    /// 都不在每帧绘制上。
    /// </para>
    /// </summary>
    internal static class SerializedValueReader
    {
        #region Public API

        /// <summary>
        /// 读出一个序列化属性的值。
        /// </summary>
        /// <param name="property">序列化属性。</param>
        /// <param name="declaredType">
        /// 该值的**声明类型**（元素的声明类型、字段类型）；认不出时传 <c>null</c>，
        /// 此时整型退到 <c>long</c>、浮点退到 <c>double</c>。
        /// </param>
        /// <param name="value">读出的值；读不出来时为 <c>null</c>。</param>
        /// <param name="reason">读不出来的原因；成功时为 <c>null</c>。</param>
        /// <returns>读出来返回 <c>true</c>。</returns>
        /// <remarks>
        /// 读不出来**不是错误**：复合元素、多态引用、本包没覆盖的类型都属此列，
        /// 调用方该做的是「值报 null 并告警一次」，而不是假装什么都没发生。
        /// </remarks>
        public static bool TryRead(
            SerializedProperty property, Type declaredType, out object value, out string reason)
        {
            value = null;
            reason = null;

            if (property == null)
            {
                reason = "没有 Unity 的序列化后端（反射成员？）";
                return false;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    value = AlignInteger(property.longValue, declaredType);
                    return true;

                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.ArraySize:
                    value = property.intValue;
                    return true;

                case SerializedPropertyType.Character:
                    value = (char)property.intValue;
                    return true;

                case SerializedPropertyType.Boolean:
                    value = property.boolValue;
                    return true;

                case SerializedPropertyType.Float:
                    value = AlignFloat(property.doubleValue, declaredType);
                    return true;

                case SerializedPropertyType.String:
                    value = property.stringValue;
                    return true;

                case SerializedPropertyType.Enum:
                    // 枚举给**枚举值本身**：给下标（enumValueIndex）或底层数值都会让
                    // 用户那句 (MyEnum)value 悄悄拿到别的东西。
                    value = declaredType != null && declaredType.IsEnum
                        ? Enum.ToObject(declaredType, property.intValue)
                        : (object)property.intValue;
                    return true;

                case SerializedPropertyType.ObjectReference:
                case SerializedPropertyType.ExposedReference:
                    value = property.objectReferenceValue;
                    return true;

                case SerializedPropertyType.Color:
                    value = property.colorValue;
                    return true;

                case SerializedPropertyType.Vector2:
                    value = property.vector2Value;
                    return true;

                case SerializedPropertyType.Vector3:
                    value = property.vector3Value;
                    return true;

                case SerializedPropertyType.Vector4:
                    value = property.vector4Value;
                    return true;

                case SerializedPropertyType.Quaternion:
                    value = property.quaternionValue;
                    return true;

                case SerializedPropertyType.Rect:
                    value = property.rectValue;
                    return true;

                case SerializedPropertyType.Bounds:
                    value = property.boundsValue;
                    return true;

                default:
                    reason = $"{property.propertyType} 类型的值读不出来";
                    return false;
            }
        }

        /// <summary>
        /// 读出一个序列化属性的值并转成显示用文本。
        /// </summary>
        /// <param name="property">序列化属性。</param>
        /// <param name="declaredType">该值的声明类型；认不出时传 <c>null</c>。</param>
        /// <param name="text">显示文本；读不出来时为 <c>null</c>。</param>
        /// <param name="reason">读不出来的原因；成功时为 <c>null</c>。</param>
        /// <returns>读出来返回 <c>true</c>。</returns>
        /// <remarks>
        /// 格式化交给 <see cref="ReflectedValueFormatter.Format"/>——「一个值该显示成什么」
        /// 这个问题全包只该有一份答案，搜索的素材与只读展示的文本因此逐字一致。
        /// </remarks>
        public static bool TryFormat(
            SerializedProperty property, Type declaredType, out string text, out string reason)
        {
            text = null;

            if (!TryRead(property, declaredType, out var value, out reason))
            {
                return false;
            }

            text = ReflectedValueFormatter.Format(value, declaredType ?? value?.GetType());
            return true;
        }

        #endregion

        #region Private Helpers

        /// <summary>整型对齐到声明类型（<c>int</c> 的元素给 <c>int</c>，而不是 Unity 的 <c>long</c>）。</summary>
        /// <param name="value">读出的整数。</param>
        /// <param name="declaredType">声明类型。</param>
        /// <returns>对齐后的装箱值。</returns>
        private static object AlignInteger(long value, Type declaredType)
        {
            if (declaredType == null)
            {
                return value;
            }

            switch (Type.GetTypeCode(declaredType))
            {
                case TypeCode.SByte:
                    return (sbyte)value;
                case TypeCode.Byte:
                    return (byte)value;
                case TypeCode.Int16:
                    return (short)value;
                case TypeCode.UInt16:
                    return (ushort)value;
                case TypeCode.Int32:
                    return (int)value;
                case TypeCode.UInt32:
                    return (uint)value;
                case TypeCode.UInt64:
                    return (ulong)value;
                default:
                    return value;
            }
        }

        /// <summary>浮点对齐到声明类型（<c>float</c> 的元素给 <c>float</c>）。</summary>
        /// <param name="value">读出的数。</param>
        /// <param name="declaredType">声明类型。</param>
        /// <returns>对齐后的装箱值。</returns>
        private static object AlignFloat(double value, Type declaredType)
        {
            return declaredType == typeof(float) ? (object)(float)value : value;
        }

        #endregion
    }
}
