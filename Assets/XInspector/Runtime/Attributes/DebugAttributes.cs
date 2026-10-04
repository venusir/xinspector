using System;

namespace XInspector
{
    /// <summary>
    /// 把该属性的**绘制器链**画成一张可展开的表：第几格、哪个绘制器、权重、由哪个特性触发。
    /// <para>
    /// 本包的核心就是绘制器链，「谁包住谁」全靠权重排序。这张表把那条链直接摊开——
    /// 排查「某个特性怎么没生效」「谁画在外面」时，它比读代码快得多。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [ShowDrawerChain]
    /// [Indent]
    /// [DisplayAsString]
    /// public int debugging;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ShowDrawerChainAttribute : Attribute
    {
    }
}
