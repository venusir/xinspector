using System;
using System.Collections;
using System.Collections.Generic;

namespace XInspector.Editor
{
    /// <summary>
    /// 从集合的**静态类型**推出元素类型——<c>[ValueDropdown]</c> 的反射源要拿它去描述选项标签。
    /// <para>
    /// <b>与 <c>CollectionElement.TypeOf</c> 是两件事，别合并。</b> 那个只认「数组 / <c>List&lt;T&gt;</c>」，
    /// 是 <c>Array.data[i]</c> 索引对的判据——放宽它会改动集合路径解析这条无关语义。
    /// 这个要的宽松得多：「任何实现了 <see cref="IList"/> 的类型，元素类型是什么」，
    /// 推不出来就给 <c>null</c>（消费侧退回运行时类型）。
    /// </para>
    /// <para>
    /// <b>纯函数、无 GUI 依赖</b>，可在无头测试里直接断言。
    /// </para>
    /// </summary>
    internal static class ListElementType
    {
        #region Public API

        /// <summary>
        /// 推一个列表类型的元素类型。
        /// </summary>
        /// <param name="listType">列表类型（静态声明类型或运行时类型皆可）；可以为 <c>null</c>。</param>
        /// <returns>
        /// 元素类型；非数组、且没有**唯一**的 <c>IList&lt;T&gt;</c> 实现时返回 <c>null</c>。
        /// </returns>
        /// <remarks>
        /// 三档：数组取 <c>GetElementType()</c>；类型本身就是 <c>IList&lt;T&gt;</c> 取它的类型实参；
        /// 其余扫接口里的 <c>IList&lt;T&gt;</c>——**恰好一种才认**，两种（少见但合法）意味着
        /// 没有单一的元素类型，宁可给 <c>null</c> 让消费侧退回运行时类型，也不猜一个。
        /// </remarks>
        public static Type TypeOf(Type listType)
        {
            if (listType == null)
            {
                return null;
            }

            if (listType.IsArray)
            {
                return listType.GetElementType();
            }

            if (IsGenericList(listType))
            {
                return listType.GetGenericArguments()[0];
            }

            var found = (Type)null;
            var interfaces = listType.GetInterfaces();

            for (var i = 0; i < interfaces.Length; i++)
            {
                if (!IsGenericList(interfaces[i]))
                {
                    continue;
                }

                var element = interfaces[i].GetGenericArguments()[0];

                // 两种不同的 IList<T>：没有单一的元素类型。
                if (found != null && found != element)
                {
                    return null;
                }

                found = element;
            }

            return found;
        }

        #endregion

        #region Private Helpers

        /// <summary>是不是那个泛型接口（<c>IList&lt;T&gt;</c>；**未绑定实参**的开放定义不算）。</summary>
        /// <param name="type">类型。</param>
        /// <returns>是已绑定实参的 <c>IList&lt;T&gt;</c> 返回 <c>true</c>。</returns>
        /// <remarks>
        /// <c>ContainsGenericParameters</c> 那一问防的是开放泛型（<c>IList&lt;&gt;</c>）：
        /// 它的类型实参是泛型形参而不是真类型，拿它当元素类型只会往下传一个没法用的 <c>Type</c>。
        /// </remarks>
        private static bool IsGenericList(Type type)
        {
            return type.IsGenericType
                && !type.ContainsGenericParameters
                && type.GetGenericTypeDefinition() == typeof(IList<>);
        }

        #endregion
    }
}
