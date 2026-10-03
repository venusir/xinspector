using UnityEditor;
using XInspector.Editor;

namespace XInspector.Sandbox.EditorTools
{
    /// <summary>
    /// 窗口基类的最小示例。**属于工程壳，不随包分发。**
    /// <para>
    /// 存在的意义是给人看：打开它应当只看到下面声明的这几个字段，
    /// **一个 Unity 的窗口内部字段都不该出现**。
    /// </para>
    /// <para>
    /// 要盯的正是那句话。基类若哪天失去了成员过滤，
    /// <c>m_MinSize</c>、<c>m_MaxSize</c>、<c>m_TitleContent</c>、<c>m_Pos</c>、
    /// <c>m_SerializedDataModeController</c>、<c>m_ViewDataDictionary</c>、<c>m_OverlayCanvas</c>
    /// 这七个会冒出来——其中后三个还会各自展开成一整棵子树，一眼就能认出来。
    /// </para>
    /// <para>
    /// 命名空间刻意没有以 <c>.Editor</c> 结尾：那样 <c>Editor</c> 这个名字会被解析成命名空间自身
    /// 而不是 <c>UnityEditor.Editor</c>（C# 先查外层命名空间成员、后查 using 指令）。
    /// 本文件里没有裸用 <c>Editor</c>，但保持与同目录其它脚本一致的写法。
    /// </para>
    /// </summary>
    internal sealed class XInspectorWindowDemo : XInspectorEditorWindow
    {
        #region Public Fields

        /// <summary>带标题且属于外层分组的字段。</summary>
        [Title("身份")]
        [BoxGroup("基础")]
        public string playerName = "Player";

        /// <summary>
        /// 未分组字段，**刻意夹在两个分组之间**——它应当留在原位，
        /// 而不是被挤到窗口末尾。
        /// </summary>
        public int ungrouped = 1;

        /// <summary>只写了深层路径，外层分组由构建期合成。</summary>
        [BoxGroup("基础/属性")]
        public int health = 100;

        /// <summary>与上者同组。</summary>
        [BoxGroup("基础/属性")]
        public float speed = 5f;

        /// <summary>第二个顶层分组，用来把未分组字段夹在中间。</summary>
        [BoxGroup("附加")]
        public string note = "末尾的分组";

        #endregion

        #region Menu

        /// <summary>菜单入口。</summary>
        [MenuItem("Tools/XInspector/窗口演示", false, 20)]
        private static void Open()
        {
            GetWindow<XInspectorWindowDemo>("XInspector 窗口演示");
        }

        #endregion
    }
}
