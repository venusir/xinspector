using System;

namespace XInspector
{
    // 这三个是**内嵌环境条件**：判据是「当前是不是正在内嵌编辑器里画」，与字段值无关，
    // 因此没有参数。
    //
    // 与「模式条件」（HideInEditorMode 那四个）同款：它们刻意只是空标记，
    // 判据由编辑器侧的处理器读绘制期的深度上下文得出——Runtime 程序集**零 Unity 依赖**
    // （这条契约由 Tests.Native 编译期强制），属性本身不可能去碰那个上下文。

    /// <summary>
    /// 只在**内嵌编辑器里**显示本成员。
    /// </summary>
    /// <remarks>
    /// 用途是「只在被内嵌时才有意义的成员」：例如一段说明、一个只对被嵌对象成立的开关。
    /// 在外层自己的 Inspector 里它不出现。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ShowInInlineEditorsAttribute : Attribute
    {
    }

    /// <summary>
    /// 在**内嵌编辑器里**隐藏本成员。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="ShowInInlineEditorsAttribute"/> 相反：外层照常显示，被内嵌时藏起来。
    /// 适合「在外层有用、塞进内嵌区只是噪音」的字段。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class HideInInlineEditorsAttribute : Attribute
    {
    }

    /// <summary>
    /// 在**内嵌编辑器里**禁用本成员（变灰但仍可见）。
    /// </summary>
    /// <remarks>
    /// 与隐藏的差别：禁用保留了「这个字段存在、只是这里不能改」的信息。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class DisableInInlineEditorsAttribute : Attribute
    {
    }
}
