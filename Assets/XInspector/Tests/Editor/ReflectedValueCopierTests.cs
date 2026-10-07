using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// <c>[ValueDropdown]</c> 的反射写回通道：托管值 → <see cref="SerializedProperty"/>。
    /// <para>
    /// 断两件事：**接受的类型确实写进去了**，以及**拒绝时目标原样不动**。后者比前者要紧
    /// ——「点了没反应」一眼看得出，「改成了别的东西」得比对半天。
    /// </para>
    /// <para>
    /// 枚举的判据与 <see cref="SerializedValueCopier"/> 同源，故这里有一条
    /// 「同名同序的**不同**枚举可以写回」——它钉的正是「判据是名字，不是类型本身」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ReflectedValueCopierTests
    {
        #region Private Fields

        private ReflectedValueCopierFixture _target;
        private SerializedObject _serializedObject;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产与序列化对象。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ReflectedValueCopierFixture>();
            _serializedObject = new SerializedObject(_target);
        }

        /// <summary>销毁临时资产并复位静态门面。</summary>
        [TearDown]
        public void TearDown()
        {
            _serializedObject?.Dispose();
            _serializedObject = null;

            if (_target != null)
            {
                Object.DestroyImmediate(_target);
                _target = null;
            }

            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 接受面

        /// <summary>整数族：可以在 <c>long</c> 里表达的每一种都收。</summary>
        [Test]
        public void 写回各种整型()
        {
            AssertAssign((sbyte)-3, "integer", -3L);
            AssertAssign((byte)3, "integer", 3L);
            AssertAssign((short)-300, "integer", -300L);
            AssertAssign((ushort)300, "integer", 300L);
            AssertAssign(42, "integer", 42L);
            AssertAssign(42u, "integer", 42L);
            AssertAssign(42L, "integer", 42L);
            AssertAssign(42L, "wide", 42L);
        }

        /// <summary>布尔只收布尔。</summary>
        [Test]
        public void 写回布尔()
        {
            AssertAssign(true, "boolean", true);
        }

        /// <summary>浮点收 <c>float</c> 与 <c>double</c>。</summary>
        [Test]
        public void 写回浮点()
        {
            AssertAssign(1.5f, "number", 1.5f);
            AssertAssign(2.5d, "number", 2.5f);
        }

        /// <summary>字符串；空值写成空串（Unity 本来就把 null 存成空串）。</summary>
        [Test]
        public void 写回字符串()
        {
            AssertAssign("hello", "text", "hello");
            AssertAssign(null, "text", string.Empty);
        }

        /// <summary>颜色收 <c>Color</c> 与 <c>Color32</c>（后者是 Unity 定义的隐式转换）。</summary>
        [Test]
        public void 写回颜色()
        {
            AssertAssign(Color.red, "color", Color.red);

            _serializedObject.FindProperty("color").colorValue = Color.black;
            Assert.That(
                ReflectedValueCopier.TryAssign(
                    new Color32(255, 0, 0, 255), _serializedObject.FindProperty("color"), typeof(Color), out var reason),
                Is.True,
                reason);
            Assert.That(_serializedObject.FindProperty("color").colorValue.r, Is.EqualTo(1f).Within(0.001f));
        }

        /// <summary>对象引用写的是引用本身。</summary>
        [Test]
        public void 写回对象引用()
        {
            var go = new GameObject("copier-target");
            try
            {
                AssertAssign(go, "gameReference", go);
                Assert.That(
                    Property("gameReference").objectReferenceValue,
                    Is.SameAs(go),
                    "指向同一个对象。");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>枚举按**成员名**定位后写序号。</summary>
        [Test]
        public void 写回枚举()
        {
            Assert.That(
                ReflectedValueCopier.TryAssign(
                    ReflectedCopierEnum.Hard, Property("enumeration"), typeof(ReflectedCopierEnum), out var reason),
                Is.True,
                reason);
            Assert.That(Property("enumeration").enumValueIndex, Is.EqualTo(2), "Hard 是第三个成员。");
        }

        /// <summary>向量族、矩形、包围盒、四元数按精确结构体类型写。</summary>
        [Test]
        public void 写回结构体族()
        {
            AssertAssign(new Vector2(1f, 2f), "vector2", new Vector2(1f, 2f));
            AssertAssign(new Vector3(1f, 2f, 3f), "vector3", new Vector3(1f, 2f, 3f));
            AssertAssign(new Vector4(1f, 2f, 3f, 4f), "vector4", new Vector4(1f, 2f, 3f, 4f));
            AssertAssign(new Rect(1f, 2f, 3f, 4f), "rect", new Rect(1f, 2f, 3f, 4f));
            AssertAssign(new Bounds(Vector3.one, Vector3.one), "bounds", new Bounds(Vector3.one, Vector3.one));
            AssertAssign(Quaternion.identity, "quaternion", Quaternion.identity);
        }

        /// <summary>声明类型未知（<c>object</c>）时跳过对象引用那一层校验，其余照常。</summary>
        [Test]
        public void 声明类型未知时跳过对象引用校验()
        {
            var go = new GameObject("copier-unknown");
            try
            {
                Assert.That(
                    ReflectedValueCopier.TryAssign(go, Property("gameReference"), null, out var reason),
                    Is.True,
                    reason);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        #endregion

        #region 拒绝面

        /// <summary>类型对不上：拒绝，且**什么都不写**。</summary>
        [Test]
        public void 类型不符拒绝且什么都不写()
        {
            Assert.That(Property("integer").longValue, Is.EqualTo(7), "夹具的初值。");

            AssertReject("不能互转", 42, "boolean");
            AssertReject("不能互转", "42", "integer");
            AssertReject("不能互转", 1.5f, "integer", "整型不收浮点（本包不做数值互转）。");
            AssertReject("不能互转", true, "integer", "布尔也不当 0/1 用。");
            AssertReject("不能互转", 5UL, "integer", "ulong 表达不了，宁可拒绝。");
            AssertReject("不能互转", ReflectedCopierEnum.Easy, "integer", "枚举不当整数写。");
            AssertReject("不能互转", 1, "enumeration", "整数也不当枚举写。");
            AssertReject("不能互转", new Vector2(1f, 2f), "vector3");
        }

        /// <summary>非 Unity 对象写不进对象引用字段。</summary>
        [Test]
        public void 非Unity对象写不进对象引用()
        {
            AssertReject("不是 Unity 对象", new ReflectedCopierPoco(), "reference");
            AssertReject("不是 Unity 对象", "Assets/foo.prefab", "reference");
        }

        /// <summary>是 Unity 对象，但赋不进声明类型更窄的字段。</summary>
        [Test]
        public void 对象引用声明类型不符拒绝()
        {
            var go = new GameObject("copier-narrow");
            try
            {
                // 控制项：同一个值写进声明类型相符的字段是成功的。
                AssertAssign(go.transform, "typedReference", go.transform);

                Assert.That(
                    ReflectedValueCopier.TryAssign(go.transform, Property("gameReference"), typeof(GameObject), out var reason),
                    Is.False);
                Assert.That(reason, Does.Contain("Transform"));
                Assert.That(reason, Does.Contain("GameObject"));
                Assert.That(Property("gameReference").objectReferenceValue, Is.Null, "拒绝时必须原样不动。");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>两个**不同的**枚举：成员名对不上，拒绝。</summary>
        [Test]
        public void 不同枚举拒绝()
        {
            AssertReject("成员名或顺序不一致", ReflectedCopierOtherEnum.Nightmare, "enumeration");
        }

        /// <summary>
        /// 两个**类型不同但成员名与顺序相同**的枚举可以写回——判据是名字，不是类型本身。
        /// </summary>
        /// <remarks>
        /// 这条与 <see cref="SerializedValueCopier"/> 的判据同源：那条通道也只能比名字
        /// （<c>SerializedProperty</c> 给不出枚举的 CLR 类型），两边口径必须一致。
        /// </remarks>
        [Test]
        public void 同名同序的不同枚举可以写回()
        {
            Assert.That(
                ReflectedValueCopier.TryAssign(
                    ReflectedCopierTwinEnum.Normal, Property("enumeration"), typeof(ReflectedCopierEnum), out var reason),
                Is.True,
                reason);
            Assert.That(Property("enumeration").enumValueIndex, Is.EqualTo(1));
        }

        /// <summary><c>[Flags]</c> 的组合值没有单一成员名，按序号写会写错——拒绝。</summary>
        [Test]
        public void 枚举组合值没有单一成员名时拒绝()
        {
            var combined = ReflectedCopierFlags.First | ReflectedCopierFlags.Second;

            Assert.That(
                ReflectedValueCopier.TryAssign(combined, Property("flags"), typeof(ReflectedCopierFlags), out var reason),
                Is.False);
            Assert.That(reason, Does.Contain("组合"));
            Assert.That(Property("flags").enumValueIndex, Is.EqualTo(0), "拒绝时必须原样不动。");

            // 控制项：单个具名成员是收的——不放行的话上面那条「拒绝」就没有说服力。
            Assert.That(
                ReflectedValueCopier.TryAssign(
                    ReflectedCopierFlags.Second, Property("flags"), typeof(ReflectedCopierFlags), out var okReason),
                Is.True,
                okReason);
        }

        /// <summary>空值只在字符串、对象引用与托管引用（清空）几格放行，别处一律拒绝。</summary>
        [Test]
        public void 空值只在字符串对象引用与托管引用放行()
        {
            AssertReject("空值", null, "integer");
            AssertReject("空值", null, "boolean");
            AssertReject("空值", null, "enumeration");
            AssertReject("空值", null, "vector2");
        }

        /// <summary>已销毁的 Unity 对象按「空」写——它按 Unity 的语义本来就是空。</summary>
        [Test]
        public void 已销毁的Unity对象按空写()
        {
            var go = new GameObject("copier-destroyed");
            Property("gameReference").objectReferenceValue = go;
            _serializedObject.ApplyModifiedPropertiesWithoutUndo();

            Object.DestroyImmediate(go);

            Assert.That(
                ReflectedValueCopier.TryAssign(go, Property("gameReference"), typeof(GameObject), out var reason),
                Is.True,
                reason);
            Assert.That(Property("gameReference").objectReferenceValue, Is.Null);
        }

        /// <summary>目标为空（反射成员没有序列化后端）时判失败并给原因，不抛。</summary>
        [Test]
        public void 目标为空时判失败()
        {
            Assert.That(ReflectedValueCopier.TryAssign(1, null, typeof(int), out var reason), Is.False);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
        }

        #endregion

        #region 类型槽位（托管引用）

        /// <summary>类型槽位写的是「那只 <c>Type</c> 实例」本身，且落盘、读得回来。</summary>
        /// <remarks>
        /// 断言用**引用比较**而不是 <c>Is.EqualTo(typeof(…))</c>：失败时 NUnit 会格式化那个值，
        /// 而托管引用恢复出来的对象一旦状态不干净，格式化会**把编辑器整个搞崩**
        /// （同类崩溃实测踩过一次，见「类型槽位写回进撤销栈」的 remarks）。
        /// </remarks>
        [Test]
        public void 写回类型槽位()
        {
            Assert.That(
                ReflectedValueCopier.TryAssign(
                    typeof(ReflectedCopierPoco), Property("chosenType"), typeof(Type), out var reason),
                Is.True,
                reason);

            _serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var after = new SerializedObject(_target).FindProperty("chosenType").managedReferenceValue;
            Assert.That(
                ReferenceEquals(after, typeof(ReflectedCopierPoco)),
                Is.True,
                "写进去的类型留得住（新开一个 SerializedObject 也读得到，且是同一个实例）。");
        }

        /// <summary>清空：空值是类型槽位唯一收的「非类型」。</summary>
        [Test]
        public void 清空类型槽位()
        {
            Property("chosenType").managedReferenceValue = typeof(ReflectedCopierEnum);
            _serializedObject.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                ReflectedValueCopier.TryAssign(null, Property("chosenType"), typeof(Type), out var reason),
                Is.True,
                reason);

            _serializedObject.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                new SerializedObject(_target).FindProperty("chosenType").managedReferenceValue,
                Is.Null,
                "空值把槽位清干净。");
        }

        /// <summary>
        /// 非类型值写不进**类型槽位**——声明类型（<c>System.Type</c>）装不下它。
        /// </summary>
        /// <remarks>
        /// 判据与多态槽位**同一条**（<c>declaredType.IsInstanceOfType(value)</c>，第二十九批合并）：
        /// 装不下的原因一律是「声明类型装不下」，不再按槽位种类分两种说法。
        /// </remarks>
        [Test]
        public void 非类型值写不进类型槽位()
        {
            AssertReject("声明类型", new ReflectedCopierPoco(), "chosenType");
            AssertReject("声明类型", 42, "chosenType");
        }

        /// <summary>类型值写不进**非类型**的托管引用槽位——声明类型装不下一只 <c>Type</c>。</summary>
        [Test]
        public void 类型值写不进非类型槽位()
        {
            var destination = Property("chosenPoco");

            Assert.That(
                ReflectedValueCopier.TryAssign(
                    typeof(ReflectedCopierPoco), destination, DeclaredTypeOf("chosenPoco"), out var reason),
                Is.False);
            Assert.That(reason, Does.Contain("声明类型"));
            Assert.That(destination.managedReferenceValue, Is.Null, "拒绝时必须原样不动。");
        }

        /// <summary>
        /// **实测并钉住**：类型槽位的写回刻意**不进撤销栈**——撤一次收回的是**先前那一步**。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 为什么刻意不进：值是一只 <c>System.Type</c> 的托管引用，**撤销恢复不回来**——
        /// 撤销真的要把这一格写回去时（槽位与快照不同），恢复出来的引用**不可用**：
        /// 实测读到过不洁的 <c>RuntimeType</c> 对象、也见过读它抛 <c>NotSupportedException</c>，
        /// 甚至有一次 NUnit 想格式化它把**编辑器整个搞崩**。状态不确定，故**不给它钉用例**
        /// （给未定义行为钉断言只会得到 flaky 测试）；登记撤销反而会让 Ctrl+Z 主动去恢复一只
        /// 不洁的对象——不登记则 Ctrl+Z 跳过这一步，两害相权取其轻。
        /// </para>
        /// <para>
        /// <b>挡不住的那一半：</b> <c>Undo.RecordObject</c> 记的是**整个对象**的状态，
        /// 故同对象上其它撤销的记录仍会尝试恢复这张槽位——那是 Unity 的限制，
        /// 本批只记录、不修（见 Pipeline 与 README）。
        /// </para>
        /// <para>
        /// 已知可撤销的一步记在**另一个资产**上：记在同一对象上会把这次写回一并回滚。
        /// </para>
        /// </remarks>
        [Test]
        public void 类型槽位写回刻意不进撤销栈()
        {
            var other = ScriptableObject.CreateInstance<ReflectedValueCopierFixture>();
            try
            {
                // 先记一步已知可撤销的（撤销栈因此非空、行为确定；且在别的对象上，撤不到我们）。
                Undo.RecordObject(other, "已知可撤销的一步");
                other.integer = 99;

                Assert.That(
                    TypeSlotWrite.TryWrite(
                        typeof(ReflectedCopierPoco), Property("chosenType"), typeof(Type), out var reason),
                    Is.True,
                    reason);

                Undo.PerformUndo();

                Assert.That(other.integer, Is.EqualTo(7), "撤一次收回的是**先前那一步**——说明它才是栈顶。");
                Assert.That(
                    ReferenceEquals(Property("chosenType").managedReferenceValue, typeof(ReflectedCopierPoco)),
                    Is.True,
                    "我们的写回没进撤销栈：撤销动不了它。");
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        #endregion

        #region 多态槽位（实例）

        /// <summary>
        /// 多态槽位写的是**一个实例**：匹配声明类型就收，且落盘、读回来是同一个活实例。
        /// </summary>
        /// <remarks>
        /// 与类型槽位共用**同一条判据**（<c>declaredType.IsInstanceOfType(value)</c>）——
        /// 这个特性是第二十九批放宽的那一格（此前托管引用只认 <c>System.Type</c>）。
        /// </remarks>
        [Test]
        public void 多态槽位接受匹配的新实例()
        {
            var instance = new ReflectedCopierPoco { value = 3 };

            Assert.That(
                ReflectedValueCopier.TryAssign(
                    instance, Property("chosenPoco"), typeof(ReflectedCopierPoco), out var reason),
                Is.True,
                reason);

            _serializedObject.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                new SerializedObject(_target).FindProperty("chosenPoco").managedReferenceValue,
                Is.SameAs(instance),
                "写进去的是那个活实例。");
        }

        /// <summary>实例装不进声明类型时拒绝，且**什么都不写**。</summary>
        [Test]
        public void 不匹配的实例写不进多态槽位()
        {
            var destination = Property("chosenPoco");

            Assert.That(
                ReflectedValueCopier.TryAssign(
                    "不是那个类型", destination, typeof(ReflectedCopierPoco), out var reason),
                Is.False);
            Assert.That(reason, Does.Contain("声明类型"));
            Assert.That(destination.managedReferenceValue, Is.Null, "拒绝时必须原样不动。");
        }

        #endregion

        #region Private Helpers

        /// <summary>按名取目标的序列化属性。</summary>
        /// <param name="name">字段名。</param>
        /// <returns>属性。</returns>
        private SerializedProperty Property(string name)
        {
            var found = _serializedObject.FindProperty(name);

            Assert.That(found, Is.Not.Null, $"夹具里没有字段 {name}。");
            return found;
        }

        /// <summary>改一个字段的值，并断言结果。</summary>
        /// <typeparam name="T">值的类型。</typeparam>
        /// <param name="value">要写入的值。</param>
        /// <param name="field">目标字段名。</param>
        /// <param name="expected">期望读回的值。</param>
        private void AssertAssign<T>(object value, string field, T expected)
        {
            var destination = Property(field);
            var declaredType = DeclaredTypeOf(field);

            Assert.That(ReflectedValueCopier.TryAssign(value, destination, declaredType, out var reason), Is.True, reason);
            Assert.That(ValueOf<T>(destination), Is.EqualTo(expected));
        }

        /// <summary>按名取夹具里那个字段的声明类型。</summary>
        /// <param name="field">字段名。</param>
        /// <returns>声明类型。</returns>
        private Type DeclaredTypeOf(string field)
        {
            var info = _target.GetType().GetField(field);

            Assert.That(info, Is.Not.Null, $"夹具里没有字段 {field}。");
            return info.FieldType;
        }

        /// <summary>断言一次写入被拒绝、给了含关键词的原因，且目标**原样不动**。</summary>
        /// <param name="keyword">原因里该出现的关键词。</param>
        /// <param name="value">要写入的值。</param>
        /// <param name="field">目标字段名。</param>
        /// <param name="why">给断言的一句说明（可选）。</param>
        private void AssertReject(string keyword, object value, string field, string why = null)
        {
            var destination = Property(field);
            var declaredType = DeclaredTypeOf(field);
            var before = Snapshot(destination);

            Assert.That(
                ReflectedValueCopier.TryAssign(value, destination, declaredType, out var reason),
                Is.False,
                why);
            Assert.That(reason, Does.Contain(keyword));
            Assert.That(Snapshot(destination), Is.EqualTo(before), "拒绝时目标必须原样不动。");
        }

        /// <summary>按属性类型读回一个值。</summary>
        /// <typeparam name="T">期望的类型。</typeparam>
        /// <param name="property">属性。</param>
        /// <returns>读回的值。</returns>
        private static T ValueOf<T>(SerializedProperty property)
        {
            var value = property.propertyType switch
            {
                SerializedPropertyType.Integer => (object)property.longValue,
                SerializedPropertyType.Boolean => property.boolValue,
                SerializedPropertyType.Float => property.floatValue,
                SerializedPropertyType.String => property.stringValue,
                SerializedPropertyType.Color => property.colorValue,
                SerializedPropertyType.ObjectReference => property.objectReferenceValue,
                SerializedPropertyType.Vector2 => property.vector2Value,
                SerializedPropertyType.Vector3 => property.vector3Value,
                SerializedPropertyType.Vector4 => property.vector4Value,
                SerializedPropertyType.Rect => property.rectValue,
                SerializedPropertyType.Bounds => property.boundsValue,
                SerializedPropertyType.Quaternion => property.quaternionValue,
                _ => null,
            };

            Assert.That(value, Is.Not.Null, $"读不出 {property.propertyType} 的值。");
            return (T)value;
        }

        /// <summary>把属性当前的值取成一个可比较的快照。</summary>
        /// <param name="property">属性。</param>
        /// <returns>快照文本。</returns>
        private static string Snapshot(SerializedProperty property)
        {
            return string.Concat(
                property.propertyType.ToString(),
                "|",
                property.propertyType switch
                {
                    SerializedPropertyType.Integer => property.longValue.ToString(),
                    SerializedPropertyType.Boolean => property.boolValue.ToString(),
                    SerializedPropertyType.Float => property.floatValue.ToString("R"),
                    SerializedPropertyType.String => property.stringValue,
                    SerializedPropertyType.ObjectReference => property.objectReferenceValue?.name ?? "(null)",
                    SerializedPropertyType.ManagedReference => property.managedReferenceValue?.ToString() ?? "(null)",
                    SerializedPropertyType.Enum => property.enumValueIndex.ToString(),
                    SerializedPropertyType.Color => property.colorValue.ToString(),
                    SerializedPropertyType.Vector2 => property.vector2Value.ToString(),
                    SerializedPropertyType.Vector3 => property.vector3Value.ToString(),
                    SerializedPropertyType.Vector4 => property.vector4Value.ToString(),
                    SerializedPropertyType.Rect => property.rectValue.ToString(),
                    SerializedPropertyType.Bounds => property.boundsValue.ToString(),
                    SerializedPropertyType.Quaternion => property.quaternionValue.ToString(),
                    _ => "(未快照)",
                });
        }

        #endregion
    }

    /// <summary>写回通道的夹具：目标侧每种类型各一个字段。</summary>
    internal sealed class ReflectedValueCopierFixture : ScriptableObject
    {
        /// <summary>整型目标（<c>int</c>）。</summary>
        public int integer = 7;

        /// <summary>更宽的整型目标。</summary>
        public long wide = 7;

        /// <summary>布尔目标。</summary>
        public bool boolean;

        /// <summary>浮点目标。</summary>
        public float number;

        /// <summary>字符串目标。</summary>
        public string text = "ini";

        /// <summary>颜色目标。</summary>
        public Color color = Color.black;

        /// <summary>声明类型很宽的引用目标——「非 Unity 对象」那条用它。</summary>
        public Object reference;

        /// <summary>声明类型是 <c>Transform</c> 的引用目标。</summary>
        public Transform typedReference;

        /// <summary>声明类型是 <c>GameObject</c> 的引用目标——窄引用那条用它。</summary>
        public GameObject gameReference;

        /// <summary>枚举目标。</summary>
        public ReflectedCopierEnum enumeration;

        /// <summary>标志位枚举目标。</summary>
        public ReflectedCopierFlags flags;

        /// <summary>向量目标。</summary>
        public Vector2 vector2;

        /// <summary>向量目标。</summary>
        public Vector3 vector3;

        /// <summary>向量目标。</summary>
        public Vector4 vector4;

        /// <summary>矩形目标。</summary>
        public Rect rect;

        /// <summary>包围盒目标。</summary>
        public Bounds bounds;

        /// <summary>四元数目标。</summary>
        public Quaternion quaternion;

        /// <summary>类型槽位：<c>[SerializeReference]</c> 包的 <c>System.Type</c>——类型选择器写回的目标。</summary>
        [SerializeReference]
        public Type chosenType;

        /// <summary>托管引用，但**不是**类型槽位——「类型值写不进非类型槽位」那条用它。</summary>
        [SerializeReference]
        public ReflectedCopierPoco chosenPoco;
    }

    /// <summary>目标枚举。</summary>
    internal enum ReflectedCopierEnum
    {
        /// <summary>第一个成员。</summary>
        Easy = 0,

        /// <summary>第二个成员。</summary>
        Normal = 1,

        /// <summary>第三个成员。</summary>
        Hard = 2,
    }

    /// <summary>与 <see cref="ReflectedCopierEnum"/> **成员名与顺序相同**的另一个枚举。</summary>
    internal enum ReflectedCopierTwinEnum
    {
        /// <summary>第一个成员。</summary>
        Easy = 0,

        /// <summary>第二个成员。</summary>
        Normal = 1,

        /// <summary>第三个成员。</summary>
        Hard = 2,
    }

    /// <summary>与 <see cref="ReflectedCopierEnum"/> 名字对不上的枚举。</summary>
    internal enum ReflectedCopierOtherEnum
    {
        /// <summary>第一个成员。</summary>
        Easy = 0,

        /// <summary>第二个成员。</summary>
        Normal = 1,

        /// <summary>第三个成员——名字与目标那份不同。</summary>
        Nightmare = 2,
    }

    /// <summary>带标志位的枚举：组合值没有单一成员名。</summary>
    [Flags]
    internal enum ReflectedCopierFlags
    {
        /// <summary>一个都不是。</summary>
        None = 0,

        /// <summary>第一位。</summary>
        First = 1,

        /// <summary>第二位。</summary>
        Second = 2,
    }

    /// <summary>非 Unity 对象的载体——「写不进对象引用字段」那条用它。</summary>
    internal sealed class ReflectedCopierPoco
    {
        /// <summary>随便一个字段，让它不是空类。</summary>
        public int value = 1;
    }
}
