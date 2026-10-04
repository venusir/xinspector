using UnityEngine;
using XInspector;

namespace XInspector.Sandbox
{
    /// <summary>
    /// 自动接管判据的**方法侧**验证台：只挂 <c>[Button]</c>，一个字段特性都不带。
    /// <para>
    /// **属于工程壳，不随包发布。**
    /// </para>
    /// <para>
    /// 为什么单独要一个组件：<c>IsUsedBy</c> 那条判据曾经只扫字段与类级特性。
    /// 只挂 <c>[Button]</c> 的类型会因此**不被自动接管**——现象是按钮完全不出现，
    /// 且**一条告警都没有**，是最难归因的一类故障。单元测试盯住了判据本身，
    /// 这里盯的是端到端：选中 <c>Demo 5</c>，按钮应当出现且能点。
    /// </para>
    /// <para>
    /// 对照方式同 <see cref="AutoTakeoverDemo"/>：把脚本宏删掉，它就退回原生外观
    /// （只剩两个普通字段），这既是「可逆」的验证，也说明按钮确实来自本管线。
    /// </para>
    /// </summary>
    [Title("按钮自动接管", Subtitle = "只有方法带特性，连类级特性都没有")]
    public class ButtonTakeoverDemo : MonoBehaviour
    {
        #region Public Fields

        /// <summary>一个普通字段——**不带任何特性**，故它本身不构成接管理由。</summary>
        public float speed = 1f;

        #endregion

        #region Buttons

        /// <summary>唯一的用法就是它：点一下速度翻倍。</summary>
        [Button("速度 ×2")]
        private void DoubleSpeed()
        {
            speed *= 2f;
        }

        /// <summary>再一个按钮，顺带确认多个按钮都在。</summary>
        [Button]
        private void ResetSpeed()
        {
            speed = 1f;
        }

        #endregion
    }
}
