using System;
using System.Collections.Generic;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 记录调用顺序而不是绘制像素的特性。日志挂在特性实例上。
    /// <para>
    /// <b>为什么日志不放在绘制器里：</b>绘制器是全工程共享的无状态单例，往它字段里写东西
    /// 会让所有属性共用一份日志。把它放在特性上，也就顺带演示了「每属性状态该放哪」这条纪律——
    /// 生产代码里对应的位置是 <see cref="PropertyState"/>。
    /// </para>
    /// </summary>
    public sealed class RecordingAttribute : Attribute
    {
        /// <summary>本实例的标识，用于在日志里区分同类型的多个特性。</summary>
        public readonly string Name;

        /// <summary>调用日志。</summary>
        public readonly List<string> Log;

        /// <summary>构造。</summary>
        /// <param name="name">标识。</param>
        /// <param name="log">日志列表。</param>
        public RecordingAttribute(string name, List<string> log)
        {
            Name = name;
            Log = log;
        }
    }

    /// <summary>
    /// 用于验证重入的落点：重入一次之后不再重入，否则会无限递归。
    /// </summary>
    public sealed class ReentrantAttribute : Attribute
    {
        /// <summary>调用日志。</summary>
        public readonly List<string> Log;

        /// <summary>是否已经重入过。</summary>
        public bool HasReentered;

        /// <summary>构造。</summary>
        /// <param name="log">日志列表。</param>
        public ReentrantAttribute(List<string> log)
        {
            Log = log;
        }
    }

    /// <summary>
    /// 记录进入/退出、并在中间调用下一个绘制器的绘制器基类。
    /// <para>
    /// 标识是 <c>readonly</c> 字段——不可变，因此不违反「绘制器不得持有可变状态」。
    /// </para>
    /// </summary>
    internal abstract class RecordingDrawerBase : AttributeDrawer<RecordingAttribute>
    {
        private readonly string _id;

        /// <summary>构造。</summary>
        /// <param name="id">日志里的标识。</param>
        protected RecordingDrawerBase(string id)
        {
            _id = id;
        }

        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, RecordingAttribute attribute, GUIContent label)
        {
            attribute.Log.Add($"enter:{_id}:{attribute.Name}");
            CallNextDrawer(property, label);
            attribute.Log.Add($"exit:{_id}:{attribute.Name}");
        }
    }

    /// <summary>权重最小的记录绘制器，应排在最外层。</summary>
    [DrawerPriority(-500d)]
    internal sealed class OuterRecordingDrawer : RecordingDrawerBase
    {
        /// <summary>构造。</summary>
        public OuterRecordingDrawer()
            : base("outer")
        {
        }
    }

    /// <summary>普通权重的记录绘制器，应排在中间。</summary>
    internal sealed class InnerRecordingDrawer : RecordingDrawerBase
    {
        /// <summary>构造。</summary>
        public InnerRecordingDrawer()
            : base("inner")
        {
        }
    }

    /// <summary>
    /// 重入绘制器：第一次进入时再整链画一遍同一属性。
    /// </summary>
    internal sealed class ReentrantDrawer : AttributeDrawer<ReentrantAttribute>
    {
        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, ReentrantAttribute attribute, GUIContent label)
        {
            attribute.Log.Add("enter");

            if (!attribute.HasReentered)
            {
                attribute.HasReentered = true;
                property.Chain.Draw(property, label);
            }

            CallNextDrawer(property, label);
            attribute.Log.Add("exit");
        }
    }

    /// <summary>
    /// 只记录、不转发的末端绘制器。
    /// <para>
    /// <see cref="CanDraw"/> 恒为 <c>false</c>——末端由构建期显式追加，从不经
    /// <c>CanDraw</c> 进入链条。若这里返回 <c>true</c>，它会被自动加进**每一条**链，
    /// 测试里的调用序列就全被污染了。
    /// </para>
    /// </summary>
    internal sealed class RecordingTerminalDrawer : XInspectorDrawer
    {
        /// <summary>供构建期显式引用。</summary>
        public static readonly RecordingTerminalDrawer Instance = new RecordingTerminalDrawer();

        /// <inheritdoc/>
        public override bool CanDraw(InspectorProperty property)
        {
            return false;
        }

        /// <inheritdoc/>
        public override void DrawPropertyLayout(InspectorProperty property, Attribute attribute, GUIContent label)
        {
            var log = property.State.GetOrCreate<RecordingLog>();
            log.Entries.Add("terminal");
        }
    }

    /// <summary>
    /// 挂在属性状态上的日志容器，供末端绘制器写入。
    /// </summary>
    /// <remarks>
    /// 末端绘制器拿不到特性实例，故日志走 <see cref="PropertyState"/>——
    /// 这正是绘制器保持无状态时的标准做法。
    /// </remarks>
    internal sealed class RecordingLog
    {
        /// <summary>
        /// 日志条目。可整体替换，便于测试把末端绘制器的日志与特性侧的日志
        /// 指向同一个列表，从而断言**同一条**完整的调用序列。
        /// </summary>
        public List<string> Entries = new List<string>();
    }

    /// <summary>
    /// 抽象绘制器：不应被注册表实例化。
    /// </summary>
    internal abstract class AbstractTestDrawer : AttributeDrawer<RecordingAttribute>
    {
    }

    /// <summary>
    /// 没有公开无参构造函数的绘制器：不应被注册表实例化。
    /// </summary>
    internal sealed class NoDefaultConstructorDrawer : XInspectorDrawer
    {
        /// <summary>构造，故意只提供带参版本。</summary>
        /// <param name="unused">占位参数。</param>
        public NoDefaultConstructorDrawer(int unused)
        {
            _ = unused;
        }

        /// <inheritdoc/>
        public override bool CanDraw(InspectorProperty property)
        {
            return false;
        }

        /// <inheritdoc/>
        public override void DrawPropertyLayout(InspectorProperty property, Attribute attribute, GUIContent label)
        {
        }
    }

    /// <summary>
    /// 用虚属性而非 <see cref="DrawerPriorityAttribute"/> 声明权重的绘制器，
    /// 用于验证「特性优先于虚属性」。
    /// </summary>
    [DrawerPriority(42d)]
    internal sealed class AttributeOverridesVirtualDrawer : XInspectorDrawer
    {
        /// <inheritdoc/>
        public override DrawerPriority Priority => DrawerPriority.SuperPriority;

        /// <inheritdoc/>
        public override bool CanDraw(InspectorProperty property)
        {
            return false;
        }

        /// <inheritdoc/>
        public override void DrawPropertyLayout(InspectorProperty property, Attribute attribute, GUIContent label)
        {
        }
    }

    /// <summary>
    /// 触发「重复调用下一个绘制器」的特性。
    /// </summary>
    public sealed class DoubleNextAttribute : Attribute
    {
        /// <summary>调用日志。</summary>
        public readonly List<string> Log;

        /// <summary>构造。</summary>
        /// <param name="log">日志列表。</param>
        public DoubleNextAttribute(List<string> log)
        {
            Log = log;
        }
    }

    /// <summary>
    /// 连续两次调用下一个绘制器：第二次应触发链尾守卫。
    /// </summary>
    internal sealed class DoubleNextDrawer : AttributeDrawer<DoubleNextAttribute>
    {
        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, DoubleNextAttribute attribute, GUIContent label)
        {
            CallNextDrawer(property, label);
            attribute.Log.Add("first-next-returned");
            CallNextDrawer(property, label);
            attribute.Log.Add("second-next-returned");
        }
    }

    /// <summary>
    /// 只用虚属性声明权重的绘制器。
    /// </summary>
    internal sealed class VirtualPriorityDrawer : XInspectorDrawer
    {
        /// <inheritdoc/>
        public override DrawerPriority Priority => new DrawerPriority(-777d);

        /// <inheritdoc/>
        public override bool CanDraw(InspectorProperty property)
        {
            return false;
        }

        /// <inheritdoc/>
        public override void DrawPropertyLayout(InspectorProperty property, Attribute attribute, GUIContent label)
        {
        }
    }
}
