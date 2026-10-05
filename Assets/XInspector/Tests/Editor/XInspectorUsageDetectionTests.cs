using System;
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

        /// <summary>
        /// **只挂 <c>[Button]</c> 方法的**类型必须被判为「用到了本插件」。
        /// <para>
        /// 这是本轮最危险的静默失败：判据此前只扫字段与类级特性，漏掉方法。
        /// 后果不是「少画了点东西」，而是类型不被自动接管、按钮完全不出现、且一条告警都没有。
        /// </para>
        /// </summary>
        [Test]
        public void IsUsedBy_只挂按钮方法的类型为真()
        {
            Assert.That(
                XInspectorUsageDetection.IsUsedBy(typeof(ButtonMethodOnlyFixture)),
                Is.True,
                "按钮特性只标在方法上；判据不扫方法，这类类型就永远不会被接管。");
        }

        /// <summary>基类方法上的按钮也让派生类型算数——同样是逐层上溯。</summary>
        [Test]
        public void IsUsedBy_基类方法上的按钮也算()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(ButtonInBaseFixture)), Is.True);
        }

        /// <summary>
        /// **只挂 <c>[ShowInInspector]</c> 属性的**类型必须被判为「用到了本插件」。
        /// <para>
        /// 与按钮那次是同一类漏洞：判据此前只扫字段与方法，而
        /// <c>[ShowInInspector]</c> 的主战场恰恰是普通属性——<c>GetFields</c> 看不见它们。
        /// 后果同样是类型不被接管、特性一次都不生效、且没有任何告警。
        /// </para>
        /// </summary>
        [Test]
        public void IsUsedBy_只挂反射属性的类型为真()
        {
            Assert.That(
                XInspectorUsageDetection.IsUsedBy(typeof(ShowInInspectorPropertyFixture)),
                Is.True,
                "反射属性只标在属性上；判据不扫属性，这类类型就永远不会被接管。");
        }

        /// <summary>只有 <c>[ShowInInspector]</c> 私有字段的类型同样为真。</summary>
        [Test]
        public void IsUsedBy_只挂反射字段的类型为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(ShowInInspectorFieldFixture)), Is.True);
        }

        /// <summary>静态成员上的 <c>[ShowInInspector]</c> 也算。</summary>
        [Test]
        public void IsUsedBy_静态反射成员也算()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(ShowInInspectorStaticFixture)), Is.True);
        }

        /// <summary>
        /// **只挂 <c>[PropertyOrder]</c> 的**类型必须被判为「用到了本插件」。
        /// <para>
        /// 它既没有绘制器也没有处理器——判据靠 <c>ITreeOrderingAttribute</c> 这个标记认出它。
        /// 漏掉的后果与条件族、生命周期钩子当年一样：类型不被接管，排序**静默不生效**。
        /// </para>
        /// </summary>
        [Test]
        public void IsUsedBy_只挂排序特性的类型为真()
        {
            Assert.That(
                XInspectorUsageDetection.IsUsedBy(typeof(PropertyOrderOnlyFixture)),
                Is.True,
                "排序特性由构建期直接消费，两张注册表都查不到它。");
        }

        /// <summary>
        /// **内联标注在字段的声明类型上**的类型必须被判为「用到了本插件」。
        /// <para>
        /// 类级 <c>[InlineProperty]</c> 标在字段的**声明类型**上，成员自身看不到它——
        /// 判据不看这一处，类型就不被接管，内联**静默失效**。
        /// </para>
        /// </summary>
        [Test]
        public void IsUsedBy_内联标注在字段类型上为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(ClassLevelInlineUsageFixture)), Is.True);
        }

        /// <summary>控制项：**数组元素类型**上的内联标注不算——数组本来就不内联（L6）。</summary>
        [Test]
        public void IsUsedBy_数组元素类型的内联标注不算()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(InlineArrayUsageFixture)), Is.False);
        }

        /// <summary>
        /// 控制项：**嵌套类型上挂着别的本包特性**不算——那些特性在嵌套层是惰性的
        /// （嵌套字段不进管线），算进来就是「过度接管」，与漏接管同属静默。
        /// </summary>
        [Test]
        public void IsUsedBy_嵌套类型上的其它支持特性不算()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(NestedOtherAttributeUsageFixture)), Is.False);
        }

        /// <summary>
        /// 控制项：属性上挂着一个**别人家的**特性时为假。
        /// <para>
        /// 没有这一条，上面三条无法区分「认得 <c>[ShowInInspector]</c>」与
        /// 「见到属性就返回真」。
        /// </para>
        /// </summary>
        [Test]
        public void IsUsedBy_属性上挂无关特性为假()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(UnrelatedPropertyAttributeFixture)), Is.False);
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
        /// 预制体上下文四个条件同样是处理器专有——判据漏了它们，带这些特性的类型就不会被
        /// 自动接管，症状是「特性静默不生效、零告警」。
        /// </summary>
        [Test]
        public void HasProcessorForAttribute_预制体条件族为真()
        {
            Assert.That(AttributeProcessorRegistry.HasProcessorForAttribute(typeof(ShowInAttribute)), Is.True);
            Assert.That(AttributeProcessorRegistry.HasProcessorForAttribute(typeof(HideInAttribute)), Is.True);
            Assert.That(AttributeProcessorRegistry.HasProcessorForAttribute(typeof(EnableInAttribute)), Is.True);
            Assert.That(AttributeProcessorRegistry.HasProcessorForAttribute(typeof(DisableInAttribute)), Is.True);
        }

        /// <summary>只挂预制体上下文特性的类型必须被判为「用到了本插件」。</summary>
        [Test]
        public void IsUsedBy_只挂预制体上下文特性的类型为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(PrefabConditionOnlyFixture)), Is.True);
        }

        /// <summary>
        /// 分组条件族同样是处理器专有（没有绘制器）——判据漏了它们，只挂
        /// <c>[ShowIfGroup]</c> 的类型就不会被自动接管，症状一如既往：**静默失效、零告警**。
        /// </summary>
        [Test]
        public void HasProcessorForAttribute_分组条件族为真()
        {
            Assert.That(
                AttributeProcessorRegistry.HasProcessorForAttribute(typeof(ShowIfGroupAttribute)), Is.True);
            Assert.That(
                AttributeProcessorRegistry.HasProcessorForAttribute(typeof(HideIfGroupAttribute)), Is.True);
        }

        /// <summary>只挂分组条件特性的类型必须被判为「用到了本插件」。</summary>
        [Test]
        public void IsUsedBy_只挂分组条件特性的类型为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(GroupConditionOnlyFixture)), Is.True);
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
        /// 类级分组分发不对应任何**单一**特性类型，因此不认领 `[BoxGroup]` 这类分组特性——
        /// 它处理的是「父节点上第一份分组特性」，判据那一半由绘制器覆盖。
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

    /// <summary>只挂排序特性的资产。</summary>
    internal sealed class PropertyOrderOnlyFixture : ScriptableObject
    {
        /// <summary>只标了顺序——它既没有绘制器也没有处理器。</summary>
        [PropertyOrder(-1f)]
        public int value = 1;
    }

    /// <summary>内联标注在**字段的声明类型**上的资产。</summary>
    internal sealed class ClassLevelInlineUsageFixture : ScriptableObject
    {
        /// <summary>声明类型带 <c>[InlineProperty]</c>——成员自身看不到它。</summary>
        public InlineMarkedType value;
    }

    /// <summary>内联只标在**数组元素类型**上的资产。</summary>
    internal sealed class InlineArrayUsageFixture : ScriptableObject
    {
        /// <summary>数组元素类型有标记，数组本身没有——判据刻意不认这一处。</summary>
        public InlineMarkedType[] items;
    }

    /// <summary>嵌套类型上挂着别的本包特性的资产。</summary>
    internal sealed class NestedOtherAttributeUsageFixture : ScriptableObject
    {
        /// <summary>嵌套类型上有 <c>[Title]</c>——它在嵌套层进不了管线。</summary>
        public TitledNestedType value;
    }

    /// <summary>挂着 <c>[Title]</c> 的嵌套类型（标题只对被检视类型生效）。</summary>
    [Serializable]
    [Title("嵌套层用不到")]
    internal sealed class TitledNestedType
    {
        /// <summary>子字段。</summary>
        public int a = 1;
    }

    /// <summary>只挂分组条件特性（同样是处理器专有）的资产。</summary>
    internal sealed class GroupConditionOnlyFixture : ScriptableObject
    {
        /// <summary>条件开关。</summary>
        public bool toggle = true;

        /// <summary>分组条件没有绘制器——判据必须靠处理器那一半把它认出来。</summary>
        [ShowIfGroup("条件组", Condition = nameof(toggle))]
        public int value = 1;
    }

    /// <summary>只挂内嵌环境特性（也是处理器专有）的资产。</summary>
    internal sealed class InlineEditorConditionOnlyFixture : ScriptableObject
    {
        /// <summary>只有内嵌环境特性，没有绘制器。</summary>
        [HideInInlineEditors]
        public int value = 1;
    }

    /// <summary>只挂预制体上下文特性（同样是处理器专有）的资产。</summary>
    internal sealed class PrefabConditionOnlyFixture : ScriptableObject
    {
        /// <summary>只有预制体上下文特性，没有绘制器。</summary>
        [DisableIn(PrefabKind.PrefabAsset)]
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

    /// <summary>只有方法上挂着按钮特性的资产——一个字段都不带特性。</summary>
    internal sealed class ButtonMethodOnlyFixture : ScriptableObject
    {
        /// <summary>没有特性的普通字段。</summary>
        public int value = 1;

        /// <summary>唯一的用法。</summary>
        [Button]
        private void DoThing()
        {
        }
    }

    /// <summary>按钮方法声明在基类上的资产。</summary>
    internal class ButtonBaseFixture : ScriptableObject
    {
        /// <summary>挂在基类方法上的按钮。</summary>
        [Button]
        public void DoThing()
        {
        }
    }

    /// <summary>派生类型自己一个特性都不带。</summary>
    internal sealed class ButtonInBaseFixture : ButtonBaseFixture
    {
        /// <summary>普通字段。</summary>
        public int value = 1;
    }

    /// <summary>只在**属性**上挂着 <c>[ShowInInspector]</c> 的资产。</summary>
    internal sealed class ShowInInspectorPropertyFixture : ScriptableObject
    {
        /// <summary>没有特性的普通字段。</summary>
        public int value = 1;

        /// <summary>唯一的用法。</summary>
        [ShowInInspector]
        public int Reflected => value * 2;
    }

    /// <summary>只在私有非序列化字段上挂着 <c>[ShowInInspector]</c> 的资产。</summary>
    internal sealed class ShowInInspectorFieldFixture : ScriptableObject
    {
        /// <summary>唯一的用法——这个字段 Unity 不会序列化。</summary>
        [ShowInInspector]
        private int _reflected = 1;

        /// <summary>读一下私有字段，避免 CS0414 告警。</summary>
        public int Value => _reflected;
    }

    /// <summary>只在**静态**成员上挂着 <c>[ShowInInspector]</c> 的资产。</summary>
    internal sealed class ShowInInspectorStaticFixture : ScriptableObject
    {
        /// <summary>唯一的用法。</summary>
        [ShowInInspector]
        public static int Reflected = 1;
    }

    /// <summary>属性上挂着一个别人家的特性的资产——不该被判为用到了本插件。</summary>
    internal sealed class UnrelatedPropertyAttributeFixture : ScriptableObject
    {
        /// <summary>无关属性。</summary>
        [System.Obsolete("只是为了让属性身上有点东西")]
        public int value => 1;
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
