using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 嵌套层的**读路径**：条件族指向同一嵌套实例上的反射成员与方法。
    /// <para>
    /// 夹具的关键在于**根上放着同名且值相反的陷阱**：不这么放假的话，「解析到了根上」与
    /// 「解析到了嵌套实例」在某些取值下结果相同，这条就测不出「条件看错了对象」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class NestedReflectionTests
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

        #region 嵌套层的反射条件

        /// <summary>嵌套层的条件指向**非序列化属性**——反射那一级，且找的是同一嵌套实例。</summary>
        [Test]
        public void 嵌套层条件指向非序列化属性()
        {
            var target = ScriptableObject.CreateInstance<NestedReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "nested.gatedByProperty");

                SetInt(target, tree, "nested.value", 1);
                Assert.That(gated.IsVisible, Is.True, "嵌套实例的 Enabled 为真。");

                SetInt(target, tree, "nested.value", 0);
                Assert.That(
                    gated.IsVisible,
                    Is.False,
                    "根上的同名属性恒为真——它若被解析到，这里就会显示。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>嵌套层的条件指向**无参返回 bool 的方法**。</summary>
        [Test]
        public void 嵌套层条件指向无参方法()
        {
            var target = ScriptableObject.CreateInstance<NestedReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "nested.gatedByMethod");

                SetInt(target, tree, "nested.value", 1);
                Assert.That(gated.IsVisible, Is.True);

                SetInt(target, tree, "nested.value", 0);
                Assert.That(gated.IsVisible, Is.False, "根上的同名方法恒为真，不该被解析到。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 换掉整个嵌套实例之后条件**跟着走**——链每帧现读，不是绑死某个实例。
        /// </summary>
        /// <remarks>
        /// 这条钉住的是「绑实例」那条被否决的方案：绑定的实例在父字段被重新赋值
        /// （<c>nested = new …</c>、Undo、预制体 revert）之后就是旧对象，条件会**静默地陈旧**。
        /// </remarks>
        [Test]
        public void 嵌套实例换掉之后条件跟着走()
        {
            var target = ScriptableObject.CreateInstance<NestedReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "nested.gatedByProperty");

                Assert.That(gated.IsVisible, Is.True, "初始值让条件为真。");

                target.nested = new NestedReflected { value = 0 };
                Assert.That(gated.IsVisible, Is.False, "换掉整个嵌套实例之后条件应当跟着变。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>嵌套实例为空时条件算假，且**不抛**。</summary>
        [Test]
        public void 嵌套实例为空时条件算假()
        {
            var target = ScriptableObject.CreateInstance<NestedReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "nested.gatedByProperty");

                Assert.DoesNotThrow(() =>
                {
                    target.nested = null;
                    Assert.That(gated.IsVisible, Is.False, "没有实例就没有值可言，条件算假。");
                });
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>嵌套层里**序列化的兄弟成员**仍然优先于反射那两级。</summary>
        [Test]
        public void 序列化兄弟成员优先于反射()
        {
            var target = ScriptableObject.CreateInstance<NestedReflectionFixture>();
            try
            {
                var tree = BuildTree(target);
                var gated = Find(tree.Root, "nested.gatedBySibling");

                // 夹具里同层的 serialized 为真、value 为 0（即属性 Enabled 为假）。
                Assert.That(gated.IsVisible, Is.True, "序列化那一级先命中，不该轮到反射。");

                SetBool(target, tree, "nested.serialized", false);
                Assert.That(gated.IsVisible, Is.False);
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
        private static InspectorProperty Find(InspectorProperty root, string path)
        {
            var found = Search(root, path);

            Assert.That(found, Is.Not.Null, $"找不到节点 {path}。");
            return found;
        }

        /// <summary>递归搜索（找不到返回 <c>null</c>，由 <see cref="Find"/> 负责断言）。</summary>
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

    /// <summary>嵌套层里的反射条件：属性与无参方法各一，外加一个序列化的同层开关。</summary>
    [Serializable]
    internal class NestedReflected
    {
        /// <summary>条件的取值来源。</summary>
        public int value = 1;

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

    /// <summary>嵌套层反射条件的对照资产。</summary>
    [HideMonoScript]
    internal sealed class NestedReflectionFixture : ScriptableObject
    {
        /// <summary>陷阱：根上的同名属性。它恒为真——旧行为会解析到它。</summary>
        public bool Enabled => true;

        /// <summary>陷阱：根上的同名方法。同样恒为真。</summary>
        public bool Ready() => true;

        /// <summary>嵌套层。</summary>
        public NestedReflected nested = new NestedReflected();
    }
}
