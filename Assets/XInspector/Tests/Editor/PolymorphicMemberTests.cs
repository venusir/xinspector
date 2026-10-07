using System;
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
    /// 多态引用（<c>[SerializeReference]</c>）进管线：序列化成员**按需**成为真节点。
    /// <para>
    /// 不测 IMGUI——断言的是「有没有子节点」「路径与类型对不对」「分组装在哪」「条件跟不跟随」
    /// 「守卫有没有拦下」。与前几轮的能力轮同一套口径。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PolymorphicMemberTests
    {
        #region Fixture

        /// <summary>复位静态门面（建树会初始化绘制器与处理器两张注册表）。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 按需展开

        /// <summary>**没有实例**时不展开：整份交回 Unity，外观与从前逐字一致。</summary>
        [Test]
        public void 多态引用没有实例时不展开()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");

                Assert.That(shape.Children.Count, Is.EqualTo(0), "空槽位没有类型可问，不展开。");
                Assert.That(
                    ChainTail(shape),
                    Is.InstanceOf<UnityFallbackDrawer>(),
                    "整份交给 Unity——与「没用到本包的类型外观不变」同款。");
                Assert.That(shape.Type, Is.EqualTo(typeof(IShape)), "类型退回声明类型。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>**有实例**且具体类型用得到本包时，按需展开成真节点。</summary>
        [Test]
        public void 多态引用有实例时按需展开()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            target.shape = new Circle { radius = 2f };
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");

                Assert.That(shape.Children.Count, Is.GreaterThan(0), "具体类型用到了本包 ⇒ 展开。");
                Assert.That(shape.Type, Is.EqualTo(typeof(Circle)), "节点的类型是**具体类型**。");
                Assert.That(
                    ChainTail(shape),
                    Is.Not.InstanceOf<UnityFallbackDrawer>(),
                    "展开过的容器不再走原生兜底。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>具体类型**没用到本包**时照样不展开——这是本轮的安全阀。</summary>
        [Test]
        public void 具体类型没用到本包时不展开()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            target.shape = new PlainShape { weight = 3f };
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");

                Assert.That(shape.Children.Count, Is.EqualTo(0), "没用到本包的具体类型，外观一个字不变。");
                Assert.That(ChainTail(shape), Is.InstanceOf<UnityFallbackDrawer>());
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **派生类型独有的字段**要看得见——这是「按运行时类型解析」的意义所在。
        /// </summary>
        /// <remarks>
        /// 按声明类型（<c>Shape</c>）解析的话，<c>Circle</c> 独有的字段在基类上找不到，
        /// <c>ResolveField</c> 会返回 null，于是**特性、分组、告警一起静默消失**。
        /// </remarks>
        [Test]
        public void 派生类型独有的字段看得见()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            target.baseShape = new DerivedShape { extra = 7 };
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "baseShape");

                Assert.That(shape.Children.Count, Is.GreaterThan(0));
                var group = Find(shape, "baseShape/派生");
                Assert.That(group, Is.Not.Null, "派生类独有字段上的分组也装配出来了。");

                var extra = Find(group, "baseShape.extra");
                Assert.That(extra, Is.Not.Null, "派生类独有的字段进了树。");
                Assert.That(extra.Member, Is.Not.Null, "而且解析出了 FieldInfo——否则特性与分组会静默丢。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 里面的特性生效

        /// <summary>多态段里的**分组**照样装配，组节点路径以父字段的路径为前缀。</summary>
        [Test]
        public void 多态段里的分组照样装配()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            target.shape = new Circle { radius = 2f };
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");

                // 分组节点挂在**容器自己**下面（分组装配是对容器那一层跑的），不是挂在根上。
                var group = Find(shape, "shape/几何");

                Assert.That(group, Is.Not.Null, "分组在**多态段里面**，路径以父字段为前缀。");
                Assert.That(group.Kind, Is.EqualTo(InspectorPropertyKind.Group));
                Assert.That(Find(group, "shape.radius"), Is.Not.Null, "成员落在组里面。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>多态段里的**条件**生效——判据按同层、再回落根。</summary>
        [Test]
        public void 多态段里的条件跟随根上的开关()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            target.shape = new Circle { radius = 2f };
            target.alive = true;
            try
            {
                var tree = BuildTree(target);
                var group = Find(Find(tree.Root, "shape"), "shape/几何");
                var segments = Find(group, "shape.segments");

                Assert.That(segments, Is.Not.Null);
                Assert.That(segments.State.IsVisible, Is.True, "开关为真时可见。");

                SetBool(target, tree, "alive", false);

                Assert.That(segments.State.IsVisible, Is.False, "开关关掉后跟着消失。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 守卫

        /// <summary>自引用在**类型重复那一层**被挡下，并告警一条。</summary>
        /// <remarks>
        /// <b>这道闸是必须的，不是保险。</b> 实测（<c>SerializedReferenceProbeTests</c>）：
        /// 自引用的多态子树在 <c>NextVisible</c> 下是**无限的**——环没有被 Unity 切断。
        /// 没有它，构建期就会无限递归。
        /// </remarks>
        [Test]
        public void 自引用在类型重复那一层被挡并告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex("已经在祖先的多态引用链上出现过"));

            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            var node = new ChainNode { id = 0 };
            node.next = node; // 自引用
            target.chain = node;
            try
            {
                var tree = BuildTree(target);

                var chain = Find(tree.Root, "chain");
                Assert.That(chain.Children.Count, Is.GreaterThan(0), "最外层照常展开。");

                var next = Find(chain, "chain.next");
                Assert.That(next, Is.Not.Null, "回边自己进了树。");
                Assert.That(next.Children.Count, Is.EqualTo(0), "但它不再往里展开——无尽递归在这里止步。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 多选

        /// <summary>多选混合态一律不展开——不拿一个目标的结构冒充全体。</summary>
        /// <remarks>
        /// 实测：<c>hasMultipleDifferentValues</c> 在「同类型、只是两个实例」时就已经为真
        /// （区分不出类型是否一致），而 <c>managedReferenceValue</c> 给的是主目标那个实例。
        /// 故判据只能取「一律不展开」——与「多选下不增删元素」同款惯例，**不告警**。
        /// </remarks>
        [Test]
        public void 多选混合态不展开()
        {
            var a = ScriptableObject.CreateInstance<PolymorphicFixture>();
            var b = ScriptableObject.CreateInstance<PolymorphicFixture>();
            a.shape = new Circle { radius = 1f };
            b.shape = new Circle { radius = 2f };
            try
            {
                var tree = PropertyTree.Create(new SerializedObject(new Object[] { a, b }));
                try
                {
                    var shape = Find(tree.Root, "shape");
                    Assert.That(shape.Children.Count, Is.EqualTo(0), "混合态不展开。");
                    Assert.That(shape.Type, Is.EqualTo(typeof(IShape)), "类型退回声明类型。");
                }
                finally
                {
                    tree.Dispose();
                }
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        #endregion

        #region Helpers

        /// <summary>建一棵树。</summary>
        /// <param name="target">目标资产。</param>
        /// <returns>属性树。</returns>
        private static PropertyTree BuildTree(ScriptableObject target)
        {
            return PropertyTree.Create(new SerializedObject(target));
        }

        /// <summary>链尾的绘制器。</summary>
        /// <param name="property">节点。</param>
        /// <returns>链尾绘制器。</returns>
        private static object ChainTail(InspectorProperty property)
        {
            return property.Chain.Entries[property.Chain.Count - 1].Drawer;
        }

        /// <summary>按完整路径查直接子节点。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>命中的节点；没有返回 <c>null</c>。</returns>
        private static InspectorProperty Find(InspectorProperty parent, string path)
        {
            var children = parent.Children;
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i].Path == path)
                {
                    return children[i];
                }
            }

            return null;
        }

        /// <summary>经**另一个** SerializedObject 改一个 bool，再让树那个 Update。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="path">字段路径。</param>
        /// <param name="value">新值。</param>
        private static void SetBool(ScriptableObject target, PropertyTree tree, string path, bool value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        #endregion
    }

    #region Fixtures

    /// <summary>多态引用的对照资产。</summary>
    [HideMonoScript]
    internal sealed class PolymorphicFixture : ScriptableObject
    {
        /// <summary>根上的开关——多态段里的条件要能回落到它。</summary>
        public bool alive = true;

        /// <summary>接口槽位（空着时用来验证「没有实例不展开」）。</summary>
        [SerializeReference]
        public IShape shape;

        /// <summary>基类槽位（用来验证派生类独有字段）。</summary>
        [SerializeReference]
        public Shape baseShape;

        /// <summary>自引用的链（用来验证守卫）。</summary>
        [SerializeReference]
        public ChainNode chain;
    }

    /// <summary>探针用的接口槽位类型。</summary>
    internal interface IShape
    {
    }

    /// <summary>用得到本包的具体类型。</summary>
    [Serializable]
    internal class Circle : IShape
    {
        /// <summary>分组里的第一个成员。</summary>
        [BoxGroup("几何")]
        public float radius = 1f;

        /// <summary>分组里的第二个成员，带一个指回根的条件。</summary>
        [BoxGroup("几何")]
        [ShowIf("alive")]
        public int segments = 8;
    }

    /// <summary>**没用到本包**的具体类型（安全阀的对照）。</summary>
    [Serializable]
    internal class PlainShape : IShape
    {
        /// <summary>一个普通字段。</summary>
        public float weight;
    }

    /// <summary>基类槽位用的基类型。</summary>
    [Serializable]
    internal class Shape
    {
        /// <summary>基类上的字段。</summary>
        public int baseValue;
    }

    /// <summary>派生类型：它独有的那个字段是「按运行时类型解析」的试金石。</summary>
    [Serializable]
    internal class DerivedShape : Shape
    {
        /// <summary>派生类独有、且带本包特性——按声明类型解析会整个丢掉它。</summary>
        [BoxGroup("派生")]
        public int extra;
    }

    /// <summary>自引用的链节点，带一个本包特性（否则不会展开，也就测不到守卫）。</summary>
    [Serializable]
    internal class ChainNode
    {
        /// <summary>普通字段。</summary>
        [BoxGroup("链")]
        public int id;

        /// <summary>指回同类型的多态引用。</summary>
        [SerializeReference]
        public ChainNode next;
    }

    #endregion
}
