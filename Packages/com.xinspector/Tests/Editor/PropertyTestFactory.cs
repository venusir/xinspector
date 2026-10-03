using System;
using System.Collections.Generic;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 直接构造 <see cref="InspectorProperty"/> 的测试辅助。
    /// <para>
    /// 本提交阶段还没有属性树构建器，而链条机制**本来就不依赖树**——
    /// 它是「特性序列 → 有序格子序列」的纯函数。直接构造节点来测，
    /// 恰好验证了这层解耦是真的。
    /// </para>
    /// </summary>
    internal static class PropertyTestFactory
    {
        /// <summary>
        /// 构造一个成员节点。
        /// </summary>
        /// <param name="name">节点名。</param>
        /// <param name="attributes">节点携带的特性。</param>
        /// <returns>构造好的节点。</returns>
        public static InspectorProperty CreateMember(string name, params Attribute[] attributes)
        {
            var list = new List<Attribute>(attributes);
            return new InspectorProperty(
                name,
                name,
                typeof(int),
                InspectorPropertyKind.Member,
                new PropertyAttributes(list));
        }

        /// <summary>
        /// 构造一个成员节点，并让它的状态里带上一份**与特性共用同一列表**的记录日志。
        /// </summary>
        /// <param name="name">节点名。</param>
        /// <param name="log">共用的日志列表。</param>
        /// <param name="attributes">节点携带的特性。</param>
        /// <returns>构造好的节点。</returns>
        /// <remarks>
        /// 共用同一个列表是刻意的：特性驱动的绘制器只能把日志写进特性实例，
        /// 而末端绘制器拿不到特性，只能写进 <see cref="PropertyState"/>。
        /// 让两者指向同一个列表，才能断言**一条完整交错**的调用序列——
        /// 而那正是「包裹」是否正确的唯一证据。
        /// </remarks>
        public static InspectorProperty CreateRecordingMember(string name, List<string> log, params Attribute[] attributes)
        {
            var property = CreateMember(name, attributes);
            property.State.GetOrCreate<RecordingLog>().Entries = log;
            return property;
        }

        /// <summary>
        /// 装配链条并挂到节点上。
        /// </summary>
        /// <param name="property">目标节点。</param>
        /// <returns>装配好的链条。</returns>
        public static DrawerChain AttachChain(InspectorProperty property)
        {
            var chain = DrawerChainBuilder.Build(property, RecordingTerminalDrawer.Instance);
            property.Chain = chain;
            return chain;
        }
    }
}
