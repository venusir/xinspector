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

        /// <summary>
        /// **没被本包接管**的数组不建元素节点。
        /// </summary>
        /// <remarks>
        /// 2026-10-06 随元素节点化改名并改口径：数组**能**节点化了，但按需——元素类型用到本包
        /// **且**这个集合被本包接管（有 <c>[ListDrawerSettings]</c> 一类）时才会。
        /// 这个夹具的 <c>array</c> 字段一个特性都没标，因此仍整份交给 Unity；
        /// 「用到本包的集合建元素层」那一半在 <c>CollectionElementNodeTests</c>。
        /// </remarks>
        [Test]
        public void 未接管的数组不建元素节点()
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

        /// <summary>
        /// 多态引用（<c>[SerializeReference]</c>）不展开——那是 L7 那条产品线。
        /// </summary>
        /// <remarks>
        /// 字段的类型与下面那个会展开的 <c>stats</c> **一模一样**：不展开只可能是因为多态这一条，
        /// 不是因为「这个类型里没有本包特性」。
        /// </remarks>
        [Test]
        public void 多态引用不展开()
        {
            var target = ScriptableObject.CreateInstance<NestedMemberFixture>();
            try
            {
                var tree = BuildTree(target);
                var payload = Find(tree.Root, "payload");

                Assert.That(payload.Children.Count, Is.EqualTo(0), "多态引用的成员进不了树。");
                Assert.That(
                    payload.Chain.Entries[payload.Chain.Count - 1].Drawer,
                    Is.InstanceOf<UnityFallbackDrawer>(),
                    "整份交给 Unity——与「没用到本包的类型外观不变」同款。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 测量：<c>[SerializeReference]</c> 的字段在序列化属性上是 <c>ManagedReference</c>，
        /// 因此本来就过不了展开判据里「要求 Generic」那一关。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 记下这条是为了说明「另一条判据为什么长年没生效也没出事」：展开判据里那句
        /// 「<c>[SerializeReference]</c> 不展开」原先写成看**类型**，而那个特性的用法声明是
        /// <c>AttributeTargets.Field</c>——**恒为假**。2026-10-06 改成看字段，从此两道闸
        /// 说的是同一件事。
        /// </para>
        /// <para>
        /// Unity 哪天换了行为这条先红：那时字段判据就是唯一的那道闸。
        /// </para>
        /// </remarks>
        [Test]
        public void 多态引用的序列化属性是托管引用()
        {
            var target = ScriptableObject.CreateInstance<NestedMemberFixture>();
            try
            {
                var serializedObject = new SerializedObject(target);
                var property = serializedObject.FindProperty("payload");

                Assert.That(property, Is.Not.Null, "多态引用照样在序列化数据里（否则判据根本见不到它）。");
                Assert.That(property.propertyType, Is.EqualTo(SerializedPropertyType.ManagedReference));
                Assert.That(property.isArray, Is.False);
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

        #region 嵌套层的成员引用

        /// <summary>
        /// 按名找成员的特性（<c>[ValueDropdown]</c> 的数据源、<c>[MinMaxSlider]</c> 的边界、
        /// <c>[ToggleGroup]</c> 的开关）在嵌套层**先找同层、再回落根上的绝对名**。
        /// </summary>
        /// <remarks>
        /// 与条件族同一条口径。不这么做的话，嵌套层里写 <c>[ValueDropdown("options")]</c>
        /// 会**静默地**绑定到根上的同名成员——取的是另一个对象的值，极难归因。
        /// </remarks>
        [Test]
        public void 嵌套层的成员引用优先解析同级()
        {
            var target = ScriptableObject.CreateInstance<NestedMemberFixture>();
            try
            {
                var tree = BuildTree(target);
                var picked = Find(Find(tree.Root, "valueSource"), "valueSource.picked");
                var state = picked.State.Get<ValueDropdownState>();

                Assert.That(state, Is.Not.Null, "构建期应当已经解析过选项来源。");
                Assert.That(state.Resolved, Is.True);
                Assert.That(state.Source, Is.Not.Null);
                Assert.That(
                    state.Source.propertyPath,
                    Is.EqualTo("valueSource.options"),
                    "嵌套层里的成员引用指的是**同层**的 options，不是根上的同名成员。");
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

        /// <summary>
        /// 多态引用不展开（L7 那条产品线）。类型与上面那个**会展开**的 <c>stats</c> 相同——
        /// 于是这条用例钉住的只可能是「多态」这一个理由。
        /// </summary>
        [SerializeReference]
        public NestedStats payload;

        /// <summary>根上的同名选项来源——用来验证「先同级后根」。</summary>
        public int[] options = { 9 };

        /// <summary>嵌套层里带成员引用的复合字段。</summary>
        public NestedValueSource valueSource = new NestedValueSource();
    }

    /// <summary>嵌套层里的成员引用：同层与根上各有一个同名来源。</summary>
    [Serializable]
    internal class NestedValueSource
    {
        /// <summary>同层的选项来源（与根上那个同名、内容不同）。</summary>
        public int[] options = { 1, 2, 3 };

        /// <summary>成员引用应解析到**同层**的 <c>options</c>。</summary>
        [ValueDropdown(nameof(options))]
        public int picked;
    }
}
