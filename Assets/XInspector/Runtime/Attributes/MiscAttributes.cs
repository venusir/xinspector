using System;

namespace XInspector
{
    /// <summary>
    /// 强制该成员**可编辑**，覆盖 <see cref="ReadOnlyAttribute"/> 与条件族（<c>[DisableIf]</c> 等）。
    /// <para>
    /// 处理器显式排在只读之后，因此并存时本特性赢——它叫 Enable，就该能开。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [ReadOnly]
    /// [EnableGUI]
    /// public int forcedEditable;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class EnableGUIAttribute : Attribute
    {
    }

    /// <summary>
    /// 在类级画一条信息框（出现在整个 Inspector 顶部）。
    /// <para>
    /// 与 <c>[InfoBox]</c> 的差别只在目标：后者类级、成员级都能用，本特性**只能用在类上**，
    /// 且没有级别参数——它要表达的就是「这一整型是什么」。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [TypeInfoBox("这个组件负责……")]
    /// public class PlayerProfile : MonoBehaviour { }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class TypeInfoBoxAttribute : Attribute
    {
        /// <summary>
        /// 以消息文本构造。
        /// </summary>
        /// <param name="message">信息框内容，不得为空白。</param>
        /// <exception cref="ArgumentException"><paramref name="message"/> 为 null、空串或仅含空白。</exception>
        public TypeInfoBoxAttribute(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("信息框的内容不能为空。", nameof(message));
            }

            Message = message;
        }

        /// <summary>信息框内容。</summary>
        public string Message { get; }
    }

    /// <summary>
    /// 让该成员**回到 Unity 的绘制路径**：本插件不再画它。
    /// <para>
    /// 绘制器画完 <c>PropertyField</c> 就结束、不调用下一个，因此链上更内侧的 XInspector
    /// 绘制器一律不运行——包括 <c>[Indent]</c> 这类修饰。这正是「交给 Unity」的含义。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [DrawWithUnity]
    /// public MyFancyType value;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class DrawWithUnityAttribute : Attribute
    {
        /// <summary>
        /// UI Toolkit 时代的后端偏好开关。**本包只有 IMGUI，该字段不产生行为**——
        /// 保留它只是为了让照着 Odin 写的调用代码编译得过。
        /// </summary>
        public bool PreferImGUI { get; set; }
    }

    /// <summary>
    /// 校验对象引用指向**同一个物体下的子物体**（GameObject、组件或 Transform 引用）。
    /// <para>
    /// 引用不是子物体时在字段上方画警告框。**只提示、不拦赋值**。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 与 Odin 的差异：官方还提供「从子物体里挑」的选择下拉，本包**只做校验**——
    /// 选择器要接管对象字段的拾取交互，那是另一块工作，没有它本特性依然成立。
    /// </remarks>
    /// <example>
    /// <code>
    /// [ChildGameObjectsOnly]
    /// public Transform muzzle;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ChildGameObjectsOnlyAttribute : Attribute
    {
        /// <summary>
        /// 是否允许引用**非激活**的子物体。默认 <c>false</c>（引用到未激活的子物体会告警）。
        /// </summary>
        public bool IncludeInactive { get; set; }

        /// <summary>
        /// 是否允许引用所在物体**自己**。默认 <c>false</c>。
        /// </summary>
        public bool IncludeSelf { get; set; }
    }

    /// <summary>
    /// 隐藏脚本槽位（Inspector 最上方的 <c>Script</c> 字段）。
    /// <para>
    /// 构建期把 <c>m_Script</c> 成员直接**不建节点**——与原生 Inspector 的差距是刻意的：
    /// 不写本特性时该槽位照旧保留，两个行为各有用途。
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [HideMonoScript]
    /// public class PlayerProfile : MonoBehaviour { }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class HideMonoScriptAttribute : Attribute
    {
    }
}
