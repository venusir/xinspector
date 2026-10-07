using System;
using System.Collections.Generic;
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
    /// <c>[PolymorphicDrawerSettings]</c> 的行：链装配（末端与绘制器各在哪）、判据档位、
    /// 候选与文本的纯逻辑。
    /// <para>
    /// 与既有弹层型特性同一套口径：**不测 IMGUI**——按钮文本、菜单弹层、只读禁用的观感
    /// 一律不写假用例；能无头断言的（链上有没有它、纯函数、构建期告警）在这里。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PolymorphicRowTests
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

        #region 误用告警（没有 [SerializeReference] 的引用字段）

        /// <summary>
        /// 没加 <c>[SerializeReference]</c> 的接口字段**根本没有节点**（不进序列化数据），
        /// 故告警只能在**构建期**发；文案要直接点名「要加 <c>[SerializeReference]</c>」。
        /// </summary>
        [Test]
        public void 裸引用字段告警并说明要加SerializeReference()
        {
            LogAssert.Expect(LogType.Warning, new Regex("\\[SerializeReference\\]"));

            var target = ScriptableObject.CreateInstance<PolymorphicRowMisuseFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(Find(tree.Root, "bare"), Is.Null, "没加 [SerializeReference] 的字段没有节点。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>正确形态（<c>[SerializeReference]</c>）在**空槽位**时不告警（那是常规状态）。</summary>
        [Test]
        public void 托管引用形态不告警()
        {
            LogAssert.NoUnexpectedReceived();

            var target = ScriptableObject.CreateInstance<PolymorphicRowFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(Find(tree.Root, "empty"), Is.Not.Null, "托管引用的槽位进了树。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 链装配

        /// <summary>展开态的末端是**自绘末端**（精确类型——继承会让 <c>InstanceOf</c> 两边都真）。</summary>
        [Test]
        public void 展开态的末端是自绘末端()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicRowFixture>();
            target.expanded = new PolymorphicRowCircle();
            target.untouched = new PolymorphicRowCircle();
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(Find(tree.Root, "expanded").Children.Count, Is.GreaterThan(0), "起点：展开了。");
                    Assert.That(
                        ChainTail(Find(tree.Root, "expanded")),
                        Is.TypeOf<PolymorphicRowTerminalDrawer>(),
                        "标了特性的字段用自绘末端。");
                    Assert.That(
                        ChainTail(Find(tree.Root, "untouched")),
                        Is.TypeOf<ManagedReferenceTerminalDrawer>(),
                        "对照：**没标**特性的字段仍是原末端（一个字段都不许被牵连）。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>空槽位：末端仍是原生兜底，链上挂着自绘绘制器（它画那一行）。</summary>
        [Test]
        public void 空槽位绘制器在链上且末端仍是兜底()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicRowFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "empty");
                    var index = IndexOfDrawer<PolymorphicDrawerSettingsDrawer>(node);

                    Assert.That(index, Is.GreaterThanOrEqualTo(0), "绘制器在链上。");
                    Assert.That(index, Is.LessThan(node.Chain.Entries.Length - 1), "末端还在它后面。");
                    Assert.That(ChainTail(node), Is.TypeOf<UnityFallbackDrawer>(), "空槽位末端不变。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 判据档位（Decide）

        /// <summary>档位：空槽位本包画；已展开归末端；多选退回；用不到本包退回；类型槽位退回。</summary>
        [Test]
        public void 判据档位逐格()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicRowFixture>();
            target.expanded = new PolymorphicRowCircle();
            target.plain = new PolymorphicRowPlain();
            target.both = typeof(PolymorphicRowCircle);
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(DispositionOf(tree, "empty"), Is.EqualTo(PolymorphicRowDisposition.DrawRow));
                    Assert.That(DispositionOf(tree, "expanded"), Is.EqualTo(PolymorphicRowDisposition.TerminalOwnsRow));
                    Assert.That(
                        DispositionOf(tree, "plain"),
                        Is.EqualTo(PolymorphicRowDisposition.FallbackNotTakenOver));
                    Assert.That(
                        DispositionOf(tree, "both"),
                        Is.EqualTo(PolymorphicRowDisposition.FallbackTypeSlot),
                        "类型槽位归 [TypeDrawerSettings]（同一字段两个都标时它赢，且这里不乱告警）。");
                    Assert.That(
                        DispositionOf(tree, "notManaged"),
                        Is.EqualTo(PolymorphicRowDisposition.FallbackNotBacked),
                        "标在 int 上：没有托管引用后端。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>多选值不一致：退回，**不告警**（常规状态，与 <c>[TypeDrawerSettings]</c> 同款）。</summary>
        [Test]
        public void 多选时退回且不告警()
        {
            LogAssert.NoUnexpectedReceived();

            var a = ScriptableObject.CreateInstance<PolymorphicRowFixture>();
            var b = ScriptableObject.CreateInstance<PolymorphicRowFixture>();
            a.empty = new PolymorphicRowCircle();
            b.empty = new PolymorphicRowPlain();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(new Object[] { a, b })))
                {
                    Assert.That(
                        DispositionOf(tree, "empty"),
                        Is.EqualTo(PolymorphicRowDisposition.FallbackMultiSelect));
                }
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        /// <summary>撞上守卫那一档：退回、**不额外告警**（构建期已响过一条）。</summary>
        [Test]
        public void 守卫档退回()
        {
            LogAssert.Expect(LogType.Warning, new Regex("已经在祖先的多态引用链上出现过"));

            var target = ScriptableObject.CreateInstance<PolymorphicRowFixture>();
            var node = new PolymorphicRowChain { id = 1 };
            node.next = node; // 自引用 ⇒ 下一层被类型重复挡下
            target.chain = node;
            try
            {
                using (var tree = BuildTree(target))
                {
                    var next = Find(Find(tree.Root, "chain"), "chain.next");

                    Assert.That(next, Is.Not.Null, "回边自己进了树。");
                    Assert.That(next.Children.Count, Is.EqualTo(0), "但被守卫挡在展开之外。");
                    Assert.That(
                        DispositionOf(next),
                        Is.EqualTo(PolymorphicRowDisposition.FallbackGuarded));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 纯函数（文本 / 只读 / 重写 / 候选）

        /// <summary>行文本：空槽位「（无）」（不带后缀）；开关只给有值时加基类型。</summary>
        [Test]
        public void 行文本按开关带基类型()
        {
            var declared = typeof(IPolymorphicRowShape);

            Assert.That(PolymorphicRow.TextFor(null, declared, true), Is.EqualTo("（无）"), "空槽位不带后缀。");
            Assert.That(
                PolymorphicRow.TextFor(typeof(PolymorphicRowCircle), declared, false),
                Is.EqualTo("PolymorphicRowCircle"));
            Assert.That(
                PolymorphicRow.TextFor(typeof(PolymorphicRowCircle), declared, true),
                Is.EqualTo("PolymorphicRowCircle （IPolymorphicRowShape）"));
        }

        /// <summary>只读判据：状态只读恒锁；开关只在**有值**时锁。</summary>
        [Test]
        public void 只读判据只在有值时被开关锁()
        {
            Assert.That(PolymorphicRow.ShouldDisableRow(false, false, true), Is.False);
            Assert.That(PolymorphicRow.ShouldDisableRow(false, true, false), Is.False, "没有值时开关不锁。");
            Assert.That(PolymorphicRow.ShouldDisableRow(false, true, true), Is.True);
            Assert.That(PolymorphicRow.ShouldDisableRow(true, false, false), Is.True, "状态只读恒锁。");
        }

        /// <summary>点当前类型 = 无操作（不拿同类型新实例换掉用户的值）；空槽位与换类型都要重写。</summary>
        [Test]
        public void 重写判据()
        {
            Assert.That(
                PolymorphicRow.ShouldRewrite(typeof(PolymorphicRowCircle), typeof(PolymorphicRowCircle)),
                Is.False);
            Assert.That(
                PolymorphicRow.ShouldRewrite(typeof(PolymorphicRowCircle), typeof(PolymorphicRowPlain)),
                Is.True);
            Assert.That(PolymorphicRow.ShouldRewrite(null, typeof(PolymorphicRowCircle)), Is.True);
        }

        /// <summary>可实例化判据：造不出来的那些一律剔除。</summary>
        [Test]
        public void 可实例化判据()
        {
            Assert.That(PolymorphicCandidateFilter.IsInstantiable(typeof(PolymorphicRowCircle)), Is.True);
            Assert.That(PolymorphicCandidateFilter.IsInstantiable(typeof(int)), Is.True, "值类型照收。");
            Assert.That(PolymorphicCandidateFilter.IsInstantiable(typeof(List<int>)), Is.True);

            Assert.That(PolymorphicCandidateFilter.IsInstantiable(typeof(IPolymorphicRowShape)), Is.False);
            Assert.That(PolymorphicCandidateFilter.IsInstantiable(typeof(PolymorphicRowAbstract)), Is.False);
            Assert.That(PolymorphicCandidateFilter.IsInstantiable(typeof(List<>)), Is.False, "开放泛型。");
            Assert.That(
                PolymorphicCandidateFilter.IsInstantiable(typeof(MonoBehaviour)),
                Is.False,
                "UnityEngine.Object 一族造不出来（也不该 new）。");
        }

        /// <summary>
        /// 候选表：**声明类型自己是具体类时也在候选里**（`[SerializeReference] Circle c;` 的唯一选项）；
        /// <c>Exclude</c> 档剔掉没有无参构造的实现。
        /// </summary>
        [Test]
        public void 候选表含声明类型自己且按档收窄()
        {
            var candidates = PolymorphicCandidateFilter.Candidates(
                typeof(IPolymorphicRowShape), NonDefaultConstructorPreference.ConstructIdeal, out _, out var error);

            Assert.That(error, Is.Null);
            Assert.That(candidates, Has.Member(typeof(PolymorphicRowCircle)));
            Assert.That(candidates, Has.Member(typeof(PolymorphicRowNoDefaultCtor)), "ConstructIdeal 档不剔它。");

            var concreteDeclared = PolymorphicCandidateFilter.Candidates(
                typeof(PolymorphicRowCircle), NonDefaultConstructorPreference.ConstructIdeal, out _, out _);

            Assert.That(concreteDeclared, Has.Member(typeof(PolymorphicRowCircle)), "声明类型自己是候选。");

            var excluded = PolymorphicCandidateFilter.Candidates(
                typeof(IPolymorphicRowShape), NonDefaultConstructorPreference.Exclude, out _, out _);

            Assert.That(excluded, Has.No.Member(typeof(PolymorphicRowNoDefaultCtor)), "Exclude 档剔掉。");
            Assert.That(excluded, Has.Member(typeof(PolymorphicRowCircle)), "有无参构造的照留。");
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

        /// <summary>链尾的绘制器。</summary>
        /// <param name="property">节点。</param>
        /// <returns>链尾绘制器。</returns>
        private static object ChainTail(InspectorProperty property)
        {
            return property.Chain.Entries[property.Chain.Count - 1].Drawer;
        }

        /// <summary>在节点的链上查找指定类型绘制器的下标。</summary>
        /// <typeparam name="T">绘制器类型。</typeparam>
        /// <param name="property">目标节点。</param>
        /// <returns>下标；不存在返回 -1。</returns>
        private static int IndexOfDrawer<T>(InspectorProperty property) where T : XInspectorDrawer
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

        /// <summary>算一个节点的判据档位（纯函数，直接问）。</summary>
        /// <param name="tree">树。</param>
        /// <param name="path">节点路径。</param>
        /// <returns>档位。</returns>
        private static PolymorphicRowDisposition DispositionOf(PropertyTree tree, string path)
        {
            return DispositionOf(Find(tree.Root, path));
        }

        /// <summary>算一个节点的判据档位。</summary>
        /// <param name="node">节点。</param>
        /// <returns>档位。</returns>
        private static PolymorphicRowDisposition DispositionOf(InspectorProperty node)
        {
            return PolymorphicRow.Decide(
                node, node.ValueEntry?.SerializedProperty, PolymorphicRow.DeclaredTypeOf(node));
        }

        #endregion
    }

    #region Fixtures

    /// <summary>多态行一族的夹具。</summary>
    [HideMonoScript]
    internal sealed class PolymorphicRowFixture : ScriptableObject
    {
        /// <summary>标了特性 + 空槽位：绘制器画行，末端仍是原生兜底。</summary>
        [SerializeReference]
        [PolymorphicDrawerSettings]
        public IPolymorphicRowShape empty;

        /// <summary>标了特性 + 有值（用得到本包）：末端换成自绘末端。</summary>
        [SerializeReference]
        [PolymorphicDrawerSettings]
        public IPolymorphicRowShape expanded;

        /// <summary>标了特性 + 有值（**用不到本包**）：回退原生。</summary>
        [SerializeReference]
        [PolymorphicDrawerSettings]
        public IPolymorphicRowShape plain;

        /// <summary>对照：**没标**特性 + 有值（用得到本包）——末端必须还是原末端。</summary>
        [SerializeReference]
        public IPolymorphicRowShape untouched;

        /// <summary>共存：类型槽位上两个选择器特性都标（<c>[TypeDrawerSettings]</c> 赢）。</summary>
        [SerializeReference]
        [TypeDrawerSettings]
        [PolymorphicDrawerSettings]
        public Type both;

        /// <summary>标了特性但**不是托管引用**（<c>int</c>）——绘制期守卫那一格。</summary>
        [PolymorphicDrawerSettings]
        public int notManaged;

        /// <summary>自引用的链——守卫那一档用它（下一层被类型重复挡下）。</summary>
        [SerializeReference]
        [PolymorphicDrawerSettings]
        public PolymorphicRowChain chain;
    }

    /// <summary>误用告警的夹具（单独一个——共享夹具上挂一条「没加 [SerializeReference]」会让
    /// 同 fixture 里用 <c>NoUnexpectedReceived</c> 的用例全红）。</summary>
    [HideMonoScript]
    internal sealed class PolymorphicRowMisuseFixture : ScriptableObject
    {
        /// <summary>误用形态：接口字段没加 <c>[SerializeReference]</c>——没有节点，构建期告警。</summary>
        [PolymorphicDrawerSettings]
        public IPolymorphicRowShape bare;
    }

    /// <summary>夹具的槽位类型。</summary>
    internal interface IPolymorphicRowShape
    {
    }

    /// <summary>用得到本包的具体类型（带分组，展开后看得见子节点）。</summary>
    [Serializable]
    internal sealed class PolymorphicRowCircle : IPolymorphicRowShape
    {
        /// <summary>分组里的成员。</summary>
        [BoxGroup("几何")]
        public float radius = 1f;
    }

    /// <summary>**用不到本包**的具体类型（安全阀的对照）。</summary>
    [Serializable]
    internal sealed class PolymorphicRowPlain : IPolymorphicRowShape
    {
        /// <summary>普通字段。</summary>
        public float weight;
    }

    /// <summary>抽象实现——「可实例化」判据那一格。</summary>
    internal abstract class PolymorphicRowAbstract : IPolymorphicRowShape
    {
    }

    /// <summary>没有公开无参构造的实现——<c>Exclude</c> 档那一格。</summary>
    [Serializable]
    internal sealed class PolymorphicRowNoDefaultCtor : IPolymorphicRowShape
    {
        /// <summary>带参构造写下的值。</summary>
        public int value;

        /// <summary>唯一的构造（带参）。</summary>
        /// <param name="value">值。</param>
        public PolymorphicRowNoDefaultCtor(int value)
        {
            this.value = value;
        }
    }

    /// <summary>自引用的链节点（字段自己带特性，否则不会展开、也就测不到守卫）。</summary>
    [Serializable]
    internal sealed class PolymorphicRowChain : IPolymorphicRowShape
    {
        /// <summary>普通字段。</summary>
        public int id;

        /// <summary>指回同类型的多态引用。</summary>
        [SerializeReference]
        [PolymorphicDrawerSettings]
        public PolymorphicRowChain next;
    }

    #endregion
}
