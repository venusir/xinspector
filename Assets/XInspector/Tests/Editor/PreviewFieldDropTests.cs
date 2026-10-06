using NUnit.Framework;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[PreviewField]</c> 方块落点的判定。纯函数，不碰 GUI。
    /// </summary>
    [TestFixture]
    public class PreviewFieldDropTests
    {
        /// <summary>类型相符就收。</summary>
        [Test]
        public void 类型相符就收()
        {
            var texture = new Texture2D(1, 1);
            try
            {
                Assert.That(
                    PreviewFieldDrop.TryAccept(new Object[] { texture }, typeof(Texture2D), out var accepted, out var reason),
                    Is.True,
                    reason);
                Assert.That(accepted, Is.SameAs(texture));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>载荷里取**第一个能收的**——拖来一堆时不该整批拒掉。</summary>
        [Test]
        public void 载荷取第一个可收的()
        {
            var texture = new Texture2D(1, 1);
            var material = new Material(Shader.Find("Diffuse"));
            try
            {
                Assert.That(
                    PreviewFieldDrop.TryAccept(
                        new Object[] { material, texture }, typeof(Texture2D), out var accepted, out var reason),
                    Is.True,
                    reason);
                Assert.That(accepted, Is.SameAs(texture), "材质在前但它不是 Texture2D。");
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
            }
        }

        /// <summary>类型不符：拒绝并说清实到的是什么类型。</summary>
        [Test]
        public void 类型不符被拒()
        {
            var material = new Material(Shader.Find("Diffuse"));
            try
            {
                Assert.That(
                    PreviewFieldDrop.TryAccept(
                        new Object[] { material }, typeof(Texture2D), out var accepted, out var reason),
                    Is.False);
                Assert.That(accepted, Is.Null);
                Assert.That(reason, Does.Contain("Texture2D"));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        /// <summary>空载荷拒绝（拖了但什么都没拖来）。</summary>
        [Test]
        public void 空载荷被拒()
        {
            Assert.That(PreviewFieldDrop.TryAccept(null, typeof(Texture2D), out _, out var nullReason), Is.False);
            Assert.That(nullReason, Is.Not.Null.And.Not.Empty);

            Assert.That(
                PreviewFieldDrop.TryAccept(new Object[0], typeof(Texture2D), out _, out var emptyReason),
                Is.False);
            Assert.That(emptyReason, Is.Not.Null.And.Not.Empty);
        }

        /// <summary>
        /// **场景对象不被拒**——与 <c>[AssetList]</c> 那条规则刻意不同的地方。
        /// </summary>
        /// <remarks>
        /// 那边是资产列表，只收工程资产；这里是对象字段，场景对象本来就是合法的值。
        /// 对齐那边会让「拖一个场景里的 GameObject 上去」失效，而那是正当操作。
        /// </remarks>
        [Test]
        public void 场景对象不被拒()
        {
            var go = new GameObject("drop-target");
            try
            {
                Assert.That(
                    PreviewFieldDrop.TryAccept(new Object[] { go }, typeof(GameObject), out var accepted, out var reason),
                    Is.True,
                    reason);
                Assert.That(accepted, Is.SameAs(go));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>声明类型未知（<c>null</c> 或 <c>object</c>）时，只要不是空就收。</summary>
        [Test]
        public void 声明类型未知时只要非空就收()
        {
            var go = new GameObject("drop-unknown");
            try
            {
                Assert.That(PreviewFieldDrop.TryAccept(new Object[] { go }, null, out _, out _), Is.True);
                Assert.That(PreviewFieldDrop.TryAccept(new Object[] { go }, typeof(object), out _, out _), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>已销毁的对象按空跳过——它按 Unity 的语义就不是一个可落的值。</summary>
        [Test]
        public void 已销毁的对象被跳过()
        {
            var destroyed = new GameObject("drop-destroyed");
            var alive = new Texture2D(1, 1);
            Object.DestroyImmediate(destroyed);

            try
            {
                Assert.That(
                    PreviewFieldDrop.TryAccept(
                        new Object[] { destroyed, alive }, typeof(Texture2D), out var accepted, out var reason),
                    Is.True,
                    reason);
                Assert.That(accepted, Is.SameAs(alive), "第一个是已销毁的，跳过它取下一个。");
            }
            finally
            {
                Object.DestroyImmediate(alive);
            }
        }
    }
}
