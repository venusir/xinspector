using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[InlineButton]</c>：解析成不成功、失败时字段还在不在、多个按钮互不串位。
    /// <para>
    /// 与按钮族其它成员同一条纪律：解析失败**告警且不静默**，但绝不连累字段本身。
    /// </para>
    /// </summary>
    [TestFixture]
    public class InlineButtonDrawerTests
    {
        #region Private Fields

        private InlineButtonFixture _target;

        #endregion

        #region Setup / Teardown

        /// <summary>建立测试资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<InlineButtonFixture>();
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

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 解析

        /// <summary>字段上的按钮解析到目标对象上的无参方法。</summary>
        [Test]
        public void 解析到目标对象上的方法()
        {
            using (var tree = Build())
            {
                var state = StateOf(tree, "resolved");

                Assert.That(state.Methods[0][0], Is.Not.Null);
                Assert.That(state.Methods[0][0].Name, Is.EqualTo("OnResolved"));
                Assert.That(state.Labels[0].text, Is.EqualTo("OnResolved"), "没给文本时用方法名。");
                Assert.That(state.Labels[0].tooltip, Is.Null.Or.Empty, "成功时不该有警告提示。");
            }
        }

        /// <summary>给了文本就用文本。</summary>
        [Test]
        public void 自定义文本生效()
        {
            using (var tree = Build())
            {
                Assert.That(StateOf(tree, "labeled").Labels[0].text, Is.EqualTo("随机"));
            }
        }

        /// <summary>
        /// 方法不存在时**告警**，按钮禁用并把原因挂在 Tooltip 上；字段本身照常绘制。
        /// </summary>
        [Test]
        public void 方法不存在时告警且不连累字段()
        {
            LogAssert.Expect(LogType.Warning, new Regex("无法调用"));

            using (var tree = Build())
            {
                var state = StateOf(tree, "missing");
                var node = Find(tree, "missing");

                Assert.That(state.Methods[0], Is.Null);
                Assert.That(state.Labels[0].tooltip, Does.Contain("找不到"), "原因要写在脸上。");
                Assert.That(node.ValueEntry, Is.Not.Null, "字段自己的值入口必须还在。");
                Assert.That(node.Chain.Entries.Length, Is.GreaterThan(0), "链还在，字段照画。");
            }
        }

        /// <summary>方法有参数时给出的是**另一句**原因：该做的是去掉参数，而不是改名。</summary>
        [Test]
        public void 方法有参数时给出对应原因()
        {
            LogAssert.Expect(LogType.Warning, new Regex("无法调用"));

            using (var tree = Build())
            {
                Assert.That(StateOf(tree, "wrongShape").Labels[0].tooltip, Does.Contain("无参"));
            }
        }

        /// <summary>同一个字段上挂两个按钮时各解析各的，互不串位。</summary>
        [Test]
        public void 多个按钮各解析各的()
        {
            using (var tree = Build())
            {
                var state = StateOf(tree, "chained");

                Assert.That(state.Methods.Length, Is.EqualTo(2), "两个按钮各占一格。");
                Assert.That(state.Methods[0][0].Name, Is.EqualTo("OnFirst"));
                Assert.That(state.Methods[1][0].Name, Is.EqualTo("OnSecond"));
                Assert.That(state.Labels[0].text, Is.EqualTo("第一个"));
                Assert.That(state.Labels[1].text, Is.EqualTo("第二个"));
            }
        }

        /// <summary>
        /// 同一个字段上挂两个**写法完全相同**的按钮也算两个：按引用比对而不是按值。
        /// <see cref="System.Attribute"/> 重写了 <c>Equals</c>（按字段值比），照搬它会漏掉一个。
        /// </summary>
        [Test]
        public void 写法相同的两个按钮也算两个()
        {
            using (var tree = Build())
            {
                var state = StateOf(tree, "twin");

                Assert.That(state.Methods.Length, Is.EqualTo(2));
                Assert.That(state.Methods[0][0], Is.Not.Null);
                Assert.That(state.Methods[1][0], Is.Not.Null);
            }
        }

        /// <summary>解析失败不影响链上其它绘制器——<c>[LabelText]</c> 照常改名。</summary>
        [Test]
        public void 失败不影响其它绘制器()
        {
            LogAssert.Expect(LogType.Warning, new Regex("无法调用"));

            using (var tree = Build())
            {
                Assert.That(Find(tree, "missing").Label.text, Is.EqualTo("改过名的字段"));
            }
        }

        #endregion

        #region 链装配

        /// <summary>
        /// 按钮绘制器在属性带之内、值绘制器之外——顺序错了按钮会被替换型绘制器吞掉。
        /// </summary>
        [Test]
        public void 位于属性带之内值绘制器之外()
        {
            using (var tree = Build())
            {
                var node = Find(tree, "displayed");
                var entries = node.Chain.Entries;

                var inline = IndexOf(entries, typeof(InlineButtonDrawer));
                var value = IndexOf(entries, typeof(DisplayAsStringDrawer));

                Assert.That(inline, Is.GreaterThanOrEqualTo(0), "链上没有 InlineButtonDrawer。");
                Assert.That(value, Is.GreaterThanOrEqualTo(0), "链上没有替换型的 DisplayAsStringDrawer。");
                Assert.That(inline, Is.LessThan(value), "行内按钮必须在替换型值绘制器之外，否则画不出来。");
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>建一棵树。</summary>
        /// <returns>属性树。</returns>
        private PropertyTree Build()
        {
            return PropertyTree.Create(new SerializedObject(_target));
        }

        /// <summary>按路径找根下的子节点。</summary>
        /// <param name="tree">属性树。</param>
        /// <param name="path">路径。</param>
        /// <returns>节点；找不到时返回 <c>null</c>。</returns>
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

        /// <summary>取某个字段的行内按钮状态。</summary>
        /// <param name="tree">属性树。</param>
        /// <param name="path">字段路径。</param>
        /// <returns>状态。</returns>
        private static InlineButtonState StateOf(PropertyTree tree, string path)
        {
            var node = Find(tree, path);

            Assert.That(node, Is.Not.Null, $"树里没有 {path}。");
            return node.State.Get<InlineButtonState>();
        }

        /// <summary>在链上找某个绘制器的位置。</summary>
        /// <param name="entries">链条格子。</param>
        /// <param name="drawerType">绘制器类型。</param>
        /// <returns>下标；找不到时返回 <c>-1</c>。</returns>
        private static int IndexOf(DrawerChainEntry[] entries, System.Type drawerType)
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (drawerType.IsInstanceOfType(entries[i].Drawer))
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion
    }

    /// <summary>行内按钮测试用的资产。</summary>
    internal sealed class InlineButtonFixture : ScriptableObject
    {
        /// <summary>正常解析。</summary>
        [InlineButton("OnResolved")]
        public int resolved;

        /// <summary>带自定义文本。</summary>
        [InlineButton("OnResolved", "随机")]
        public int labeled;

        /// <summary>方法不存在。</summary>
        [InlineButton("NoSuchMethod")]
        [LabelText("改过名的字段")]
        public int missing;

        /// <summary>方法存在但有参数。</summary>
        [InlineButton("WithParameter")]
        public int wrongShape;

        /// <summary>两个按钮。</summary>
        [InlineButton("OnFirst", "第一个")]
        [InlineButton("OnSecond", "第二个")]
        public int chained;

        /// <summary>两个写法完全相同的按钮。</summary>
        [InlineButton("OnFirst")]
        [InlineButton("OnFirst")]
        public int twin;

        /// <summary>与替换型值绘制器同挂一个字段。</summary>
        [InlineButton("OnFirst")]
        [DisplayAsString]
        public int displayed;

        /// <summary>被引用的方法。</summary>
        private void OnResolved()
        {
        }

        /// <summary>另一个被引用的方法。</summary>
        private void OnFirst()
        {
        }

        /// <summary>第三个被引用的方法。</summary>
        private void OnSecond()
        {
        }

        /// <summary>带参数——行内按钮只接受无参方法。</summary>
        private void WithParameter(int value)
        {
        }
    }
}
