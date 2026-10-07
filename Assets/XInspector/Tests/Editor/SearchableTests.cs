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

        /// <summary>
        /// 标在多态引用上：按需展开之后走**复合成员**那条路，搜索照常过滤它里面的成员。
        /// </summary>
        /// <remarks>
        /// <b>2026-10-07 翻面。</b>此前这里断言的是「告警 + 没有子节点」（多态引用不展开）。
        /// 多态引用进管线之后它展开了，处理器在上面那句 <c>Children.Count &gt; 0</c> 提前返回，
        /// 那条专项告警文案随之成了死代码、已删。
        /// </remarks>
        [Test]
        public void 标在多态引用上时按需展开并搜索()
        {
            var target = ScriptableObject.CreateInstance<SearchableOnReferenceFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var poly = Find(tree.Root, "poly");

                    Assert.That(poly.Children.Count, Is.GreaterThan(0), "多态引用按需展开成子节点。");

                    // 夹具里的两个成员都标了 [BoxGroup("基础")]——分组在**多态段里面**照样装配，
                    // 组节点路径以父字段的路径为前缀。这一句顺带钉住那条。
                    var group = Find(poly, "poly/基础");
                    Assert.That(group, Is.Not.Null, "多态段里的分组照样装配。");

                    var scope = ScopeOf(poly, "mana");
                    Assert.That(
                        scope.VisibilityOf(Find(group, "poly.mana")),
                        Is.EqualTo(SearchVisibility.KeepAll),
                        "命中的成员留下。");
                    Assert.That(
                        scope.VisibilityOf(Find(group, "poly.health")),
                        Is.EqualTo(SearchVisibility.Hidden),
                        "没命中的被过滤——多态段里的成员**真的**进了行掩码。");
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

                    Assert.That(state.EnsureListRows(node), Is.EqualTo(new[] { true, false }));
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

                    Assert.That(state.EnsureListRows(node), Is.EqualTo(new[] { false, true }));
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
                        state.EnsureTableRows(node, model.Columns),
                        Is.EqualTo(new[] { true, false }));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **同一搜索宿主下两个同尺寸的列表不串掩码**——缓存键带宿主身份。
        /// </summary>
        /// <remarks>
        /// 这条钉的是**既有缺陷的回归**：键原先只比「查询 + 长度」，第二个列表会直接复用
        /// 第一个列表留下的掩码（静默筛错行）。元素层支持深度 &gt; 1 之后，「外层查询落到
        /// 内层集合」成了常态，这条就更容易撞上。
        /// </remarks>
        [Test]
        public void 同宿主下两个同尺寸的列表不串掩码()
        {
            var target = ScriptableObject.CreateInstance<TwoListSearchFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var host = Find(tree.Root, "host");
                    var state = StateOf(host, "剑");

                    var swords = Find(host, "host.swords");
                    var wands = Find(host, "host.wands");

                    Assert.That(
                        state.EnsureListRows(swords),
                        Is.EqualTo(new[] { true, false }),
                        "第一个列表：剑命中。");

                    Assert.That(
                        state.EnsureListRows(wands),
                        Is.EqualTo(new[] { false, false }),
                        "第二个列表同尺寸但值不同——键不认宿主的话这里会拿到上一个列表的掩码。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **重建之后内层集合的行掩码重算**——节点身份进了键，新节点必然 miss。
        /// </summary>
        /// <remarks>
        /// 深度 &gt; 1 的场景：外层列表是搜索宿主，内层列表在它的元素里。外层改长度会让
        /// 内层换成**新节点**——旧键（同尺寸、同查询）若不认节点，就会把重建前的掩码
        /// 接着用。
        /// </remarks>
        [Test]
        public void 重建之后内层集合的行掩码重算()
        {
            var target = ScriptableObject.CreateInstance<SearchDepthFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var rows = Find(tree.Root, "rows");
                    var state = StateOf(rows, "剑");

                    var before = Find(rows.Children[0], "rows.Array.data[0].items");
                    Assert.That(state.EnsureListRows(before), Is.EqualTo(new[] { true }), "起点：剑命中。");

                    SetString(target, tree, "rows.Array.data[0].items.Array.data[0]", "盾");
                    SetArraySize(target, tree, "rows", 2);

                    Assert.That(CollectionElementSync.ReconcileAll(tree), Is.GreaterThan(0), "外层重建。");

                    var after = Find(Find(tree.Root, "rows").Children[0], "rows.Array.data[0].items");
                    Assert.That(after, Is.Not.SameAs(before), "重建换了新节点。");
                    Assert.That(
                        state.EnsureListRows(after),
                        Is.EqualTo(new[] { false }),
                        "新节点 ⇒ 键 miss ⇒ 按新数据重算。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>表格的**列集合**也进缓存键——同一节点换了列模型必须重算。</summary>
        [Test]
        public void 表格的列集合进了缓存键()
        {
            var target = ScriptableObject.CreateInstance<SearchableFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "table");
                    var model = node.State.Get<TableModel>();
                    var state = StateOf(node, "哥布");

                    Assert.That(model, Is.Not.Null);
                    Assert.That(state.EnsureTableRows(node, model.Columns), Is.EqualTo(new[] { false, true }));

                    // 同节点、同查询、同尺寸，只换列集合（只留 level 列，值是 1/2）——
                    // 键不认列的话会复用上面那份掩码。
                    var narrow = new[] { model.Columns[0] };
                    Assert.That(
                        state.EnsureTableRows(node, narrow),
                        Is.EqualTo(new[] { false, false }),
                        "只有 level 列时「哥布」不命中——列变了就该重算。");
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
                    var state = StateOf(node, "剑");

                    Assert.That(state.EnsureListRows(node), Is.EqualTo(new[] { true, false }));

                    state.Query = "盾";
                    Assert.That(state.EnsureListRows(node), Is.EqualTo(new[] { false, true }));
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

                    Assert.That(state.EnsureListRows(node).Length, Is.EqualTo(2));

                    array.arraySize = 3;
                    Assert.That(state.EnsureListRows(node).Length, Is.EqualTo(3), "新元素是末元素的副本，故也跟着命中。");
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
                        SearchMatcher.HasAnyRow(state.EnsureListRows(node)),
                        Is.False);

                    state.Query = "   ";
                    Assert.That(state.EnsureListRows(node), Is.Null, "空白查询不产生掩码。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 元素层：行掩码管辖的那一段

        /// <summary>
        /// 元素里的**分组节点**在搜索生效时照常画。
        /// </summary>
        /// <remarks>
        /// 元素的行是容器挑的，元素子树因此不进节点命中集；而元素里的分组节点会自己去问
        /// <see cref="SearchScope.ShouldDraw"/>。不特判的话它恒是 <c>Hidden</c>——
        /// 症状是**分组框画出来、里面一个字段都没有**（静默，且没有任何既有用例覆盖）。
        /// </remarks>
        [Test]
        public void 元素里的分组在搜索生效时照常画()
        {
            var target = ScriptableObject.CreateInstance<SearchableElementFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var host = Find(tree.Root, "rows");
                    var element = Find(host, "rows.Array.data[0]");
                    var group = Find(element, "rows.Array.data[0]/元素里的分组");

                    var state = StateOf(host, "哥布");
                    state.EnsureNodes(host);

                    // 控制项：元素子树确实不在命中集里——这是既有的、刻意保留的契约。
                    Assert.That(state.VisibilityOf(element), Is.EqualTo(SearchVisibility.Hidden));

                    var scope = SearchScope.Find(group);
                    Assert.That(scope.IsActive, Is.True, "上游的 [Searchable] 该被找到。");
                    Assert.That(scope.ShouldDraw(group), Is.True, "归行掩码管的那一段不参与节点级过滤。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>元素里的**复合成员**在搜索生效时照常画（同一条根因，第二个受害者）。</summary>
        /// <remarks>它比分组多一个症状：除了内容全空，还会多画一句「没有匹配的项。」。</remarks>
        [Test]
        public void 元素里的复合成员在搜索生效时照常画()
        {
            var target = ScriptableObject.CreateInstance<SearchableElementFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var host = Find(tree.Root, "rows");
                    var element = Find(host, "rows.Array.data[0]");
                    var nested = Find(element, "rows.Array.data[0].stats");

                    StateOf(host, "哥布").EnsureNodes(host);

                    var scope = SearchScope.Find(nested);
                    Assert.That(scope.ShouldDraw(nested), Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 对照组：**不在元素里**的节点照旧按命中集筛——特判只放行行掩码管辖的那一段，
        /// 不是把整个搜索放水。
        /// </summary>
        [Test]
        public void 不在元素里的节点照旧按命中集筛()
        {
            var target = ScriptableObject.CreateInstance<SearchableElementFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var host = Find(tree.Root, "plain");
                    var title = Find(host, "plain.title");

                    StateOf(host, "哥布").EnsureNodes(host);

                    var scope = SearchScope.Find(title);
                    Assert.That(scope.ShouldDraw(title), Is.False, "没命中就该被筛掉。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 元素层：活值通道

        /// <summary>
        /// 元素里的**反射成员**按当前值参与行匹配（此前只比序列化值，故那种元素搜不到）。
        /// </summary>
        /// <remarks>夹具第一行 <c>level = 30</c> ⇒ <c>Doubled = "60"</c>，序列化字段里没有 "60"。</remarks>
        [Test]
        public void 元素里的反射成员的值参与行匹配()
        {
            var target = ScriptableObject.CreateInstance<SearchableElementFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "rows");
                    var state = StateOf(node, "60");

                    Assert.That(state.EnsureListRows(node), Is.EqualTo(new[] { true, false, false }));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 活值通道**只比值、不比标签**——与序列化那一半同款（元素标签是 <c>Element 0</c>，
        /// 让它参与的话任何含 <c>element</c> 的查询都会全中）。
        /// </summary>
        [Test]
        public void 元素里的反射成员不按标签匹配()
        {
            var target = ScriptableObject.CreateInstance<SearchableElementFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "rows");
                    var state = StateOf(node, "Doubled");

                    Assert.That(state.EnsureListRows(node), Is.EqualTo(new[] { false, false, false }));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>元素为 <c>null</c> 时读不到值（算「不一致」），不算命中，也不抛。</summary>
        [Test]
        public void 元素为null时反射成员不算命中()
        {
            var target = ScriptableObject.CreateInstance<SearchableElementFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "rows");

                    Assert.DoesNotThrow(() => StateOf(node, "60").EnsureListRows(node));
                    Assert.That(StateOf(node, "60").EnsureListRows(node)[2], Is.False, "第三行是 null 元素。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 反射成员的 getter 抛异常时不算命中，也**不在搜索路径上告警**（该错误在成员未被
        /// 过滤时由终端自己画出来——不静默；而按元素各报一条只会刷屏）。
        /// </summary>
        [Test]
        public void 元素里的反射成员出错时不算命中()
        {
            var target = ScriptableObject.CreateInstance<SearchableThrowingFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "rows");

                    Assert.DoesNotThrow(() => StateOf(node, "Boom").EnsureListRows(node));
                    Assert.That(
                        StateOf(node, "Boom").EnsureListRows(node),
                        Is.EqualTo(new[] { false, false }),
                        "连标签都不比——活值通道只认值。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 元素里的反射值命中 ⇒ **集合节点自己**留下（否则整块集合会连同那一行一起消失）。
        /// </summary>
        /// <remarks>
        /// 只有「集合在另一个宿主**里面**」的形态测得到：宿主直接是集合时，它的子节点全是
        /// 元素节点，`Collect` 在第一步就跳过了，那条分支根本走不到。
        /// </remarks>
        [Test]
        public void 元素里的反射值命中时集合节点自己留下()
        {
            var target = ScriptableObject.CreateInstance<SearchableElementFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var host = Find(tree.Root, "host");
                    var group = Find(host, "host/宿主里的分组");
                    var rows = Find(group, "host.rows");

                    StateOf(host, "60").EnsureNodes(host);

                    var state = StateOf(host, "60");
                    Assert.That(
                        state.VisibilityOf(rows),
                        Is.EqualTo(SearchVisibility.KeepAll),
                        "行掩码只挑行、不挑集合，集合自己得留下。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>命中的集合的**祖先分组**算「靠后代命中」那一档。</summary>
        [Test]
        public void 元素里的反射值命中时祖先分组是过滤档()
        {
            var target = ScriptableObject.CreateInstance<SearchableElementFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var host = Find(tree.Root, "host");
                    var group = Find(host, "host/宿主里的分组");

                    var state = StateOf(host, "60");
                    state.EnsureNodes(host);

                    Assert.That(state.VisibilityOf(group), Is.EqualTo(SearchVisibility.Filtered));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>节点级那一半：复合宿主里的反射成员按值命中，整棵子树保留。</summary>
        [Test]
        public void 复合宿主里的反射成员按值命中()
        {
            var target = ScriptableObject.CreateInstance<SearchableElementFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var host = Find(tree.Root, "host");
                    var total = Find(host, "host.Total");

                    var state = StateOf(host, "99");
                    state.EnsureNodes(host);

                    Assert.That(state.VisibilityOf(total), Is.EqualTo(SearchVisibility.KeepAll));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 元素层**重建**之后行掩码重算——哪怕长度与查询都没变。
        /// </summary>
        /// <remarks>
        /// 掩码的输入是元素节点（活值从那儿读），而元素层能在净长度不变时因脏标记重建；
        /// 键里因此带了**首个元素节点**这个身份。
        /// 顺带钉住既有契约：「搜索框没动时改值不刷新命中集」。
        /// </remarks>
        [Test]
        public void 元素层重建后行掩码重算()
        {
            var target = ScriptableObject.CreateInstance<SearchableElementFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "rows");
                    var state = StateOf(node, "60");

                    Assert.That(state.EnsureListRows(node), Is.EqualTo(new[] { true, false, false }));

                    target.rows[0].level = 5; // Doubled：60 → 10

                    Assert.That(
                        state.EnsureListRows(node),
                        Is.EqualTo(new[] { true, false, false }),
                        "键没变 ⇒ 照旧给缓存（既有契约：查询或长度变化才重算）。");

                    CollectionElementExpansion.MarkLayerDirty(node);
                    Assert.That(CollectionElementSync.ReconcileAll(tree), Is.GreaterThan(0), "脏标记该让这一层重建。");

                    Assert.That(
                        state.EnsureListRows(node),
                        Is.EqualTo(new[] { false, false, false }),
                        "重建换了元素节点 ⇒ 键 miss ⇒ 按新值重算。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 表格行**不**按反射成员匹配——表格不建元素层，而列模型只收序列化字段，
        /// 那些成员在表格里根本看不见（「用户搜的是看得见的东西」）。
        /// </summary>
        [Test]
        public void 表格行不按反射成员匹配()
        {
            var target = ScriptableObject.CreateInstance<SearchableElementFixture>();
            try
            {
                using (var tree = BuildTree(target))
                {
                    var node = Find(tree.Root, "table");
                    var model = node.State.Get<TableModel>();

                    Assert.That(model, Is.Not.Null, "表格模型要在（[TableList] 建的）。");

                    var state = StateOf(node, "60");
                    Assert.That(state.EnsureTableRows(node, model.Columns), Is.EqualTo(new[] { false }));
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

        /// <summary>改一个字符串叶子的值，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="path">序列化路径。</param>
        /// <param name="value">新值。</param>
        private static void SetString(ScriptableObject target, PropertyTree tree, string path, string value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        /// <summary>改一个数组的长度，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="path">数组字段路径。</param>
        /// <param name="size">新长度。</param>
        private static void SetArraySize(ScriptableObject target, PropertyTree tree, string path, int size)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).arraySize = size;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
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

    /// <summary>
    /// 元素类型：带分组、复合成员与**反射成员**——容器末端与活值通道都靠它。
    /// </summary>
    [Serializable]
    internal sealed class SearchElementRow
    {
        /// <summary>分组里的成员（元素里的分组节点靠它出现，活值那一格也拿它算）。</summary>
        [BoxGroup("元素里的分组")]
        public int level = 1;

        /// <summary>分组里的第二个成员——分组有两个孩子才像个分组。</summary>
        [BoxGroup("元素里的分组")]
        public string title = "史莱姆";

        /// <summary>复合成员（元素里的折叠头）。</summary>
        public SearchStats stats = new SearchStats();

        /// <summary>没分组也没复合的普通成员——行值匹配的老路。</summary>
        public string tag = "普通";

        /// <summary>活值：跟着 <see cref="level"/> 走（level=30 → "60"）。</summary>
        [ShowInInspector]
        public int Doubled => level * 2;
    }

    /// <summary>
    /// 复合宿主：集合在它**里面**——「集合节点自己会不会被节点级筛掉」只有这个形态测得到
    /// （宿主直接是集合时，它的子节点全是元素节点，那条分支根本走不到）。
    /// </summary>
    [Serializable]
    internal class SearchReflectedHost
    {
        /// <summary>节点级的反射成员——活值通道的**节点级**那一半。</summary>
        [ShowInInspector]
        public int Total => 99;

        /// <summary>
        /// 带元素层的集合，且它在**分组里面**——祖先被算成过滤档那一条靠它测。
        /// <c>[ListDrawerSettings]</c> 少不了：没有自绘容器就没有画元素行的落点，
        /// 元素层根本不会建（那是既有的、构建期会告警的边界）。
        /// </summary>
        [BoxGroup("宿主里的分组")]
        [ListDrawerSettings]
        public List<SearchElementRow> rows = new List<SearchElementRow>
        {
            new SearchElementRow { level = 30 },
        };
    }

    /// <summary>元素层 + 搜索的对照资产。</summary>
    [HideMonoScript]
    internal sealed class SearchableElementFixture : ScriptableObject
    {
        /// <summary>元素里带分组与复合成员——宿主搜索生效时它们必须照常画；还带一个 null 元素。</summary>
        [Searchable]
        public List<SearchElementRow> rows = new List<SearchElementRow>
        {
            new SearchElementRow { level = 30 },
            new SearchElementRow { tag = "哥布林" },
            null,
        };

        /// <summary>对照组：**不在元素里**的复合成员，子节点照旧按命中集筛。</summary>
        [Searchable]
        public SearchStats plain = new SearchStats();

        /// <summary>复合宿主：集合在它里面。</summary>
        [Searchable]
        public SearchReflectedHost host = new SearchReflectedHost();

        /// <summary>表格：列模型只收序列化字段——反射成员不参与（不建元素层）。</summary>
        [Searchable]
        [TableList]
        public List<SearchElementRow> table = new List<SearchElementRow>
        {
            new SearchElementRow { level = 30 },
        };
    }

    /// <summary>会抛的反射成员——活值通道不该被它带崩，也不该把它算成命中。</summary>
    [Serializable]
    internal sealed class SearchThrowingRow
    {
        /// <summary>恒抛的 getter。</summary>
        [ShowInInspector]
        public int Boom => throw new InvalidOperationException("搜索不该把出错的值当命中");
    }

    /// <summary>会抛的夹具单独一处：它的 Console 行为与别的用例互不干扰。</summary>
    [HideMonoScript]
    internal sealed class SearchableThrowingFixture : ScriptableObject
    {
        /// <summary>两个元素都恒抛。</summary>
        [Searchable]
        public List<SearchThrowingRow> rows = new List<SearchThrowingRow>
        {
            new SearchThrowingRow(),
            new SearchThrowingRow(),
        };
    }

    /// <summary>同一搜索宿主下两个**同尺寸**的列表——掩码缓存必须按宿主分家。</summary>
    [Serializable]
    internal class TwoListHost
    {
        /// <summary>第一个列表。</summary>
        [ListDrawerSettings]
        public List<string> swords = new List<string> { "剑", "盾" };

        /// <summary>第二个列表（与第一个同尺寸、值不同）。</summary>
        [ListDrawerSettings]
        public List<string> wands = new List<string> { "弓", "杖" };
    }

    /// <summary>两个同尺寸列表的对照资产（搜索标在复合宿主上）。</summary>
    [HideMonoScript]
    internal sealed class TwoListSearchFixture : ScriptableObject
    {
        /// <summary>宿主：两个列表都在它下面，共用一份搜索状态。</summary>
        [Searchable]
        public TwoListHost host = new TwoListHost();
    }

    /// <summary>元素里嵌一个内层列表——外层列表是搜索宿主（深度 &gt; 1 的宿主穿透场景）。</summary>
    [Serializable]
    internal class SearchDepthRow
    {
        /// <summary>内层列表。</summary>
        [ListDrawerSettings]
        public List<string> items = new List<string> { "剑" };
    }

    /// <summary>深度搜索对照资产。</summary>
    [HideMonoScript]
    internal sealed class SearchDepthFixture : ScriptableObject
    {
        /// <summary>外层：搜索宿主 + 元素层（容器由 [Searchable] 顺带注入）。</summary>
        [Searchable]
        public List<SearchDepthRow> rows = new List<SearchDepthRow> { new SearchDepthRow() };
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
