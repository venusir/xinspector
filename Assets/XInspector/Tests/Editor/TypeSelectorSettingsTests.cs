using System;
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
    /// <c>[TypeSelectorSettings].FilterTypesFunction</c> 的解析通道：绑成 <c>Func&lt;Type, bool&gt;</c>
    /// 存进状态（构建期一次），以及解析失败三档、误用第三支。
    /// <para>
    /// 断言的是**状态对象**（本仓不测 IMGUI）：<c>State.Resolved</c> 与过滤器的真值。
    /// </para>
    /// </summary>
    [TestFixture]
    public class TypeSelectorSettingsTests
    {
        #region Fixture

        private TypeSelectorFixture _target;

        /// <summary>建立测试资产。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<TypeSelectorFixture>();
        }

        /// <summary>销毁资产并复位静态门面。</summary>
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

        #region 解析成功

        /// <summary>过滤器解析成委托，真值跟着方法走。</summary>
        [Test]
        public void 过滤器解析成委托()
        {
            using (var tree = BuildTree())
            {
                var state = StateOf(tree, "picked");

                Assert.That(state, Is.Not.Null, "处理器没跑？");
                Assert.That(state.Resolved, Is.True);
                Assert.That(state.Filter, Is.Not.Null);
                Assert.That(state.Filter(typeof(TypeSelectorCircle)), Is.True);
                Assert.That(state.Filter(typeof(TypeSelectorSquare)), Is.False, "夹具只放圆过关。");
            }
        }

        /// <summary>过滤器**每次现读实例**：改宿主字段之后同一个委托结果跟着变。</summary>
        [Test]
        public void 过滤器每次现读实例()
        {
            using (var tree = BuildTree())
            {
                var filter = StateOf(tree, "picked").Filter;

                Assert.That(filter(typeof(TypeSelectorSquare)), Is.False);

                _target.allowEverything = true;

                Assert.That(filter(typeof(TypeSelectorSquare)), Is.True, "读的是当时的实例。");
            }
        }

        #endregion

        #region 解析失败三档（构建期告警 + 不过滤）

        /// <summary>方法名不存在：告警一次，状态未解析（绘制期退回不过滤）。</summary>
        [Test]
        public void 方法不存在时告警且不过滤()
        {
            LogAssert.Expect(LogType.Warning, new Regex("该过滤器已忽略"));

            using (var tree = BuildTree())
            {
                var state = StateOf(tree, "missingMethod");

                Assert.That(state, Is.Not.Null, "失败也要有状态（Resolved 为假）。");
                Assert.That(state.Resolved, Is.False);
                Assert.That(state.Filter, Is.Null);
            }
        }

        /// <summary>同名但**形状不对**（参数不是单个 <c>Type</c>）：同样告警 + 不过滤。</summary>
        [Test]
        public void 形状不对时告警且不过滤()
        {
            LogAssert.Expect(LogType.Warning, new Regex("该过滤器已忽略"));

            using (var tree = BuildTree())
            {
                Assert.That(StateOf(tree, "wrongShape").Resolved, Is.False);
            }
        }

        /// <summary>同名的是**字段**而不是方法：同样告警 + 不过滤。</summary>
        [Test]
        public void 同名字段时告警且不过滤()
        {
            LogAssert.Expect(LogType.Warning, new Regex("该过滤器已忽略"));

            using (var tree = BuildTree())
            {
                Assert.That(StateOf(tree, "fieldNotMethod").Resolved, Is.False);
            }
        }

        #endregion

        #region 嵌套层与值类型容器

        /// <summary>
        /// 嵌套层里找的是**那一层的实例**——根上放同名方法作陷阱（它恒真/恒假相反）。
        /// </summary>
        [Test]
        public void 嵌套层用嵌套实例的方法()
        {
            using (var tree = BuildTree())
            {
                var state = StateOf(tree, "nested.picked");

                Assert.That(state.Resolved, Is.True);
                Assert.That(
                    state.Filter(typeof(TypeSelectorSquare)),
                    Is.True,
                    "嵌套实例放行方形；根上的同名方法恒假——解析到根上这里就是假。");
            }
        }

        /// <summary>值类型容器上的调用一律拒绝：告警 + 不过滤（判据与文案来自 NestedInstanceScope）。</summary>
        [Test]
        public void 值类型容器拒绝()
        {
            LogAssert.Expect(LogType.Warning, new Regex("值类型"));

            using (var tree = BuildTree())
            {
                Assert.That(
                    StateOf(tree, "holder.picked").Resolved,
                    Is.False,
                    "struct 容器里的过滤器不解析（方法调用改的是装箱副本）。");
            }
        }

        #endregion

        #region 误用第三支

        /// <summary>
        /// 标了 <c>[TypeSelectorSettings]</c> 而**没有任何选择器特性**：构建期告警
        /// （判据与「有没有节点」无关——没有选择器特性就没有任何本包选择器被渲染）。
        /// </summary>
        [Test]
        public void 设置单独标时告警()
        {
            LogAssert.Expect(LogType.Warning, new Regex("没有任何选择器由本包渲染"));

            var target = ScriptableObject.CreateInstance<TypeSelectorMisuseFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    Assert.That(Find(tree.Root, "alone"), Is.Not.Null, "字段照常进树，只是特性不生效。");
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>对照：与 <c>[TypeDrawerSettings]</c> 同标时**不告警**（那是正确用法）。</summary>
        [Test]
        public void 与选择器特性同标时不告警()
        {
            LogAssert.NoUnexpectedReceived();

            var target = ScriptableObject.CreateInstance<TypeSelectorMisuseFixture>();
            try
            {
                using (var tree = PropertyTree.Create(new SerializedObject(target)))
                {
                    Assert.That(Find(tree.Root, "paired"), Is.Not.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>构建被测的树。</summary>
        /// <returns>属性树。</returns>
        private PropertyTree BuildTree()
        {
            return PropertyTree.Create(new SerializedObject(_target));
        }

        /// <summary>取一个节点上的过滤器状态。</summary>
        /// <param name="tree">树。</param>
        /// <param name="path">节点路径。</param>
        /// <returns>状态。</returns>
        private static TypeSelectorFilterState StateOf(PropertyTree tree, string path)
        {
            var node = Find(tree.Root, path);

            Assert.That(node, Is.Not.Null, $"找不到节点 {path}。");
            return node.State.Get<TypeSelectorFilterState>();
        }

        /// <summary>深度优先按路径找节点。</summary>
        /// <param name="node">当前节点。</param>
        /// <param name="path">完整路径。</param>
        /// <returns>节点；没有返回 <c>null</c>。</returns>
        private static InspectorProperty Find(InspectorProperty node, string path)
        {
            if (string.Equals(node.Path, path, StringComparison.Ordinal))
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                var found = Find(child, path);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        #endregion
    }

    #region Fixtures

    /// <summary>过滤器解析的夹具：四档失败 + 嵌套 + 值类型容器。</summary>
    [HideMonoScript]
    internal sealed class TypeSelectorFixture : ScriptableObject
    {
        /// <summary>嵌套层里那个「看错对象」的陷阱：根上的同名方法恒为**假**。</summary>
        public bool Allow(Type type) => false;

        /// <summary>过滤器读的那个开关（「每帧现读」那条用它）。</summary>
        public bool allowEverything;

        /// <summary>顶层：正常解析。</summary>
        [SerializeReference]
        [TypeDrawerSettings]
        [TypeSelectorSettings(FilterTypesFunction = nameof(AllowCircle))]
        public Type picked;

        /// <summary>方法名不存在。</summary>
        [SerializeReference]
        [TypeDrawerSettings]
        [TypeSelectorSettings(FilterTypesFunction = "NoSuchMethod")]
        public Type missingMethod;

        /// <summary>同名方法形状不对（参数不是单个 <c>Type</c>）。</summary>
        [SerializeReference]
        [TypeDrawerSettings]
        [TypeSelectorSettings(FilterTypesFunction = nameof(WrongShape))]
        public Type wrongShape;

        /// <summary>同名的是字段而不是方法。</summary>
        [SerializeReference]
        [TypeDrawerSettings]
        [TypeSelectorSettings(FilterTypesFunction = nameof(fieldNotMethodValue))]
        public Type fieldNotMethod;

        /// <summary>嵌套层的对照资产。</summary>
        public TypeSelectorNested nested = new TypeSelectorNested();

        /// <summary>值类型容器的对照资产。</summary>
        public TypeSelectorStructHolder holder;

        /// <summary>同名字段（喂「同名的是字段」那一档）。</summary>
        public bool fieldNotMethodValue;

        /// <summary>正常过滤器：只放行圆；开关打开时全放行（「每帧现读」那条用它）。</summary>
        /// <param name="type">候选类型。</param>
        /// <returns>是否放行。</returns>
        private bool AllowCircle(Type type) => allowEverything || type == typeof(TypeSelectorCircle);

        /// <summary>形状不对的同名方法（两个参数）。</summary>
        /// <param name="type">参数一。</param>
        /// <param name="extra">参数二。</param>
        /// <returns>恒真。</returns>
        private bool WrongShape(Type type, int extra) => true;
    }

    /// <summary>嵌套层：方法在嵌套实例上（根上的同名方法作陷阱）。</summary>
    [Serializable]
    internal sealed class TypeSelectorNested
    {
        /// <summary>嵌套层的过滤器：只放行方形（根上的同名方法恒假——解析到根上就露馅）。</summary>
        /// <param name="type">候选类型。</param>
        /// <returns>是不是方形。</returns>
        public bool Allow(Type type) => type == typeof(TypeSelectorSquare);

        /// <summary>带过滤器的槽位。</summary>
        [SerializeReference]
        [TypeDrawerSettings]
        [TypeSelectorSettings(FilterTypesFunction = nameof(Allow))]
        public Type picked;
    }

    /// <summary>值类型容器：里面的过滤器不解析（方法调用改的是装箱副本）。</summary>
    [Serializable]
    internal struct TypeSelectorStructHolder
    {
        /// <summary>带过滤器的槽位。</summary>
        [SerializeReference]
        [TypeDrawerSettings]
        [TypeSelectorSettings(FilterTypesFunction = nameof(Allow))]
        public Type picked;

        /// <summary>过滤器（结构体上的方法照常写得出来）。</summary>
        /// <param name="type">候选类型。</param>
        /// <returns>恒真。</returns>
        public bool Allow(Type type) => true;
    }

    /// <summary>误用第三支的夹具（单独一个：共享夹具上的告警会污染别处）。</summary>
    [HideMonoScript]
    internal sealed class TypeSelectorMisuseFixture : ScriptableObject
    {
        /// <summary>只标了设置、没有任何选择器特性——该特性不生效（构建期告警）。</summary>
        [SerializeReference]
        [TypeSelectorSettings]
        public Type alone;

        /// <summary>对照：与选择器特性同标——正确用法，不告警。</summary>
        [SerializeReference]
        [TypeDrawerSettings]
        [TypeSelectorSettings(PreferNamespaces = false)]
        public Type paired;
    }

    /// <summary>候选类型一。</summary>
    [Serializable]
    internal sealed class TypeSelectorCircle
    {
    }

    /// <summary>候选类型二。</summary>
    [Serializable]
    internal sealed class TypeSelectorSquare
    {
    }

    #endregion
}
