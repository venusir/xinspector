using UnityEngine;
using XInspector;

namespace XInspector.Sandbox
{
    /// <summary>
    /// 垂直切片的完整演示：类级与成员级标题、两段路径的嵌套分组，
    /// 外加一个**夹在两个分组之间**的未分组字段。
    /// <para>
    /// **属于工程壳，不随包发布。**
    /// </para>
    /// <para>
    /// 这个组件的编辑器是显式写的（<c>AttributeDemoEditor</c>），
    /// 与 <see cref="AutoTakeoverDemo"/> 形成对照——两者渲染结果应当完全一致，
    /// 差别只在「谁接管」这件事上：一个是使用方明写的，一个是宏开着时自动接管的。
    /// </para>
    /// </summary>
    [Title("XInspector 演示", Subtitle = "类级标题 + 嵌套分组")]
    public class AttributeDemo : MonoBehaviour
    {
        #region Public Fields

        /// <summary>成员级标题，且属于外层分组。</summary>
        [Title("身份")]
        [BoxGroup("基础")]
        public string playerName = "Player";

        /// <summary>
        /// 未分组字段，**刻意夹在两个分组之间**。
        /// <para>
        /// 它应当留在原位——夹在「基础」与「附加」两个分组中间，
        /// 而不是被挤到 Inspector 末尾。后者是「先摆所有分组、再摆散字段」那种朴素实现的
        /// 必然结果，一眼就能看出不对。
        /// </para>
        /// </summary>
        public int ungrouped = 1;

        /// <summary>只写了深层路径，外层分组由构建期合成。</summary>
        [BoxGroup("基础/属性")]
        public int health = 100;

        /// <summary>与上者同组。</summary>
        [BoxGroup("基础/属性")]
        public float speed = 5f;

        /// <summary>第二个顶层分组，用来把未分组字段夹在中间。</summary>
        [BoxGroup("附加")]
        public string note = "末尾的分组";

        #endregion
    }
}
