using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[TableList]</c>：注入、构建期列模型与列宽分配。
    /// <para>
    /// 不测 IMGUI——断言的是「特性在不在节点上」「模型里有哪些列」「宽度怎么分」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class TableListTests
    {
        #region Fixture

        /// <summary>复位静态门面：建树会初始化绘制器与处理器两张注册表。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 注入

        /// <summary>单独标 <c>[TableList]</c> 也由列表绘制器接管——构建期注入了一份列表设置。</summary>
        [Test]
        public void 单独标记时注入列表设置()
        {
            var target = ScriptableObject.CreateInstance<TableFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "rows");

                Assert.That(node.Attributes.Has<ListDrawerSettingsAttribute>(), Is.True, "不注入就没人画这张表。");
                Assert.That(IndexOf<ListDrawerSettingsDrawer>(node), Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>注入的是**各字段各一份**的新实例（共享一份就是「改一处串一片」）。</summary>
        [Test]
        public void 注入的实例互不共享()
        {
            var target = ScriptableObject.CreateInstance<TableFixture>();
            try
            {
                var tree = BuildTree(target);
                var first = Find(tree.Root, "rows").Attributes.Get<ListDrawerSettingsAttribute>();
                var second = Find(tree.Root, "plain").Attributes.Get<ListDrawerSettingsAttribute>();

                Assert.That(first, Is.Not.SameAs(second));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>自己标了列表设置就不再注入（<c>AllowMultiple = false</c>）。</summary>
        [Test]
        public void 显式列表设置不再注入()
        {
            var target = ScriptableObject.CreateInstance<TableFixture>();
            try
            {
                var tree = BuildTree(target);
                var attributes = Find(tree.Root, "explicitSettings").Attributes;
                var count = 0;

                for (var i = 0; i < attributes.Count; i++)
                {
                    if (attributes[i] is ListDrawerSettingsAttribute)
                    {
                        count++;
                    }
                }

                Assert.That(count, Is.EqualTo(1), "两份会使同一格配两次绘制器。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 列模型

        /// <summary>列按声明顺序、<c>[HideInTables]</c> 不进列、列宽来自 <c>[TableColumnWidth]</c>。</summary>
        [Test]
        public void 列模型_顺序隐藏与列宽()
        {
            var target = ScriptableObject.CreateInstance<TableFixture>();
            try
            {
                var tree = BuildTree(target);
                var model = Find(tree.Root, "rows").State.Get<TableModel>();

                Assert.That(model, Is.Not.Null);
                Assert.That(NamesOf(model), Is.EqualTo(new[] { "id", "name" }), "memo 标了 [HideInTables]。");
                Assert.That(model.Columns[0].Width, Is.EqualTo(60f), "固定宽来自 [TableColumnWidth(60)]。");
                Assert.That(model.Columns[1].Width, Is.EqualTo(0f), "不标列宽 = 弹性。");
                Assert.That(model.ShowIndexLabels, Is.True, "旋钮随模型带过来。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>空列表也能建模型，顺序走反射（基类的列在前）。</summary>
        [Test]
        public void 列模型_空列表用反射顺序且基类在前()
        {
            var target = ScriptableObject.CreateInstance<TableFixture>();
            try
            {
                var tree = BuildTree(target);
                var model = Find(tree.Root, "children").State.Get<TableModel>();

                Assert.That(model, Is.Not.Null);
                Assert.That(NamesOf(model), Is.EqualTo(new[] { "id", "name", "extra" }));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 元素没有可成列的成员时表格不成立：不给模型（绘制器据此退回列表形态），
        /// 且构建期就把原因说清楚。
        /// </summary>
        [Test]
        public void 列模型_非复合元素类型为空()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("\\[TableList\\] 无法生效"));

            var target = ScriptableObject.CreateInstance<TableFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "numbers");

                Assert.That(node.State.Get<TableModel>(), Is.Null);
                Assert.That(
                    node.Attributes.Has<ListDrawerSettingsAttribute>(),
                    Is.True,
                    "仍然要保证有绘制器——退回列表形态，而不是没人画。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 列宽分配

        /// <summary>固定宽原样、弹性列均分、序号列固定宽——纯数学，可无头断言。</summary>
        [Test]
        public void 列宽分配_固定与弹性()
        {
            var columns = new[]
            {
                new TableColumn { Name = "a", Width = 60f },
                new TableColumn { Name = "b", Width = 0f },
                new TableColumn { Name = "c", Width = 0f },
            };

            var widths = TableLayout.AllocateWidths(300f, columns, indexColumn: true);

            Assert.That(widths.Length, Is.EqualTo(4));
            Assert.That(widths[0], Is.EqualTo(24f), "序号列固定宽。");
            Assert.That(widths[1], Is.EqualTo(60f), "固定列原样。");
            Assert.That(widths[2], Is.EqualTo(widths[3]), "弹性列均分。");
            Assert.That(widths[2], Is.EqualTo((300f - 60f - 24f) / 2f));
        }

        /// <summary>全都固定宽时没有弹性列，多余宽度不被瓜分。</summary>
        [Test]
        public void 列宽分配_全固定()
        {
            var columns = new[] { new TableColumn { Name = "a", Width = 50f }, new TableColumn { Name = "b", Width = 70f } };

            var widths = TableLayout.AllocateWidths(300f, columns, indexColumn: false);

            Assert.That(widths, Is.EqualTo(new[] { 50f, 70f }));
        }

        /// <summary>列多到装不下时弹性列缩到下限为止，不出负数。</summary>
        [Test]
        public void 列宽分配_装不下时不缩成负数()
        {
            var columns = new[]
            {
                new TableColumn { Name = "a", Width = 0f },
                new TableColumn { Name = "b", Width = 0f },
            };

            var widths = TableLayout.AllocateWidths(10f, columns, indexColumn: false);

            Assert.That(widths[0], Is.GreaterThan(0f));
            Assert.That(widths[1], Is.GreaterThan(0f));
        }

        #endregion

        #region Private Helpers

        /// <summary>构建被测的树。</summary>
        /// <param name="target">目标资产。</param>
        /// <returns>属性树。</returns>
        private static PropertyTree BuildTree(ScriptableObject target)
        {
            return PropertyTree.Create(new SerializedObject(target));
        }

        /// <summary>按路径查找直接子节点。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；不存在时断言失败。</returns>
        private static InspectorProperty Find(InspectorProperty parent, string path)
        {
            foreach (var child in parent.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            Assert.Fail($"找不到节点 {path}。");
            return null;
        }

        /// <summary>在节点的链上查找指定类型绘制器的下标。</summary>
        /// <typeparam name="T">绘制器类型。</typeparam>
        /// <param name="property">目标节点。</param>
        /// <returns>下标；不存在返回 -1。</returns>
        private static int IndexOf<T>(InspectorProperty property) where T : XInspectorDrawer
        {
            var entries = property.Chain.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Drawer is T)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>取模型的列名。</summary>
        /// <param name="model">表格模型。</param>
        /// <returns>列名数组。</returns>
        private static string[] NamesOf(TableModel model)
        {
            var names = new string[model.Columns.Length];
            for (var i = 0; i < model.Columns.Length; i++)
            {
                names[i] = model.Columns[i].Name;
            }

            return names;
        }

        #endregion
    }

    /// <summary>表格的对照行类型。</summary>
    [Serializable]
    internal class TableRow
    {
        /// <summary>固定宽的列。</summary>
        [TableColumnWidth(60)]
        public int id;

        /// <summary>弹性宽度的列。</summary>
        public string name = "行";

        /// <summary>不进表格的成员。</summary>
        [HideInTables]
        public string memo = "备注";
    }

    /// <summary>派生行类型——用来验证「基类的列在前」。</summary>
    [Serializable]
    internal class TableRowChild : TableRow
    {
        /// <summary>派生类自己的列。</summary>
        public float extra;
    }

    /// <summary>表格的对照资产。</summary>
    [HideMonoScript]
    internal sealed class TableFixture : ScriptableObject
    {
        /// <summary>普通表格。</summary>
        [TableList(ShowIndexLabels = true)]
        public List<TableRow> rows = new List<TableRow> { new TableRow() };

        /// <summary>空表格 + 派生元素（走反射顺序）。</summary>
        [TableList]
        public List<TableRowChild> children = new List<TableRowChild>();

        /// <summary>元素没有可成列的成员——降级。</summary>
        [TableList]
        public List<int> numbers = new List<int> { 1, 2 };

        /// <summary>自己带了列表设置——不该被再注入一份。</summary>
        [TableList]
        [ListDrawerSettings(HideAddButton = true)]
        public TableRow[] explicitSettings = { new TableRow() };

        /// <summary>另一个表格（注入实例互不共享的对照）。</summary>
        [TableList]
        public TableRow[] plain;
    }
}
