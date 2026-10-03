using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 窗口基类：惰性建树、重置、以及**成员过滤在窗口上的实际效果**。
    /// <para>
    /// 这里最要紧的是「Unity 那批内部字段一个都没进树」——基类存在的全部意义就在于此。
    /// 绘制本身不测：IMGUI 的渲染结果无法有意义地断言。
    /// </para>
    /// </summary>
    [TestFixture]
    public class XInspectorEditorWindowTests
    {
        #region Private Fields

        private const BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private WindowFixture _window;

        #endregion

        #region Setup / Teardown

        /// <summary>
        /// 直接构造窗口实例。
        /// <para>
        /// 不走 <c>GetWindow</c>：那会真的弹出一个窗口，测试不该有这种副作用。
        /// <see cref="UnityEditor.EditorWindow"/> 是 <see cref="ScriptableObject"/>，
        /// 因此可以这样造出来——用例本身也在验证这一点成立。
        /// </para>
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _window = ScriptableObject.CreateInstance<WindowFixture>();
        }

        /// <summary>销毁窗口并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            if (_window != null)
            {
                Object.DestroyImmediate(_window);
                _window = null;
            }

            DrawerTypeRegistry.Reset();
        }

        #endregion

        #region 惰性

        /// <summary>
        /// 树是惰性的：还没绘制或重置之前为 <c>null</c>。
        /// <para>
        /// 这不是优化。重置内部会临时造一个同类型实例，若 <c>OnEnable</c> 就建树，
        /// 那个一次性实例会白白跑一遍；而且窗口没被显示时本就不该做建树那点工作。
        /// </para>
        /// </summary>
        [Test]
        public void Window_TreeIsLazyUntilFirstUse()
        {
            Assert.That(_window, Is.Not.Null, "窗口实例没造出来，后续用例都无从谈起。");
            Assert.That(_window.ExposedTree, Is.Null, "尚未使用之前不该建树。");
        }

        #endregion

        #region 成员过滤的实际效果

        /// <summary>
        /// 树里只有自己的字段。
        /// </summary>
        [Test]
        public void Window_TreeContainsOwnFields()
        {
            var paths = AttachAndGetPaths();

            Assert.That(paths, Does.Contain("health"));
            Assert.That(paths, Does.Contain("displayName"));
        }

        /// <summary>
        /// **本基类存在的全部意义**：<see cref="UnityEditor.EditorWindow"/> 的内部序列化字段
        /// 一个都不进树。
        /// <para>
        /// 用「枚举它所有带 <c>[SerializeField]</c> 的字段」而不是写死名字——Unity 增删这些字段时
        /// 用例不失效，保护也更强。配了负向控制：一个都没枚举到就判失败，否则反射标志写错时
        /// 用例会静默通过，而恒真的守卫等于没有守卫。
        /// </para>
        /// </summary>
        [Test]
        public void Window_TreeExcludesEveryEditorWindowInternalField()
        {
            var paths = AttachAndGetPaths();
            var checkedCount = 0;

            foreach (var field in typeof(UnityEditor.EditorWindow).GetFields(InstanceFields))
            {
                if (!HasSerializeField(field))
                {
                    continue;
                }

                checkedCount++;
                Assert.That(paths, Does.Not.Contain(field.Name),
                    $"{nameof(UnityEditor.EditorWindow)}.{field.Name} 是 Unity 的内部字段，不该出现在窗口里。");
            }

            Assert.That(checkedCount, Is.GreaterThan(0),
                "一个带 [SerializeField] 的 EditorWindow 字段都没枚举到，说明这条用例本身失效了。");
        }

        /// <summary>
        /// 脚本槽位也不进树——它声明在 <see cref="ScriptableObject"/> 上，被同一条规则排除。
        /// </summary>
        [Test]
        public void Window_TreeExcludesScriptBinding()
        {
            Assert.That(AttachAndGetPaths(), Does.Not.Contain("m_Script"));
        }

        #endregion

        #region 重置

        /// <summary>
        /// 重置把字段恢复成 C# 初始值。
        /// </summary>
        [Test]
        public void Window_ResetToDefaultsRestoresInitialValues()
        {
            SetHealth(999);

            Assert.That(_window.ResetToDefaults(), Is.True);
            Assert.That(_window.health, Is.EqualTo(WindowFixture.DefaultHealth));
        }

        /// <summary>
        /// 重置会顺带把树建起来——它需要树来知道有哪些成员。
        /// </summary>
        [Test]
        public void Window_ResetToDefaultsAttachesTree()
        {
            Assert.That(_window.ExposedTree, Is.Null);

            _window.ResetToDefaults();

            Assert.That(_window.ExposedTree, Is.Not.Null);
        }

        /// <summary>
        /// 分组内的字段同样被重置覆盖。
        /// </summary>
        [Test]
        public void Window_ResetToDefaultsCoversGroupedMembers()
        {
            SetString("displayName", "改过了");

            _window.ResetToDefaults();

            Assert.That(_window.displayName, Is.EqualTo(WindowFixture.DefaultName));
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 让窗口建起树。
        /// </summary>
        /// <returns>属性树。</returns>
        /// <remarks>
        /// 借重置来触发——它内部会先附加再重置，是最省事的「首次使用」途径，而且它本身是公开行为，
        /// 不依赖测试专用的后门。
        /// </remarks>
        private PropertyTree AttachTree()
        {
            _window.ResetToDefaults();
            return _window.ExposedTree;
        }

        /// <summary>取树里所有成员的路径。</summary>
        /// <returns>路径列表。</returns>
        private List<string> AttachAndGetPaths()
        {
            var tree = AttachTree();
            Assert.That(tree, Is.Not.Null, "树没建起来，用例前提不成立。");

            return PropertyTreeReset.CollectMemberPaths(tree);
        }

        /// <summary>经序列化对象改写窗口上的整数字段。</summary>
        /// <param name="value">新值。</param>
        private void SetHealth(int value)
        {
            var serializedObject = new SerializedObject(_window);
            serializedObject.FindProperty("health").intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>经序列化对象改写窗口上的字符串字段。</summary>
        /// <param name="propertyPath">属性路径。</param>
        /// <param name="value">新值。</param>
        private void SetString(string propertyPath, string value)
        {
            var serializedObject = new SerializedObject(_window);
            serializedObject.FindProperty(propertyPath).stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>判断字段是否带 <c>[SerializeField]</c>。</summary>
        /// <param name="field">字段。</param>
        /// <returns>带该特性返回 <c>true</c>。</returns>
        /// <remarks>
        /// 按类型名比较而非 <c>is SerializeField</c>：<c>SerializeField</c> 是 internal 的，
        /// 拿不到它的类型。
        /// </remarks>
        private static bool HasSerializeField(FieldInfo field)
        {
            foreach (var attribute in field.GetCustomAttributes(false))
            {
                if (attribute.GetType().Name == "SerializeField")
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// 窗口测试用的具体窗口。
    /// </summary>
    internal sealed class WindowFixture : XInspectorEditorWindow
    {
        /// <summary>默认整数值。</summary>
        public const int DefaultHealth = 100;

        /// <summary>默认字符串值。</summary>
        public const string DefaultName = "Player";

        /// <summary>带标题且属于分组的字段。</summary>
        [Title("设置")]
        [BoxGroup("基础")]
        public int health = DefaultHealth;

        /// <summary>同组的字段。</summary>
        [BoxGroup("基础")]
        public string displayName = DefaultName;

        /// <summary>
        /// 把受保护的树暴露给测试。
        /// </summary>
        /// <remarks>
        /// <c>Tree</c> 是 <c>protected</c>，测试从外部读不到。用一个只读出口暴露它，
        /// 比把成员改成 public 或给测试开 InternalsVisibleTo 更干净——后者需要动产品代码的可见性。
        /// </remarks>
        public PropertyTree ExposedTree => Tree;
    }
}
