using System;

namespace XInspector
{
    /// <summary>
    /// 让一个方法**自己画一段自定义 IMGUI**，画在它在 Inspector 里出现的位置上。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用途是「把一段手写界面混进管线」——例如画一根只读的条形图、一个自定义的预览。
    /// 标了它的方法会像 <see cref="ButtonAttribute"/> 那样拿到一个方法节点，
    /// 于是位置由「字段之后、按钮之间」的那套顺序决定，条件族也照常作用于它。
    /// </para>
    /// <para>
    /// 方法必须**无参且非泛型**，否则构建期告警、该位置画一条说明而不是静默消失。
    /// 返回值会被忽略。方法体里直接写 <c>GUILayout</c>／<c>EditorGUILayout</c> 即可。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异：</b>只做**标在方法上**的无参形式。Odin 另有
    /// <c>[OnInspectorGUI("方法名")]</c> 与标在字段上（在该字段之后追加绘制）两种形式，
    /// 前者是 resolved string（本包一律不做），后者依赖 Odin 的方法命名约定。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [OnInspectorGUI]
    /// private void DrawManaBar()
    /// {
    ///     var rect = EditorGUILayout.GetControlRect();
    ///     EditorGUI.ProgressBar(rect, mana / maxMana, "法力");
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class OnInspectorGUIAttribute : Attribute
    {
    }
}
