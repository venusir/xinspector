using UnityEngine;
using XInspector;
using XInspector.Samples;

namespace XInspector.Sandbox
{
    /// <summary>
    /// 自动接管的验证用组件：**刻意不写编辑器**。
    /// <para>
    /// **属于工程壳，不随包发布。**
    /// </para>
    /// <para>
    /// 本开发工程把脚本宏 <c>XINSPECTOR_AUTO_EDITOR</c> 开着，因此这个组件应当
    /// 与 <see cref="OverviewComponent"/>（包内示例，走显式编辑器那条路）渲染得一模一样
    /// ——尽管它没有任何编辑器代码。
    /// 把 <c>ProjectSettings</c> 里的宏删掉，它就会退回 Unity 原生外观（特性不再生效），
    /// 这就是「可逆」的验证方式。
    /// </para>
    /// </summary>
    [Title("自动接管演示", Subtitle = "没有写编辑器，靠宏生效")]
    public class AutoTakeoverDemo : MonoBehaviour
    {
        #region Public Fields

        /// <summary>带标题且属于分组的成员。</summary>
        [Title("配置")]
        [BoxGroup("参数")]
        public float duration = 1.5f;

        /// <summary>同组成员。</summary>
        [BoxGroup("参数")]
        public bool loop = true;

        #endregion
    }
}
