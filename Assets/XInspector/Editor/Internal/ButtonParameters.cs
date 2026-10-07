using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// 按钮方法的**参数支持集**：哪些类型画得出输入框、各类型的初值是什么。
    /// <para>
    /// 判定与取值分开放在这里，是为了让「哪些类型支持」这条决策可无头测试——
    /// 画输入框那半（<c>EditorGUILayout</c>）测不了，但「不支持的类型必须给出原因」
    /// 这条必须有人守。
    /// </para>
    /// </summary>
    internal static class ButtonParameters
    {
        #region Public API

        /// <summary>
        /// 该类型能不能画成输入框。
        /// </summary>
        /// <param name="type">参数类型。</param>
        /// <returns>支持返回 <c>true</c>。</returns>
        /// <remarks>
        /// <c>ref</c>／<c>out</c> 参数在这里就落选：它们的类型带 <c>&amp;</c>（如 <c>Int32&amp;</c>），
        /// 既不在支持集里，也不该悄悄当成普通参数处理。
        /// </remarks>
        public static bool IsSupported(Type type)
        {
            if (type == null || type.IsByRef || type.IsPointer)
            {
                return false;
            }

            if (type == typeof(bool) || type == typeof(int) || type == typeof(float) ||
                type == typeof(double) || type == typeof(string) || type == typeof(Vector2) ||
                type == typeof(Vector3) || type == typeof(Vector4) || type == typeof(Color) ||
                type == typeof(Rect))
            {
                return true;
            }

            if (type.IsEnum)
            {
                return true;
            }

            return typeof(Object).IsAssignableFrom(type);
        }

        /// <summary>
        /// 参数的初值。
        /// </summary>
        /// <param name="type">参数类型。</param>
        /// <returns>该类型的默认值。</returns>
        /// <remarks>
        /// <para>
        /// 取的是 **C# 的 <c>default</c>**，不是「看起来更顺眼的值」：值与方法来签名里的默认参数
        /// 语义一致才解释得通。举例，<c>Color</c> 得到的是全零（透明黑），而不是 <c>Color.black</c>
        /// ——后者 alpha 为 1，与 <c>default</c> 不是一回事。
        /// </para>
        /// <para>
        /// 唯一的例外是字符串：取空串而不是 <c>null</c>。文本框拿到 <c>null</c> 会显示成空，
        /// 而用户点进去又清空时会得到空串，两种「空」在状态里换来换去没有意义。
        /// </para>
        /// <para>
        /// <b>多态槽位的构造参数也用它</b>（<c>PolymorphicInstanceFactory</c> 挑「最直接的构造」
        /// 时逐个参数填的就是这里）——两处问的是同一个问题（「没给定时填什么」），
        /// 答案该由同一份代码回答。
        /// </para>
        /// </remarks>
        public static object DefaultFor(Type type)
        {
            if (type == typeof(string))
            {
                return string.Empty;
            }

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        #endregion
    }
}
