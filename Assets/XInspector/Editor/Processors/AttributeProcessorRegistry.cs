using System;
using System.Collections.Generic;

namespace XInspector.Editor
{
    /// <summary>
    /// 特性处理器的发现与注册：扫描所有已加载的编辑器程序集，各实例化一份共享单例。
    /// <para>
    /// 与 <see cref="DrawerTypeRegistry"/> 同构——**扫全部程序集而非只扫本包**，
    /// 使用方在自己项目的编辑器程序集里写一个 <c>AttributeProcessor&lt;MyAttribute&gt;</c>
    /// 无需任何注册调用就会被发现。
    /// </para>
    /// <para>
    /// 本类型是 <c>internal</c> 的，与公开的 <see cref="DrawerTypeRegistry"/> 不同：
    /// 后者对外暴露 <c>Drawers</c> 是给使用方诊断「我的绘制器到底被扫到了没」用的，
    /// 而处理器目前没有这样的诉求。若日后需要，把可见性改成 <c>public</c> 是纯新增。
    /// </para>
    /// </summary>
    internal static class AttributeProcessorRegistry
    {
        #region Private Fields

        private static AttributeProcessor[] _processors;
        private static AttributeProcessor[] _firstPass;
        private static AttributeProcessor[] _groupPass;

        #endregion

        #region Public API

        /// <summary>
        /// 已发现的所有处理器，顺序稳定（优先级升序，同优先级按类型名）。
        /// </summary>
        public static AttributeProcessor[] Processors
        {
            get
            {
                EnsureInitialized();
                return _processors;
            }
        }

        /// <summary>
        /// **第一趟**（分组装配之前）要跑的处理器——处理分组特性的那些不在其中。
        /// </summary>
        /// <remarks>
        /// 两趟共用同一份优先级排序：两趟作用的节点集合不相交，不存在跨趟的顺序依赖，
        /// 只有「第一趟全部跑完，第二趟才开始」这一条。
        /// </remarks>
        public static AttributeProcessor[] FirstPassProcessors
        {
            get
            {
                EnsureInitialized();
                return _firstPass;
            }
        }

        /// <summary>
        /// **第二趟**（分组装配之后、只对分组节点）要跑的处理器。
        /// </summary>
        /// <remarks>
        /// 判据见 <see cref="RunsAfterGrouping"/>：它是一条**推导**出来的规则，不是开关——
        /// 分组特性只存在于分组节点上，而分组节点到第一趟时还不存在，于是「处理分组特性的
        /// 处理器只能在装配之后跑」在构造上成立。
        /// </remarks>
        public static AttributeProcessor[] GroupProcessors
        {
            get
            {
                EnsureInitialized();
                return _groupPass;
            }
        }

        /// <summary>
        /// 清空缓存，强制下次使用时重新扫描。
        /// </summary>
        /// <remarks>
        /// 供测试复位用。本仓约定：fixture 必须复位它触碰的静态门面——
        /// PlayMode 下所有用例共享一个 player 实例，不复位即互相污染。
        /// </remarks>
        public static void Reset()
        {
            _processors = null;
            _firstPass = null;
            _groupPass = null;
        }

        /// <summary>
        /// 判断是否存在能处理指定特性类型的处理器。
        /// </summary>
        /// <param name="attributeType">特性类型。</param>
        /// <returns>存在处理它的处理器返回 <c>true</c>。</returns>
        /// <remarks>
        /// 与 <see cref="DrawerTypeRegistry.HasDrawerForAttribute"/> 对称，用途也是同一个：
        /// 自动接管要回答「这个类型用到了本插件吗」。**两张表必须都查**——
        /// 条件族只有处理器、没有绘制器，只查绘制器会让「只用了条件族的类型」不被接管，
        /// 特性于是静默失效。
        /// </remarks>
        internal static bool HasProcessorForAttribute(Type attributeType)
        {
            if (attributeType == null)
            {
                return false;
            }

            var processors = Processors;
            for (var i = 0; i < processors.Length; i++)
            {
                var handled = processors[i].HandledAttributeType;
                if (handled != null && handled.IsAssignableFrom(attributeType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 该处理器是否只处理**分组特性**——即它配对的特性类型派生自
        /// <see cref="PropertyGroupAttribute"/>，因而走构建期的**第二趟**
        /// （分组装配之后、只对分组节点）。
        /// </summary>
        /// <param name="processor">处理器。</param>
        /// <returns>只处理分组特性返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// 判据是**推导**出来的，不是开关：分组特性只可能出现在分组节点上（成员与根携带它
        /// 只为归属），而分组节点要到分组装配之后才存在——两条合起来使「处理分组特性的
        /// 处理器只能在装配之后跑」在构造上成立。做成开关的话，漏开关的症状是
        /// 「静默地什么都不做」，正是本仓最想避免的一类。
        /// </para>
        /// <para>
        /// 非泛型处理器（<see cref="AttributeProcessor.HandledAttributeType"/> 为 <c>null</c>）
        /// 恒返回 <c>false</c>，永远留在第一趟——类级分组分发正需要如此：它注入的分组特性
        /// 必须被随后的分组装配看到。
        /// </para>
        /// </remarks>
        internal static bool RunsAfterGrouping(AttributeProcessor processor)
        {
            var handled = processor?.HandledAttributeType;
            return handled != null && typeof(PropertyGroupAttribute).IsAssignableFrom(handled);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 首次使用时执行扫描并缓存。
        /// </summary>
        private static void EnsureInitialized()
        {
            if (_processors != null)
            {
                return;
            }

            var found = new List<AttributeProcessor>();

            foreach (var type in EditorTypeScanner.CollectInstantiable<AttributeProcessor>("特性处理器"))
            {
                found.Add((AttributeProcessor)Activator.CreateInstance(type));
            }

            found.Sort(CompareByPriorityThenName);
            _processors = found.ToArray();

            // 按「跑第几趟」拆一份。两趟各自保持上面的优先级顺序。
            var firstPass = new List<AttributeProcessor>(found.Count);
            var groupPass = new List<AttributeProcessor>();

            for (var i = 0; i < found.Count; i++)
            {
                (RunsAfterGrouping(found[i]) ? groupPass : firstPass).Add(found[i]);
            }

            _firstPass = firstPass.ToArray();
            _groupPass = groupPass.ToArray();
        }

        /// <summary>
        /// 优先级升序；同优先级按类型全名排序。
        /// </summary>
        /// <param name="a">左操作数。</param>
        /// <param name="b">右操作数。</param>
        /// <returns>比较结果。</returns>
        /// <remarks>
        /// 同优先级用类型名兜底是为了让顺序**确定**：扫描结果本身不保证跨平台或跨版本稳定，
        /// 而处理器的顺序是契约——一个处理器注入的特性可能被后一个读到。
        /// </remarks>
        private static int CompareByPriorityThenName(AttributeProcessor a, AttributeProcessor b)
        {
            var byPriority = a.ProcessorPriority.CompareTo(b.ProcessorPriority);
            if (byPriority != 0)
            {
                return byPriority;
            }

            return string.CompareOrdinal(a.GetType().FullName, b.GetType().FullName);
        }

        #endregion
    }
}
