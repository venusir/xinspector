using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;
using Object = UnityEngine.Object;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 多态槽位的一步写回：造实例 → 写值 → 当场提交（默认**带撤销**，窗口档才关）。
    /// <para>
    /// 与 <c>TypeSlotWrite</c> 的撤销处置**刻意相反**（那条的 <c>System.Type</c> 撤销恢复不出来，
    /// 恒 <c>WithoutUndo</c>）——两条各有一个「撤一次收回的是哪一步」的用例把差异钉住。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PolymorphicSlotWriteTests
    {
        #region Fixture

        private PolymorphicSlotFixture _target;
        private SerializedObject _serializedObject;

        /// <summary>建立临时资产与序列化对象。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<PolymorphicSlotFixture>();
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

        #region 写回

        /// <summary>换类型：造一个新实例写进去，且落盘（新开一个 SerializedObject 读得回来）。</summary>
        [Test]
        public void 写回造实例并落盘()
        {
            Assert.That(
                PolymorphicSlotWrite.TryWrite(
                    typeof(SlotCircle),
                    NonDefaultConstructorPreference.ConstructIdeal,
                    null,
                    _serializedObject.FindProperty("shape"),
                    typeof(ISlotShape),
                    undoEnabled: false,
                    out var reason),
                Is.True,
                reason);

            var after = new SerializedObject(_target).FindProperty("shape").managedReferenceValue;
            Assert.That(after, Is.InstanceOf<SlotCircle>(), "写进去的是新造的那个实现。");
        }

        /// <summary>装不进声明类型的值：拒绝，且**什么都不写**。</summary>
        [Test]
        public void 不匹配时拒绝且不写()
        {
            Assert.That(
                PolymorphicSlotWrite.TryWrite(
                    typeof(string),
                    NonDefaultConstructorPreference.ConstructIdeal,
                    null,
                    _serializedObject.FindProperty("shape"),
                    typeof(ISlotShape),
                    undoEnabled: false,
                    out var reason),
                Is.False);
            Assert.That(reason, Does.Contain("声明类型"));
            Assert.That(
                _serializedObject.FindProperty("shape").managedReferenceValue,
                Is.Null,
                "拒绝时必须原样不动。");
        }

        /// <summary>造不出实例的类型（接口）：拒绝，原因来自造实例那一层。</summary>
        [Test]
        public void 造不出实例时给原因()
        {
            Assert.That(
                PolymorphicSlotWrite.TryWrite(
                    typeof(ISlotShape),
                    NonDefaultConstructorPreference.ConstructIdeal,
                    null,
                    _serializedObject.FindProperty("shape"),
                    typeof(ISlotShape),
                    undoEnabled: false,
                    out var reason),
                Is.False);
            Assert.That(reason, Does.Contain("接口"));
        }

        #endregion

        #region 自定义造实例（CreateInstanceFunction）

        /// <summary>自定义工厂被调用（拿到选中的类型），写回的是**它给的实例**。</summary>
        [Test]
        public void 自定义工厂被调用且写回它给的实例()
        {
            Type received = null;
            var made = new SlotCircle { radius = 9f };
            Func<Type, object> factory = type =>
            {
                received = type;
                return made;
            };

            Assert.That(
                PolymorphicSlotWrite.TryWrite(
                    typeof(SlotCircle),
                    NonDefaultConstructorPreference.ConstructIdeal,
                    factory,
                    _serializedObject.FindProperty("shape"),
                    typeof(ISlotShape),
                    undoEnabled: false,
                    out var reason),
                Is.True,
                reason);

            Assert.That(received, Is.EqualTo(typeof(SlotCircle)), "工厂拿到的就是选中的那个类型。");
            Assert.That(
                _serializedObject.FindProperty("shape").managedReferenceValue,
                Is.SameAs(made),
                "写进去的是工厂给的那个实例（不是内置工厂造的）。");
        }

        /// <summary>工厂返回 <c>null</c>：拒绝、**不回落内置工厂**、槽位原样不动。</summary>
        [Test]
        public void 工厂返回null时拒绝且不写()
        {
            _target.shape = new SlotSquare { side = 5f };
            _serializedObject.Update();

            Assert.That(
                PolymorphicSlotWrite.TryWrite(
                    typeof(SlotCircle),
                    NonDefaultConstructorPreference.ConstructIdeal,
                    _ => null,
                    _serializedObject.FindProperty("shape"),
                    typeof(ISlotShape),
                    undoEnabled: false,
                    out var reason),
                Is.False);
            Assert.That(reason, Does.Contain("返回了 null"));
            Assert.That(reason, Does.Contain("不回落"), "文案要说清「为什么不拿内置工厂兜底」。");
            Assert.That(
                _serializedObject.FindProperty("shape").managedReferenceValue,
                Is.InstanceOf<SlotSquare>(),
                "拒绝时必须原样不动。");
        }

        /// <summary>工厂抛异常：拒绝并给原因（异常不冒出去打断绘制）。</summary>
        [Test]
        public void 工厂抛异常时拒绝且不写()
        {
            Assert.That(
                PolymorphicSlotWrite.TryWrite(
                    typeof(SlotCircle),
                    NonDefaultConstructorPreference.ConstructIdeal,
                    _ => throw new InvalidOperationException("工厂里炸了"),
                    _serializedObject.FindProperty("shape"),
                    typeof(ISlotShape),
                    undoEnabled: false,
                    out var reason),
                Is.False);
            Assert.That(reason, Does.Contain("工厂里炸了"));
            Assert.That(_serializedObject.FindProperty("shape").managedReferenceValue, Is.Null);
        }

        /// <summary>工厂返回的实例**不是选中的类型**：拒绝（否则「显示的是 X、装进去的是 Y」）。</summary>
        [Test]
        public void 工厂返回错类型时拒绝()
        {
            Assert.That(
                PolymorphicSlotWrite.TryWrite(
                    typeof(SlotSquare),
                    NonDefaultConstructorPreference.ConstructIdeal,
                    _ => new SlotCircle(),
                    _serializedObject.FindProperty("shape"),
                    typeof(ISlotShape),
                    undoEnabled: false,
                    out var reason),
                Is.False);
            Assert.That(reason, Does.Contain("不是选中的"));
            Assert.That(_serializedObject.FindProperty("shape").managedReferenceValue, Is.Null);
        }

        #endregion

        #region 撤销两档

        /// <summary>
        /// **带撤销档**：我们的写回参与撤销——撤一次换回旧实例、旧值。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 写法照既有多态撤销探针（<c>SerializedReferenceProbeTests.多态引用的写回进撤销栈</c>）：
        /// 先在**目标**上 <c>Undo.RecordObject</c> 记一步，再写、再撤。
        /// <b>编辑器里这一步由 <c>ApplyModifiedProperties</c> 自己登记</b>（文档口径——
        /// 整套 Inspector 的撤销都靠它）；批处理测试里观察不到它的隐式登记
        ///（实测：撤到的是先前那一步——那正是窗口档那一档的行为），故用显式 RecordObject
        /// 把「值换得回来」这一半钉住。
        /// </para>
        /// <para>值按**成员值**断言而不是引用：撤销恢复出来的可能是副本。</para>
        /// </remarks>
        [Test]
        public void 带撤销的写回能撤销()
        {
            _target.shape = new SlotSquare { side = 3 };
            _serializedObject.Update();

            Undo.RecordObject(_target, "多态写回起点");

            Assert.That(
                PolymorphicSlotWrite.TryWrite(
                    typeof(SlotCircle),
                    NonDefaultConstructorPreference.ConstructIdeal,
                    null,
                    _serializedObject.FindProperty("shape"),
                    typeof(ISlotShape),
                    undoEnabled: true,
                    out var reason),
                Is.True,
                reason);

            Undo.PerformUndo();

            var after = new SerializedObject(_target).FindProperty("shape").managedReferenceValue;
            Assert.That(after, Is.InstanceOf<SlotSquare>(), "撤销把值换回旧的那个实现。");
            Assert.That(((SlotSquare)after).side, Is.EqualTo(3), "是旧实例的那份值（不是新造的默认值）。");
        }

        /// <summary>
        /// **窗口档**（<c>undoEnabled</c> 为假）：我们的写回**不进撤销栈**——
        /// 撤一次收回的是先前那一步，槽位里的新值不受影响。
        /// </summary>
        /// <remarks>
        /// 已知可撤销的一步记在**另一个资产**上：<c>Undo.RecordObject</c> 记的是**整个对象**的
        /// 状态，记在同一对象上会把我们的写回一并回滚（上一批踩过这个坑）。
        /// </remarks>
        [Test]
        public void 窗口档不进撤销栈()
        {
            _target.shape = new SlotSquare { side = 3 };
            _serializedObject.Update();

            var other = ScriptableObject.CreateInstance<PolymorphicSlotFixture>();
            try
            {
                Undo.RecordObject(other, "已知可撤销的一步");
                other.marker = 99;

                Assert.That(
                    PolymorphicSlotWrite.TryWrite(
                        typeof(SlotCircle),
                        NonDefaultConstructorPreference.ConstructIdeal,
                    null,
                        _serializedObject.FindProperty("shape"),
                        typeof(ISlotShape),
                        undoEnabled: false,
                        out var reason),
                    Is.True,
                    reason);

                Undo.PerformUndo();
                _serializedObject.Update();

                Assert.That(other.marker, Is.EqualTo(7), "撤一次收回的是**先前那一步**——说明它才是栈顶。");
                Assert.That(
                    _serializedObject.FindProperty("shape").managedReferenceValue,
                    Is.InstanceOf<SlotCircle>(),
                    "我们的写回没进撤销栈，撤销动不了它。");
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        #endregion
    }

    #region Fixtures

    /// <summary>多态写回的夹具。</summary>
    [HideMonoScript]
    internal sealed class PolymorphicSlotFixture : ScriptableObject
    {
        /// <summary>接口槽位——多态选择器的目标。</summary>
        [SerializeReference]
        public ISlotShape shape;

        /// <summary>对照字段：撤销那两条用它记「已知可撤销的一步」。</summary>
        public int marker = 7;
    }

    /// <summary>槽位类型。</summary>
    internal interface ISlotShape
    {
    }

    /// <summary>一个实现。</summary>
    internal sealed class SlotCircle : ISlotShape
    {
        /// <summary>随便一个值，便于辨认实例。</summary>
        public float radius = 1f;
    }

    /// <summary>另一个实现。</summary>
    internal sealed class SlotSquare : ISlotShape
    {
        /// <summary>随便一个值，便于辨认实例。</summary>
        public float side = 2f;
    }

    #endregion
}
