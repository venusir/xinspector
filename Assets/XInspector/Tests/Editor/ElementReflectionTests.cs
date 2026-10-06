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
    /// 元素层的**读路径**：条件族指向同一元素实例上的反射成员与方法、元素里的
    /// <c>[ShowInInspector]</c> 与 <c>[Button]</c>。
    /// <para>
    /// 夹具的关键与嵌套层同款：**拥有者上放着同名且效果相反的陷阱**——不这么放假的话，
    /// 「解析到了拥有者」与「解析到了元素实例」在某些取值下结果相同，就测不出「看错了对象」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ElementReflectionTests
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

        #region 元素里的反射成员

        /// <summary>元素里的 <c>[ShowInInspector]</c> 成为节点，挂在**那个元素**之下。</summary>
        [Test]
        public void 元素里的反射成员成为节点()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "items.Array.data[0].Doubled");

                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.ReflectedMember));
                Assert.That(node.Parent.Path, Is.EqualTo("items.Array.data[0]"));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>取值读的是**那个元素实例**，且两个元素各是各的。</summary>
        [Test]
        public void 元素反射成员读的是元素实例()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(Read(Find(tree.Root, "items.Array.data[0].Doubled")), Is.EqualTo(4), "value 为 2。");
                Assert.That(Read(Find(tree.Root, "items.Array.data[1].Doubled")), Is.EqualTo(6), "第二个元素是 3。");

                SetInt(target, tree, "items.Array.data[0].value", 5);

                Assert.That(
                    Read(Find(tree.Root, "items.Array.data[0].Doubled")),
                    Is.EqualTo(10),
                    "改元素里的字段，读到的值应当跟着变。");
                Assert.That(
                    Read(Find(tree.Root, "items.Array.data[1].Doubled")),
                    Is.EqualTo(6),
                    "另一个元素不受影响——逐元素编译，不串号。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>元素反射成员仍是只读的——写入口恒抛（与嵌套层、根层同一条承诺）。</summary>
        [Test]
        public void 元素反射成员不可写()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var entry = Find(tree.Root, "items.Array.data[0].Doubled").ValueEntry;

                Assert.That(entry.IsUnityBacked, Is.False);
                Assert.That(entry.SerializedProperty, Is.Null);
                Assert.Throws<NotSupportedException>(() => entry.SetValue(entry.GetValue()));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 元素里的反射条件

        /// <summary>条件指向**元素实例**的非序列化属性——拥有者上的同名属性恒为真，作陷阱。</summary>
        [Test]
        public void 元素条件指向非序列化属性()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "items.Array.data[0].gatedByProperty");

                Assert.That(gated.IsVisible, Is.True, "起点：value > 0。");

                SetInt(target, tree, "items.Array.data[0].value", 0);

                Assert.That(
                    gated.IsVisible,
                    Is.False,
                    "拥有者的同名属性恒为真——它若被解析到，这里就会一直显示。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>条件指向**元素实例**的无参返回 bool 的方法。</summary>
        [Test]
        public void 元素条件指向无参方法()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "items.Array.data[0].gatedByMethod");

                Assert.That(gated.IsVisible, Is.True);

                SetInt(target, tree, "items.Array.data[0].value", 0);

                Assert.That(gated.IsVisible, Is.False, "拥有者的同名方法恒为真，不该被解析到。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>条件指向**元素实例**的非序列化字段。</summary>
        [Test]
        public void 元素条件指向非序列化字段()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "items.Array.data[0].gatedByField");

                Assert.That(gated.IsVisible, Is.True, "元素实例的 _active 初值为真。");

                target.items[0].Deactivate();

                Assert.That(gated.IsVisible, Is.False, "把元素实例上的字段改掉，条件应当跟着走。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>控制项：同层的**序列化**成员仍优先于反射那一级。</summary>
        [Test]
        public void 元素条件优先解析同层序列化成员()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "items.Array.data[0].gatedBySibling");

                Assert.That(gated.IsVisible, Is.True);

                SetBool(target, tree, "items.Array.data[0].serialized", false);

                Assert.That(gated.IsVisible, Is.False, "同层的序列化开关才是条件指的那个。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>换掉整个元素实例之后条件跟着走——链每帧现读，不是绑死某个实例。</summary>
        [Test]
        public void 元素实例换掉之后条件跟着走()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "items.Array.data[0].gatedByProperty");

                Assert.That(gated.IsVisible, Is.True, "初始值让条件为真。");

                target.items[0] = new ElementReflected { value = 0 };

                Assert.That(gated.IsVisible, Is.False, "换掉整个元素实例之后条件应当跟着变。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 元素为 <c>null</c> 时：条件算假、读值给不出（「—」）、**全程不抛**。
        /// </summary>
        [Test]
        public void 元素为空时不抛且读不到值()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "items.Array.data[0].gatedByProperty");
                var doubled = Find(tree.Root, "items.Array.data[0].Doubled");
                var entry = doubled.ValueEntry as ReflectedValueEntry;

                target.items[0] = null;

                Assert.DoesNotThrow(() =>
                {
                    Assert.That(gated.IsVisible, Is.False, "取不到实例，条件按假处置。");

                    Assert.That(
                        entry.TryGetDisplayValue(out _, out var mixed, out _),
                        Is.False);
                    Assert.That(mixed, Is.True, "取不到实例 = 「不一致」——终端据此画「—」。");
                });
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 元素里的按钮

        /// <summary>元素里的方法节点挂在元素之下，路径带元素前缀。</summary>
        [Test]
        public void 元素里的按钮解析到元素实例()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "items.Array.data[0].Heal()");
                var state = node.State.Get<ButtonState>();

                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.Method));
                Assert.That(node.Parent.Path, Is.EqualTo("items.Array.data[0]"));
                Assert.That(state, Is.Not.Null, "构建期应当已经解析过按钮。");
                Assert.That(state.Reason, Is.Null);
                Assert.That(
                    state.Methods[0].DeclaringType,
                    Is.EqualTo(typeof(ElementReflected)),
                    "拥有者上有同名按钮作陷阱——解析到它就会拿到拥有者的那份。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **两个元素各改各的**：按各自元素节点的作用域调用，只有对应那个元素实例被改到，
        /// 拥有者上的同名按钮一次都没被调。
        /// </summary>
        [Test]
        public void 两个元素的按钮各改各的()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var first = Find(tree.Root, "items.Array.data[0].Heal()").State.Get<ButtonState>();
                var second = Find(tree.Root, "items.Array.data[1].Heal()").State.Get<ButtonState>();

                MethodInvoker.Invoke(first.Methods, tree.Targets, first.Scopes, null, false, "测试");

                Assert.That(target.items[0].value, Is.EqualTo(100), "第一个元素被治好了。");
                Assert.That(target.items[1].value, Is.EqualTo(3), "第二个元素原封不动。");
                Assert.That(target.ownerHeals, Is.EqualTo(0), "拥有者上的同名按钮不该被调到。");

                MethodInvoker.Invoke(second.Methods, tree.Targets, second.Scopes, null, false, "测试");

                Assert.That(target.items[1].value, Is.EqualTo(100));
                Assert.That(target.ownerHeals, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>元素为空时按钮**跳过该目标**（不抛、也不拿别人的实例冒充）。</summary>
        [Test]
        public void 元素为空时按钮跳过()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var state = Find(tree.Root, "items.Array.data[0].Heal()").State.Get<ButtonState>();

                target.items[0] = null;

                Assert.DoesNotThrow(() =>
                    MethodInvoker.Invoke(state.Methods, tree.Targets, state.Scopes, null, false, "测试"));
                Assert.That(target.items[1].value, Is.EqualTo(3), "没有被误伤。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 重建之后

        /// <summary>增删重建之后元素里的反射成员仍然正确：新节点拿到**新**的作用域。</summary>
        /// <remarks>
        /// 元素层是同步投影（见 <see cref="CollectionElementSync"/>）：增删之后整层重建，
        /// 重建流水线里会重新收集反射成员、重新编译作用域——这条钉住那条流水线在元素侧
        /// 也是完整的（缺一环的症状是「增删一次之后反射成员读的是旧实例」）。
        /// </remarks>
        [Test]
        public void 重建后元素反射成员仍正确()
        {
            var target = ScriptableObject.CreateInstance<ElementReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var items = Find(tree.Root, "items");

                // 外部把列表加长（模拟增删 / 撤销），对账会整层重建。
                var serializedObject = new SerializedObject(target);
                serializedObject.FindProperty("items").arraySize = 3;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                tree.SerializedObject.Update();

                Assert.That(CollectionElementSync.Reconcile(items), Is.True, "长度变了就该重建。");

                var doubled = Find(tree.Root, "items.Array.data[2].Doubled");
                Assert.That(Read(doubled), Is.EqualTo(6), "新元素是末元素的副本（value 3 → Doubled 6）。");

                SetInt(target, tree, "items.Array.data[2].value", 7);
                Assert.That(Read(doubled), Is.EqualTo(14), "重建出来的节点读的是**那个元素**的活值。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 深度 2 的读路径（拥有者 / 外层元素 / 内层元素三层同名）

        /// <summary>内层元素的反射成员读的是**内层**元素实例（三层同名、值各不相同）。</summary>
        [Test]
        public void 内层元素的反射成员读的是内层元素实例()
        {
            var target = ScriptableObject.CreateInstance<DeepReflectionFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(
                    Read(Find(tree.Root, "outers.Array.data[0].inners.Array.data[0].Doubled")),
                    Is.EqualTo(14),
                    "内层 value 为 7；外层是 33、拥有者是 -1——读错一层这里就是别的数。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>内层元素的按钮调的是内层实例；外层与拥有者上的同名按钮一次都没被调。</summary>
        [Test]
        public void 内层元素的按钮调的是内层元素实例()
        {
            var target = ScriptableObject.CreateInstance<DeepReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var node = Find(tree.Root, "outers.Array.data[0].inners.Array.data[0].Heal()");
                var state = node.State.Get<ButtonState>();

                Assert.That(node.Kind, Is.EqualTo(InspectorPropertyKind.Method));
                Assert.That(
                    state.Methods[0].DeclaringType,
                    Is.EqualTo(typeof(DeepReflected)),
                    "外层与拥有者上都有同名按钮作陷阱。");

                MethodInvoker.Invoke(state.Methods, tree.Targets, state.Scopes, null, false, "测试");

                var outer = target.outers[0];
                Assert.That(outer.inners[0].value, Is.EqualTo(700), "内层被治好了。");
                Assert.That(outer.value, Is.EqualTo(3), "外层原封不动。");
                Assert.That(outer.outerHeals, Is.EqualTo(0));
                Assert.That(target.ownerHeals, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>内层元素的条件指向**内层实例**的非序列化属性（外层与拥有者的同名属性恒为真）。</summary>
        [Test]
        public void 内层元素的条件指向内层元素()
        {
            var target = ScriptableObject.CreateInstance<DeepReflectionFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(
                    Find(tree.Root, "outers.Array.data[0].inners.Array.data[0].gatedByInner").IsVisible,
                    Is.False,
                    "内层的 Enabled 恒为假——解析到外层或拥有者就会「永远显示」。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>内层元素的按名条件先找**同层**的序列化成员（内层的 flag 为真、外层的为假）。</summary>
        [Test]
        public void 按名找成员在内层先找同层()
        {
            var target = ScriptableObject.CreateInstance<DeepReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "outers.Array.data[0].inners.Array.data[0].gatedByInnerFlag");

                Assert.That(gated.IsVisible, Is.True, "内层的 flag 为真；外层的是假。");

                SetBool(target, tree, "outers.Array.data[0].inners.Array.data[0].flag", false);

                Assert.That(gated.IsVisible, Is.False, "改内层的开关立刻跟随。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **不查中间祖先**：条件名内层没有、外层有、根也有——两级语义（最近容器 → 根绝对名）
        /// 取的是**根**，外层那份不进候选。
        /// </summary>
        [Test]
        public void 按名解析不查中间祖先()
        {
            var target = ScriptableObject.CreateInstance<DeepReflectionFixture>();
            try
            {
                var tree = BuildTree(target);

                Assert.That(
                    Find(tree.Root, "outers.Array.data[0].inners.Array.data[0].gatedByRoot").IsVisible,
                    Is.False,
                    "根上的 outerOnly 为假——若查询了中间祖先（外层为真）这里就会显示。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>内层元素为 <c>null</c> 时读值给不出（「—」）、条件算假，**全程不抛**。</summary>
        [Test]
        public void 内层元素为空时读路径不抛()
        {
            var target = ScriptableObject.CreateInstance<DeepReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "outers.Array.data[0].inners.Array.data[0].gatedByInner");
                var doubled = Find(tree.Root, "outers.Array.data[0].inners.Array.data[0].Doubled");
                var entry = doubled.ValueEntry as ReflectedValueEntry;

                target.outers[0].inners[0] = null;

                Assert.DoesNotThrow(() =>
                {
                    Assert.That(gated.IsVisible, Is.False, "取不到实例，条件按假处置。");

                    Assert.That(
                        entry.TryGetDisplayValue(out _, out var mixed, out _),
                        Is.False);
                    Assert.That(mixed, Is.True, "取不到实例 = 「不一致」——终端据此画「—」。");
                });
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
        /// <param name="tree">属性树。</param>
        /// <param name="path">序列化路径。</param>
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

        /// <summary>
        /// 按**完整路径**在子树里找节点。
        /// </summary>
        /// <param name="parent">起始节点。</param>
        /// <param name="path">完整路径（元素路径里含 <c>Array.data[i]</c>，逐段拼不出来，故按路径找）。</param>
        /// <returns>节点；不存在时断言失败。</returns>
        /// <remarks>元素层的节点夹在元素节点/分组节点之下，逐层 <c>Find</c> 也得知道中间隔了谁——直接按路径深度优先找最省事。</remarks>
        private static InspectorProperty Find(InspectorProperty parent, string path)
        {
            var found = TryFind(parent, path);
            Assert.That(found, Is.Not.Null, $"找不到节点 {path}。");
            return found;
        }

        /// <summary>深度优先找；找不到返回 <c>null</c>。</summary>
        /// <param name="node">当前节点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；没有返回 <c>null</c>。</returns>
        private static InspectorProperty TryFind(InspectorProperty node, string path)
        {
            if (string.Equals(node.Path, path, StringComparison.Ordinal))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var found = TryFind(child, path);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        #endregion
    }

    /// <summary>
    /// 元素类型：反射条件（属性 / 方法 / 非序列化字段 / 同层序列化各一）、
    /// 反射成员、以及一个改**自身字段**的按钮。
    /// </summary>
    [Serializable]
    internal class ElementReflected
    {
        /// <summary>条件的取值来源，也是按钮改的那个字段。</summary>
        public int value = 2;

        /// <summary>序列化的同层开关——序列化那一级应当先命中。</summary>
        public bool serialized = true;

        /// <summary>非序列化属性：只有反射那一级看得到它。</summary>
        public bool Enabled => value > 0;

        /// <summary>无参返回 bool 的方法。</summary>
        public bool Ready() => value > 0;

        /// <summary>私有的非序列化字段——只有反射那一级看得到它。</summary>
        [ShowInInspector]
        private bool _active = true;

        /// <summary>把上面的字段关掉（测试用）。</summary>
        public void Deactivate()
        {
            _active = false;
        }

        /// <summary>条件指向**非序列化属性**。</summary>
        [ShowIf(nameof(Enabled))]
        public int gatedByProperty;

        /// <summary>条件指向**无参方法**。</summary>
        [ShowIf(nameof(Ready))]
        public int gatedByMethod;

        /// <summary>条件指向**非序列化字段**。</summary>
        [ShowIf(nameof(_active))]
        public int gatedByField;

        /// <summary>条件指向同层的**序列化**成员。</summary>
        [ShowIf(nameof(serialized))]
        public int gatedBySibling;

        /// <summary>反射成员——元素里的读路径让它画得出来。</summary>
        [ShowInInspector]
        public int Doubled => value * 2;

        /// <summary>元素里的按钮：改的是**这个元素实例**上的字段。</summary>
        [Button("元素按钮：满血")]
        public void Heal()
        {
            value = 100;
        }
    }

    /// <summary>元素层反射的对照资产：陷阱（同名且效果相反）全放在**拥有者**上。</summary>
    [HideMonoScript]
    internal sealed class ElementReflectionFixture : ScriptableObject
    {
        /// <summary>陷阱：拥有者上的同名属性。它恒为真——解析到拥有者就会「永远显示」。</summary>
        public bool Enabled => true;

        /// <summary>陷阱：拥有者上的同名方法。同样恒为真。</summary>
        public bool Ready() => true;

        /// <summary>陷阱的计分板：拥有者上的同名按钮若被调到，改的是这里。</summary>
        public int ownerHeals;

        /// <summary>陷阱：拥有者上的同名按钮。</summary>
        [Button("拥有者按钮")]
        public void Heal()
        {
            ownerHeals++;
        }

        /// <summary>元素层：反射条件、反射成员与按钮都在里面（两个元素，值不同）。</summary>
        [ListDrawerSettings]
        public List<ElementReflected> items = new List<ElementReflected>
        {
            new ElementReflected(),
            new ElementReflected { value = 3 },
        };
    }

    /// <summary>深度 2 的**内层**元素：三层同名陷阱的最内那一层。</summary>
    [Serializable]
    internal class DeepReflected
    {
        /// <summary>内层的值（与拥有者 / 外层都不同，用来识别读到了谁）。</summary>
        public int value = 7;

        /// <summary>内层的开关——三层同名里**只有内层**为真。</summary>
        public bool flag = true;

        /// <summary>三层同名的反射属性：内层的恒为**假**（外层与拥有者恒为真）。</summary>
        public bool Enabled => false;

        /// <summary>三层同名的反射成员（内层 14、外层 33、拥有者 -1）。</summary>
        [ShowInInspector]
        public int Doubled => value * 2;

        /// <summary>条件指向内层实例的非序列化属性（外层与拥有者的同名属性恒为真）。</summary>
        [ShowIf(nameof(Enabled))]
        public int gatedByInner;

        /// <summary>条件指向**同层**（内层）的序列化开关。</summary>
        [ShowIf(nameof(flag))]
        public int gatedByInnerFlag;

        /// <summary>条件名内层没有、外层有、根也有——两级语义下取**根**（不为中间祖先让路）。</summary>
        [ShowIf("outerOnly")]
        public int gatedByRoot;

        /// <summary>内层按钮：改的是内层实例。</summary>
        [Button("内层按钮")]
        public void Heal()
        {
            value = 700;
        }
    }

    /// <summary>深度 2 的**外层**元素：里面有内层集合，自己带着同名的陷阱。</summary>
    [Serializable]
    internal class DeepOuterElement
    {
        /// <summary>外层的值。</summary>
        public int value = 3;

        /// <summary>外层同名开关——为假（内层为真，用来验证「先找最近的容器」）。</summary>
        public bool flag = false;

        /// <summary>中间祖先独有的开关——两级语义下**不该**被查到。</summary>
        public bool outerOnly = true;

        /// <summary>外层同名反射属性——恒为真。</summary>
        public bool Enabled => true;

        /// <summary>外层同名反射成员——33（内层是 14）。</summary>
        [ShowInInspector]
        public int Doubled => 30 + value;

        /// <summary>外层的计分板。</summary>
        public int outerHeals;

        /// <summary>外层同名按钮。</summary>
        [Button("外层按钮")]
        public void Heal()
        {
            outerHeals++;
        }

        /// <summary>内层集合（深度 &gt; 1 起节点化）。</summary>
        [ListDrawerSettings]
        public List<DeepReflected> inners = new List<DeepReflected> { new DeepReflected() };
    }

    /// <summary>深度 2 的读路径对照资产：拥有者、外层元素、内层元素**三层同名陷阱**。</summary>
    [HideMonoScript]
    internal sealed class DeepReflectionFixture : ScriptableObject
    {
        /// <summary>拥有者层的同名开关——恒为真。</summary>
        public bool Enabled => true;

        /// <summary>「不查中间祖先」用例的落点：根上的同名成员为**假**。</summary>
        public bool outerOnly = false;

        /// <summary>拥有者层的计分板。</summary>
        public int ownerHeals;

        /// <summary>拥有者层的同名按钮。</summary>
        [Button("拥有者按钮")]
        public void Heal()
        {
            ownerHeals++;
        }

        /// <summary>拥有者层的同名反射成员——值刻意与两层元素都不同。</summary>
        [ShowInInspector]
        public int Doubled => -1;

        /// <summary>外层集合。</summary>
        [ListDrawerSettings]
        public List<DeepOuterElement> outers = new List<DeepOuterElement> { new DeepOuterElement() };
    }
}
