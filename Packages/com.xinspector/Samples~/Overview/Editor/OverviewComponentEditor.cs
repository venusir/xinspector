using UnityEditor;
using XInspector.Editor;
using XInspector.Samples;

namespace XInspector.Samples.EditorTools
{
    /// <summary>
    /// 接入 XInspector 的**全部代码**就是这几行。
    /// <para>
    /// 注意命名空间刻意没有以 <c>.Editor</c> 结尾。若写成 <c>XInspector.Samples.Editor</c>，
    /// 那么在本文件里写 <c>Editor</c> 会被解析成该命名空间自身，而不是
    /// <c>UnityEditor.Editor</c>——C# 先查外层命名空间成员、后查 using 指令。
    /// 基类写成全名可以规避，但换个命名空间更省事。
    /// </para>
    /// </summary>
    [CustomEditor(typeof(OverviewComponent))]
    [CanEditMultipleObjects]
    public class OverviewComponentEditor : XInspectorEditor
    {
    }
}
