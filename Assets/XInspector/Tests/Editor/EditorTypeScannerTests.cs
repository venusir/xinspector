using NUnit.Framework;
using UnityEngine.TestTools;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 类型扫描器：**扫不扫**与**告不告警**是两件事，这里把后者钉住。
    /// <para>
    /// 由来：本仓的两个测试程序集里各有一个**故意**不可实例化的夹具
    /// （<c>NoDefaultConstructorProcessor</c>、<c>NoDefaultConstructorDrawer</c>），
    /// 而扫描覆盖所有已加载程序集——于是「你的扩展写坏了」那条告警会打到**正常使用**的
    /// Console 里（每次域重载后首次建树各一次，绘制器与处理器各一条），
    /// 使用方只要装了 Test Framework 也会同样看到。
    /// </para>
    /// <para>
    /// 修法是只收窄**告警**、不收窄**扫描**：扫描范围不得收窄是两条契约用例守着的承诺
    /// （<c>Registry_Discovers*FromThisAssembly</c>），而且判据若被用来决定注册范围，
    /// 误判的后果是使用方的绘制器**静默不注册**——那比多一行日志糟得多。
    /// </para>
    /// </summary>
    [TestFixture]
    public class EditorTypeScannerTests
    {
        #region 判据

        /// <summary>
        /// 测试程序集的判据：**引用了 nunit.framework**。
        /// </summary>
        [Test]
        public void 测试程序集的判据()
        {
            Assert.That(EditorTypeScanner.IsTestAssembly(typeof(EditorTypeScannerTests).Assembly), Is.True,
                "本测试程序集应当被判为测试程序集。");
            Assert.That(EditorTypeScanner.IsTestAssembly(typeof(EditorTypeScanner).Assembly), Is.False,
                "包的 Editor 程序集不是测试程序集——它里面写坏了的扩展必须被告警。");
            Assert.That(EditorTypeScanner.IsTestAssembly(typeof(string).Assembly), Is.False);
            Assert.That(EditorTypeScanner.IsTestAssembly(null), Is.False);
        }

        /// <summary>告警判据：非测试程序集才告警。</summary>
        [Test]
        public void 告警判据只对非测试程序集为真()
        {
            Assert.That(EditorTypeScanner.ShouldWarnAbout(typeof(NoDefaultConstructorProcessor)), Is.False,
                "测试里故意不可实例化的夹具不告警。");
            Assert.That(EditorTypeScanner.ShouldWarnAbout(typeof(NoDefaultConstructorDrawer)), Is.False);

            Assert.That(EditorTypeScanner.ShouldWarnAbout(typeof(EditorTypeScanner)), Is.True);
            Assert.That(EditorTypeScanner.ShouldWarnAbout(typeof(string)), Is.True);
        }

        #endregion

        #region 生产路径

        /// <summary>
        /// 走生产入口扫一遍：那两个夹具仍然被跳过（**扫描范围没收窄**），
        /// 但 Console 里一条日志都不该有。
        /// </summary>
        /// <remarks>
        /// 「一条日志都没有」比「没有那条告警」更强，也就更值得断言：
        /// 扫描器本该只在「有类型因缺无参构造而被跳过」时说一句话，
        /// 若它还悄悄说了别的，这条用例会替我们听见。
        /// </remarks>
        [Test]
        public void 生产扫描对测试夹具静默()
        {
            var found = EditorTypeScanner.CollectInstantiable<AttributeProcessor>("特性处理器");

            Assert.That(found, Has.No.Member(typeof(NoDefaultConstructorProcessor)),
                "它仍应被跳过——这条判据没变，变的只是要不要说话。");
            Assert.That(found, Has.Member(typeof(ShowInProcessor)),
                "生产处理器照旧被发现，扫描范围没有被收窄。");

            LogAssert.NoUnexpectedReceived();
        }

        #endregion
    }
}
