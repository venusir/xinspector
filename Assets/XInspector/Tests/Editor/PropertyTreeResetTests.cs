using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 重置机制本身。
    /// <para>
    /// 这个 fixture 刻意**不碰窗口**：机制是对两个 <see cref="SerializedObject"/> 的纯操作，
    /// 用两个普通资产就能完整覆盖。窗口只是它的一个调用方，把机制的可测性绑在窗口上没有必要。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PropertyTreeResetTests
    {
        #region Private Fields

        private ResetFixture _target;
        private ResetFixture _defaults;

        #endregion

        #region Setup / Teardown

        /// <summary>建立两个资产：一个被改，一个保持初始值充当「默认来源」。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ResetFixture>();
            _defaults = ScriptableObject.CreateInstance<ResetFixture>();
        }

        /// <summary>销毁资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            Destroy(ref _target);
            Destroy(ref _defaults);

            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region Apply

        /// <summary>
        /// 改过的值被来源上的初始值覆盖回去。
        /// </summary>
        [Test]
        public void Apply_CopiesValuesFromSource()
        {
            SetTargetValue("number", 999);
            SetTargetValue("text", "改过了");

            var applied = PropertyTreeReset.Apply(Paths("number", "text"), new SerializedObject(_target), _defaults);

            Assert.That(applied, Is.True);
            Assert.That(_target.number, Is.EqualTo(ResetFixture.DefaultNumber));
            Assert.That(_target.text, Is.EqualTo(ResetFixture.DefaultText));
        }

        /// <summary>
        /// 只重置给定的路径，没给的保持不变。
        /// </summary>
        [Test]
        public void Apply_LeavesUnlistedMembersAlone()
        {
            SetTargetValue("number", 999);
            SetTargetValue("text", "改过了");

            PropertyTreeReset.Apply(Paths("number"), new SerializedObject(_target), _defaults);

            Assert.That(_target.number, Is.EqualTo(ResetFixture.DefaultNumber));
            Assert.That(_target.text, Is.EqualTo("改过了"), "没列进路径的成员不该被动。");
        }

        /// <summary>
        /// 来源上不存在的路径**跳过而不抛**——重置是尽力而为的便利操作，
        /// 为一条对不上的路径让整个操作失败没有意义。返回值让调用方能区分
        /// 「重置了」与「什么都没做」。
        /// </summary>
        [Test]
        public void Apply_UnknownPathIsSkipped()
        {
            SetTargetValue("number", 999);

            var applied = PropertyTreeReset.Apply(
                Paths("number", "这个路径不存在"),
                new SerializedObject(_target),
                _defaults);

            Assert.That(applied, Is.True, "有一条命中就该算重置成功。");
            Assert.That(_target.number, Is.EqualTo(ResetFixture.DefaultNumber));
        }

        /// <summary>
        /// 一条路径都不命中时返回 false，且不写回。
        /// </summary>
        [Test]
        public void Apply_NoMatchingPathReturnsFalse()
        {
            SetTargetValue("number", 999);

            var applied = PropertyTreeReset.Apply(
                Paths("并不存在"),
                new SerializedObject(_target),
                _defaults);

            Assert.That(applied, Is.False);
            Assert.That(_target.number, Is.EqualTo(999), "什么都没命中时不该顺手写回。");
        }

        /// <summary>
        /// 空路径列表返回 false。
        /// </summary>
        [Test]
        public void Apply_EmptyPathListReturnsFalse()
        {
            var applied = PropertyTreeReset.Apply(
                new List<string>(),
                new SerializedObject(_target),
                _defaults);

            Assert.That(applied, Is.False);
        }

        /// <summary>
        /// 三个参数任一为 null 都返回 false，不抛。
        /// </summary>
        [Test]
        public void Apply_NullArgumentsReturnFalse()
        {
            var paths = Paths("number");

            Assert.That(PropertyTreeReset.Apply(null, new SerializedObject(_target), _defaults), Is.False);
            Assert.That(PropertyTreeReset.Apply(paths, null, _defaults), Is.False);
            Assert.That(PropertyTreeReset.Apply(paths, new SerializedObject(_target), null), Is.False);
        }

        #endregion

        #region CollectMemberPaths

        /// <summary>
        /// 收集到的路径覆盖成员。
        /// </summary>
        [Test]
        public void CollectMemberPaths_IncludesMembers()
        {
            var tree = BuildTree(_target);
            var paths = PropertyTreeReset.CollectMemberPaths(tree);

            Assert.That(paths, Does.Contain("number"));
            Assert.That(paths, Does.Contain("text"));
        }

        /// <summary>
        /// **必须排除脚本绑定。**
        /// <para>
        /// 默认来源是 <c>CreateInstance</c> 出来的一次性实例，其 <c>m_Script</c> 是空的；
        /// 把它复制回去会清掉目标的脚本关联——接到真实 <c>.asset</c> 上就是把资产弄坏。
        /// 我们自己造的目标本来就绑定为空，所以现在看不出问题；用例把这条防护钉住，
        /// 免得日后有人「顺手」把它当成普通成员收进去。
        /// </para>
        /// </summary>
        [Test]
        public void CollectMemberPaths_ExcludesScriptBinding()
        {
            var tree = BuildTree(_target);

            // 先确认前提：不过滤的树里确实有 m_Script，否则这条用例是空转。
            var rawPaths = new List<string>();
            foreach (var child in tree.Root.Children)
            {
                rawPaths.Add(child.Path);
            }

            Assert.That(rawPaths, Does.Contain("m_Script"), "前提不成立：树里没有 m_Script，本用例无意义。");

            Assert.That(PropertyTreeReset.CollectMemberPaths(tree), Does.Not.Contain("m_Script"));
        }

        /// <summary>
        /// 被分组包着的成员也要收进来。
        /// <para>
        /// 分组节点不是成员，若只取根的直接子节点，被分组的字段会被静默漏掉——
        /// 症状是「重置没生效」，很难联想到遍历方式。
        /// </para>
        /// </summary>
        [Test]
        public void CollectMemberPaths_IncludesMembersInsideGroups()
        {
            var grouped = ScriptableObject.CreateInstance<GroupedHostFixture>();

            try
            {
                var paths = PropertyTreeReset.CollectMemberPaths(BuildTree(grouped));

                Assert.That(paths, Does.Contain("grouped"));
                Assert.That(paths, Does.Contain("plain"));
            }
            finally
            {
                Object.DestroyImmediate(grouped);
            }
        }

        /// <summary>
        /// 传 null 树返回空列表，不抛。
        /// </summary>
        [Test]
        public void CollectMemberPaths_NullTreeReturnsEmpty()
        {
            var paths = PropertyTreeReset.CollectMemberPaths(null);

            Assert.That(paths, Is.Not.Null);
            Assert.That(paths, Is.Empty);
        }

        #endregion

        #region Private Helpers

        /// <summary>为一个对象建树。</summary>
        /// <param name="target">目标对象。</param>
        /// <returns>属性树。</returns>
        private static PropertyTree BuildTree(Object target)
        {
            return PropertyTree.Create(new SerializedObject(target));
        }

        /// <summary>构造路径列表。</summary>
        /// <param name="paths">路径。</param>
        /// <returns>列表。</returns>
        private static List<string> Paths(params string[] paths)
        {
            return new List<string>(paths);
        }

        /// <summary>经序列化对象改写目标上的一个字段。</summary>
        /// <param name="propertyPath">属性路径。</param>
        /// <param name="value">新值。</param>
        private void SetTargetValue(string propertyPath, object value)
        {
            var serializedObject = new SerializedObject(_target);
            var property = serializedObject.FindProperty(propertyPath);

            Assert.That(property, Is.Not.Null, $"找不到属性 {propertyPath}，用例前提不成立。");

            if (value is int intValue)
            {
                property.intValue = intValue;
            }
            else
            {
                property.stringValue = (string)value;
            }

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
    /// 重置测试用的资产。
    /// </summary>
    internal sealed class ResetFixture : ScriptableObject
    {
        /// <summary>默认整数值。</summary>
        public const int DefaultNumber = 7;

        /// <summary>默认字符串值。</summary>
        public const string DefaultText = "默认";

        /// <summary>整数成员。</summary>
        public int number = DefaultNumber;

        /// <summary>字符串成员。</summary>
        public string text = DefaultText;
    }
}
