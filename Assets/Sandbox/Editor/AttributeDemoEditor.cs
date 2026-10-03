using UnityEditor;
using XInspector.Editor;
using XInspector.Sandbox;

namespace XInspector.Sandbox.EditorTools
{
    /// <summary>
    /// 接入 XInspector 管线的**全部代码**就是这几行。
    /// <para>
    /// 与 <see cref="XInspector.Sandbox.AutoTakeoverDemo"/> 对照：
    /// 那个组件没有编辑器，靠 <c>XINSPECTOR_AUTO_EDITOR</c> 宏自动接管。
    /// 两条路的渲染结果应当完全一致。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(AttributeDemo))]
    [CanEditMultipleObjects]
    public class AttributeDemoEditor : XInspectorEditor
    {
    }
}
