using System;
using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 属性值的读写抽象：树只管「这个节点是谁」，值的来去由本类负责。
    /// <para>
    /// <b>它是树与序列化之间的那条缝。</b> XInspector 后续要支持「画 Unity 不序列化的东西」
    /// （如 <c>[ShowInInspector]</c> 标注的普通属性），那需要另一套后端——反射读写。
    /// 届时只需新增一个派生类，树的其余部分不动。
    /// </para>
    /// <para>
    /// <b>当前唯一的后端是 <see cref="SerializedObject"/>。</b> 这个选择买下了 Undo/Redo、
    /// 预制体覆盖、场景标脏、多对象编辑、域重载后取值这五件事——全部由 Unity 免费提供。
    /// 纯反射路线则要自己重实现一遍序列化，且这五件一件也拿不回来。
    /// 代价是 v0 只能画 Unity 会序列化的成员（public 字段与 <c>[SerializeField]</c> 私有字段）。
    /// </para>
    /// </summary>
    public abstract class PropertyValueEntry
    {
        #region Public API

        /// <summary>
        /// 值的类型。
        /// </summary>
        public abstract Type ValueType { get; }

        /// <summary>
        /// 是否由 Unity 的序列化系统支撑。
        /// </summary>
        /// <remarks>
        /// 为 <c>false</c> 时 <see cref="SerializedProperty"/> 为 <c>null</c>，
        /// 依赖它的绘制器（如 <see cref="UnityFallbackDrawer"/>）无法工作，
        /// 需要由对应的绘制器自行处理取值与绘制。
        /// </remarks>
        public abstract bool IsUnityBacked { get; }

        /// <summary>
        /// 底层序列化属性；非 Unity 支撑的后端返回 <c>null</c>。
        /// </summary>
        public abstract SerializedProperty SerializedProperty { get; }

        /// <summary>
        /// 多对象编辑时各目标的值是否不一致。
        /// </summary>
        public abstract bool HasMultipleDifferentValues { get; }

        /// <summary>
        /// 读取当前值。
        /// </summary>
        /// <returns>当前值，装箱返回。</returns>
        /// <remarks>
        /// 会装箱。它服务于少数需要「读到值再决定怎么画」的绘制器，
        /// 不是每帧每字段的必经之路——绝大多数绘制器直接用
        /// <see cref="SerializedProperty"/> 交给 <c>PropertyField</c>，那条路不经过装箱。
        /// </remarks>
        public abstract object GetValue();

        /// <summary>
        /// 写入值。
        /// </summary>
        /// <param name="value">新值。</param>
        /// <remarks>
        /// 只改内存中的序列化副本，需在绘制结束后由
        /// <c>SerializedObject.ApplyModifiedProperties</c> 统一落盘——
        /// 那一步同时也是 Undo 记录与预制体覆盖的来源。
        /// </remarks>
        public abstract void SetValue(object value);

        #endregion
    }
}
