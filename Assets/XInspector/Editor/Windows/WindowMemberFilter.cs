using System;
using System.Reflection;

namespace XInspector.Editor
{
    /// <summary>
    /// 窗口路径的成员过滤规则：**只收声明在指定窗口基类及其派生类型上的字段**。
    /// <para>
    /// <b>为什么需要它。</b> <see cref="UnityEditor.EditorWindow"/> 是
    /// <see cref="UnityEngine.ScriptableObject"/>，自带若干带 <c>[SerializeField]</c> 的内部字段
    /// ——实测 7 个：<c>m_MinSize</c>、<c>m_MaxSize</c>、<c>m_TitleContent</c>、<c>m_Pos</c>、
    /// <c>m_SerializedDataModeController</c>、<c>m_ViewDataDictionary</c>、<c>m_OverlayCanvas</c>。
    /// 它们全都声明在 <see cref="UnityEditor.EditorWindow"/> 或更上层，因此本规则一句就能挡掉；
    /// 其中后三个还是复杂类型，不挡的话会各自展开成一整棵子树。
    /// </para>
    /// <para>
    /// <b>为什么不能做成全局默认。</b> Inspector 路径**刻意保留** MonoBehaviour 的 <c>m_Script</c>
    /// 槽位，以与原生 Inspector 逐像素一致；而 <c>m_Script</c> 同样声明在 Unity 的基类上。
    /// 任何「排除 Unity 声明的成员」式的全局规则都会把它一并跳掉。故本规则只用于窗口路径，
    /// 由调用方显式传入，<see cref="PropertyTree.Create(UnityEditor.SerializedObject)"/> 的
    /// 默认行为一字不改。
    /// </para>
    /// <para>
    /// <b>顺带解决 <c>m_Script</c>。</b> 窗口里本就不该出现脚本槽位，而它声明在
    /// <see cref="UnityEngine.ScriptableObject"/> 上，被同一条规则排除——不需要任何特例代码。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 基类类型是**参数**而不是写死为 <c>XInspectorEditorWindow</c>：那条规则的判据是
    /// 「声明类型是否可赋给某个基类」，与具体是哪个基类无关。把它做成参数，规则就能在
    /// 窗口基类尚未存在时被独立测试，测试夹具也不必真的继承本包的窗口基类。
    /// </remarks>
    internal static class WindowMemberFilter
    {
        #region Public API

        /// <summary>
        /// 判断一个字段是否应出现在窗口的属性树里。
        /// </summary>
        /// <param name="field">字段信息；Unity 注入的成员（如 <c>m_Script</c>）为 <c>null</c>。</param>
        /// <param name="windowBaseType">窗口基类，通常是 <c>typeof(XInspectorEditorWindow)</c>。</param>
        /// <returns>应出现返回 <c>true</c>。</returns>
        /// <remarks>
        /// <paramref name="field"/> 为 <c>null</c> 时返回 <c>false</c>——那正是排除
        /// <c>m_Script</c> 这类「Unity 会序列化、但没有对应托管字段」的成员的途径。
        /// 判空由过滤器**全权负责**，上层不做额外的 null 判断，规则只有这一处真相。
        /// </remarks>
        public static bool Accepts(FieldInfo field, Type windowBaseType)
        {
            if (field == null || windowBaseType == null)
            {
                return false;
            }

            var declaringType = field.DeclaringType;
            return declaringType != null && windowBaseType.IsAssignableFrom(declaringType);
        }

        /// <summary>
        /// 取一个可直接交给 <c>PropertyTree.Create</c> 的过滤器委托。
        /// </summary>
        /// <param name="windowBaseType">窗口基类。</param>
        /// <returns>过滤器委托。</returns>
        /// <remarks>
        /// 委托在创建时闭合一次，此后每棵树只分配一个；建树发生在构建期，不在绘制路径上。
        /// </remarks>
        public static Func<FieldInfo, bool> For(Type windowBaseType)
        {
            if (windowBaseType == null)
            {
                throw new ArgumentNullException(nameof(windowBaseType));
            }

            return field => Accepts(field, windowBaseType);
        }

        #endregion
    }
}
