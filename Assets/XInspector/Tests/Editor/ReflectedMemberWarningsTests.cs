using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 反射成员上「需要序列化后端的特性」的告警措辞。
    /// <para>
    /// <c>[ShowInInspector, PropertyRange(0, 1)]</c> 是最现实的组合（Odin 的官方样例就这么写），
    /// 而 <c>[PropertyRange]</c> 在本包里对它无效。放行的行为不变，但**说法必须对**——
    /// 「字段退回普通绘制」这句话对只读成员是错的：它没有字段可退，值是照常显示的。
    /// 照着错话去查「为什么字段没画出来」会一无所获。
    /// </para>
    /// <para>
    /// 这里测的是告警文本的生成（纯函数），不是绘制——绘制那一半照本仓策略不测 GUI。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ReflectedMemberWarningsTests
    {
        #region Private Fields

        private ReflectedMemberFixture _target;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ReflectedMemberFixture>();
        }

        /// <summary>销毁临时资产并复位两张注册表。</summary>
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

        #region 措辞

        /// <summary>反射成员的告警说的是「这条特性对它无效」，而不是「字段退回普通绘制」。</summary>
        [Test]
        public void 反射成员的告警不提字段退回()
        {
            using (var tree = PropertyTree.Create(new SerializedObject(_target)))
            {
                var message = DrawerWarnings.TypeMismatch(
                    ReflectedMemberTests.Find(tree.Root, "_reflected"), "[PropertyRange]", "数值类型");

                Assert.That(message, Does.Contain("[PropertyRange]"));
                Assert.That(message, Does.Contain("[ShowInInspector]"));
                Assert.That(message, Does.Contain("只读"));
                Assert.That(message, Does.Not.Contain("字段退回普通绘制"));
            }
        }

        /// <summary>
        /// 控制项：序列化成员的措辞一个字没变。
        /// <para>
        /// 没有这一条，上面那条无法区分「按后端分叉」与「把所有告警都换了个说法」。
        /// </para>
        /// </summary>
        [Test]
        public void 序列化成员的告警措辞不变()
        {
            using (var tree = PropertyTree.Create(new SerializedObject(_target)))
            {
                var message = DrawerWarnings.TypeMismatch(
                    ReflectedMemberTests.Find(tree.Root, "serialized"), "[PropertyRange]", "数值类型");

                Assert.That(message, Does.Contain("字段退回普通绘制"));
                Assert.That(message, Does.Not.Contain("[ShowInInspector]"));
            }
        }

        #endregion
    }
}
