using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
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

        #region 工具

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
