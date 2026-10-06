using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// 把一个反射成员的当前值转成显示用文本。
    /// <para>
    /// <b>纯函数，没有任何 GUI 依赖</b>——这是本仓测试策略的要求：绘制那一半测不了，
    /// 「一个值该显示成什么」这一半就必须能无头测。
    /// </para>
    /// <para>
    /// <b>集合只报项数，不枚举。</b> 反射成员在本包一律只读，展开集合是 L6 的事；
    /// 而枚举一个 <see cref="IEnumerable"/> 每帧都可能无界（惰性序列、甚至无限序列），
    /// 那不是显示该付的代价。同理只认 <see cref="ICollection"/>，因为只有它给得出 <c>Count</c>。
    /// </para>
    /// <para>
    /// <b>每帧分配一个字符串是显示本身的开销</b>，与既有的 <c>[DisplayAsString]</c> 同一档，
    /// 不是多余分配——值随时会变，缓存反而会「该变不变」。
    /// </para>
    /// </summary>
    internal static class ReflectedValueFormatter
    {
        #region Public API

        /// <summary>
        /// 显示文本的长度上限，超出部分截断。
        /// </summary>
        /// <remarks>
        /// 一条几万字符的字符串会把 Inspector 撑爆。截断而不是省略显示：
        /// 「这个值很长」与「这个值看起来正常」必须一眼分得开。
        /// </remarks>
        public const int MaxTextLength = 512;

        /// <summary>
        /// 把值转成显示文本。
        /// </summary>
        /// <param name="value">当前值，可为 <c>null</c>。</param>
        /// <param name="declaredType">成员的声明类型，用来区分「Unity 对象的空」与「普通的空」。</param>
        /// <returns>显示文本，不会为 <c>null</c>。</returns>
        public static string Format(object value, Type declaredType)
        {
            return Truncate(FormatCore(value, declaredType));
        }

        #endregion

        #region Private Helpers

        /// <summary>按类型挑一条格式化路径。</summary>
        /// <param name="value">当前值。</param>
        /// <param name="declaredType">成员的声明类型。</param>
        /// <returns>显示文本。</returns>
        /// <remarks>
        /// 顺序有讲究：先处理「值的运行时类型说了算」的几类（字符串、布尔、枚举、Unity 对象），
        /// 再按**声明类型**的数字分类走，最后才是集合与兜底的 <c>ToString</c>。
        /// 枚举必须排在数字之前——装箱的枚举不是 <c>int</c>，但它的 <c>TypeCode</c> 是底层整数类型。
        /// </remarks>
        private static string FormatCore(object value, Type declaredType)
        {
            if (value == null)
            {
                // Unity 的「伪 null」（已销毁对象）也走这里：声明类型是 Unity 对象时按 Unity 的惯例显示 None。
                return IsUnityObjectType(declaredType) ? "None" : "null";
            }

            switch (value)
            {
                case string text:
                    return text;
                case bool flag:
                    return flag ? "True" : "False";
                case Enum:
                    // [Flags] 组合位天然得到 "A, B"，正是想要的。
                    return value.ToString();
                case Object unity:
                    return unity == null ? "None" : unity.name;

                // 下面这些是 Unity 的值类型。自己拼而不是用它们自带的 ToString：
                // 那个 ToString 按当前文化格式化（德语环境下小数点会变成逗号），
                // 又固定两位小数——既不可测，也不如这里统一。
                case Vector2 vector2:
                    return Join(vector2.x, vector2.y);
                case Vector3 vector3:
                    return Join(vector3.x, vector3.y, vector3.z);
                case Vector4 vector4:
                    return Join(vector4.x, vector4.y, vector4.z, vector4.w);
                case Quaternion quaternion:
                    return Join(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
                case Color color:
                    return string.Concat("RGBA", Join(color.r, color.g, color.b, color.a));
                case Rect rect:
                    return string.Concat(
                        "x:", FormatNumber(rect.x), " y:", FormatNumber(rect.y),
                        " w:", FormatNumber(rect.width), " h:", FormatNumber(rect.height));
                case Bounds bounds:
                    return string.Concat(
                        "Center", Join(bounds.center.x, bounds.center.y, bounds.center.z),
                        " Extent", Join(bounds.extents.x, bounds.extents.y, bounds.extents.z));
            }

            switch (Type.GetTypeCode(declaredType))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);

                case TypeCode.Single:
                case TypeCode.Double:
                    return FormatNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture));

                case TypeCode.Decimal:
                    return ((decimal)value).ToString(CultureInfo.InvariantCulture);
            }

            if (value is ICollection collection)
            {
                return string.Concat(
                    TypeName(value.GetType()),
                    "（",
                    collection.Count.ToString(CultureInfo.InvariantCulture),
                    " 项）");
            }

            if (value is IEnumerable)
            {
                return string.Concat(TypeName(value.GetType()), "（无法计数）");
            }

            return value.ToString();
        }

        /// <summary>
        /// 格式化浮点数。
        /// </summary>
        /// <param name="value">值。</param>
        /// <returns>显示文本。</returns>
        /// <remarks>
        /// <c>"0.######"</c> 而不是默认格式：<c>float</c> 转 <c>double</c> 会拖出一串尾巴
        /// （0.1 → 0.10000000149…）。与 <c>ValueTextFormatter.Format</c> 对 <c>float</c> 的取法一致
        /// ——两处问的是同一个问题「这个值显示成什么」，答案不该有两个。
        /// </remarks>
        private static string FormatNumber(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>把两个分量拼成 <c>(1, 2)</c>。</summary>
        /// <param name="x">第一个分量。</param>
        /// <param name="y">第二个分量。</param>
        /// <returns>显示文本。</returns>
        private static string Join(float x, float y)
        {
            return string.Concat("(", FormatNumber(x), ", ", FormatNumber(y), ")");
        }

        /// <summary>把三个分量拼成 <c>(1, 2, 3)</c>。</summary>
        /// <param name="x">第一个分量。</param>
        /// <param name="y">第二个分量。</param>
        /// <param name="z">第三个分量。</param>
        /// <returns>显示文本。</returns>
        private static string Join(float x, float y, float z)
        {
            return string.Concat("(", FormatNumber(x), ", ", FormatNumber(y), ", ", FormatNumber(z), ")");
        }

        /// <summary>把四个分量拼成 <c>(1, 2, 3, 4)</c>。</summary>
        /// <param name="x">第一个分量。</param>
        /// <param name="y">第二个分量。</param>
        /// <param name="z">第三个分量。</param>
        /// <param name="w">第四个分量。</param>
        /// <returns>显示文本。</returns>
        private static string Join(float x, float y, float z, float w)
        {
            return string.Concat(
                "(", FormatNumber(x), ", ", FormatNumber(y), ", ", FormatNumber(z), ", ", FormatNumber(w), ")");
        }

        /// <summary>截断过长的文本。</summary>
        /// <param name="text">文本。</param>
        /// <returns>不超过 <see cref="MaxTextLength"/> 字符的文本。</returns>
        private static string Truncate(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= MaxTextLength)
            {
                return text;
            }

            return string.Concat(text.Substring(0, MaxTextLength), "…");
        }

        /// <summary>声明类型是不是 Unity 对象。</summary>
        /// <param name="type">类型。</param>
        /// <returns>是 <see cref="UnityEngine.Object"/> 的派生类型返回 <c>true</c>。</returns>
        private static bool IsUnityObjectType(Type type)
        {
            return type != null && typeof(Object).IsAssignableFrom(type);
        }

        /// <summary>
        /// 类型的可读名字，泛型展开成 <c>List&lt;Int32&gt;</c> 的样子。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <returns>显示用类型名。</returns>
        /// <remarks>
        /// 不直接用 <c>Type.Name</c>：泛型类型的名字带反引号元数（<c>List`1</c>），
        /// 直接显示出来像是坏了。
        /// <b>告警文案也走这里</b>（<see cref="ReflectedAccessor.DescribeType"/> 转调），
        /// 免得同一件事有两种拼法。
        /// </remarks>
        internal static string TypeName(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }

            var name = type.Name;
            var arity = name.IndexOf('`');
            if (arity >= 0)
            {
                name = name.Substring(0, arity);
            }

            var arguments = type.GetGenericArguments();
            var names = new string[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                names[i] = TypeName(arguments[i]);
            }

            return string.Concat(name, "<", string.Join(", ", names), ">");
        }

        #endregion
    }
}
