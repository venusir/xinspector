using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// XInspector 的 Unity 集成入口。使用方继承它即可让某类型走 XInspector 管线。
    /// <para>
    /// <b>不自动接管任何类型。</b> 为一个类型启用需要显式写它的编辑器：
    /// </para>
    /// <code>
    /// [CustomEditor(typeof(PlayerProfile))]
    /// [CanEditMultipleObjects]
    /// public class PlayerProfileEditor : XInspectorEditor
    /// {
    /// }
    /// </code>
    /// <para>
    /// 想让带 XInspector 特性的类型「自动接管」，请定义脚本宏
    /// <c>XINSPECTOR_AUTO_EDITOR</c>——它启用一个独立的门控程序集，删掉宏即完全恢复
    /// Unity 默认行为。不默认接管是刻意的：全局替换所有 Inspector 影响面太大，
    /// 且发布之后很难回头。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 基类必须写成全名 <c>UnityEditor.Editor</c>，不能简写成 <c>Editor</c>：
    /// 本文件位于 <c>XInspector.Editor</c> 命名空间内，而 <c>Editor</c> 恰好是
    /// <c>XInspector</c> 命名空间的一个成员（即本命名空间自己）。C# 的名字解析先查
    /// 外层命名空间的成员、后查 using 指令，于是简写会被解析成命名空间而非 Unity 的类型，
    /// 报「Editor 是命名空间，不能用作类型」。使用方的代码只要也处在某个以
    /// <c>.Editor</c> 结尾的命名空间里，就会遇到同一个坑——这里显式全名，兼作示范。
    /// </remarks>
    public abstract class XInspectorEditor : UnityEditor.Editor
    {
        #region Public API

        /// <summary>
        /// 本编辑器持有的属性树。
        /// </summary>
        protected PropertyTree Tree { get; private set; }

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// 建立属性树。树在编辑器存活期间只构建一次——它的结构不随帧变化。
        /// </summary>
        protected virtual void OnEnable()
        {
            Tree = PropertyTree.Create(serializedObject);
        }

        /// <summary>
        /// 绘制 Inspector。
        /// </summary>
        /// <remarks>
        /// <c>Update</c> / <c>ApplyModifiedProperties</c> 的配对位置是 Undo、预制体覆盖、
        /// 场景标脏与域重载后取值保持的来源，不可省略：
        /// 前者把磁盘上的值拉进内存副本，后者把内存副本写回并登记 Undo。
        /// </remarks>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Tree.Draw();
            serializedObject.ApplyModifiedProperties();
        }

        #endregion
    }
}
