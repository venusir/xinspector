using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.TestTools;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// L7 核验的**测量用例**：把「Unity 6 的原生多态能力面到底给到哪」钉成守卫。
    /// <para>
    /// 本夹具**不测本包的绘制**，只测 Unity 自己的行为——它存在的理由是
    /// 「Unity 哪天换了行为，这条先红」，而不是「这段代码对不对」。
    /// 每一条都对应 <c>Documentation/Modules/Pipeline.md</c> 的 L7 核验一节里的一个结论。
    /// </para>
    /// <para>
    /// 判据全部走 <see cref="SerializedObject"/> / <see cref="SerializedProperty"/> 的**公开面**
    /// （<c>managedReference*</c> 那一组已核过是 public；<c>isReferencingAManagedReferenceField</c>、
    /// <c>managedReferencePropertyPath</c>、<c>FindFirstPropertyFromManagedReferencePath</c> 是 internal，
    /// 本包用不到，也不该依赖）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class SerializedReferenceProbeTests
    {
        #region 夹具

        /// <summary>复位静态门面（其他夹具的既有纪律，这里保持一致）。</summary>
        [TearDown]
        public void TearDown()
        {
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 探针一：多态引用的公开能力面

        /// <summary>
        /// 多态引用字段的值入口**给的是活实例**，而且**可写**——
        /// 这是「多态引用能不能进本包管线」的第一块基石。
        /// </summary>
        [Test]
        public void 多态引用可读可写且给的是活实例()
        {
            var target = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            var shape = new ProbeShape { hp = 7, label = "circle" };
            target.shape = shape;
            try
            {
                var so = new SerializedObject(target);
                var prop = so.FindProperty("shape");

                Assert.That(prop.propertyType, Is.EqualTo(SerializedPropertyType.ManagedReference));

                var read = prop.managedReferenceValue as ProbeShape;
                Assert.That(read, Is.Not.Null, "managedReferenceValue 应当给出实例本身。");
                Assert.That(read.hp, Is.EqualTo(7));
                Assert.That(ReferenceEquals(read, shape), Is.True, "给的是活实例，不是快照。");

                // 写：换一个实例，落盘后再读。
                prop.managedReferenceValue = new ProbeShape { hp = 42, label = "box" };
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                var after = so.FindProperty("shape").managedReferenceValue as ProbeShape;
                Assert.That(after, Is.Not.Null);
                Assert.That(after.hp, Is.EqualTo(42), "写进去的值留得住。");
                Assert.That(after.label, Is.EqualTo("box"));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 具体类型与声明类型两个名字都拿得到——类型选择器要显示的就是这两个。
        /// </summary>
        /// <remarks>
        /// 只钉「拿得到且非空」，**不钉字符串格式**：那是 Unity 的内部拼法
        /// （形如 <c>AssemblyName Namespace.TypeName</c>），钉死格式会让这条在无害改动上先红。
        /// </remarks>
        [Test]
        public void 多态引用报得出具体类型与声明类型()
        {
            var target = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            target.shape = new ProbeShape { hp = 1 };
            try
            {
                var prop = new SerializedObject(target).FindProperty("shape");

                Assert.That(prop.managedReferenceFullTypename, Is.Not.Empty.And.Contains("ProbeShape"),
                    "具体类型名（实例那一侧）。");
                Assert.That(prop.managedReferenceFieldTypename, Is.Not.Empty.And.Contains("ProbeShape"),
                    "声明类型名（字段那一侧）。");

                TestContext.WriteLine($"[探针] full = {prop.managedReferenceFullTypename}");
                TestContext.WriteLine($"[探针] field = {prop.managedReferenceFieldTypename}");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **决定性的一条**：多态引用**里面**的子字段有没有独立的序列化属性句柄？
        /// </summary>
        /// <remarks>
        /// <para>
        /// 展成真节点要求每个子成员都能拿到一个稳定的 <see cref="SerializedProperty"/>——
        /// 拿不到，就只剩「整份交给 <c>PropertyField</c>」这一条路，本包的特性也就进不去。
        /// </para>
        /// <para>
        /// 路径的拼法是**测量**出来的，不是猜的：这里先按 <c>shape.hp</c> 找，
        /// 找不到就沿着子属性枚举一遍，把真实路径打进日志再断言。
        /// </para>
        /// </remarks>
        [Test]
        public void 多态引用里的子字段有独立的序列化属性句柄()
        {
            var target = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            target.shape = new ProbeShape { hp = 7 };
            try
            {
                var so = new SerializedObject(target);
                var root = so.FindProperty("shape");

                // 先把整棵子树的可视属性路径列出来——这是「测量」的那一半。
                var paths = new List<string>();
                var cursor = root.Copy();
                var end = cursor.GetEndProperty();
                var enterChildren = true;
                while (cursor.NextVisible(enterChildren) && !SerializedProperty.EqualContents(cursor, end))
                {
                    enterChildren = false;
                    paths.Add($"{cursor.propertyPath} ({cursor.propertyType})");
                }

                TestContext.WriteLine("[探针] shape 子树的可视路径：" + string.Join(" | ", paths));

                var hp = so.FindProperty("shape.hp");
                Assert.That(hp, Is.Not.Null,
                    "按点分路径要能找到多态引用里面的子字段；实测子树路径：" + string.Join(" | ", paths));
                Assert.That(hp.propertyType, Is.EqualTo(SerializedPropertyType.Integer));
                Assert.That(hp.intValue, Is.EqualTo(7), "句柄读得到当前值。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 接口类型的字段同样由托管引用承载——Odin 的多态引用主战场就是接口与抽象类。
        /// </summary>
        [Test]
        public void 接口字段由托管引用承载()
        {
            var target = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            target.contract = new ProbeShape { hp = 3 };
            try
            {
                var prop = new SerializedObject(target).FindProperty("contract");

                Assert.That(prop, Is.Not.Null, "接口字段照样在序列化数据里。");
                Assert.That(prop.propertyType, Is.EqualTo(SerializedPropertyType.ManagedReference));
                Assert.That(prop.managedReferenceValue as ProbeShape, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 探针二：可写的 System.Type 通道

        /// <summary>
        /// <c>System.Type</c>：**裸字段进不了树，但 <c>[SerializeReference]</c> 的进得了**。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 这条是 <c>[TypeDrawerSettings]</c> 判「L7 前置」的那条依据的关键分岔：
        /// 裸 <c>System.Type</c> 字段没有序列化属性 ⇒ 进不了树、写不回去；
        /// 但**加一个 <c>[SerializeReference]</c>** 之后它在序列化数据里是 <c>ManagedReference</c>，
        /// 于是**有句柄、也就有写回的落点**。
        /// </para>
        /// <para>
        /// 代价是**使用方要改字段声明**（多一个特性）。这算不算「通道」，取决于口径——
        /// 本仓的口径是：**凡要求使用方改数据形状的，都不是「通道」，而是一种约定写法**。
        /// 但它只多一个 Unity 自带的特性，与「自研序列化器」不是一个量级，故单列一条。
        /// </para>
        /// </remarks>
        [Test]
        public void 系统类型字段裸的进不了树托管引用的进得了()
        {
            var target = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            try
            {
                var so = new SerializedObject(target);

                TestContext.WriteLine($"[探针] 裸 System.Type 字段：{Describe(so.FindProperty("typeField"))}");
                TestContext.WriteLine($"[探针] [SerializeReference] 的 System.Type 字段：{Describe(so.FindProperty("managedTypeField"))}");
                TestContext.WriteLine($"[探针] MonoScript 引用字段：{Describe(so.FindProperty("scriptRef"))}");

                Assert.That(so.FindProperty("typeField"), Is.Null,
                    "裸 System.Type 字段：没有序列化属性 ⇒ 进不了树、写不回去。");

                var managed = so.FindProperty("managedTypeField");
                Assert.That(managed, Is.Not.Null,
                    "加了 [SerializeReference] 之后它进得了序列化数据。");
                Assert.That(managed.propertyType, Is.EqualTo(SerializedPropertyType.ManagedReference));

                Assert.That(so.FindProperty("scriptRef"), Is.Not.Null,
                    "MonoScript 引用是**另一条**路：它进得了序列化数据（但只能指脚本，指不了任意类型）。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// <c>[SerializeReference]</c> 承载的 <c>System.Type</c> **写得住**：写进去的类型能落盘、能读回。
        /// </summary>
        /// <remarks>
        /// 这一条决定 <c>[TypeDrawerSettings]</c> 是「缺一条通道」还是「通道已在、只差一个约定」。
        /// 只测「写进去读得回来」，**不测 Undo**——那是另一件事，没测就不写。
        /// </remarks>
        [Test]
        public void 托管引用承载的系统类型能写回并落盘()
        {
            var target = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            try
            {
                var so = new SerializedObject(target);
                var prop = so.FindProperty("managedTypeField");

                prop.managedReferenceValue = typeof(ProbeShape);
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                var after = new SerializedObject(target).FindProperty("managedTypeField");
                TestContext.WriteLine($"[探针] 写回后 full = {after.managedReferenceFullTypename}");
                TestContext.WriteLine($"[探针] 写回后 field = {after.managedReferenceFieldTypename}");

                Assert.That(after.managedReferenceValue, Is.EqualTo(typeof(ProbeShape)),
                    "写进去的 System.Type 留得住（新开一个 SerializedObject 也读得到）。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **清空**一个托管引用槽位：赋 <c>null</c> 与置 <c>RefIdNull</c> **两条路都通**。
        /// </summary>
        /// <remarks>
        /// 一条类型选择器要用到的边角——「None」那一项得把槽位清掉。
        /// <para>
        /// <b>写这条时先踩了一次坑：</b>上一版把 null 赋在**另一个</b> <see cref="SerializedObject"/>
        /// 的句柄上、却拿原来那个 <c>so</c> 去 <c>ApplyModifiedProperties</c>——于是「清不掉」，
        /// 还差点被记成 Unity 的行为。**判据是同一个 SerializedObject 上的句柄**，
        /// 这条注释留着就是为了下次不再把它误读成一条 Unity 的怪癖。
        /// </para>
        /// </remarks>
        [Test]
        public void 清空托管引用槽位两条路都通()
        {
            var target = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            try
            {
                var so = new SerializedObject(target);
                so.FindProperty("managedTypeField").managedReferenceValue = typeof(ProbeShape);
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                // 第一次尝试：赋 null。
                var prop = so.FindProperty("managedTypeField");
                prop.managedReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                var afterNull = new SerializedObject(target).FindProperty("managedTypeField");
                TestContext.WriteLine($"[探针] 赋 null 之后：{(afterNull.managedReferenceValue == null ? "已清空" : "仍有值（清不掉）")}");
                Assert.That(afterNull.managedReferenceValue, Is.Null, "赋 null 能清空。");

                // 第二次尝试：走 id。
                var prop2 = so.FindProperty("managedTypeField");
                prop2.managedReferenceId = ManagedReferenceUtility.RefIdNull;
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                var afterId = new SerializedObject(target).FindProperty("managedTypeField");
                TestContext.WriteLine($"[探针] 置 RefIdNull 之后：{(afterId.managedReferenceValue == null ? "已清空" : "仍有值")}");

                Assert.That(afterId.managedReferenceValue, Is.Null, "走 managedReferenceId 能清空。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **测量**：类型槽位的 <c>hasVisibleChildren</c> 是什么——决定它会不会被多态管线展开成
        /// 「RuntimeType 的空壳子树」。
        /// </summary>
        /// <remarks>
        /// 类型槽位的「具体类型」是 <c>System.RuntimeType</c>（见上一条测量）。它若报有可见子级，
        /// 本包多态展开判据的最后一条腿（<c>hasVisibleChildren</c>）就会放行，
        /// 之后是一条按 <c>RuntimeType</c> 解析的空壳子树。
        /// </remarks>
        [Test]
        public void 类型槽位报不报可见子级()
        {
            var target = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            try
            {
                var so = new SerializedObject(target);
                so.FindProperty("managedTypeField").managedReferenceValue = typeof(ProbeShape);
                so.ApplyModifiedPropertiesWithoutUndo();

                var prop = new SerializedObject(target).FindProperty("managedTypeField");
                TestContext.WriteLine(
                    $"[探针] 类型槽位：{Describe(prop)}，hasVisibleChildren = {prop.hasVisibleChildren}；"
                    + $"managedReferenceValue 的运行时类 = {prop.managedReferenceValue?.GetType().FullName}");

                Assert.That(
                    prop.hasVisibleChildren,
                    Is.False,
                    "实测为假 ⇒ 类型槽位不会被展开成空壳子树。这条若翻红（Unity 换了行为），"
                    + "要在 NestedMemberExpansion.IsCompositeCandidate 的多态支加一道"
                    + "「值是 System.Type 就不展开」的前置。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **测量**：没 <c>Apply</c> 的改动，活不活得过一次 <c>Update()</c>。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 决定「菜单回调里的写回」要不要**当场提交**。菜单回调发生在绘制周期之外，
        /// 宿主（Inspector 路径是 <c>Update → Draw → Apply</c>）管不到它；若改动活不过下一帧开头的
        /// <c>Update()</c>，那么既有三个菜单写回（<c>[ValueDropdown]</c> 两条通道、
        /// <c>[AssetSelector]</c>、<c>[AssetList]</c>）就有同一个隐患——本批**只报不修**。
        /// </para>
        /// <para>普通字段与类型槽位各测一格：两者的属性形态不同（<c>Integer</c> 对 <c>ManagedReference</c>）。</para>
        /// </remarks>
        [Test]
        public void 未提交的改动活不活得过Update()
        {
            var target = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            target.plainArray = new[] { 1 };
            try
            {
                var so = new SerializedObject(target);

                so.FindProperty("plainArray").arraySize = 2;
                so.FindProperty("managedTypeField").managedReferenceValue = typeof(ProbeShape);

                // **不 Apply**，只 Update——下一帧宿主开头就是这样。
                so.Update();

                var size = so.FindProperty("plainArray").arraySize;
                var type = so.FindProperty("managedTypeField").managedReferenceValue as Type;
                TestContext.WriteLine(
                    $"[探针] Update 之后（未 Apply）：arraySize = {size}（设的是 2）；"
                    + $"类型槽位 = {type?.Name ?? "null"}（设的是 typeof(ProbeShape)）");

                Assert.That(
                    size,
                    Is.EqualTo(1),
                    "**实测：Update() 会丢掉未 Apply 的改动**（arraySize 回到 1）"
                    + "⇒ 绘制周期之外的写回（菜单回调）必须**当场**用设值的那个 SerializedObject 调 "
                    + "ApplyModifiedProperties。**推论（未实测）**：既有三个菜单写回"
                    + "（[ValueDropdown] 两条通道 / [AssetSelector] / [AssetList]）若在绘制周期之外执行，"
                    + "会有同一个隐患——需要一次手动确认，本批只报不修。");
                Assert.That(
                    type,
                    Is.Null,
                    "类型槽位同理：未 Apply 的托管引用改动也被 Update() 丢掉了。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// 多态引用的写回**进撤销栈**——「若把它纳进管线，那五件事还在不在」里最先要问的一件。
        /// </summary>
        /// <remarks>
        /// 判据照本仓既有的撤销用例写法：先记一步已知可撤销的，再撤一次看收回的是哪一步。
        /// </remarks>
        [Test]
        public void 多态引用的写回进撤销栈()
        {
            var target = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            target.shape = new ProbeShape { hp = 1 };
            try
            {
                var so = new SerializedObject(target);

                Undo.RecordObject(target, "L7 probe");
                so.FindProperty("shape").managedReferenceValue = new ProbeShape { hp = 2 };
                so.ApplyModifiedProperties();

                Undo.PerformUndo();

                var after = new SerializedObject(target).FindProperty("shape").managedReferenceValue as ProbeShape;
                Assert.That(after, Is.Not.Null, "撤销之后槽位不该空掉。");
                Assert.That(after.hp, Is.EqualTo(1), "撤销要把多态引用的值换回旧的那个实例。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 探针三：字典与矩阵

        /// <summary>
        /// 字典、多维数组、交错数组**都不进序列化数据**——这是「字段进不了树」的直接证据，
        /// 也是 <c>[DictionaryDrawerSettings]</c> / <c>[TableMatrix]</c> 判「L7 前置」的依据。
        /// </summary>
        [Test]
        public void 字典与矩阵不进序列化数据()
        {
            var target = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            try
            {
                var so = new SerializedObject(target);

                TestContext.WriteLine($"[探针] Dictionary：{Describe(so.FindProperty("dict"))}");
                TestContext.WriteLine($"[探针] 多维数组：{Describe(so.FindProperty("grid"))}");
                TestContext.WriteLine($"[探针] 交错数组：{Describe(so.FindProperty("jagged"))}");
                TestContext.WriteLine($"[探针] 对照（普通 List）：{Describe(so.FindProperty("plainList"))}");
                TestContext.WriteLine($"[探针] 对照（一维数组）：{Describe(so.FindProperty("plainArray"))}");

                Assert.That(so.FindProperty("dict"), Is.Null, "Unity 不序列化字典。");
                Assert.That(so.FindProperty("grid"), Is.Null, "Unity 不序列化多维数组。");
                Assert.That(so.FindProperty("jagged"), Is.Null, "Unity 不序列化交错数组。");

                Assert.That(so.FindProperty("plainList"), Is.Not.Null, "对照：List 是序列化的。");
                Assert.That(so.FindProperty("plainArray"), Is.Not.Null, "对照：一维数组是序列化的。");
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        #endregion

        #region 探针四：进管线的三条行为（各自决定一个设计点）

        /// <summary>
        /// **测量**：自引用的多态引用在 <c>NextVisible</c> 下**停不停得下来**。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 展开的守卫要防的是「深」还是「根本停不下来」——这两件事的处置完全不同：
        /// 前者加深度预算就够，后者必须有一道**硬刹车**，否则构建期就无限递归。
        /// </para>
        /// <para>
        /// <b>实测结论：是后者。</b>自引用的多态子树在 <c>NextVisible</c> 下**是无限的**
        /// （本用例走满 5000 的上限）。环没有被 Unity 切断——回边照样 <c>hasVisibleChildren</c>。
        /// 故枚举带硬上限：真无限时这条**不会挂死**，而是把「撞到上限」当成结论记下来。
        /// </para>
        /// <para>
        /// 递归类型会触发 Unity 自己的「Serialization depth limit」告警（那是类型的账、
        /// 不是本包的），故这里临时 <c>ignoreFailingMessages</c>，并把收到的警告记进日志。
        /// </para>
        /// </remarks>
        [Test]
        public void 自引用的多态引用在枚举下停不停得下来()
        {
            const int cap = 5000;
            var target = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            var notices = new List<string>();

            void Handler(string condition, string stackTrace, LogType type)
            {
                notices.Add($"{type}: {condition}");
            }

            var previous = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += Handler;
            try
            {
                var node = new ProbeNode { id = 0 };
                node.next = node; // 自引用
                target.node = node;

                var so = new SerializedObject(target);
                var root = so.FindProperty("node");
                Assert.That(root, Is.Not.Null, "自引用的多态字段照样在序列化数据里。");

                // **整棵子树**走：每一跳都带 enterChildren，否则只走了兄弟那一圈——
                // 那样量到的是「node 有几个字段」，不是「环有没有铺开」。
                // （第一版就是这么写错的：`enterChildren = false` 让它只走了 2 个属性，
                //   而回边自己的 hasVisibleChildren 其实是 true。）
                var visited = 0;
                var maxDepth = 0;
                var cursor = root.Copy();
                var end = cursor.GetEndProperty();
                while (visited < cap
                       && cursor.NextVisible(true)
                       && !SerializedProperty.EqualContents(cursor, end))
                {
                    visited++;
                    maxDepth = Math.Max(maxDepth, cursor.depth);
                }

                var backEdge = so.FindProperty("node.next");

                TestContext.WriteLine(
                    $"[探针] 自引用枚举：走过 {visited} 个属性（上限 {cap}），最大 depth = {maxDepth}");
                TestContext.WriteLine(
                    $"[探针] 回边 node.next：{Describe(backEdge)}，hasVisibleChildren = {backEdge?.hasVisibleChildren}");
                TestContext.WriteLine(
                    $"[探针] 期间 Unity 报了 {notices.Count} 条日志"
                    + (notices.Count > 0 ? "，第一条：" + notices[0] : string.Empty));

                Assert.That(backEdge, Is.Not.Null, "回边自己还是一个属性。");
                Assert.That(backEdge.hasVisibleChildren, Is.True,
                    "回边**有**可见子级——环没有被 Unity 切断。");
                Assert.That(visited, Is.EqualTo(cap),
                    "**枚举不会自己停下来**：自引用的多态子树在 NextVisible 下是无限的（走满上限）。"
                    + "⇒ 展开的刹车是**必须**的，不是保险——没有它，构建期就无限递归。"
                    + "这也是「按具体类型的链去重」比「深度预算」更该先落地的理由：它停在第一次重复处，"
                    + "既更早、也说得清为什么。");
            }
            finally
            {
                Application.logMessageReceived -= Handler;
                LogAssert.ignoreFailingMessages = previous;
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>
        /// **测量**：多态字段在多选下的混合态——<c>hasMultipleDifferentValues</c> 与
        /// <c>managedReferenceValue</c> 分别给什么。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 这条决定「多选下展不展开」的判据。关键在**同类型不同实例**那一格：
        /// 若它也为真，<c>hasMultipleDifferentValues</c> 就不能当判据——那会把常规的
        /// 多选编辑整个挡在门外。
        /// </para>
        /// <para>
        /// 另一头是 <c>managedReferenceValue</c>：它是「取主目标那个实例」还是「混合态给 null」，
        /// 决定它能不能直接拿来当运行时类型与对账的来源（本包纪律：不拿一个目标冒充全体）。
        /// </para>
        /// </remarks>
        [Test]
        public void 多态字段在多选下的混合态()
        {
            var a = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            var b = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            try
            {
                var so = new SerializedObject(new Object[] { a, b });
                var prop = so.FindProperty("shape");
                Assert.That(prop, Is.Not.Null);

                a.shape = new ProbeShape { hp = 1 };
                b.shape = new ProbeShape { hp = 2 };
                so.Update();
                Report(prop, "同类型、不同实例");

                // **决定性的一条**：同类型、只是两个实例，它就已经报混合了 ⇒
                // 它不能当「各目标的类型是否一致」的判据，否则常规的多选编辑会整个被挡在门外。
                Assert.That(prop.hasMultipleDifferentValues, Is.True,
                    "同类型不同实例也报混合 ⇒ hasMultipleDifferentValues 不能用来判「类型一致」。");
                Assert.That(prop.managedReferenceValue, Is.SameAs(a.shape),
                    "混合态下属性给的是**主目标那个实例**——所以不能拿它冒充全体（本包既有纪律）。");

                b.shape = new ProbeDerivedShape { hp = 3 };
                so.Update();
                Report(prop, "不同具体类型");
                Assert.That(prop.hasMultipleDifferentValues, Is.True);

                b.shape = null;
                so.Update();
                Report(prop, "一个有一个空");
                Assert.That(prop.hasMultipleDifferentValues, Is.True);

                a.shape = null;
                b.shape = null;
                so.Update();
                Report(prop, "两个都空");
                Assert.That(prop.hasMultipleDifferentValues, Is.False,
                    "两个都空 ⇒ 不算混合（这是唯一「不一致却报一致」的一格）。");
                Assert.That(prop.managedReferenceValue, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        /// <summary>
        /// 补充测量：**同类型、同字段值、只是两个不同实例**时，多态槽位算不算混合态。
        /// </summary>
        /// <remarks>
        /// 上一条用例里两个实例的 <c>hp</c> 值也不同，因此严格说它没隔离出「仅实例不同」那一格。
        /// 这一条把值也调成一样：若仍报混合，则判的是**实例身份**而不是内容，
        /// 「展开的多态容器恒为单目标」成立（读路径的逐目标口径因此只是保险，不是必需）；
        /// 若报不混合，多目标展开就可能发生——那时要给 <c>PolymorphicReflectionTests</c>
        /// 补一条「多目标同值展开后逐目标各读各的」。
        /// </remarks>
        [Test]
        public void 多态槽位同类型同值不同实例算不算混合态()
        {
            var a = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            var b = ScriptableObject.CreateInstance<ManagedReferenceProbeFixture>();
            try
            {
                var so = new SerializedObject(new Object[] { a, b });
                var prop = so.FindProperty("shape");

                a.shape = new ProbeShape { hp = 7 };
                b.shape = new ProbeShape { hp = 7 };
                so.Update();
                Report(prop, "同类型、同值、不同实例");

                Assert.That(prop.hasMultipleDifferentValues, Is.True,
                    "仍报混合 ⇒ 判据是「实例身份」——展开的多态容器恒为单目标（实测）。");
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        #endregion

        #region 工具

        /// <summary>把一格的混合态写进日志，供上面的测量用例读。</summary>
        /// <param name="property">多态字段的序列化属性。</param>
        /// <param name="capture">这一格在测什么。</param>
        private static void Report(SerializedProperty property, string capture)
        {
            var value = property.managedReferenceValue;
            TestContext.WriteLine(
                $"[探针] {capture}：hasMultiple={property.hasMultipleDifferentValues}；"
                + $"managedReferenceValue={(value == null ? "null" : value.GetType().Name)}");
        }

        /// <summary>把「找没找到 / 是什么类型」写成一行，供测量用例打日志用。</summary>
        /// <param name="property">序列化属性；可以为 null。</param>
        /// <returns>一行描述。</returns>
        private static string Describe(SerializedProperty property)
        {
            return property == null
                ? "（null——不在序列化数据里）"
                : $"{property.propertyType}；isArray={property.isArray}";
        }

        #endregion
    }

    /// <summary>探针夹具：一个类型把「L7 要碰的每一类字段」各放一个。</summary>
    [HideMonoScript]
    internal sealed class ManagedReferenceProbeFixture : ScriptableObject
    {
        /// <summary>多态引用（具体声明类型）。</summary>
        [SerializeReference]
        public ProbeShape shape;

        /// <summary>多态引用（接口声明类型）。</summary>
        [SerializeReference]
        public IProbeContract contract;

        /// <summary>裸的 <c>System.Type</c> 字段。</summary>
        public Type typeField;

        /// <summary>包一层托管引用的 <c>System.Type</c> 字段。</summary>
        [SerializeReference]
        public Type managedTypeField;

        /// <summary>MonoScript 引用（对照：它进得了序列化数据）。</summary>
        public MonoScript scriptRef;

        /// <summary>字典（探针三）。</summary>
        public Dictionary<int, int> dict = new Dictionary<int, int>();

        /// <summary>多维数组（探针三）。</summary>
        public int[,] grid;

        /// <summary>交错数组（探针三）。</summary>
        public int[][] jagged;

        /// <summary>对照：普通 List。</summary>
        public List<int> plainList = new List<int>();

        /// <summary>对照：一维数组。</summary>
        public int[] plainArray;

        /// <summary>自引用的多态引用（探针四）。</summary>
        [SerializeReference]
        public ProbeNode node;
    }

    /// <summary>探针四用的自引用类型。</summary>
    [Serializable]
    internal class ProbeNode
    {
        /// <summary>一个普通字段。</summary>
        public int id;

        /// <summary>指向同类型的多态引用——自引用时枚举会怎样，是本条要测的。</summary>
        [SerializeReference]
        public ProbeNode next;
    }

    /// <summary>探针用的派生实例类型（用来构造「不同具体类型」那一格）。</summary>
    [Serializable]
    internal class ProbeDerivedShape : ProbeShape
    {
        /// <summary>派生类型独有的一格。</summary>
        public int extra;
    }

    /// <summary>探针用的契约类型。</summary>
    internal interface IProbeContract
    {
    }

    /// <summary>探针用的托管引用实例类型。</summary>
    [Serializable]
    internal class ProbeShape : IProbeContract
    {
        /// <summary>一个序列化子字段。</summary>
        public int hp;

        /// <summary>一个字符串子字段。</summary>
        public string label;
    }
}
