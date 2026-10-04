using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 没有 <c>SerializedObject</c> 的树：目标可以是任意对象，树只收带标记的成员。
    /// <para>
    /// 这是 L3 里唯一一处**会让已有字段的类型变宽**的改动（目标列表从 <c>Object[]</c>
    /// 变成 <c>object[]</c>），故边界要钉得比别处紧。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PropertyTreeReflectedTests
    {
        #region Teardown

        /// <summary>复位静态注册表。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 建树

        /// <summary>POCO 也能建树，且树里没有序列化对象——这正是「任意对象」的含义。</summary>
        [Test]
        public void 反射树没有序列化对象()
        {
            using (var tree = PropertyTree.CreateReflected(new ReflectedPoco()))
            {
                Assert.That(tree.SerializedObject, Is.Null);
                Assert.That(tree.Root, Is.Not.Null);
                Assert.That(tree.Root.ValueEntry, Is.Null, "根节点任何情况下都没有值入口。");
            }
        }

        /// <summary>目标列表只有一个元素，且就是传进去的那个实例。</summary>
        [Test]
        public void 目标列表只含那一个对象()
        {
            var poco = new ReflectedPoco();

            using (var tree = PropertyTree.CreateReflected(poco))
            {
                Assert.That(tree.Targets.Length, Is.EqualTo(1));
                Assert.That(tree.Targets[0], Is.SameAs(poco));
            }
        }

        /// <summary>带标记的字段与属性都进树。</summary>
        [Test]
        public void 带标记的成员进树()
        {
            using (var tree = PropertyTree.CreateReflected(new ReflectedPoco()))
            {
                Assert.That(ReflectedMemberTests.Find(tree.Root, "Marked"), Is.Not.Null);
                Assert.That(ReflectedMemberTests.Find(tree.Root, "Name"), Is.Not.Null);
            }
        }

        /// <summary>
        /// 没标记的公开成员**一个都不收**。
        /// <para>
        /// 这是本包对 POCO 目标的立场：自动收一批 public 成员会制造出
        /// 「Inspector 里靠序列化、窗口里靠可见性」两套语义，而 Odin 那套自动收建立在他的
        /// Serializer 类型系统上，本包没有。想让它出现，就标上。
        /// </para>
        /// </summary>
        [Test]
        public void 未标记的公开成员不进树()
        {
            using (var tree = PropertyTree.CreateReflected(new ReflectedPoco()))
            {
                Assert.That(ReflectedMemberTests.Find(tree.Root, "Unmarked"), Is.Null);
                Assert.That(tree.Root.Children.Count, Is.EqualTo(2), "树里应当只有那两个带标记的成员。");
            }
        }

        /// <summary>没有带标记成员的 POCO 建出来的是一棵空树——不抛异常。</summary>
        [Test]
        public void 没有标记成员时是空树()
        {
            Assert.DoesNotThrow(() =>
            {
                using (var tree = PropertyTree.CreateReflected(new EmptyPoco()))
                {
                    Assert.That(tree.Root.Children.Count, Is.EqualTo(0));
                }
            });
        }

        /// <summary>Unity 对象也能走反射通道——窗口的 <c>GetTarget()</c> 返回非窗口对象时是另一条路，但这里要能各自工作。</summary>
        [Test]
        public void Unity对象也可以走反射通道()
        {
            var asset = ScriptableObject.CreateInstance<ReflectedPocoAsset>();

            try
            {
                using (var tree = PropertyTree.CreateReflected(asset))
                {
                    Assert.That(tree.SerializedObject, Is.Null, "反射通道不建序列化对象。");
                    Assert.That(ReflectedMemberTests.Find(tree.Root, "Marked"), Is.Not.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        #endregion

        #region 参数防御

        /// <summary>传 <c>null</c> 必须报错而不是给出一棵空树。</summary>
        [Test]
        public void Null目标抛异常()
        {
            Assert.That(() => PropertyTree.CreateReflected(null), Throws.ArgumentNullException);
        }

        /// <summary>已销毁的 Unity 对象等同于 <c>null</c>——形参是 <c>object</c>，裸判空挡不住它。</summary>
        [Test]
        public void 已销毁的目标抛异常()
        {
            var asset = ScriptableObject.CreateInstance<ReflectedPocoAsset>();
            var destroyed = asset;
            Object.DestroyImmediate(asset);

            Assert.That(() => PropertyTree.CreateReflected(destroyed), Throws.ArgumentNullException);
        }

        #endregion

        #region 与既有不变量

        /// <summary>
        /// 反射成员**不进重置范围**。
        /// <para>
        /// 重置是按序列化路径逐条复制值的，而反射成员根本没有序列化路径。
        /// 它天然被排除——因为它是独立的一种 <see cref="InspectorPropertyKind"/>，
        /// 而收集只认 <see cref="InspectorPropertyKind.Member"/>。这条守的就是那个 Kind 的意义：
        /// 混用 <c>Member</c> 的话，这里会多出一批永远复制失败的路径。
        /// </para>
        /// </summary>
        [Test]
        public void 反射成员不进重置范围()
        {
            using (var tree = PropertyTree.CreateReflected(new ReflectedPoco()))
            {
                Assert.That(PropertyTreeReset.CollectMemberPaths(tree), Is.Empty);
            }
        }

        /// <summary>
        /// 控制项：序列化成员**在**重置范围里。
        /// <para>
        /// 没有这一条，上面那条无法区分「反射成员被正确排除」与「收集根本什么都没收」。
        /// </para>
        /// </summary>
        [Test]
        public void 序列化成员仍在重置范围里()
        {
            var asset = ScriptableObject.CreateInstance<HostFixture>();

            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(asset)))
                {
                    Assert.That(PropertyTreeReset.CollectMemberPaths(tree), Does.Contain("number"));
                }
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        #endregion
    }

    /// <summary>反射树测试用的普通对象——刻意不是 Unity 对象。</summary>
    internal sealed class ReflectedPoco
    {
        /// <summary>没有标记的公开字段，不该进树。</summary>
        public int Unmarked = 1;

        /// <summary>带标记的字段。</summary>
        [ShowInInspector]
        public int Marked = 2;

        /// <summary>带标记的属性。</summary>
        [ShowInInspector]
        public string Name => "样本";
    }

    /// <summary>一个标记都没带的普通对象。</summary>
    internal sealed class EmptyPoco
    {
        /// <summary>普通字段。</summary>
        public int value = 1;
    }

    /// <summary>Unity 对象版本的同一个夹具——用来验证反射通道不挑目标的类型。</summary>
    internal sealed class ReflectedPocoAsset : ScriptableObject
    {
        /// <summary>带标记的字段。</summary>
        [ShowInInspector]
        public int Marked = 2;
    }
}
