using System;
using System.Collections.Generic;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 绘制期告警的「每个属性只报一次」机制。
    /// <para>
    /// 绘制器每帧跑一遍，条件不满足时若每帧都 <c>Debug.LogWarning</c>，控制台会被刷爆——
    /// 而真正当回事的告警反而被淹掉。计数放在 <see cref="PropertyState"/> 的附加状态袋里
    /// （绘制器不得持有可变字段），属性随树重建时账本一并清零。
    /// </para>
    /// </summary>
    internal static class DrawerWarnings
    {
        #region Public API

        /// <summary>
        /// 报一条告警，同一属性上的同一 <paramref name="key"/> 只报一次。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="key">去重键，同一属性上不同的告警用不同的键。</param>
        /// <param name="message">完整的告警文本（含 <c>[XInspector]</c> 前缀）。</param>
        public static void Once(InspectorProperty property, string key, string message)
        {
            if (property == null)
            {
                Debug.LogWarning(message);
                return;
            }

            var state = property.State.GetOrCreate<DrawerWarningState>();
            if (state.Reported.Add(key))
            {
                Debug.LogWarning(message);
            }
        }

        /// <summary>
        /// 生成「类型不符、已退回普通绘制」的告警文本。
        /// </summary>
        /// <param name="property">目标属性。</param>
        /// <param name="attributeName">特性名（含方括号）。</param>
        /// <param name="requirement">它要求的类型描述。</param>
        /// <returns>告警文本。</returns>
        /// <remarks>
        /// <para>
        /// 失败时一律**放行**（调用下一个绘制器）而不是什么都不画——一个标错位置的特性
        /// 不该让字段消失，那正是本包最忌讳的现象。
        /// </para>
        /// <para>
        /// <b>反射成员要单独说。</b> 值绘制器拿不到 <c>SerializedProperty</c> 时统统走这里，
        /// 而「字段退回普通绘制」那句话对 <c>[ShowInInspector]</c> 的只读成员是错的——
        /// 它没有字段可退，值是照常显示的，只是这个特性不起作用。措辞错了比不说更糟：
        /// 照着它去查「为什么字段没画出来」会一无所获。
        /// </para>
        /// </remarks>
        public static string TypeMismatch(InspectorProperty property, string attributeName, string requirement)
        {
            var entry = property.ValueEntry;

            if (entry != null && !entry.IsUnityBacked)
            {
                return $"[XInspector] 属性「{property.Path}」上的 {attributeName} 需要 Unity 的序列化后端，" +
                       "而这是一个 [ShowInInspector] 的只读成员，该特性对它无效——值仍以只读文本显示。";
            }

            var actual = entry != null ? entry.ValueType.Name : "未知";
            return $"[XInspector] 属性「{property.Path}」上的 {attributeName} 只支持{requirement}，" +
                   $"当前是 {actual}，该特性已忽略、字段退回普通绘制。";
        }

        #endregion

        #region Private Types

        /// <summary>已报过的告警键集合，挂在 <see cref="PropertyState"/> 上。</summary>
        private sealed class DrawerWarningState
        {
            /// <summary>已报过的告警键。</summary>
            public readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);
        }

        #endregion
    }
}
