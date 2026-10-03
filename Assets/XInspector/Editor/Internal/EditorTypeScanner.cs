using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 扫描所有已加载编辑器程序集中的派生类，筛出**可实例化**的那些。
    /// <para>
    /// 绘制器注册表与特性处理器注册表的发现逻辑完全相同：扫全部程序集、跳过抽象与泛型定义、
    /// 跳过没有公开无参构造的、对跳过的发出告警。把这段抽出来不只是为了少写几行——
    /// **两份各自演化的扫描器迟早会分叉**，而分叉的那一份会忘记跳过无参构造那种情况，
    /// 症状是启动时抛一个 <see cref="MissingMethodException"/>，且只在特定程序集组合下出现。
    /// </para>
    /// <para>
    /// 本类型只负责「哪些类型可用」这个判断，不负责实例化、也不管优先级——
    /// 那两件事各注册表自己知道（绘制器看 <see cref="DrawerPriorityAttribute"/>，
    /// 处理器看 <see cref="AttributeProcessor.ProcessorPriority"/>）。
    /// </para>
    /// </summary>
    internal static class EditorTypeScanner
    {
        #region Public API

        /// <summary>
        /// 收集可实例化的派生类型。
        /// </summary>
        /// <typeparam name="TBase">基类或接口。</typeparam>
        /// <param name="roleName">角色名，用于告警文案，如「绘制器」「特性处理器」。</param>
        /// <returns>可实例化的类型列表；顺序不保证，调用方需自行排序。</returns>
        /// <remarks>
        /// <para>
        /// 用 <see cref="TypeCache"/> 而非手写反射扫描：它由 Unity 维护、快得多，
        /// 且域重载后自动失效。扫描在首次使用时惰性发生，各注册表自己缓存结果。
        /// </para>
        /// <para>
        /// 没有公开无参构造的类型**跳过并告警**，而不是让扫描整个失败：
        /// 一个写坏的第三方扩展不该让整个 Inspector 瘫掉。
        /// </para>
        /// </remarks>
        public static List<Type> CollectInstantiable<TBase>(string roleName) where TBase : class
        {
            var result = new List<Type>();
            List<string> skipped = null;

            foreach (var type in TypeCache.GetTypesDerivedFrom<TBase>())
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    skipped ??= new List<string>();
                    skipped.Add(type.FullName);
                    continue;
                }

                result.Add(type);
            }

            if (skipped != null)
            {
                Debug.LogWarning(
                    $"[XInspector] 以下{roleName}没有公开无参构造函数，已跳过。" +
                    $"{roleName}是全工程共享的单例，必须能无参实例化：\n  " +
                    string.Join("\n  ", skipped));
            }

            return result;
        }

        #endregion
    }
}
