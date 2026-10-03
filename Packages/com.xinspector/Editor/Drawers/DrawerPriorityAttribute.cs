using System;

namespace XInspector.Editor
{
    /// <summary>
    /// 声明绘制器在链上的位置权重，覆盖 <see cref="XInspectorDrawer.Priority"/> 的默认值。
    /// <para>
    /// 两种写法等价，选择取决于是否需要动态计算：固定值用本特性（注册表直接读取，无需实例化
    /// 后再问一次），需要按状态计算则覆写 <see cref="XInspectorDrawer.Priority"/>。
    /// 同时存在时以本特性为准。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [DrawerPriority(-500)]   // 排到普通特性绘制器之前，从而能包住它们
    /// public sealed class MyBackgroundDrawer : XInspectorDrawer { }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DrawerPriorityAttribute : Attribute
    {
        #region Public API

        /// <summary>
        /// 以指定权重声明绘制器优先级。
        /// </summary>
        /// <param name="priority">权重，越小越靠外层。可用 <see cref="DrawerPriority"/> 的档位常量。</param>
        public DrawerPriorityAttribute(double priority)
        {
            Priority = new DrawerPriority(priority);
        }

        /// <summary>
        /// 声明的优先级。
        /// </summary>
        public DrawerPriority Priority { get; }

        #endregion
    }
}
