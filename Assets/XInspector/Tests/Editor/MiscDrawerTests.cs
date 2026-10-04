using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 结构与门控五特性：链装配、只读覆盖、子物体判定、脚本槽位抑制。
    /// <para>
    /// <b>测不了的</b>：信息框外观、<c>[DrawWithUnity]</c> 画出来的控件长什么样（本仓策略不测 IMGUI）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class MiscDrawerTests
    {
        #region Private Fields

        private MiscFixture _target;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<MiscFixture>();
        }

        /// <summary>销毁临时资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
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

        #region 链装配与状态

        /// <summary>类级 [TypeInfoBox] 落在根节点链上（与类级 [Title] 同一机制）。</summary>
        [Test]
        public void 类级信息框落在根节点()
        {
            var root = TreeRoot();

            Assert.That(IndexOf<TypeInfoBoxDrawer>(root), Is.GreaterThanOrEqualTo(0));
        }

        /// <summary>
        /// [DrawWithUnity] 几乎最外（SuperPriority），且与 [Indent] 并存时排在最前面。
        /// <para>
        /// 它不调用下一个绘制器，故 [Indent] 不会运行——「交给 Unity」就是这个含义。
        /// </para>
        /// </summary>
        [Test]
        public void 直通绘制器在最外层()
        {
            var property = Find("unityDrawn");
            var drawWithUnity = IndexOf<DrawWithUnityDrawer>(property);
            var indent = IndexOf<IndentDrawer>(property);
            var terminal = IndexOf<UnityFallbackDrawer>(property);

            Assert.That(drawWithUnity, Is.EqualTo(0), "SuperPriority 应当排第 0 格。");
            Assert.That(indent, Is.GreaterThan(drawWithUnity));
            Assert.That(terminal, Is.GreaterThan(drawWithUnity));
        }

        /// <summary>[ChildGameObjectsOnly] 在链上。</summary>
        [Test]
        public void 子物体校验在链上()
        {
            var property = Find("childOnly");

            Assert.That(IndexOf<ChildGameObjectsOnlyDrawer>(property), Is.GreaterThanOrEqualTo(0));
            Assert.That(IndexOf<ChildGameObjectsOnlyDrawer>(property), Is.LessThan(IndexOf<UnityFallbackDrawer>(property)));
        }

        /// <summary>
        /// [EnableGUI] 覆盖 [ReadOnly]：并存时可编辑，单独 [ReadOnly] 时只读。
        /// <para>处理器优先级只在构建期生效，这里断言的是**构建之后的状态**。</para>
        /// </summary>
        [Test]
        public void 强制可编辑覆盖只读()
        {
            Assert.That(Find("readOnly").State.IsReadOnly, Is.True);
            Assert.That(Find("forcedEditable").State.IsReadOnly, Is.False, "[EnableGUI] 排在只读之后，它赢。");
        }

        #endregion

        #region 脚本槽位抑制

        /// <summary>
        /// 类级 [HideMonoScript] 把脚本槽位直接不建节点；不带该特性时照旧保留。
        /// <para>
        /// 保留是 Inspector 路径的默认行为（与原生渲染一致），本特性是它的显式退出。
        /// </para>
        /// </summary>
        [Test]
        public void 隐藏脚本槽位()
        {
            var hiddenTarget = ScriptableObject.CreateInstance<HideScriptFixture>();
            try
            {
                using (var hiddenObject = new SerializedObject(hiddenTarget))
                using (var keptObject = new SerializedObject(_target))
                {
                    var hidden = PropertyTree.Create(hiddenObject);
                    var kept = PropertyTree.Create(keptObject);

                    Assert.That(FindPath(hidden.Root, "m_Script"), Is.Null, "带 [HideMonoScript] 时不应有脚本槽位。");
                    Assert.That(FindPath(kept.Root, "m_Script"), Is.Not.Null, "不带时照旧保留。");
                }
            }
            finally
            {
                Object.DestroyImmediate(hiddenTarget);
            }
        }

        #endregion

        #region 子物体判定

        /// <summary>
        /// 子物体/非子物体/非激活子物体/自身四个分支。
        /// <para>
        /// 判定要用到被检视对象的 Transform，故夹具是真实的 MonoBehaviour 而非 ScriptableObject。
        /// </para>
        /// </summary>
        [Test]
        public void 子物体判定的四个分支()
        {
            var root = new GameObject("MiscTestRoot");
            var child = new GameObject("Child");
            var inactiveChild = new GameObject("InactiveChild");
            var stranger = new GameObject("Stranger");

            try
            {
                child.transform.SetParent(root.transform);
                inactiveChild.transform.SetParent(root.transform);
                inactiveChild.SetActive(false);

                var behaviour = root.AddComponent<ChildOnlyBehaviour>();
                using (var serializedObject = new SerializedObject(behaviour))
                {
                    var property = serializedObject.FindProperty("target");

                    property.objectReferenceValue = null;
                    Assert.That(ChildObjectValidator.TryDescribeViolation(property, false, false, out _), Is.False,
                        "空引用不算违反——那是 [Required] 的职责。");

                    property.objectReferenceValue = child;
                    Assert.That(ChildObjectValidator.TryDescribeViolation(property, false, false, out _), Is.False);

                    property.objectReferenceValue = stranger;
                    Assert.That(ChildObjectValidator.TryDescribeViolation(property, false, false, out var strangerMessage), Is.True);
                    Assert.That(strangerMessage, Does.Contain("Stranger"));

                    property.objectReferenceValue = inactiveChild;
                    Assert.That(ChildObjectValidator.TryDescribeViolation(property, false, false, out _), Is.True,
                        "非激活子物体默认不允许。");
                    Assert.That(ChildObjectValidator.TryDescribeViolation(property, true, false, out _), Is.False,
                        "IncludeInactive 后允许。");

                    property.objectReferenceValue = root;
                    Assert.That(ChildObjectValidator.TryDescribeViolation(property, false, false, out _), Is.True,
                        "引用所在物体自己默认不允许。");
                    Assert.That(ChildObjectValidator.TryDescribeViolation(property, false, true, out _), Is.False,
                        "IncludeSelf 后允许。");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(stranger);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>取树根；树在首次调用时构建，同一用例内复用。</summary>
        /// <returns>根节点。</returns>
        private InspectorProperty TreeRoot()
        {
            if (_tree == null)
            {
                _tree = PropertyTree.Create(new SerializedObject(_target));
            }

            return _tree.Root;
        }

        /// <summary>按路径取成员节点。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>成员节点。</returns>
        private InspectorProperty Find(string path)
        {
            var found = FindPath(TreeRoot(), path);

            Assert.That(found, Is.Not.Null, $"找不到成员 {path}。");
            return found;
        }

        /// <summary>在直接子节点里按路径查找。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">路径。</param>
        /// <returns>节点；不存在返回 <c>null</c>。</returns>
        private static InspectorProperty FindPath(InspectorProperty parent, string path)
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

    /// <summary>结构与门控测试用的资产。</summary>
    [TypeInfoBox("这条信息框来自类级 [TypeInfoBox]。")]
    internal sealed class MiscFixture : ScriptableObject
    {
        /// <summary>恒只读。</summary>
        [ReadOnly]
        public int readOnly = 1;

        /// <summary>只读 + 强制可编辑，后者赢。</summary>
        [ReadOnly]
        [EnableGUI]
        public int forcedEditable = 2;

        /// <summary>直通绘制器与修饰叠加：修饰不会运行。</summary>
        [DrawWithUnity]
        [Indent]
        public int unityDrawn = 3;

        /// <summary>子物体限定。</summary>
        [ChildGameObjectsOnly]
        public GameObject childOnly;
    }

    /// <summary>带 [HideMonoScript] 的对照资产。</summary>
    [HideMonoScript]
    internal sealed class HideScriptFixture : ScriptableObject
    {
        /// <summary>随便一个字段，保证树非空。</summary>
        public int value;
    }

    /// <summary>子物体判定的宿主组件。</summary>
    internal sealed class ChildOnlyBehaviour : MonoBehaviour
    {
        /// <summary>被校验的引用。</summary>
        [ChildGameObjectsOnly]
        public GameObject target;
    }
}
