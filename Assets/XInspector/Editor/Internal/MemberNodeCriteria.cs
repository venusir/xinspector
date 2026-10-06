using System;
using System.Reflection;

namespace XInspector.Editor
{
    /// <summary>
    /// 「这个成员会不会变成节点」的判据——**只有一个定义处**。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 收集通道（<c>PropertyTreeBuilder</c>）与展开判据（<c>NestedMemberExpansion</c>）
    /// 都要问这句话，而两处若各写一份，迟早分家——症状是「判据看不见的展开」：
    /// 类型不被接管、或展开了却什么都不画，都**没有告警**。
    /// 本仓在这类判据上已经吃过四次同类亏（`[Button]` 扫漏方法、`[ShowInInspector]` 扫漏属性、
    /// `[PropertyOrder]` 性质漏、类级 `[InlineProperty]` 看错处）。
    /// </para>
    /// <para>
    /// 这里只回答「**会不会产生节点**」，不回答「本包支不支持它」——后者是
    /// <see cref="XInspectorUsageDetection.HasSupportedAttribute"/> 的事。
    /// 两张表的收件人不同：那个问的是「有没有绘制器或处理器」，这个问的是「会不会多出一个节点」。
    /// </para>
    /// </remarks>
    internal static class MemberNodeCriteria
    {
        #region Private Fields

        /// <summary>
        /// 会生成**方法节点**的特性类型。
        /// </summary>
        /// <remarks>
        /// 新增一个标在**方法**上的特性时往这里加一项——收集、交错、分组装配、处理器都会自动覆盖。
        /// </remarks>
        private static readonly Type[] MethodNodeAttributes =
        {
            typeof(ButtonAttribute),
            typeof(OnInspectorGUIAttribute),
        };

        #endregion

        #region Public API

        /// <summary>成员身上有没有 <c>[ShowInInspector]</c>。</summary>
        /// <param name="member">成员。</param>
        /// <returns>有返回 <c>true</c>。</returns>
        /// <remarks>
        /// 不继承（<c>inherit: false</c>）：逐层收集已经让基类声明的那一份自己出现，
        /// 这里再继承会把覆写与声明算成两条。
        /// </remarks>
        public static bool CarriesShowInInspector(MemberInfo member)
        {
            return member.GetCustomAttributes(typeof(ShowInInspectorAttribute), false).Length > 0;
        }

        /// <summary>这个方法会不会变成一个方法节点。</summary>
        /// <param name="method">方法。</param>
        /// <returns>会返回 <c>true</c>。</returns>
        public static bool CreatesMethodNode(MethodInfo method)
        {
            for (var i = 0; i < MethodNodeAttributes.Length; i++)
            {
                // 不继承：特性自己声明了 Inherited = false，这里跟着走才不会把覆写重复收一遍。
                if (method.GetCustomAttributes(MethodNodeAttributes[i], false).Length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
