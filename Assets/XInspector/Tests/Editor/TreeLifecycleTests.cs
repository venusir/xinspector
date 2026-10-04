using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 生命周期三件（<c>[OnInspectorInit]</c> / <c>[OnInspectorDispose]</c> /
    /// <c>[OnStateUpdate]</c>）：什么时候调、调几次、多选怎么算。
    /// <para>
    /// 绘制路径本身要 GUI 上下文，但「钩子有没有被调用」是可无头验证的——
    /// <see cref="PropertyTree.RunStateUpdate"/> 那条入口就是为此留的。
    /// </para>
    /// </summary>
    [TestFixture]
    public class TreeLifecycleTests
    {
        #region Private Fields

        private LifecycleFixture _first;
        private LifecycleFixture _second;

        #endregion

        #region Setup / Teardown

        /// <summary>建立两个测试资产并清空静态账本。</summary>
        [SetUp]
        public void SetUp()
        {
            LifecycleFixture.Log.Clear();
            _first = ScriptableObject.CreateInstance<LifecycleFixture>();
            _second = ScriptableObject.CreateInstance<LifecycleFixture>();
        }

        /// <summary>销毁资产。</summary>
        [TearDown]
        public void TearDown()
        {
            Destroy(ref _first);
            Destroy(ref _second);
            LifecycleFixture.Log.Clear();

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region Init

        /// <summary>建树时调一次。</summary>
        [Test]
        public void 建树时调用Init()
        {
            using (Build(_first))
            {
                Assert.That(LifecycleFixture.Log, Does.Contain("init"));
            }
        }

        /// <summary>多选时对每个目标各一次。</summary>
        [Test]
        public void 多选时每个目标各调一次()
        {
            using (PropertyTree.Create(new SerializedObject(new Object[] { _first, _second })))
            {
                Assert.That(Count("init"), Is.EqualTo(2));
            }
        }

        /// <summary>每建一棵树就调一次——切换目标、域重载之后会重来。</summary>
        [Test]
        public void 每次建树都调一次()
        {
            using (Build(_first))
            {
            }

            using (Build(_first))
            {
            }

            Assert.That(Count("init"), Is.EqualTo(2));
        }

        #endregion

        #region Dispose

        /// <summary>释放时调一次，且**早于**节点状态复位。</summary>
        [Test]
        public void 释放时调用Dispose()
        {
            var tree = Build(_first);

            Assert.That(LifecycleFixture.Log, Does.Not.Contain("dispose"));

            tree.Dispose();

            Assert.That(LifecycleFixture.Log, Does.Contain("dispose"));
        }

        /// <summary>重复释放是幂等的：第二次不该再调一遍钩子。</summary>
        [Test]
        public void 重复释放不重复调用()
        {
            var tree = Build(_first);

            tree.Dispose();
            tree.Dispose();

            Assert.That(Count("dispose"), Is.EqualTo(1));
        }

        #endregion

        #region StateUpdate

        /// <summary>按趟调用：判定在 Layout 趟为真、其余为假。</summary>
        [Test]
        public void 只在布局趟调用()
        {
            Assert.That(TreeLifecycle.ShouldRunStateUpdate(EventType.Layout), Is.True);
            Assert.That(TreeLifecycle.ShouldRunStateUpdate(EventType.Repaint), Is.False);
            Assert.That(TreeLifecycle.ShouldRunStateUpdate(EventType.MouseDown), Is.False);
        }

        /// <summary>跑一趟就调一次——判据之外的调用路径本身也要对。</summary>
        [Test]
        public void 跑一趟调一次()
        {
            using (var tree = Build(_first))
            {
                tree.RunStateUpdate();
                tree.RunStateUpdate();

                Assert.That(Count("update"), Is.EqualTo(2));
            }
        }

        /// <summary>建树与释放都不该顺带把 update 跑了。</summary>
        [Test]
        public void 建树与释放不触发update()
        {
            Build(_first).Dispose();

            Assert.That(Count("update"), Is.EqualTo(0));
        }

        #endregion

        #region 边界

        /// <summary>带参数的方法被跳过并告警——调不动的方法不该静默地占着钩子。</summary>
        [Test]
        public void 带参数的方法被跳过并告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex("调不动它"));

            using (var tree = Build(_first))
            {
                Assert.That(tree.Lifecycle, Is.Not.Null, "另外两类的钩子还在。");
                Assert.That(LifecycleFixture.Log, Does.Not.Contain("withParam"));
            }
        }

        /// <summary>钩子方法**不产生属性树节点**——它们不在某个位置上画东西。</summary>
        [Test]
        public void 钩子不产生节点()
        {
            using (var tree = Build(_first))
            {
                foreach (var child in tree.Root.Children)
                {
                    Assert.That(
                        child.Path,
                        Does.Not.Contain("LifecycleTick"),
                        "钩子方法混进节点会让 Inspector 里多出空白行。");
                }
            }
        }

        /// <summary>生命周期特性也算「用到了本插件」——否则类型不被接管，钩子一次都不跑。</summary>
        [Test]
        public void 只挂生命周期特性的类型也算用到()
        {
            Assert.That(XInspectorUsageDetection.IsUsedBy(typeof(LifecycleOnlyFixture)), Is.True);
        }

        #endregion

        #region Private Helpers

        /// <summary>建一棵树。</summary>
        /// <param name="target">目标对象。</param>
        /// <returns>属性树。</returns>
        private static PropertyTree Build(Object target)
        {
            return PropertyTree.Create(new SerializedObject(target));
        }

        /// <summary>数一个记号出现了几次。</summary>
        /// <param name="tag">记号。</param>
        /// <returns>次数。</returns>
        private static int Count(string tag)
        {
            var count = 0;

            for (var i = 0; i < LifecycleFixture.Log.Count; i++)
            {
                if (LifecycleFixture.Log[i] == tag)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>销毁资产并把引用清空。</summary>
        /// <param name="target">资产引用。</param>
        private static void Destroy(ref LifecycleFixture target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
                target = null;
            }
        }

        #endregion
    }

    /// <summary>生命周期测试用的资产：每个钩子往静态账本里记一个记号。</summary>
    internal sealed class LifecycleFixture : ScriptableObject
    {
        /// <summary>账本。静态是为了让测试读得到——钩子的返回值与方法名都不携带信息。</summary>
        public static readonly System.Collections.Generic.List<string> Log =
            new System.Collections.Generic.List<string>();

        /// <summary>普通字段，让树有内容。</summary>
        public int value = 1;

        /// <summary>建树时记一笔。</summary>
        [OnInspectorInit]
        private void OnInit()
        {
            Log.Add("init");
        }

        /// <summary>释放时记一笔。</summary>
        [OnInspectorDispose]
        private void OnDispose()
        {
            Log.Add("dispose");
        }

        /// <summary>每趟记一笔。</summary>
        [OnStateUpdate]
        private void LifecycleTick()
        {
            Log.Add("update");
        }

        /// <summary>带参数——该被跳过。</summary>
        [OnInspectorInit]
        private void WithParameter(int amount)
        {
            Log.Add("withParam");
        }
    }

    /// <summary>只挂生命周期特性的资产——一个字段特性都没有。</summary>
    internal sealed class LifecycleOnlyFixture : ScriptableObject
    {
        /// <summary>普通字段。</summary>
        public int value = 1;

        /// <summary>唯一的用法。</summary>
        [OnInspectorInit]
        private void Prepare()
        {
        }
    }
}
