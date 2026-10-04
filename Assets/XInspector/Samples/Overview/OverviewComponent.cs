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
        /// 未分组字段，**刻意夹在两个分组之间**。
        /// <para>
        /// 它应当留在原位——夹在「基础」与「附加」中间，而不是被挤到 Inspector 末尾。
        /// 后者是「先摆所有分组、再摆散字段」那种朴素实现的必然结果，一眼就能看出不对。
        /// </para>
        /// </summary>
        public int ungrouped = 1;

        /// <summary>第二个顶层分组，用来把未分组字段夹在中间。</summary>
        [BoxGroup("附加")]
        public string note = "末尾的分组";

        #endregion
    }
}
