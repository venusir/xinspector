using System;
using System.Reflection;
using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 属性树：一次检视所对应的完整节点树，以及绘制它的入口。
    /// <para>
    /// 树在编辑器存活期间只构建一次（<c>OnEnable</c>），之后每帧只是重绘——
    /// 节点结构不随帧变化。构建期的全部工作（反射遍历、特性收集、分组装配、链条装配）
    /// 都集中在 <see cref="PropertyTreeBuilder"/>。
    /// </para>
    /// </summary>
    public sealed class PropertyTree
    {
        #region Construction

        /// <summary>
        /// 构造树。由构建期调用。
        /// </summary>
        /// <param name="serializedObject">底层序列化对象。</param>
        /// <param name="root">根节点。</param>
        internal PropertyTree(SerializedObject serializedObject, InspectorProperty root)
        {
            SerializedObject = serializedObject ?? throw new ArgumentNullException(nameof(serializedObject));
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        #endregion

        #region Public API

        /// <summary>
        /// 从序列化对象构建属性树。
        /// </summary>
        /// <param name="serializedObject">目标序列化对象。</param>
        /// <returns>构建好的树。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="serializedObject"/> 为 <c>null</c>。</exception>
        /// <exception cref="NotSupportedException">目标对象为 <c>null</c>（如脚本丢失）。</exception>
        public static PropertyTree Create(SerializedObject serializedObject)
        {
            return Create(serializedObject, null);
        }

        /// <summary>
        /// 从序列化对象构建属性树，并**过滤掉**不被接受的成员。
        /// </summary>
        /// <param name="serializedObject">目标序列化对象。</param>
        /// <param name="memberFilter">
        /// 成员过滤器；返回 <c>false</c> 的成员**不建节点**。传 <c>null</c> 等价于全收。
        /// </param>
        /// <returns>构建好的树。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="serializedObject"/> 为 <c>null</c>。</exception>
        /// <exception cref="NotSupportedException">目标对象为 <c>null</c>（如脚本丢失）。</exception>
        /// <remarks>
        /// <para>
        /// <b>为什么需要它。</b> <see cref="UnityEditor.EditorWindow"/> 是
        /// <see cref="UnityEngine.ScriptableObject"/>，自带若干 <c>[SerializeField]</c> 内部字段
        /// （实测 7 个：<c>m_MinSize</c>、<c>m_MaxSize</c>、<c>m_TitleContent</c>、<c>m_Pos</c>、
        /// <c>m_SerializedDataModeController</c>、<c>m_ViewDataDictionary</c>、<c>m_OverlayCanvas</c>）。
        /// 不过滤的话，用本管线绘制窗口时它们会连同用户自己的字段一起画出来，其中几个还是复杂类型、
        /// 会各自展开成一整棵子树。
        /// </para>
        /// <para>
        /// <b>过滤必须是可选的，不能改默认行为。</b> Inspector 路径**刻意保留** MonoBehaviour 的
        /// <c>m_Script</c> 槽位以与原生 Inspector 逐像素一致，而 <c>m_Script</c> 声明在
        /// <see cref="UnityEngine.ScriptableObject"/> 上——任何「排除 Unity 声明的成员」式的全局规则
        /// 都会把它一并跳掉。故过滤与否由调用方决定。
        /// </para>
        /// </remarks>
        internal static PropertyTree Create(SerializedObject serializedObject, Func<FieldInfo, bool> memberFilter)
        {
            if (serializedObject == null)
            {
                throw new ArgumentNullException(nameof(serializedObject));
            }

            if (serializedObject.targetObject == null)
            {
                throw new NotSupportedException(
                    "序列化对象没有目标（通常是脚本丢失）。XInspector 无法为不存在的对象建树。");
            }

            return PropertyTreeBuilder.Build(serializedObject, memberFilter);
        }

        /// <summary>
        /// 底层的序列化对象。
        /// </summary>
        public SerializedObject SerializedObject { get; }

        /// <summary>
        /// 根节点。
        /// </summary>
        public InspectorProperty Root { get; }

        /// <summary>
        /// 绘制整棵树。
        /// </summary>
        /// <remarks>
        /// 调用方负责在此前后配对 <c>SerializedObject.Update</c> 与
        /// <c>ApplyModifiedProperties</c>——见 <see cref="XInspectorEditor.OnInspectorGUI"/>。
        /// </remarks>
        public void Draw()
        {
            Root.Draw();
        }

        #endregion
    }
}
