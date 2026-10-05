using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// 方法节点：谁会被收进来、成为什么形状、排在哪儿、末端接什么。
    /// <para>
    /// 这一层是按钮族的地基——<c>[Button]</c> 只能挂在方法节点上，
    /// 而方法节点是树上第一种**没有值**的节点。
    /// </para>
    /// </summary>
    [TestFixture]
    public class MethodNodeTests
    {
        #region Private Fields

        private MethodCollectionFixture _collection;
        private MethodOrderFixture _order;

        #endregion

        #region Setup / Teardown

        /// <summary>建立测试资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _collection = ScriptableObject.CreateInstance<MethodCollectionFixture>();
            _order = ScriptableObject.CreateInstance<MethodOrderFixture>();
        }

        /// <summary>销毁资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            Destroy(ref _collection);
            Destroy(ref _order);

            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region 收集

        /// <summary>只有带 <c>[Button]</c> 的方法建节点，其余方法一概不进树。</summary>
        [Test]
        public void 只收带Button的方法()
        {
            using (var tree = Build(_collection))
            {
                var paths = PathsOf(tree);

                Assert.That(paths, Does.Contain("WithButton()"));
                Assert.That(paths, Does.Not.Contain("WithoutButton()"), "没有特性的普通方法不该出现在树里。");
            }
        }

        /// <summary>方法节点没有值入口，但带着反射信息——绘制器靠它找方法。</summary>
        [Test]
        public void 方法节点没有值入口但带反射信息()
        {
            using (var tree = Build(_collection))
            {
                var node = Find(tree, "WithButton()");

                Assert.That(node, Is.Not.Null);
                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.Method));
                Assert.That(node.ValueEntry, Is.Null, "方法没有值，不该有值入口。");
                Assert.That(node.Member, Is.InstanceOf<MethodInfo>());
                Assert.That(node.Type, Is.EqualTo(typeof(void)), "节点类型取方法的返回类型。");
            }
        }

        /// <summary>私有、公开、静态方法都收——反射能拿到的都算数。</summary>
        [Test]
        public void 私有与静态方法都收()
        {
            using (var tree = Build(_collection))
            {
                var paths = PathsOf(tree);

                Assert.That(paths, Does.Contain("WithButton()"), "私有方法。");
                Assert.That(paths, Does.Contain("PublicButton()"));
                Assert.That(paths, Does.Contain("StaticButton()"));
            }
        }

        /// <summary>
        /// 覆写链上只留最派生的一份：覆写方没带特性时不重复收集，带了则保留覆写方那一份。
        /// </summary>
        [Test]
        public void 覆写只保留最派生的一份()
        {
            using (var tree = Build(_collection))
            {
                var paths = PathsOf(tree);

                Assert.That(Count(paths, "Overridden()"), Is.EqualTo(1), "基类声明与覆写各来一遍就成了两个按钮。");
                Assert.That(Count(paths, "OverriddenAgain()"), Is.EqualTo(1));

                var node = Find(tree, "OverriddenAgain()");
                Assert.That(
                    ((MethodInfo)node.Member).DeclaringType,
                    Is.EqualTo(typeof(MethodDerivedFixture)),
                    "覆写方自己也标了特性时，保留的该是覆写方那一份。");
            }
        }

        /// <summary>
        /// 同名重载只能留一个：按钮文本默认就是方法名，两个同名按钮谁也分不清谁。
        /// </summary>
        [Test]
        public void 同名重载保留一个并告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex("多个重载带 \\[Button\\]"));

            using (var tree = Build(_collection))
            {
                Assert.That(Count(PathsOf(tree), "Overloaded()"), Is.EqualTo(1));
            }
        }

        /// <summary>一个字段都没有、只有按钮方法时，树里照样有它。</summary>
        [Test]
        public void 没有字段只有按钮也能建树()
        {
            var only = ScriptableObject.CreateInstance<MethodOnlyFixture>();

            try
            {
                using (var tree = Build(only))
                {
                    Assert.That(PathsOf(tree), Does.Contain("Run()"));
                }
            }
            finally
            {
                Object.DestroyImmediate(only);
            }
        }

        #endregion

        #region 按钮的位置

        /// <summary>
        /// <b>没有 <c>[PropertyOrder]</c> 时，按钮排在字段之后</b>——这是实测结论，不是偷懒。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 本想按声明顺序把按钮插回字段之间（Odin 的手感），实测做不到：见下面两条守卫。
        /// 位置不理想是小事，把按钮插到随机位置才是大事；「可预测」优先于「看起来更聪明」。
        /// </para>
        /// <para>
        /// **2026-10-05 起这是「默认」而非铁律**：<c>[PropertyOrder]</c> 给了显式出口——
        /// 给方法一个负的顺序即可排到字段之间（<c>PropertyOrderTests</c> 钉着那条路径）。
        /// 本 fixture 一个标注都没有，因此原文的断言逐字成立。
        /// </para>
        /// <para>
        /// 断言用相对位置而不是整份清单：<c>m_Script</c> 这类 Unity 注入的成员在不在树里
        /// 取决于对象种类，写死清单会把无关差异也判成失败。
        /// </para>
        /// </remarks>
        [Test]
        public void 按钮默认排在字段之后()
        {
            using (var tree = Build(_order))
            {
                var paths = PathsOf(tree);

                Assert.That(
                    paths.IndexOf("after"),
                    Is.LessThan(paths.IndexOf("Middle()")),
                    $"按钮该排在字段之后。实际：[{string.Join(", ", paths)}]");
                Assert.That(paths.IndexOf("after"), Is.LessThan(paths.IndexOf("Last()")));
            }
        }

        /// <summary>按钮之间仍按**声明顺序**排——同一张元数据表内的行号是可信的。</summary>
        [Test]
        public void 按钮之间按声明顺序()
        {
            using (var tree = Build(_order))
            {
                var paths = PathsOf(tree);

                Assert.That(
                    paths.IndexOf("Middle()"),
                    Is.LessThan(paths.IndexOf("Last()")),
                    $"实际：[{string.Join(", ", paths)}]");
            }
        }

        /// <summary>字段之间的相对顺序不受影响——它来自 Unity 的序列化顺序；没有 <c>[PropertyOrder]</c> 时我们从不重排字段。</summary>
        [Test]
        public void 字段之间的相对顺序不受影响()
        {
            using (var tree = Build(_order))
            {
                var paths = PathsOf(tree);

                Assert.That(paths.IndexOf("before"), Is.LessThan(paths.IndexOf("after")));
            }
        }

        #endregion

        #region 为什么排不到字段之间（三条测量的守卫）

        /// <summary>
        /// 字段令牌与方法令牌**分属元数据的两张表**，各自编号，跨表比大小没有意义。
        /// <para>
        /// 这就是「按令牌交错字段与方法」不可行的第一条理由。哪天 Unity 换了令牌方案
        /// （表号不再区分字段与方法），这条会先红，届时可以重新评估交错方案。
        /// </para>
        /// </summary>
        [Test]
        public void 元数据令牌跨表不可比()
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.DeclaredOnly;

            var type = typeof(MethodOrderFixture);
            var fieldTable = type.GetFields(Flags)[0].MetadataToken >> 24;
            var methodTable = type.GetMethods(Flags)[0].MetadataToken >> 24;

            Assert.That(fieldTable, Is.EqualTo(0x04), "字段在 FieldDef 表。");
            Assert.That(methodTable, Is.EqualTo(0x06), "方法在 MethodDef 表。");
            Assert.That(fieldTable, Is.Not.EqualTo(methodTable), "两张表各自编号，令牌不可跨界比较。");
        }

        /// <summary>同一张表内，行号递增就是声明顺序——按钮之间的先后靠它。</summary>
        [Test]
        public void 同表内行号即声明顺序()
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.DeclaredOnly;

            var type = typeof(MethodOrderFixture);
            var middle = type.GetMethod("Middle", Flags);
            var last = type.GetMethod("Last", Flags);

            Assert.That(middle.MetadataToken, Is.LessThan(last.MetadataToken));
        }

        /// <summary>
        /// <see cref="Type.GetMembers(BindingFlags)"/> **不按声明顺序**返回成员——
        /// 实测它把方法、构造函数、字段分组返回（本夹具里是方法在前、字段在后）。
        /// <para>
        /// 这是交错方案不可行的第二条理由：反射层面**根本拿不到**「这个方法声明在哪两个字段之间」。
        /// </para>
        /// </summary>
        [Test]
        public void GetMembers不给出声明顺序()
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.DeclaredOnly;

            var names = new List<string>();
            foreach (var member in typeof(MethodOrderFixture).GetMembers(Flags))
            {
                names.Add(member.Name);
            }

            var middleIndex = names.IndexOf("Middle");
            var beforeIndex = names.IndexOf("before");

            Assert.That(middleIndex, Is.GreaterThanOrEqualTo(0), "方法必须在 GetMembers 里。");
            Assert.That(beforeIndex, Is.GreaterThanOrEqualTo(0), "字段必须在 GetMembers 里。");
            Assert.That(
                middleIndex,
                Is.LessThan(beforeIndex),
                $"Middle() 声明在 before 之后，GetMembers 却把它排前面——正说明它不是声明顺序。实际：[{string.Join(", ", names)}]");
        }

        #endregion

        #region 末端

        /// <summary>
        /// 方法节点的末端是方法专用绘制器——接值绘制器只会画出一句「没有 Unity 序列化后端」，
        /// 那句话本身没错，但答非所问。
        /// </summary>
        [Test]
        public void 方法节点的末端是方法专用绘制器()
        {
            using (var tree = Build(_collection))
            {
                var node = Find(tree, "WithButton()");
                var entries = node.Chain.Entries;

                Assert.That(entries.Length, Is.GreaterThan(0));
                Assert.That(
                    entries[entries.Length - 1].Drawer,
                    Is.TypeOf<MethodTerminalDrawer>(),
                    "末端必须是方法专用绘制器。");
            }
        }

        /// <summary>成员节点的末端仍是值绘制器——新末端只该接到方法节点上。</summary>
        [Test]
        public void 成员节点的末端仍是值绘制器()
        {
            using (var tree = Build(_collection))
            {
                var node = Find(tree, "value");
                var entries = node.Chain.Entries;

                Assert.That(entries[entries.Length - 1].Drawer, Is.TypeOf<UnityFallbackDrawer>());
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>建一棵树。</summary>
        /// <param name="target">目标对象。</param>
        /// <returns>属性树。</returns>
        private static PropertyTree Build(Object target)
        {
            return PropertyTree.Create(new SerializedObject(target));
        }

        /// <summary>取根下所有子节点的路径。</summary>
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

        /// <summary>按路径找根下的子节点。</summary>
        /// <param name="tree">属性树。</param>
        /// <param name="path">节点路径。</param>
        /// <returns>找到的节点；不存在时返回 <c>null</c>。</returns>
        private static InspectorProperty Find(PropertyTree tree, string path)
        {
            foreach (var child in tree.Root.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>数一个路径出现了几次。</summary>
        /// <param name="paths">路径列表。</param>
        /// <param name="path">目标路径。</param>
        /// <returns>出现次数。</returns>
        private static int Count(List<string> paths, string path)
        {
            var count = 0;
            for (var i = 0; i < paths.Count; i++)
            {
                if (paths[i] == path)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>销毁资产并把引用清空。</summary>
        /// <typeparam name="T">资产类型。</typeparam>
        /// <param name="target">资产引用。</param>
        private static void Destroy<T>(ref T target) where T : Object
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
                target = null;
            }
        }

        #endregion
    }

    /// <summary>
    /// 覆写链的起点：三个方法都带 <c>[Button]</c>，派生类分别「不覆写」「覆写但不带特性」
    /// 「覆写且带特性」——三种情形对应收集器的三条分支。
    /// </summary>
    internal class MethodBaseFixture : ScriptableObject
    {
        /// <summary>派生类覆写它但**不带**特性。</summary>
        [Button]
        public virtual void Overridden()
        {
        }

        /// <summary>没人覆写它。</summary>
        [Button]
        public virtual void NotOverridden()
        {
        }

        /// <summary>派生类覆写它、且**也带**特性。</summary>
        [Button]
        public virtual void OverriddenAgain()
        {
        }
    }

    /// <summary>派生夹具。覆写方法体是空的——这里只关心反射层面看到了什么。</summary>
    internal class MethodDerivedFixture : MethodBaseFixture
    {
        /// <summary>覆写但不带特性：<c>[Button]</c> 是 <c>Inherited = false</c>，故只该由基类声明建一个节点。</summary>
        public override void Overridden()
        {
        }

        /// <summary>覆写且带特性：两份声明都命中，去重后保留这一份。</summary>
        [Button]
        public override void OverriddenAgain()
        {
        }
    }

    /// <summary>收集行为用的夹具：各类方法各一个。</summary>
    internal sealed class MethodCollectionFixture : MethodDerivedFixture
    {
        /// <summary>一个普通字段，用来验证成员节点的末端没被新末端顶掉。</summary>
        public int value;

        /// <summary>带特性的私有方法。</summary>
        [Button]
        private void WithButton()
        {
        }

        /// <summary>不带特性——不该出现在树里。</summary>
        private void WithoutButton()
        {
        }

        /// <summary>带特性的公开方法。</summary>
        [Button]
        public void PublicButton()
        {
        }

        /// <summary>带特性的静态方法。</summary>
        [Button]
        private static void StaticButton()
        {
        }

        /// <summary>同名重载之一。</summary>
        [Button]
        private void Overloaded()
        {
        }

        /// <summary>同名重载之二——与上一个同名，只能留一个并告警。</summary>
        [Button]
        private void Overloaded(int amount)
        {
        }
    }

    /// <summary>交错用的夹具：字段与方法按声明顺序交替。</summary>
    internal sealed class MethodOrderFixture : ScriptableObject
    {
        /// <summary>排在最前的字段。</summary>
        public int before = 1;

        /// <summary>夹在两个字段之间的按钮。</summary>
        [Button]
        private void Middle()
        {
        }

        /// <summary>夹在按钮与按钮之间的字段。</summary>
        public int after = 2;

        /// <summary>排在最后的按钮。</summary>
        [Button]
        private void Last()
        {
        }
    }

    /// <summary>一个字段都没有、只有按钮方法的夹具。</summary>
    internal sealed class MethodOnlyFixture : ScriptableObject
    {
        /// <summary>唯一的成员。</summary>
        [Button]
        private void Run()
        {
        }
    }

}
