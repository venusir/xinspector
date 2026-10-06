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
    /// <c>[Searchable]</c>：展开判据、注入、匹配语义，以及那条最容易写错的设计决定——
    /// **过滤是策略不是可见性**。
    /// <para>
    /// 不测 IMGUI——断言的是「谁被算作命中」「末端怎么画子节点」「被筛掉的节点还可见吗」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class SearchableTests
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

        #region 展开与注入

        /// <summary>
        /// 只带 <c>[Searchable]</c> 的复合字段也会展开——不展开就没有子成员可过滤，
        /// 而这个特性比「外观保持原生」更值钱（它是用户显式写下的）。
        /// </summary>
        [Test]
        public void 仅带搜索的复合类型也会展开()
        {
            var target = ScriptableObject.CreateInstance<SearchableExpansionFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var stats = Find(tree.Root, "stats");

                    Assert.That(stats.Children.Count, Is.EqualTo(2));
                    Assert.That(
                        stats.Chain.Entries[stats.Chain.Count - 1].Drawer,
                        Is.InstanceOf<CompositeMemberTerminalDrawer>(),
                        "展开的复合成员接复合末端（搜索框与过滤都在那一格）。");

                    var native = Find(tree.Root, "native");

                    Assert.That(native.Children.Count, Is.EqualTo(0), "对照：同样的类型没标搜索就不展开。");
                    Assert.That(
                        native.Chain.Entries[native.Chain.Count - 1].Drawer,
                        Is.InstanceOf<UnityFallbackDrawer>(),
                        "整份仍交给 Unity——「没用到本包的类型外观不变」这条契约不受影响。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>数组上没标列表设置时补一份——否则整份列表仍由 Unity 画，搜索无从谈起。</summary>
        [Test]
        public void 搜索会注入列表设置()
        {
            var target = ScriptableObject.CreateInstance<SearchableFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "numbers");

                    Assert.That(node.Attributes.Has<ListDrawerSettingsAttribute>(), Is.True);
                    Assert.That(IndexOf<ListDrawerSettingsDrawer>(node), Is.GreaterThanOrEqualTo(0));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>标在标量上：构建期告警（没有可筛选的子成员），不注入、不改外观。</summary>
        [Test]
        public void 标在标量上时告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex("只对复合成员与数组/List 有效"));

            var target = ScriptableObject.CreateInstance<SearchableOnScalarFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(Find(tree.Root, "count").Attributes.Has<ListDrawerSettingsAttribute>(), Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>标在多态引用上：告警里点明是 <c>[SerializeReference]</c> 那条边界。</summary>
        [Test]
        public void 标在多态引用上时告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex("多态引用"));

            var target = ScriptableObject.CreateInstance<SearchableOnReferenceFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(Find(tree.Root, "poly").Children.Count, Is.EqualTo(0));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>标在反射成员上：没有序列化后端，也就没有子成员。</summary>
        [Test]
        public void 标在反射成员上时告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex("需要 Unity 的序列化后端"));

            var target = ScriptableObject.CreateInstance<SearchableOnReflectedFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(Find(tree.Root, "Reflected").ValueEntry.SerializedProperty, Is.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 匹配语义

        /// <summary>大小写不敏感的子串；空查询恒真。</summary>
        [Test]
        public void 文本匹配忽略大小写且是子串()
        {
            Assert.That(SearchMatcher.TextMatches("heal", "Health"), Is.True);
            Assert.That(SearchMatcher.TextMatches("HEALTH", "health"), Is.True);
            Assert.That(SearchMatcher.TextMatches("ealt", "health"), Is.True);
            Assert.That(SearchMatcher.TextMatches("mana", "health"), Is.False);
            Assert.That(SearchMatcher.TextMatches(string.Empty, "health"), Is.True, "空查询不过滤。");
            Assert.That(SearchMatcher.TextMatches("a", null), Is.False);
        }

        /// <summary>空白查询不算「在生效」——搜索框里敲个空格不该把列表清空。</summary>
        [Test]
        public void 查询为空白时不生效()
        {
            Assert.That(SearchMatcher.IsActive(null), Is.False);
            Assert.That(SearchMatcher.IsActive("   "), Is.False);
            Assert.That(SearchMatcher.IsActive("a"), Is.True);
        }

        /// <summary>命中的叶子与它的祖先分组都留下，不相干的兄弟被筛掉。</summary>
        [Test]
        public void 命中的叶子与其祖先分组都保留()
        {
            var target = ScriptableObject.CreateInstance<SearchableFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var stats = Find(tree.Root, "stats");
                    var group = Find(stats, "stats/基础");
                    var scope = ScopeOf(stats, "heal");

                    Assert.That(scope.VisibilityOf(group), Is.EqualTo(SearchVisibility.Filtered), "靠后代命中活下来。");
                    Assert.That(scope.VisibilityOf(Find(group, "stats.health")), Is.EqualTo(SearchVisibility.KeepAll));
                    Assert.That(scope.VisibilityOf(Find(group, "stats.mana")), Is.EqualTo(SearchVisibility.Hidden));
                    Assert.That(scope.VisibilityOf(Find(stats, "stats.title")), Is.EqualTo(SearchVisibility.Hidden));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>分组**自己的名字**命中时整组保留——不然会得到一个空框。</summary>
        [Test]
        public void 分组标签命中时整组保留()
        {
            var target = ScriptableObject.CreateInstance<SearchableFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var stats = Find(tree.Root, "stats");
                    var group = Find(stats, "stats/基础");
                    var scope = ScopeOf(stats, "基础");

                    Assert.That(scope.VisibilityOf(group), Is.EqualTo(SearchVisibility.KeepAll));
                    Assert.That(
                        scope.VisibilityOf(Find(group, "stats.mana")),
                        Is.EqualTo(SearchVisibility.KeepAll),
                        "组内成员跟着整组留下。");
                    Assert.That(scope.VisibilityOf(Find(stats, "stats.title")), Is.EqualTo(SearchVisibility.Hidden));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **被筛掉的节点仍然可见。** 过滤走的是策略（末端跳过 Draw），不是可见性槽——
        /// 往那上面写会把 <c>[ShowIf]</c> 装的闭包永久顶掉。
        /// </summary>
        [Test]
        public void 被过滤掉的节点仍然可见()
        {
            var target = ScriptableObject.CreateInstance<SearchableFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var stats = Find(tree.Root, "stats");
                    var group = Find(stats, "stats/基础");
                    var mana = Find(group, "stats.mana");
                    var scope = ScopeOf(stats, "heal");

                    Assert.That(scope.VisibilityOf(mana), Is.EqualTo(SearchVisibility.Hidden));
                    Assert.That(mana.IsVisible, Is.True, "过滤不碰可见性槽。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>作用域沿父链上溯：孙子节点也找得到宿主那一份策略。</summary>
        [Test]
        public void 作用域沿父链上溯()
        {
            var target = ScriptableObject.CreateInstance<SearchableFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var stats = Find(tree.Root, "stats");
                    ScopeOf(stats, "heal");
                    var group = Find(stats, "stats/基础");

                    var scope = SearchScope.Find(Find(group, "stats.health"));

                    Assert.That(scope.IsActive, Is.True);
                    Assert.That(scope.ShouldDraw(Find(group, "stats.mana")), Is.False);
                    Assert.That(scope.ShouldDraw(Find(group, "stats.health")), Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>没有 <c>[Searchable]</c> 的树上，作用域恒不生效（不会误伤）。</summary>
        [Test]
        public void 没有搜索时作用域不生效()
        {
            var target = ScriptableObject.CreateInstance<SearchableExpansionFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    Assert.That(SearchScope.Find(Find(tree.Root, "native")).IsActive, Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 行过滤

        /// <summary>列表行按**值**匹配：元素标签是 <c>Element 0</c> 那种索引名，不能拿来当素材。</summary>
        [Test]
        public void 列表行按值匹配()
        {
            var target = ScriptableObject.CreateInstance<SearchableFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "loadout");
                    var state = StateOf(node, "剑");

                    Assert.That(state.EnsureListRows(node.ValueEntry.SerializedProperty), Is.EqualTo(new[] { true, false }));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>复合元素：下面任意一层的叶子命中，这一行就算命中。</summary>
        [Test]
        public void 复合元素按子字段匹配()
        {
            var target = ScriptableObject.CreateInstance<SearchableFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "rows");
                    var state = StateOf(node, "哥布");

                    Assert.That(state.EnsureListRows(node.ValueEntry.SerializedProperty), Is.EqualTo(new[] { false, true }));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>表格行按**任一单元格**的值匹配（列之外的字段不算——用户搜的是看得见的东西）。</summary>
        [Test]
        public void 表格行按单元格值匹配()
        {
            var target = ScriptableObject.CreateInstance<SearchableFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "table");
                    var model = node.State.Get<TableModel>();
                    var state = StateOf(node, "史莱");

                    Assert.That(model, Is.Not.Null, "表格模型要在（[TableList] 建的）。");
                    Assert.That(
                        state.EnsureTableRows(node.ValueEntry.SerializedProperty, model.Columns),
                        Is.EqualTo(new[] { true, false }));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>查询变了要重算——缓存不能一直认上一次的答案。</summary>
        [Test]
        public void 查询变化后重算()
        {
            var target = ScriptableObject.CreateInstance<SearchableFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "loadout");
                    var array = node.ValueEntry.SerializedProperty;
                    var state = StateOf(node, "剑");

                    Assert.That(state.EnsureListRows(array), Is.EqualTo(new[] { true, false }));

                    state.Query = "盾";
                    Assert.That(state.EnsureListRows(array), Is.EqualTo(new[] { false, true }));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>数组长度变了也要重算（掩码长度跟着走）。</summary>
        [Test]
        public void 数组长度变化后重算()
        {
            var target = ScriptableObject.CreateInstance<SearchableFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "loadout");
                    var array = node.ValueEntry.SerializedProperty;
                    var state = StateOf(node, "盾");

                    Assert.That(state.EnsureListRows(array).Length, Is.EqualTo(2));

                    array.arraySize = 3;
                    Assert.That(state.EnsureListRows(array).Length, Is.EqualTo(3), "新元素是末元素的副本，故也跟着命中。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>空查询不返回掩码（等于不过滤），一行都不命中时判定为假（宿主据此画提示）。</summary>
        [Test]
        public void 没有命中时给出提示判定()
        {
            var target = ScriptableObject.CreateInstance<SearchableFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "loadout");
                    var state = StateOf(node, "不存在的东西");

                    Assert.That(
                        SearchMatcher.HasAnyRow(state.EnsureListRows(node.ValueEntry.SerializedProperty)),
                        Is.False);

                    state.Query = "   ";
                    Assert.That(state.EnsureListRows(node.ValueEntry.SerializedProperty), Is.Null, "空白查询不产生掩码。");
                }
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

        /// <summary>给某个宿主编一份搜索状态并设好查询。</summary>
        /// <param name="host">挂着 <c>[Searchable]</c> 的节点。</param>
        /// <param name="query">查询。</param>
        /// <returns>状态。</returns>
        private static SearchFilterState StateOf(InspectorProperty host, string query)
        {
            var state = host.State.GetOrCreate<SearchFilterState>();
            state.Query = query;
            return state;
        }

        /// <summary>算好命中集，拿状态直接问（等价于绘制期末端做的事）。</summary>
        /// <param name="host">宿主节点。</param>
        /// <param name="query">查询。</param>
        /// <returns>已算好命中集的状态。</returns>
        private static SearchFilterState ScopeOf(InspectorProperty host, string query)
        {
            var state = StateOf(host, query);
            state.EnsureNodes(host);

            return state;
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

    /// <summary>嵌套类型的对照：左边那个字段只因为 <c>[Searchable]</c> 才展开。</summary>
    [Serializable]
    internal class SearchStats
    {
        /// <summary>分组里的第一个成员。</summary>
        [BoxGroup("基础")]
        public int health = 100;

        /// <summary>分组里的第二个成员。</summary>
        [BoxGroup("基础")]
        public int mana = 50;

        /// <summary>未分组的成员。</summary>
        public string title = "玩家";
    }

    /// <summary>一个本包特性都不带的嵌套类型。</summary>
    [Serializable]
    internal class PlainStats
    {
        /// <summary>一个字段。</summary>
        public int health = 100;

        /// <summary>另一个字段。</summary>
        public int mana = 50;
    }

    /// <summary>表格/列表的行类型。</summary>
    [Serializable]
    internal struct SearchRow
    {
        /// <summary>等级。</summary>
        public int level;

        /// <summary>名字。</summary>
        public string name;
    }

    /// <summary>搜索的对照资产。</summary>
    [HideMonoScript]
    internal sealed class SearchableFixture : ScriptableObject
    {
        /// <summary>没标列表设置的数组。</summary>
        [Searchable]
        public int[] numbers = { 1, 2, 3 };

        /// <summary>字符串数组：行按值匹配。</summary>
        [Searchable]
        public string[] loadout = { "剑", "盾" };

        /// <summary>复合元素：行按子字段匹配。</summary>
        [Searchable]
        public List<SearchRow> rows = new List<SearchRow>
        {
            new SearchRow { level = 1, name = "史莱姆" },
            new SearchRow { level = 2, name = "哥布林" },
        };

        /// <summary>表格：行按单元格匹配。</summary>
        [Searchable]
        [TableList]
        public List<SearchRow> table = new List<SearchRow>
        {
            new SearchRow { level = 1, name = "史莱姆" },
            new SearchRow { level = 2, name = "哥布林" },
        };

        /// <summary>复合成员：子节点里带分组。</summary>
        [Searchable]
        public SearchStats stats = new SearchStats();
    }

    /// <summary>展开判据的对照资产。</summary>
    [HideMonoScript]
    internal sealed class SearchableExpansionFixture : ScriptableObject
    {
        /// <summary>只因为 <c>[Searchable]</c> 才展开。</summary>
        [Searchable]
        public PlainStats stats = new PlainStats();

        /// <summary>对照：同样的类型没标搜索，整份交给 Unity。</summary>
        public PlainStats native = new PlainStats();
    }

    /// <summary>标在标量上的对照资产。</summary>
    [HideMonoScript]
    internal sealed class SearchableOnScalarFixture : ScriptableObject
    {
        /// <summary>标在标量上。</summary>
        [Searchable]
        public int count = 1;
    }

    /// <summary>标在多态引用上的对照资产。</summary>
    [HideMonoScript]
    internal sealed class SearchableOnReferenceFixture : ScriptableObject
    {
        /// <summary>多态引用：成员进不了树。</summary>
        [SerializeReference]
        [Searchable]
        public SearchStats poly = new SearchStats();
    }

    /// <summary>标在反射成员上的对照资产。</summary>
    [HideMonoScript]
    internal sealed class SearchableOnReflectedFixture : ScriptableObject
    {
        /// <summary>只读的反射成员。</summary>
        [ShowInInspector]
        [Searchable]
        public SearchStats Reflected => null;
    }
}
