using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 本文件同时 using 了 System 与 UnityEngine，裸写 Object 会在 System.Object 与
// UnityEngine.Object 之间产生二义（CS0104）。别名消歧，比在每处写全名可读。
using Object = UnityEngine.Object;

namespace XInspector.Editor
{
    /// <summary>
    /// 在**非 Inspector 上下文**里托管一棵属性树：编辑器窗口、窗口里的预览面板。
    /// <para>
    /// Inspector 那边这些事由 Unity 的 <c>Editor</c> 宿主代劳；离开 Inspector 之后，
    /// 下面这几件都得自己做，而且做错了症状都不直观：
    /// </para>
    /// <list type="bullet">
    /// <item><c>Update</c> → <c>Draw</c> → <c>ApplyModifiedProperties</c> 的配对；</item>
    /// <item><c>labelWidth</c> / <c>wideMode</c> 的设置与**还原**（窗口里不会自动来，不还原会污染
    /// 同一帧里其它窗口与 Inspector 的布局）；</item>
    /// <item>建树失败要变成**可见的错误**而不是白屏。</item>
    /// </list>
    /// <para>
    /// <b>所有权约定：宿主拥有树与序列化对象，调用方拥有目标对象。</b>
    /// 这样窗口（目标是它自己，由 Unity 拥有）与预览面板（目标是自己造的临时对象）
    /// 能共用同一套机制，而不用给宿主加一个「要不要顺手把它也销毁」的开关。
    /// </para>
    /// </summary>
    /// <remarks>
    /// <b>为什么用 <c>ApplyModifiedPropertiesWithoutUndo</c>（与
    /// <see cref="XInspectorEditor.OnInspectorGUI"/> 唯一的实质偏离）：</b>
    /// 带 Undo 的版本会往**全局** Undo 栈写记录。窗口字段既不属于场景也不属于资产，
    /// 用户按 Ctrl+Z 想撤销场景操作、撤销到的却是窗口里的一个数字。**在 Inspector 里注册 Undo
    /// 是特性，在窗口里是污染。** 代价是窗口内的编辑不可撤销，由「重置」补偿。
    /// </remarks>
    internal sealed class PropertyTreeHost : IDisposable
    {
        #region Private Fields

        private readonly Func<FieldInfo, bool> _memberFilter;

        private Object _target;
        private SerializedObject _serializedObject;
        private PropertyTree _tree;
        private bool _disposed;

        #endregion

        #region Construction

        /// <summary>
        /// 构造宿主。
        /// </summary>
        /// <param name="memberFilter">
        /// 成员过滤器，见 <see cref="PropertyTree.Create(SerializedObject, Func{FieldInfo, bool})"/>。
        /// 传 <c>null</c> 表示全收——窗口路径应传 <see cref="WindowMemberFilter"/> 产出的那个，
        /// 预览路径通常全收。
        /// </param>
        public PropertyTreeHost(Func<FieldInfo, bool> memberFilter)
        {
            _memberFilter = memberFilter;
        }

        #endregion

        #region Public API

        /// <summary>
        /// 当前托管的目标对象；未附加时为 <c>null</c>。
        /// </summary>
        public Object Target => _target;

        /// <summary>
        /// 当前的属性树；未成功附加时为 <c>null</c>。
        /// </summary>
        public PropertyTree Tree => _tree;

        /// <summary>
        /// 最近一次建树失败的可读原因；成功时为 <c>null</c>。
        /// </summary>
        /// <remarks>
        /// 暴露出来是为了可测：错误路径的行为不该只能靠肉眼看 HelpBox 来验证。
        /// </remarks>
        public string Error { get; private set; }

        /// <summary>
        /// 是否已成功附加。
        /// </summary>
        public bool IsAttached => _tree != null;

        /// <summary>
        /// 附加到一个既有对象并建树。
        /// </summary>
        /// <param name="target">目标对象；传 <c>null</c> 等价于清空。</param>
        /// <returns>建树成功返回 <c>true</c>。</returns>
        /// <remarks>
        /// <para>
        /// **幂等**：目标与当前相同则直接返回，不重建——调用方可以在每帧的绘制入口无脑调用它。
        /// 注意「相同」是按引用比较，且**失败也算处理过**：否则建树失败的宿主会每帧重试一次，
        /// 而重试的结果必然相同，只是白白抛异常。
        /// </para>
        /// <para>
        /// 建树异常在这里被捕获并转成 <see cref="Error"/>，**不向外抛**。理由：一个写坏的目标
        /// （例如 <c>[BoxGroup("")]</c> 会在构建期抛 <see cref="ArgumentException"/>）
        /// 不该让整个窗口白屏——那通常是使用方看到本插件的第一眼。错误由 <see cref="Draw"/>
        /// 画成显眼的 HelpBox，**显式可见而非静默吞掉**。
        /// </para>
        /// <para>
        /// **绘制期的异常不在此列**，也不该在这里被捕获：那类异常要修在绘制器里，
        /// 而把它们吞掉会让「画错了」变成「什么都没画」，更难查。
        /// </para>
        /// </remarks>
        public bool Attach(Object target)
        {
            if (_disposed)
            {
                return false;
            }

            if (target == null)
            {
                Clear();
                return false;
            }

            if (ReferenceEquals(_target, target))
            {
                return _tree != null;
            }

            Clear();

            _target = target;

            try
            {
                _serializedObject = new SerializedObject(target);
                _tree = PropertyTree.Create(_serializedObject, _memberFilter);

                // 窗口内编辑不进 Undo——本包对窗口的一贯约定。绘制器里的写操作
                // （眼下是按钮调用）据此不记撤销步。
                _tree.UndoEnabled = false;
                return true;
            }
            catch (Exception exception)
            {
                _serializedObject = null;
                _tree = null;
                Error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 丢弃当前的树并用同一目标重建。
        /// </summary>
        /// <remarks>
        /// 用途是「重置预览」这类操作：目标被换掉内容之后，树与序列化对象的缓存都要丢弃。
        /// 目标为 <c>null</c> 时是空操作。
        /// </remarks>
        public void Reload()
        {
            var target = _target;
            Clear();

            if (target != null)
            {
                Attach(target);
            }
        }

        /// <summary>
        /// 绘制属性树。
        /// </summary>
        /// <param name="availableWidth">可用宽度，用于推算标签占比。</param>
        public void Draw(float availableWidth)
        {
            if (Error != null)
            {
                EditorGUILayout.HelpBox($"构建属性树失败：{Error}", MessageType.Error);
                return;
            }

            if (_tree == null)
            {
                EditorGUILayout.HelpBox("尚未附加任何对象。", MessageType.Info);
                return;
            }

            var savedLabelWidth = EditorGUIUtility.labelWidth;
            var savedWideMode = EditorGUIUtility.wideMode;

            try
            {
                // Inspector 里这两项由宿主设好；窗口与预览面板里不会自动来，
                // 不设的话标签会挤成一列、或者跑到字段上方。
                EditorGUIUtility.labelWidth = Mathf.Clamp(availableWidth * 0.38f, 90f, 220f);
                EditorGUIUtility.wideMode = availableWidth >= 320f;

                _serializedObject.Update();
                _tree.Draw();
                _serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                // 必须还原：这两个是全局状态，同一帧里还有别的窗口与 Inspector 要用。
                EditorGUIUtility.labelWidth = savedLabelWidth;
                EditorGUIUtility.wideMode = savedWideMode;
            }
        }

        /// <summary>
        /// 把树上所有成员的值重置为其类型的 C# 初始值。
        /// </summary>
        /// <returns>至少重置了一项返回 <c>true</c>。</returns>
        /// <remarks>
        /// 「默认值」的来源是一个**同类型的一次性实例**——它的字段还停在 C# 初始值上。
        /// 这是「窗口内编辑不可撤销」（见类型级 remarks）的补偿手段。
        /// </remarks>
        public bool ResetToDefaults()
        {
            if (_tree == null || _target == null)
            {
                return false;
            }

            var defaults = ScriptableObject.CreateInstance(_target.GetType());
            if (defaults == null)
            {
                return false;
            }

            // 一次性对象：不进 Hierarchy、不进 Inspector、不被保存，
            // 也不被 Resources.UnloadUnusedAssets 回收（含 DontUnloadUnusedAsset）——
            // 最后一条不必要（我们立刻销毁它），但保持与预览对象同一套标志更省心。
            defaults.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                _serializedObject.Update();
                return PropertyTreeReset.Apply(PropertyTreeReset.CollectMemberPaths(_tree), _serializedObject, defaults);
            }
            finally
            {
                // 编辑模式下必须用 DestroyImmediate——Destroy 会报「may not be called from edit mode」。
                UnityEngine.Object.DestroyImmediate(defaults);
            }
        }

        /// <summary>
        /// 释放宿主：丢弃树与序列化对象。**不销毁目标对象**（所有权在调用方）。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Clear();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 丢弃树、序列化对象与目标引用，顺带清掉错误。
        /// </summary>
        /// <remarks>
        /// 树要**先释放再丢引用**：节点状态里可能有必须显式销毁的原生对象（内嵌编辑器创建的
        /// 嵌套 <c>Editor</c> 实例）。换目标（<see cref="Attach"/>）、<see cref="Reload"/>、
        /// <see cref="Dispose"/> 三条路都经这里，释放写在这一处即可全覆盖。
        /// </remarks>
        private void Clear()
        {
            _tree?.Dispose();
            _tree = null;

            if (_serializedObject != null)
            {
                _serializedObject.Dispose();
                _serializedObject = null;
            }

            _target = null;
            Error = null;
        }

        #endregion
    }
}
