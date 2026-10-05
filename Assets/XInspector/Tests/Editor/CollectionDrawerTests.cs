using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[ListDrawerSettings]</c>：链上占值档位、降级判定，以及**增删配方**的两条单元守卫
    /// （本仓第一次做结构性修改，两处 Unity 经典坑都要有用例钉着）。
    /// <para>
    /// 不测 IMGUI——断言的是「链上有没有它、在第几位」与「增删之后数组长什么样」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class CollectionDrawerTests
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

        #region 链

        /// <summary>标了列表设置的数组，链上有集合绘制器，且排在末端之前。</summary>
        [Test]
        public void 标了列表设置的数组链上有集合绘制器()
        {
            var target = ScriptableObject.CreateInstance<CollectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "numbers");
                var index = IndexOf<ListDrawerSettingsDrawer>(node);

                Assert.That(index, Is.GreaterThanOrEqualTo(0));
                Assert.That(index, Is.LessThan(node.Chain.Count - 1), "末端永远是链上最后一格。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>未标注的数组链上没有它——外观与原生逐像素一致的默认路径不受影响。</summary>
        [Test]
        public void 未标注的数组链上不出现集合绘制器()
        {
            var target = ScriptableObject.CreateInstance<CollectionFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(IndexOf<ListDrawerSettingsDrawer>(Find(tree.Root, "plain")), Is.EqualTo(-1));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 降级判定

        /// <summary>纯函数：数组与 List 可自绘；简单类型、字符串、反射成员都不行。</summary>
        [Test]
        public void 降级判定()
        {
            var target = ScriptableObject.CreateInstance<CollectionFixture>();
            try
            {
                var serializedObject = new SerializedObject(target);

                Assert.That(CollectionDrawerLayout.CanDraw(serializedObject.FindProperty("numbers")), Is.True);
                Assert.That(
                    CollectionDrawerLayout.CanDraw(serializedObject.FindProperty("list")),
                    Is.True,
                    "List 形态同样可以。");
                Assert.That(
                    CollectionDrawerLayout.CanDraw(serializedObject.FindProperty("notACollection")),
                    Is.False);
                Assert.That(
                    CollectionDrawerLayout.CanDraw(serializedObject.FindProperty("text")),
                    Is.False,
                    "字符串被 Unity 在若干语境下视作数组，要单独排除。");
                Assert.That(CollectionDrawerLayout.CanDraw(null), Is.False, "反射成员没有序列化后端。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 增删配方

        /// <summary>
        /// 追加**复用前一个元素的值**——这是 Unity 自己的语义（官方手册：新增元素会复用前一个
        /// 元素的值），原生「+」按钮亦然，本包保持逐字一致。Odin 有个「不复制」的旋钮，本包不做
        /// （要它就得给任意元素类型造默认值写入器），写进已知限制。
        /// </summary>
        [Test]
        public void 追加_新元素复用前一个元素的值()
        {
            var target = ScriptableObject.CreateInstance<CollectionFixture>();
            try
            {
                var serializedObject = new SerializedObject(target);
                var numbers = serializedObject.FindProperty("numbers");
                numbers.arraySize = 2;
                numbers.GetArrayElementAtIndex(0).intValue = 7;
                numbers.GetArrayElementAtIndex(1).intValue = 8;

                CollectionMutation.Add(numbers);

                Assert.That(numbers.arraySize, Is.EqualTo(3));
                Assert.That(numbers.GetArrayElementAtIndex(0).intValue, Is.EqualTo(7), "原有元素不受影响。");
                Assert.That(
                    numbers.GetArrayElementAtIndex(2).intValue,
                    Is.EqualTo(8),
                    "新元素是末元素的副本——Unity 的语义，与原生「+」一致（Odin 默认不复制，本包不做那个旋钮）。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>空列表上追加得到默认值（没有「前一个元素」可复用）。</summary>
        [Test]
        public void 追加_空列表得到默认值()
        {
            var target = ScriptableObject.CreateInstance<CollectionFixture>();
            try
            {
                var serializedObject = new SerializedObject(target);
                var numbers = serializedObject.FindProperty("numbers");
                numbers.arraySize = 0;

                CollectionMutation.Add(numbers);

                Assert.That(numbers.arraySize, Is.EqualTo(1));
                Assert.That(numbers.GetArrayElementAtIndex(0).intValue, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 删除**只删一次**并真的删掉。老文档里「引用类型要删两次」的做法在 2021.2 之后已不适用，
        /// 照抄会多删一个元素——本包下限 6000.3，所以这里钉的是「一次就够」。
        /// </summary>
        [Test]
        public void 删除_引用类型一次删净()
        {
            var target = ScriptableObject.CreateInstance<CollectionFixture>();
            try
            {
                var serializedObject = new SerializedObject(target);
                var texts = serializedObject.FindProperty("texts");

                var removed = CollectionMutation.TryRemove(texts, 1);

                Assert.That(removed, Is.True, "删掉了要如实返回 true（调用方据此决定要不要告警）。");
                Assert.That(texts.arraySize, Is.EqualTo(2), "长度要减一；只清引用那种老行为会留下长度不变。");
                Assert.That(texts.GetArrayElementAtIndex(1).stringValue, Is.EqualTo("c"), "后面的元素顶上来了。");
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

        #endregion
    }

    /// <summary>集合绘制的对照资产。</summary>
    [HideMonoScript]
    internal sealed class CollectionFixture : ScriptableObject
    {
        /// <summary>标了列表设置的数组。</summary>
        [ListDrawerSettings]
        public int[] numbers = { 1, 2, 3 };

        /// <summary>未标注的对照——走原生的数组绘制。</summary>
        public int[] plain = { 4, 5 };

        /// <summary>标在非集合上：绘制期降级（判定为假）。</summary>
        [ListDrawerSettings]
        public int notACollection = 1;

        /// <summary>字符串不是集合。</summary>
        [ListDrawerSettings]
        public string text = "abc";

        /// <summary>List 形态同样可自绘。</summary>
        [ListDrawerSettings]
        public List<int> list = new List<int> { 1 };

        /// <summary>引用类型数组——删除配方的对照（第一下只清引用）。</summary>
        [ListDrawerSettings]
        public string[] texts = { "a", "b", "c" };
    }
}
