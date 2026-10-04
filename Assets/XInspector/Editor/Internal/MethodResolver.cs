using System;
using System.Reflection;

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
        /// </remarks>
        public static MethodInfo ByName(Type type, string name, out string reason)
        {
            reason = null;

            if (type == null)
            {
                reason = "取不到目标对象的类型。";
                return null;
            }

            var sawTheName = false;

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

                    if (candidate.GetParameters().Length == 0 && !candidate.ContainsGenericParameters)
                    {
                        return candidate;
                    }
                }
            }

            reason = sawTheName
                ? $"方法 \"{name}\" 必须**无参且非泛型**，本包只调得动这样的方法。"
                : $"目标对象上找不到名为 \"{name}\" 的方法。";

            return null;
        }

        #endregion

        #region Private Helpers

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
