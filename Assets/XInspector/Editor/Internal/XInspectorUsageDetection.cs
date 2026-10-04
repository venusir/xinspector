using System;
using System.Reflection;

namespace XInspector.Editor
{
    /// <summary>
    /// 判断一个类型是否用到了 XInspector——即「值不值得接管它的 Inspector」。
    /// <para>
    /// 自动编辑器会对**所有**没有更具体编辑器的类型生效，若不加判断，
    /// 使用方项目里每一个 MonoBehaviour 的外观都会被我们经手一遍。
    /// 有了这个判断，没用到本插件的类型走 <c>DrawDefaultInspector</c>，
    /// 外观与接管前完全一致——这才让「开一个宏就自动接管」变得可以接受。
    /// </para>
    /// <para>
    /// <b>本类住在核心 Editor 程序集，而不是宏门控的自动接管程序集。</b>
    /// 原因是可测性：测试程序集不能引用宏门控程序集（宏关掉时那份程序集根本不存在，
    /// 测试会编译不过），判据留在那里就永远测不到。判据本身是「值不值得接管」这层策略，
    /// 自动接管只是它的一个调用方。
    /// </para>
    /// </summary>
    internal static class XInspectorUsageDetection
    {
        #region Public API

        /// <summary>
        /// 判断类型自身、其可序列化字段或其方法上，是否有**能被处理**的特性。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <returns>用到了 XInspector 返回 <c>true</c>。</returns>
        /// <remarks>
        /// <b>方法必须一起扫。</b> <c>[Button]</c> 一族只标在方法上，漏掉的后果不是「少画了点东西」，
        /// 而是**类型不被接管、按钮完全不出现、且没有任何告警**——判据漏一半是最难归因的形态。
        /// </remarks>
        public static bool IsUsedBy(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (HasSupportedAttribute(type.GetCustomAttributes(true)))
            {
                return true;
            }

            // DeclaredOnly 逐层上溯：私有字段各自声明在自己的类里，
            // GetFields 不带 DeclaredOnly 是拿不到基类私有字段的。
            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            const BindingFlags MethodFlags = Flags | BindingFlags.Static;

            for (var current = type; current != null; current = current.BaseType)
            {
                var fields = current.GetFields(Flags);
                for (var i = 0; i < fields.Length; i++)
                {
                    if (HasSupportedAttribute(fields[i].GetCustomAttributes(true)))
                    {
                        return true;
                    }
                }

                var methods = current.GetMethods(MethodFlags);
                for (var i = 0; i < methods.Length; i++)
                {
                    if (HasSupportedAttribute(methods[i].GetCustomAttributes(true)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region Private Helpers

        /// <summary>判断一组特性里是否有任何一个存在对应的绘制器**或处理器**。</summary>
        /// <param name="attributes">特性实例数组。</param>
        /// <returns>有能被处理的特性返回 <c>true</c>。</returns>
        /// <remarks>
        /// <b>两张表都要查。</b>绘制器决定「画得出来吗」，处理器决定「有没有东西会响应它」——
        /// 条件族（<c>[ShowIf]</c> 等）只有后者。漏掉处理器这一半的后果不是「少画了点东西」，
        /// 而是**特性完全失效且没有任何提示**：类型不被接管，走的还是原生 Inspector。
        /// </remarks>
        private static bool HasSupportedAttribute(object[] attributes)
        {
            for (var i = 0; i < attributes.Length; i++)
            {
                if (!(attributes[i] is Attribute attribute))
                {
                    continue;
                }

                var attributeType = attribute.GetType();
                if (DrawerTypeRegistry.HasDrawerForAttribute(attributeType) ||
                    AttributeProcessorRegistry.HasProcessorForAttribute(attributeType))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
