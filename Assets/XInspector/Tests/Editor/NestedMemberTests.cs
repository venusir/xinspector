using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 嵌套类型成员节点化：按需展开的判据、子节点的形状与末端、以及**嵌套层的条件第一次生效**。
    /// <para>
    /// 不测 IMGUI——断言的是「有没有子节点」「路径与值入口对不对」「链尾是不是复合末端」
    /// 「条件的可见性跟不跟随」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class NestedMemberTests
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

        #region 展开判据

        /// <summary>嵌套层里有本包特性 → 展开成子节点，路径是 <c>stats.hp</c> 这样的点分路径。</summary>
        [Test]
        public void 嵌套层里的特性触发子节点()
        {
            var target = ScriptableObject.CreateInstance<NestedMemberFixture>();
            try
            {
                var tree = BuildTree(target);
                var stats = Find(tree.Root, "stats");

                Assert.That(stats.Children.Count, Is.EqualTo(3));

                // [PropertyOrder(-1)] 在嵌套层同样生效：priority 排到那一层的最前。
                Assert.That(stats.Children[0].Path, Is.EqualTo("stats.priority"));
                Assert.That(stats.Children[1].Path, Is.EqualTo("stats.alive"));
                Assert.That(stats.Children[2].Path, Is.EqualTo("stats.hp"));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>只带原生装饰器的嵌套类型不展开——「没用到本包的类型外观不变」的回归守卫。</summary>
        [Test]
        public void 未用到的嵌套类型不建子节点()
        {
            var target = ScriptableObject.CreateInstance<NestedMemberFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "native");

                Assert.That(node.Children.Count, Is.EqualTo(0));
                Assert.That(
                    node.Chain.Entries[node.Chain.Count - 1].Drawer,
                    Is.InstanceOf<UnityFallbackDrawer>(),
                    "整份仍交给 Unity——与从前逐字一致。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>判据只看**成员级**特性：嵌套类型上的类级特性不触发展开（它本轮不生效）。</summary>
        [Test]
        public void 类级特性不触发嵌套展开()
        {
            var target = ScriptableObject.CreateInstance<NestedMemberFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(Find(tree.Root, "classLevel").Children.Count, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>数组不展开（元素个数随时可变，那是元素节点化的领域）。</summary>
        [Test]
        public void 数组不展开()
        {
            var target = ScriptableObject.CreateInstance<NestedMemberFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(Find(tree.Root, "array").Children.Count, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 子节点的形状

        /// <summary>
        /// 每个子节点的值入口是**独立实例**且路径相符——守「<c>GetIterator</c> 是共享实例」
        /// 那条老坑（违反的症状是「每个字段显示的都是同一个值」）。
        /// </summary>
        [Test]
        public void 子节点的值入口独立且路径相符()
        {
            var target = ScriptableObject.CreateInstance<NestedMemberFixture>();
            try
            {
                var tree = BuildTree(target);
                var stats = Find(tree.Root, "stats");
                var first = stats.Children[0].ValueEntry.SerializedProperty;
                var second = stats.Children[1].ValueEntry.SerializedProperty;

                Assert.That(first.propertyPath, Is.EqualTo(stats.Children[0].Path));
                Assert.That(second.propertyPath, Is.EqualTo(stats.Children[1].Path));
                Assert.That(first, Is.Not.SameAs(second));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>嵌套子节点也要能回到所属的树（<c>Owner</c> 递归回填）——否则条件解析会落空。</summary>
        [Test]
        public void 每个嵌套节点都能回到所属的树()
        {
            var target = ScriptableObject.CreateInstance<NestedMemberFixture>();
            try
            {
                var tree = BuildTree(target);
                var stats = Find(tree.Root, "stats");

                Assert.That(stats.Owner, Is.SameAs(tree));

                foreach (var child in stats.Children)
                {
                    Assert.That(child.Owner, Is.SameAs(tree), $"「{child.Path}」的 Owner 没回填。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>展开过的成员接复合末端，没展开的仍是值末端（接错就是「子字段画两遍」）。</summary>
        [Test]
        public void 复合成员的末端是复合末端()
        {
            var target = ScriptableObject.CreateInstance<NestedMemberFixture>();
            try
            {
                var tree = BuildTree(target);
                var stats = Find(tree.Root, "stats");
                var hp = Find(stats, "stats.hp");

                Assert.That(
                    stats.Chain.Entries[stats.Chain.Count - 1].Drawer,
                    Is.InstanceOf<CompositeMemberTerminalDrawer>());
                Assert.That(
                    hp.Chain.Entries[hp.Chain.Count - 1].Drawer,
                    Is.InstanceOf<UnityFallbackDrawer>());
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 嵌套层的条件

        /// <summary>嵌套层里的 <c>[ShowIf]</c> 第一次生效：条件跟着同级成员走。</summary>
        [Test]
        public void 嵌套层里的条件跟随同级成员()
        {
            var target = ScriptableObject.CreateInstance<NestedMemberFixture>();
            try
            {
                var tree = BuildTree(target);
                var hp = Find(Find(tree.Root, "stats"), "stats.hp");

                Assert.That(hp.IsVisible, Is.True, "同级 alive 为真。");

                SetBool(target, tree, "stats.alive", false);
                Assert.That(hp.IsVisible, Is.False, "同级 alive 为假——条件指的是**同层**的 alive。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 同名成员在根与嵌套层各有一个时，**取同级的那个**（先同级后根）。
        /// </summary>
        [Test]
        public void 嵌套层的条件优先解析同级()
        {
            var target = ScriptableObject.CreateInstance<NestedMemberFixture>();
            try
            {
                var tree = BuildTree(target);
                var hp = Find(Find(tree.Root, "stats"), "stats.hp");

                // 根上的 alive 为真、嵌套层的为假——若解析到了根上，hp 就会显示。
                Assert.That(
                    hp.IsVisible,
                    Is.True,
                    "起点：两层都真。");

                SetBool(target, tree, "stats.alive", false);

                Assert.That(hp.IsVisible, Is.False, "同级的 alive 才是条件指的那个。");
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

        /// <summary>改一个 bool 字段的值，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树——条件求值器读的是**树自己的**序列化对象。</param>
        /// <param name="path">序列化路径（可点分）。</param>
        /// <param name="value">新值。</param>
        private static void SetBool(ScriptableObject target, PropertyTree tree, string path, bool value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
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

        #endregion
    }

    /// <summary>嵌套层里带本包特性的类型——它会被展开成子节点。</summary>
    [Serializable]
    internal class NestedStats
    {
        /// <summary>条件开关（**同层**的成员）。</summary>
        public bool alive = true;

        /// <summary>带条件的成员——嵌套层的特性第一次生效。</summary>
        [ShowIf(nameof(alive))]
        public int hp = 10;

        /// <summary>带顺序的成员——嵌套层的排序也生效。</summary>
        [PropertyOrder(-1f)]
        public int priority;
    }

    /// <summary>只带原生装饰器的嵌套类型——不该被展开。</summary>
    [Serializable]
    internal class NativeOnlyNested
    {
        /// <summary>Unity 自己的装饰器（写全名：NUnit 也有一个 <c>[Range]</c>）。</summary>
        [UnityEngine.Range(0f, 1f)]
        public float ratio;
    }

    /// <summary>类级特性标在类型上的嵌套类型——同样不该触发（判据只看成员级特性）。</summary>
    [Serializable]
    [Title("嵌套层的类级特性不生效")]
    internal class ClassLevelNested
    {
        /// <summary>普通字段。</summary>
        public int plain;
    }

    /// <summary>嵌套节点化的对照资产。</summary>
    [HideMonoScript]
    internal sealed class NestedMemberFixture : ScriptableObject
    {
        /// <summary>根上的同名开关——用来验证「先同级后根」。</summary>
        public bool alive = true;

        /// <summary>嵌套层带本包特性 → 展开。</summary>
        public NestedStats stats = new NestedStats();

        /// <summary>只带原生装饰器 → 不展开。</summary>
        public NativeOnlyNested native;

        /// <summary>类级特性不算 → 不展开。</summary>
        public ClassLevelNested classLevel;

        /// <summary>数组不展开。</summary>
        public NestedStats[] array;
    }
}
