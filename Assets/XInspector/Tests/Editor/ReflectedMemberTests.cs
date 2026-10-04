using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XInspector.Editor;

// 本文件同时 using 了 System 与 UnityEngine，裸写 Object 会在 System.Object 与
// UnityEngine.Object 之间产生二义（CS0104）。别名消歧，比在每处写全名可读。
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 反射成员进树：收谁、不收谁、排在哪、末端接谁。
    /// <para>
    /// 全部是结构断言——不碰 GUI，也不需要把值画出来。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ReflectedMemberTests
    {
        #region Private Fields

        private ReflectedMemberFixture _target;

        #endregion

        #region Setup / Teardown

        /// <summary>建立用于构建树的临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ReflectedMemberFixture>();
        }

        /// <summary>销毁临时资产并复位两张静态注册表。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 收谁

        /// <summary>非序列化私有字段以反射成员进树，值入口是反射后端。</summary>
        [Test]
        public void 非序列化私有字段进树()
        {
            using (var tree = BuildTree())
            {
                var node = Find(tree.Root, "_reflected");

                Assert.That(node, Is.Not.Null, "带 [ShowInInspector] 的私有字段应当进树。");
                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.ReflectedMember));
                Assert.That(node.Member, Is.InstanceOf<FieldInfo>());
                Assert.That(node.Type, Is.EqualTo(typeof(int)));
                Assert.That(node.ValueEntry, Is.Not.Null);
                Assert.That(node.ValueEntry.IsUnityBacked, Is.False);
                Assert.That(node.ValueEntry.SerializedProperty, Is.Null);
            }
        }

        /// <summary>普通属性以反射成员进树——这是 <c>[ShowInInspector]</c> 的主战场。</summary>
        [Test]
        public void 普通属性进树()
        {
            using (var tree = BuildTree())
            {
                var node = Find(tree.Root, "ReflectedProperty");

                Assert.That(node, Is.Not.Null);
                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.ReflectedMember));
                Assert.That(node.Member, Is.InstanceOf<PropertyInfo>());
                Assert.That(node.ValueEntry.IsUnityBacked, Is.False);
            }
        }

        /// <summary>静态成员同样进树。</summary>
        [Test]
        public void 静态成员进树()
        {
            using (var tree = BuildTree())
            {
                var node = Find(tree.Root, "StaticReflected");

                Assert.That(node, Is.Not.Null);
                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.ReflectedMember));
            }
        }

        /// <summary>
        /// 只收带标记的成员。
        /// <para>
        /// 这条同时是「不需要跳过 UnityEngine 框架成员的规则」那一条的守卫：
        /// 收集是按标记来的，框架成员身上不可能有本包的标记，于是
        /// <c>EditorWindow</c> 那几十个属性一个都进不来——构造上就成立，不靠名单。
        /// </para>
        /// </summary>
        [Test]
        public void 只收带标记的成员()
        {
            using (var tree = BuildTree())
            {
                Assert.That(Find(tree.Root, "NotMarked"), Is.Null, "没标记的普通属性不该出现。");
                Assert.That(Find(tree.Root, "UnmarkedBaseField"), Is.Null, "没标记的基类公开字段不该出现。");
                Assert.That(Find(tree.Root, "UnmarkedBaseProperty"), Is.Null, "没标记的基类公开属性不该出现。");
            }
        }

        /// <summary>
        /// <c>[ShowInInspector]</c> 标在**已被序列化通道收走**的成员上，不会出现第二遍。
        /// <para>
        /// 控制项是「它确实在树里」——只断言「出现一次」的话，一个什么都不收的实现也能过。
        /// </para>
        /// </summary>
        [Test]
        public void 与序列化成员不重复()
        {
            using (var tree = BuildTree())
            {
                var node = Find(tree.Root, "both");

                Assert.That(node, Is.Not.Null, "该成员应当照常出现在树里。");
                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.Member),
                    "它归序列化通道，于是是**可编辑**的成员节点，不是只读的反射成员。");
                Assert.That(CountNodes(tree.Root, "both"), Is.EqualTo(1), "同一个成员只该有一个节点。");
            }
        }

        /// <summary>
        /// 被 Unity 序列化但不在 Inspector 里的字段（<c>[HideInInspector]</c>），
        /// 加了 <c>[ShowInInspector]</c> 之后会被反射通道收进来。
        /// <para>
        /// 判重看的是「树里有没有它」，不是「Unity 会不会序列化它」——两者不等价，
        /// 而差别正在这里。没有标记的那一个则必须老老实实待在外面。
        /// </para>
        /// </summary>
        [Test]
        public void 隐藏的序列化字段被收进来()
        {
            using (var tree = BuildTree())
            {
                var node = Find(tree.Root, "hiddenButShown");

                Assert.That(node, Is.Not.Null);
                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.ReflectedMember));
                Assert.That(Find(tree.Root, "hiddenByUnity"), Is.Null, "没有标记的隐藏字段不该出现。");
            }
        }

        #endregion

        #region 排在哪

        /// <summary>顺序是：序列化成员 → 反射成员 → 方法节点。</summary>
        [Test]
        public void 顺序为序列化在前反射居中方法在后()
        {
            using (var tree = BuildTree())
            {
                var paths = PathsOf(tree);

                var serialized = paths.IndexOf("serialized");
                var reflected = paths.IndexOf("_reflected");
                var method = paths.IndexOf("DoThing()");

                Assert.That(serialized, Is.GreaterThanOrEqualTo(0));
                Assert.That(reflected, Is.GreaterThanOrEqualTo(0));
                Assert.That(method, Is.GreaterThanOrEqualTo(0));

                Assert.That(serialized, Is.LessThan(reflected), $"实际顺序：[{string.Join(", ", paths)}]");
                Assert.That(reflected, Is.LessThan(method), $"实际顺序：[{string.Join(", ", paths)}]");
            }
        }

        /// <summary>继承链上基类在前，且按名字去重只留最派生的那一份。</summary>
        [Test]
        public void 基类的反射成员排在前面()
        {
            var target = ScriptableObject.CreateInstance<ReflectedDerivedFixture>();

            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var paths = PathsOf(tree);

                    Assert.That(paths, Does.Contain("MarkedBase"));
                    Assert.That(paths, Does.Contain("MarkedDerived"));
                    Assert.That(
                        paths.IndexOf("MarkedBase"),
                        Is.LessThan(paths.IndexOf("MarkedDerived")),
                        $"基类成员应当排在派生类之前，实际顺序：[{string.Join(", ", paths)}]");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>反射成员照样享受分组——它与其它成员走同一套分组装配。</summary>
        [Test]
        public void 反射成员也能进分组()
        {
            using (var tree = BuildTree())
            {
                var node = Find(tree.Root, "grouped");

                Assert.That(node, Is.Not.Null, "带分组特性的反射成员应当进树。");
                Assert.That(node.Parent, Is.Not.Null);
                Assert.That(node.Parent.Kind, Is.EqualTo(InspectorPropertyKind.Group),
                    "它应当被搬进分组节点，而不是留在根下。");
                Assert.That(node.Parent.Path, Is.EqualTo("组"));
            }
        }

        #endregion

        #region 末端

        /// <summary>反射成员的链条末端是反射专用绘制器，不是那个画 Unity 值的。</summary>
        [Test]
        public void 反射成员接反射末端()
        {
            using (var tree = BuildTree())
            {
                var entries = Find(tree.Root, "_reflected").Chain.Entries;

                Assert.That(
                    entries[entries.Length - 1].Drawer,
                    Is.TypeOf<ReflectedMemberTerminalDrawer>(),
                    "末端接错了会画出一句答非所问的「没有 Unity 序列化后端」。");
            }
        }

        /// <summary>
        /// 控制项：序列化成员接的仍是 Unity 末端。
        /// <para>
        /// 没有这一条，上面那条无法区分「按种类选末端」与「一律接反射末端」。
        /// </para>
        /// </summary>
        [Test]
        public void 序列化成员接Unity末端()
        {
            using (var tree = BuildTree())
            {
                var entries = Find(tree.Root, "serialized").Chain.Entries;

                Assert.That(entries[entries.Length - 1].Drawer, Is.TypeOf<UnityFallbackDrawer>());
            }
        }

        #endregion

        #region 收集不出来的成员

        /// <summary>
        /// 取值访问器编译不出来（只写属性）时**跳过该成员并告警**，
        /// 而不是建一个画不出值的节点。
        /// </summary>
        [Test]
        public void 编译不出的成员被跳过并告警()
        {
            var target = ScriptableObject.CreateInstance<ReflectedUnsupportedFixture>();

            try
            {
                LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex("WriteOnly.*无法生效.*只写属性"));

                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    Assert.That(Find(tree.Root, "WriteOnly"), Is.Null, "读不到的成员不该建节点。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 特性的标注面

        /// <summary>
        /// <c>[ShowInInspector]</c> 不能标在方法上。
        /// <para>
        /// 让它**编译期报错**（而不是标了没反应）是刻意的：方法有 <c>[Button]</c>，
        /// 而「显示一个方法的返回值」在本包没有既定语义——与其猜一个形状，不如让它响亮地拒绝。
        /// </para>
        /// </summary>
        [Test]
        public void 特性不允许标注方法()
        {
            var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
                typeof(ShowInInspectorAttribute), typeof(AttributeUsageAttribute));

            Assert.That(usage, Is.Not.Null);
            Assert.That(usage.ValidOn & AttributeTargets.Method, Is.EqualTo((AttributeTargets)0));
            Assert.That(usage.ValidOn & AttributeTargets.Field, Is.EqualTo(AttributeTargets.Field));
            Assert.That(usage.ValidOn & AttributeTargets.Property, Is.EqualTo(AttributeTargets.Property));
            Assert.That(usage.AllowMultiple, Is.False);
        }

        #endregion

        #region Private Helpers

        /// <summary>构建被测的树。</summary>
        /// <returns>属性树。</returns>
        private PropertyTree BuildTree()
        {
            return PropertyTree.Create(new SerializedObject(_target));
        }

        /// <summary>深度优先找一个路径等于给定值的节点。</summary>
        /// <param name="node">起始节点。</param>
        /// <param name="path">目标路径。</param>
        /// <returns>节点；找不到返回 <c>null</c>。</returns>
        /// <remarks><c>internal</c> 是为了让告警措辞那个 fixture 复用同一份查找，不必再抄一遍。</remarks>
        internal static InspectorProperty Find(InspectorProperty node, string path)
        {
            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (string.Equals(child.Path, path, StringComparison.Ordinal))
                {
                    return child;
                }

                var found = Find(child, path);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>数一数树里有几个节点用了这个路径——树的身份契约要求它是 1。</summary>
        /// <param name="node">起始节点。</param>
        /// <param name="path">目标路径。</param>
        /// <returns>节点个数。</returns>
        private static int CountNodes(InspectorProperty node, string path)
        {
            var count = 0;
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (string.Equals(node.Children[i].Path, path, StringComparison.Ordinal))
                {
                    count++;
                }

                count += CountNodes(node.Children[i], path);
            }

            return count;
        }

        /// <summary>取根的直接子节点路径（分组里的成员不在其中）。</summary>
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

    /// <summary>反射成员测试用的资产：每个成员演示一种「收不收、归哪条通道」。</summary>
    internal sealed class ReflectedMemberFixture : ScriptableObject
    {
        /// <summary>会被 Unity 序列化——方法节点顺序断言要拿它当参照。</summary>
        public int serialized = 1;

        /// <summary>两条通道都想要：只该出现一次，且归序列化通道（可编辑）。</summary>
        [ShowInInspector]
        public int both = 2;

        /// <summary>被 Unity 序列化但不在 Inspector 里，且没有标记——不该出现。</summary>
        [HideInInspector]
        public int hiddenByUnity = 3;

        /// <summary>被 Unity 序列化、不在 Inspector 里，但有标记——该被反射通道收进来。</summary>
        [HideInInspector]
        [ShowInInspector]
        public int hiddenButShown = 4;

        /// <summary>没有标记的普通属性——不该出现。</summary>
        public int NotMarked => 5;

        /// <summary>带分组的反射成员。</summary>
        [ShowInInspector]
        [BoxGroup("组")]
        public int grouped = 6;

        /// <summary>静态成员也可以标。</summary>
        [ShowInInspector]
        public static int StaticReflected = 7;

        /// <summary>私有字段，没有 <c>[SerializeField]</c>。</summary>
        [ShowInInspector]
        private int _reflected = 8;

        /// <summary>普通属性，读一个私有字段。</summary>
        [ShowInInspector]
        private int ReflectedProperty => _reflected * 2;

        /// <summary>方法节点，用来钉住「反射成员排在方法之前」。</summary>
        [Button]
        private void DoThing()
        {
            _reflected++;
        }
    }

    /// <summary>基类夹具：公开但没标记的成员一个都不该被收走。</summary>
    internal class ReflectedBaseFixture : ScriptableObject
    {
        /// <summary>公开但没标记的字段。</summary>
        public int UnmarkedBaseField = 1;

        /// <summary>公开但没标记的属性。</summary>
        public int UnmarkedBaseProperty => 2;

        /// <summary>标了记的基类成员。</summary>
        [ShowInInspector]
        public int MarkedBase = 3;
    }

    /// <summary>派生夹具：用来看继承链上的先后。</summary>
    internal sealed class ReflectedDerivedFixture : ReflectedBaseFixture
    {
        /// <summary>标了记的派生类成员。</summary>
        [ShowInInspector]
        public int MarkedDerived = 4;
    }

    /// <summary>带一个「收不了」的成员的夹具：单独一个资产，好让告警只出现在它自己的用例里。</summary>
    internal sealed class ReflectedUnsupportedFixture : ScriptableObject
    {
        /// <summary>只写属性：取值访问器编译不出来，该被跳过。</summary>
        [ShowInInspector]
        public int WriteOnly
        {
            set => _eaten = value;
        }

        /// <summary>读一下被吃掉的值——顺带免得 <c>_eaten</c> 落进「赋值了却没用过」的告警。</summary>
        public int Eaten => _eaten;

        /// <summary>一个被吃掉的值，用来让上面的 setter 有实际内容。</summary>
        private int _eaten;
    }
}
