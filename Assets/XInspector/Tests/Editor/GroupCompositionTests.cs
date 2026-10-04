using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

// 同一文件里同时 using System 与 using UnityEngine 时，裸写 Object 是 CS0104 二义
// （System.Object vs UnityEngine.Object）——用别名消歧，这是本仓记过的坑。
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 分组装配的组合规则：同路径多类型并存、成员按前缀归属、类级分发不漏第二个。
    /// <para>
    /// 用测试自带的第二种分组特性（<see cref="MarkerGroupAttribute"/>）而不是尚未实现的
    /// 分组族——装配规则与具体分组类型无关，这是刻意的：装配对类型一无所知。
    /// </para>
    /// </summary>
    [TestFixture]
    public class GroupCompositionTests
    {
        #region Setup / Teardown

        /// <summary>复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 同路径多类型

        /// <summary>
        /// 两种分组特性落同一路径时**并存**：节点上两份特性、链上两格绘制器。
        /// <para>
        /// 此前只留先创建者那份，第二种被静默丢弃；且祖先节点带哪种类型取决于声明顺序。
        /// </para>
        /// </summary>
        [Test]
        public void 同路径多类型并存()
        {
            WithTree(new MultiTypeFixture(), tree =>
            {
                var first = Find(tree.Root, "BoxFirst");
                Assert.That(first.Attributes.Has<BoxGroupAttribute>(), Is.True);
                Assert.That(first.Attributes.Has<MarkerGroupAttribute>(), Is.True, "第二种类型不该被丢弃。");
                Assert.That(IndexOf<BoxGroupDrawer>(first), Is.GreaterThanOrEqualTo(0));
                Assert.That(IndexOf<MarkerGroupDrawer>(first), Is.GreaterThanOrEqualTo(0), "两种类型各有一格绘制器。");

                var second = Find(tree.Root, "MarkerFirst");
                Assert.That(second.Attributes.Has<BoxGroupAttribute>(), Is.True, "换个声明顺序同样并存。");
                Assert.That(second.Attributes.Has<MarkerGroupAttribute>(), Is.True);
            });
        }

        /// <summary>同类型重复声明仍走 Combine（先声明者优先），不是并存两份。</summary>
        [Test]
        public void 同类型仍走合并()
        {
            WithTree(new MultiTypeFixture(), tree =>
            {
                var node = Find(tree.Root, "SameType");
                var count = 0;

                foreach (var attribute in node.Attributes)
                {
                    if (attribute is BoxGroupAttribute)
                    {
                        count++;
                    }
                }

                Assert.That(count, Is.EqualTo(1), "同类型的多次声明应压成一份。");
            });
        }

        /// <summary>节点上的**每一份**分组特性的 GroupID 都等于节点路径（不变量加强版）。</summary>
        [Test]
        public void 每份分组特性都满足路径不变量()
        {
            WithTree(new MultiTypeFixture(), tree =>
            {
                var visited = 0;
                AssertPathInvariant(tree.Root, ref visited);

                Assert.That(visited, Is.GreaterThan(0), "没有访问到任何分组节点，这条不变量等于没检查。");
            });
        }

        #endregion

        #region 前缀归属

        /// <summary>
        /// 成员的每个「属于目标路径链」的分组特性各贡献自己那段路径：外层节点拿得到它自己那份声明。
        /// <para>
        /// 不这么做的话 <c>[MarkerGroup("H")] [BoxGroup("H/Box")]</c> 里 H 拿不到 Marker 特性，
        /// 那类分组的表现（行、标题……）会**静默不生效**。
        /// </para>
        /// </summary>
        [Test]
        public void 前缀归属让外层拿到自己的声明()
        {
            WithTree(new PrefixFixture(), tree =>
            {
                var outer = Find(tree.Root, "H");
                var inner = Find(outer, "H/Box");

                Assert.That(outer.Attributes.Has<MarkerGroupAttribute>(), Is.True,
                    "外层节点要拿到成员为它声明的那份特性。");
                Assert.That(inner.Attributes.Has<BoxGroupAttribute>(), Is.True);
                Assert.That(Find(inner, "nested"), Is.Not.Null, "成员落在最深的节点里。");
            });
        }

        /// <summary>不是前缀的分组仍被忽略——成员只归属最深的一条链。</summary>
        [Test]
        public void 不相关的分组仍被忽略()
        {
            WithTree(new PrefixFixture(), tree =>
            {
                Assert.That(IndexOfChild(tree.Root, "Unrelated"), Is.EqualTo(-1), "不相干的分组不该长出节点。");
            });
        }

        #endregion

        #region 类级分发

        /// <summary>
        /// 类级分发给成员的**每个**分组特性都加前缀。
        /// <para>
        /// 只加第一个的话，第二种会留在类级分组外面——成员于是跑出类级分组。
        /// </para>
        /// </summary>
        [Test]
        public void 类级分发不漏第二个分组特性()
        {
            WithTree(new ClassLevelMultiFixture(), tree =>
            {
                var classNode = Find(tree.Root, "类级");
                var middle = Find(classNode, "类级/甲");
                var leaf = Find(middle, "类级/甲/乙");

                Assert.That(leaf, Is.Not.Null, "两种分组特性都该带上类级前缀。");
                Assert.That(Find(leaf, "two"), Is.Not.Null, "成员落在最深的那层。");
            });
        }

        #endregion

        #region 排序

        /// <summary>
        /// 节点的排序权重取**最小的非零 Order**：多类型并存时若取「第一个特性的 Order」，
        /// 排序就会重新依赖声明顺序。
        /// </summary>
        [Test]
        public void 排序取最小的非零权重()
        {
            WithTree(new OrderingMixFixture(), tree =>
            {
                var mixed = IndexOfChild(tree.Root, "Mixed");
                var plain = IndexOfChild(tree.Root, "Plain");

                Assert.That(mixed, Is.LessThan(plain),
                    "Mixed 的最小非零 Order 是 -5，应排在 Order 全为 0 的 Plain 之前。");
            });
        }

        #endregion

        #region Private Helpers

        /// <summary>建树、执行断言、销毁资产。</summary>
        /// <typeparam name="T">夹具类型。</typeparam>
        /// <param name="target">夹具实例。</param>
        /// <param name="assert">断言委托。</param>
        private static void WithTree<T>(T target, Action<PropertyTree> assert) where T : ScriptableObject
        {
            try
            {
                assert(PropertyTree.Create(new SerializedObject(target)));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>按路径查找直接子节点。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点。</returns>
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

        /// <summary>取直接子节点的下标；不存在返回 -1。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">路径。</param>
        /// <returns>下标。</returns>
        private static int IndexOfChild(InspectorProperty parent, string path)
        {
            for (var i = 0; i < parent.Children.Count; i++)
            {
                if (parent.Children[i].Path == path)
                {
                    return i;
                }
            }

            return -1;
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

        /// <summary>递归校验「节点上所有分组特性的 GroupID 等于节点路径」。</summary>
        /// <param name="node">当前节点。</param>
        /// <param name="visited">已访问的分组节点计数。</param>
        private static void AssertPathInvariant(InspectorProperty node, ref int visited)
        {
            if (node.Kind == InspectorPropertyKind.Group)
            {
                var found = false;

                foreach (var attribute in node.Attributes)
                {
                    if (!(attribute is PropertyGroupAttribute group))
                    {
                        continue;
                    }

                    found = true;
                    Assert.That(group.GroupID, Is.EqualTo(node.Path),
                        $"分组特性描述的路径与节点路径不一致（节点 {node.Path}）。");
                    Assert.That(group.GroupName, Is.EqualTo(node.Name));
                }

                Assert.That(found, Is.True, $"分组节点 {node.Path} 上没有分组特性。");
                visited++;
            }

            foreach (var child in node.Children)
            {
                AssertPathInvariant(child, ref visited);
            }
        }

        #endregion
    }

    /// <summary>
    /// 测试专用的第二种分组特性——装配规则与具体类型无关，用一个简单的实现即可验证。
    /// </summary>
    /// <remarks>
    /// 跨程序集覆写 <c>protected internal</c> 的 <see cref="PropertyGroupAttribute.Combine"/>
    /// 必须声明为 <c>protected</c>（C# 的限制，Runtime README 记过）。
    /// </remarks>
    internal sealed class MarkerGroupAttribute : PropertyGroupAttribute
    {
        /// <summary>以路径构造。</summary>
        /// <param name="groupID">分组路径。</param>
        /// <param name="order">排序权重。</param>
        public MarkerGroupAttribute(string groupID, float order = 0f)
            : base(groupID, order)
        {
        }

        /// <summary>子类字段：验证 Combine 与克隆把它带走。</summary>
        public string Marker { get; set; }

        /// <inheritdoc/>
        protected internal override void Combine(PropertyGroupAttribute other)
        {
            base.Combine(other);

            if (other is MarkerGroupAttribute marker && string.IsNullOrEmpty(Marker))
            {
                Marker = marker.Marker;
            }
        }
    }

    /// <summary>MarkerGroup 的绘制器：证明「另一种分组特性也各有一格」。</summary>
    internal sealed class MarkerGroupDrawer : AttributeDrawer<MarkerGroupAttribute>
    {
        /// <inheritdoc/>
        protected override void DrawPropertyLayout(InspectorProperty property, MarkerGroupAttribute attribute, GUIContent label)
        {
            CallNextDrawer(property, label);
        }
    }

    /// <summary>同路径与同类型两组对照。</summary>
    internal sealed class MultiTypeFixture : ScriptableObject
    {
        /// <summary>先 Box 后 Marker。</summary>
        [BoxGroup("BoxFirst")]
        public int a;

        /// <summary>同路径的第二种类型。</summary>
        [MarkerGroup("BoxFirst")]
        public int b;

        /// <summary>先 Marker 后 Box。</summary>
        [MarkerGroup("MarkerFirst")]
        public int c;

        /// <summary>同路径的第二种类型。</summary>
        [BoxGroup("MarkerFirst")]
        public int d;

        /// <summary>同类型只该压成一份。</summary>
        [BoxGroup("SameType")]
        public int e;

        /// <summary>同类型的第二次声明。</summary>
        [BoxGroup("SameType", 3f)]
        public int f;
    }

    /// <summary>前缀归属的对照。</summary>
    internal sealed class PrefixFixture : ScriptableObject
    {
        /// <summary>外层是 Marker、内层是 Box。</summary>
        [MarkerGroup("H")]
        [BoxGroup("H/Box")]
        public int nested;

        /// <summary>与成员目标路径不相干的分组——应当被忽略。</summary>
        [MarkerGroup("Unrelated")]
        [BoxGroup("H/Box")]
        public int unrelated;
    }

    /// <summary>类级分组 + 成员自己的两种分组。</summary>
    [MarkerGroup("类级")]
    internal sealed class ClassLevelMultiFixture : ScriptableObject
    {
        /// <summary>两种分组特性都该带上类级前缀。</summary>
        [MarkerGroup("甲")]
        [BoxGroup("甲/乙")]
        public int two;
    }

    /// <summary>排序取最小非零权重的对照：权重大小与声明先后刻意相反。</summary>
    internal sealed class OrderingMixFixture : ScriptableObject
    {
        /// <summary>声明在前、Order 为 0。</summary>
        [BoxGroup("Plain")]
        public int c;

        /// <summary>声明在后，路径上的最小非零权重是 -5——应当被排到前面。</summary>
        [BoxGroup("Mixed", 5f)]
        public int a;

        /// <summary>同路径的第二种类型，它带来那个 -5。</summary>
        [MarkerGroup("Mixed", -5f)]
        public int b;
    }
}
