using UnityEditor;
using XInspector.Editor;
using XInspector.Sandbox;

namespace XInspector.Sandbox.EditorTools
{
    /// <summary>
    /// L0 验证台的编辑器：让它走 XInspector 管线，才能与 Demo 1（原生 Inspector）逐字段对照。
    /// <para>
    /// 命名空间刻意不以 <c>.Editor</c> 结尾——理由见 <c>DemoComponentEditor</c>。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(NativeDecoratorDemo))]
    [CanEditMultipleObjects]
    public class NativeDecoratorDemoEditor : XInspectorEditor
    {
    }
}
