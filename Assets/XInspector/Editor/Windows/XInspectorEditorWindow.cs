using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 用 XInspector 管线绘制**自身序列化字段**的编辑器窗口基类。
    /// <para>
    /// 继承它、声明字段、自己写一个 <c>[MenuItem]</c> 就够了——窗口的 <c>OnGUI</c> 不必写：
    /// </para>
    /// <code>
    /// public sealed class MyWindow : XInspectorEditorWindow
    /// {
    ///     [Title("设置")] [BoxGroup("基础")] public int health = 100;
    ///     [BoxGroup("基础")]             public string name = "Player";
    ///
    ///     [MenuItem("Tools/我的窗口")]
    ///     private static void Open() =&gt; GetWindow&lt;MyWindow&gt;("我的窗口");
    /// }
    /// </code>
    /// <para>
    /// <b>窗口路径要过滤成员。</b> <see cref="UnityEditor.EditorWindow"/> 是
    /// <see cref="ScriptableObject"/>，自带 7 个带 <c>[SerializeField]</c> 的内部字段
    /// （<c>m_MinSize</c>、<c>m_Pos</c>、<c>m_ViewDataDictionary</c> 等）。基类已用
    /// <see cref="WindowMemberFilter"/> 挡掉它们，只画**声明在本基类及其派生类型上**的字段；
    /// 顺带也挡掉了 <c>m_Script</c> 槽位。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>窗口内的编辑不进 Undo。</b> 窗口字段既不属于场景也不属于资产，Unity 的 Undo 体系里
    /// 没有它的位置；若强行登记，用户按 Ctrl+Z 想撤销场景操作、撤销到的却是窗口里的一个数字。
    /// 补偿手段是 <see cref="ResetToDefaults"/>（工具栏上有一个「重置」按钮）：
    /// 把字段恢复成它们的 C# 初始值。
    /// </para>
    /// <para>
    /// <b>值随窗口布局持久化。</b> Unity 会序列化 <see cref="UnityEditor.EditorWindow"/> 的字段，
    /// 因此窗口里的改动在域重载、编辑器重启后仍在——与 Inspector 里字段的行为一致。
    /// </para>
    /// <para>
    /// <b>默认画窗口自身的序列化字段</b>（<c>public</c> 字段与 <c>[SerializeField]</c> 私有字段），
    /// 外加带 <c>[ShowInInspector]</c> 的成员——普通属性、静态成员都行。
    /// 想检视别的对象，覆写 <see cref="GetTarget"/>。
    /// </para>
    /// <para>
    /// 本类必须 <c>public</c>：Unity 经类型反射实例化 <see cref="UnityEditor.EditorWindow"/>
    /// （<c>GetWindow&lt;T&gt;</c>），非公开类型不能可靠构造。
    /// </para>
    /// </remarks>
    public abstract class XInspectorEditorWindow : UnityEditor.EditorWindow
    {
        #region Private Fields

        private PropertyTreeHost _host;

        /// <summary>窗口自身的成员过滤器。缓存起来：每帧新建一个闭包是要计入绘制路径的。</summary>
        private Func<FieldInfo, bool> _windowFilter;

        #endregion

        #region Public API

        /// <summary>
        /// 本窗口的属性树；**尚未首次绘制或重置时为 <c>null</c>**。
        /// </summary>
        /// <remarks>
        /// 树是**惰性构建**的：窗口没被画出来就不该做建树那点工作，而且
        /// <see cref="ResetToDefaults"/> 内部会临时造一个同类型实例，若 <c>OnEnable</c>
        /// 就建树，那个一次性实例会白白跑一遍。
        /// </remarks>
        protected PropertyTree Tree => _host?.Tree;

        /// <summary>
        /// 这个窗口要检视的对象。默认是**窗口自身**。
        /// </summary>
        /// <returns>被检视的对象。</returns>
        /// <remarks>
        /// <para>
        /// 覆写它就能检视**任意**对象——不必可序列化，甚至不必是
        /// <see cref="UnityEngine.Object"/>：
        /// </para>
        /// <code>
        /// protected override object GetTarget() =&gt; Selection.activeObject;
        /// </code>
        /// <para>
        /// <b>不是 Unity 对象的目标只显示带 <c>[ShowInInspector]</c> 的成员</b>，
        /// 且一律只读——那种目标没有序列化后端，写进去也无处保存。
        /// </para>
        /// <para>
        /// <b>它每帧都会被调用，必须返回稳定引用。</b> 每次返回一个新对象会让宿主每帧重建
        /// 整棵树——那是绘制路径上的建树，代价远超本意。
        /// </para>
        /// </remarks>
        protected virtual object GetTarget()
        {
            return this;
        }

        /// <summary>
        /// 把目标上的字段恢复为它们的 C# 初始值。
        /// </summary>
        /// <returns>至少重置了一项返回 <c>true</c>。</returns>
        /// <remarks>
        /// 这是「窗口内编辑不可撤销」的补偿手段，工具栏上的「重置」按钮调的就是它。
        /// 默认值的来源是一个同类型的一次性实例——它的字段还停在 C# 初始值上。
        /// <para>
        /// 目标不是 Unity 对象时恒返回 <c>false</c>：反射后端是只读的，没有写路径。
        /// 工具栏那个按钮据此置灰。
        /// </para>
        /// </remarks>
        public virtual bool ResetToDefaults()
        {
            var host = EnsureHost();
            var target = GetTarget();
            host.Attach(target, FilterFor(target));
            return host.ResetToDefaults();
        }

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// 建立宿主。**不建树**——树是惰性的，见 <see cref="Tree"/>。
        /// </summary>
        protected virtual void OnEnable()
        {
            EnsureHost();
        }

        /// <summary>
        /// 释放宿主。
        /// </summary>
        protected virtual void OnDisable()
        {
            _host?.Dispose();
            _host = null;
        }

        /// <summary>
        /// 绘制窗口。
        /// </summary>
        /// <remarks>
        /// 子类可以覆写它并调用 <c>base.OnGUI()</c> 来插入自己的内容，但更推荐覆写
        /// <see cref="DrawToolbar"/>——那里是留给窗口自己加控件的位置。
        /// </remarks>
        protected virtual void OnGUI()
        {
            var host = EnsureHost();
            var target = GetTarget();
            host.Attach(target, FilterFor(target));

            DrawToolbar();
            host.Draw(position.width);
        }

        /// <summary>
        /// 绘制窗口顶部的工具栏。
        /// </summary>
        /// <remarks>
        /// 默认只放一个右对齐的「重置」按钮。子类覆写时若想保留它，记得自行调用
        /// <see cref="ResetToDefaults"/> 或调用 <c>base.DrawToolbar()</c>。
        /// </remarks>
        protected virtual void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.FlexibleSpace();

                // 目标不是 Unity 对象时置灰：反射后端没有写路径，重置无从谈起。
                // 置灰而不是「点了没反应」——后者正是本包最忌讳的那种现象。
                using (new EditorGUI.DisabledScope(!EnsureHost().CanResetToDefaults))
                {
                    if (GUILayout.Button("重置", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    {
                        ResetToDefaults();
                    }
                }
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 取宿主，必要时创建。
        /// </summary>
        /// <returns>宿主实例。</returns>
        /// <remarks>
        /// 与 <see cref="OnEnable"/> 分开是为了让「创建宿主」与「建树」两件事解耦：
        /// 前者很便宜、随时可做，后者要等真的需要画。
        /// </remarks>
        private PropertyTreeHost EnsureHost()
        {
            if (_host == null)
            {
                _host = new PropertyTreeHost(WindowMemberFilter.For(typeof(XInspectorEditorWindow)));
            }

            return _host;
        }

        /// <summary>
        /// 按目标挑这次要用的字段过滤器。
        /// </summary>
        /// <param name="target">本次要检视的对象。</param>
        /// <returns>过滤器；全收时为 <c>null</c>。</returns>
        /// <remarks>
        /// <b>目标是窗口自身时才用窗口过滤器。</b> 那条过滤器要求「字段声明在窗口基类及其派生
        /// 类型上」——它是为「画窗口自身」设计的（挡掉 <see cref="UnityEditor.EditorWindow"/>
        /// 自带的那 7 个内部字段），套到别的目标上会把目标的字段**全部拒掉**，树是空的。
        /// 这个错误很隐蔽：不是画错，是几乎什么都不画。
        /// </remarks>
        private Func<FieldInfo, bool> FilterFor(object target)
        {
            if (!ReferenceEquals(target, this))
            {
                return null;
            }

            return _windowFilter ?? (_windowFilter = WindowMemberFilter.For(typeof(XInspectorEditorWindow)));
        }

        #endregion
    }
}
