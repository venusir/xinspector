using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XInspector.Editor
{
    /// <summary>
    /// 把一组成员的值重置为某个「默认来源」对象上的对应值。
    /// <para>
    /// 机制与调用方分离，是刻意的：窗口路径要重置的是**窗口自身的字段**，预览路径要重置的是
    /// **一个临时对象**，两者只有「从哪儿取值」不同。把机制抽成对两个 <see cref="SerializedObject"/>
    /// 的纯操作之后，它就能在无 GUI、甚至不需要真的开一个窗口的情况下被测试。
    /// </para>
    /// <para>
    /// <b>为什么用 <see cref="SerializedObject.CopyFromSerializedProperty"/> 而不是逐类型赋值：</b>
    /// 值可能是任意 Unity 可序列化类型（结构、数组、对象引用、嵌套）。按路径复制让 Unity 自己
    /// 处理这些差异，不必为每种类型写一遍，也不会在遇到没覆盖的类型时静默漏掉。
    /// </para>
    /// </summary>
    internal static class PropertyTreeReset
    {
        #region Private Fields

        /// <summary>
        /// Unity 用来记录「这个对象属于哪个脚本」的序列化属性名。
        /// </summary>
        /// <remarks>
        /// 它必须被排除在重置之外：默认来源是 <c>ScriptableObject.CreateInstance</c> 出来的一次性实例，
        /// 其脚本绑定是空的，把它复制回去**会清掉目标的脚本关联**——接到真实 <c>.asset</c> 上就等于
        /// 把资产弄坏。我们自己造的目标（窗口、预览对象）本来就是空绑定，所以现下无害；
        /// 但宿主是通用的，这条防护不该等到有人接真实资产时才发现。
        /// </remarks>
        private const string ScriptBindingProperty = "m_Script";

        #endregion

        #region Public API

        /// <summary>
        /// 收集一棵树上所有成员节点的序列化路径，供 <see cref="Apply"/> 使用。
        /// </summary>
        /// <param name="tree">属性树。</param>
        /// <returns>成员路径列表；<paramref name="tree"/> 为 <c>null</c> 时返回空列表。</returns>
        /// <remarks>
        /// <para>
        /// 必须**递归**：成员可能被分组节点包着，而分组节点本身不是成员。
        /// 成员的 <see cref="InspectorProperty.Path"/> 是它自己的序列化路径（不含分组前缀），
        /// 因此可以直接交给 <see cref="SerializedObject.FindProperty"/>。
        /// </para>
        /// <para>
        /// 排除脚本绑定，理由见 <see cref="ScriptBindingProperty"/>。
        /// </para>
        /// </remarks>
        public static List<string> CollectMemberPaths(PropertyTree tree)
        {
            var paths = new List<string>();

            if (tree != null)
            {
                Collect(tree.Root, paths);
            }

            return paths;
        }

        /// <summary>
        /// 按路径把 <paramref name="defaultSource"/> 上的值复制到 <paramref name="target"/>。
        /// </summary>
        /// <param name="memberPaths">要复制的序列化属性路径。</param>
        /// <param name="target">被写入的序列化对象。</param>
        /// <param name="defaultSource">默认值的来源对象，通常是同类型的一个全新实例。</param>
        /// <returns>至少复制成功一项返回 <c>true</c>；没有任何一项可复制时返回 <c>false</c>。</returns>
        /// <remarks>
        /// 路径在来源上不存在时**跳过而不抛异常**：成员集合来自属性树，而树是按
        /// <see cref="SerializedObject"/> 建的，理论上路径总是对得上；但重置是个「尽力而为」的
        /// 便利操作，为一条对不上的路径让整个操作失败没有意义。返回值让调用方能区分
        /// 「重置了」与「什么都没做」。
        /// </remarks>
        public static bool Apply(IList<string> memberPaths, SerializedObject target, Object defaultSource)
        {
            if (memberPaths == null || target == null || defaultSource == null)
            {
                return false;
            }

            var source = new SerializedObject(defaultSource);
            source.Update();

            var applied = false;

            for (var i = 0; i < memberPaths.Count; i++)
            {
                var origin = source.FindProperty(memberPaths[i]);
                if (origin == null)
                {
                    continue;
                }

                target.CopyFromSerializedProperty(origin);
                applied = true;
            }

            if (applied)
            {
                // 窗口/预览路径一律不进 Undo——见 PropertyTreeHost 的说明。
                target.ApplyModifiedPropertiesWithoutUndo();
            }

            return applied;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 递归收集成员路径。
        /// </summary>
        /// <param name="node">起始节点。</param>
        /// <param name="paths">收集到的路径。</param>
        private static void Collect(InspectorProperty node, List<string> paths)
        {
            // 元素子树整体不进名单：集合字段自己的路径已经覆盖了整份数组，而这条路在
            // **跨趟**的时机运行（工具栏按钮），那时元素层可能早已重建过若干轮——
            // 旧路径只是白跑一次 FindProperty。见 CollectionElementExpansion 的有效窗口。
            if (CollectionElementExpansion.IsElementNode(node))
            {
                return;
            }

            if (node.Kind == InspectorPropertyKind.Member &&
                !string.Equals(node.Path, ScriptBindingProperty, System.StringComparison.Ordinal))
            {
                paths.Add(node.Path);
            }

            var children = node.Children;
            for (var i = 0; i < children.Count; i++)
            {
                Collect(children[i], paths);
            }
        }

        #endregion
    }
}
