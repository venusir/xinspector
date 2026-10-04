using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 非 Inspector 上下文的属性树宿主：附加、幂等、错误路径、重置、释放。
    /// <para>
    /// 绘制本身不在这里验证——IMGUI 的渲染结果无法有意义地断言。宿主里一切**决策**
    /// （附加与否、是否重建、错误内容、重置是否成功）都做成了可读属性，正是为了能被这里覆盖。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PropertyTreeHostTests
    {
        #region Private Fields

        private HostFixture _target;
        private HostFixture _other;
        private PropertyTreeHost _host;

        #endregion

        #region Setup / Teardown

        /// <summary>建立资产与宿主。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<HostFixture>();
            _other = ScriptableObject.CreateInstance<HostFixture>();
            _host = new PropertyTreeHost(null);
        }

        /// <summary>释放宿主、销毁资产、复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            _host?.Dispose();
            _host = null;

            Destroy(ref _target);
            Destroy(ref _other);

            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region 附加

        /// <summary>
        /// 附加后建出树，根下有成员。
        /// </summary>
        [Test]
        public void Attach_BuildsTree()
        {
            Assert.That(_host.Attach(_target), Is.True);

            Assert.That(_host.IsAttached, Is.True);
            Assert.That(_host.Target, Is.SameAs(_target));
            Assert.That(_host.Error, Is.Null);
            Assert.That(_host.Tree, Is.Not.Null);
            Assert.That(_host.Tree.Root.Children.Count, Is.GreaterThan(0));
        }

        /// <summary>
        /// 同一目标重复附加是幂等的——**树实例不变**，不重建。
        /// <para>
        /// 调用方会在每帧的绘制入口无脑调它，所以这条是必需的，不是优化。
        /// </para>
        /// </summary>
        [Test]
        public void Attach_IsIdempotentForSameTarget()
        {
            _host.Attach(_target);
            var firstTree = _host.Tree;

            Assert.That(_host.Attach(_target), Is.True);
            Assert.That(_host.Tree, Is.SameAs(firstTree), "同目标不该重建树。");
        }

        /// <summary>
        /// 宿主建出来的树**不记 Undo**——窗口内编辑不进撤销栈是本包对窗口的一贯约定，
        /// 按钮调用这类写操作同样受它约束。
        /// <para>
        /// 由宿主统一置位而不是让每个绘制器自己判断：绘制器无从知道自己在哪条路径上被画，
        /// 而宿主恰好知道。
        /// </para>
        /// </summary>
        [Test]
        public void Attach_窗口树不记Undo()
        {
            Assert.That(_host.Attach(_target), Is.True);

            Assert.That(_host.Tree.UndoEnabled, Is.False);
        }

        /// <summary>
        /// 换目标会重建树。
        /// </summary>
        [Test]
        public void Attach_DifferentTargetRebuilds()
        {
            _host.Attach(_target);
            var firstTree = _host.Tree;

            _host.Attach(_other);

            Assert.That(_host.Target, Is.SameAs(_other));
            Assert.That(_host.Tree, Is.Not.SameAs(firstTree));
        }

        /// <summary>
        /// 传 null 等于清空，且不抛。
        /// </summary>
        [Test]
        public void Attach_NullClears()
        {
            _host.Attach(_target);

            Assert.That(_host.Attach(null), Is.False);
            Assert.That(_host.IsAttached, Is.False);
            Assert.That(_host.Tree, Is.Null);
            Assert.That(_host.Target, Is.Null);
        }

        #endregion

        #region 错误路径

        /// <summary>
        /// 建树失败时**记录错误而不抛**，且不留下半棵树。
        /// <para>
        /// 一个写坏的目标（这里是 <c>[BoxGroup("")]</c>，它在构建期抛
        /// <see cref="System.ArgumentException"/>）不该让窗口白屏——那往往是使用方看到本插件的
        /// 第一眼。错误要显式可见（<see cref="PropertyTreeHost.Draw"/> 会画成 HelpBox），
        /// 但不能是异常。
        /// </para>
        /// </summary>
        [Test]
        public void Attach_BrokenTargetRecordsErrorInsteadOfThrowing()
        {
            var broken = ScriptableObject.CreateInstance<BrokenHostTarget>();

            try
            {
                Assert.That(() => _host.Attach(broken), Throws.Nothing);
                Assert.That(_host.IsAttached, Is.False);
                Assert.That(_host.Tree, Is.Null);
                Assert.That(_host.Error, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        /// <summary>
        /// 失败也算「处理过」：重复附加同一坏目标不会重试、不会累积异常。
        /// <para>
        /// 若把失败排除在幂等之外，宿主会每帧重试一次，而重试的结果必然相同——
        /// 白白抛异常，错误信息也不会变。
        /// </para>
        /// </summary>
        [Test]
        public void Attach_BrokenTargetIsAlsoIdempotent()
        {
            var broken = ScriptableObject.CreateInstance<BrokenHostTarget>();

            try
            {
                _host.Attach(broken);
                var firstError = _host.Error;

                Assert.That(_host.Attach(broken), Is.False);
                Assert.That(_host.Error, Is.EqualTo(firstError));
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        /// <summary>
        /// 失败后换一个正常目标仍能成功——错误不会粘住宿主。
        /// </summary>
        [Test]
        public void Attach_AfterFailure_SucceedsOnGoodTarget()
        {
            var broken = ScriptableObject.CreateInstance<BrokenHostTarget>();

            try
            {
                _host.Attach(broken);
                Assert.That(_host.Error, Is.Not.Null);

                Assert.That(_host.Attach(_target), Is.True);
                Assert.That(_host.Error, Is.Null);
                Assert.That(_host.IsAttached, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        #endregion

        #region 重建

        /// <summary>
        /// <see cref="PropertyTreeHost.Reload"/> 用同一目标重建，树实例变化、内容仍在。
        /// </summary>
        [Test]
        public void Reload_RebuildsForSameTarget()
        {
            _host.Attach(_target);
            var firstTree = _host.Tree;

            _host.Reload();

            Assert.That(_host.Target, Is.SameAs(_target));
            Assert.That(_host.Tree, Is.Not.SameAs(firstTree));
            Assert.That(_host.IsAttached, Is.True);
        }

        /// <summary>
        /// 未附加时重建是空操作，不抛。
        /// </summary>
        [Test]
        public void Reload_WithoutAttachIsNoOp()
        {
            Assert.That(() => _host.Reload(), Throws.Nothing);
            Assert.That(_host.IsAttached, Is.False);
        }

        #endregion

        #region 重置

        /// <summary>
        /// 重置把成员恢复成 C# 初始值。
        /// <para>
        /// 这条同时验证了「无 Undo 的写回确实生效」——重置走的就是那条路。
        /// </para>
        /// </summary>
        [Test]
        public void ResetToDefaults_RestoresInitialValues()
        {
            _host.Attach(_target);
            SetTargetNumber(999);

            Assert.That(_host.ResetToDefaults(), Is.True);
            Assert.That(_target.number, Is.EqualTo(HostFixture.DefaultNumber));
        }

        /// <summary>
        /// 未附加时重置返回 false，不抛。
        /// </summary>
        [Test]
        public void ResetToDefaults_WithoutAttachReturnsFalse()
        {
            Assert.That(_host.ResetToDefaults(), Is.False);
        }

        /// <summary>
        /// 树里的成员被分组包着时，重置仍要覆盖到它们。
        /// <para>
        /// 这条守的是「收集路径必须递归」：分组节点不是成员，若只取根的直接子节点，
        /// 被分组的字段会被静默漏掉——而漏掉的症状是「重置没生效」，很难联想到遍历方式。
        /// </para>
        /// </summary>
        [Test]
        public void ResetToDefaults_CoversMembersInsideGroups()
        {
            var grouped = ScriptableObject.CreateInstance<GroupedHostFixture>();

            try
            {
                var host = new PropertyTreeHost(null);
                try
                {
                    host.Attach(grouped);

                    var serializedObject = new SerializedObject(grouped);
                    serializedObject.FindProperty("grouped").intValue = 999;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();

                    Assert.That(host.ResetToDefaults(), Is.True);
                    Assert.That(grouped.grouped, Is.EqualTo(GroupedHostFixture.DefaultGrouped));
                }
                finally
                {
                    host.Dispose();
                }
            }
            finally
            {
                Object.DestroyImmediate(grouped);
            }
        }

        #endregion

        #region 窗口路径

        /// <summary>
        /// 窗口上的 <c>[Button]</c> 方法照样进树——成员过滤只管**字段**
        /// （它滤的是 <c>EditorWindow</c> 那七个内部字段），方法收不收由特性决定。
        /// </summary>
        [Test]
        public void Attach_窗口上的按钮方法进树()
        {
            var window = ScriptableObject.CreateInstance<ButtonWindow>();

            try
            {
                var host = new PropertyTreeHost(WindowMemberFilter.For(typeof(XInspectorEditorWindow)));

                try
                {
                    Assert.That(host.Attach(window), Is.True);

                    var paths = new List<string>();
                    foreach (var child in host.Tree.Root.Children)
                    {
                        paths.Add(child.Path);
                    }

                    Assert.That(paths, Does.Contain("DoThing()"), "窗口上的按钮方法该进树。");
                    Assert.That(paths, Does.Contain("ownField"), "窗口自己的字段该在。");
                    Assert.That(
                        paths,
                        Does.Not.Contain("m_ViewDataDictionary"),
                        "EditorWindow 的内部字段仍要被过滤掉。");
                }
                finally
                {
                    host.Dispose();
                }
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        #endregion

        #region 反射目标

        /// <summary>
        /// 任意对象都能附加——目标不必是 <see cref="UnityEngine.Object"/>。
        /// </summary>
        [Test]
        public void Attach_反射目标也能附加()
        {
            var host = new PropertyTreeHost(null);

            try
            {
                Assert.That(host.Attach(new ReflectedPoco()), Is.True);
                Assert.That(host.IsAttached, Is.True);
                Assert.That(host.Tree.SerializedObject, Is.Null, "反射树没有序列化对象。");
                Assert.That(host.Tree.Root.Children.Count, Is.EqualTo(2), "两个带标记的成员都在。");
            }
            finally
            {
                host.Dispose();
            }
        }

        /// <summary>
        /// 反射目标不能重置——后端是只读的，没有写路径。
        /// <para>
        /// 返回 <c>false</c> 而不是抛异常，工具栏据 <see cref="PropertyTreeHost.CanResetToDefaults"/>
        /// 把按钮置灰：<b>不让它「看起来能点」</b>。
        /// </para>
        /// </summary>
        [Test]
        public void Attach_反射目标不可重置()
        {
            var host = new PropertyTreeHost(null);

            try
            {
                host.Attach(new ReflectedPoco());

                Assert.That(host.CanResetToDefaults, Is.False);
                Assert.That(() => host.ResetToDefaults(), Throws.Nothing);
                Assert.That(host.ResetToDefaults(), Is.False);
            }
            finally
            {
                host.Dispose();
            }
        }

        /// <summary>
        /// 控制项：Unity 目标可以重置。
        /// <para>
        /// 没有这一条，上面那条无法区分「按后端判断」与「恒返回 false」。
        /// </para>
        /// </summary>
        [Test]
        public void Attach_Unity目标可重置()
        {
            _host.Attach(_target);

            Assert.That(_host.CanResetToDefaults, Is.True);
        }

        /// <summary>
        /// 空树要被认得出来——窗口据此给一句「只显示标注成员」的解释，而不是留一片空白。
        /// </summary>
        [Test]
        public void Attach_空反射树可识别()
        {
            var host = new PropertyTreeHost(null);

            try
            {
                host.Attach(new EmptyPoco());

                Assert.That(host.HasNothingToDraw, Is.True);
            }
            finally
            {
                host.Dispose();
            }
        }

        /// <summary>
        /// 控制项：只要树里有东西，就不是「空树」。
        /// </summary>
        [Test]
        public void HasNothingToDraw_有成员时为假()
        {
            var host = new PropertyTreeHost(null);

            try
            {
                host.Attach(new ReflectedPoco());

                Assert.That(host.HasNothingToDraw, Is.False);
            }
            finally
            {
                host.Dispose();
            }
        }

        /// <summary>
        /// 已销毁的 Unity 对象等价于清空。
        /// <para>
        /// 形参类型是 <c>object</c>，裸写 <c>target == null</c> 会退化成引用比较、
        /// 挡不住销毁过的对象——放它进去的后果是 <c>new SerializedObject</c> 抛异常，
        /// 附加以「错误」而不是「清空」收场。
        /// </para>
        /// </summary>
        [Test]
        public void Attach_已销毁的目标等价于清空()
        {
            var asset = ScriptableObject.CreateInstance<HostFixture>();
            _host.Attach(_target);

            var destroyed = asset;
            Object.DestroyImmediate(asset);

            Assert.That(() => _host.Attach(destroyed), Throws.Nothing);
            Assert.That(_host.Attach(destroyed), Is.False);
            Assert.That(_host.IsAttached, Is.False);
            Assert.That(_host.Target, Is.Null);
        }

        #endregion

        #region 释放

        /// <summary>
        /// 释放后树与目标都被清空，但**目标对象本身不被销毁**——所有权在调用方。
        /// </summary>
        [Test]
        public void Dispose_ClearsTreeButNotTarget()
        {
            _host.Attach(_target);

            _host.Dispose();

            Assert.That(_host.Tree, Is.Null);
            Assert.That(_host.Target, Is.Null);
            Assert.That(_target, Is.Not.Null, "宿主不该销毁它并不拥有的目标对象。");
        }

        /// <summary>
        /// 释放是幂等的。
        /// </summary>
        [Test]
        public void Dispose_IsIdempotent()
        {
            _host.Attach(_target);

            Assert.That(() =>
            {
                _host.Dispose();
                _host.Dispose();
            }, Throws.Nothing);
        }

        /// <summary>
        /// 释放宿主会释放树上的状态——节点状态里可释放的资源（如内嵌编辑器的嵌套
        /// <c>Editor</c> 实例）不跟着宿主一起漏掉。
        /// </summary>
        [Test]
        public void Dispose_DisposesTreeState()
        {
            _host.Attach(_target);
            var probe = _host.Tree.Root.Children[0].State.GetOrCreate<DisposableProbeState>();

            _host.Dispose();

            Assert.That(probe.DisposeCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 换目标时旧树被释放。这条走的是 <c>Attach</c> 里的 <c>Clear</c>——
        /// 最容易被漏掉的一条路（它看起来只是「改个引用」）。
        /// </summary>
        [Test]
        public void Attach_SwitchingTarget_DisposesPreviousTreeState()
        {
            _host.Attach(_target);
            var probe = _host.Tree.Root.Children[0].State.GetOrCreate<DisposableProbeState>();

            _host.Attach(_other);

            Assert.That(probe.DisposeCount, Is.EqualTo(1));
        }

        /// <summary>重载同样经 <c>Clear</c>，旧树的状态照样释放。</summary>
        [Test]
        public void Reload_DisposesPreviousTreeState()
        {
            _host.Attach(_target);
            var probe = _host.Tree.Root.Children[0].State.GetOrCreate<DisposableProbeState>();

            _host.Reload();

            Assert.That(probe.DisposeCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 释放后附加一律失败，不会让已释放的宿主复活。
        /// </summary>
        [Test]
        public void Attach_AfterDisposeFails()
        {
            _host.Dispose();

            Assert.That(_host.Attach(_target), Is.False);
            Assert.That(_host.IsAttached, Is.False);
        }

        #endregion

        #region Private Helpers

        /// <summary>经序列化对象改写目标上的整数字段。</summary>
        /// <param name="value">新值。</param>
        private void SetTargetNumber(int value)
        {
            var serializedObject = new SerializedObject(_target);
            serializedObject.FindProperty("number").intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>销毁资产。</summary>
        /// <typeparam name="T">资产类型。</typeparam>
        /// <param name="target">引用，销毁后置空。</param>
        private static void Destroy<T>(ref T target) where T : ScriptableObject
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
                target = null;
            }
        }

        #endregion
    }

    /// <summary>
    /// 宿主测试用的资产。
    /// </summary>
    internal sealed class HostFixture : ScriptableObject
    {
        /// <summary>默认整数值。</summary>
        public const int DefaultNumber = 7;

        /// <summary>整数成员。</summary>
        public int number = DefaultNumber;
    }

    /// <summary>
    /// 成员被分组包着的资产——用来验证重置的路径收集确实递归。
    /// </summary>
    internal sealed class GroupedHostFixture : ScriptableObject
    {
        /// <summary>默认值。</summary>
        public const int DefaultGrouped = 3;

        /// <summary>位于分组内的成员。</summary>
        [BoxGroup("分组")]
        public int grouped = DefaultGrouped;

        /// <summary>分组外的成员。</summary>
        public int plain;
    }

    /// <summary>
    /// 构建期必抛的目标：<c>[BoxGroup("")]</c> 的空路径在构造分组特性时即报错。
    /// </summary>
    internal sealed class BrokenHostTarget : ScriptableObject
    {
        /// <summary>带无效分组路径的成员。</summary>
        [BoxGroup("")]
        public int value;
    }

    /// <summary>
    /// 带按钮方法的窗口——用来验证窗口路径的成员过滤不会把方法节点一起滤掉。
    /// <para>
    /// 窗口可以无头建出来（<c>CreateInstance</c>），故这条用例不需要真的开一个窗口。
    /// </para>
    /// </summary>
    internal sealed class ButtonWindow : XInspectorEditorWindow
    {
        /// <summary>窗口自己的字段。</summary>
        public int ownField;

        /// <summary>窗口上的按钮。</summary>
        [Button]
        private void DoThing()
        {
        }
    }
}
