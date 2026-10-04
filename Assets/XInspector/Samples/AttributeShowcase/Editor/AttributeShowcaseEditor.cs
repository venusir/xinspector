using UnityEditor;
using XInspector.Editor;
using XInspector.Samples;

namespace XInspector.Samples.EditorTools
{
    /// <summary>
    /// 展示台的编辑器：接入 XInspector 管线的**全部代码**就是这几行。
    /// <para>
    /// 注意命名空间刻意没有以 <c>.Editor</c> 结尾——若写成
    /// <c>XInspector.Samples.Editor</c>，那么在本文件里写 <c>Editor</c> 会被解析成
    /// 该命名空间自身而不是 <c>UnityEditor.Editor</c>（C# 先查外层命名空间成员、
    /// 后查 using 指令）。这是接入时最容易踩的一个坑，详见 <see cref="XInspectorEditor"/> 的备注。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(AttributeShowcase))]
    [CanEditMultipleObjects]
    public class AttributeShowcaseEditor : XInspectorEditor
    {
    }
}
