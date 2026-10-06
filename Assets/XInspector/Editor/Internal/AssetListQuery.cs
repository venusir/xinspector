using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// <c>[AssetList].Path</c> 的归一化：<c>|</c> 分隔、工程相对、**兼容官方样例的前导斜杠写法**。
    /// </summary>
    /// <remarks>
    /// 拆段复用 <see cref="AssetSelectorOptions.SplitPaths"/>（去空白、去尾部 <c>/</c>、丢空段）——
    /// 「<c>|</c> 分隔的目录列表」只有一份实现。这里只加本包对官方样例
    /// （<c>Path = "/Plugins/Sirenix/"</c>）的解释：前导 <c>/</c> 去掉，若剩余部分不以
    /// <c>Assets/</c> 或 <c>Packages/</c> 开头就补 <c>Assets/</c>。
    /// </remarks>
    internal static class AssetListPaths
    {
        #region Public API

        /// <summary>
        /// 归一化目录列表。
        /// </summary>
        /// <param name="path">特性上的原文；可为 <c>null</c>。</param>
        /// <returns>目录数组；没有有效目录时为空数组（表示整个工程）。</returns>
        public static string[] Normalize(string path)
        {
            var parts = AssetSelectorOptions.SplitPaths(path);
            if (parts.Length == 0)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>(parts.Length);

            for (var i = 0; i < parts.Length; i++)
            {
                var normalized = NormalizeOne(parts[i]);
                if (normalized.Length > 0)
                {
                    result.Add(normalized);
                }
            }

            return result.ToArray();
        }

        #endregion

        #region Private Helpers

        /// <summary>单段归一化。</summary>
        /// <param name="part">已去空白、去尾部斜杠的一段。</param>
        /// <returns>工程相对路径；无意义时返回空串。</returns>
        private static string NormalizeOne(string part)
        {
            var trimmed = part.TrimStart('/');

            if (trimmed.Length == 0)
            {
                // 整条就是一个「/」：那是整个工程的意思，交给空数组表达。
                return string.Empty;
            }

            if (trimmed.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return "Assets/" + trimmed;
        }

        #endregion
    }

    /// <summary>
    /// 资产列表的两个过滤判定——**纯函数**，可无头测试。
    /// </summary>
    internal static class AssetListFilter
    {
        #region Public API

        /// <summary>
        /// 按类型收窄的搜索串（<c>t:Xxx</c>）。
        /// </summary>
        /// <param name="elementType">元素类型。</param>
        /// <returns>搜索串；类型为空时返回 <c>null</c>。</returns>
        /// <remarks>
        /// <b>它只是启发式收窄，不保证完备</b>：抽象类、不常见的派生命中不了是可能的，
        /// 而**正确性由落值前那一道类型校验保证**（<c>AssetListWrite</c>），
        /// 菜单空了也有一条响亮的告警（不静默）。
        /// </remarks>
        public static string TypeFilter(Type elementType)
        {
            return elementType == null ? null : "t:" + elementType.Name;
        }

        /// <summary>
        /// 资产路径的文件名（不含扩展名）是否以给定前缀开头。
        /// </summary>
        /// <param name="assetPath">工程路径。</param>
        /// <param name="prefix">前缀；空白表示不过滤。</param>
        /// <returns>通过返回 <c>true</c>。</returns>
        /// <remarks>本包自定语义（<c>AssetDatabase</c> 里没有名字前缀过滤器）：大小写不敏感。</remarks>
        public static bool MatchesNamePrefix(string assetPath, string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return true;
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            return name != null &&
                   name.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }

    /// <summary>
    /// 资产查询：<c>AssetDatabase</c> 只在这里出现，这样其余判定都还能无头测试。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="AssetSelectorQuery"/> 同一条纪律：**只在事件路径调用**（菜单弹出、拖放），
    /// 不是每帧——全工程搜索是重活，放在绘制路径上会拖垮 Inspector。
    /// </remarks>
    internal static class AssetListQuery
    {
        #region Public API

        /// <summary>
        /// 按模型找出候选资产路径（已套名字前缀过滤）。
        /// </summary>
        /// <param name="model">构建期模型。</param>
        /// <returns>资产路径列表；一处都搜不到时为空列表（调用方负责告警）。</returns>
        public static List<string> Find(AssetListModel model)
        {
            var paths = AssetSelectorQuery.FindAssetPaths(model.Folders, model.TypeFilter);

            if (string.IsNullOrWhiteSpace(model.NamePrefix))
            {
                return paths;
            }

            var result = new List<string>(paths.Count);

            for (var i = 0; i < paths.Count; i++)
            {
                if (AssetListFilter.MatchesNamePrefix(paths[i], model.NamePrefix))
                {
                    result.Add(paths[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// 按路径取出**元素类型**的资产。
        /// </summary>
        /// <param name="assetPath">工程路径。</param>
        /// <param name="elementType">要的类型。</param>
        /// <returns>资产；取不到时返回 <c>null</c>（调用方负责告警）。</returns>
        /// <remarks>
        /// <b>先主资产、再全部子资产。</b> 这一步不能省：<c>t:Sprite</c> / <c>t:Mesh</c> 命中的是
        /// <c>.png</c> / <c>.fbx</c> 的**路径**，而 <c>LoadMainAssetAtPath</c> 给的是
        /// <c>Texture2D</c> / <c>GameObject</c>——只用它会表现成「菜单里点了没反应」。
        /// </remarks>
        public static UnityEngine.Object Load(string assetPath, Type elementType)
        {
            var main = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (main != null && elementType.IsInstanceOfType(main))
            {
                return main;
            }

            var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);

            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null && elementType.IsInstanceOfType(all[i]))
                {
                    return all[i];
                }
            }

            return null;
        }

        #endregion
    }
}
