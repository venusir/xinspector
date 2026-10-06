using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 「按名找成员」那一层的四级阶梯：嵌套同层 → 根绝对名 → 反射字段/属性 → 无参方法。
    /// <para>
    /// 断的是**解析结果本身**（绑到了哪一个序列化成员、读取器读到什么），
    /// 而不是某个特性的最终外观——阶梯是这一层的能力，条件族与值绘制族都只是它的消费者。
    /// </para>
    /// <para>
    /// 夹具的关键在**两级同名的陷阱**：根与嵌套层各有一个 <c>flag</c>、值相反。
    /// 不这么放假的话，「解析到了同层」与「解析到了根上」在某些取值下结果相同，
    /// 「看错对象」这一类缺陷就测不出来。
    /// </para>
    /// </summary>
    [TestFixture]
    public class MemberReferenceResolverTests
    {
        #region Teardown

        /// <summary>复位静态门面：建树会初始化绘制器与处理器两张注册表。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region bool：四级阶梯

        /// <summary>第 0 级：嵌套层里的同层序列化兄弟成员先命中，**根上的同名成员不参与**。</summary>
        [Test]
        public void 嵌套同层优先于根绝对名()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = Find(tree.Root, "nested.anchor");
                    var resolved = ResolveBoolean(node, "flag");

                    Assert.That(resolved.Serialized, Is.Not.Null);
                    Assert.That(resolved.Serialized.propertyPath, Is.EqualTo("nested.flag"), "绑的是同层那个。");
                    Assert.That(resolved.Read(), Is.False, "同层 flag 为假——读到真就说明解析到了根上。");
                    Assert.That(target.flag, Is.True, "控制项：根上的同名成员确实是真。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>第 1 级：同层没有这个成员时回落到根上的绝对名。</summary>
        [Test]
        public void 同层没有时回落到根绝对名()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var resolved = ResolveBoolean(Find(tree.Root, "nested.anchor"), "rootOnly");

                    Assert.That(resolved.Serialized.propertyPath, Is.EqualTo("rootOnly"));
                    Assert.That(resolved.Read(), Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **找到了但类型不符即停**：同层那个是 <c>int</c>，根上那个是 <c>bool</c>——
        /// 不许继续往下找到根上去。
        /// </summary>
        /// <remarks>
        /// 继续找的话，同一个名字会报第二次警，而两条消息互相矛盾
        /// （一句说「找到了但类型不对」、一句说「找不到」）。这条次序是刻意的。
        /// </remarks>
        [Test]
        public void 同层类型不符时不再往下找()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = Find(tree.Root, "nested.anchor");

                    Assert.That(
                        MemberReferenceResolver.TryResolveBoolean(
                            node, "level", MemberScope.Object, out var read, out _, out var reason),
                        Is.False);
                    Assert.That(read, Is.Null);
                    Assert.That(reason, Does.Contain("不是 bool"));
                    Assert.That(reason, Does.Contain("Integer"), "说清楚实到的是什么类型。");

                    // 控制项：根上确实有一个同名的 bool——不这么放假就分不出「停住了」与「本来就没有」。
                    Assert.That(target.level, Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>第 2 级（容器层）：嵌套实例上的**非序列化属性**。</summary>
        [Test]
        public void 嵌套层解析到实例上的属性()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = Find(tree.Root, "nested.anchor");
                    var resolved = ResolveBoolean(node, "Enabled");

                    Assert.That(resolved.Serialized, Is.Null, "反射成员没有可写句柄。");
                    Assert.That(resolved.Read(), Is.False, "读的是嵌套实例的 sibling（假），不是根上的同名属性。");

                    SetBool(target, tree, "nested.sibling", true);

                    Assert.That(resolved.Read(), Is.True, "每帧现读，改值之后不必重新解析。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>第 2 级的方法那一格：无参、返回 bool 的方法。</summary>
        [Test]
        public void 嵌套层解析到实例上的方法()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = Find(tree.Root, "nested.anchor");
                    var resolved = ResolveBoolean(node, "Ready");

                    Assert.That(resolved.Read(), Is.False);

                    SetBool(target, tree, "nested.sibling", true);

                    Assert.That(resolved.Read(), Is.True, "方法走的是开实例调用，每次吃当时的实例。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>第 3 级（根层）：顶层成员的条件可以指向目标对象上的普通属性。</summary>
        [Test]
        public void 顶层解析到根上的属性()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var resolved = ResolveBoolean(Find(tree.Root, "rootAnchor"), "RootProperty");

                    Assert.That(resolved.Serialized, Is.Null);
                    Assert.That(resolved.Read(), Is.True);

                    SetBool(target, tree, "flag", false);

                    Assert.That(resolved.Read(), Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>普通对象（POCO）树上同样解析得出来——它没有序列化对象，直接走反射那一级。</summary>
        [Test]
        public void 反射树上的成员引用也能解析()
        {
            var poco = new MemberReferencePoco();

            using (var tree = PropertyTree.CreateReflected(poco))
            {
                var node = Find(tree.Root, "anchor");
                var resolved = ResolveBoolean(node, "RootProperty");

                Assert.That(resolved.Serialized, Is.Null);
                Assert.That(resolved.Read(), Is.True);

                poco.flag = false;

                Assert.That(resolved.Read(), Is.False);
            }
        }

        /// <summary>嵌套实例为空时读取器给「缺失值」（bool 是 <c>false</c>），且**不抛**。</summary>
        [Test]
        public void 嵌套实例为空时给缺失值()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = Find(tree.Root, "nested.anchor");
                    var resolved = ResolveBoolean(node, "Enabled");
                    var readFloat = ResolveFloat(node, "Span");

                    Assert.DoesNotThrow(() =>
                    {
                        target.nested = null;

                        Assert.That(resolved.Read(), Is.False);
                        Assert.That(float.IsNaN(readFloat()), Is.True, "float 的缺失值是 NaN。");
                    });

                    target.nested = new MemberReferenceNested { sibling = true };

                    Assert.That(resolved.Read(), Is.True, "换掉实例之后跟着走。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>名字一个都找不到时给出原因，不抛。</summary>
        [Test]
        public void 找不到时给出原因()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = Find(tree.Root, "nested.anchor");

                    Assert.That(
                        MemberReferenceResolver.TryResolveBoolean(
                            node, "nope", MemberScope.Object, out _, out _, out var reason),
                        Is.False);
                    Assert.That(reason, Does.Contain("找不到名为「nope」"));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 值对象内部（[Toggle] 那一格）

        /// <summary>相对范围：在**值对象内部**找（<c>[Toggle]</c> 的开关就住在那儿）。</summary>
        [Test]
        public void 相对范围解析值对象内部的成员()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = Find(tree.Root, "box");
                    var resolved = ResolveBoolean(node, "Enabled", MemberScope.Relative);

                    Assert.That(resolved.Serialized.propertyPath, Is.EqualTo("box.Enabled"));
                    Assert.That(resolved.Read(), Is.True);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 相对范围**没有反射腿**：值对象只有一个序列化句柄，没有实例可以下钻。
        /// </summary>
        [Test]
        public void 相对范围不走反射()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = Find(tree.Root, "box");

                    // Reflected 是**值对象类型上**的非序列化属性——值对象内部按名找它应当失败。
                    Assert.That(
                        MemberReferenceResolver.TryResolveBoolean(
                            node, "Reflected", MemberScope.Relative, out _, out _, out var reason),
                        Is.False);
                    Assert.That(reason, Does.Contain("值对象内部"));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region float 与 Vector2

        /// <summary>float：序列化成员、容器层反射属性、根层反射属性三条都要通。</summary>
        [Test]
        public void float的三种来源都解析得出来()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var nested = Find(tree.Root, "nested.anchor");

                    Assert.That(ReadFloat(nested, "span"), Is.EqualTo(4f), "同层的序列化 float。");

                    var byProperty = ResolveFloat(nested, "Span");
                    Assert.That(byProperty(), Is.EqualTo(8f), "嵌套实例上的反射属性。");

                    SetFloat(target, tree, "nested.span", 5f);
                    Assert.That(
                        byProperty(),
                        Is.EqualTo(10f),
                        "根上也有个同名的 Span（恒为 1）——读到 10 才说明找的是嵌套实例。");

                    Assert.That(ReadFloat(Find(tree.Root, "rootAnchor"), "RootFloat"), Is.EqualTo(10f));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>Vector2：同一套阶梯，绑到成员之后每帧现读。</summary>
        [Test]
        public void vector2的三种来源都解析得出来()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var nested = Find(tree.Root, "nested.anchor");

                    Assert.That(ReadVector2(nested, "box"), Is.EqualTo(new Vector2(0f, 9f)));
                    Assert.That(ReadVector2(nested, "Box"), Is.EqualTo(new Vector2(1f, 2f)));
                    Assert.That(
                        ReadVector2(Find(tree.Root, "rootAnchor"), "RootBox"),
                        Is.EqualTo(new Vector2(-3f, 3f)));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>类型不符同样要停住并说清楚——三种种类共用同一条判定。</summary>
        [Test]
        public void float的类型不符时给原因()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    Assert.That(
                        MemberReferenceResolver.TryResolveFloat(
                            Find(tree.Root, "rootAnchor"), "flag", out var read, out var reason),
                        Is.False);
                    Assert.That(read, Is.Null);
                    Assert.That(reason, Does.Contain("不是 float"));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>反射那一侧的字段、属性、方法三条形状都可以；返回值不对则拒绝。</summary>
        [Test]
        public void 反射方法返回值不对时给原因()
        {
            var target = ScriptableObject.CreateInstance<MemberReferenceFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    var node = Find(tree.Root, "nested.anchor");

                    Assert.That(ReadFloat(node, "Computed"), Is.EqualTo(2.5f), "无参返回 float 的方法。");

                    Assert.That(
                        MemberReferenceResolver.TryResolveFloat(node, "Ready", out _, out var reason),
                        Is.False);
                    Assert.That(reason, Does.Contain("必须返回 float"));
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>解析出来的 bool 成员引用。</summary>
        private readonly struct BooleanReference
        {
            /// <summary>每帧现读的读取器。</summary>
            public readonly Func<bool> Read;

            /// <summary>解析到序列化成员时的活句柄；反射成员为 <c>null</c>。</summary>
            public readonly SerializedProperty Serialized;

            /// <summary>构造。</summary>
            /// <param name="read">读取器。</param>
            /// <param name="serialized">活句柄。</param>
            public BooleanReference(Func<bool> read, SerializedProperty serialized)
            {
                Read = read;
                Serialized = serialized;
            }
        }

        /// <summary>解析一个 bool 成员引用，失败即断言失败。</summary>
        /// <param name="node">起点节点。</param>
        /// <param name="memberName">成员名。</param>
        /// <returns>解析结果。</returns>
        private static BooleanReference ResolveBoolean(InspectorProperty node, string memberName)
        {
            return ResolveBoolean(node, memberName, MemberScope.Object);
        }

        /// <summary>按范围解析一个 bool 成员引用，失败即断言失败。</summary>
        /// <param name="node">起点节点。</param>
        /// <param name="memberName">成员名。</param>
        /// <param name="scope">查找范围。</param>
        /// <returns>解析结果。</returns>
        private static BooleanReference ResolveBoolean(InspectorProperty node, string memberName, MemberScope scope)
        {
            var ok = MemberReferenceResolver.TryResolveBoolean(
                node, memberName, scope, out var read, out var serialized, out var reason);

            Assert.That(ok, Is.True, reason);
            return new BooleanReference(read, serialized);
        }

        /// <summary>解析一个 float 成员引用，失败即断言失败。</summary>
        /// <param name="node">起点节点。</param>
        /// <param name="memberName">成员名。</param>
        /// <returns>读取器。</returns>
        private static Func<float> ResolveFloat(InspectorProperty node, string memberName)
        {
            var ok = MemberReferenceResolver.TryResolveFloat(node, memberName, out var read, out var reason);

            Assert.That(ok, Is.True, reason);
            return read;
        }

        /// <summary>解析一个 Vector2 成员引用，失败即断言失败。</summary>
        /// <param name="node">起点节点。</param>
        /// <param name="memberName">成员名。</param>
        /// <returns>读取器。</returns>
        private static Func<Vector2> ResolveVector2(InspectorProperty node, string memberName)
        {
            var ok = MemberReferenceResolver.TryResolveVector2(node, memberName, out var read, out var reason);

            Assert.That(ok, Is.True, reason);
            return read;
        }

        /// <summary>解析并读一次 float。</summary>
        /// <param name="node">起点节点。</param>
        /// <param name="memberName">成员名。</param>
        /// <returns>当前值。</returns>
        private static float ReadFloat(InspectorProperty node, string memberName)
        {
            return ResolveFloat(node, memberName)();
        }

        /// <summary>解析并读一次 Vector2。</summary>
        /// <param name="node">起点节点。</param>
        /// <param name="memberName">成员名。</param>
        /// <returns>当前值。</returns>
        private static Vector2 ReadVector2(InspectorProperty node, string memberName)
        {
            return ResolveVector2(node, memberName)();
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

        /// <summary>改一个 float 字段的值，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="path">序列化路径。</param>
        /// <param name="value">新值。</param>
        private static void SetFloat(ScriptableObject target, PropertyTree tree, string path, float value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        /// <summary>递归查找节点（按完整路径）。</summary>
        /// <param name="root">搜索起点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；不存在时断言失败。</returns>
        private static InspectorProperty Find(InspectorProperty root, string path)
        {
            var found = Search(root, path);

            Assert.That(found, Is.Not.Null, $"找不到节点 {path}。");
            return found;
        }

        /// <summary>递归搜索（找不到返回 <c>null</c>）。</summary>
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

    /// <summary>
    /// 成员引用的夹具：根与嵌套层**各有一个同名成员，值相反**——两级的次序靠它们才测得出。
    /// </summary>
    internal sealed class MemberReferenceFixture : ScriptableObject
    {
        #region 根层

        /// <summary>根上的 <c>flag</c>（真）：嵌套层那个是假，用来证明「解析到了哪一级」。</summary>
        public bool flag = true;

        /// <summary>根上的 <c>level</c>（bool）：同层的那个是 int——类型不符即停那条用例的控制项。</summary>
        public bool level = true;

        /// <summary>只有根上有的成员——回落那一级用它。</summary>
        public bool rootOnly = true;

        /// <summary>根上的 float。</summary>
        public float range = 5f;

        /// <summary>根上的 Vector2。</summary>
        public Vector2 rootBox = new Vector2(-3f, 3f);

        /// <summary>反射那一级才看得到的属性。</summary>
        public bool RootProperty => flag;

        /// <summary>根上的 float 反射属性。</summary>
        public float RootFloat => range * 2f;

        /// <summary>根上的 Vector2 反射属性。</summary>
        public Vector2 RootBox => rootBox;

        /// <summary>与嵌套层同名但恒为 1 的 float 属性——「看错对象」的陷阱。</summary>
        public float Span => 1f;

        /// <summary>被解析的那个顶层节点。</summary>
        [ShowIf(nameof(flag))]
        public int rootAnchor;

        /// <summary>值对象内部有一个 bool 开关的字段——相对范围那一格用它。</summary>
        public MemberReferenceBox box = new MemberReferenceBox();

        #endregion

        #region 嵌套层

        /// <summary>嵌套实例。</summary>
        public MemberReferenceNested nested = new MemberReferenceNested();

        #endregion
    }

    /// <summary>相对范围的载体：值对象内部有一个序列化的 bool 开关。</summary>
    [Serializable]
    internal class MemberReferenceBox
    {
        /// <summary>开关（序列化，在值对象内部）。</summary>
        public bool Enabled = true;

        /// <summary>值。</summary>
        public int value = 1;

        /// <summary>
        /// 非序列化的同名形态——<see cref="MemberScope.Relative"/> 那一格用它证明**没有反射腿**：
        /// 值对象只有一个序列化句柄，没有实例可以下钻。
        /// </summary>
        public bool Reflected => Enabled;
    }

    /// <summary>
    /// 嵌套类型：同层的序列化成员与反射成员各备一份，另有两个**与根同名的陷阱**。
    /// </summary>
    [Serializable]
    internal class MemberReferenceNested
    {
        /// <summary>与根同名的 bool（假）——第 0 级该命中它。</summary>
        public bool flag;

        /// <summary>与根同名的 int（1）——根上是 bool，用来钉「类型不符即停」。</summary>
        public int level = 1;

        /// <summary>序列化的 bool 兄弟。</summary>
        public bool sibling = false;

        /// <summary>序列化的 float。</summary>
        public float span = 4f;

        /// <summary>序列化的 Vector2。</summary>
        public Vector2 box = new Vector2(0f, 9f);

        /// <summary>只读属性：反射那一级。</summary>
        public bool Enabled => sibling;

        /// <summary>无参返回 bool 的方法。</summary>
        public bool Ready()
        {
            return sibling;
        }

        /// <summary>float 反射属性。</summary>
        public float Span => span * 2f;

        /// <summary>Vector2 反射属性。</summary>
        public Vector2 Box => new Vector2(1f, 2f);

        /// <summary>无参返回 float 的方法。</summary>
        /// <returns>常量 2.5。</returns>
        public float Computed()
        {
            return 2.5f;
        }

        /// <summary>让这个嵌套类型进管线（夹具的锚点，也是所有解析的起点）。</summary>
        [ShowIf(nameof(sibling))]
        public int anchor;
    }

    /// <summary>反射树（没有序列化对象）用的普通对象。</summary>
    internal class MemberReferencePoco
    {
        /// <summary>取值来源。</summary>
        public bool flag = true;

        /// <summary>反射那一级的属性。</summary>
        public bool RootProperty => flag;

        /// <summary>被解析的节点。</summary>
        [ShowInInspector]
        [ShowIf(nameof(RootProperty))]
        public int anchor;
    }
}
