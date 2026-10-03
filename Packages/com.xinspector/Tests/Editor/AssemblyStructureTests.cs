using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 程序集结构的守卫。
    /// <para>
    /// 与 Runtime 侧的 <c>AssemblyReferenceTests</c> 互补：那边守「Runtime 不许碰 UnityEditor」，
    /// 这边守依赖方向——<c>XInspector.Runtime</c> 不得引用 <c>XInspector.Editor</c>。
    /// 方向一旦反过来，使用方的玩家构建就会连带编入编辑器代码，而且**只在打包时才暴露**。
    /// </para>
    /// <para>
    /// <b>为什么这里不断言反方向（编辑器引用 Runtime）。</b> 本条最初写成断言
    /// <c>XInspector.Editor</c> 引用 <c>XInspector.Runtime</c>，实测**误报**：
    /// <c>Assembly.GetReferencedAssemblies()</c> 返回的是**编译器实际发出**的引用，
    /// 而 C# 编译器会裁掉未被使用的程序集引用。编辑器程序集当时只有 <c>AssemblyInfo.cs</c>，
    /// 不碰任何 Runtime 类型，引用就被裁掉了——程序集明明在 asmdef 里声明了依赖。
    /// 也就是说，这个方法反映的是「用到了什么」而不是「声明了什么」。
    /// </para>
    /// <para>
    /// 反方向之所以可靠：一旦 Runtime 真的用了编辑器类型，编译器**必然**发出该引用，
    /// 断言就一定会失败。有依赖就报，没依赖就不报——这正是守卫需要的语义。
    /// 至于「编辑器引用 Runtime」无需守卫：真缺这个引用，代码根本编译不过，编译器已经兜住了。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AssemblyStructureTests
    {
        #region Private Fields

        private const string EditorAssemblyName = "XInspector.Editor";
        private const string RuntimeAssemblyName = "XInspector.Runtime";

        #endregion

        #region Tests

        /// <summary>
        /// 依赖方向不得反转：Runtime 一旦引用编辑器程序集，玩家构建必然失败。
        /// </summary>
        [Test]
        public void Runtime程序集不引用编辑器程序集()
        {
            var runtimeAssembly = FindAssembly(RuntimeAssemblyName);
            Assert.That(runtimeAssembly, Is.Not.Null, $"{RuntimeAssemblyName} 未加载。");

            Assert.That(ReferencedAssemblyNames(runtimeAssembly), Does.Not.Contain(EditorAssemblyName),
                $"依赖方向反了：{RuntimeAssemblyName} 引用了 {EditorAssemblyName}。" +
                "这会让使用方的玩家构建连带编入编辑器代码。");
        }

        /// <summary>
        /// 编辑器程序集存在且已加载。
        /// <para>
        /// 只断言存在性，不断言其引用内容——理由见类型级注释。
        /// </para>
        /// </summary>
        [Test]
        public void 编辑器程序集已加载()
        {
            Assert.That(FindAssembly(EditorAssemblyName), Is.Not.Null,
                $"{EditorAssemblyName} 未加载。");
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 按简单名在当前 <see cref="AppDomain"/> 的已加载程序集里查找。
        /// </summary>
        /// <param name="simpleName">程序集简单名。</param>
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
        /// </summary>
        /// <param name="assembly">目标程序集。</param>
        /// <returns>被引用程序集的简单名集合。未使用到的引用会被编译器裁掉，故本集合只反映实际用法。</returns>
        private static IReadOnlyCollection<string> ReferencedAssemblyNames(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        }

        #endregion
    }
}
