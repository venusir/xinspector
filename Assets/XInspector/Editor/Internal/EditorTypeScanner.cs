using System;
using System.Collections.Generic;
using System.Reflection;
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
        /// （测试程序集里的类型跳过但**不**告警，见 <see cref="ShouldWarnAbout"/>。）
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
                    // 测试程序集里故意不可实例化的夹具静默跳过——告警的收件人是使用方，见 ShouldWarnAbout。
                    if (ShouldWarnAbout(type))
                    {
                        skipped ??= new List<string>();
                        skipped.Add(type.FullName);
                    }

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

        /// <summary>
        /// 这条告警该不该为这个类型发出。
        /// </summary>
        /// <param name="type">被跳过的类型。</param>
        /// <returns>该告警返回 <c>true</c>。</returns>
        /// <remarks>
        /// <b>测试程序集里的类型不告警。</b> 这是刻意的收窄，而且只收窄**告警**、不收窄**扫描**：
        /// 扫描必须覆盖所有已加载的程序集（那是「使用方零注册扩展」的前提，两条
        /// <c>Registry_Discovers*FromThisAssembly</c> 用例守着它）。
        /// 理由在告警的收件人身上——它要说给**写坏了自己扩展的使用方**听；
        /// 测试里那些故意不可实例化的夹具不是它的目标，那两条
        /// <c>Registry_SkipsTypesWithoutPublicParameterlessConstructor</c> 用例
        /// 自己断言了「确实被跳过」，不必借 Console 说话。
        /// </remarks>
        public static bool ShouldWarnAbout(Type type)
        {
            return !IsTestAssembly(type.Assembly);
        }

        /// <summary>
        /// 这个程序集是不是测试程序集。
        /// </summary>
        /// <param name="assembly">候选程序集，可为 <c>null</c>。</param>
        /// <returns>是测试程序集返回 <c>true</c>。</returns>
        /// <remarks>
        /// 判据是**引用了 <c>nunit.framework</c>**——测试程序集必然用到它，
        /// 而生产程序集不会去引用一个测试框架。这与 Unity 自己分辨测试程序集的口径一致。
        /// <para>
        /// 判据只决定「要不要打一行日志」，故万一误判，代价仅是少一行话，
        /// **不会让任何绘制器或处理器少注册一个**——这正是它不去动扫描范围的原因。
        /// </para>
        /// </remarks>
        public static bool IsTestAssembly(Assembly assembly)
        {
            if (assembly == null)
            {
                return false;
            }

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (string.Equals(reference.Name, "nunit.framework", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
