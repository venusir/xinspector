using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[AssetSelector]</c>：链装配、<c>Paths</c> 拆分、菜单项构造。
    /// <para>
    /// <b>测不了的</b>：弹出菜单本身（<c>GenericMenu</c> 是原生菜单），
    /// 以及 <c>AssetDatabase.FindAssets</c> 的结果（取决于工程里真有什么资产）。
    /// 故这里测的是「拆分与分层」那两段纯逻辑——它们决定了菜单长什么样。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AssetSelectorDrawerTests
    {
        #region Private Fields

        private AssetSelectorFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产与序列化对象。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<AssetSelectorFixture>();
            _serializedObject = new SerializedObject(_target);
        }

        /// <summary>销毁临时资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            _serializedObject?.Dispose();
            _serializedObject = null;

            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            _tree = null;
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 链装配

        /// <summary>绘制器在链上，且排在末端之前。</summary>
        [Test]
        public void 绘制器在链上()
        {
            AssertDrawer<AssetSelectorDrawer>("plain");
            AssertDrawer<AssetSelectorDrawer>("scoped");
        }

        /// <summary>无特性成员只有末端；非对象引用成员上不装它。</summary>
        [Test]
        public void 非对象引用成员没有该绘制器()
        {
            Assert.That(Find("noAttribute").Chain.Count, Is.EqualTo(1));
            Assert.That(IndexOf<AssetSelectorDrawer>(Find("aString")), Is.EqualTo(-1), "string 字段不该装 [AssetSelector]。");
        }

        #endregion

        #region Paths 拆分

        /// <summary>竖线分隔、逐个去空白、去掉结尾斜杠、丢掉空段。</summary>
        [Test]
        public void 拆分Paths()
        {
            CollectionAssert.AreEqual(
                new[] { "Assets/A", "Assets/B" },
                AssetSelectorOptions.SplitPaths("Assets/A|Assets/B"));

            CollectionAssert.AreEqual(
                new[] { "Assets/A", "Assets/B" },
                AssetSelectorOptions.SplitPaths(" Assets/A/ | Assets/B "));

            CollectionAssert.AreEqual(
                new[] { "Assets/A" },
                AssetSelectorOptions.SplitPaths("Assets/A||"), "空段丢掉。");

            CollectionAssert.AreEqual(new string[0], AssetSelectorOptions.SplitPaths(null));
            CollectionAssert.AreEqual(new string[0], AssetSelectorOptions.SplitPaths("   "));
            CollectionAssert.AreEqual(new string[0], AssetSelectorOptions.SplitPaths("|"));
        }

        #endregion

        #region 菜单项

        /// <summary>树形：去掉开头的 Assets/，其余原样交给菜单分层。</summary>
        [Test]
        public void 树形去掉Assets前缀()
        {
            var options = AssetSelectorOptions.Build(
                new[] { "Assets/Art/Materials/a.mat", "Assets/Scripts/b.cs" }, false);

            Assert.That(options.Count, Is.EqualTo(2));
            Assert.That(options[0].Path, Is.EqualTo("Art/Materials/a.mat"));
            Assert.That(options[1].Path, Is.EqualTo("Scripts/b.cs"));
            Assert.That(options[0].AssetPath, Is.EqualTo("Assets/Art/Materials/a.mat"), "资产路径要是完整的。");
        }

        /// <summary>拍平：只留文件名。</summary>
        [Test]
        public void 拍平只留文件名()
        {
            var options = AssetSelectorOptions.Build(new[] { "Assets/Art/Materials/a.mat" }, true);

            Assert.That(options[0].Path, Is.EqualTo("a.mat"));
            Assert.That(options[0].AssetPath, Is.EqualTo("Assets/Art/Materials/a.mat"));
        }

        /// <summary>按菜单路径排序（序数比较），资产路径跟着走。</summary>
        [Test]
        public void 按菜单路径排序()
        {
            var options = AssetSelectorOptions.Build(
                new[] { "Assets/z.mat", "Assets/a.mat", "Assets/m.mat" }, false);

            Assert.That(options.Select(option => option.Path), Is.EqualTo(new[] { "a.mat", "m.mat", "z.mat" }));
            Assert.That(options[0].AssetPath, Is.EqualTo("Assets/a.mat"));
        }

        /// <summary>开头的 Assets 前缀大小写不敏感；不带前缀的路径原样保留。</summary>
        [Test]
        public void 前缀处理()
        {
            Assert.That(AssetSelectorOptions.Build(new[] { "assets/a.mat" }, false)[0].Path, Is.EqualTo("a.mat"));
            Assert.That(AssetSelectorOptions.Build(new[] { "Packages/x/a.mat" }, false)[0].Path,
                Is.EqualTo("Packages/x/a.mat"), "不是 Assets/ 开头的原样保留。");
        }

        /// <summary>空输入与空段不产生菜单项。</summary>
        [Test]
        public void 空输入不产生项()
        {
            Assert.That(AssetSelectorOptions.Build(null, false).Count, Is.EqualTo(0));
            Assert.That(AssetSelectorOptions.Build(new string[0], false).Count, Is.EqualTo(0));
            Assert.That(AssetSelectorOptions.Build(new[] { "", null }, false).Count, Is.EqualTo(0));
        }

        #endregion

        #region Private Helpers

        /// <summary>按路径取成员节点；树在首次调用时构建。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>成员节点。</returns>
        private InspectorProperty Find(string path)
        {
            if (_tree == null)
            {
                _tree = PropertyTree.Create(new SerializedObject(_target));
            }

            foreach (var child in _tree.Root.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            Assert.Fail($"找不到成员 {path}。");
            return null;
        }

        /// <summary>断言某成员链上有指定绘制器，且它排在末端之前。</summary>
        /// <typeparam name="T">绘制器类型。</typeparam>
        /// <param name="path">成员路径。</param>
        private void AssertDrawer<T>(string path) where T : XInspectorDrawer
        {
            var property = Find(path);
            var index = IndexOf<T>(property);
            var terminal = IndexOf<UnityFallbackDrawer>(property);

            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"{path} 上应有 {typeof(T).Name}。");
            Assert.That(index, Is.LessThan(terminal), $"{path} 上 {typeof(T).Name} 应排在末端之前。");
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

        /// <summary><c>[AssetSelector]</c> 的测试宿主。</summary>
        private class AssetSelectorFixture : ScriptableObject
        {
            /// <summary>无特性的对照。</summary>
            public Object noAttribute;

            /// <summary>非对象引用的对照。</summary>
            public string aString;

            /// <summary>无参形态。</summary>
            [AssetSelector]
            public Material plain;

            /// <summary>带目录与过滤串。</summary>
            [AssetSelector(Paths = "Assets/Art|Assets/Shared", Filter = "t:Material", FlattenTreeView = true)]
            public Material scoped;
        }
    }
}
