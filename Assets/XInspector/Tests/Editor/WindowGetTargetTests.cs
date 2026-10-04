using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 窗口的 <c>GetTarget()</c>：默认检视自身，覆写后可检视任意对象。
    /// <para>
    /// 窗口可以无头建出来（<c>CreateInstance</c>），故这些断言不需要真的开一个窗口。
    /// 触发附加的那条非 GUI 路径是 <c>ResetToDefaults()</c>——它内部会先
    /// <c>Attach(GetTarget(), FilterFor(...))</c>，与 <c>OnGUI</c> 走的是同一段。
    /// </para>
    /// </summary>
    [TestFixture]
    public class WindowGetTargetTests
    {
        #region Teardown

        /// <summary>复位静态注册表。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 默认目标

        /// <summary>默认目标是窗口自身：画自己的字段，且 EditorWindow 的内部字段被挡在外面。</summary>
        [Test]
        public void 默认目标是窗口自身()
        {
            var window = ScriptableObject.CreateInstance<SelfTargetWindow>();

            try
            {
                window.ResetToDefaults();

                var paths = PathsOf(window.CurrentTree);

                Assert.That(paths, Does.Contain("ownField"), "窗口自己的字段该在。");
                Assert.That(
                    paths,
                    Does.Not.Contain("m_ViewDataDictionary"),
                    "EditorWindow 的内部字段仍要被过滤掉。");
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        #endregion

        #region 换目标

        /// <summary>
        /// 覆写成另一个 Unity 对象后，树画的是**那个对象**的字段，窗口自身的字段不在。
        /// <para>
        /// 这条同时钉住过滤器要跟着目标换：窗口那条过滤器要求「字段声明在窗口基类及派生类型上」，
        /// 若把它套到别的目标上，目标的字段会被**全部拒掉**——那时 <c>number</c> 不会出现。
        /// 症状不是画错，是几乎什么都不画，很隐蔽。
        /// </para>
        /// </summary>
        [Test]
        public void 换成Unity对象后画的是那个对象()
        {
            var asset = ScriptableObject.CreateInstance<HostFixture>();
            var window = ScriptableObject.CreateInstance<SwappableTargetWindow>();

            try
            {
                window.NextTarget = asset;
                window.ResetToDefaults();

                var paths = PathsOf(window.CurrentTree);

                Assert.That(paths, Does.Contain("number"), "目标对象的字段该在——过滤器没跟着换的话它会消失。");
                Assert.That(paths, Does.Not.Contain("ownField"), "窗口自身的字段不该在。");
            }
            finally
            {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// 覆写成普通对象时，树只收带 <c>[ShowInInspector]</c> 的成员，且没有序列化后端。
        /// </summary>
        [Test]
        public void 换成普通对象时只收标注成员()
        {
            var window = ScriptableObject.CreateInstance<SwappableTargetWindow>();

            try
            {
                window.NextTarget = new ReflectedPoco();
                window.ResetToDefaults();

                var tree = window.CurrentTree;

                Assert.That(tree, Is.Not.Null);
                Assert.That(tree.SerializedObject, Is.Null, "普通对象没有序列化后端。");
                Assert.That(ReflectedMemberTests.Find(tree.Root, "Marked"), Is.Not.Null);
                Assert.That(
                    ReflectedMemberTests.Find(tree.Root, "Unmarked"),
                    Is.Null,
                    "没标记的公开字段不该出现——否则就有了「Inspector 靠序列化、窗口靠可见性」两套语义。");
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        /// <summary>
        /// <c>GetTarget()</c> 返回 <c>null</c> 时窗口是空的，不抛。
        /// </summary>
        [Test]
        public void 目标为空时不抛()
        {
            var window = ScriptableObject.CreateInstance<SwappableTargetWindow>();

            try
            {
                window.NextTarget = null;

                Assert.That(() => window.ResetToDefaults(), Throws.Nothing);
                Assert.That(window.CurrentTree, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>取根的直接子节点路径。</summary>
        /// <param name="tree">属性树；可为 <c>null</c>。</param>
        /// <returns>路径列表。</returns>
        private static List<string> PathsOf(PropertyTree tree)
        {
            var paths = new List<string>();

            if (tree == null)
            {
                return paths;
            }

            foreach (var child in tree.Root.Children)
            {
                paths.Add(child.Path);
            }

            return paths;
        }

        #endregion
    }

    /// <summary>不覆写 <c>GetTarget</c> 的窗口：默认检视自身。</summary>
    internal sealed class SelfTargetWindow : XInspectorEditorWindow
    {
        /// <summary>窗口自己的字段。</summary>
        public int ownField;

        /// <summary>把受保护的树暴露出来供断言。</summary>
        public PropertyTree CurrentTree => Tree;
    }

    /// <summary>覆写了 <c>GetTarget</c> 的窗口：检视谁由 <see cref="NextTarget"/> 决定。</summary>
    internal sealed class SwappableTargetWindow : XInspectorEditorWindow
    {
        /// <summary>窗口自己的字段——换目标后它不该再出现。</summary>
        public int ownField;

        /// <summary>下一个被检视的对象。</summary>
        public object NextTarget;

        /// <summary>把受保护的树暴露出来供断言。</summary>
        public PropertyTree CurrentTree => Tree;

        /// <inheritdoc/>
        protected override object GetTarget()
        {
            return NextTarget;
        }
    }
}
