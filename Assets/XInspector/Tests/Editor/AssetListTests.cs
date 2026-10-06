using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[AssetList]</c> 的**构建期**：形态判定、模型、容器注入、降级与同现规则。
    /// <para>
    /// 不测 IMGUI——断言的是「模型里有什么、链上有没有它、注入了没有」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AssetListTests
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

        #region 形态与模型

        /// <summary>列表形态：注入列表容器（画法与增删归它），模型说清形态与元素类型。</summary>
        [Test]
        public void 列表形态_注入容器并建模型()
        {
            var target = ScriptableObject.CreateInstance<AssetListFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "materials");

                Assert.That(
                    IndexOf<ListDrawerSettingsDrawer>(node),
                    Is.GreaterThanOrEqualTo(0),
                    "列表的画法与增删由集合容器提供——不注入就没人画。");

                var model = node.State.Get<AssetListModel>();
                Assert.That(model, Is.Not.Null);
                Assert.That(model.Form, Is.EqualTo(AssetListForm.List));
                Assert.That(model.ElementType, Is.EqualTo(typeof(Material)));
                Assert.That(model.TypeFilter, Is.EqualTo("t:Material"));
                Assert.That(model.Folders, Is.Empty, "没写 Path 表示整个工程。");
                Assert.That(model.NamePrefix, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>目录与前缀在构建期算完——绘制路径不碰字符串拼接。</summary>
        [Test]
        public void 路径与前缀在构建期预计算()
        {
            var target = ScriptableObject.CreateInstance<AssetListFixture>();
            try
            {
                var tree = BuildTree(target);
                var model = Find(tree.Root, "rocks").State.Get<AssetListModel>();

                CollectionAssert.AreEqual(
                    new[] { "Assets/Plugins/Sirenix", "Assets/Art" },
                    model.Folders,
                    "前导斜杠写入的目录要归一化成工程相对路径。");
                Assert.That(model.NamePrefix, Is.EqualTo("Rock"));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>单元素形态：**不**注入列表容器（它有自己的替换型绘制器）。</summary>
        [Test]
        public void 单元素形态_不注入容器()
        {
            var target = ScriptableObject.CreateInstance<AssetListFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "single");

                Assert.That(IndexOf<ListDrawerSettingsDrawer>(node), Is.EqualTo(-1));
                Assert.That(
                    IndexOf<AssetListDrawer>(node),
                    Is.GreaterThanOrEqualTo(0),
                    "单元素形态由它自己那一格替换型绘制器画。");

                var model = node.State.Get<AssetListModel>();
                Assert.That(model, Is.Not.Null);
                Assert.That(model.Form, Is.EqualTo(AssetListForm.Single));
                Assert.That(model.ElementType, Is.EqualTo(typeof(Texture2D)));
                Assert.That(model.TypeFilter, Is.EqualTo("t:Texture2D"));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>对照：没标特性就没有模型，外观与从前一致。</summary>
        [Test]
        public void 未标注不建模型()
        {
            var target = ScriptableObject.CreateInstance<AssetListFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(Find(tree.Root, "plain").State.Get<AssetListModel>(), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 降级与同现

        /// <summary>元素不是 <c>UnityEngine.Object</c> 的列表：告警一次，字段仍按本包列表绘制。</summary>
        [Test]
        public void 降级_非对象元素列表()
        {
            LogAssert.Expect(LogType.Warning, new Regex("不是 UnityEngine.Object 派生"));

            var target = ScriptableObject.CreateInstance<AssetListFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "numbers");

                Assert.That(node.State.Get<AssetListModel>(), Is.Null);
                Assert.That(
                    IndexOf<ListDrawerSettingsDrawer>(node),
                    Is.GreaterThanOrEqualTo(0),
                    "特性不生效也要保证「有人画」——退回本包的普通列表绘制。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>标量字段：告警一次，什么也不注入（没什么可退回的）。</summary>
        [Test]
        public void 降级_标量字段()
        {
            LogAssert.Expect(LogType.Warning, new Regex("只对数组 / List<T> 或单个 Unity 对象字段有效"));

            var target = ScriptableObject.CreateInstance<AssetListFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "scalar");

                Assert.That(node.State.Get<AssetListModel>(), Is.Null);
                Assert.That(IndexOf<ListDrawerSettingsDrawer>(node), Is.EqualTo(-1));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 与 <c>[TableList]</c> 同现（列表形态）：<c>[AssetList]</c> **让位**并告警一次，
        /// 表格保持既有行为、只注入一份列表设置。
        /// </summary>
        [Test]
        public void 同现_表格优先并告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex("已让位"));

            var target = ScriptableObject.CreateInstance<AssetListFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "both");

                Assert.That(node.State.Get<TableModel>(), Is.Not.Null, "表格照建。");
                Assert.That(node.State.Get<AssetListModel>(), Is.Null, "[AssetList] 不建模型。");
                Assert.That(CountListSettings(node), Is.EqualTo(1), "注入只该有一份。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 写入

        /// <summary>类型不符的写入被拒、**零改动**，并告警一次（不静默）。</summary>
        [Test]
        public void 写入_类型不符被拒且零改动()
        {
            LogAssert.Expect(LogType.Warning, new Regex("不是属性"));

            var target = ScriptableObject.CreateInstance<AssetListFixture>();
            var wrong = ScriptableObject.CreateInstance<AssetListProbeAsset>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "materials");
                var array = node.ValueEntry.SerializedProperty;

                Assert.That(
                    AssetListWrite.TryAppend(node, array, typeof(Material), new Object[] { wrong }),
                    Is.False,
                    "元素类型要的是 Material，给脚本资产就该被拒。");
                Assert.That(array.arraySize, Is.EqualTo(0), "被拒就是零改动。");
            }
            finally
            {
                Object.DestroyImmediate(wrong);
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>批量追加：值按顺序落进新槽，走的是集合回调那条通道。</summary>
        [Test]
        public void 写入_批量追加的值与顺序()
        {
            var target = ScriptableObject.CreateInstance<AssetListFixture>();
            var first = ScriptableObject.CreateInstance<AssetListProbeAsset>();
            var second = ScriptableObject.CreateInstance<AssetListProbeAsset>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "probes");
                var array = node.ValueEntry.SerializedProperty;

                Assert.That(
                    AssetListWrite.TryAppend(
                        node, array, typeof(AssetListProbeAsset), new Object[] { first, second }),
                    Is.True);

                Assert.That(array.arraySize, Is.EqualTo(2));
                Assert.That(array.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(first));
                Assert.That(
                    array.GetArrayElementAtIndex(1).objectReferenceValue,
                    Is.SameAs(second),
                    "顺序即写入顺序——批量长出的副本槽被逐个覆盖。");
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>空入参不产生改动（「长度真的变了」这条判据不成立）。</summary>
        [Test]
        public void 写入_空入参不产生改动()
        {
            var target = ScriptableObject.CreateInstance<AssetListFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "probes");
                var array = node.ValueEntry.SerializedProperty;

                Assert.That(
                    AssetListWrite.TryAppend(node, array, typeof(AssetListProbeAsset), Array.Empty<Object>()),
                    Is.False);
                Assert.That(array.arraySize, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
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

        /// <summary>数节点身上有几份 <c>[ListDrawerSettings]</c>（注入幂等性的守卫）。</summary>
        /// <param name="property">目标节点。</param>
        /// <returns>份数。</returns>
        private static int CountListSettings(InspectorProperty property)
        {
            var count = 0;
            var attributes = property.Attributes;

            for (var i = 0; i < attributes.Count; i++)
            {
                if (attributes[i] is ListDrawerSettingsAttribute)
                {
                    count++;
                }
            }

            return count;
        }

        #endregion
    }

    /// <summary>资产列表的对照资产：两种形态、带过滤的对照、以及三条降级 / 同现反例。</summary>
    [HideMonoScript]
    internal sealed class AssetListFixture : ScriptableObject
    {
        /// <summary>列表形态。</summary>
        [AssetList]
        public List<Material> materials = new List<Material>();

        /// <summary>目录与前缀（前导斜杠按 Odin 样例写）。</summary>
        [AssetList(Path = "/Plugins/Sirenix/|Assets/Art", AssetNamePrefix = "Rock")]
        public Texture2D[] rocks = Array.Empty<Texture2D>();

        /// <summary>单元素形态。</summary>
        [AssetList]
        public Texture2D single;

        /// <summary>对照：没标特性。</summary>
        public Material plain;

        /// <summary>降级：元素不是 UnityEngine.Object。</summary>
        [AssetList]
        public List<int> numbers = new List<int>();

        /// <summary>降级：标量字段。</summary>
        [AssetList]
        public int scalar;

        /// <summary>同现：两个容器特性都标上——[AssetList] 让位给 [TableList]。</summary>
        [AssetList]
        [TableList]
        public List<AssetListProbeAsset> both = new List<AssetListProbeAsset>();

        /// <summary>写入用例的落点（元素类型是脚本资产，测试里现造实例）。</summary>
        [AssetList]
        public List<AssetListProbeAsset> probes = new List<AssetListProbeAsset>();
    }

    /// <summary>
    /// 同现反例用的元素类型：**既是** <c>UnityEngine.Object</c> 派生（资产列表要的），
    /// **又有**可成列的序列化字段（表格要的）——只有这种类型能让两个特性同时成立。
    /// </summary>
    internal sealed class AssetListProbeAsset : ScriptableObject
    {
        /// <summary>可成列。</summary>
        public int level;

        /// <summary>可成列。</summary>
        public string label;
    }
}
