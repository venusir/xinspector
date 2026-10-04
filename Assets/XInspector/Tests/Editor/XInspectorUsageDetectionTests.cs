using NUnit.Framework;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 自动接管的判据：类型有没有用到本插件。
    /// <para>
    /// 判据问的是「画得出来吗」——而**处理器也能处理特性**（条件族就是处理器专有、
    /// 没有绘制器）。只查绘制器会让「只用了条件族的类型」不被接管，
    /// 而特性也就一次都不会生效：**症状是「特性静默失效」**，最难归因的一类现象。
    /// 本 fixture 就是这条的回归守卫。
    /// </para>
    /// <para>
    /// 判据类住在核心 Editor 程序集（<see cref="XInspectorUsageDetection"/>），
    /// 正是为了这里能直接断言——测试程序集引用不了宏门控的自动接管程序集。
    /// </para>
    /// </summary>
    [TestFixture]
    public class XInspectorUsageDetectionTests
    {
        #region Teardown

        /// <summary>复位静态门面：判据会碰绘制器与处理器两张注册表。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 判据

        /// <summary>
        /// **只挂条件特性**的类型必须被判为「用到了本插件」。
        /// <para>
        /// 条件族没有绘制器，只有处理器。这条曾经是红的——判据只查绘制器，
        /// 于是这类类型不被接管、条件静默失效。
        /// </para>
        /// </summary>
        [Test]
        public void IsUsedBy_只挂条件特性的类型为真()
        {
            Assert.That(
                XInspectorUsageDetection.IsUsedBy(typeof(ConditionOnlyFixture)),
                Is.True,
                "条件族只有处理器没有绘制器；判据若只查绘制器，这类类型就永远不会被接管。");
        }

        /// <summary>什么都不挂的类型不得被判为「用到了本插件」——否则每个类型都会被经手一遍。</summary>
        [Test]
        public void IsUsedBy_无特性类型为假()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(NoAttributeFixture)), Is.False);
        }

        /// <summary>只挂绘制器特性的类型照旧为真。</summary>
        [Test]
        public void IsUsedBy_只挂绘制器特性的类型为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(DrawerOnlyFixture)), Is.True);
        }

        /// <summary>类级分组特性也是「用到了本插件」。</summary>
        [Test]
        public void IsUsedBy_类级分组为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(UsageClassGroupFixture)), Is.True);
        }

        /// <summary>私有字段上的特性同样算数——扫描须逐层 DeclaredOnly 上溯。</summary>
        [Test]
        public void IsUsedBy_私有字段上的特性也算()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(PrivateFieldFixture)), Is.True);
        }

        /// <summary>传 null 不得抛异常。</summary>
        [Test]
        public void IsUsedBy_Null为假()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(null), Is.False);
        }

        #endregion

        #region 处理器注册表

        /// <summary>
        /// 条件族有处理器，注册表必须认得出来——判据的另一半就架在它上面。
        /// </summary>
        [Test]
        public void HasProcessorForAttribute_条件族为真()
        {
            Assert.That(
                AttributeProcessorRegistry.HasProcessorForAttribute(typeof(ShowIfAttribute)),
                Is.True,
                "条件族是处理器专有的；这一半查不出来，判据就还是只认绘制器。");
        }

        /// <summary>
        /// 内嵌环境三兄弟同样是处理器专有——它们也是「只挂处理器特性」的那一类，
        /// 同样要能被判据看见，否则带它们的类型不会被自动接管。
        /// </summary>
        [Test]
        public void HasProcessorForAttribute_内嵌环境族为真()
        {
            Assert.That(
                AttributeProcessorRegistry.HasProcessorForAttribute(typeof(ShowInInlineEditorsAttribute)),
                Is.True);
            Assert.That(
                AttributeProcessorRegistry.HasProcessorForAttribute(typeof(HideInInlineEditorsAttribute)),
                Is.True);
            Assert.That(
                AttributeProcessorRegistry.HasProcessorForAttribute(typeof(DisableInInlineEditorsAttribute)),
                Is.True);
        }

        /// <summary>只挂内嵌环境特性的类型必须被判为「用到了本插件」。</summary>
        [Test]
        public void IsUsedBy_只挂内嵌环境特性的类型为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(InlineEditorConditionOnlyFixture)), Is.True);
        }

        /// <summary>
        /// 只有绘制器的特性不该被处理器注册表认领——两张表的回答各管各的。
        /// </summary>
        [Test]
        public void HasProcessorForAttribute_只有绘制器的特性为假()
        {
            Assert.That(AttributeProcessorRegistry.HasProcessorForAttribute(typeof(TitleAttribute)), Is.False);
        }

        /// <summary>
        /// 非泛型处理器（如类级分组分发）不暴露所处理的特性类型，因此查不到——
        /// 这是刻意的：它不对应任何**单一**特性类型，判据那一半由绘制器覆盖。
        /// </summary>
        [Test]
        public void HasProcessorForAttribute_非泛型处理器不认领特性()
        {
            Assert.That(AttributeProcessorRegistry.HasProcessorForAttribute(typeof(BoxGroupAttribute)), Is.False);
        }

        /// <summary>传 null 不得抛异常。</summary>
        [Test]
        public void HasProcessorForAttribute_Null为假()
        {
            Assert.That(AttributeProcessorRegistry.HasProcessorForAttribute(null), Is.False);
        }

        #endregion
    }

    /// <summary>只挂条件特性（处理器专有）的资产。</summary>
    internal sealed class ConditionOnlyFixture : ScriptableObject
    {
        /// <summary>条件成员。</summary>
        public bool flag = true;

        /// <summary>只有条件特性，没有绘制器。</summary>
        [ShowIf(nameof(flag))]
        public int value = 1;
    }

    /// <summary>只挂内嵌环境特性（也是处理器专有）的资产。</summary>
    internal sealed class InlineEditorConditionOnlyFixture : ScriptableObject
    {
        /// <summary>只有内嵌环境特性，没有绘制器。</summary>
        [HideInInlineEditors]
        public int value = 1;
    }

    /// <summary>什么都不挂的资产。</summary>
    internal sealed class NoAttributeFixture : ScriptableObject
    {
        /// <summary>普通字段。</summary>
        public int value = 1;
    }

    /// <summary>只挂绘制器特性的资产。</summary>
    internal sealed class DrawerOnlyFixture : ScriptableObject
    {
        /// <summary>有绘制器的特性。</summary>
        [Title("有绘制器")]
        public int value = 1;
    }

    /// <summary>类上挂分组特性的资产。</summary>
    [BoxGroup("整块")]
    internal sealed class UsageClassGroupFixture : ScriptableObject
    {
        /// <summary>普通字段。</summary>
        public int value = 1;
    }

    /// <summary>特性挂在私有字段上的资产。</summary>
    internal sealed class PrivateFieldFixture : ScriptableObject
    {
        /// <summary>私有字段上的绘制器特性，须能被逐层上溯扫到。</summary>
        [Title("私有字段")]
        [SerializeField]
        private int _value = 1;

        /// <summary>读一下私有字段，避免 CS0414 告警。</summary>
        public int Value => _value;
    }
}
