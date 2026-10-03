using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using XInspector.Editor;

namespace XInspector.Tests.Editor
{
    /// <summary>
    /// 窗口路径的成员过滤规则。
    /// <para>
    /// 这些用例的价值全在**真的挡住了 Unity 那批内部字段**上，所以断言直接对着
    /// <see cref="EditorWindow"/> 的真实字段做，而不是造几个假对象来演一遍规则。
    /// 造假的写法会通过，却证明不了任何事。
    /// </para>
    /// </summary>
    [TestFixture]
    public class WindowMemberFilterTests
    {
        #region Private Fields

        private const BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        #endregion

        #region 收

        /// <summary>
        /// 具体窗口类型自己声明的字段要收。
        /// </summary>
        [Test]
        public void Accepts_ConcreteWindowOwnField()
        {
            var field = typeof(FilterDerivedWindow).GetField(nameof(FilterDerivedWindow.derivedField), InstanceFields);

            Assert.That(field, Is.Not.Null, "测试夹具的字段没找到，后续断言无意义。");
            Assert.That(WindowMemberFilter.Accepts(field, typeof(FilterBaseWindow)), Is.True);
        }

        /// <summary>
        /// 用户自己写的中间基类窗口上的字段也要收——它同样在继承链上，
        /// 过滤的是「Unity 的内部字段」，不是「基类的字段」。
        /// </summary>
        [Test]
        public void Accepts_IntermediateBaseWindowField()
        {
            var field = typeof(FilterBaseWindow).GetField(nameof(FilterBaseWindow.baseField), InstanceFields);

            Assert.That(field, Is.Not.Null);
            Assert.That(WindowMemberFilter.Accepts(field, typeof(FilterBaseWindow)), Is.True);
        }

        #endregion

        #region 拒

        /// <summary>
        /// **本规则存在的全部理由**：<see cref="EditorWindow"/> 自己声明的内部字段必须全部被拒。
        /// <para>
        /// 用「枚举它所有带 <c>[SerializeField]</c> 的字段」而不是写死几个名字——
        /// Unity 增删这些字段时用例不会失效，而被保护的断言也更强。
        /// </para>
        /// </summary>
        [Test]
        public void Accepts_RejectsEverySerializedEditorWindowField()
        {
            var checkedCount = 0;

            foreach (var field in typeof(EditorWindow).GetFields(InstanceFields))
            {
                if (!HasSerializeField(field))
                {
                    continue;
                }

                checkedCount++;
                Assert.That(
                    WindowMemberFilter.Accepts(field, typeof(FilterBaseWindow)),
                    Is.False,
                    $"{nameof(EditorWindow)}.{field.Name} 是 Unity 的内部序列化字段，不该出现在窗口的属性树里。");
            }

            // 负向控制：若哪天「带 [SerializeField] 的字段」一个都枚举不到（比如反射标志写错），
            // 上面的循环会一次都不执行、用例静默通过——那种「恒真」的守卫等于没有守卫。
            Assert.That(checkedCount, Is.GreaterThan(0),
                "一个带 [SerializeField] 的 EditorWindow 字段都没枚举到，说明这条用例本身失效了。");
        }

        /// <summary>
        /// <c>m_Script</c> 一类的 Unity 注入成员没有对应的托管字段，过滤器收到 <c>null</c>，
        /// 必须判为拒——这正是窗口里不出现脚本槽位的原因。
        /// </summary>
        [Test]
        public void Accepts_RejectsNullField()
        {
            Assert.That(WindowMemberFilter.Accepts(null, typeof(FilterBaseWindow)), Is.False);
        }

        /// <summary>
        /// 与窗口基类无关的类型上的字段要拒。
        /// </summary>
        [Test]
        public void Accepts_RejectsFieldOfUnrelatedType()
        {
            var field = typeof(FilterUnrelated).GetField(nameof(FilterUnrelated.value), InstanceFields);

            Assert.That(field, Is.Not.Null);
            Assert.That(WindowMemberFilter.Accepts(field, typeof(FilterBaseWindow)), Is.False);
        }

        /// <summary>
        /// 基类传 null 时一律拒，而不是抛异常——过滤器是被上层当纯函数调用的。
        /// </summary>
        [Test]
        public void Accepts_RejectsWhenBaseTypeIsNull()
        {
            var field = typeof(FilterDerivedWindow).GetField(nameof(FilterDerivedWindow.derivedField), InstanceFields);

            Assert.That(WindowMemberFilter.Accepts(field, null), Is.False);
        }

        #endregion

        #region For

        /// <summary>
        /// <see cref="WindowMemberFilter.For"/> 产出的委托与 <c>Accepts</c> 判定一致。
        /// </summary>
        [Test]
        public void For_ProducesDelegateMatchingAccepts()
        {
            var filter = WindowMemberFilter.For(typeof(FilterBaseWindow));

            Assert.That(filter(null), Is.False);
            Assert.That(filter(typeof(FilterDerivedWindow).GetField(nameof(FilterDerivedWindow.derivedField), InstanceFields)), Is.True);
            Assert.That(filter(typeof(EditorWindow).GetField("m_MinSize", InstanceFields)), Is.False);
        }

        /// <summary>
        /// 传 null 基类时构造过滤器即报错，而不是产出一个恒假的静默过滤器。
        /// </summary>
        [Test]
        public void For_NullBaseTypeThrows()
        {
            Assert.That(() => WindowMemberFilter.For(null), Throws.ArgumentNullException);
        }

        #endregion

        #region Private Helpers

        /// <summary>判断字段是否带 <c>[SerializeField]</c>。</summary>
        /// <param name="field">字段。</param>
        /// <returns>带该特性返回 <c>true</c>。</returns>
        /// <remarks>
        /// 按类型名比较而非 <c>is SerializeField</c>：<c>SerializeField</c> 是 internal 的，
        /// 拿不到它的类型。Unity 也不在同一个程序集之外暴露它。
        /// </remarks>
        private static bool HasSerializeField(FieldInfo field)
        {
            foreach (var attribute in field.GetCustomAttributes(false))
            {
                if (attribute.GetType().Name == "SerializeField")
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// 过滤测试用的中间基类窗口——模拟「用户自己抽的一层基类窗口」。
    /// </summary>
    internal class FilterBaseWindow : EditorWindow
    {
        /// <summary>基类上的字段。</summary>
        public int baseField;
    }

    /// <summary>
    /// 过滤测试用的具体窗口。
    /// </summary>
    internal sealed class FilterDerivedWindow : FilterBaseWindow
    {
        /// <summary>具体类型上的字段。</summary>
        public int derivedField;
    }

    /// <summary>
    /// 与窗口无关的类型，用来验证规则不会误收。
    /// </summary>
    internal sealed class FilterUnrelated : ScriptableObject
    {
        /// <summary>无关字段。</summary>
        public int value;
    }
}
