using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// 目标对象数组里「这一格还能不能用」的判定。
    /// <para>
    /// <b>为什么需要它。</b> 树的目标列表是 <c>object[]</c>——它要同时装得下
    /// <see cref="UnityEngine.Object"/> 与 POCO。但一旦形参类型写成 <c>object</c>，
    /// 裸写 <c>target != null</c> 就退化成**引用比较**，而 Unity 的已销毁对象
    /// 恰恰是「引用不为 null、按它自己的语义却是空」。
    /// </para>
    /// <para>
    /// 这条语义以前是白送的（数组类型是 <c>Object[]</c>，<c>!= null</c> 自动走 Unity 的重载）。
    /// 改型会把它悄悄弄丢，而症状是「多选里混了一个已销毁的对象时，按钮对它照调不误、
    /// 抛出的异常被打进 Console」——不致命，但那是**静默的行为退化**。
    /// 故把判定收成一处，让每个调用点显式带上这条语义。
    /// </para>
    /// </summary>
    internal static class TargetObjects
    {
        /// <summary>
        /// 这个目标还能用吗。
        /// </summary>
        /// <param name="target">目标对象，可为 <c>null</c>。</param>
        /// <returns>可用返回 <c>true</c>。</returns>
        public static bool IsAlive(object target)
        {
            if (target == null)
            {
                return false;
            }

            // 已销毁的 Unity 对象：引用还在，但 Unity 的 == 说它是空。
            return !(target is Object unity) || unity != null;
        }
    }
}
