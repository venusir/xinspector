using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 属性树构建器：成员收集范围、顺序、节点装配。
    /// <para>
    /// 这些断言全部不需要 GUI——树的**结构**是可无头验证的，而结构恰好是最容易出错的
    /// 一部分（漏字段、顺序乱、把不该显示的字段显示出来）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PropertyTreeBuilderTests
    {
        #region Private Fields

        private BuilderFixture _target;

        #endregion

        #region Setup / Teardown

        /// <summary>建立用于构建树的临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<BuilderFixture>();
        }

        /// <summary>销毁临时资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region 结构与顺序

        /// <summary>
        /// 根节点种类正确，且至少有一个子节点。
        /// </summary>
        [Test]
        public void Build_RootHasMemberChildren()
        {
            var tree = BuildTree();

            Assert.That(tree.Root.Kind, Is.EqualTo(InspectorPropertyKind.Root));
            Assert.That(tree.Root.Children.Count, Is.GreaterThan(0));

            foreach (var child in tree.Root.Children)
            {
                Assert.That(child.Kind, Is.EqualTo(InspectorPropertyKind.Member));
                Assert.That(child.Parent, Is.SameAs(tree.Root), "父子引用必须双向接上。");
            }
        }

        /// <summary>
        /// 可序列化成员按声明顺序被收集。
        /// </summary>
        [Test]
        public void Build_CollectsSerializedMembersInDeclarationOrder()
        {
            var tree = BuildTree();
            var paths = PathsOf(tree);

            Assert.That(paths, Does.Contain("first"));
            Assert.That(paths, Does.Contain("_second"), "[SerializeField] 的私有字段必须出现。");
            Assert.That(paths, Does.Contain("third"));

            // 顺序断言失败时把实际顺序打出来：只报「期望小于」而不给全貌，
            // 排查时还得再跑一次才知道真实顺序是什么。
            Assert.That(
                paths.IndexOf("first"),
                Is.LessThan(paths.IndexOf("_second")),
                $"字段顺序与声明顺序不符，实际顺序：[{string.Join(", ", paths)}]");
            Assert.That(
                paths.IndexOf("_second"),
                Is.LessThan(paths.IndexOf("third")),
                $"字段顺序与声明顺序不符，实际顺序：[{string.Join(", ", paths)}]");
        }

        /// <summary>
        /// <c>[HideInInspector]</c> 的成员不得出现在树里。
        /// <para>
        /// 这条守的是「与原生 Inspector 一致」：既然我们复用 Unity 的序列化可见性判断，
        /// 就不该出现「原生不显示、我们却显示」的字段。
        /// </para>
        /// </summary>
        [Test]
        public void Build_ExcludesHiddenMembers()
        {
            var tree = BuildTree();

            Assert.That(PathsOf(tree), Does.Not.Contain("hidden"));
        }

        /// <summary>
        /// 每个成员节点都带有值入口与已装配的绘制器链。
        /// </summary>
        [Test]
        public void Build_EveryMemberHasValueEntryAndChain()
        {
            var tree = BuildTree();

            foreach (var child in tree.Root.Children)
            {
                Assert.That(child.ValueEntry, Is.Not.Null, $"成员 {child.Path} 缺少值入口。");
                Assert.That(child.ValueEntry.IsUnityBacked, Is.True);
                Assert.That(child.Chain, Is.Not.Null, $"成员 {child.Path} 缺少绘制器链。");
                Assert.That(child.Chain.Count, Is.GreaterThan(0), "链条永不为空。");
            }
        }

        /// <summary>
        /// 根节点是分组性的，自身没有值入口。
        /// </summary>
        [Test]
        public void Build_RootHasNoValueEntry()
        {
            var tree = BuildTree();

            Assert.That(tree.Root.ValueEntry, Is.Null);
        }

        /// <summary>
        /// 值入口暴露的底层序列化属性与节点路径一致。
        /// </summary>
        [Test]
        public void Build_值入口指向同一序列化属性()
        {
            var tree = BuildTree();

            foreach (var child in tree.Root.Children)
            {
                Assert.That(child.ValueEntry.SerializedProperty.propertyPath, Is.EqualTo(child.Path));
            }
        }

        /// <summary>
        /// 各节点的底层序列化属性必须是**彼此独立**的实例。
        /// <para>
        /// 这条是回归守卫。<c>SerializedObject.GetIterator()</c> 返回的是同一个实例，
        /// <c>NextVisible</c> 就地改写它；若构建期图省事直接把遍历器存进各节点，
        /// 走完遍历后所有节点会共享那一个对象、全部指向最后一个属性——
        /// 症状是「Inspector 里每个字段显示的都是同一个值」，而现象看起来像是绘制器坏了。
        /// 实测踩过一次，故这里既比对路径也比对实例身份。
        /// </para>
        /// </summary>
        [Test]
        public void Build_各节点的序列化属性互相独立()
        {
            var tree = BuildTree();
            var seen = new HashSet<SerializedProperty>();

            foreach (var child in tree.Root.Children)
            {
                var property = child.ValueEntry.SerializedProperty;

                Assert.That(property.propertyPath, Is.EqualTo(child.Path),
                    "值入口指向的序列化属性不是该节点自己的那个。");
                Assert.That(seen.Add(property), Is.True,
                    $"节点 {child.Path} 与其他节点共享同一个 SerializedProperty 实例。");
            }
        }

        #endregion

        #region 树的归属

        /// <summary>
        /// 树里的每个节点都能回到它所属的树——**包括分组装配期才挂进来的分组节点**。
        /// <para>
        /// 这条是「处理器在树构造之后跑」那套顺序的地基：需要目标对象的处理器
        /// （按钮族按名解析方法、条件族定位序列化对象）只有这一条路能拿到树。
        /// 分组节点走的是 <c>AddChild</c> 传播，与构造期的递归回填是两条不同的路径，
        /// 故用一棵**带分组**的树来钉。
        /// </para>
        /// </summary>
        [Test]
        public void Build_每个节点都能回到所属的树()
        {
            var owner = ScriptableObject.CreateInstance<OwnershipFixture>();

            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(owner)))
                {
                    var visited = 0;
                    AssertOwnedBy(tree.Root, tree, ref visited);

                    Assert.That(visited, Is.GreaterThan(2), "至少该有根、分组、成员三个节点，用例才验到了东西。");
                    Assert.That(tree.Root.Kind, Is.EqualTo(InspectorPropertyKind.Root));
                }
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>目标对象列表来自序列化对象，单目标时只有一个。</summary>
        [Test]
        public void Build_目标对象列表与序列化对象一致()
        {
            var serializedObject = new SerializedObject(_target);

            using (var tree = PropertyTree.Create(serializedObject))
            {
                Assert.That(tree.Targets.Length, Is.EqualTo(1));
                Assert.That(tree.Targets[0], Is.SameAs(_target));
            }
        }

        /// <summary>多选时列表含全部目标——按钮「对每个目标各调用一次」靠的就是它。</summary>
        [Test]
        public void Build_多选时目标列表含全部目标()
        {
            var second = ScriptableObject.CreateInstance<BuilderFixture>();

            try
            {
                var serializedObject = new SerializedObject(new Object[] { _target, second });

                using (var tree = PropertyTree.Create(serializedObject))
                {
                    Assert.That(tree.Targets.Length, Is.EqualTo(2));
                    Assert.That(tree.Targets, Has.Member(_target));
                    Assert.That(tree.Targets, Has.Member(second));
                }
            }
            finally
            {
                Object.DestroyImmediate(second);
            }
        }

        /// <summary>默认记 Undo；窗口路径由宿主置为假（见 PropertyTreeHostTests）。</summary>
        [Test]
        public void Build_默认记Undo()
        {
            using (var tree = PropertyTree.Create(new SerializedObject(_target)))
            {
                Assert.That(tree.UndoEnabled, Is.True);
            }
        }

        #endregion

        #region 参数防御

        /// <summary>
        /// 传 null 必须报错而不是返回一棵空树。
        /// </summary>
        [Test]
        public void Create_NullSerializedObject_Throws()
        {
            Assert.That(() => PropertyTree.Create(null), Throws.ArgumentNullException);
        }

        #endregion

        #region Private Helpers

        /// <summary>构建被测的树。</summary>
        /// <returns>属性树。</returns>
        private PropertyTree BuildTree()
        {
            return PropertyTree.Create(new SerializedObject(_target));
        }

        /// <summary>递归断言每个节点都指向同一棵树，并数一数总共走了几个节点。</summary>
        /// <param name="node">当前节点。</param>
        /// <param name="expected">期望所属的树。</param>
        /// <param name="visited">累计访问数。</param>
        private static void AssertOwnedBy(InspectorProperty node, PropertyTree expected, ref int visited)
        {
            visited++;
            Assert.That(node.Owner, Is.SameAs(expected), $"节点「{node.Path}」没有指回它所属的树。");

            for (var i = 0; i < node.Children.Count; i++)
            {
                AssertOwnedBy(node.Children[i], expected, ref visited);
            }
        }

        /// <summary>取根下所有成员的路径。</summary>
        /// <param name="tree">属性树。</param>
        /// <returns>路径列表。</returns>
        private static List<string> PathsOf(PropertyTree tree)
        {
            var paths = new List<string>();
            foreach (var child in tree.Root.Children)
            {
                paths.Add(child.Path);
            }

            return paths;
        }

        #endregion
    }

    /// <summary>
    /// 构建器测试用的资产。
    /// <para>
    /// 字段刻意覆盖「应出现」「应出现但为私有」「不应出现」三类，
    /// 以便一次验证可见性判断是照搬 Unity 的而不是自己猜的。
    /// </para>
    /// </summary>
    internal sealed class BuilderFixture : ScriptableObject
    {
        /// <summary>第一个可见成员。</summary>
        public int first = 1;

        /// <summary>第二个可见成员，私有但可序列化。</summary>
        [SerializeField]
        private string _second = "two";

        /// <summary>第三个可见成员。</summary>
        public Vector3 third;

        /// <summary>被隐藏，不应出现在树里。</summary>
        [HideInInspector]
        public int hidden = 3;

        /// <summary>读一下私有字段，避免 CS0414 告警。</summary>
        public string Second => _second;
    }

    /// <summary>
    /// 验证「每个节点都能回到所属的树」用的资产：刻意**带一个分组**，
    /// 好让断言覆盖到分组装配期才挂进来的节点（它们走 <c>AddChild</c> 传播，与构造期回填不是同一条路）。
    /// </summary>
    internal sealed class OwnershipFixture : ScriptableObject
    {
        /// <summary>归入一个分组，于是树里会多出一个分组节点。</summary>
        [BoxGroup("组")]
        public int grouped;
    }
}
