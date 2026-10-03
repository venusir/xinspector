using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// L0 验证：Unity **原生**装饰器与内置绘制器是否流经我们的属性树。
    /// <para>
    /// <c>OdinGap.md</c> 里唯一没实测过的推断是「原生装饰器由 <c>PropertyField</c> 照常绘制」，
    /// 逐条表里标 ➖ 的那几项全建立在它上面。本 fixture 把那条推断拆成三条**可无头断言**的性质：
    /// 装饰器不产生额外节点、特性实例确实到达节点、链条保持「只有末端」原样。
    /// 三者合起来说明「我们不重复画、也不吞掉装饰器，一切照旧交给 <c>PropertyField</c>」。
    /// </para>
    /// <para>
    /// <b>这套断言证明不了「画得出来」。</b>渲染发生在 <c>EditorGUILayout.PropertyField</c> 内部，
    /// 按本仓策略不测 IMGUI（伪造 GUI 上下文只会得到「测试断言了自己的 mock」）。
    /// 「画得出来」那一条只能靠沙盒肉眼对照（<c>NativeDecoratorDemo</c> 对 Demo 1）。
    /// 别把本 fixture 当成「渲染已被自动化覆盖」——它守的是结构，不是像素。
    /// </para>
    /// </summary>
    [TestFixture]
    public class NativeDecoratorTests
    {
        #region Private Fields

        private NativeDecoratorFixture _target;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<NativeDecoratorFixture>();
        }

        /// <summary>销毁临时资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null)
            {
                UnityEngine.Object.DestroyImmediate(_target);
                _target = null;
            }

            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region 节点集合

        /// <summary>
        /// 装饰器**不产生额外节点**：根下的路径集合恰好等于字段集合，顺序即声明序。
        /// <para>
        /// 这条是核心。若 Unity 把装饰器暴露成独立的迭代条目，它们会各自变成一个节点
        /// （构建期对任何可见属性都建节点、<c>FindField</c> 返回 null 也照建），
        /// 于是出现「幽灵字段」，且两个 <c>[Header]</c> 的条目还会撞上树的身份契约
        /// （<see cref="InspectorProperty.Path"/> 树内唯一）。断言失败时把实际节点打出来，
        /// 一眼就能看出装饰器是以什么形态进来的。
        /// </para>
        /// <para>
        /// <c>m_Script</c> 在期望列表里是**刻意的**：Inspector 路径不加成员过滤器，
        /// 脚本槽位因此与原生 Inspector 一致地保留（窗口路径才过滤它）。
        /// </para>
        /// </summary>
        [Test]
        public void Build_装饰器不产生额外节点()
        {
            var paths = PathsOf(BuildTree());

            Assert.That(
                paths,
                Is.EqualTo(new[] { "m_Script", "ranged", "tooltipped", "notes", "address", "afterHeader", "nested" }),
                $"节点集合与字段集合不符——装饰器条目可能变成了独立节点。实际节点：[{string.Join(", ", paths)}]");
        }

        /// <summary>
        /// <c>[HideInInspector]</c> 的字段依然被排除（装饰器的存在不影响可见性判断）。
        /// </summary>
        [Test]
        public void Build_隐藏字段仍被排除()
        {
            Assert.That(PathsOf(BuildTree()), Does.Not.Contain("hidden"));
        }

        #endregion

        #region 特性收集

        /// <summary>
        /// 原生特性实例确实到达对应节点——装饰器不会被特性收集环节滤掉。
        /// <para>
        /// 这一条决定了「将来若要自绘装饰器」是否拿得到实例。目前我们不自绘，
        /// 但它守住了「特性收集照搬反射、不挑食」这条性质。
        /// </para>
        /// </summary>
        [Test]
        public void Build_原生特性实例到达节点()
        {
            var root = BuildTree().Root;

            AssertHas<HeaderAttribute>(root, "ranged");
            AssertHas<SpaceAttribute>(root, "ranged");
            AssertHas<UnityEngine.RangeAttribute>(root, "ranged");

            AssertHas<TooltipAttribute>(root, "tooltipped");
            AssertHas<TextAreaAttribute>(root, "notes");
            AssertHas<MultilineAttribute>(root, "address");

            // 第二个 [Header]：两处段头各自独立，互不干扰。
            AssertHas<HeaderAttribute>(root, "afterHeader");
        }

        #endregion

        #region 链条

        /// <summary>
        /// 带装饰器的字段，链上**只有末端绘制器**——我们不重复画装饰器，一切交给 <c>PropertyField</c>。
        /// </summary>
        [Test]
        public void Build_装饰字段的链只有末端绘制器()
        {
            var root = BuildTree().Root;

            foreach (var path in new[] { "ranged", "tooltipped", "notes", "address", "afterHeader" })
            {
                var member = Find(root, path);
                Assert.That(member, Is.Not.Null, $"找不到字段 {path}。");

                Assert.That(
                    member.Chain.Count,
                    Is.EqualTo(1),
                    $"字段 {path} 的链上出现了额外绘制器：装饰器应由 PropertyField 绘制，不该由我们重复画。");

                Assert.That(
                    member.Chain.Entries[0].Drawer,
                    Is.TypeOf<UnityFallbackDrawer>(),
                    $"字段 {path} 的链上唯一一格应当是末端绘制器。");

                Assert.That(
                    member.ValueEntry.SerializedProperty.propertyPath,
                    Is.EqualTo(path),
                    $"字段 {path} 的值入口指向了别的序列化属性。");
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

        /// <summary>取根下所有成员的路径。</summary>
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

        /// <summary>按完整路径查找直接子节点。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">子节点完整路径。</param>
        /// <returns>找到的子节点；不存在返回 <c>null</c>。</returns>
        private static InspectorProperty Find(InspectorProperty parent, string path)
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

        /// <summary>断言指定路径的节点携带某种特性。</summary>
        /// <typeparam name="T">特性类型。</typeparam>
        /// <param name="root">根节点。</param>
        /// <param name="path">成员路径。</param>
        private static void AssertHas<T>(InspectorProperty root, string path) where T : Attribute
        {
            var member = Find(root, path);
            Assert.That(member, Is.Not.Null, $"找不到字段 {path}。");
            Assert.That(
                member.HasAttribute<T>(),
                Is.True,
                $"字段 {path} 上的 {typeof(T).Name} 没有到达节点。");
        }

        #endregion
    }

    /// <summary>
    /// L0 验证用资产：字段与沙盒的 <c>NativeDecoratorDemo</c> 一一对应，
    /// 但不依赖 MonoBehaviour（无头可建）。
    /// </summary>
    internal sealed class NativeDecoratorFixture : ScriptableObject
    {
        /// <summary>三个装饰器叠在同一个字段上。</summary>
        [Header("整数段")]
        [Space(12f)]
        [UnityEngine.Range(0, 10)]
        public int ranged = 5;

        /// <summary>原生 Tooltip。</summary>
        [Tooltip("这是 Unity 原生的 Tooltip")]
        public float tooltipped = 1f;

        /// <summary>多行文本框。</summary>
        [TextArea(2, 5)]
        public string notes = "多行文本";

        /// <summary>固定行数的多行框。</summary>
        [Multiline(3)]
        public string address = "第一行\n第二行";

        /// <summary>第二个段头，与第一个同名。</summary>
        [Header("第二个段头")]
        public string afterHeader = "两个 Header 同时出现";

        /// <summary>嵌套结构，其成员上带原生 <c>[Range]</c>。</summary>
        public NativeDecoratorNested nested;

        /// <summary>被隐藏，不应出现在树里。</summary>
        [HideInInspector]
        public int hidden = 3;
    }

    /// <summary>L0 验证用的嵌套结构。</summary>
    [Serializable]
    internal struct NativeDecoratorNested
    {
        /// <summary>嵌套内部的滑块。</summary>
        [UnityEngine.Range(0f, 1f)]
        public float ratio;
    }
}
