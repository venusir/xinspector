using System;
using System.Collections.Generic;
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
        /// 内联标注在**深一层**的字段类型上时同样算：外层类型要为它展开，
        /// 而「展开」正是判据与注入必须对齐的那处（判据看不见，类型就不被接管、内联静默失效）。
        /// </summary>
        [Test]
        public void IsUsedBy_深层嵌套里的内联标注为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(InlineNestedUsageFixture)), Is.True);
        }

        /// <summary>
        /// **只把 <c>[ShowInInspector]</c> 挂在嵌套类型里**的类型必须被判为「用到了本插件」。
        /// <para>
        /// 与「只标在嵌套层成员上」同一条腿，但落点不同：那个夹具挂的是条件族，走的是
        /// <c>SerializedProperty</c> 迭代器那一半判据；这个挂的是**非序列化**成员，
        /// 迭代器根本看不见它——判据若只扫可序列化字段，这种类型不会被接管，
        /// 而那时嵌套层展开与否也就无从谈起，症状是**零告警**。
        /// </para>
        /// </summary>
        [Test]
        public void IsUsedBy_嵌套类型里只挂反射成员为真()
        {
            Assert.That(
                XInspectorUsageDetection.IsUsedBy(typeof(NestedInspectedOnlyUsageFixture)),
                Is.True,
                "判据要看得见嵌套类型里的非序列化成员。");
        }

        /// <summary>
        /// 控制项：嵌套类型上只挂**类级非分组**特性、内部一个成员特性都没有时不算——它不触发展开。
        /// </summary>
        [Test]
        public void IsUsedBy_嵌套类型只有类级非分组特性不算()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(NestedTypeLevelOnlyUsageFixture)), Is.False);
        }

        /// <summary>
        /// **多态槽位里的具体类型用到了本包**时判据看不出来——这是记录在案的近似（不修）。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 判据只吃**声明类型**（这里是没有任何特性的接口），看不见槽位里装着的具体类型上的
        /// 特性。后果：这种组件**不被自动接管**，多态段里的读路径成员不会生效——
        /// 逃生口是给它一个显式 <c>[CustomEditor]</c>（见包 README 与 Roadmap §十二）。
        /// </para>
        /// <para>
        /// 为什么留着：要看具体类型就得在「这个类型用没用到本插件」这一问里解引用**运行时
        /// 实例**——多选、空槽位、类型随时可变，那是构建期判据不该做的事。
        /// 这条若翻红，说明判据改了，请同步 README 与 Roadmap 的那两处措辞。
        /// </para>
        /// </remarks>
        [Test]
        public void IsUsedBy_多态槽位里的具体类型不算()
        {
            Assert.That(
                XInspectorUsageDetection.IsUsedBy(typeof(PolymorphicBlindSpotFixture)),
                Is.False,
                "记录在案的近似：判据只看声明类型。");
        }

        /// <summary>
        /// **嵌套类型上只挂类级分组**的类型必须被判为「用到了本插件」。
        /// <para>
        /// 类级分组自 2026-10-06 起会真的分发（<c>ClassLevelGroupProcessor</c>）——判据看不见它，
        /// 类型就不被接管、分组**静默失效**。判据经 <c>HasSupportedField</c> 的自检腿到达，
        /// 与注入同源。
        /// </para>
        /// </summary>
        [Test]
        public void IsUsedBy_嵌套类型上的类级分组为真()
        {
            Assert.That(
                XInspectorUsageDetection.IsUsedBy(typeof(ClassLevelGroupNestedUsageFixture)),
                Is.True,
                "判据要看得见嵌套类型自己的类级分组。");
        }

        /// <summary>深层嵌套里的类级分组同样为真——钉住两条递归判据的类级腿。</summary>
        [Test]
        public void IsUsedBy_深层嵌套里的类级分组为真()
        {
            Assert.That(
                XInspectorUsageDetection.IsUsedBy(typeof(DeepClassLevelGroupUsageFixture)),
                Is.True,
                "外层类型的字段里嵌着一个带类级分组的类型，也要判为用到了本包。");
        }

        /// <summary>
        /// **只挂 <c>[TableList]</c> 的**类型必须被判为「用到了本插件」。
        /// <para>
        /// 它没有自己的绘制器，靠**处理器**那一半被认出来——而处理器的「所处理的特性类型」
        /// 只有泛型基类才暴露（非泛型恒为 <c>null</c>）。处理器写错了基类，这条就红。
        /// </para>
        /// </summary>
        [Test]
        public void IsUsedBy_只挂表特性的类型为真()
        {
            Assert.That(
                XInspectorUsageDetection.IsUsedBy(typeof(TableListOnlyFixture)),
                Is.True,
                "表格靠处理器接手绘制，判据必须看得见它。");
        }

        /// <summary>只挂 <c>[ListDrawerSettings]</c> 的类型同样为真（它靠绘制器那一半）。</summary>
        [Test]
        public void IsUsedBy_只挂列表特性的类型为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(ListSettingsOnlyFixture)), Is.True);
        }

        /// <summary>
        /// 只挂 <c>[TypeDrawerSettings]</c> 的类型必须被判为「用到了本插件」——它靠绘制器那一半
        /// 被认出来（处理器表对它一无所知）。
        /// </summary>
        [Test]
        public void IsUsedBy_只挂类型选择器特性的类型为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(TypeDrawerSettingsOnlyFixture)), Is.True);
        }

        /// <summary>
        /// 只挂 <c>[PolymorphicDrawerSettings]</c> 的类型同样为真——同样靠绘制器那一半
        /// （末端是按特性选的，但判据走的是链上那枚替换型绘制器）。
        /// </summary>
        [Test]
        public void IsUsedBy_只挂多态选择器特性的类型为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(PolymorphicDrawerSettingsOnlyFixture)), Is.True);
        }

        /// <summary>
        /// 集合被本包接管、且**元素类型用到了本包**时为真——与元素层的安全阀镜像：
        /// 那种集合真的会建元素层，元素里的特性真的会生效。
        /// </summary>
        [Test]
        public void IsUsedBy_被接管的集合其元素类型用特性为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(ElementTypeUsageFixture)), Is.True);
        }

        /// <summary>
        /// 控制项：**元素类型用到了本包、但集合自己没被接管**时为假——安全阀逐字镜像的另一半：
        /// 那种集合不建元素层（元素里的特性本就无处生效），判据因此不该说「用到了本包」。
        /// <para>
        /// 这条同时钉住「判据**不需要**沿字段类型解包集合」这个核对结论：接管凭的是
        /// **字段自己的**特性，而判据一直在扫字段。
        /// </para>
        /// </summary>
        [Test]
        public void IsUsedBy_没被接管的集合的元素类型不算()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(UnmanagedElementTypeUsageFixture)), Is.False);
        }

        /// <summary>
        /// 控制项：**元素类型只带类级分组、但集合自己没被接管**时为假——安全阀镜像的另一半，
        /// 与上面那条逐字同款（接管凭的是字段自己的特性，判据因此不该沿字段类型解包集合）。
        /// </summary>
        [Test]
        public void IsUsedBy_没被接管的集合其元素类型只有类级分组不算()
        {
            Assert.That(
                XInspectorUsageDetection.IsUsedBy(typeof(UnmanagedClassLevelGroupElementFixture)),
                Is.False);
        }

        /// <summary>
        /// 只挂 <c>[Searchable]</c> 的类型为真——它**没有自己的绘制器**（搜索框由宿主绘制器顺带画），
        /// 全靠处理器那一半被认出来。处理器写错了基类，这条就红。
        /// </summary>
        [Test]
        public void IsUsedBy_只挂搜索特性的类型为真()
        {
            Assert.That(
                XInspectorUsageDetection.IsUsedBy(typeof(SearchableOnlyFixture)),
                Is.True,
                "搜索靠处理器保证不写了个寂寞，判据必须看得见它。");
        }

        /// <summary>
        /// **只把本包特性标在嵌套类型内部**的类型必须被判为「用到了本插件」。
        /// <para>
        /// 嵌套层从本轮起会真的展开（<c>NestedMemberExpansion</c>）——判据看不见它，
        /// 类型就不被接管、嵌套层的特性**静默失效**。这是判据第四次漏同一类东西
        /// （前三次：方法、属性、字段声明类型）。
        /// </para>
        /// </summary>
        [Test]
        public void IsUsedBy_只标在嵌套层成员上为真()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(NestedOnlyUsageFixture)), Is.True);
        }

        /// <summary>控制项：嵌套类型里只挂**原生**装饰器的不算（那不会触发展开）。</summary>
        [Test]
        public void IsUsedBy_嵌套层里的原生特性不算()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(NativeNestedUsageFixture)), Is.False);
        }

        /// <summary>控制项：嵌套类型上的**类级非分组**特性不算（那仍只在被检视类型上生效，也不触发展开）。</summary>
        [Test]
        public void IsUsedBy_嵌套类型上的类级非分组特性不算()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(ClassLevelNestedUsageFixture)), Is.False);
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

    /// <summary>内联标在**深一层**字段类型上的资产。</summary>
    internal sealed class InlineNestedUsageFixture : ScriptableObject
    {
        /// <summary>直接字段的类型没标记，标记在它**内部**的字段类型上。</summary>
        public InlineNestedUsageHolder value;
    }

    /// <summary>内部字段的声明类型带内联标记——外层类型因此值得被接管。</summary>
    [Serializable]
    internal sealed class InlineNestedUsageHolder
    {
        /// <summary>声明类型带标记。</summary>
        public InlineMarkedType inner;
    }

    /// <summary>嵌套类型上挂着别的本包特性的资产。</summary>
    internal sealed class NestedInspectedOnlyUsageFixture : ScriptableObject
    {
        /// <summary>嵌套类型里只挂着一个 <c>[ShowInInspector]</c> 属性。</summary>
        public InspectedOnlyNested value = new InspectedOnlyNested();
    }

    /// <summary>嵌套类型上挂着**类级非分组** <c>[Title]</c>——它只在被检视类型上生效。</summary>
    internal sealed class NestedTypeLevelOnlyUsageFixture : ScriptableObject
    {
        /// <summary>嵌套类型上有类级特性，且它内部一个成员特性都没有。</summary>
        public TitledNestedType value;
    }

    /// <summary>嵌套类型上挂着**类级分组**——它自 2026-10-06 起真的会分发到成员。</summary>
    internal sealed class ClassLevelGroupNestedUsageFixture : ScriptableObject
    {
        /// <summary>字段类型带类级分组，且它内部一个成员特性都没有。</summary>
        public BoxedNestedType value;
    }

    /// <summary>外层类型里嵌着一个带类级分组的类型——判据要递归看见它。</summary>
    internal sealed class DeepClassLevelGroupUsageFixture : ScriptableObject
    {
        /// <summary>中间层：自己没有特性，但它的字段类型带着类级分组。</summary>
        public MiddleWithoutAttrs middle;
    }

    /// <summary>只放一个非序列化反射成员的嵌套类型。</summary>
    [Serializable]
    internal class InspectedOnlyNested
    {
        /// <summary>非序列化属性——序列化迭代器看不见它。</summary>
        [ShowInInspector]
        public int Tag => 1;
    }

    /// <summary>多态槽位：声明类型是**没有特性**的接口，具体类型里有本包特性（控制项）。</summary>
    internal sealed class PolymorphicBlindSpotFixture : ScriptableObject
    {
        /// <summary>判据只看这个字段的**声明类型**（接口），看不见槽位里的具体类型。</summary>
        [SerializeReference]
        public IBlindSpotShape shape;
    }

    /// <summary>没有特性的槽位类型——判据看到的就是它。</summary>
    internal interface IBlindSpotShape
    {
    }

    /// <summary>用到了本包的具体类型——自动接管的判据**看不见**它（那条近似就是此事）。</summary>
    [Serializable]
    internal class BlindSpotShape : IBlindSpotShape
    {
        /// <summary>唯一的用法。</summary>
        [ShowInInspector]
        public int Tag => 1;
    }

    /// <summary>只挂表格特性的资产。</summary>
    internal sealed class TableListOnlyFixture : ScriptableObject
    {
        /// <summary>只标了表格——它没有绘制器，靠处理器那一半被认出来。</summary>
        [TableList]
        public List<TableRow> rows = new List<TableRow>();
    }

    /// <summary>只挂列表设置特性的资产。</summary>
    internal sealed class ListSettingsOnlyFixture : ScriptableObject
    {
        /// <summary>只标了列表设置——靠绘制器那一半被认出来。</summary>
        [ListDrawerSettings]
        public int[] values = { 1 };
    }

    /// <summary>只挂类型选择器特性的资产。</summary>
    internal sealed class TypeDrawerSettingsOnlyFixture : ScriptableObject
    {
        /// <summary>唯一的用法：托管引用的 <c>System.Type</c> 槽位。</summary>
        [SerializeReference]
        [TypeDrawerSettings]
        public Type chosen;
    }

    /// <summary>只挂多态选择器特性的资产。</summary>
    internal sealed class PolymorphicDrawerSettingsOnlyFixture : ScriptableObject
    {
        /// <summary>唯一的用法：多态引用槽位。</summary>
        [SerializeReference]
        [PolymorphicDrawerSettings]
        public IDisposable shape;
    }

    /// <summary>集合被本包接管、元素类型也用到了本包的资产。</summary>
    internal sealed class ElementTypeUsageFixture : ScriptableObject
    {
        /// <summary><c>[ListDrawerSettings]</c> 是元素层的**容器项**——两者都真才建层。</summary>
        [ListDrawerSettings]
        public List<ElementItem> items = new List<ElementItem> { new ElementItem() };
    }

    /// <summary>元素类型用到了本包、但集合自己一个特性都没标的资产（控制项）。</summary>
    internal sealed class UnmanagedElementTypeUsageFixture : ScriptableObject
    {
        /// <summary>没被接管 → 不建元素层 → 元素里的特性本就无处生效。</summary>
        public List<ElementItem> items = new List<ElementItem> { new ElementItem() };
    }

    /// <summary>只挂搜索特性的资产。</summary>
    internal sealed class SearchableOnlyFixture : ScriptableObject
    {
        /// <summary>只标了搜索——它没有绘制器，靠处理器那一半被认出来。</summary>
        [Searchable]
        public int[] values = { 1 };
    }

    /// <summary>只把本包特性标在**嵌套类型内部**的资产。</summary>
    internal sealed class NestedOnlyUsageFixture : ScriptableObject
    {
        /// <summary>嵌套类型内部带 <c>[ShowIf]</c> 与 <c>[PropertyOrder]</c>。</summary>
        public NestedStats stats = new NestedStats();
    }

    /// <summary>嵌套类型里只挂原生装饰器的资产（控制项）。</summary>
    internal sealed class NativeNestedUsageFixture : ScriptableObject
    {
        /// <summary>只带 Unity 自己的 <c>[Range]</c>。</summary>
        public NativeOnlyNested nested;
    }

    /// <summary>嵌套类型上有类级**非分组**特性的资产（控制项）。</summary>
    internal sealed class ClassLevelNestedUsageFixture : ScriptableObject
    {
        /// <summary>类级 <c>[Title]</c>——嵌套层的类级非分组特性仍不生效。</summary>
        public ClassLevelNested nested;
    }

    /// <summary>挂着类级 <c>[BoxGroup]</c> 的嵌套类型（分发到它的每个成员）。</summary>
    [Serializable]
    [BoxGroup("嵌套类级组")]
    internal sealed class BoxedNestedType
    {
        /// <summary>普通字段——分组靠类型上那份分发下来。</summary>
        public int a = 1;
    }

    /// <summary>中间层类型——自己没有特性，只有字段的声明类型带类级分组。</summary>
    [Serializable]
    internal sealed class MiddleWithoutAttrs
    {
        /// <summary>字段的声明类型带类级分组。</summary>
        public BoxedNestedType inner;
    }

    /// <summary>集合未被本包接管、元素类型只带类级分组（判据不该沿字段类型解包集合）。</summary>
    internal sealed class UnmanagedClassLevelGroupElementFixture : ScriptableObject
    {
        /// <summary>没有 <c>[ListDrawerSettings]</c> 一类 → 不建元素层。</summary>
        public List<BoxedNestedType> items = new List<BoxedNestedType> { new BoxedNestedType() };
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
