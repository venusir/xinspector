using System;
using System.Reflection;
using XInspector.Internal;

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
        /// 判断类型自身、其字段、属性或方法上，是否有**能被处理**的特性。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <returns>用到了 XInspector 返回 <c>true</c>。</returns>
        /// <remarks>
        /// <b>方法必须一起扫。</b> <c>[Button]</c> 一族只标在方法上，漏掉的后果不是「少画了点东西」，
        /// 而是**类型不被接管、按钮完全不出现、且没有任何告警**——判据漏一半是最难归因的形态。
        /// <para>
        /// <b>属性也必须一起扫。</b> <c>[ShowInInspector]</c> 的主战场就是普通属性，而属性
        /// 恰恰是 <c>GetFields</c> 看不见的那一类。这是同一种漏法的第二次——判据要覆盖
        /// 「成员收集真会去看的每一处」，而不是「上次漏过的那一处」。
        /// </para>
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

            const BindingFlags StaticFlags = Flags | BindingFlags.Static;

            for (var current = type; current != null; current = current.BaseType)
            {
                var fields = current.GetFields(StaticFlags);
                for (var i = 0; i < fields.Length; i++)
                {
                    if (HasSupportedAttribute(fields[i].GetCustomAttributes(true)))
                    {
                        return true;
                    }
                }

                var properties = current.GetProperties(StaticFlags);
                for (var i = 0; i < properties.Length; i++)
                {
                    if (HasSupportedAttribute(properties[i].GetCustomAttributes(true)))
                    {
                        return true;
                    }
                }

                var methods = current.GetMethods(StaticFlags);
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

                // 生命周期钩子（[OnInspectorInit] 一族）既没有绘制器也没有处理器
                // ——它们是属性树自己在特定时机调的。漏掉这一类的后果与条件族当年一样：
                // 类型不被接管，于是特性一次都不生效，且没有任何告警。
                if (attribute is ITreeLifecycleAttribute)
                {
                    return true;
                }

                // 第三类：[ShowInInspector] 那样「会产生节点，但既不画也不改别人」的。
                // 它的作用发生在成员收集那一步，注册表里查不到它——见 ITreeMembershipAttribute。
                if (attribute is ITreeMembershipAttribute)
                {
                    return true;
                }

                // 第四类：[PropertyOrder] 那样「不产生节点，但改变它们的排列」的。
                // 它由构建期在成员收集之后直接消费（排的是收集完的那份列表），
                // 两张注册表同样查不到——见 ITreeOrderingAttribute。
                if (attribute is ITreeOrderingAttribute)
                {
                    return true;
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
