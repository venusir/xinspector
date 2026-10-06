using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[InlineProperty]</c>（**观感派**）：链上占值档位、不产生子节点、类级注入与降级判定。
    /// <para>
    /// 不测 IMGUI——断言的是「链上有没有它、在第几位」「特性在不在节点上」「纯函数返回什么」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class InlinePropertyTests
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

        #region 内联绘制器

        /// <summary>标了内联的复合成员链上有内联绘制器，且排在末端之前。</summary>
        [Test]
        public void 成员级内联在链上占值档位()
        {
            var target = ScriptableObject.CreateInstance<InlineFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "inline");
                var index = IndexOf<InlinePropertyDrawer>(node);

                Assert.That(index, Is.GreaterThanOrEqualTo(0));
                Assert.That(index, Is.LessThan(node.Chain.Count - 1), "末端永远是链上最后一格。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>未标内联的成员链上没有它（控制项）。</summary>
        [Test]
        public void 未标注成员链上不出现内联绘制器()
        {
            var target = ScriptableObject.CreateInstance<InlineFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(IndexOf<InlinePropertyDrawer>(Find(tree.Root, "plain")), Is.EqualTo(-1));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 内联的复合成员**产生子节点**（完全体）——子字段成为真节点、身上的本包特性随之生效；
        /// 末端换成复合末端，且**折叠头由外层绘制器负责**（构建期定案的 <c>FoldoutSuppressed</c>）。
        /// </summary>
        [Test]
        public void 内联产生子节点且折叠头归外层()
        {
            var target = ScriptableObject.CreateInstance<InlineFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "inline");

                Assert.That(node.Children.Count, Is.EqualTo(2), "子字段成为真节点。");
                Assert.That(node.Children[0].Path, Is.EqualTo("inline.a"));
                Assert.That(
                    node.Chain.Entries[node.Chain.Count - 1].Drawer,
                    Is.InstanceOf<CompositeMemberTerminalDrawer>());

                var state = node.State.Get<CompositeMemberState>();
                Assert.That(state, Is.Not.Null, "状态在构建期定案。");
                Assert.That(state.FoldoutSuppressed, Is.True, "标签与内联由外层绘制器说了算，末端不画折叠头。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>向量这类**原生复合类型**不展开真节点——仍走观感派的自画路径。</summary>
        [Test]
        public void 向量走观感派不建子节点()
        {
            var target = ScriptableObject.CreateInstance<InlineFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "inlineVector");

                Assert.That(node.Children.Count, Is.EqualTo(0), "向量由原生控件整块画，本包不拆。");
                Assert.That(IndexOf<InlinePropertyDrawer>(node), Is.GreaterThanOrEqualTo(0), "但内联绘制器仍在。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 降级判定

        /// <summary>纯函数：复合类型与向量可内联；简单类型、数组、反射成员都不行。</summary>
        [Test]
        public void 降级判定()
        {
            var target = ScriptableObject.CreateInstance<InlineFixture>();
            try
            {
                var serializedObject = new SerializedObject(target);

                Assert.That(
                    InlinePropertyLayout.CanInline(serializedObject.FindProperty("inline")),
                    Is.True);
                Assert.That(
                    InlinePropertyLayout.CanInline(serializedObject.FindProperty("vector")),
                    Is.True,
                    "向量类型本来就为 x/y 暴露了子属性。");
                Assert.That(
                    InlinePropertyLayout.CanInline(serializedObject.FindProperty("simple")),
                    Is.False);
                Assert.That(
                    InlinePropertyLayout.CanInline(serializedObject.FindProperty("array")),
                    Is.False,
                    "数组与列表不内联——展开集合不是本特性的职责。");
                Assert.That(InlinePropertyLayout.CanInline(null), Is.False, "反射成员没有序列化后端。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>标签宽度：正值原样用，非正值一律按「不改全局值」处理。</summary>
        [Test]
        public void 标签宽度非正按不改处理()
        {
            Assert.That(InlinePropertyLayout.ResolveLabelWidth(60), Is.EqualTo(60f));
            Assert.That(InlinePropertyLayout.ResolveLabelWidth(0), Is.LessThanOrEqualTo(0f));
            Assert.That(InlinePropertyLayout.ResolveLabelWidth(-5), Is.LessThanOrEqualTo(0f));
        }

        #endregion

        #region 类级（标在字段的声明类型上）

        /// <summary>声明类型上标了内联 → 注入到该类型的字段上，且赶在挂链之前。</summary>
        [Test]
        public void 类级内联注入到该类型的字段()
        {
            var target = ScriptableObject.CreateInstance<InlineClassLevelFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "marked");

                Assert.That(node.Attributes.Has<InlinePropertyAttribute>(), Is.True);
                Assert.That(
                    node.Attributes.Get<InlinePropertyAttribute>().LabelWidth,
                    Is.EqualTo(60),
                    "标签宽度随注入一起带过来。");
                Assert.That(
                    IndexOf<InlinePropertyDrawer>(node),
                    Is.GreaterThanOrEqualTo(0),
                    "注入赶在挂链之前，绘制器才进得了链。");
                Assert.That(
                    Find(tree.Root, "plain").Attributes.Has<InlinePropertyAttribute>(),
                    Is.False,
                    "控制项：声明类型没标的字段不注入。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>同一类型的两个字段各拿一份**独立**实例——共享一份就是「改一处串一片」。</summary>
        [Test]
        public void 类级注入的实例互不共享()
        {
            var target = ScriptableObject.CreateInstance<InlineClassLevelFixture>();
            try
            {
                var tree = BuildTree(target);
                var first = Find(tree.Root, "marked").Attributes.Get<InlinePropertyAttribute>();
                var second = Find(tree.Root, "markedAgain").Attributes.Get<InlinePropertyAttribute>();

                Assert.That(first, Is.Not.SameAs(second));
                Assert.That(first.LabelWidth, Is.EqualTo(second.LabelWidth));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>成员自己标了就不再注入（<c>AllowMultiple = false</c>）。</summary>
        [Test]
        public void 字段自己标了就不再注入()
        {
            var target = ScriptableObject.CreateInstance<InlineClassLevelFixture>();
            try
            {
                var tree = BuildTree(target);
                var attributes = Find(tree.Root, "both").Attributes;
                var count = 0;

                for (var i = 0; i < attributes.Count; i++)
                {
                    if (attributes[i] is InlinePropertyAttribute)
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

        /// <summary>
        /// 类级内联标在**深一层**的字段类型上时，外层的容器也要跟着展开。
        /// <para>
        /// 这条原先会红：注入是真的会发生（处理器只看「父节点是不是成员」），可**展开判据**
        /// 的递归那一半只看成员级特性——外层容器不开，内层字段根本进不了树，
        /// 类级内联**静默失效**。判据与注入看的是同一处，修法是让递归判据也问这一句
        /// （<c>NestedMemberExpansion.IsClassLevelInlineMarked</c>，四处共用一份）。
        /// </para>
        /// </summary>
        [Test]
        public void 类级内联在深层嵌套里也触发展开()
        {
            var target = ScriptableObject.CreateInstance<InlineNestedHolderFixture>();
            try
            {
                var tree = BuildTree(target);
                var outer = Find(tree.Root, "outer");
                var inner = Find(outer, "outer.inner");

                Assert.That(inner.Attributes.Has<InlinePropertyAttribute>(), Is.True, "注入本身照常发生。");
                Assert.That(
                    IndexOf<InlinePropertyDrawer>(inner),
                    Is.GreaterThanOrEqualTo(0),
                    "外层展开了，内层的内联才轮到生效。");
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

    /// <summary>成员级内联的对照。</summary>
    [HideMonoScript]
    internal sealed class InlineFixture : ScriptableObject
    {
        /// <summary>复合类型，标了内联。</summary>
        [InlineProperty]
        public InlineSample inline;

        /// <summary>同样的复合类型，未标（控制项）。</summary>
        public InlineSample plain;

        /// <summary>简单类型：标了也只能降级。</summary>
        [InlineProperty]
        public int simple;

        /// <summary>数组：不内联（展开集合是 L6）。</summary>
        [InlineProperty]
        public InlineSample[] array;

        /// <summary>向量：原生就有 x/y 子属性，可内联（走观感派那条自画路径）。</summary>
        [InlineProperty]
        public Vector2Int inlineVector;

        /// <summary>未标内联的向量（控制项）。</summary>
        public Vector2Int vector;
    }

    /// <summary>类级内联的对照：字段的声明类型带标记。</summary>
    [HideMonoScript]
    internal sealed class InlineClassLevelFixture : ScriptableObject
    {
        /// <summary>声明类型带标记 → 注入。</summary>
        public InlineMarkedType marked;

        /// <summary>同一类型的第二个字段——注入的是各自独立的新实例。</summary>
        public InlineMarkedType markedAgain;

        /// <summary>未标记的类型（控制项）。</summary>
        public InlineSample plain;

        /// <summary>自己标了内联，声明类型上也标了——不该被注入两次。</summary>
        [InlineProperty]
        public InlineMarkedType both;
    }

    /// <summary>类级内联标在**深一层**字段类型上的对照：外层类型自己一个特性都没有。</summary>
    [HideMonoScript]
    internal sealed class InlineNestedHolderFixture : ScriptableObject
    {
        /// <summary>展开与否只看它**内部**有没有用得上的东西。</summary>
        public InlineNestedOuter outer;
    }

    /// <summary>内部字段的声明类型带内联标记——外层必须为它展开。</summary>
    [Serializable]
    internal sealed class InlineNestedOuter
    {
        /// <summary>声明类型带标记。</summary>
        public InlineMarkedType inner;
    }

    /// <summary>带标记的复合类型：标在类型上 → 该类型的字段一律内联。</summary>
    [Serializable]
    [InlineProperty(LabelWidth = 60)]
    internal sealed class InlineMarkedType
    {
        /// <summary>子字段。</summary>
        public int x;

        /// <summary>子字段。</summary>
        public int y;
    }

    /// <summary>未标记的复合类型。</summary>
    [Serializable]
    internal sealed class InlineSample
    {
        /// <summary>子字段。</summary>
        public int a;

        /// <summary>子字段。</summary>
        public int b;
    }
}
