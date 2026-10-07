using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 多态段的**读路径**：条件族指向同一具体实例上的反射成员与方法。
    /// <para>
    /// 夹具的关键与嵌套层 / 元素层同款：**根上放着同名且值相反的陷阱**——不这么放假的话，
    /// 「解析到了根上」与「解析到了多态实例」在某些取值下结果相同，就测不出「条件看错了对象」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PolymorphicReflectionTests
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

        #region 多态段的反射条件

        /// <summary>条件指向**多态实例**的非序列化属性——根上的同名属性恒为真，作陷阱。</summary>
        [Test]
        public void 多态段里的条件指向实例上的属性()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "shape.gatedByProperty");

                Assert.That(gated.IsVisible, Is.True, "起点：value > 0。");

                SetInt(target, tree, "shape.value", 0);

                Assert.That(
                    gated.IsVisible,
                    Is.False,
                    "根上的同名属性恒为真——它若被解析到，这里就会一直显示。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>条件指向**多态实例**的无参返回 bool 的方法。</summary>
        [Test]
        public void 多态段里的条件指向实例上的无参方法()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "shape.gatedByMethod");

                Assert.That(gated.IsVisible, Is.True);

                SetInt(target, tree, "shape.value", 0);

                Assert.That(gated.IsVisible, Is.False, "根上的同名方法恒为真，不该被解析到。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>控制项：同层的**序列化**成员仍优先于反射那一级（这条今天就不许红）。</summary>
        [Test]
        public void 多态段里的条件先找同层序列化成员()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "shape.gatedBySibling");

                Assert.That(gated.IsVisible, Is.True);

                SetBool(target, tree, "shape.serialized", false);

                Assert.That(gated.IsVisible, Is.False, "同层的序列化开关才是条件指的那个。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 多态段里的读路径成员

        /// <summary>多态段里的 <c>[ShowInInspector]</c> 成为节点，挂在**多态容器**之下。</summary>
        [Test]
        public void 多态段里的反射成员成为节点()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "shape.Doubled");

                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.ReflectedMember));
                Assert.That(node.Parent.Path, Is.EqualTo("shape"), "挂在多态容器之下。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>取值读的是**多态实例**，不是根对象，且跟着实例走（根上同名是陷阱）。</summary>
        [Test]
        public void 多态段里的反射成员读的是实例()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "shape.Doubled");

                Assert.That(Read(node), Is.EqualTo(4), "value 为 2，Doubled 应当是 4（根上的陷阱是 -1）。");

                SetInt(target, tree, "shape.value", 5);

                Assert.That(Read(node), Is.EqualTo(10), "改实例里的字段，读到的值应当跟着变。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>多态反射成员仍是只读的——写入口恒抛（与根层、嵌套层、元素层同一条承诺）。</summary>
        [Test]
        public void 多态段里的反射成员不可写()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var entry = Find(tree.Root, "shape.Doubled").ValueEntry;

                Assert.That(entry.IsUnityBacked, Is.False);
                Assert.That(entry.SerializedProperty, Is.Null);
                Assert.Throws<NotSupportedException>(() => entry.SetValue(entry.GetValue()));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **只放** <c>[ShowInInspector]</c> 的具体类型也会展开——它一个可见的序列化子字段都没有。
        /// </summary>
        /// <remarks>
        /// 这条钉住的是多态支上的「第二道腿」：展开判据里那条 <c>hasVisibleChildren</c> 若不放宽，
        /// 这种类型连门都进不了，里面的读路径成员永远没机会生效——而且**没有告警**。
        /// </remarks>
        [Test]
        public void 只带反射成员的具体类型也会展开()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyOnlyReflected();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "shape.Tag");

                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.ReflectedMember));
                Assert.That(Read(node), Is.EqualTo(7));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>静态反射成员照样收——跨目标一致、与实例无关。</summary>
        [Test]
        public void 多态段里的静态反射成员照样收()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                Assert.That(Read(Find(tree.Root, "shape.Shared")), Is.EqualTo(42));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 路径**穿过**多态段的嵌套复合成员：里面的反射成员同样生效（<c>shape.inner.Doubled</c>）。
        /// </summary>
        [Test]
        public void 穿过多态段的嵌套复合成员的反射成员生效()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "shape.inner.Doubled");

                Assert.That(Read(node), Is.EqualTo(10), "inner.value 为 5。");

                SetInt(target, tree, "shape.inner.value", 7);

                Assert.That(Read(node), Is.EqualTo(14), "改嵌套实例里的字段，读到的值应当跟着变。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 路径**穿段 + 过索引对**：多态段里集合的元素里的反射成员同样生效。
        /// </summary>
        [Test]
        public void 穿过多态段的元素层里的反射成员生效()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "shape.points.Array.data[0].Doubled");

                Assert.That(Read(node), Is.EqualTo(6), "元素 value 为 3。");

                SetInt(target, tree, "shape.points.Array.data[0].value", 8);

                Assert.That(Read(node), Is.EqualTo(16), "改元素里的字段，读到的值应当跟着变。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 多态段里的按钮与按名回调

        /// <summary>多态段里的按钮解析到**具体实例**（根上同名按钮是陷阱）。</summary>
        [Test]
        public void 多态段里的按钮解析到实例()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "shape.Heal()");
                var state = node.State.Get<ButtonState>();

                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.Method));
                Assert.That(node.Parent.Path, Is.EqualTo("shape"));
                Assert.That(state, Is.Not.Null, "构建期应当已经解析过按钮。");
                Assert.That(state.Reason, Is.Null);
                Assert.That(
                    state.Methods[0].DeclaringType,
                    Is.EqualTo(typeof(PolyReflected)),
                    "根上有同名按钮作陷阱——解析到它就会拿到根上那份。");

                MethodInvoker.Invoke(state.Methods, tree.Targets, state.Scopes, null, false, "测试");

                Assert.That(((PolyReflected)target.shape).value, Is.EqualTo(100), "段内实例被治好了。");
                Assert.That(target.ownerHeals, Is.EqualTo(0), "根上的同名按钮一次都没被调到。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>多态段里的行内按钮解析到具体实例（按名解析走同一份作用域）。</summary>
        [Test]
        public void 多态段里的行内按钮解析到实例()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var state = Find(tree.Root, "shape.pinged").State.Get<InlineButtonState>();

                Assert.That(state, Is.Not.Null, "构建期应当已经解析过行内按钮。");
                Assert.That(state.Methods[0][0], Is.Not.Null);
                Assert.That(state.Methods[0][0].DeclaringType, Is.EqualTo(typeof(PolyReflected)));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>多态段里的按名回调（<c>[OnValueChanged]</c> 与 <c>[CustomContextMenu]</c>）解析到具体实例。</summary>
        [Test]
        public void 多态段里的按名回调解析到实例()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);

                var changed = Find(tree.Root, "shape.watched").State.Get<ValueChangedState>();
                Assert.That(changed, Is.Not.Null);
                Assert.That(changed.Entries[0].Methods[0], Is.Not.Null);
                Assert.That(changed.Entries[0].Methods[0].DeclaringType, Is.EqualTo(typeof(PolyReflected)));

                var menu = Find(tree.Root, "shape.menu").State.Get<ContextMenuState>();
                Assert.That(menu, Is.Not.Null);
                Assert.That(menu.Entries[0].Methods[0], Is.Not.Null);
                Assert.That(menu.Entries[0].Methods[0].DeclaringType, Is.EqualTo(typeof(PolyReflected)));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 换实现 / 清空槽位

        /// <summary>换实现之后读路径跟着换：旧反射成员/按钮撤掉，新的按新类型解析出来。</summary>
        [Test]
        public void 换实现之后读路径跟着换()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");
                Assert.That(Find(shape, "shape.Doubled"), Is.Not.Null, "起点：旧的反射成员在。");

                target.shape = new PolySwapped();
                tree.SerializedObject.Update();

                Assert.That(PolymorphicReferenceSync.ReconcileAll(tree), Is.EqualTo(1), "对账发现类型变了。");

                var rebuilt = Find(tree.Root, "shape");
                Assert.That(rebuilt, Is.SameAs(shape), "容器节点自己不动。");
                Assert.That(Search(rebuilt, "shape.Doubled"), Is.Null, "旧类型的反射成员撤掉。");
                Assert.That(Search(rebuilt, "shape.Heal()"), Is.Null, "旧按钮撤掉。");
                Assert.That(Search(rebuilt, "shape.Label"), Is.Not.Null, "新类型的反射成员在。");
                Assert.That(Search(rebuilt, "shape.Swap()"), Is.Not.Null, "新按钮解析出来。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>清空槽位之后读路径节点一并撤掉（空槽位 = 无类型可问）。</summary>
        [Test]
        public void 清空槽位之后读路径节点一并撤掉()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var shape = Find(tree.Root, "shape");
                Assert.That(Find(shape, "shape.Doubled"), Is.Not.Null, "起点：反射成员在。");

                target.shape = null;
                tree.SerializedObject.Update();

                Assert.That(PolymorphicReferenceSync.ReconcileAll(tree), Is.EqualTo(1));

                Assert.That(shape.Children.Count, Is.EqualTo(0), "子节点撤干净——连读路径的一起。");
                Assert.That(shape.Type, Is.EqualTo(typeof(IPolyShape)), "类型退回声明类型。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>读一次反射成员的显示值。</summary>
        /// <param name="node">反射成员节点。</param>
        /// <returns>读到的值。</returns>
        private static object Read(InspectorProperty node)
        {
            var entry = node.ValueEntry as ReflectedValueEntry;
            Assert.That(entry, Is.Not.Null, $"{node.Path} 不是反射成员节点。");

            Assert.That(entry.TryGetDisplayValue(out var value, out var mixed, out var error), Is.True, error);
            Assert.That(mixed, Is.False);
            return value;
        }

        /// <summary>构建被测的树。</summary>
        /// <param name="target">目标资产。</param>
        /// <returns>属性树。</returns>
        private static PropertyTree BuildTree(ScriptableObject target)
        {
            return PropertyTree.Create(new SerializedObject(target));
        }

        /// <summary>改一个 int 字段的值，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树——条件求值器读的是**树自己的**序列化对象。</param>
        /// <param name="path">序列化路径（可点分）。</param>
        /// <param name="value">新值。</param>
        private static void SetInt(ScriptableObject target, PropertyTree tree, string path, int value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        /// <summary>改一个 bool 字段的值，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="path">序列化路径。</param>
        /// <param name="value">新值。</param>
        private static void SetBool(ScriptableObject target, PropertyTree tree, string path, bool value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        /// <summary>递归查找节点（按完整路径）。</summary>
        /// <param name="root">搜索起点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；不存在时断言失败。</returns>
        /// <remarks>多态段里的节点夹在容器/分组节点之下，直接按路径深度优先找最省事。</remarks>
        private static InspectorProperty Find(InspectorProperty root, string path)
        {
            var found = Search(root, path);

            Assert.That(found, Is.Not.Null, $"找不到节点 {path}。");
            return found;
        }

        /// <summary>深度优先搜索（找不到返回 <c>null</c>，由 <see cref="Find"/> 负责断言）。</summary>
        /// <param name="node">当前节点。</param>
        /// <param name="path">目标路径。</param>
        /// <returns>节点；不存在返回 <c>null</c>。</returns>
        private static InspectorProperty Search(InspectorProperty node, string path)
        {
            foreach (var child in node.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }

                var nested = Search(child, path);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        #endregion
    }

    /// <summary>多态段读路径的槽位类型——声明成接口是刻意的（多态引用的主战场）。</summary>
    internal interface IPolyShape
    {
    }

    /// <summary>
    /// 用得到本包的具体类型：反射条件、读路径成员（含穿段的两处）、按钮与按名回调都在里面。
    /// </summary>
    [Serializable]
    internal class PolyReflected : IPolyShape
    {
        /// <summary>条件的取值来源，也是按钮要改的那个字段。</summary>
        public int value = 2;

        /// <summary>序列化的同层开关——序列化那一级应当先命中。</summary>
        public bool serialized = true;

        /// <summary>非序列化属性：只有反射那一级看得到它。</summary>
        public bool Enabled => value > 0;

        /// <summary>无参返回 bool 的方法。</summary>
        public bool Ready() => value > 0;

        /// <summary>条件指向**非序列化属性**。</summary>
        [ShowIf(nameof(Enabled))]
        public int gatedByProperty;

        /// <summary>条件指向**无参方法**。</summary>
        [ShowIf(nameof(Ready))]
        public int gatedByMethod;

        /// <summary>条件指向同层的**序列化**成员。</summary>
        [ShowIf(nameof(serialized))]
        public int gatedBySibling;

        /// <summary>反射成员——多态段里的读路径让它画得出来（根上同名的是陷阱，恒为 -1）。</summary>
        [ShowInInspector]
        public int Doubled => value * 2;

        /// <summary>静态反射成员——跨目标一致、与实例无关。</summary>
        [ShowInInspector]
        public static int Shared => 42;

        /// <summary>多态段里的按钮：改的是**这个实例**上的字段。</summary>
        [Button("多态按钮：满血")]
        public void Heal()
        {
            value = 100;
        }

        /// <summary>行内按钮的目标方法。</summary>
        public void Ping()
        {
            pinged = true;
        }

        /// <summary>挂着行内按钮的字段——按名解析要在**这个实例**上找。</summary>
        [InlineButton(nameof(Ping), "行内")]
        public bool pinged;

        /// <summary>值变化回调的目标方法。</summary>
        public void OnWatchedChanged()
        {
            pinged = true;
        }

        /// <summary>值变化监听——回调要在**这个实例**上解析。</summary>
        [OnValueChanged(nameof(OnWatchedChanged))]
        public int watched;

        /// <summary>右键菜单项——同上，按名解析。</summary>
        [CustomContextMenu("段内菜单", nameof(OnWatchedChanged))]
        public int menu;

        /// <summary>穿段用的嵌套复合成员（它自己带反射成员）。</summary>
        public PolyInner inner = new PolyInner();

        /// <summary>穿段 + 索引段用的元素集合。</summary>
        [ListDrawerSettings]
        public List<PolyElement> points = new List<PolyElement>
        {
            new PolyElement { value = 3 },
        };
    }

    /// <summary>多态段里的嵌套复合成员——路径要**穿过**多态段才够得着它。</summary>
    [Serializable]
    internal class PolyInner
    {
        /// <summary>反射成员读的就是它。</summary>
        public int value = 5;

        /// <summary>穿段之后要看的反射成员。</summary>
        [ShowInInspector]
        public int Doubled => value * 2;
    }

    /// <summary>多态段里的集合元素——路径要**穿段 + 过索引对**才够得着它。</summary>
    [Serializable]
    internal class PolyElement
    {
        /// <summary>反射成员读的就是它。</summary>
        public int value = 3;

        /// <summary>元素里的反射成员。</summary>
        [ShowInInspector]
        public int Doubled => value * 2;
    }

    /// <summary>**只放**反射成员的具体类型——一个可见的序列化子字段都没有。</summary>
    [Serializable]
    internal class PolyOnlyReflected : IPolyShape
    {
        /// <summary>非序列化属性。</summary>
        [ShowInInspector]
        public int Tag => 7;
    }

    /// <summary>换掉具体类型时用的另一个实现——读路径成员与按钮都与 <see cref="PolyReflected"/> 不同。</summary>
    [Serializable]
    internal class PolySwapped : IPolyShape
    {
        /// <summary>换过去之后要看得见的新反射成员。</summary>
        [ShowInInspector]
        public string Label => "换过了";

        /// <summary>换过去之后要解析得到的新按钮。</summary>
        [Button("换过的按钮")]
        public void Swap()
        {
        }
    }

    /// <summary>多态段读路径的对照资产：陷阱（同名且效果相反）全放在**根**上。</summary>
    [HideMonoScript]
    internal sealed class PolymorphicReflectionFixture : ScriptableObject
    {
        /// <summary>陷阱：根上的同名属性。它恒为真——解析到根上就会「永远显示」。</summary>
        public bool Enabled => true;

        /// <summary>陷阱：根上的同名方法。同样恒为真。</summary>
        public bool Ready() => true;

        /// <summary>陷阱：根上的同名反射成员——段内读 4/10/…（instance 的 value×2），它恒为 -1。</summary>
        [ShowInInspector]
        public int Doubled => -1;

        /// <summary>陷阱的计分板：根上的同名按钮若被调到，改的是这里。</summary>
        public int ownerHeals;

        /// <summary>陷阱：根上的同名按钮。</summary>
        [Button("根上的按钮")]
        public void Heal()
        {
            ownerHeals++;
        }

        /// <summary>多态段：条件、读路径成员、按钮与按名回调都在里面。</summary>
        [SerializeReference]
        public IPolyShape shape;
    }
}
