using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests
{
    /// <summary>
    /// Runtime 程序集的依赖守卫。
    /// <para>
    /// 这个 fixture 的价值不在覆盖率而在**契约**：XInspector 的 Runtime 侧必须能在玩家构建里
    /// 安全使用，即不得依赖 <c>UnityEditor</c>。一旦有人在 Runtime 代码里写了
    /// <c>using UnityEditor;</c> 而没套 <c>#if UNITY_EDITOR</c>，编辑器里一切正常、
    /// 打包时才炸——那是最晚才能发现的一类问题。这里把它挡在测试阶段。
    /// </para>
    /// <para>
    /// 用程序集名查找而非 <c>typeof</c>：这样本测试不依赖任何具体的 XInspector 类型，
    /// 在 Runtime 程序集还没有公开类型的阶段也能运行。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AssemblyReferenceTests
    {
        #region Private Fields

        private const string RuntimeAssemblyName = "Venusir.Xinspector";

        #endregion

        #region Tests

        /// <summary>
        /// Runtime 程序集必须存在，且不得引用 <c>UnityEditor</c>。
        /// </summary>
        [Test]
        public void RuntimeAssembly_DoesNotReferenceUnityEditor()
        {
            var assembly = FindAssembly(RuntimeAssemblyName);

            Assert.That(assembly, Is.Not.Null,
                $"{RuntimeAssemblyName} 未加载。Runtime 程序集必须存在——它承载所有公开特性。");

            var referenced = ReferencedAssemblyNames(assembly);
            Assert.That(referenced, Does.Not.Contain("UnityEditor"),
                $"{RuntimeAssemblyName} 引用了 UnityEditor，这会让它在玩家构建里编译失败。" +
                "编辑器专属代码必须放进 Venusir.Xinspector.Editor 程序集，或用 #if UNITY_EDITOR 包起来。");
        }

        /// <summary>
        /// 反向对照：本条断言 <c>UnityEditor</c> 在编辑器环境下确实是一个可被引用的程序集。
        /// <para>
        /// 没有它，上面那条测试可能因为「<c>UnityEditor</c> 根本没加载」而永远通过——
        /// 一个恒真的守卫等于没有守卫。
        /// </para>
        /// </summary>
        [Test]
        public void EditorEnvironment_UnityEditorAssemblyIsPresent()
        {
            Assert.That(FindAssembly("UnityEditor"), Is.Not.Null,
                "本测试须在编辑器中运行；找不到 UnityEditor 说明上面那条守卫是恒真的，起不到作用。");
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 按简单名在当前 <see cref="AppDomain"/> 的已加载程序集里查找。
        /// </summary>
        /// <param name="simpleName">程序集简单名，如 <c>Venusir.Xinspector</c>。</param>
        /// <returns>找到的程序集；未找到返回 <c>null</c>。</returns>
        private static Assembly FindAssembly(string simpleName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, simpleName, StringComparison.Ordinal))
                {
                    return assembly;
                }
            }

            return null;
        }

        /// <summary>
        /// 取程序集**编译器实际发出**的引用程序集简单名。
        /// <para>
        /// 注意这个方法反映的是「用到了什么」，不是「asmdef 里声明了什么」——C# 编译器会裁掉
        /// 未被使用的程序集引用。因此它适合用来断言<b>不存在</b>某个依赖（用了就必然发出引用，
        /// 断言可靠），不适合用来断言<b>存在</b>某个依赖（没用到就会被裁掉，会误报）。
        /// </para>
        /// </summary>
        /// <param name="assembly">目标程序集。</param>
        /// <returns>被引用程序集的简单名集合。</returns>
        private static IReadOnlyCollection<string> ReferencedAssemblyNames(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        }

        #endregion
    }
}
