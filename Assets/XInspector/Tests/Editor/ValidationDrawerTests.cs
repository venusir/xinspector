using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 校验与钳制五特性：链装配、判空规则、引用判定、钳制数学。
    /// <para>
    /// <b>测不了的</b>：提示框与警告框的真实渲染、以及「绘制后才钳制」的时序
    /// （那是绘制期行为）。钳制的判定与算术都在纯函数里，这里逐条钉住。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ValidationDrawerTests
    {
        #region Private Fields

        private ValidationFixture _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;

        #endregion

        #region Setup / Teardown

        /// <summary>建立临时资产与序列化对象。</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<ValidationFixture>();
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

            _tree = null;
            DrawerTypeRegistry.Reset();
            AttributeProcessorRegistry.Reset();
        }

        #endregion

        #region 链装配

        /// <summary>五个绘制器都配在各自的成员上，且都在末端之前。</summary>
        [Test]
        public void 五个绘制器都在链上()
        {
            AssertDrawer<RequiredDrawer>("required");
            AssertDrawer<RequiredDrawer>("customRequired");
            AssertDrawer<MinValueDrawer>("minInt");
            AssertDrawer<MaxValueDrawer>("maxFloat");
            AssertDrawer<AssetsOnlyDrawer>("assetOnly");
            AssertDrawer<SceneObjectsOnlyDrawer>("sceneOnly");
        }

        /// <summary>
        /// 校验绘制器在信息框之内、末端之外。
        /// <para>
        /// 顺序是功能性的：信息框讲「这个字段是干什么的」，校验讲「它现在有问题」——
        /// 后者贴在字段附近更合理，故权重更大（更靠内）。
        /// </para>
        /// </summary>
        [Test]
        public void 校验绘制器在信息框之内()
        {
            var property = Find("both");
            var infoBox = IndexOf<InfoBoxDrawer>(property);
            var required = IndexOf<RequiredDrawer>(property);
            var terminal = IndexOf<UnityFallbackDrawer>(property);

            Assert.That(infoBox, Is.GreaterThanOrEqualTo(0));
            Assert.That(required, Is.GreaterThanOrEqualTo(0));
            Assert.That(infoBox, Is.LessThan(required), "信息框在外、校验在内。");
            Assert.That(required, Is.LessThan(terminal));
        }

        /// <summary>钳制绘制器在末端之外、后缀之内。</summary>
        [Test]
        public void 钳制绘制器在末端之外()
        {
            var property = Find("bothClamps");
            var min = IndexOf<MinValueDrawer>(property);
            var max = IndexOf<MaxValueDrawer>(property);
            var terminal = IndexOf<UnityFallbackDrawer>(property);

            Assert.That(min, Is.GreaterThanOrEqualTo(0));
            Assert.That(max, Is.GreaterThanOrEqualTo(0));
            Assert.That(min, Is.LessThan(terminal), "下限钳制要包住值控件。");
            Assert.That(max, Is.LessThan(terminal), "上限钳制要包住值控件。");
            Assert.That(min, Is.LessThan(max), "两者同挂时下限在外（先抬后压，或反之，结果一致但顺序必须确定）。");
        }

        #endregion

        #region 判空

        /// <summary>可能为空的类型集合。</summary>
        [Test]
        public void 判空_支持的类型集合()
        {
            Assert.That(RequiredValidator.CanBeEmpty(Property("required")), Is.True, "字符串");
            Assert.That(RequiredValidator.CanBeEmpty(Property("assetOnly")), Is.True, "对象引用");
            Assert.That(RequiredValidator.CanBeEmpty(Property("list")), Is.True, "集合");
            Assert.That(RequiredValidator.CanBeEmpty(Property("minInt")), Is.False, "整数恒有值");
            Assert.That(RequiredValidator.CanBeEmpty(Property("flag")), Is.False, "bool 恒有值");
            Assert.That(RequiredValidator.CanBeEmpty(null), Is.False);
        }

        /// <summary>空的判定：null/空串/空集合为空，空白串**不算**空。</summary>
        [Test]
        public void 判空_空格不算空()
        {
            var text = Property("required");

            text.stringValue = "";
            Assert.That(RequiredValidator.IsEmpty(text), Is.True);

            text.stringValue = "   ";
            Assert.That(RequiredValidator.IsEmpty(text), Is.False, "纯空白串按非空——这是本包自定的语义。");

            text.stringValue = "x";
            Assert.That(RequiredValidator.IsEmpty(text), Is.False);

            var objectReference = Property("assetOnly");
            objectReference.objectReferenceValue = null;
            Assert.That(RequiredValidator.IsEmpty(objectReference), Is.True);

            var list = Property("list");
            list.arraySize = 0;
            Assert.That(RequiredValidator.IsEmpty(list), Is.True, "空数组为空。");
            list.arraySize = 1;
            Assert.That(RequiredValidator.IsEmpty(list), Is.False);
        }

        #endregion

        #region 引用校验

        /// <summary>
        /// 资产与场景对象的判定互斥，空引用不算违反。
        /// <para>
        /// 资产用包内的 asmdef（它必然是持久化的），场景对象现造一个 GameObject。
        /// </para>
        /// </summary>
        [Test]
        public void 引用校验_区分资产与场景对象()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>("Assets/XInspector/Runtime/Venusir.Xinspector.asmdef");
            Assert.That(asset, Is.Not.Null, "前提不成立：找不到包内 asmdef 资产。");
            Assert.That(EditorUtility.IsPersistent(asset), Is.True);

            var sceneObject = new GameObject("ValidationTestTemp");
            try
            {
                Assert.That(EditorUtility.IsPersistent(sceneObject), Is.False, "前提：新建的 GameObject 是场景对象。");

                var property = Property("assetOnly");

                property.objectReferenceValue = null;
                Assert.That(ObjectReferenceWarning.TryDescribeViolation(property, true, out _), Is.False,
                    "空引用不算违反——那是 [Required] 的职责。");

                property.objectReferenceValue = sceneObject;
                Assert.That(ObjectReferenceWarning.TryDescribeViolation(property, true, out var assetsMessage), Is.True);
                Assert.That(assetsMessage, Does.Contain(sceneObject.name));
                Assert.That(ObjectReferenceWarning.TryDescribeViolation(property, false, out _), Is.False,
                    "场景对象满足 [SceneObjectsOnly]。");

                property.objectReferenceValue = asset;
                Assert.That(ObjectReferenceWarning.TryDescribeViolation(property, true, out _), Is.False,
                    "资产满足 [AssetsOnly]。");
                Assert.That(ObjectReferenceWarning.TryDescribeViolation(property, false, out var sceneMessage), Is.True);
                Assert.That(sceneMessage, Does.Contain(asset.name));
            }
            finally
            {
                Object.DestroyImmediate(sceneObject);
            }
        }

        #endregion

        #region 钳制

        /// <summary>类型与前提判断。</summary>
        [Test]
        public void 钳制_前提判断()
        {
            var state = new PropertyState();

            Assert.That(ValueClamper.IsClampableType(Property("minInt")), Is.True);
            Assert.That(ValueClamper.IsClampableType(Property("maxFloat")), Is.True);
            Assert.That(ValueClamper.IsClampableType(Property("required")), Is.False, "字符串不钳。");
            Assert.That(ValueClamper.IsClampableType(null), Is.False);

            Assert.That(ValueClamper.CanClamp(Property("minInt"), state), Is.True);

            state.SetReadOnly(true);
            Assert.That(ValueClamper.CanClamp(Property("minInt"), state), Is.False,
                "只读字段不钳制——只读意味着这个值不归你改。");
        }

        /// <summary>多对象值不一致时跳过钳制，避免把各目标统一成同一个值。</summary>
        [Test]
        public void 钳制_多对象不一致时跳过()
        {
            var second = ScriptableObject.CreateInstance<ValidationFixture>();
            try
            {
                second.minInt = 7;

                using (var serializedObject = new SerializedObject(new Object[] { _target, second }))
                {
                    var property = serializedObject.FindProperty("minInt");
                    Assert.That(property.hasMultipleDifferentValues, Is.True, "前提：两个目标的值应当不同。");
                    Assert.That(ValueClamper.CanClamp(property, new PropertyState()), Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(second);
            }
        }

        /// <summary>下限：整数向上取整，浮点直接抬。</summary>
        [Test]
        public void 钳制_下限()
        {
            var intProperty = Property("minInt");
            intProperty.longValue = -5;
            Assert.That(ValueClamper.TryClampMin(intProperty, 2.5), Is.True);
            Assert.That(intProperty.longValue, Is.EqualTo(3), "最小合法整数是 3（向上取整）。");
            Assert.That(ValueClamper.TryClampMin(intProperty, 2.5), Is.False, "已满足时不改值、返回 false。");

            var floatProperty = Property("maxFloat");
            floatProperty.doubleValue = -1.5;
            Assert.That(ValueClamper.TryClampMin(floatProperty, 0.25), Is.True);
            Assert.That(floatProperty.doubleValue, Is.EqualTo(0.25).Within(1e-6));
        }

        /// <summary>上限：整数向下取整。</summary>
        [Test]
        public void 钳制_上限()
        {
            var intProperty = Property("minInt");
            intProperty.longValue = 99;
            Assert.That(ValueClamper.TryClampMax(intProperty, 2.5), Is.True);
            Assert.That(intProperty.longValue, Is.EqualTo(2), "最大合法整数是 2（向下取整）。");

            var floatProperty = Property("maxFloat");
            floatProperty.doubleValue = 10f;
            Assert.That(ValueClamper.TryClampMax(floatProperty, 3.5), Is.True);
            Assert.That(floatProperty.doubleValue, Is.EqualTo(3.5).Within(1e-6));
        }

        /// <summary>NaN 边界不动数据（钳到一个表达不出来的值是错的）。</summary>
        [Test]
        public void 钳制_NaN边界不动数据()
        {
            var property = Property("maxFloat");
            property.doubleValue = 1f;

            Assert.That(ValueClamper.TryClampMin(property, double.NaN), Is.False);
            Assert.That(ValueClamper.TryClampMax(property, double.NaN), Is.False);
            Assert.That(property.doubleValue, Is.EqualTo(1f));
        }

        #endregion

        #region Private Helpers

        /// <summary>按路径取成员节点；树在首次调用时构建，同一用例内复用。</summary>
        /// <param name="path">成员路径。</param>
        /// <returns>成员节点。</returns>
        private InspectorProperty Find(string path)
        {
            if (_tree == null)
            {
                _tree = PropertyTree.Create(new SerializedObject(_target));
            }

            foreach (var child in _tree.Root.Children)
            {
                if (child.Path == path)
                {
                    return child;
                }
            }

            Assert.Fail($"找不到成员 {path}。");
            return null;
        }

        /// <summary>取临时资产上某字段的序列化属性。</summary>
        /// <param name="path">字段名。</param>
        /// <returns>序列化属性。</returns>
        private SerializedProperty Property(string path)
        {
            return _serializedObject.FindProperty(path);
        }

        /// <summary>断言某成员链上有指定绘制器，且它排在末端之前。</summary>
        /// <typeparam name="T">绘制器类型。</typeparam>
        /// <param name="path">成员路径。</param>
        private void AssertDrawer<T>(string path) where T : XInspectorDrawer
        {
            var property = Find(path);
            var index = IndexOf<T>(property);
            var terminal = IndexOf<UnityFallbackDrawer>(property);

            Assert.That(index, Is.GreaterThanOrEqualTo(0), $"{path} 上应有 {typeof(T).Name}。");
            Assert.That(terminal, Is.GreaterThanOrEqualTo(0), $"{path} 上应有末端绘制器。");
            Assert.That(index, Is.LessThan(terminal), $"{path} 上 {typeof(T).Name} 应排在末端之前。");
        }

        /// <summary>在节点的链上查找指定类型绘制器的下标。</summary>
        /// <typeparam name="T">绘制器类型。</typeparam>
        /// <param name="property">目标节点。</param>
        /// <returns>下标；不存在返回 -1。</returns>
        private static int IndexOf<T>(InspectorProperty property) where T : XInspectorDrawer
        {
            var entries = property.Chain.Entries;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].Drawer is T)
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion
    }

    /// <summary>校验与钳制测试用的资产。</summary>
    internal sealed class ValidationFixture : ScriptableObject
    {
        /// <summary>必填（默认消息）。</summary>
        [Required]
        public string required = "";

        /// <summary>必填（自定义消息与级别）。</summary>
        [Required("必须填一个名字", InfoMessageType.Warning)]
        public string customRequired = "";

        /// <summary>下限钳制。</summary>
        [MinValue(0)]
        public int minInt = -5;

        /// <summary>上限钳制。</summary>
        [MaxValue(100f)]
        public float maxFloat = 50f;

        /// <summary>要求工程资产。</summary>
        [AssetsOnly]
        public Object assetOnly;

        /// <summary>要求场景对象。</summary>
        [SceneObjectsOnly]
        public Object sceneOnly;

        /// <summary>信息框 + 必填，用于顺序断言。</summary>
        [InfoBox("说明")]
        [Required]
        public string both = "";

        /// <summary>上下限同时存在，用于顺序断言。</summary>
        [MinValue(0)]
        [MaxValue(10)]
        public int bothClamps;

        /// <summary>集合判空。</summary>
        public int[] list;

        /// <summary>bool 恒有值。</summary>
        public bool flag;
    }
}
