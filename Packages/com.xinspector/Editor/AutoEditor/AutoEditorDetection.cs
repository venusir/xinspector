using System;
using System.Reflection;

namespace XInspector.Editor.AutoEditor
{
    /// <summary>
    /// 判断一个类型是否用到了 XInspector——即「值不值得接管它的 Inspector」。
    /// <para>
    /// 自动编辑器会对**所有**没有更具体编辑器的类型生效，若不加判断，
    /// 使用方项目里每一个 MonoBehaviour 的外观都会被我们经手一遍。
    /// 有了这个判断，没用到本插件的类型走 <c>DrawDefaultInspector</c>，
    /// 外观与接管前完全一致——这才让「开一个宏就自动接管」变得可以接受。
    /// </para>
    /// </summary>
    internal static class AutoEditorDetection
    {
        #region Public API

        /// <summary>
        /// 判断类型自身或其可序列化字段上是否有可绘制的特性。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <returns>用到了 XInspector 返回 <c>true</c>。</returns>
        public static bool ShouldTakeOver(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (HasDrawableAttribute(type.GetCustomAttributes(true)))
            {
                return true;
            }

            // DeclaredOnly 逐层上溯：私有字段各自声明在自己的类里，
            // GetFields 不带 DeclaredOnly 是拿不到基类私有字段的。
            const BindingFlags Flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var current = type; current != null; current = current.BaseType)
            {
                var fields = current.GetFields(Flags);
                for (var i = 0; i < fields.Length; i++)
                {
                    if (HasDrawableAttribute(fields[i].GetCustomAttributes(true)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region Private Helpers

        /// <summary>判断一组特性里是否有任何一个存在对应的绘制器。</summary>
        /// <param name="attributes">特性实例数组。</param>
        /// <returns>有可绘制的特性返回 <c>true</c>。</returns>
        private static bool HasDrawableAttribute(object[] attributes)
        {
            for (var i = 0; i < attributes.Length; i++)
            {
                if (attributes[i] is Attribute attribute &&
                    DrawerTypeRegistry.HasDrawerForAttribute(attribute.GetType()))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
