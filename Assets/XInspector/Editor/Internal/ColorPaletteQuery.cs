using System;
using System.Collections.Generic;
using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 调色板资产的查找：按名找一份、或取全部。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="AssetDatabase"/> 只在这里出现</b>——与 <c>AssetListQuery</c> 同一条纪律：
    /// 扫描与缓存收在一处，别处只认结果。
    /// </para>
    /// <para>
    /// <b>惰性全扫一次，结果进静态表。</b> 查找发生在建树期与绘制期（色块行每帧都要那几份颜色），
    /// 每次都全工程搜一遍是不可能的。失效靠 <see cref="EditorApplication.projectChanged"/>
    /// ——资产导入 / 改名 / 移动 / 删除都会触发它。
    /// </para>
    /// <para>
    /// <b>失效是两级节流</b>：事件只置一个脏标记，真正的重扫推给下一次查找。这样即使某个常见操作
    /// 让 <c>projectChanged</c> 连着触发，代价也只是「下次查找多扫一遍」，而不是每次事件扫一遍。
    /// </para>
    /// <para>
    /// <b>这是本包第一条 <see cref="EditorApplication"/> 静态订阅</b>：订阅写成幂等的
    /// （先减后加），域重载后不会累积；<see cref="Invalidate"/> 幂等且代价为零，
    /// 故 fixture 的 <c>TearDown</c> 可以随手调它复位。
    /// </para>
    /// </remarks>
    internal static class ColorPaletteQuery
    {
        #region Private Fields

        /// <summary>按类型搜资产——判据是**类型**而不是目录，故调色板放哪都找得到。</summary>
        private const string TypeFilter = "t:XInspectorColorPalette";

        /// <summary>全部调色板，按名字排序（顺序稳定——告警里要列候选）。</summary>
        private static List<XInspectorColorPalette> _all;

        /// <summary>按名字索引（大小写不敏感）。</summary>
        private static Dictionary<string, XInspectorColorPalette> _byName;

        /// <summary>资产库动过，下次查找要重扫。</summary>
        private static bool _dirty;

        #endregion

        #region Public API

        /// <summary>
        /// 工程里所有的调色板，**按名字排序**。
        /// </summary>
        /// <returns>列表；一份都没有时返回空表（不会为 <c>null</c>）。</returns>
        public static IReadOnlyList<XInspectorColorPalette> All()
        {
            EnsureScanned();
            return _all;
        }

        /// <summary>
        /// 按名字找一份调色板。
        /// </summary>
        /// <param name="name">
        /// 调色板名 = **资产名**（Unity 的 <c>Object.name</c> 就是文件名去扩展名，
        /// 也正是工程窗口里显示的那个名字）。大小写不敏感。
        /// </param>
        /// <returns>找到返回它；找不到返回 <c>null</c>。</returns>
        public static XInspectorColorPalette Find(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            EnsureScanned();

            // 比一下是不是 Unity 的「伪 null」（资产刚被删）——那种情况当作没找到。
            return _byName.TryGetValue(name, out var palette) && palette != null ? palette : null;
        }

        /// <summary>
        /// 标记「下次查找要重扫」。
        /// </summary>
        /// <remarks>幂等，代价为零；测试用它复位，<c>projectChanged</c> 用它失效。</remarks>
        public static void Invalidate()
        {
            _dirty = true;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 订阅资产库变更。**幂等**：域重载后重跑也只是先减后加，不会累积。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Subscribe()
        {
            EditorApplication.projectChanged -= Invalidate;
            EditorApplication.projectChanged += Invalidate;
        }

        /// <summary>必要时重扫一遍。</summary>
        private static void EnsureScanned()
        {
            if (_all != null && !_dirty)
            {
                return;
            }

            var found = new List<XInspectorColorPalette>();
            var byName = new Dictionary<string, XInspectorColorPalette>(StringComparer.OrdinalIgnoreCase);

            foreach (var guid in AssetDatabase.FindAssets(TypeFilter))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var palette = AssetDatabase.LoadAssetAtPath<XInspectorColorPalette>(path);

                if (palette == null)
                {
                    continue;
                }

                found.Add(palette);
                byName[palette.name] = palette;
            }

            found.Sort(CompareByName);

            _all = found;
            _byName = byName;
            _dirty = false;
        }

        /// <summary>按资产名排序（序数比较，跨平台稳定——与别处的排序同一口径）。</summary>
        /// <param name="left">左。</param>
        /// <param name="right">右。</param>
        /// <returns>比较结果。</returns>
        private static int CompareByName(XInspectorColorPalette left, XInspectorColorPalette right)
        {
            return string.CompareOrdinal(left.name, right.name);
        }

        #endregion
    }
}
