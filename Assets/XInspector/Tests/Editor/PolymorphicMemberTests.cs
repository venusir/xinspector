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

        /// <summary>
        /// 多态段里的读路径成员**成为节点**——读路径那一批已开，跳过告警已撤。
        /// </summary>
        /// <remarks>
        /// 这条此前钉的是「响亮跳过」（专门告警、成员不进树）；读路径落地后翻面：
        /// 成员进树、且**一条告警都不该有**（旧告警留着的症状是「说了跳过、其实没跳过」）。
        /// </remarks>
        [Test]
        public void 多态段里的读路径成员成为节点且不告警()
        {
            LogAssert.NoUnexpectedReceived();

            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            target.shape = new AnnotatedShape();
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");

                Assert.That(shape.Children.Count, Is.GreaterThan(0), "序列化成员照常展开。");

                var badge = Find(shape, "shape.badge");
                Assert.That(badge, Is.Not.Null, "[ShowInInspector] 的成员进了树。");
                Assert.That(badge.Kind, Is.EqualTo(InspectorPropertyKind.ReflectedMember));
                Assert.That(badge.Parent, Is.SameAs(shape), "挂在多态容器之下。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>没有读路径成员时**不告警**：那是「外观不变」的正常路径，不该吵。</summary>
        [Test]
        public void 多态段没有读路径成员时不告警()
        {
            LogAssert.NoUnexpectedReceived();

            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            target.shape = new Circle { radius = 2f };
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");

                Assert.That(shape.Children.Count, Is.GreaterThan(0));
                Assert.That(
                    CountReadPathNodes(shape),
                    Is.EqualTo(0),
                    "没有读路径成员时，一个反射/方法节点都不该多出来。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 换具体类型（对账与重建）

        /// <summary>换了具体类型之后整棵子树重建，且对账随后是廉价 no-op。</summary>
        [Test]
        public void 换了具体类型之后整棵子树重建()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            target.shape = new Circle { radius = 2f };
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");

                Assert.That(shape.Type, Is.EqualTo(typeof(Circle)));
                Assert.That(Find(shape, "shape/几何"), Is.Not.Null, "Circle 的分组在。");

                // 换实现——今天只能靠代码（Unity 原生不给类型选择器，那是下一批）。
                target.shape = new Square { side = 3f };
                tree.SerializedObject.Update();

                Assert.That(PolymorphicReferenceSync.ReconcileAll(tree), Is.EqualTo(1), "对账发现类型变了。");

                var rebuilt = Find(tree.Root, "shape");
                Assert.That(rebuilt, Is.SameAs(shape), "容器节点自己不动，换的是它的子树。");
                Assert.That(rebuilt.Type, Is.EqualTo(typeof(Square)), "容器的类型跟着换。");
                Assert.That(Find(rebuilt, "shape/几何"), Is.Null, "旧类型的子树整棵撤掉。");
                Assert.That(Find(rebuilt, "shape/方形"), Is.Not.Null, "新类型的分组装配出来。");

                Assert.That(PolymorphicReferenceSync.ReconcileAll(tree), Is.EqualTo(0), "类型没再变 ⇒ 廉价 no-op。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>槽位被清空之后子树撤掉，但**容器自己的末端不变**（链是冻结的）。</summary>
        [Test]
        public void 清空槽位之后子树撤掉()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            target.shape = new Circle { radius = 2f };
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");
                var terminal = ChainTail(shape);

                target.shape = null;
                tree.SerializedObject.Update();

                Assert.That(PolymorphicReferenceSync.ReconcileAll(tree), Is.EqualTo(1));
                Assert.That(shape.Children.Count, Is.EqualTo(0), "子节点撤干净。");
                Assert.That(shape.Type, Is.EqualTo(typeof(IShape)), "类型退回声明类型。");
                Assert.That(
                    ChainTail(shape),
                    Is.SameAs(terminal),
                    "末端不变——链在构建期冻结，而它本来就画得对（原生那一行照画）。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 空槽位与外部赋值（首建）

        /// <summary>
        /// 空槽位写值之后，对账**立刻**按同一道闸展开——与 Unity 原生的「选了类型就出现子字段」一致。
        /// </summary>
        /// <remarks>
        /// 首建走的是**同一条七步流水线**（`RebuildPolymorphicLayer` 的 `layer == null` 分支）：
        /// 补建层状态、按新类型展开、挂链、分组装配。末端也从原生兜底换成多态末端——
        /// 链是重接的（对账在一切绘制之前，此刻换链安全）。
        /// </remarks>
        [Test]
        public void 空槽位写值之后对账即展开()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");

                Assert.That(shape.Children.Count, Is.EqualTo(0), "起点：空槽位没有子节点。");
                Assert.That(ChainTail(shape), Is.InstanceOf<UnityFallbackDrawer>(), "起点：走原生兜底。");

                Assert.That(
                    PolymorphicSlotWrite.TryWrite(
                        typeof(Circle),
                        NonDefaultConstructorPreference.ConstructIdeal,
                        null,
                        shape.ValueEntry.SerializedProperty,
                        typeof(IShape),
                        undoEnabled: false,
                        out var reason),
                    Is.True,
                    reason);

                Assert.That(
                    PolymorphicReferenceSync.ReconcileAll(tree),
                    Is.EqualTo(1),
                    "对账发现了新值并按闸展开。");

                Assert.That(shape.Children.Count, Is.GreaterThan(0), "子字段出现了。");
                Assert.That(shape.Type, Is.EqualTo(typeof(Circle)));
                Assert.That(
                    ChainTail(shape),
                    Is.Not.InstanceOf<UnityFallbackDrawer>(),
                    "末端已被换成多态末端（链重接过了）。");
                Assert.That(Find(shape, "shape/几何"), Is.Not.Null, "分组也装上了——首建是完整流水线。");

                Assert.That(PolymorphicReferenceSync.ReconcileAll(tree), Is.EqualTo(0), "类型没再变 ⇒ 廉价 no-op。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 写进来的类型**用不到本包**时不展开——安全阀照旧（首建也要过构建期那同一道闸）。
        /// </summary>
        [Test]
        public void 写进来的类型用不到本包时不展开()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");

                Assert.That(
                    PolymorphicSlotWrite.TryWrite(
                        typeof(PlainShape),
                        NonDefaultConstructorPreference.ConstructIdeal,
                        null,
                        shape.ValueEntry.SerializedProperty,
                        typeof(IShape),
                        undoEnabled: false,
                        out var reason),
                    Is.True,
                    reason);

                Assert.That(PolymorphicReferenceSync.ReconcileAll(tree), Is.EqualTo(0), "没过闸，不展开。");
                Assert.That(shape.Children.Count, Is.EqualTo(0), "外观一个字不变。");
                Assert.That(ChainTail(shape), Is.InstanceOf<UnityFallbackDrawer>(), "仍走原生兜底。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **未展开**的托管引用成员也进登记表——槽位随时可能被赋值（本包的选择器、原生 UI、
        /// 代码、撤销都算），对账要看得见它。
        /// </summary>
        [Test]
        public void 未展开的托管引用成员进了登记表()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(
                    tree.PolymorphicContainers.Contains(Find(tree.Root, "shape")),
                    Is.True,
                    "空槽位也在表上（只看着，不展开）。");
                Assert.That(
                    tree.PolymorphicContainers.Contains(Find(tree.Root, "alive")),
                    Is.False,
                    "普通成员不在表里。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>首建之后新子树的**链**也在（不是只建了节点）。</summary>
        [Test]
        public void 首建之后新子树的链也在()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicFixture>();
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");

                Assert.That(
                    PolymorphicSlotWrite.TryWrite(
                        typeof(Circle),
                        NonDefaultConstructorPreference.ConstructIdeal,
                        null,
                        shape.ValueEntry.SerializedProperty,
                        typeof(IShape),
                        undoEnabled: false,
                        out var reason),
                    Is.True,
                    reason);

                PolymorphicReferenceSync.ReconcileAll(tree);

                var group = Find(shape, "shape/几何");
                Assert.That(group, Is.Not.Null, "新分组装配出来了。");

                var radius = Find(group, "shape.radius");
                Assert.That(radius, Is.Not.Null, "新子节点在。");
                Assert.That(radius.Chain.Entries.Length, Is.GreaterThan(0), "新子树的链接上了。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 多选

        /// <summary>多选混合态一律不展开——不拿一个目标的结构冒充全体（读路径成员同样不出现）。</summary>
        /// <remarks>
        /// 实测：<c>hasMultipleDifferentValues</c> 在「同类型、只是两个实例」时就已经为真
        /// （区分不出类型是否一致），而 <c>managedReferenceValue</c> 给的是主目标那个实例。
        /// 故判据只能取「一律不展开」——与「多选下不增删元素」同款惯例，**不告警**。
        /// 夹具用 <see cref="AnnotatedShape"/>（带读路径成员）：读路径那一批落地后，
        /// 混合态连反射节点也不该出现——孩子数归零这一条一并钉住了那件事。
        /// </remarks>
        [Test]
        public void 多选混合态不展开()
        {
            var a = ScriptableObject.CreateInstance<PolymorphicFixture>();
            var b = ScriptableObject.CreateInstance<PolymorphicFixture>();
            a.shape = new AnnotatedShape { size = 1 };
            b.shape = new AnnotatedShape { size = 2 };
            try
            {
                var tree = PropertyTree.Create(new SerializedObject(new Object[] { a, b }));
                try
                {
                    var shape = Find(tree.Root, "shape");
                    Assert.That(shape.Children.Count, Is.EqualTo(0), "混合态不展开（连读路径成员一起挡）。");
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

        /// <summary>整棵子树里读路径节点的个数（<c>[ShowInInspector]</c> 与方法节点）。</summary>
        /// <param name="node">子树根。</param>
        /// <returns>个数。</returns>
        private static int CountReadPathNodes(InspectorProperty node)
        {
            var count = 0;

            foreach (var child in node.Children)
            {
                if (child.Kind == InspectorPropertyKind.ReflectedMember ||
                    child.Kind == InspectorPropertyKind.Method)
                {
                    count++;
                }

                count += CountReadPathNodes(child);
            }

            return count;
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

    /// <summary>带**读路径**成员的具体类型（反射成员与序列化成员混在一起）。</summary>
    [Serializable]
    internal class AnnotatedShape : IShape
    {
        /// <summary>序列化成员——照常展开。</summary>
        [BoxGroup("有注解")]
        public int size = 1;

        /// <summary>读路径成员——与序列化成员同层，一起进树。</summary>
        [ShowInInspector]
        public string badge => "★";
    }

    /// <summary>另一个用得到本包的具体类型——用来验证「换了实现就换子树」。</summary>
    [Serializable]
    internal class Square : IShape
    {
        /// <summary>与 <see cref="Circle"/> 完全不同的分组名，便于断言旧子树真的撤了。</summary>
        [BoxGroup("方形")]
        public float side = 1f;
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
