using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using XInspector.Internal;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[Button]</c> 的构建期成果：链上有没有那格绘制器、方法解析对不对、
    /// 参数缓冲备好了没、不可调用时有没有给出原因。
    /// <para>
    /// 绘制本身不在这里验证（IMGUI 测不了），但**按钮能不能点、点了调谁**全部落在这里——
    /// 这两件事恰恰是「画了个按钮却没反应」的成因。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ButtonDrawerTests
    {
        #region Private Fields

        private ButtonFixture _target;

        #endregion

        #region Setup / Teardown

        /// <summary>建立测试资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ButtonFixture>();
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

        #region 装配

        /// <summary>方法节点的链上必须有按钮绘制器——没有它就等于按钮不存在。</summary>
        [Test]
        public void 链上有按钮绘制器()
        {
            using (var tree = Build())
            {
                var node = Find(tree, "Plain()");
                var entries = node.Chain.Entries;

                var found = false;
                for (var i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Drawer is ButtonDrawer)
                    {
                        found = true;
                    }
                }

                Assert.That(found, Is.True, "链上没有 ButtonDrawer。");
            }
        }

        /// <summary>处理器备好了状态，且可调用的方法没有原因。</summary>
        [Test]
        public void 可调用时没有原因()
        {
            using (var tree = Build())
            {
                var state = StateOf(tree, "Plain()");

                Assert.That(state, Is.Not.Null, "处理器没有建状态。");
                Assert.That(state.Reason, Is.Null);
                Assert.That(state.Methods, Is.Not.Null);
                Assert.That(state.Methods[0].Name, Is.EqualTo("Plain"));
                Assert.That(state.Parameters, Is.Empty, "无参方法不该有参数。");
            }
        }

        /// <summary>自定义文本走标签通道——于是包裹型绘制器看到的也是同一个文本。</summary>
        [Test]
        public void 自定义文本写进标签()
        {
            using (var tree = Build())
            {
                Assert.That(Find(tree, "Renamed()").Label.text, Is.EqualTo("改名了"));
            }
        }

        /// <summary>
        /// 覆写链上解析到的是**目标类型上那一份**，不是收集期记下的线索。
        /// <para>
        /// 线索可能是基类上的抽象声明或虚方法，拿它去 <c>Invoke</c> 会调到错的那一份
        /// （抽象声明更是直接抛异常）。
        /// </para>
        /// </summary>
        [Test]
        public void 覆写的方法解析到目标类型那一份()
        {
            var derived = ScriptableObject.CreateInstance<MethodDerivedFixture>();

            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(derived)))
                {
                    var state = StateOf(tree, "Overridden()");

                    Assert.That(state.Reason, Is.Null);
                    Assert.That(
                        state.Methods[0].DeclaringType,
                        Is.EqualTo(typeof(MethodDerivedFixture)),
                        "该调到派生类的覆写，而不是基类的声明。");
                }
            }
            finally
            {
                Object.DestroyImmediate(derived);
            }
        }

        #endregion

        #region 参数

        /// <summary>参数缓冲按类型给初值，长度与参数表一致。</summary>
        [Test]
        public void 参数缓冲按类型给初值()
        {
            using (var tree = Build())
            {
                var state = StateOf(tree, "WithParameters()");

                Assert.That(state.Reason, Is.Null);
                Assert.That(state.Parameters.Length, Is.EqualTo(4));
                Assert.That(state.Arguments.Length, Is.EqualTo(4));
                Assert.That(state.Arguments[0], Is.EqualTo(0), "int 的初值是 0。");
                Assert.That(state.Arguments[1], Is.False, "bool 的初值是 false。");
                Assert.That(state.Arguments[2], Is.EqualTo(string.Empty), "字符串的初值是空串而不是 null。");
                Assert.That(
                    state.Arguments[3],
                    Is.EqualTo(default(Color)),
                    "结构体取 C# 的 default——Color 的全零是透明黑，不是 Color.black。");
            }
        }

        /// <summary>参数区默认收起——与 Odin 带参按钮的默认形态一致。</summary>
        [Test]
        public void 参数区默认收起()
        {
            using (var tree = Build())
            {
                Assert.That(StateOf(tree, "WithParameters()").Expanded, Is.False);
            }
        }

        /// <summary>不在支持集里的参数类型：给出原因，按钮不静默消失。</summary>
        [Test]
        public void 不支持的类型给出原因()
        {
            using (var tree = Build())
            {
                var state = StateOf(tree, "Unsupported()");

                Assert.That(state.Reason, Is.Not.Null);
                Assert.That(state.Reason, Does.Contain("List"), "原因里该点名是哪个参数、什么类型。");
            }
        }

        /// <summary><c>ref</c>／<c>out</c> 参数给出专门的原因，别混在「类型不支持」里。</summary>
        [Test]
        public void ref参数给出原因()
        {
            using (var tree = Build())
            {
                Assert.That(StateOf(tree, "ByRef()").Reason, Does.Contain("ref"));
            }
        }

        /// <summary>泛型方法无法反射调用，给出原因。</summary>
        [Test]
        public void 泛型方法给出原因()
        {
            using (var tree = Build())
            {
                Assert.That(StateOf(tree, "Generic()").Reason, Does.Contain("泛型"));
            }
        }

        #endregion

        #region 高度

        /// <summary>按钮自己没表态时，按钮组的高度说了算。</summary>
        [Test]
        public void 按钮组高度在按钮没表态时生效()
        {
            using (var tree = Build())
            {
                var node = Find(tree, "InTallGroup()");

                Assert.That(ButtonDrawer.ResolveHeight(node, node.GetAttribute<ButtonAttribute>()),
                    Is.EqualTo(40f));
            }
        }

        /// <summary>按钮自己指定了档位时，组上的高度不生效——「按钮自己说了算」。</summary>
        [Test]
        public void 按钮自己表态时组高度不生效()
        {
            using (var tree = Build())
            {
                var node = Find(tree, "SmallInTallGroup()");

                Assert.That(ButtonDrawer.ResolveHeight(node, node.GetAttribute<ButtonAttribute>()),
                    Is.EqualTo(ButtonSizeMetrics.HeightOf(ButtonSizes.Small)));
            }
        }

        /// <summary>没有所属分组时按自己的档位。</summary>
        [Test]
        public void 无分组时按自己的档位()
        {
            using (var tree = Build())
            {
                var node = Find(tree, "Large()");

                Assert.That(ButtonDrawer.ResolveHeight(node, node.GetAttribute<ButtonAttribute>()),
                    Is.EqualTo(ButtonSizeMetrics.HeightOf(ButtonSizes.Large)));
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

            // 分组里的按钮：分组节点是根的子节点，按钮再往里一层。
            foreach (var child in tree.Root.Children)
            {
                foreach (var grandChild in child.Children)
                {
                    if (grandChild.Path == path)
                    {
                        return grandChild;
                    }
                }
            }

            return null;
        }

        /// <summary>取某个按钮的状态。</summary>
        /// <param name="tree">属性树。</param>
        /// <param name="path">方法节点路径。</param>
        /// <returns>按钮状态。</returns>
        private static ButtonState StateOf(PropertyTree tree, string path)
        {
            var node = Find(tree, path);

            Assert.That(node, Is.Not.Null, $"树里没有 {path}。");
            return node.State.Get<ButtonState>();
        }

        #endregion
    }

    /// <summary>按钮绘制器测试用的资产：每个特性各来一份。</summary>
    internal sealed class ButtonFixture : ScriptableObject
    {
        /// <summary>普通按钮。</summary>
        [Button]
        private void Plain()
        {
        }

        /// <summary>改了名的按钮。</summary>
        [Button("改名了")]
        private void Renamed()
        {
        }

        /// <summary>四个参数，覆盖四种初值形态。</summary>
        [Button]
        private void WithParameters(int count, bool flag, string text, Color tint)
        {
        }

        /// <summary>参数类型不在支持集里。</summary>
        [Button]
        private void Unsupported(List<int> values)
        {
        }

        /// <summary>带 ref 参数。</summary>
        [Button]
        private void ByRef(ref int value)
        {
        }

        /// <summary>泛型方法。</summary>
        [Button]
        private void Generic<T>()
        {
        }

        /// <summary>大号按钮，不在任何分组里。</summary>
        [Button(ButtonSizes.Large)]
        private void Large()
        {
        }

        /// <summary>组里身高 40，按钮自己没表态。</summary>
        [ButtonGroup("高个子", ButtonHeight = 40)]
        [Button]
        private void InTallGroup()
        {
        }

        /// <summary>同一个组，但按钮自己指定了小号。</summary>
        [ButtonGroup("高个子", ButtonHeight = 40)]
        [Button(ButtonSizes.Small)]
        private void SmallInTallGroup()
        {
        }
    }
}
