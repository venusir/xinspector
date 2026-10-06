using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 命名调色板资产：一组颜色，供 <see cref="XInspector.ColorPaletteAttribute"/> 画成色块行。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>放在 Editor 程序集而不是 Runtime，这是硬约束不是偏好。</b>
    /// Runtime 侧「零 Unity 依赖」是**编译期强制**的（<c>Tests.Native</c> 直接编译
    /// <c>Runtime/**</c>，只引 BCL），而 <see cref="ScriptableObject"/> 是 UnityEngine 类型。
    /// 代价写清楚：使用方只能在编辑器代码里引用本类型——但调色板本来就只在编辑器里用。
    /// </para>
    /// <para>
    /// <b>按资产名查找</b>（文件名去扩展名，大小写不敏感），故**改名就是改调色板名**。
    /// 同名的两份资产是允许存在的（Unity 会提示重名），那时按名查找取先扫到的那一份——
    /// 用「唯一」形态（<c>[ColorPalette]</c>）的多份歧义是另一条独立的告警。
    /// </para>
    /// <para>
    /// <b>本类型自己就用本包的画法</b>（下面的字段上标着 <c>[Title]</c> 等）——建出来就是一个
    /// 顺手可编辑的资产：色块列表、可增删、带索引标签。
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "XInspector/Color Palette", fileName = "New Color Palette")]
    public sealed class XInspectorColorPalette : ScriptableObject
    {
        #region Private Fields

        /// <summary>
        /// 调色板里的颜色。
        /// </summary>
        /// <remarks>
        /// <b>新建的资产带一份默认色</b>（本包自定值，删掉改掉都行）：空调色板等于「这个特性
        /// 什么都不做」，而那是用户建完资产第一眼就看到的现象。
        /// </remarks>
        [Title("调色板", Subtitle = "色块行的顺序就是这里的顺序")]
        [InfoBox("这个资产由 [ColorPalette] 按**文件名**查找——改名就是改调色板名。")]
        [ListDrawerSettings(ShowIndexLabels = true)]
        [SerializeField]
        private Color[] _colors =
        {
            new Color(0.10f, 0.10f, 0.10f),
            new Color(0.90f, 0.90f, 0.90f),
            new Color(0.85f, 0.25f, 0.25f),
            new Color(0.90f, 0.60f, 0.20f),
            new Color(0.90f, 0.85f, 0.30f),
            new Color(0.35f, 0.75f, 0.40f),
            new Color(0.30f, 0.60f, 0.90f),
            new Color(0.60f, 0.45f, 0.85f),
        };

        #endregion

        #region Public API

        /// <summary>
        /// 调色板里的颜色，**不会为 <c>null</c>**（空的调色板给空表）。
        /// </summary>
        /// <remarks>
        /// 只给只读视图：给一个公开的可变数组，一处误写就会散播到每个消费者（同一份资产可能被
        /// 几十个字段共用）。要改就整体换——<see cref="SetColors"/>。
        /// </remarks>
        public IReadOnlyList<Color> Colors => _colors ?? Array.Empty<Color>();

        /// <summary>
        /// 整体替换调色板里的颜色。
        /// </summary>
        /// <param name="colors">新的颜色；传 <c>null</c> 等于清空。</param>
        public void SetColors(params Color[] colors)
        {
            _colors = colors ?? Array.Empty<Color>();
        }

        #endregion
    }
}
