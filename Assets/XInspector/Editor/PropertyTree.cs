using System;
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
            if (serializedObject == null)
            {
                throw new ArgumentNullException(nameof(serializedObject));
            }

            if (serializedObject.targetObject == null)
            {
                throw new NotSupportedException(
                    "序列化对象没有目标（通常是脚本丢失）。XInspector 无法为不存在的对象建树。");
            }

            return PropertyTreeBuilder.Build(serializedObject);
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
