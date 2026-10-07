using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 多态段的**读路径**：条件族指向同一具体实例上的反射成员与方法。
    /// <para>
    /// 夹具的关键与嵌套层 / 元素层同款：**根上放着同名且值相反的陷阱**——不这么放假的话，
    /// 「解析到了根上」与「解析到了多态实例」在某些取值下结果相同，就测不出「条件看错了对象」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PolymorphicReflectionTests
    {
        #region Fixture

        /// <summary>复位静态门面：建树会初始化绘制器与处理器两张注册表。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 多态段的反射条件

        /// <summary>条件指向**多态实例**的非序列化属性——根上的同名属性恒为真，作陷阱。</summary>
        [Test]
        public void 多态段里的条件指向实例上的属性()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "shape.gatedByProperty");

                Assert.That(gated.IsVisible, Is.True, "起点：value > 0。");

                SetInt(target, tree, "shape.value", 0);

                Assert.That(
                    gated.IsVisible,
                    Is.False,
                    "根上的同名属性恒为真——它若被解析到，这里就会一直显示。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>条件指向**多态实例**的无参返回 bool 的方法。</summary>
        [Test]
        public void 多态段里的条件指向实例上的无参方法()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "shape.gatedByMethod");

                Assert.That(gated.IsVisible, Is.True);

                SetInt(target, tree, "shape.value", 0);

                Assert.That(gated.IsVisible, Is.False, "根上的同名方法恒为真，不该被解析到。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>控制项：同层的**序列化**成员仍优先于反射那一级（这条今天就不许红）。</summary>
        [Test]
        public void 多态段里的条件先找同层序列化成员()
        {
            var target = ScriptableObject.CreateInstance<PolymorphicReflectionFixture>();
            target.shape = new PolyReflected();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "shape.gatedBySibling");

                Assert.That(gated.IsVisible, Is.True);

                SetBool(target, tree, "shape.serialized", false);

                Assert.That(gated.IsVisible, Is.False, "同层的序列化开关才是条件指的那个。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>构建被测的树。</summary>
        /// <param name="target">目标资产。</param>
        /// <returns>属性树。</returns>
        private static PropertyTree BuildTree(ScriptableObject target)
        {
            return PropertyTree.Create(new SerializedObject(target));
        }

        /// <summary>改一个 int 字段的值，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树——条件求值器读的是**树自己的**序列化对象。</param>
        /// <param name="path">序列化路径（可点分）。</param>
        /// <param name="value">新值。</param>
        private static void SetInt(ScriptableObject target, PropertyTree tree, string path, int value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        /// <summary>改一个 bool 字段的值，并让树看到它。</summary>
        /// <param name="target">目标资产。</param>
        /// <param name="tree">属性树。</param>
        /// <param name="path">序列化路径。</param>
        /// <param name="value">新值。</param>
        private static void SetBool(ScriptableObject target, PropertyTree tree, string path, bool value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(path).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            tree.SerializedObject.Update();
        }

        /// <summary>递归查找节点（按完整路径）。</summary>
        /// <param name="root">搜索起点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；不存在时断言失败。</returns>
        /// <remarks>多态段里的节点夹在容器/分组节点之下，直接按路径深度优先找最省事。</remarks>
        private static InspectorProperty Find(InspectorProperty root, string path)
        {
            var found = Search(root, path);

            Assert.That(found, Is.Not.Null, $"找不到节点 {path}。");
            return found;
        }

        /// <summary>深度优先搜索（找不到返回 <c>null</c>，由 <see cref="Find"/> 负责断言）。</summary>
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

        #endregion
    }

    /// <summary>多态段读路径的槽位类型——声明成接口是刻意的（多态引用的主战场）。</summary>
    internal interface IPolyShape
    {
    }

    /// <summary>
    /// 用得到本包的具体类型：反射条件（属性 / 方法 / 同层序列化各一）与读路径成员都在里面。
    /// </summary>
    [Serializable]
    internal class PolyReflected : IPolyShape
    {
        /// <summary>条件的取值来源，也是按钮要改的那个字段。</summary>
        public int value = 2;

        /// <summary>序列化的同层开关——序列化那一级应当先命中。</summary>
        public bool serialized = true;

        /// <summary>非序列化属性：只有反射那一级看得到它。</summary>
        public bool Enabled => value > 0;

        /// <summary>无参返回 bool 的方法。</summary>
        public bool Ready() => value > 0;

        /// <summary>条件指向**非序列化属性**。</summary>
        [ShowIf(nameof(Enabled))]
        public int gatedByProperty;

        /// <summary>条件指向**无参方法**。</summary>
        [ShowIf(nameof(Ready))]
        public int gatedByMethod;

        /// <summary>条件指向同层的**序列化**成员。</summary>
        [ShowIf(nameof(serialized))]
        public int gatedBySibling;
    }

    /// <summary>多态段读路径的对照资产：陷阱（同名且效果相反）全放在**根**上。</summary>
    [HideMonoScript]
    internal sealed class PolymorphicReflectionFixture : ScriptableObject
    {
        /// <summary>陷阱：根上的同名属性。它恒为真——解析到根上就会「永远显示」。</summary>
        public bool Enabled => true;

        /// <summary>陷阱：根上的同名方法。同样恒为真。</summary>
        public bool Ready() => true;

        /// <summary>多态段：条件与读路径成员都在里面。</summary>
        [SerializeReference]
        public IPolyShape shape;
    }
}
