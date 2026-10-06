using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 属性树：一次检视所对应的完整节点树，以及绘制它的入口。
    /// <para>
    /// 树在编辑器存活期间只构建一次（<c>OnEnable</c>），之后每帧只是重绘——
    /// 节点结构不随帧变化。构建期的全部工作（反射遍历、特性收集、分组装配、链条装配）
    /// 都集中在 <see cref="PropertyTreeBuilder"/>。
    /// </para>
    /// </summary>
    public sealed class PropertyTree : IDisposable
    {
        #region Private Fields

        /// <summary>是否已释放。用来让释放整体幂等（含生命周期钩子）。</summary>
        private bool _disposed;

        #endregion

        #region Construction

        /// <summary>
        /// 构造树。由构建期调用。
        /// </summary>
        /// <param name="serializedObject">底层序列化对象；反射树没有它，传 <c>null</c>。</param>
        /// <param name="targets">目标对象列表。</param>
        /// <param name="root">根节点，其子树须已全部成形。</param>
        /// <remarks>
        /// <b>构造点必须在处理器之前</b>（见 <see cref="PropertyTreeBuilder"/> 的构建顺序）：
        /// 需要目标对象的处理器，只能经 <see cref="InspectorProperty.Owner"/> 拿到树。
        /// 因此这里要求「子树已成形」——分组装配会把新节点挂进来，那些节点不跑第一趟处理器，
        /// 但会在装配之后跑第二趟（只跑处理分组特性的那些，见
        /// <see cref="AttributeProcessorRegistry.GroupProcessors"/>）。
        /// </remarks>
        internal PropertyTree(SerializedObject serializedObject, object[] targets, InspectorProperty root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));

            SerializedObject = serializedObject;
            Targets = targets ?? Array.Empty<object>();
            AssignOwner(root, this);
        }

        #endregion

        #region Private Helpers

        /// <summary>递归回填每个节点的 <see cref="InspectorProperty.Owner"/>。</summary>
        /// <param name="node">起始节点。</param>
        /// <param name="owner">所属的树。</param>
        /// <remarks>
        /// 构建期在成员还没上树时先给每个成员**各调一次**（那时它的子树已经成形）——
        /// 嵌套子节点是收集期挂到父节点上的，而那时父节点的 <c>Owner</c> 还是
        /// <c>null</c>，<c>AddChild</c> 传播过去也是 <c>null</c>。漏了这一步的症状是
        /// 「嵌套层里需要 <c>Owner.Targets</c> 的条件与解析器全部落空」。
        /// </remarks>
        internal static void AssignOwner(InspectorProperty node, PropertyTree owner)
        {
            node.Owner = owner;

            var children = node.RawChildren;
            for (var i = 0; i < children.Count; i++)
            {
                AssignOwner(children[i], owner);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 从序列化对象构建属性树。
        /// </summary>
        /// <param name="serializedObject">目标序列化对象。</param>
        /// <returns>构建好的树。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="serializedObject"/> 为 <c>null</c>。</exception>
        /// <exception cref="NotSupportedException">目标对象为 <c>null</c>（如脚本丢失）。</exception>
        public static PropertyTree Create(SerializedObject serializedObject)
        {
            return Create(serializedObject, null);
        }

        /// <summary>
        /// 从序列化对象构建属性树，并**过滤掉**不被接受的成员。
        /// </summary>
        /// <param name="serializedObject">目标序列化对象。</param>
        /// <param name="memberFilter">
        /// 成员过滤器；返回 <c>false</c> 的成员**不建节点**。传 <c>null</c> 等价于全收。
        /// </param>
        /// <returns>构建好的树。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="serializedObject"/> 为 <c>null</c>。</exception>
        /// <exception cref="NotSupportedException">目标对象为 <c>null</c>（如脚本丢失）。</exception>
        /// <remarks>
        /// <para>
        /// <b>为什么需要它。</b> <see cref="UnityEditor.EditorWindow"/> 是
        /// <see cref="UnityEngine.ScriptableObject"/>，自带若干 <c>[SerializeField]</c> 内部字段
        /// （实测 7 个：<c>m_MinSize</c>、<c>m_MaxSize</c>、<c>m_TitleContent</c>、<c>m_Pos</c>、
        /// <c>m_SerializedDataModeController</c>、<c>m_ViewDataDictionary</c>、<c>m_OverlayCanvas</c>）。
        /// 不过滤的话，用本管线绘制窗口时它们会连同用户自己的字段一起画出来，其中几个还是复杂类型、
        /// 会各自展开成一整棵子树。
        /// </para>
        /// <para>
        /// <b>过滤必须是可选的，不能改默认行为。</b> Inspector 路径**刻意保留** MonoBehaviour 的
        /// <c>m_Script</c> 槽位以与原生 Inspector 逐像素一致，而 <c>m_Script</c> 声明在
        /// <see cref="UnityEngine.ScriptableObject"/> 上——任何「排除 Unity 声明的成员」式的全局规则
        /// 都会把它一并跳掉。故过滤与否由调用方决定。
        /// </para>
        /// </remarks>
        internal static PropertyTree Create(SerializedObject serializedObject, Func<FieldInfo, bool> memberFilter)
        {
            if (serializedObject == null)
            {
                throw new ArgumentNullException(nameof(serializedObject));
            }

            if (serializedObject.targetObject == null)
            {
                throw new NotSupportedException(
                    "序列化对象没有目标（通常是脚本丢失）。XInspector 无法为不存在的对象建树。");
            }

            return PropertyTreeBuilder.Build(serializedObject, serializedObject.targetObjects, memberFilter);
        }

        /// <summary>
        /// 从一个**任意对象**构建属性树——它不必是 <see cref="UnityEngine.Object"/>，
        /// 也不必可序列化。
        /// </summary>
        /// <param name="target">目标对象。</param>
        /// <returns>构建好的树。</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="target"/> 为 <c>null</c>，或是一个已被销毁的 Unity 对象。
        /// </exception>
        /// <remarks>
        /// <para>
        /// <b>这种树只画带 <c>[ShowInInspector]</c> 的成员。</b> 目标不是 Unity 对象时没有
        /// <see cref="SerializedObject"/>，于是没有「Unity 会序列化什么」这回事——
        /// 自动收一批 public 成员会制造出「Inspector 里靠序列化、窗口里靠可见性」两套语义。
        /// 想让它出现，就标上。
        /// </para>
        /// <para>
        /// <b>名字里带 <c>Reflected</c> 而不是做成 <c>Create(object)</c> 重载</b>：
        /// 目标可以为 <c>null</c>，那就与 <see cref="Create(SerializedObject)"/> 撞成二义，
        /// 现有调用 <c>Create(null)</c> 会直接编译不过。
        /// </para>
        /// </remarks>
        public static PropertyTree CreateReflected(object target)
        {
            return CreateReflected(target, null);
        }

        /// <summary>
        /// 从一个任意对象构建属性树，并**过滤掉**不被接受的成员。
        /// </summary>
        /// <param name="target">目标对象。</param>
        /// <param name="memberFilter">字段过滤器，语义同 <see cref="Create(SerializedObject, Func{FieldInfo, bool})"/>。</param>
        /// <returns>构建好的树。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> 为 <c>null</c> 或已被销毁。</exception>
        internal static PropertyTree CreateReflected(object target, Func<FieldInfo, bool> memberFilter)
        {
            if (!TargetObjects.IsAlive(target))
            {
                throw new ArgumentNullException(
                    nameof(target),
                    "目标对象为 null（或是一个已被销毁的 Unity 对象），XInspector 无法为不存在的对象建树。");
            }

            return PropertyTreeBuilder.Build(null, new[] { target }, memberFilter);
        }

        /// <summary>
        /// 底层的序列化对象；**反射树为 <c>null</c>**。
        /// </summary>
        /// <remarks>
        /// 为 <c>null</c> 时这棵树的目标不是 Unity 对象：没有 Undo、没有多对象编辑、
        /// 也没有「域重载后取值」，值只可能来自 <c>[ShowInInspector]</c> 那条反射通道。
        /// </remarks>
        public SerializedObject SerializedObject { get; }

        /// <summary>
        /// 根节点。
        /// </summary>
        public InspectorProperty Root { get; }

        /// <summary>
        /// 本树正在检视的全部目标对象（多选时不止一个）。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 构造时取一次并缓存：目标集合在树的存活期内不会变（选中项一变，编辑器就重建整棵树）。
        /// 保持数组形态是为了把它原样交给 <c>Undo.RecordObjects</c>——多选下的一次点击
        /// 只该产生一步撤销，那就得一次传整个数组。
        /// </para>
        /// <para>
        /// <b>类型是 <c>object[]</c> 而不是 <c>Object[]</c>：</b> 反射树的目标可以是任意对象。
        /// 序列化路径下传进来的实例**仍然是运行时的 <c>Object[]</c>**（
        /// <see cref="UnityEditor.SerializedObject.targetObjects"/> 本来就返回它，协变不复制），
        /// 故 <see cref="MethodInvoker"/> 用一个 <c>is Object[]</c> 判断就能零分配地保住 Undo。
        /// 代价是各处判空不能再裸写 <c>!= null</c>——见 <see cref="TargetObjects"/>。
        /// </para>
        /// </remarks>
        internal object[] Targets { get; }

        /// <summary>
        /// 绘制器发起的写操作是否记入 Undo。默认 <c>true</c>；窗口路径由宿主置为 <c>false</c>。
        /// </summary>
        /// <remarks>
        /// 「窗口内编辑不进 Undo」是本包对窗口的一贯约定：窗口是工具面板，
        /// 它上面的改动不该混进场景的撤销栈里。按钮调用也是写操作，同样受这条约束——
        /// 否则在窗口里点一下，场景里按 Ctrl+Z 会撤销到一个用户看不见的地方。
        /// </remarks>
        internal bool UndoEnabled { get; set; } = true;

        /// <summary>
        /// 本树的生命周期钩子（<c>[OnInspectorInit]</c> 一族）。由构建期收集，可为 <c>null</c>。
        /// </summary>
        internal TreeLifecycleHooks Lifecycle { get; set; }

        /// <summary>
        /// 建了元素层的集合节点，供每趟绘制之前的对账使用（见 <see cref="CollectionElementSync"/>）。
        /// </summary>
        /// <remarks>
        /// 登记制而不是每趟走树找：走树是 O(全部节点)，而名单只有几个元素、比较也只是读一次
        /// <c>arraySize</c>——这条路径每帧（其实是每个 GUI 事件）都要跑。
        /// 只有**真的建了层**的集合会进来，故绝大多数工程里它是空的。
        /// <b>写路径只有 <see cref="AddElementCollection"/> 与
        /// <see cref="UnregisterElementLayersIn"/> 两条</b>——「祖先恒先于后代」（DFS 先序）
        /// 这条不变量由那两条维持，见 <see cref="CollectionElementSync.ReconcileAll"/>。
        /// </remarks>
        internal List<InspectorProperty> ElementCollections { get; } = new List<InspectorProperty>();

        /// <summary>
        /// 登记一个建了元素层的集合（幂等）。
        /// </summary>
        /// <param name="collection">集合节点。</param>
        /// <remarks>
        /// 幂等是保险而不是必需：今天的调用路径不会重复登记，但 <c>CreateElementLayer</c>
        /// 既服务构建期也服务重建期，「不重复」该由代码说、而不是由调用方记。
        /// </remarks>
        internal void AddElementCollection(InspectorProperty collection)
        {
            if (!ElementCollections.Contains(collection))
            {
                ElementCollections.Add(collection);
            }
        }

        /// <summary>
        /// 递归注销一棵子树里登记过的元素层（那棵子树即将被释放）。
        /// </summary>
        /// <param name="node">子树根（含它自己）。</param>
        /// <remarks>
        /// <b>必须在该子树被 <see cref="DisposeNode"/> 之前调用</b>：释放会把节点状态袋
        /// <c>Reset</c> 清空，之后再也认不出谁带过元素层，条目就成了强引用作废子树的僵尸。
        /// 判据是**结构遍历**而不是路径前缀——<c>data[1]</c> 与 <c>data[10]</c> 的前缀比较
        /// 会误伤，而遍历的代价与紧跟着的释放同阶。
        /// </remarks>
        internal void UnregisterElementLayersIn(InspectorProperty node)
        {
            if (node == null)
            {
                return;
            }

            if (node.State.Get<CollectionElementLayerState>() != null)
            {
                ElementCollections.Remove(node);
            }

            var children = node.RawChildren;
            for (var i = 0; i < children.Count; i++)
            {
                UnregisterElementLayersIn(children[i]);
            }
        }

        /// <summary>
        /// 绘制整棵树。
        /// </summary>
        /// <remarks>
        /// 调用方负责在此前后配对 <c>SerializedObject.Update</c> 与
        /// <c>ApplyModifiedProperties</c>——见 <see cref="XInspectorEditor.OnInspectorGUI"/>。
        /// </remarks>
        public void Draw()
        {
            // [OnStateUpdate] 每趟 GUI 布局跑一次。判据取 Layout 趟而不是「每次 Draw」：
            // 一帧里布局与重绘各 Draw 一次，按 Draw 计数会跑两遍。
            // 没有 GUI 上下文时（测试、批处理）Event.current 为 null，自然跳过。
            if (Lifecycle?.Update != null && Event.current != null &&
                TreeLifecycle.ShouldRunStateUpdate(Event.current.type))
            {
                RunStateUpdate();
            }

            // 元素层对账：集合的元素节点必须与 arraySize 一致——不一致就整层重建
            // （撤销、外部改动、以及我们自己的增删都靠这一下收敛）。
            //
            // **放在最前面**：搜索过滤会从任意一个 ShouldDraw 惰性触发、整棵子树走树，
            // 可能先于集合绘制器读到元素节点；「元素层在本趟内有效」必须是先于**所有**
            // 消费者的前置条件。理由与「为什么只比长度就够」见 CollectionElementSync。
            CollectionElementSync.ReconcileAll(this);

            Root.Draw();
        }

        /// <summary>
        /// 跑一次 <c>[OnStateUpdate]</c> 钩子。
        /// </summary>
        /// <remarks>
        /// 单独开一个入口是为了可测：绘制路径要 GUI 上下文，而「钩子有没有被调用、调了几次」
        /// 是可无头验证的。
        /// </remarks>
        internal void RunStateUpdate()
        {
            TreeLifecycle.InvokeAll(Lifecycle?.Update, Targets, "OnStateUpdate");
        }

        /// <summary>
        /// 释放树：递归复位每个节点的状态。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>只管节点状态，不管序列化对象。</b> <see cref="SerializedObject"/> 的所有权在调用方
        /// ——Inspector 路径上它是 Unity 给的（<c>Editor.serializedObject</c>），宿主路径上由
        /// <see cref="PropertyTreeHost"/> 自己建、自己释放。在这里顺手释放它会变成双重释放。
        /// </para>
        /// <para>
        /// 复位状态会释放其中实现 <see cref="IDisposable"/> 的附加状态——内嵌编辑器持有的
        /// 嵌套 <c>Editor</c> 实例就是靠这条路径销毁的。因此树一旦释放就不该再绘制：
        /// 状态已空，重建出来的也不是原来那些。
        /// </para>
        /// <para>
        /// 重复调用是幂等的（第二次遍历到的袋已清空，无事可做）。
        /// </para>
        /// </remarks>
        public void Dispose()
        {
            // 重复调用是幂等的，钩子也必须跟着幂等——否则「顺手多释放一次」会让
            // 用户的清理逻辑跑两遍。状态复位本身幂等（袋已清空），钩子得自己把关。
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // 先跑钩子再复位状态：`[OnInspectorDispose]` 想看一眼自己的状态时，
            // 那份状态还该在。
            TreeLifecycle.InvokeAll(Lifecycle?.Dispose, Targets, "OnInspectorDispose");

            DisposeNode(Root);
        }

        /// <summary>
        /// 递归复位一个节点及其全部子节点的状态。
        /// </summary>
        /// <param name="node">起始节点。</param>
        /// <remarks>
        /// <para>
        /// 手写递归而非走 <see cref="InspectorProperty.Children"/>：那是个只读列表接口，
        /// 而这里与构建期一样可以直接用内部列表，少一层接口调用。
        /// </para>
        /// <para>
        /// 元素层的重建也调它（<c>PropertyTreeBuilder.RebuildElementLayer</c>）：
        /// 被丢掉的元素子树里的 <c>IDisposable</c> 状态必须释放，语义与整树释放完全相同。
        /// </para>
        /// </remarks>
        internal static void DisposeNode(InspectorProperty node)
        {
            node.State.Reset();

            var children = node.RawChildren;
            for (var i = 0; i < children.Count; i++)
            {
                DisposeNode(children[i]);
            }
        }

        #endregion
    }
}
