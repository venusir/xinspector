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
}
