using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[ShowDrawerChain]</c>：表格内容与链逐格对应。
    /// <para>
    /// 表格是排查「谁包住谁」的工具，因此断言的重点不是文字长什么样，而是
    /// **每一格都在、顺序与链一致、触发来源标得对**。渲染不测（本仓策略）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ShowDrawerChainTests
    {
        #region Private Fields

        private ChainReportFixture _target;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ChainReportFixture>();
        }

        /// <summary>销毁临时资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            _tree = null;
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 表格内容

        /// <summary>标题给出链长。</summary>
        [Test]
        public void 标题给出链长()
        {
            var property = Find("traced");
            var expected = $"绘制器链（{property.Chain.Entries.Length} 格）";

            Assert.That(DrawerChainReport.BuildHeader(property), Is.EqualTo(expected));
        }

        /// <summary>
        /// 行数与链一一对应，逐格标出绘制器与触发它的特性。
        /// <para>
        /// 该字段叠了三个特性，链应为：ShowDrawerChain（最外，-950）→ Indent（-880）→
        /// DisplayAsString（0）→ 末端。
        /// </para>
        /// </summary>
        [Test]
        public void 逐格对应且标出触发来源()
        {
            var rows = DrawerChainReport.BuildRows(Find("traced"));

            Assert.That(rows.Length, Is.EqualTo(4), "三个特性绘制器 + 末端。");
            Assert.That(rows[0], Does.Contain("ShowDrawerChainDrawer"), "它自己是最外层，第 0 格。");
            Assert.That(rows[0], Does.Contain("-950"));
            Assert.That(rows[1], Does.Contain("IndentDrawer"));
            Assert.That(rows[2], Does.Contain("DisplayAsStringDrawer"));
            Assert.That(rows[2], Does.Contain("DisplayAsStringAttribute"), "标出是哪个特性触发的。");
            Assert.That(rows[3], Does.Contain("UnityFallbackDrawer"));
            Assert.That(rows[3], Does.Contain("末端"));
        }

        /// <summary>无特性的对照：只有末端一格，且它标为「构建期显式追加」。</summary>
        [Test]
        public void 无特性成员只有末端一格()
        {
            var rows = DrawerChainReport.BuildRows(Find("plain"));

            Assert.That(rows.Length, Is.EqualTo(1));
            Assert.That(rows[0], Does.Contain("UnityFallbackDrawer"));
            Assert.That(rows[0], Does.Contain("构建期显式追加"));
        }

        /// <summary>序号从 0 起、逐格递增——它就是「谁在外」的读法。</summary>
        [Test]
        public void 序号自外向内递增()
        {
            var rows = DrawerChainReport.BuildRows(Find("traced"));

            for (var i = 0; i < rows.Length; i++)
            {
                Assert.That(rows[i], Does.StartWith($"[{i}]"));
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>按路径取成员节点；树在首次调用时构建，同一用例内复用。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>成员节点。</returns>
        private InspectorProperty Find(string path)
        {
            if (_tree == null)
            {
                _tree = PropertyTree.Create(new SerializedObject(_target));
            }

            foreach (var child in _tree.Root.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            Assert.Fail($"找不到成员 {path}。");
            return null;
        }

        #endregion
    }

    /// <summary>绘制器链表测试用的资产。</summary>
    internal sealed class ChainReportFixture : ScriptableObject
    {
        /// <summary>三个特性叠在一起，用来验证表格逐格对应。</summary>
        [ShowDrawerChain]
        [Indent]
        [DisplayAsString]
        public int traced = 1;

        /// <summary>无特性的对照。</summary>
        public int plain;
    }
}
