using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 属性树构建的成员过滤管道。
    /// <para>
    /// 这里最重要的不是「过滤生效了」，而是**不过滤时行为一字未变**——Inspector 路径依赖
    /// 那份行为（它刻意保留 MonoBehaviour/ScriptableObject 的 <c>m_Script</c> 槽位以与原生
    /// Inspector 逐像素一致）。所以本 fixture 里既有正向用例，也有守默认行为的回归守卫。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PropertyTreeMemberFilterTests
    {
        #region Private Fields

        private TreeFilterFixture _target;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<TreeFilterFixture>();
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

            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region 默认行为（回归守卫）

        /// <summary>
        /// 不带过滤器时，两个字段都在。
        /// </summary>
        [Test]
        public void Build_WithoutFilter_KeepsEveryField()
        {
            var paths = PathsOf(BuildTree(null));

            Assert.That(paths, Does.Contain("kept"));
            Assert.That(paths, Does.Contain("dropped"));
        }

        /// <summary>
        /// **关键回归守卫**：不带过滤器时，Unity 注入的成员（<c>m_Script</c>）仍然出现。
        /// <para>
        /// 这正是 Inspector 路径刻意要的行为——原生 Inspector 会画出 Script 槽位，
        /// 我们跟着画才谈得上「逐像素一致」。过滤规则是**窗口专用**的，绝不能变成全局默认，
        /// 这条用例钉住的就是这一点。
        /// </para>
        /// </summary>
        [Test]
        public void Build_WithoutFilter_KeepsUnityInjectedMembers()
        {
            var paths = PathsOf(BuildTree(null));

            Assert.That(paths, Does.Contain("m_Script"),
                "不过滤时 m_Script 必须在——Inspector 路径靠它对齐原生渲染。");
        }

        /// <summary>
        /// 显式传 null 过滤器与调用单参重载等价。
        /// </summary>
        [Test]
        public void Build_WithNullFilter_BehavesLikeDefault()
        {
            var withNull = PathsOf(BuildTree(null));
            var withDefault = PathsOf(PropertyTree.Create(new SerializedObject(_target)));

            Assert.That(withNull, Is.EqualTo(withDefault));
        }

        #endregion

        #region 过滤生效

        /// <summary>
        /// 被拒的成员不建节点，其余照旧。
        /// </summary>
        [Test]
        public void Build_WithFilter_DropsRejectedMemberOnly()
        {
            var paths = PathsOf(BuildTree(field => field == null || field.Name != "dropped"));

            Assert.That(paths, Does.Contain("kept"));
            Assert.That(paths, Does.Not.Contain("dropped"));
        }

        /// <summary>
        /// 一律拒绝时树仍成立——只有根，没有成员。空树不是错误。
        /// </summary>
        [Test]
        public void Build_WithRejectAllFilter_ProducesChildlessRoot()
        {
            var tree = BuildTree(field => false);

            Assert.That(tree.Root.Children, Is.Empty);
            Assert.That(tree.Root.Kind, Is.EqualTo(InspectorPropertyKind.Root));
            Assert.That(tree.Root.Chain, Is.Not.Null, "根仍要装配链条——链条永不为空。");
        }

        /// <summary>
        /// 过滤器对**每个可见成员各调用一次**，且全收时节点数与之一致。
        /// <para>
        /// 这条挡的是「过滤与建节点各解析一次字段、结果不一致」——实现里刻意只解析一次并共用，
        /// 用例把那个约定钉住。
        /// </para>
        /// </summary>
        [Test]
        public void Build_FilterIsCalledOncePerMember()
        {
            var seen = new List<string>();
            var tree = BuildTree(field =>
            {
                seen.Add(field?.Name ?? "<无托管字段>");
                return true;
            });

            Assert.That(seen.Count, Is.EqualTo(tree.Root.Children.Count),
                "过滤器应对每个可见成员各调用一次，且被收下的都建了节点。");
            Assert.That(seen, Does.Contain("kept"));
            Assert.That(seen, Does.Contain("dropped"));
        }

        /// <summary>
        /// Unity 注入的成员（没有对应托管字段）在过滤器里表现为 <c>null</c>。
        /// <para>
        /// 这正是窗口路径赖以排除 <c>m_Script</c> 的途径——<see cref="WindowMemberFilter"/>
        /// 收到 <c>null</c> 即判拒。<c>m_Script</c> 对 <see cref="ScriptableObject"/> 而言
        /// 是序列化层面的东西，没有对应的托管字段。
        /// </para>
        /// </summary>
        [Test]
        public void Build_FilterSeesNullForUnityInjectedMembers()
        {
            var sawNull = false;

            BuildTree(field =>
            {
                if (field == null)
                {
                    sawNull = true;
                }

                return true;
            });

            Assert.That(sawNull, Is.True,
                "没有任何成员让过滤器收到 null——说明本用例的前提变了，窗口排除 m_Script 的机制需要重新确认。");
        }

        #endregion

        #region 参数防御

        /// <summary>
        /// 过滤重载同样校验序列化对象。
        /// </summary>
        [Test]
        public void Create_WithFilter_NullSerializedObjectThrows()
        {
            Assert.That(() => PropertyTree.Create(null, field => true), Throws.ArgumentNullException);
        }

        #endregion

        #region Private Helpers

        /// <summary>构建被测的树。</summary>
        /// <param name="filter">成员过滤器，可为 <c>null</c>。</param>
        /// <returns>属性树。</returns>
        private PropertyTree BuildTree(System.Func<System.Reflection.FieldInfo, bool> filter)
        {
            return PropertyTree.Create(new SerializedObject(_target), filter);
        }

        /// <summary>取根下所有子节点的路径。</summary>
        /// <param name="tree">属性树。</param>
        /// <returns>路径列表。</returns>
        private static List<string> PathsOf(PropertyTree tree)
        {
            var paths = new List<string>();
            foreach (var child in tree.Root.Children)
            {
                paths.Add(child.Path);
            }

            return paths;
        }

        #endregion
    }

    /// <summary>
    /// 成员过滤测试用的资产：两个字段，一个留一个去。
    /// </summary>
    internal sealed class TreeFilterFixture : ScriptableObject
    {
        /// <summary>留下的字段。</summary>
        public int kept = 1;

        /// <summary>被过滤掉的字段。</summary>
        public int dropped = 2;
    }
}
