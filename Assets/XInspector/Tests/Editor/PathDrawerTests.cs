using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 路径两特性：链装配、类型守卫，以及全部路径数学。
    /// <para>
    /// <b>测不了的</b>：系统文件面板本身、文本框与按钮的渲染（本仓策略不测 IMGUI）。
    /// 所以判定全部挤在纯函数 <see cref="PathEditing"/> 里，这里测的就是它——
    /// 绘制器只剩「画、把面板返回值写回」两件事。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PathDrawerTests
    {
        #region Private Fields

        /// <summary>测试用工程根（假根，不碰真磁盘）。</summary>
        private const string Root = "E:/Proj";

        private PathFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产与序列化对象。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<PathFixture>();
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

        /// <summary>两个绘制器各就各位，且都排在末端之前。</summary>
        [Test]
        public void 绘制器都在链上()
        {
            AssertDrawer<FilePathDrawer>("plainFile");
            AssertDrawer<FilePathDrawer>("suffixed");
            AssertDrawer<FolderPathDrawer>("plainFolder");
        }

        /// <summary>无特性的对照只有末端；非字符串成员上不装这两个绘制器。</summary>
        [Test]
        public void 非字符串成员退回末端()
        {
            Assert.That(Find("plain").Chain.Count, Is.EqualTo(1), "无特性成员。");
            Assert.That(IndexOf<FilePathDrawer>(Find("aNumber")), Is.EqualTo(-1), "int 字段不该装 [FilePath]。");
        }

        #endregion

        #region 绝对路径判定

        /// <summary>Windows 上认盘符与 UNC，不把 Unity 风格的「/开头」当成绝对路径。</summary>
        [Test]
        public void 绝对路径判定()
        {
            Assert.That(PathEditing.IsAbsolute("E:/Proj/Assets/a.txt"), Is.True, "盘符正斜杠。");
            Assert.That(PathEditing.IsAbsolute(@"E:\Proj\Assets\a.txt"), Is.True, "盘符反斜杠。");
            Assert.That(PathEditing.IsAbsolute(@"\\server\share\a.txt"), Is.True, "UNC。");
            Assert.That(PathEditing.IsAbsolute("Assets/a.txt"), Is.False);
            Assert.That(PathEditing.IsAbsolute("/Assets/a.txt"), Is.False, "Unity 风格：以 / 开头仍是工程相对。");
            Assert.That(PathEditing.IsAbsolute(""), Is.False);
            Assert.That(PathEditing.IsAbsolute(null), Is.False);
        }

        #endregion

        #region 分隔符归一化

        /// <summary>归一化只换分隔符，不碰别的字符。</summary>
        [Test]
        public void 分隔符归一化()
        {
            Assert.That(PathEditing.Normalize(@"a\b\c", false), Is.EqualTo("a/b/c"));
            Assert.That(PathEditing.Normalize("a/b/c", true), Is.EqualTo(@"a\b\c"));
            Assert.That(PathEditing.Normalize("a/b", false), Is.EqualTo("a/b"), "已是目标方向时原样。");
            Assert.That(PathEditing.Normalize("", false), Is.EqualTo(""));
        }

        #endregion

        #region 扩展名

        /// <summary>逗号分隔、点可选、去空白、大小写不敏感。</summary>
        [Test]
        public void 扩展名解析()
        {
            CollectionAssert.AreEqual(new[] { "cs", "unity" }, PathEditing.ParseExtensions("cs, unity"));
            CollectionAssert.AreEqual(new[] { "cs", "unity" }, PathEditing.ParseExtensions(".cs,.unity"));
            CollectionAssert.AreEqual(new[] { "cs" }, PathEditing.ParseExtensions(" CS "));
            CollectionAssert.AreEqual(new string[0], PathEditing.ParseExtensions(null));
            CollectionAssert.AreEqual(new string[0], PathEditing.ParseExtensions("   "));
            CollectionAssert.AreEqual(new string[0], PathEditing.ParseExtensions(",, . ,"));
        }

        /// <summary>白名单为空表示不限制；有白名单时按扩展名比对。</summary>
        [Test]
        public void 扩展名白名单判定()
        {
            var allowed = PathEditing.ParseExtensions("cs, unity");

            Assert.That(PathEditing.IsExtensionAllowed("a/b.cs", allowed), Is.True);
            Assert.That(PathEditing.IsExtensionAllowed("a/b.CS", allowed), Is.True, "大小写不敏感。");
            Assert.That(PathEditing.IsExtensionAllowed("a/b.txt", allowed), Is.False);
            Assert.That(PathEditing.IsExtensionAllowed("a/b", allowed), Is.False, "没有扩展名。");
            Assert.That(PathEditing.IsExtensionAllowed("a/b.txt", null), Is.True, "不限制。");
            Assert.That(PathEditing.IsExtensionAllowed("a/b.txt", new string[0]), Is.True, "不限制。");
        }

        #endregion

        #region 拼接与基准目录

        /// <summary>右段为绝对路径时原样返回；否则去重首尾分隔符再拼。</summary>
        [Test]
        public void 路径拼接()
        {
            Assert.That(PathEditing.Combine("E:/Proj", "Assets/a.txt"), Is.EqualTo("E:/Proj/Assets/a.txt"));
            Assert.That(PathEditing.Combine("E:/Proj/", "/Assets/a.txt"), Is.EqualTo("E:/Proj/Assets/a.txt"));
            Assert.That(PathEditing.Combine("E:/Proj", "F:/other/a.txt"), Is.EqualTo("F:/other/a.txt"));
            Assert.That(PathEditing.Combine("", "Assets"), Is.EqualTo("Assets"));
            Assert.That(PathEditing.Combine("E:/Proj", ""), Is.EqualTo("E:/Proj"));
        }

        /// <summary><c>ParentFolder</c> 为空时基准就是工程根；本身是绝对路径时以它为准。</summary>
        [Test]
        public void 基准目录()
        {
            Assert.That(PathEditing.BaseDirectory(Root, null), Is.EqualTo(Root));
            Assert.That(PathEditing.BaseDirectory(Root, "   "), Is.EqualTo(Root));
            Assert.That(PathEditing.BaseDirectory(Root, "Assets/Resources"), Is.EqualTo("E:/Proj/Assets/Resources"));
            Assert.That(PathEditing.BaseDirectory(Root, "F:/Ext"), Is.EqualTo("F:/Ext"), "绝对路径的 ParentFolder 直接生效。");
        }

        #endregion

        #region 存储形态 ↔ 绝对路径

        /// <summary>默认（工程相对）：存储形态是相对工程根的路径。</summary>
        [Test]
        public void 换算_工程相对()
        {
            var stored = PathEditing.ToStored("E:/Proj/Assets/Plugins/a.cs", Root, null, false, false);
            Assert.That(stored, Is.EqualTo("Assets/Plugins/a.cs"));

            var absolute = PathEditing.ToAbsolute("Assets/Plugins/a.cs", Root, null, false);
            Assert.That(absolute, Is.EqualTo("E:/Proj/Assets/Plugins/a.cs"));
        }

        /// <summary><c>ParentFolder</c> 之下的路径存成相对**它**的路径。</summary>
        [Test]
        public void 换算_相对父目录()
        {
            var stored = PathEditing.ToStored("E:/Proj/Assets/Resources/data.json", Root, "Assets/Resources", false, false);
            Assert.That(stored, Is.EqualTo("data.json"));

            var absolute = PathEditing.ToAbsolute("data.json", Root, "Assets/Resources", false);
            Assert.That(absolute, Is.EqualTo("E:/Proj/Assets/Resources/data.json"));
        }

        /// <summary><c>AbsolutePath</c> 时两个方向都原样保留绝对路径。</summary>
        [Test]
        public void 换算_绝对路径()
        {
            Assert.That(
                PathEditing.ToStored("E:/Proj/Assets/a.cs", Root, null, true, false),
                Is.EqualTo("E:/Proj/Assets/a.cs"));
            Assert.That(
                PathEditing.ToAbsolute("E:/Proj/Assets/a.cs", Root, null, true),
                Is.EqualTo("E:/Proj/Assets/a.cs"));
        }

        /// <summary>选到基准目录之外时存绝对路径——不悄悄把位置改成别的。</summary>
        [Test]
        public void 换算_基准目录之外存绝对路径()
        {
            var stored = PathEditing.ToStored("F:/Elsewhere/a.cs", Root, null, false, false);
            Assert.That(stored, Is.EqualTo("F:/Elsewhere/a.cs"));
        }

        /// <summary>反斜杠开关作用于写入结果。</summary>
        [Test]
        public void 换算_反斜杠()
        {
            Assert.That(
                PathEditing.ToStored("E:/Proj/Assets/a.cs", Root, null, false, true),
                Is.EqualTo(@"Assets\a.cs"));
        }

        /// <summary>空值双向都是空串，不产生「/」这种半截路径。</summary>
        [Test]
        public void 换算_空值()
        {
            Assert.That(PathEditing.ToAbsolute("", Root, null, false), Is.EqualTo(""));
            Assert.That(PathEditing.ToStored("", Root, null, false, false), Is.EqualTo(""));
        }

        #endregion

        #region 目录归属

        /// <summary>段级比较：前缀相同但不是一个目录的不算「之下」。</summary>
        [Test]
        public void 目录归属()
        {
            Assert.That(PathEditing.IsUnder("E:/Proj/Assets/a.cs", "E:/Proj"), Is.True);
            Assert.That(PathEditing.IsUnder("E:/Proj/AssetsExtra/a.cs", "E:/Proj/Assets"), Is.False, "AssetsExtra 不是 Assets 之下。");
            Assert.That(PathEditing.IsUnder("E:/Proj", "E:/Proj"), Is.False, "自身不算之下。");
            Assert.That(PathEditing.IsUnder("E:/PROJ/Assets/a.cs", "E:/Proj"), Is.True, "大小写不敏感。");
        }

        /// <summary>
        /// 面板起始目录：文件取其**所在目录**，目录取它自己；工程内给工程相对路径，
        /// 工程外给绝对路径，无起点给空串。
        /// </summary>
        [Test]
        public void 面板起始目录()
        {
            Assert.That(PathEditing.ToPanelDirectory("E:/Proj/Assets/a.cs", false, Root), Is.EqualTo("Assets"),
                "文件要给所在目录，不是路径本身。");
            Assert.That(PathEditing.ToPanelDirectory("E:/Proj/Assets/Resources", true, Root), Is.EqualTo("Assets/Resources"),
                "目录给自己。");
            Assert.That(PathEditing.ToPanelDirectory("F:/Elsewhere/a.cs", false, Root), Is.EqualTo("F:/Elsewhere"),
                "工程外给绝对路径。");
            Assert.That(PathEditing.ToPanelDirectory("E:/Proj/Assets", true, Root), Is.EqualTo("Assets"));
            Assert.That(PathEditing.ToPanelDirectory("E:/Proj", true, Root), Is.EqualTo(""), "工程根本身 → 空串（面板按工程根开）。");
            Assert.That(PathEditing.ToPanelDirectory("", false, Root), Is.EqualTo(""));
            Assert.That(PathEditing.ToPanelDirectory(null, true, Root), Is.EqualTo(""));
            Assert.That(PathEditing.ToPanelDirectory("a.cs", false, Root), Is.EqualTo(""),
                "没有目录段 → 无起点（面板开在系统默认位置）。");
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

        /// <summary>路径两特性的测试宿主。</summary>
        private class PathFixture : ScriptableObject
        {
            /// <summary>无特性的对照。</summary>
            public string plain;

            /// <summary>非字符串的对照。</summary>
            public int aNumber;

            /// <summary>默认 <c>[FilePath]</c>。</summary>
            [FilePath]
            public string plainFile;

            /// <summary>带具名参数的 <c>[FilePath]</c>。</summary>
            [FilePath(ParentFolder = "Assets/Resources", Extensions = "cs, unity", RequireExistingPath = true)]
            public string suffixed;

            /// <summary>默认 <c>[FolderPath]</c>。</summary>
            [FolderPath]
            public string plainFolder;
        }
    }
}
