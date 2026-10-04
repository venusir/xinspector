using System;

namespace XInspector
{
    /// <summary>
    /// 在字段前面挂一个开关，用它**门控该字段能否编辑**：开关关着时字段变灰。
    /// <para>
    /// 开关指向的 bool 在**值对象内部**（官方示例：<c>[Toggle("Enabled")] public MyToggleable t;</c>
    /// 指的是 <c>t.Enabled</c>），是相对路径，可写 <c>"a/b"</c> 这样的嵌套路径。
    /// 不支持 <c>static</c> 成员。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 门控走 <c>PropertyState.ReadOnlyResolver</c>：**解析在构建期一次**（处理器），
    /// 求值每帧（读一个 bool）。解析失败（名字不存在、不是 bool）时告警一次并**保持可编辑**
    /// ——一个拼错的名字不该让字段变得不可用，与条件族「失败即放行」同一条规矩。
    /// </para>
    /// <para>
    /// 与其它状态的叠加顺序：条件族（0）&lt; 本特性（50）&lt; 恒只读（100）&lt; 恒可编辑（110）。
    /// 即与 <c>[DisableIf]</c> 并存时本特性赢；与 <c>[ReadOnly]</c> 并存时后者赢
    /// （恒只读比条件门控更具体）。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Toggle("Enabled")]
    /// public MyToggleable settings;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ToggleAttribute : Attribute
    {
        /// <summary>
        /// 以开关成员名构造。
        /// </summary>
        /// <param name="toggleMemberName">值对象内部的 bool 成员路径，不得为空白。</param>
        /// <exception cref="ArgumentException"><paramref name="toggleMemberName"/> 为 null、空串或仅含空白。</exception>
        public ToggleAttribute(string toggleMemberName)
        {
            if (string.IsNullOrWhiteSpace(toggleMemberName))
            {
                throw new ArgumentException("开关成员名不能为空。", nameof(toggleMemberName));
            }

            ToggleMemberName = toggleMemberName;
        }

        /// <summary>值对象内部的 bool 成员路径。</summary>
        public string ToggleMemberName { get; }

        /// <summary>
        /// 展开一个时是否收起其它。**只留字段、不做行为**——跨成员协调没有明确语义
        /// （「其它」指哪些成员？），保留它只是为了让照着 Odin 写的调用代码编译得过。
        /// </summary>
        public bool CollapseOthersOnExpand { get; set; }
    }
}
