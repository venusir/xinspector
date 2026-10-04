using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 两个按钮分组：方法进没进分组、组长什么样、折行算得对不对。
    /// <para>
    /// 折行是纯函数，故这里能把它测透；行作用域的开关在末端，属于 IMGUI，不在这里验。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ButtonGroupDrawerTests
    {
        #region Private Fields

        private ButtonGroupFixture _target;

        #endregion

        #region Setup / Teardown

        /// <summary>建立测试资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ButtonGroupFixture>();
        }

        /// <summary>销毁资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 分组装配

        /// <summary>同一组名的按钮收进同一个分组节点，未分组的留在根下。</summary>
        [Test]
        public void 同组名的按钮收进同一个分组节点()
        {
            using (var tree = Build())
            {
                var group = GroupNode(tree, ButtonGroupAttribute.DefaultGroupName);

                Assert.That(group, Is.Not.Null, "裸用 [ButtonGroup] 时应归入默认组。");
                Assert.That(PathsOf(group), Is.EquivalentTo(new[] { "Alpha()", "Beta()" }));
                Assert.That(PathsOfRoot(tree), Does.Contain("Loose()"), "没分组的按钮留在根下。");
            }
        }

        /// <summary>分组节点上有分组绘制器——没有它这组就只是一堆竖排的按钮。</summary>
        [Test]
        public void 分组节点上有按钮组绘制器()
        {
            using (var tree = Build())
            {
                var entries = GroupNode(tree, ButtonGroupAttribute.DefaultGroupName).Chain.Entries;

                var found = false;
                for (var i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Drawer is ButtonGroupDrawer)
                    {
                        found = true;
                    }
                }

                Assert.That(found, Is.True, "分组节点上没有 ButtonGroupDrawer。");
            }
        }

        /// <summary>响应式按钮组同样装配成分组节点，并配自己的绘制器。</summary>
        [Test]
        public void 响应式分组也装配成分组节点()
        {
            using (var tree = Build())
            {
                var group = GroupNode(tree, ResponsiveButtonGroupAttribute.DefaultGroupName);

                Assert.That(group, Is.Not.Null);
                Assert.That(PathsOf(group), Is.EquivalentTo(new[] { "Gamma()", "Delta()" }));

                var entries = group.Chain.Entries;
                var found = false;
                for (var i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Drawer is ResponsiveButtonGroupDrawer)
                    {
                        found = true;
                    }
                }

                Assert.That(found, Is.True);
            }
        }

        /// <summary>组上的等宽设定由合并规则合并——两个成员各写一半时也要成立。</summary>
        [Test]
        public void 等宽设定按或合并()
        {
            using (var tree = Build())
            {
                var group = GroupNode(tree, "等宽组");

                Assert.That(group.Attributes.Get<ResponsiveButtonGroupAttribute>().UniformLayout, Is.True);
            }
        }

        #endregion

        #region 折行

        /// <summary>装得下就是一行。</summary>
        [Test]
        public void 装得下就是一行()
        {
            var widths = new[] { 30f, 30f, 30f };
            var rows = new int[3];

            Assert.That(ResponsiveButtonRows.Resolve(widths, 3, 2f, 200f, false, rows), Is.EqualTo(1));
            Assert.That(rows, Is.EqualTo(new[] { 0, 0, 0 }));
        }

        /// <summary>装不下就换行，且行号连续递增。</summary>
        [Test]
        public void 装不下就换行()
        {
            // 30 + 2 + 30 = 62 ≤ 70；再加一格 30 就超了。
            var widths = new[] { 30f, 30f, 30f };
            var rows = new int[3];

            Assert.That(ResponsiveButtonRows.Resolve(widths, 3, 2f, 70f, false, rows), Is.EqualTo(2));
            Assert.That(rows, Is.EqualTo(new[] { 0, 0, 1 }));
        }

        /// <summary>首格不计间距——否则窄面板里第一格会被无端挤到第二行。</summary>
        [Test]
        public void 首格不计间距()
        {
            var widths = new[] { 50f, 50f };
            var rows = new int[2];

            // 50 + 2 + 50 = 102 > 100，但 50 ≤ 100，故第一格一定在第一行。
            Assert.That(ResponsiveButtonRows.Resolve(widths, 2, 2f, 100f, false, rows), Is.EqualTo(2));
            Assert.That(rows[0], Is.EqualTo(0));
            Assert.That(rows[1], Is.EqualTo(1));
        }

        /// <summary>超宽的格子独占一行，而不是被截断。</summary>
        [Test]
        public void 超宽的格子独占一行()
        {
            var widths = new[] { 300f, 30f };
            var rows = new int[2];

            Assert.That(ResponsiveButtonRows.Resolve(widths, 2, 2f, 100f, false, rows), Is.EqualTo(2));
            Assert.That(rows, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(widths[0], Is.EqualTo(300f), "宽度不该被裁——裁了按钮上的字就被切掉了。");
        }

        /// <summary>等宽模式把所有格子统一成最宽的那一格，并就地改写宽度。</summary>
        [Test]
        public void 等宽模式统一成最宽()
        {
            var widths = new[] { 20f, 50f, 30f };
            var rows = new int[3];

            Assert.That(ResponsiveButtonRows.Resolve(widths, 3, 0f, 60f, true, rows), Is.EqualTo(3));
            Assert.That(widths, Is.EqualTo(new[] { 50f, 50f, 50f }));
        }

        /// <summary>没有格子时返回一行——调用方不必为「空组」写特例。</summary>
        [Test]
        public void 空组返回一行()
        {
            Assert.That(ResponsiveButtonRows.Resolve(new float[0], 0, 2f, 100f, false, new int[0]), Is.EqualTo(1));
        }

        #endregion

        #region Private Helpers

        /// <summary>建一棵树。</summary>
        /// <returns>属性树。</returns>
        private PropertyTree Build()
        {
            return PropertyTree.Create(new SerializedObject(_target));
        }

        /// <summary>按路径找根下的分组节点。</summary>
        /// <param name="tree">属性树。</param>
        /// <param name="path">分组路径。</param>
        /// <returns>分组节点；找不到时返回 <c>null</c>。</returns>
        private static InspectorProperty GroupNode(PropertyTree tree, string path)
        {
            foreach (var child in tree.Root.Children)
            {
                if (child.Kind == InspectorPropertyKind.Group && child.Path == path)
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>取根下子节点的路径。</summary>
        /// <param name="tree">属性树。</param>
        /// <returns>路径列表。</returns>
        private static List<string> PathsOfRoot(PropertyTree tree)
        {
            return PathsOf(tree.Root);
        }

        /// <summary>取某个节点下子节点的路径。</summary>
        /// <param name="node">节点。</param>
        /// <returns>路径列表。</returns>
        private static List<string> PathsOf(InspectorProperty node)
        {
            var paths = new List<string>();
            foreach (var child in node.Children)
            {
                paths.Add(child.Path);
            }

            return paths;
        }

        #endregion
    }

    /// <summary>按钮分组测试用的资产。</summary>
    internal sealed class ButtonGroupFixture : ScriptableObject
    {
        /// <summary>默认组里的第一个。</summary>
        [ButtonGroup]
        [Button]
        private void Alpha()
        {
        }

        /// <summary>默认组里的第二个。</summary>
        [ButtonGroup]
        [Button]
        private void Beta()
        {
        }

        /// <summary>不属于任何组。</summary>
        [Button]
        private void Loose()
        {
        }

        /// <summary>响应式默认组里的第一个。</summary>
        [ResponsiveButtonGroup]
        [Button]
        private void Gamma()
        {
        }

        /// <summary>响应式默认组里的第二个。</summary>
        [ResponsiveButtonGroup]
        [Button]
        private void Delta()
        {
        }

        /// <summary>只在一半成员上声明等宽——合并规则该让整个组都等宽。</summary>
        [ResponsiveButtonGroup("等宽组", UniformLayout = true)]
        [Button]
        private void UniformOne()
        {
        }

        /// <summary>同一个组的另一半，不带等宽设定。</summary>
        [ResponsiveButtonGroup("等宽组")]
        [Button]
        private void UniformTwo()
        {
        }
    }
}
