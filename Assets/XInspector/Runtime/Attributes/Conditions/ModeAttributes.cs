using System;

namespace XInspector
{
    // 这四个是**模式条件**：判据是「当前在不在播放模式」，与字段值无关，因此没有参数。
    //
    // 它们刻意只是空标记：判据由编辑器侧的处理器读取 Application.isPlaying 得出，
    // 而 Runtime 程序集**零 Unity 依赖**（这条契约由 Tests.Native 编译期强制），
    // 因此属性本身不能去碰 UnityEngine。数据与判据分居两侧，是那条契约的直接后果。
    //
    // 四个都允许标在方法上：与 [Button] 配合时「播放中禁用这个按钮」是最常见的用法。
    // 判据与 ConditionalAttributes 那四个相同——方法会产生属性树节点，普通属性不会。

    /// <summary>
    /// 在**编辑模式**下隐藏本成员（即播放中可见、未播放时隐藏）。
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class HideInEditorModeAttribute : Attribute
    {
    }

    /// <summary>
    /// 在**播放模式**下隐藏本成员。
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class HideInPlayModeAttribute : Attribute
    {
    }

    /// <summary>
    /// 在**编辑模式**下禁用本成员（变灰但仍可见）。
    /// </summary>
    /// <remarks>
    /// 与隐藏的差别：禁用保留了「这个字段存在、只是现在不能改」的信息。
    /// 调试用的字段通常更适合禁用而不是藏起来。
    /// </remarks>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class DisableInEditorModeAttribute : Attribute
    {
    }

    /// <summary>
    /// 在**播放模式**下禁用本成员。
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class DisableInPlayModeAttribute : Attribute
    {
    }
}
