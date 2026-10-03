using System;
using UnityEngine;

namespace XInspector.Sandbox
{
    /// <summary>
    /// 开发用演示组件。**属于工程壳，不随包发布。**
    /// <para>
    /// 字段刻意覆盖了各类值：基础类型、枚举、结构、对象引用、数组、嵌套可序列化结构，
    /// 以及「应当出现」与「不应出现」的两种可见性（<c>[SerializeField]</c> 私有字段
    /// 应出现，<c>[HideInInspector]</c> 不应出现）。渲染结果要与 Unity 原生 Inspector
    /// 逐像素一致，字段覆盖面不足的话这个比较就没有意义。
    /// </para>
    /// <para>
    /// 本阶段刻意**不带任何 XInspector 特性**：这样渲染差异只可能来自值管道，
    /// 而不可能来自某个特性画错了。
    /// </para>
    /// </summary>
    public class DemoComponent : MonoBehaviour
    {
        #region Public Fields

        /// <summary>字符串。</summary>
        public string playerName = "Player";

        /// <summary>整数。</summary>
        public int health = 100;

        /// <summary>浮点数。</summary>
        public float speed = 5f;

        /// <summary>布尔。</summary>
        public bool isAlive = true;

        /// <summary>枚举。</summary>
        public DemoQuality quality = DemoQuality.High;

        /// <summary>结构。</summary>
        public Vector3 spawnPoint = Vector3.zero;

        /// <summary>颜色。</summary>
        public Color tint = Color.white;

        /// <summary>对象引用。</summary>
        public GameObject target;

        /// <summary>嵌套可序列化结构——验证 <c>includeChildren</c> 路径。</summary>
        public DemoNested nested;

        /// <summary>数组——验证 Unity 自带的列表绘制。</summary>
        public string[] tags = { "alpha", "beta" };

        /// <summary>被 <c>[HideInInspector]</c> 标注，**不应出现**在 Inspector 里。</summary>
        [HideInInspector]
        public int shouldNotAppear = 3;

        #endregion

        #region Private Fields

        /// <summary>私有但可序列化，**应当出现**在 Inspector 里。</summary>
        [SerializeField]
        private int _serializedPrivate = 7;

        #endregion

        #region Public Properties

        /// <summary>
        /// 读一下私有字段。
        /// <para>
        /// 纯粹为了让编译器认为该字段被使用过，避免 CS0414「已赋值但从未使用」告警——
        /// 本工程的验收要求是控制台零告警。属性本身不参与序列化，故不影响 Inspector 的呈现。
        /// </para>
        /// </summary>
        public int SerializedPrivate => _serializedPrivate;

        #endregion
    }

    /// <summary>演示用的嵌套可序列化结构。</summary>
    [Serializable]
    public struct DemoNested
    {
        /// <summary>整数成员。</summary>
        public int count;

        /// <summary>浮点成员。</summary>
        public float ratio;
    }

    /// <summary>演示用的枚举。</summary>
    public enum DemoQuality
    {
        /// <summary>低。</summary>
        Low,

        /// <summary>中。</summary>
        Medium,

        /// <summary>高。</summary>
        High,
    }
}
