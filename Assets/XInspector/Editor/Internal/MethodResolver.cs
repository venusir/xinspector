using System;
using System.Reflection;
using System.Text;

namespace XInspector.Editor
{
    /// <summary>
    /// 在**目标对象的运行时类型**上解析方法。
    /// <para>
    /// 按钮族（<c>[Button]</c>、<c>[InlineButton]</c>、回调族）都要做这件事，差别只在
    /// 「手里有什么线索」：一个是收集期记下的 <see cref="MethodInfo"/>，一个是特性里写的方法名。
    /// 逐层上溯、参数表比对这些细节只该有一份实现。
    /// </para>
    /// </summary>
    internal static class MethodResolver
    {
        #region Private Fields

        /// <summary>
        /// 逐层查找用的标志。
        /// </summary>
        /// <remarks>
        /// <b><c>DeclaredOnly</c> 是必需的</b>：不带它时 <see cref="Type.GetMethods(BindingFlags)"/>
        /// 拿不到基类的私有方法，而按钮方法多半就是私有的。静态也要带上——静态按钮允许。
        /// </remarks>
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        /// <summary>「无参」这一种形状——收窄成它的那个重载与它共用同一条查找。</summary>
        private static readonly Type[][] NoParameters = { Type.EmptyTypes };

        #endregion

        #region Public API

        /// <summary>
        /// 按**同名同参数表**解析。
        /// </summary>
        /// <param name="type">目标对象的运行时类型。</param>
        /// <param name="clue">线索方法（收集期记下的那一份）。</param>
        /// <returns>目标类型上可调用的那一份；找不到时返回 <c>null</c>。</returns>
        /// <remarks>
        /// 线索可能是基类上的抽象声明或虚方法，拿它去调用会调到错的那一份
        /// （抽象声明更是直接抛异常），故必须回到目标类型上重新解析。
        /// </remarks>
        public static MethodInfo BySignature(Type type, MethodInfo clue)
        {
            if (type == null || clue == null)
            {
                return null;
            }

            var parameters = clue.GetParameters();

            for (var current = type; current != null; current = current.BaseType)
            {
                var candidates = current.GetMethods(Flags);

                for (var i = 0; i < candidates.Length; i++)
                {
                    if (candidates[i].Name == clue.Name && SignatureMatches(candidates[i], parameters))
                    {
                        return candidates[i];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 按**名字**解析一个无参、非泛型的方法。
        /// </summary>
        /// <param name="type">目标对象的运行时类型。</param>
        /// <param name="name">方法名。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>解析出的方法；失败时返回 <c>null</c>。</returns>
        /// <remarks>
        /// 失败时区分「压根没有」与「有但不合形状」：两种情形用户要做的事不一样
        /// （改名字 vs 去掉参数），给同一句话等于让人白找。
        /// <para>
        /// 这是**形状收窄成「无参」的特例**——要收多种参数表用另一个重载。
        /// 两者的查找是同一条，只有失败文案不同（这一条的文案是既有行为，别改）。
        /// </para>
        /// </remarks>
        public static MethodInfo ByName(Type type, string name, out string reason)
        {
            reason = null;

            if (type == null)
            {
                reason = "取不到目标对象的类型。";
                return null;
            }

            var method = Find(type, name, NoParameters, out var sawTheName);

            if (method == null)
            {
                reason = sawTheName
                    ? $"方法 \"{name}\" 必须**无参且非泛型**，本包只调得动这样的方法。"
                    : $"目标对象上找不到名为 \"{name}\" 的方法。";
            }

            return method;
        }

        /// <summary>
        /// 按**名字 + 期望参数表**解析方法：<paramref name="shapes"/> 里任一形状匹配即可。
        /// </summary>
        /// <param name="type">目标对象的运行时类型。</param>
        /// <param name="name">方法名。</param>
        /// <param name="shapes">可接受的参数表，**按顺序**试；其中空数组表示「无参」。</param>
        /// <param name="reason">失败原因；成功时为 <c>null</c>。</param>
        /// <returns>解析出的方法；失败时返回 <c>null</c>。</returns>
        /// <remarks>
        /// <para>
        /// <b>形状表按顺序取第一个匹配</b>：同一个名字上有多个重载时，写在前的形状赢。
        /// 名字在、形状都不对时，失败原因会**列出可接受的形状**——用户照着改签名就行，
        /// 不必回来翻文档。
        /// </para>
        /// <para>
        /// 泛型方法一律拒：拿不到泛型实参就构造不出可调用的那一份（与上面那条同款）。
        /// </para>
        /// </remarks>
        public static MethodInfo ByName(Type type, string name, Type[][] shapes, out string reason)
        {
            reason = null;

            if (type == null)
            {
                reason = "取不到目标对象的类型。";
                return null;
            }

            var method = Find(type, name, shapes, out var sawTheName);

            if (method == null)
            {
                reason = sawTheName
                    ? $"方法 \"{name}\" 的参数表必须是以下之一：{DescribeShapes(shapes)}。"
                    : $"目标对象上找不到名为 \"{name}\" 的方法。";
            }

            return method;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 逐层上溯找**名字对得上、参数表落在形状表里、且不是泛型**的那一份。
        /// </summary>
        /// <param name="type">目标对象的运行时类型。</param>
        /// <param name="name">方法名。</param>
        /// <param name="shapes">可接受的参数表。</param>
        /// <param name="sawTheName">这趟有没有见过这个名字（用于区分两种失败）。</param>
        /// <returns>解析出的方法；找不到时返回 <c>null</c>。</returns>
        /// <remarks>
        /// <b>形状在外层、类型层次在内层。</b> 反过来的话「谁赢」由 <c>GetMethods</c> 的返回顺序决定，
        /// 而那个顺序没有任何保证——同名重载同时存在时，结果会变成不可预期。
        /// 外层的形状因此是有优先级的：写在前面的先试。
        /// </remarks>
        private static MethodInfo Find(Type type, string name, Type[][] shapes, out bool sawTheName)
        {
            sawTheName = false;

            if (type == null)
            {
                return null;
            }

            for (var s = 0; s < shapes.Length; s++)
            {
                for (var current = type; current != null; current = current.BaseType)
                {
                    var candidates = current.GetMethods(Flags);

                    for (var i = 0; i < candidates.Length; i++)
                    {
                        var candidate = candidates[i];
                        if (candidate.Name != name)
                        {
                            continue;
                        }

                        sawTheName = true;

                        if (candidate.ContainsGenericParameters)
                        {
                            continue;
                        }

                        if (ParameterTypesMatch(candidate, shapes[s]))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>候选方法的参数表是否与某个形状逐项同型。</summary>
        /// <param name="candidate">候选方法。</param>
        /// <param name="shape">期望的参数表。</param>
        /// <returns>同型返回 <c>true</c>。</returns>
        private static bool ParameterTypesMatch(MethodInfo candidate, Type[] shape)
        {
            var parameters = candidate.GetParameters();

            if (parameters.Length != shape.Length)
            {
                return false;
            }

            for (var i = 0; i < shape.Length; i++)
            {
                if (parameters[i].ParameterType != shape[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>把形状表渲染成诊断文案里的 <c>(CollectionChangeInfo, object)、()</c> 一串。</summary>
        /// <param name="shapes">形状表。</param>
        /// <returns>渲染结果。</returns>
        private static string DescribeShapes(Type[][] shapes)
        {
            var builder = new StringBuilder();

            for (var i = 0; i < shapes.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('、');
                }

                builder.Append('(');

                for (var p = 0; p < shapes[i].Length; p++)
                {
                    if (p > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(Keyword(shapes[i][p]));
                }

                builder.Append(')');
            }

            return builder.ToString();
        }

        /// <summary>
        /// 类型的 C# 关键字写法。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <returns>诊断里显示的类型名。</returns>
        /// <remarks>
        /// 用户源码里写的是 <c>object</c>，诊断里印 <c>Object</c> 会让人以为要写全名。
        /// 认不出的类型退回 <see cref="Type.Name"/>——本包用到的那几个（<c>CollectionChangeInfo</c>）
        /// 恰好就是源码里的写法。
        /// </remarks>
        private static string Keyword(Type type)
        {
            if (type == typeof(object))
            {
                return "object";
            }

            if (type == typeof(string))
            {
                return "string";
            }

            if (type == typeof(bool))
            {
                return "bool";
            }

            if (type == typeof(int))
            {
                return "int";
            }

            if (type == typeof(long))
            {
                return "long";
            }

            if (type == typeof(float))
            {
                return "float";
            }

            if (type == typeof(double))
            {
                return "double";
            }

            if (type == typeof(char))
            {
                return "char";
            }

            if (type == typeof(byte))
            {
                return "byte";
            }

            if (type == typeof(short))
            {
                return "short";
            }

            if (type == typeof(uint))
            {
                return "uint";
            }

            if (type == typeof(ulong))
            {
                return "ulong";
            }

            if (type == typeof(ushort))
            {
                return "ushort";
            }

            if (type == typeof(sbyte))
            {
                return "sbyte";
            }

            if (type == typeof(decimal))
            {
                return "decimal";
            }

            return type.Name;
        }

        /// <summary>候选方法的参数表是否与线索方法逐项同型。</summary>
        /// <param name="candidate">候选方法。</param>
        /// <param name="parameters">线索方法的参数。</param>
        /// <returns>同型返回 <c>true</c>。</returns>
        private static bool SignatureMatches(MethodInfo candidate, ParameterInfo[] parameters)
        {
            var candidateParameters = candidate.GetParameters();

            if (candidateParameters.Length != parameters.Length)
            {
                return false;
            }

            for (var i = 0; i < parameters.Length; i++)
            {
                if (candidateParameters[i].ParameterType != parameters[i].ParameterType)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
