using UnityEngine;
using XInspector;

namespace XInspector.Samples
{
    /// <summary>
    /// Overview 示例组件：展示标题与嵌套分组的最小可用形态。
    /// </summary>
    [Title("玩家档案", Subtitle = "XInspector Overview 示例")]
    public class OverviewComponent : MonoBehaviour
    {
        #region Public Fields

        /// <summary>成员级标题，同时属于外层分组。</summary>
        [Title("身份")]
        [BoxGroup("基础")]
        public string playerName = "Player";

        /// <summary>
        /// 只声明了深层路径 <c>基础/属性</c>，外层的「基础」分组由构建期自动合成
        /// ——不必再写一遍 <c>[BoxGroup("基础")]</c>。
        /// </summary>
        [BoxGroup("基础/属性")]
        public int health = 100;

        /// <summary>与上者同组。</summary>
        [BoxGroup("基础/属性")]
        public float speed = 5f;

        /// <summary>
        /// 未分组字段：它留在根层级，不会因为上面有分组就被吞进去。
        /// <para>
        /// 「未分组字段夹在两个分组之间时留在原位」那条规则，在
        /// <c>Assets/Sandbox/AttributeDemo.cs</c> 里有更直观的演示。
        /// </para>
        /// </summary>
        public int ungrouped = 1;

        #endregion
    }
}
