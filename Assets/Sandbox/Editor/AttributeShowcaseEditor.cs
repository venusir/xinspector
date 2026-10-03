using UnityEditor;
using XInspector.Editor;
using XInspector.Sandbox;

namespace XInspector.Sandbox.EditorTools
{
    /// <summary>
    /// L1a 展示台的编辑器。命名空间刻意不以 <c>.Editor</c> 结尾——理由见 <c>DemoComponentEditor</c>。
    /// </summary>
    [CustomEditor(typeof(AttributeShowcase))]
    [CanEditMultipleObjects]
    public class AttributeShowcaseEditor : XInspectorEditor
    {
    }
}
