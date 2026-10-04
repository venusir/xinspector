using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 开关分组：链装配、开关解析（含「第一个孩子是分组」那条坑）、失败放行。
    /// <para>
    /// <b>测不了的</b>：复选框与标题的绘制、关掉时内容不画的效果（绘制期分支）。
    /// 解析逻辑本身抽成了 <see cref="ToggleGroupFlag"/> 的静态方法，因此这里能逐条断言——
    /// 它是本包唯一一处绘制期解析，值得钉住。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ToggleGroupDrawerTests
    {
        #region Private Fields

        private ToggleGroupFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产与序列化对象。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ToggleGroupFixture>();
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

        #region 链与档位

        /// <summary>绘制器在分组节点上，且排在末端之前。</summary>
        [Test]
        public void 绘制器在链上()
        {
            var group = FindGroup("showAdvanced");

            Assert.That(IndexOf<ToggleGroupDrawer>(group), Is.GreaterThanOrEqualTo(0));
            Assert.That(IndexOf<ToggleGroupDrawer>(group), Is.LessThan(IndexOf<ChildrenDrawer>(group)));
        }

        /// <summary>开关分组（-180）在框（-130）之外：关掉时框该一起消失。</summary>
        [Test]
        public void 开关分组在框之外()
        {
            var node = FindGroup("frameFlag");

            Assert.That(IndexOf<ToggleGroupDrawer>(node), Is.LessThan(IndexOf<BoxGroupDrawer>(node)));
        }

        #endregion

        #region 开关解析

        /// <summary>解析到同一个对象上的 bool 成员。</summary>
        [Test]
        public void 解析到开关()
        {
            var group = FindGroup("showAdvanced");
            var flag = ToggleGroupFlag.Resolve(group, "showAdvanced", out var reason);

            Assert.That(reason, Is.Null);
            Assert.That(flag, Is.Not.Null);
            Assert.That(flag.propertyType, Is.EqualTo(SerializedPropertyType.Boolean));
            Assert.That(flag.boolValue, Is.False, "夹具里它是 false。");
        }

        /// <summary>
        /// 第一个孩子是**分组节点**时照样能找到开关。
        /// <para>
        /// 分组节点没有值入口，所以要递归找第一个带值入口的后代，不能假定 <c>Children[0]</c>。
        /// </para>
        /// </summary>
        [Test]
        public void 第一个孩子是分组时仍能找到开关()
        {
            var group = FindGroup("nestedFlag");

            Assert.That(group.Children[0].Kind, Is.EqualTo(InspectorPropertyKind.Group),
                "前提：第一个孩子是分组节点（它没有值入口）。");

            var flag = ToggleGroupFlag.Resolve(group, "nestedFlag", out var reason);
            Assert.That(flag, Is.Not.Null, reason);
        }

        /// <summary>名字不存在、类型不是 bool 都返回 null 并给出原因。</summary>
        [Test]
        public void 解析失败的两种原因()
        {
            var group = FindGroup("showAdvanced");

            Assert.That(ToggleGroupFlag.Resolve(group, "missing", out var missingReason), Is.Null);
            Assert.That(missingReason, Does.Contain("找不到"));

            Assert.That(ToggleGroupFlag.Resolve(group, "number", out var typeReason), Is.Null);
            Assert.That(typeReason, Does.Contain("不是 bool"));
        }

        #endregion

        #region Private Helpers

        /// <summary>取树根；树在首次调用时构建，同一用例内复用。</summary>
        /// <returns>根节点。</returns>
        private InspectorProperty TreeRoot()
        {
            if (_tree == null)
            {
                _tree = PropertyTree.Create(_serializedObject);
            }

            return _tree.Root;
        }

        /// <summary>
        /// 按路径找**分组节点**。
        /// </summary>
        /// <param name="path">分组路径（即开关成员名）。</param>
        /// <returns>分组节点。</returns>
        /// <remarks>
        /// 必须限定 Kind：开关成员名同时是分组 ID，树里因此**有两个同名节点**
        /// （开关那个成员字段、以及以它为名的分组节点），按路径搜会撞上成员。
        /// </remarks>
        private InspectorProperty FindGroup(string path)
        {
            var found = SearchGroup(TreeRoot(), path);

            Assert.That(found, Is.Not.Null, $"找不到分组节点 {path}。");
            return found;
        }

        /// <summary>递归查找分组节点。</summary>
        /// <param name="node">当前节点。</param>
        /// <param name="path">目标路径。</param>
        /// <returns>节点；不存在返回 <c>null</c>。</returns>
        private static InspectorProperty SearchGroup(InspectorProperty node, string path)
        {
            foreach (var child in node.Children)
            {
                if (child.Kind == InspectorPropertyKind.Group && child.Path == path)
                {
                    return child;
                }

                var nested = SearchGroup(child, path);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        /// <summary>递归搜索。</summary>
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

    /// <summary>开关分组测试用的资产。</summary>
    internal sealed class ToggleGroupFixture : ScriptableObject
    {
        /// <summary>开关本身（组 ID 即它的名字）。</summary>
        public bool showAdvanced;

        /// <summary>被门控的成员。</summary>
        [ToggleGroup("showAdvanced", groupTitle: "高级选项")]
        public int debugLevel;

        /// <summary>同组第二个成员。</summary>
        [ToggleGroup("showAdvanced")]
        public int traceFlags;

        /// <summary>非 bool 的对照（解析失败用例要它）。</summary>
        public int number;

        /// <summary>另一个开关：它的组里第一个孩子是分组节点。</summary>
        public bool nestedFlag;

        /// <summary>嵌套更深的分组会先成为容器的一个孩子。</summary>
        [ToggleGroup("nestedFlag")]
        [BoxGroup("nestedFlag/框")]
        public int nestedValue;

        /// <summary>开关分组 + 框同路径：验证档位。</summary>
        public bool frameFlag;

        /// <summary>同路径的框与开关分组。</summary>
        [ToggleGroup("frameFlag")]
        [BoxGroup("frameFlag")]
        public int framed;
    }
}
