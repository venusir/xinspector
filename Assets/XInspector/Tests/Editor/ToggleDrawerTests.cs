using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[Toggle]</c>：链装配、解析、门控求值、失败放行。
    /// <para>
    /// <b>测不了的</b>：开关的真实绘制与拖动（本仓策略不测 IMGUI）。
    /// 「开关永远可点」那条纪律因此只有注释与目视保证。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ToggleDrawerTests
    {
        #region Private Fields

        private ToggleFixture _target;
        private SerializedObject _serializedObject;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产与序列化对象。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ToggleFixture>();
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

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 链与解析

        /// <summary>开关绘制器在链上，且排在末端之前。</summary>
        [Test]
        public void 开关绘制器在链上()
        {
            var property = Find("settings");

            Assert.That(IndexOf<ToggleDrawer>(property), Is.GreaterThanOrEqualTo(0));
            Assert.That(IndexOf<ToggleDrawer>(property), Is.LessThan(IndexOf<UnityFallbackDrawer>(property)));
        }

        /// <summary>
        /// 门控每帧求值：开关的真假直接决定只读状态，不需要重建树。
        /// <para>
        /// 解析发生在构建期（<see cref="ToggleProcessor"/>），故这里改的是
        /// <b>同一份</b>序列化对象上的值——不必再调 <c>Update()</c>。
        /// </para>
        /// </summary>
        [Test]
        public void 开关门控只读状态()
        {
            var property = Find("settings");
            var toggle = _serializedObject.FindProperty("settings.Enabled");

            toggle.boolValue = true;
            Assert.That(property.State.IsReadOnly, Is.False, "开关打开时可编辑。");

            toggle.boolValue = false;
            Assert.That(property.State.IsReadOnly, Is.True, "开关关掉时只读。");

            toggle.boolValue = true;
            Assert.That(property.State.IsReadOnly, Is.False, "再打开又可变——门控不是单向闸门。");
        }

        /// <summary>
        /// 解析失败：告警一次、保持可编辑、字段照常绘制。
        /// <para>「失败即放行」与条件族同一条规矩——拼错名字不该让字段变得不可用。</para>
        /// </summary>
        [Test]
        public void 解析失败时保持可编辑并告警()
        {
            var broken = ScriptableObject.CreateInstance<BrokenToggleFixture>();

            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("\\[Toggle\\].*无法解析"));

                var tree = PropertyTree.Create(new SerializedObject(broken));
                var property = ChildOf(tree.Root, "settings");

                Assert.That(property, Is.Not.Null, "解析失败不影响建节点。");
                Assert.That(property.State.IsReadOnly, Is.False, "解析失败不能用「恒只读」冒充门控。");
                Assert.That(IndexOf<ToggleDrawer>(property), Is.GreaterThanOrEqualTo(0), "绘制器仍在链上，只是不画开关。");
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        /// <summary>
        /// 与 <c>[ReadOnly]</c> 并存时后者赢：恒只读（100）排在门控（50）之后。
        /// </summary>
        [Test]
        public void 恒只读排在门控之后()
        {
            var property = Find("lockedSettings");

            _serializedObject.FindProperty("lockedSettings.Enabled").boolValue = true;

            Assert.That(property.State.IsReadOnly, Is.True,
                "开关打开也仍只读——[ReadOnly] 的优先级更高，这条顺序写进了 [Toggle] 的文档。");
        }

        #endregion

        #region Private Helpers

        /// <summary>按路径取成员节点。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>成员节点。</returns>
        private InspectorProperty Find(string path)
        {
            var found = ChildOf(PropertyTree.Create(_serializedObject).Root, path);

            Assert.That(found, Is.Not.Null, $"找不到成员 {path}。");
            return found;
        }

        /// <summary>在直接子节点里按路径查找。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">路径。</param>
        /// <returns>节点；不存在返回 <c>null</c>。</returns>
        private static InspectorProperty ChildOf(InspectorProperty parent, string path)
        {
            foreach (var child in parent.Children)
            {
                if (child.Path == path)
                {
                    return child;
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

    /// <summary>可序列化的设置块（开关在值对象内部）。</summary>
    [System.Serializable]
    internal struct ToggleSettings
    {
        /// <summary>开关。</summary>
        public bool Enabled;

        /// <summary>被门控的值。</summary>
        public int Value;
    }

    /// <summary>[Toggle] 测试用的资产。</summary>
    internal sealed class ToggleFixture : ScriptableObject
    {
        /// <summary>普通门控。</summary>
        [Toggle("Enabled")]
        public ToggleSettings settings;

        /// <summary>恒只读与门控并存：前者赢。</summary>
        [ReadOnly]
        [Toggle("Enabled")]
        public ToggleSettings lockedSettings;
    }

    /// <summary>开关名解析失败的对照资产。</summary>
    internal sealed class BrokenToggleFixture : ScriptableObject
    {
        /// <summary>指向不存在的成员。</summary>
        [Toggle("Missing")]
        public ToggleSettings settings;
    }
}
