using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[AssetList]</c> 的拖放**判定**（纯逻辑）：接受哪些、拒哪些、为什么。
    /// <para>
    /// 事件接线（<c>DragAndDrop</c> / <c>visualMode</c>）是 IMGUI，本仓不测——
    /// 这里测的是它调用的那个决策函数。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AssetListDropTests
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

        #region 判定

        /// <summary>接受工程资产，且**保持拖入顺序**。</summary>
        [Test]
        public void 拖放_接受工程资产并保持顺序()
        {
            var target = ScriptableObject.CreateInstance<AssetListDropFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "anything");
                var accepted = new List<Object>();

                var first = LoadSource("AssetListDrop.cs");
                var second = LoadSource("AssetListWrite.cs");

                var plan = AssetListDrop.Build(
                    new[] { first, second }, typeof(Object), node.ValueEntry.SerializedProperty, accepted);

                Assert.That(plan.Accepted, Is.EqualTo(2));
                Assert.That(plan.HasAny, Is.True);
                Assert.That(plan.TotalRejected, Is.EqualTo(0));
                Assert.That(accepted, Has.Count.EqualTo(2));
                Assert.That(accepted[0], Is.SameAs(first));
                Assert.That(accepted[1], Is.SameAs(second), "顺序即拖入顺序。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>类型不符的被拒并计数——不静默。</summary>
        [Test]
        public void 拖放_类型不符被拒()
        {
            var target = ScriptableObject.CreateInstance<AssetListDropFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "materials");
                var accepted = new List<Object>();

                var plan = AssetListDrop.Build(
                    new[] { LoadSource("AssetListDrop.cs") },
                    typeof(Material),
                    node.ValueEntry.SerializedProperty,
                    accepted);

                Assert.That(plan.Accepted, Is.EqualTo(0));
                Assert.That(plan.RejectedByType, Is.EqualTo(1));
                Assert.That(accepted, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>已在列表里的被拒（本包自定语义：去重）。</summary>
        [Test]
        public void 拖放_已在列表被拒()
        {
            var target = ScriptableObject.CreateInstance<AssetListDropFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "anything");
                var array = node.ValueEntry.SerializedProperty;
                var existing = LoadSource("AssetListDrop.cs");

                array.arraySize = 1;
                array.GetArrayElementAtIndex(0).objectReferenceValue = existing;

                var accepted = new List<Object>();
                var plan = AssetListDrop.Build(new[] { existing }, typeof(Object), array, accepted);

                Assert.That(plan.Accepted, Is.EqualTo(0));
                Assert.That(plan.RejectedDuplicate, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>场景对象（非工程资产）被拒——那是 <c>[AssetList]</c> 之外的语义。</summary>
        [Test]
        public void 拖放_不是工程资产被拒()
        {
            var target = ScriptableObject.CreateInstance<AssetListDropFixture>();
            var transient = ScriptableObject.CreateInstance<AssetListProbeAsset>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "anything");
                var accepted = new List<Object>();

                var plan = AssetListDrop.Build(
                    new[] { transient }, typeof(Object), node.ValueEntry.SerializedProperty, accepted);

                Assert.That(plan.Accepted, Is.EqualTo(0));
                Assert.That(plan.RejectedNotAsset, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(transient);
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>载荷里同一个对象出现两次只收一个（第二次算重复）。</summary>
        [Test]
        public void 拖放_载荷内重复只收一个()
        {
            var target = ScriptableObject.CreateInstance<AssetListDropFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "anything");
                var accepted = new List<Object>();
                var asset = LoadSource("AssetListDrop.cs");

                var plan = AssetListDrop.Build(
                    new[] { asset, asset }, typeof(Object), node.ValueEntry.SerializedProperty, accepted);

                Assert.That(plan.Accepted, Is.EqualTo(1));
                Assert.That(plan.RejectedDuplicate, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>空载荷什么都不做；载荷里的空引用直接跳过（不计入拒绝）。</summary>
        [Test]
        public void 拖放_空载荷与空引用()
        {
            var target = ScriptableObject.CreateInstance<AssetListDropFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "anything");
                var array = node.ValueEntry.SerializedProperty;
                var accepted = new List<Object>();

                var empty = AssetListDrop.Build(
                    System.Array.Empty<Object>(), typeof(Object), array, accepted);
                Assert.That(empty.HasAny, Is.False);
                Assert.That(empty.TotalRejected, Is.EqualTo(0));

                var withNull = AssetListDrop.Build(
                    new[] { null, LoadSource("AssetListDrop.cs") }, typeof(Object), array, accepted);
                Assert.That(withNull.Accepted, Is.EqualTo(1));
                Assert.That(withNull.TotalRejected, Is.EqualTo(0), "空引用不算「被拒」。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>三种拒绝原因各说各的（告警文案的原料）。</summary>
        [Test]
        public void 拒绝原因描述()
        {
            var described = AssetListDrop.DescribeRejections(new AssetListDrop.Plan(0, 1, 2, 3));

            Assert.That(described, Does.Contain("类型不符 1"));
            Assert.That(described, Does.Contain("重复 2"));
            Assert.That(described, Does.Contain("不是工程资产"));
            Assert.That(described, Does.Contain("3"));
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

        /// <summary>
        /// 从包内源码文件取一件**工程资产**（<c>.cs</c> 是 <c>MonoScript</c>——稳定存在、
        /// 不必往盘上写夹具）。
        /// </summary>
        /// <param name="fileName">Internal 目录下的文件名。</param>
        /// <returns>资产；取不到时断言失败。</returns>
        private static Object LoadSource(string fileName)
        {
            var path = "Assets/XInspector/Editor/Internal/" + fileName;
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);

            Assert.That(asset, Is.Not.Null, $"取不到资产 {path}。");
            return asset;
        }

        #endregion
    }

    /// <summary>拖放判定的对照资产：一个「什么都收」的列表与一个 Material 列表。</summary>
    [HideMonoScript]
    internal sealed class AssetListDropFixture : ScriptableObject
    {
        /// <summary>元素类型是基类——什么工程资产都合型（拖放用例的主战场）。</summary>
        [AssetList]
        public List<Object> anything = new List<Object>();

        /// <summary>元素类型具体——用来验「类型不符被拒」。</summary>
        [AssetList]
        public List<Material> materials = new List<Material>();
    }
}
