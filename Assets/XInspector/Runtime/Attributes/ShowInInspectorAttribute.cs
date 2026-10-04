using System;
using XInspector.Internal;

namespace XInspector
{
    /// <summary>
    /// 把 Unity **不会序列化**的成员也画进 Inspector：普通属性、没有 <c>[SerializeField]</c>
    /// 的私有字段、静态成员。
    /// <para>
    /// 本包的值后端是 <c>SerializedObject</c>（UnityEditor 的类型，Runtime 侧只能当名字提，
    /// 不能写 cref），它只认 Unity 会序列化的成员。
    /// 本特性打开的是另一条通道——反射读取，因此这些成员**一律只读展示**。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么只读。</b> 这些成员按定义不在 Unity 的序列化里，因此拿不到
    /// Undo/Redo、预制体覆盖、场景标脏、多对象编辑、域重载后取值这五件事中的任何一件：
    /// 写进去既不可撤销，也会在下次域重载或重新载入存档时无声消失。
    /// Odin 的文档同样写着「<c>[ShowInInspector]</c> 不序列化任何东西，改动不会随之保存」——
    /// 本包把这句话落实成「干脆不给写」，而不是做一个看起来能改、改完就丢的控件。
    /// </para>
    /// <para>
    /// <b>已被序列化通道收进树里的成员不受影响。</b> <c>[ShowInInspector]</c> 标在一个
    /// public 字段上什么也不会多画——那个字段本来就由序列化通道画，且**可编辑**。
    /// 反过来，<c>[HideInInspector] public int x;</c> 虽然被 Unity 序列化，却不在 Inspector 里，
    /// 于是它会被本特性收进来并以只读展示。
    /// </para>
    /// <para>
    /// <b>静态成员也可以标。</b> 它们不随实例走，显示的是全局值；也因此不参与多选时
    /// 「各目标值是否一致」的比较（本来就一致）。
    /// </para>
    /// <para>
    /// <b>多选时各目标值不一致的成员显示「—」</b>，与 Unity 自身及本包其它只读展示一致。
    /// 成员在一部分目标上不存在时同样按「—」处理——给不出值就不要拿其中一个目标的值冒充。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异：</b> 只读（Odin 可写，但改动同样不会保存）；集合只显示「类型名（N 项）」
    /// 这样的摘要而不展开（展开集合是 L6 的事）；嵌套 <c>[Serializable]</c> 类型里的成员
    /// 收不到（拿到嵌套实例需要一条本包还没有的「只读反射路径解析」）。
    /// </para>
    /// <para>
    /// <b>标在方法上编译不过</b>——方法请用 <see cref="ButtonAttribute"/>。
    /// 「显示一个方法的返回值」在本包没有既定语义，与其猜一个形状，不如让它响亮地报错。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// private int _hidden;                       // 没有 [SerializeField]：Unity 不画
    ///
    /// [ShowInInspector]
    /// private int Hidden => _hidden * 2;         // 计算属性：以只读文本出现
    ///
    /// [ShowInInspector]
    /// public static int InstanceCount;           // 静态成员也可以
    /// </code>
    /// </example>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class ShowInInspectorAttribute : Attribute, ITreeMembershipAttribute
    {
    }
}
