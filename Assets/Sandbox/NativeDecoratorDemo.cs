using System;
using UnityEngine;

namespace XInspector.Sandbox
{
    /// <summary>
    /// L0 验证台：Unity **原生**装饰器与内置绘制器是否照常工作。
    /// <para>
    /// **属于工程壳，不随包发布。**
    /// </para>
    /// <para>
    /// <c>OdinGap.md</c> 里唯一没实测过的推断是「Unity 原生装饰器由 <c>PropertyField</c>
    /// 照常绘制，因此不算缺口」——逐条表里标 ➖ 的那几项全建立在它上面。本组件就是那条推断的
    /// 验证台，**不含任何 XInspector 特性**，这样渲染差异只可能来自「原生装饰器有没有流经我们的管线」。
    /// </para>
    /// <list type="bullet">
    /// <item><c>[Header]</c> <c>[Space]</c>——DecoratorDrawer：画在字段之外，且**两个字段各带一个
    /// <c>[Header]</c>**（若装饰器被暴露成独立条目，两个同名条目会撞树的身份契约，这正是要看的）</item>
    /// <item><c>[Range]</c> <c>[TextArea]</c> <c>[Multiline]</c>——Unity 内置绘制器：替换值的画法</item>
    /// <item><c>[Tooltip]</c>——由 <c>PropertyField</c> 直接读取</item>
    /// </list>
    /// <para>
    /// 对照方式：场景里 Demo 1 走原生 Inspector，本组件走 XInspector 管线
    /// （<c>NativeDecoratorDemoEditor</c>），两者外观应当逐字段一致。
    /// <b>结构可无头断言，渲染不可</b>——渲染在 <c>PropertyField</c> 内部发生，
    /// 按本仓策略不测 IMGUI，故「画得出来」这一条只能靠肉眼。
    /// 结构侧的回归守卫见 <c>NativeDecoratorTests</c>。
    /// </para>
    /// </summary>
    public class NativeDecoratorDemo : MonoBehaviour
    {
        #region Public Fields

        /// <summary>三个装饰器叠在同一个字段上：段头、间距、滑块。</summary>
        [Header("整数段")]
        [Space(12f)]
        [Range(0, 10)]
        public int ranged = 5;

        /// <summary>原生 Tooltip，悬停标签时应出现。</summary>
        [Tooltip("这是 Unity 原生的 Tooltip")]
        public float tooltipped = 1f;

        /// <summary>多行文本框（带滚动条）。</summary>
        [TextArea(2, 5)]
        public string notes = "多行文本";

        /// <summary>固定行数的多行框。</summary>
        [Multiline(3)]
        public string address = "第一行\n第二行";

        /// <summary>第二个段头——与上面的段头同名，用来暴露「装饰器是否成为独立节点」。</summary>
        [Header("第二个段头")]
        public string afterHeader = "两个 Header 同时出现";

        /// <summary>嵌套结构内部的 <c>[Range]</c> 走 <c>includeChildren</c> 路径。</summary>
        public DecoratorNested nested;

        /// <summary>被 <c>[HideInInspector]</c> 标注，**不应出现**。</summary>
        [HideInInspector]
        public int shouldNotAppear = 3;

        #endregion
    }

    /// <summary>演示用的嵌套结构，成员上带原生 <c>[Range]</c>。</summary>
    [Serializable]
    public struct DecoratorNested
    {
        /// <summary>应当在展开的嵌套块里表现为滑块。</summary>
        [Range(0f, 1f)]
        public float ratio;
    }
}
