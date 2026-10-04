using UnityEngine;

namespace XInspector.Sandbox
{
    /// <summary>
    /// 自动接管判据的**属性侧**对照组：一个字段特性都不带，只在普通属性上挂
    /// <see cref="ShowInInspectorAttribute"/>。
    /// <para>
    /// 判据漏扫属性时，它会静默地退回原生外观——而原生 Inspector 里那个属性**根本不出现**，
    /// 于是症状是「特性毫无动静、零告警」。这与当年 <c>[Button]</c> 漏扫方法同型：
    /// 漏掉的不是「少画了点东西」，而是整类特性一次都不生效。
    /// </para>
    /// <para>
    /// 选中本对象时应当看到：<c>editable</c> 是一个可编辑的整数字段，
    /// 而 <c>Doubled</c> 与 <c>Shared</c> 以**只读文本**出现（它们不在 Unity 的序列化里）。
    /// 改一改 <c>editable</c>，<c>Doubled</c> 会跟着变——它每帧现读。
    /// </para>
    /// </summary>
    public sealed class ShowInInspectorTakeoverDemo : MonoBehaviour
    {
        /// <summary>普通序列化字段：由序列化通道画，可编辑。</summary>
        public int editable = 5;

        /// <summary>静态成员：全局值，不随实例走。</summary>
        [ShowInInspector]
        public static string Shared = "静态成员也可以标";

        /// <summary>反射成员：只读展示，且每帧现读——改上面的字段它立刻跟着变。</summary>
        [ShowInInspector]
        private int Doubled => editable * 2;
    }
}
