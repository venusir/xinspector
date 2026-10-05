using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 两条与集合相邻的守卫：<c>[RequiredListLength]</c> 的纯校验/文案/链位置，
    /// 以及 <c>[ValueDropdown]</c> 的目标判定（数组目标要挡住）。
    /// </summary>
    [TestFixture]
    public class CollectionValidationTests
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

        #region 长度校验

        /// <summary>纯函数：范围内通过，两侧越界都拦，只给一侧时另一侧不限。</summary>
        [Test]
        public void 长度校验()
        {
            Assert.That(ListLengthValidator.IsSatisfied(3, 3, 3), Is.True);
            Assert.That(ListLengthValidator.IsSatisfied(2, 3, 3), Is.False, "少于下限。");
            Assert.That(ListLengthValidator.IsSatisfied(4, 3, 3), Is.False, "多于上限。");
            Assert.That(ListLengthValidator.IsSatisfied(0, 1, null), Is.False);
            Assert.That(ListLengthValidator.IsSatisfied(99, 1, null), Is.True, "只给下限时上限不限。");
            Assert.That(ListLengthValidator.IsSatisfied(0, null, 8), Is.True, "只给上限时下限不限。");
            Assert.That(ListLengthValidator.IsSatisfied(9, null, 8), Is.False);
        }

        /// <summary>默认文案说清要求与现状；给了自定义文本就用它。</summary>
        [Test]
        public void 提示文案()
        {
            Assert.That(
                ListLengthValidator.Describe(2, new RequiredListLengthAttribute(3)),
                Is.EqualTo("此列表需要恰好 3 项（当前 2 项）。"));
            Assert.That(
                ListLengthValidator.Describe(2, new RequiredListLengthAttribute(1, 8)),
                Is.EqualTo("此列表需要 1–8 项（当前 2 项）。"));
            Assert.That(
                ListLengthValidator.Describe(0, new RequiredListLengthAttribute(1, null)),
                Is.EqualTo("此列表至少需要 1 项（当前 0 项）。"));
            Assert.That(
                ListLengthValidator.Describe(9, new RequiredListLengthAttribute(null, 8)),
                Is.EqualTo("此列表最多 8 项（当前 9 项）。"));
            Assert.That(
                ListLengthValidator.Describe(2, new RequiredListLengthAttribute(3) { ErrorMessage = "队伍必须三个人。" }),
                Is.EqualTo("队伍必须三个人。"));
        }

        /// <summary>校验绘制器在链上（校验档），且不吞掉后面的绘制。</summary>
        [Test]
        public void 校验绘制器在链上()
        {
            var target = ScriptableObject.CreateInstance<CollectionValidationFixture>();
            try
            {
                var tree = PropertyTree.Create(new SerializedObject(target));
                var node = Find(tree.Root, "team");
                var index = IndexOf<RequiredListLengthDrawer>(node);

                Assert.That(index, Is.GreaterThanOrEqualTo(0));
                Assert.That(index, Is.LessThan(node.Chain.Count - 1), "末端仍是最后一格。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 下拉的目标判定

        /// <summary>
        /// 单值与字符串放行（字符串在若干语境下被 Unity 算作数组，而它恰恰是常用目标）；
        /// 数组与反射成员挡住。
        /// </summary>
        [Test]
        public void 下拉目标判定()
        {
            var target = ScriptableObject.CreateInstance<CollectionValidationFixture>();
            try
            {
                var serializedObject = new SerializedObject(target);

                Assert.That(ValueDropdownTarget.IsSupported(serializedObject.FindProperty("single")), Is.True);
                Assert.That(
                    ValueDropdownTarget.IsSupported(serializedObject.FindProperty("text")),
                    Is.True,
                    "字符串是常用的目标形态，要放行。");
                Assert.That(
                    ValueDropdownTarget.IsSupported(serializedObject.FindProperty("many")),
                    Is.False,
                    "数组目标按元素画属集合自绘那一层，这里挡住并告警。");
                Assert.That(ValueDropdownTarget.IsSupported(null), Is.False, "反射成员没有序列化后端。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>按路径查找直接子节点。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；不存在时断言失败。</returns>
        private static InspectorProperty Find(InspectorProperty parent, string path)
        {
            foreach (var child in parent.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            Assert.Fail($"找不到节点 {path}。");
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

    /// <summary>长度校验与下拉目标判定的对照资产。</summary>
    [HideMonoScript]
    internal sealed class CollectionValidationFixture : ScriptableObject
    {
        /// <summary>标了长度校验的列表（长度不足时会画提示）。</summary>
        [RequiredListLength(3)]
        public string[] team = { "a" };

        /// <summary>单值目标——下拉可挂。</summary>
        [ValueDropdown(nameof(team))]
        public string single = "a";

        /// <summary>字符串目标——同样放行。</summary>
        public string text = "文案";

        /// <summary>数组目标——下拉要挡住。</summary>
        [ValueDropdown(nameof(team))]
        public string[] many = { "a" };
    }
}
