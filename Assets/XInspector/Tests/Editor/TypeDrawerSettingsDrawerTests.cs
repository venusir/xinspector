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
    /// <c>[TypeDrawerSettings]</c> 的绘制器与候选集：链装配、候选过滤、菜单构建、误用告警。
    /// <para>
    /// 与既有弹层型特性同一套口径：**不测 IMGUI**——只读禁用、按钮文本、菜单弹层与渲染一律
    /// 不写假用例；能无头断言的（链上有没有它、纯函数、构建期告警）在这里，其余的留目视。
    /// </para>
    /// </summary>
    [TestFixture]
    public class TypeDrawerSettingsDrawerTests
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

        #region 误用告警（裸 System.Type 字段）

        /// <summary>
        /// 裸 <c>System.Type</c> 字段**根本没有节点**（不进序列化数据），故告警只能在**构建期**发；
        /// 文案要直接点名「要加 <c>[SerializeReference]</c>」。
        /// </summary>
        [Test]
        public void 裸类型字段告警并说明要加SerializeReference()
        {
            LogAssert.Expect(LogType.Warning, new Regex("\\[SerializeReference\\]"));

            var target = ScriptableObject.CreateInstance<TypeDrawerFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(Find(tree.Root, "bare"), Is.Null, "裸 System.Type 字段没有节点。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>正确形态（<c>[SerializeReference]</c>）不告警。</summary>
        [Test]
        public void 托管引用形态不告警()
        {
            LogAssert.NoUnexpectedReceived();

            var target = ScriptableObject.CreateInstance<TypeDrawerFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(Find(tree.Root, "chosen"), Is.Not.Null, "托管引用的类型槽位进了树。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 链装配与判据

        /// <summary>类型槽位上挂着绘制器，且排在末端之前（替换型必须自己画完还要接上末端）。</summary>
        [Test]
        public void 绘制器在链上且排在末端之前()
        {
            var target = ScriptableObject.CreateInstance<TypeDrawerFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "chosen");
                    var index = IndexOfDrawer<TypeDrawerSettingsDrawer>(node);

                    Assert.That(index, Is.GreaterThanOrEqualTo(0), "绘制器在链上。");
                    Assert.That(index, Is.LessThan(node.Chain.Entries.Length - 1), "末端还在它后面。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// <b>钉住（P1 实测）</b>：类型槽位**不展开**——它是托管引用，但按「派生」扫不出用户成员，
        /// 不会被多态管线展开成 RuntimeType 的空壳子树。
        /// </summary>
        /// <remarks>
        /// 这条变红＝类型槽位开始展开成空壳子树：回去在
        /// <c>NestedMemberExpansion.IsCompositeCandidate</c> 的多态支加一道
        /// 「值是 <c>System.Type</c> 就不展开」的前置（见 Pipeline §三十二）。
        /// </remarks>
        [Test]
        public void 类型槽位不展开()
        {
            var target = ScriptableObject.CreateInstance<TypeDrawerFixture>();
            target.chosen = typeof(TypeDrawerFixtureCircle);
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "chosen");

                    Assert.That(node.Children.Count, Is.EqualTo(0), "类型槽位没有子节点。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>槽位有值时判据照旧成立——`IsTypeSlot` 传的是**字段的声明类型**，与槽位状态无关。</summary>
        [Test]
        public void 槽位判据只认字段的声明类型()
        {
            var target = ScriptableObject.CreateInstance<TypeDrawerFixture>();
            target.chosen = typeof(TypeDrawerFixtureCircle);
            try
            {
                using (var tree = BuildTree(target))
                {
                    var chosen = Find(tree.Root, "chosen").ValueEntry.SerializedProperty;
                    var notASlot = Find(tree.Root, "notATypeSlot").ValueEntry.SerializedProperty;

                    Assert.That(TypeSelectorTarget.IsTypeSlot(chosen, typeof(Type)), Is.True);
                    Assert.That(
                        TypeSelectorTarget.IsTypeSlot(notASlot, typeof(ITypeDrawerFixtureShape)),
                        Is.False,
                        "托管引用但不是类型槽位——绘制器会退回原生那一行。");
                    Assert.That(TypeSelectorTarget.CanSelect(chosen), Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>多选混合态（同类型、两个实例也是混合）不能弹本包的选择器。</summary>
        [Test]
        public void 多选混合态不弹选择器()
        {
            var a = ScriptableObject.CreateInstance<TypeDrawerFixture>();
            var b = ScriptableObject.CreateInstance<TypeDrawerFixture>();
            a.chosen = typeof(TypeDrawerFixtureCircle);
            b.chosen = typeof(TypeDrawerFixtureSquare);
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(new Object[] { a, b })))
                {
                    var chosen = Find(tree.Root, "chosen").ValueEntry.SerializedProperty;

                    Assert.That(chosen.hasMultipleDifferentValues, Is.True, "多选值不一致（这里是两个不同实现）报混合。");
                    Assert.That(TypeSelectorTarget.CanSelect(chosen), Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        #endregion

        #region 纯函数：候选归类与过滤

        /// <summary>归类：六种形状各命中哪几位（泛型接口同时命中两位；静态类归抽象）。</summary>
        [Test]
        public void 归类命中正确的位()
        {
            Assert.That(
                TypeCandidateFilter.KindOf(typeof(TypeDrawerFixtureShapeBase)),
                Is.EqualTo(TypeInclusionFilter.IncludeAbstracts));
            Assert.That(
                TypeCandidateFilter.KindOf(typeof(TypeDrawerFixtureCircle)),
                Is.EqualTo(TypeInclusionFilter.IncludeConcreteTypes));
            Assert.That(
                TypeCandidateFilter.KindOf(typeof(ITypeDrawerFixtureShape)),
                Is.EqualTo(TypeInclusionFilter.IncludeInterfaces));
            Assert.That(
                TypeCandidateFilter.KindOf(typeof(System.Collections.Generic.List<>)),
                Is.EqualTo(TypeInclusionFilter.IncludeConcreteTypes | TypeInclusionFilter.IncludeGenerics),
                "开放泛型的具体类：具体 + 泛型。");
            Assert.That(
                TypeCandidateFilter.KindOf(typeof(ITypeDrawerFixtureGeneric<>)),
                Is.EqualTo(TypeInclusionFilter.IncludeInterfaces | TypeInclusionFilter.IncludeGenerics),
                "泛型接口同时命中两位——位标志的描述能力正在于此。");
            Assert.That(
                TypeCandidateFilter.KindOf(typeof(TypeDrawerFixtureStatic)),
                Is.EqualTo(TypeInclusionFilter.IncludeAbstracts),
                "静态类在 IL 里是 abstract sealed，归抽象（自定边界）。");
        }

        /// <summary>过滤：求交非空；<c>None</c> 一个都不收、<c>IncludeAll</c> 全收。</summary>
        [Test]
        public void 过滤是求交非空()
        {
            var concrete = typeof(TypeDrawerFixtureCircle);
            var iface = typeof(ITypeDrawerFixtureShape);

            Assert.That(TypeCandidateFilter.IsIncluded(concrete, TypeInclusionFilter.None), Is.False);
            Assert.That(TypeCandidateFilter.IsIncluded(iface, TypeInclusionFilter.None), Is.False);

            Assert.That(TypeCandidateFilter.IsIncluded(concrete, TypeInclusionFilter.IncludeAll), Is.True);
            Assert.That(TypeCandidateFilter.IsIncluded(iface, TypeInclusionFilter.IncludeAll), Is.True);

            Assert.That(TypeCandidateFilter.IsIncluded(concrete, TypeInclusionFilter.IncludeConcreteTypes), Is.True);
            Assert.That(TypeCandidateFilter.IsIncluded(iface, TypeInclusionFilter.IncludeConcreteTypes), Is.False);

            Assert.That(
                TypeCandidateFilter.IsIncluded(
                    concrete, TypeInclusionFilter.IncludeAbstracts | TypeInclusionFilter.IncludeInterfaces),
                Is.False,
                "两位都不命中具体类。");
            Assert.That(
                TypeCandidateFilter.IsIncluded(
                    iface, TypeInclusionFilter.IncludeAbstracts | TypeInclusionFilter.IncludeInterfaces),
                Is.True);
        }

        /// <summary>噪音：闭包类与匿名类型剔除，普通类型保留。</summary>
        [Test]
        public void 编译器生成的噪音被剔除()
        {
            var captured = 1;
            Func<int> closure = () => captured;

            Assert.That(
                TypeCandidateFilter.IsNoise(closure.Target.GetType()),
                Is.True,
                "闭包显示类是编译器生成的（委托本身的类型是 Func<int>，闭包类在 Target 上）。");
            Assert.That(TypeCandidateFilter.IsNoise(new { Value = 1 }.GetType()), Is.True, "匿名类型同理。");
            Assert.That(TypeCandidateFilter.IsNoise(typeof(TypeDrawerFixtureCircle)), Is.False);
            Assert.That(TypeCandidateFilter.IsNoise(typeof(System.Collections.Generic.List<int>)), Is.False);
        }

        /// <summary>Apply：排序按全名序数；全名相同的第二条计入重复并丢掉。</summary>
        [Test]
        public void 应用时排序并去重()
        {
            var result = TypeCandidateFilter.Apply(
                new[]
                {
                    typeof(TypeDrawerFixtureSquare),
                    typeof(TypeDrawerFixtureCircle),
                    typeof(TypeDrawerFixtureCircle), // 全名相同 ⇒ 第二条算重复
                },
                TypeInclusionFilter.IncludeAll,
                out var duplicates);

            Assert.That(duplicates, Is.EqualTo(1));
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(
                result[0].Name,
                Is.EqualTo("TypeDrawerFixtureCircle"),
                "序数比较：Circle 在 Square 前面。");
        }

        #endregion

        #region 纯函数：菜单路径与显示名

        /// <summary>显示名：去命名空间、嵌套 <c>+</c> 换 <c>.</c>、元数记号换 <c>&lt;&gt;</c>。</summary>
        [Test]
        public void 显示名去掉命名空间与记号()
        {
            Assert.That(TypeSelectorOptions.DisplayName(typeof(TypeDrawerFixtureCircle)),
                Is.EqualTo("TypeDrawerFixtureCircle"));
            Assert.That(TypeSelectorOptions.DisplayName(typeof(TypeDrawerFixtureNested.Child)),
                Is.EqualTo("TypeDrawerFixtureNested.Child"));
            Assert.That(TypeSelectorOptions.DisplayName(typeof(System.Collections.Generic.List<>)),
                Is.EqualTo("List<>"));
            Assert.That(
                TypeSelectorOptions.DisplayName(typeof(System.Collections.Generic.Dictionary<,>.Enumerator)),
                Is.EqualTo("Dictionary<>.Enumerator"),
                "嵌套类型 + 元数记号一起出现。");
            Assert.That(
                TypeSelectorOptions.DisplayName(typeof(TypeSelectorGlobalProbe)),
                Is.EqualTo("TypeSelectorGlobalProbe"),
                "全局命名空间的类型没有前缀可去。");
        }

        /// <summary>菜单路径：命名空间各段分层 + 叶名；全局命名空间直接落在根上。</summary>
        [Test]
        public void 菜单路径按命名空间分层()
        {
            Assert.That(
                TypeSelectorOptions.MenuPath(typeof(TypeDrawerFixtureCircle)),
                Is.EqualTo("XInspector/Tests/Editor/TypeDrawerFixtureCircle"));
            Assert.That(
                TypeSelectorOptions.MenuPath(typeof(TypeSelectorGlobalProbe)),
                Is.EqualTo("TypeSelectorGlobalProbe"));
        }

        /// <summary>选项表：只有当前值带勾；空槽位时一个都不勾。</summary>
        [Test]
        public void 选项表标出当前值()
        {
            var candidates = new[] { typeof(TypeDrawerFixtureCircle), typeof(TypeDrawerFixtureSquare) };

            var withCurrent = TypeSelectorOptions.Build(candidates, typeof(TypeDrawerFixtureSquare));
            Assert.That(withCurrent[0].IsCurrent, Is.False);
            Assert.That(withCurrent[1].IsCurrent, Is.True);

            var without = TypeSelectorOptions.Build(candidates, null);
            Assert.That(without[0].IsCurrent, Is.False);
            Assert.That(without[1].IsCurrent, Is.False, "空槽位没有当前值。");
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

        #endregion
    }

    #region Fixtures

    /// <summary>类型选择器的夹具：误用形态与正确形态各一。</summary>
    [HideMonoScript]
    internal sealed class TypeDrawerFixture : ScriptableObject
    {
        /// <summary>误用形态：裸 <c>System.Type</c>——没有节点，选择器不生效（构建期告警）。</summary>
        [TypeDrawerSettings]
        public Type bare;

        /// <summary>正确形态：托管引用的 <c>System.Type</c>，带基类型约束。</summary>
        [SerializeReference]
        [TypeDrawerSettings(BaseType = typeof(ITypeDrawerFixtureShape))]
        public Type chosen;

        /// <summary>对照：无约束的托管引用类型槽位。</summary>
        [SerializeReference]
        [TypeDrawerSettings]
        public Type loose;

        /// <summary>对照：非类型槽位的托管引用（选择器应当退回原生）。</summary>
        [SerializeReference]
        [TypeDrawerSettings]
        public ITypeDrawerFixtureShape notATypeSlot;
    }

    /// <summary>夹具的基类型。</summary>
    internal interface ITypeDrawerFixtureShape
    {
    }

    /// <summary>一个具体实现。</summary>
    internal sealed class TypeDrawerFixtureCircle : ITypeDrawerFixtureShape
    {
    }

    /// <summary>另一个具体实现。</summary>
    internal sealed class TypeDrawerFixtureSquare : ITypeDrawerFixtureShape
    {
    }

    /// <summary>抽象基类——归类那一格用它。</summary>
    internal abstract class TypeDrawerFixtureShapeBase
    {
    }

    /// <summary>静态类——归「抽象」（IL 里是 <c>abstract sealed</c>，自定边界）。</summary>
    internal static class TypeDrawerFixtureStatic
    {
    }

    /// <summary>开放泛型接口——同时命中「泛型」与「接口」。</summary>
    internal interface ITypeDrawerFixtureGeneric<T>
    {
    }

    /// <summary>带嵌套类型的夹具——显示名那条用它。</summary>
    internal sealed class TypeDrawerFixtureNested
    {
        /// <summary>嵌套类型。</summary>
        public sealed class Child
        {
        }
    }

    #endregion
}

/// <summary>全局命名空间的夹具——菜单路径「没有前缀可去」那一格用它。</summary>
internal sealed class TypeSelectorGlobalProbe
{
}
