using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 条件族：处理器装的是**每帧求值的解析器**，而不是一次性读到的定值。
    /// <para>
    /// 这些用例全部不碰 GUI——「要不要显示 / 能不能改」是纯状态判断，
    /// 这正是本仓把决策挪出绘制器所要换来的东西。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ConditionProcessorTests
    {
        #region Private Fields

        private ConditionFixture _target;

        #endregion

        #region Setup / Teardown

        /// <summary>建立资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ConditionFixture>();

            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        /// <summary>销毁资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            AttributeProcessorRegistry.Reset();
            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region ShowIf / HideIf

        /// <summary>
        /// 条件为真时可见、为假时隐藏，且**跟随条件的当前值**——不需要重建树。
        /// </summary>
        [Test]
        public void ShowIf_FollowsConditionValue()
        {
            var tree = BuildTree();
            var node = Find(tree, "shown");

            Assert.That(node, Is.Not.Null);

            SetFlag(tree, true);
            Assert.That(node.IsVisible, Is.True, "条件为真应当显示。");

            SetFlag(tree, false);
            Assert.That(node.IsVisible, Is.False, "条件变为假后应当隐藏——解析器是每帧求值的，不需要重建树。");
        }

        /// <summary>
        /// <c>[HideIf]</c> 是同一套机制取反。
        /// </summary>
        [Test]
        public void HideIf_InvertsCondition()
        {
            var tree = BuildTree();
            var node = Find(tree, "hidden");

            SetFlag(tree, true);
            Assert.That(node.IsVisible, Is.False, "条件为真应当隐藏。");

            SetFlag(tree, false);
            Assert.That(node.IsVisible, Is.True);
        }

        #endregion

        #region EnableIf / DisableIf

        /// <summary>
        /// <c>[EnableIf]</c>：条件为真时可编辑，为假时只读。
        /// </summary>
        [Test]
        public void EnableIf_InstallsReadOnlyResolver()
        {
            var tree = BuildTree();
            var node = Find(tree, "enabled");

            SetFlag(tree, true);
            Assert.That(node.State.IsReadOnly, Is.False, "条件为真应该可编辑。");

            SetFlag(tree, false);
            Assert.That(node.State.IsReadOnly, Is.True, "条件为假应该只读。");
        }

        /// <summary>
        /// <c>[DisableIf]</c>：条件为真时只读。
        /// </summary>
        [Test]
        public void DisableIf_ReadsOnlyWhenConditionTrue()
        {
            var tree = BuildTree();
            var node = Find(tree, "disabled");

            SetFlag(tree, true);
            Assert.That(node.State.IsReadOnly, Is.True, "条件为真应当只读。");

            SetFlag(tree, false);
            Assert.That(node.State.IsReadOnly, Is.False);
        }

        /// <summary>
        /// 没装条件的属性默认可编辑。
        /// <para>
        /// <c>IsReadOnly</c> 从「存下来的 bool」改成「算出来的」之后，默认值写错会让
        /// **所有普通字段**变灰——这条守的就是那个默认值。
        /// </para>
        /// <para>
        /// 断言的是**行为**而不是「解析器是否为 null」：两个解析器都有非 null 的哨兵默认值
        /// （恒可见 / 恒可编辑），null 在这个设计里不表示「未安装」。
        /// </para>
        /// </summary>
        [Test]
        public void WithoutCondition_DefaultsToEditable()
        {
            var tree = BuildTree();
            var node = Find(tree, "flag");

            Assert.That(node.State.IsReadOnly, Is.False);
            Assert.That(node.IsVisible, Is.True);
        }

        #endregion

        #region 模式条件

        /// <summary>
        /// 模式条件的判据是 <c>Application.isPlaying</c>。编辑模式下测试跑，
        /// 所以「编辑模式」那一组生效、「播放模式」那一组不生效。
        /// </summary>
        [Test]
        public void ModeConditions_FollowPlayState()
        {
            var tree = BuildTree();

            // 编辑模式：HideInEditorMode 隐藏、HideInPlayMode 可见
            Assert.That(Find(tree, "editorHidden").IsVisible, Is.False);
            Assert.That(Find(tree, "playHidden").IsVisible, Is.True);

            // 同理作用于只读
            Assert.That(Find(tree, "editorDisabled").State.IsReadOnly, Is.True);
            Assert.That(Find(tree, "playDisabled").State.IsReadOnly, Is.False);
        }

        /// <summary>
        /// 模式条件用的是缓存的静态委托——没有捕获，可安全共享。
        /// <para>
        /// 这条守的是「不要为每个属性各 new 一个闭包」：条件每帧都要调，而静态委托
        /// 在多个属性之间是同一个实例。
        /// </para>
        /// </summary>
        [Test]
        public void ModeConditions_ShareStaticResolvers()
        {
            var tree = BuildTree();

            // 直接对着静态字段比实例：这些求值器没有捕获、可以安全共享，
            // 而条件是每帧要调的——省下的是每次建树时每个属性一个闭包。
            // 若哪天有人把它们改成 `() => Application.isPlaying` 内联字面量，
            // 比较会失败，而性能退化本身不会在别处暴露出来。
            Assert.That(
                Find(tree, "playHidden").State.VisibilityResolver,
                Is.SameAs(ModeConditions.IsNotPlaying));

            Assert.That(
                Find(tree, "editorHidden").State.VisibilityResolver,
                Is.SameAs(ModeConditions.IsPlaying));

            Assert.That(
                Find(tree, "playDisabled").State.ReadOnlyResolver,
                Is.SameAs(ModeConditions.IsPlaying));
        }

        #endregion

        #region 条件解析失败

        /// <summary>
        /// 条件名不存在时：**保持可见、记一条告警、不抛**。
        /// <para>
        /// 一个拼错的条件名不该让使用方的第一眼是一片白屏。放弃的后果是「条件不生效、
        /// 字段照常显示」，配合告警足以定位。
        /// </para>
        /// </summary>
        [Test]
        public void MissingCondition_StaysVisibleAndWarns()
        {
            LogAssert.Expect(LogType.Warning, new Regex("无法求值"));

            var broken = ScriptableObject.CreateInstance<BrokenConditionFixture>();

            try
            {
                var tree = PropertyTree.Create(new SerializedObject(broken));
                var node = Find(tree, "broken");

                Assert.That(node.IsVisible, Is.True, "解析失败应当保持可见，而不是藏起来。");
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>构建被测的树。</summary>
        /// <returns>属性树。</returns>
        private PropertyTree BuildTree()
        {
            return PropertyTree.Create(new SerializedObject(_target));
        }

        /// <summary>
        /// 经序列化对象改写条件字段，并刷新**树自己的**序列化对象。
        /// </summary>
        /// <param name="tree">属性树——条件求值器读的是它所属的那个序列化对象。</param>
        /// <param name="value">新值。</param>
        /// <remarks>
        /// 那次 <c>Update</c> 不是可有可无的：求值器捕获的是树所属
        /// <see cref="SerializedObject"/> 上的属性，而这里是通过**另一个** SerializedObject
        /// 写的值，不刷新就读不到。真实绘制路径每帧开头本来就会 Update
        /// （见 <see cref="PropertyTreeHost.Draw"/> 与 <see cref="XInspectorEditor.OnInspectorGUI"/>），
        /// 测试要照着做才等价。
        /// </remarks>
        private void SetFlag(PropertyTree tree, bool value)
        {
            var serializedObject = new SerializedObject(_target);
            serializedObject.FindProperty("flag").boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            tree.SerializedObject.Update();
        }

        /// <summary>按路径查找根的直接子节点。</summary>
        /// <param name="tree">属性树。</param>
        /// <param name="path">节点路径。</param>
        /// <returns>找到的节点；不存在返回 <c>null</c>。</returns>
        private static InspectorProperty Find(PropertyTree tree, string path)
        {
            foreach (var child in tree.Root.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            return null;
        }

        #endregion
    }

    /// <summary>
    /// 条件族测试用资产。
    /// </summary>
    internal sealed class ConditionFixture : ScriptableObject
    {
        /// <summary>被所有条件引用的开关。</summary>
        public bool flag;

        /// <summary>条件为真时显示。</summary>
        [ShowIf(nameof(flag))]
        public int shown;

        /// <summary>条件为真时隐藏。</summary>
        [HideIf(nameof(flag))]
        public int hidden;

        /// <summary>条件为真时可编辑。</summary>
        [EnableIf(nameof(flag))]
        public int enabled;

        /// <summary>条件为真时只读。</summary>
        [DisableIf(nameof(flag))]
        public int disabled;

        /// <summary>播放中隐藏。</summary>
        [HideInPlayMode]
        public int playHidden;

        /// <summary>编辑模式下隐藏。</summary>
        [HideInEditorMode]
        public int editorHidden;

        /// <summary>播放中禁用。</summary>
        [DisableInPlayMode]
        public int playDisabled;

        /// <summary>编辑模式下禁用。</summary>
        [DisableInEditorMode]
        public int editorDisabled;
    }

    /// <summary>
    /// 条件名不存在的资产——**单独一个 fixture**，避免它的告警污染其它用例。
    /// </summary>
    internal sealed class BrokenConditionFixture : ScriptableObject
    {
        /// <summary>引用了不存在的条件。</summary>
        [ShowIf("这个成员不存在")]
        public int broken;
    }
}
