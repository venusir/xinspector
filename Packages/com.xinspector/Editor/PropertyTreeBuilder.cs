using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace XInspector.Editor
{
    /// <summary>
    /// 从 <see cref="SerializedObject"/> 构建属性树。
    /// <para>
    /// 这里是**值后端选择的落点**：成员遍历走 Unity 的序列化属性而非裸反射，
    /// 于是 Undo、预制体覆盖、场景标脏、多对象编辑、域重载后取值全部免费获得。
    /// 反射只用来补一件序列化系统不给的东西——成员的 <see cref="FieldInfo"/>，
    /// 以便读到它身上的特性。
    /// </para>
    /// <para>
    /// 遍历顺序即 Unity 的序列化顺序，因此渲染出来的字段次序与原生 Inspector 一致。
    /// </para>
    /// </summary>
    internal static class PropertyTreeBuilder
    {
        #region Private Fields

        // 末端绘制器无状态，可以安全共享；建一次即可，不必每个节点新建。
        private static readonly RootChildrenDrawer RootTerminal = new RootChildrenDrawer();
        private static readonly UnityFallbackDrawer MemberTerminal = new UnityFallbackDrawer();

        #endregion

        #region Public API

        /// <summary>
        /// 构建属性树。
        /// </summary>
        /// <param name="serializedObject">目标序列化对象，其 <c>targetObject</c> 须非空。</param>
        /// <returns>构建好的树。</returns>
        public static PropertyTree Build(SerializedObject serializedObject)
        {
            var targetType = serializedObject.targetObject.GetType();

            var root = new InspectorProperty(
                targetType.Name,
                string.Empty,
                targetType,
                InspectorPropertyKind.Root,
                new PropertyAttributes(CollectTypeAttributes(targetType)));

            AttachChain(root, RootTerminal);

            var iterator = serializedObject.GetIterator();

            // 与 Editor.DrawDefaultInspector 的遍历方式保持一致：首帧进入子级，
            // 之后只在同层推进。这样字段集合与顺序都与原生 Inspector 逐一对齐——
            // 本提交的验收标准正是「渲染结果与原生 Inspector 一致」。
            if (iterator.NextVisible(true))
            {
                do
                {
                    AddMember(root, serializedObject, targetType, iterator);
                }
                while (iterator.NextVisible(false));
            }

            return new PropertyTree(serializedObject, root);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 为一个可见的序列化属性建立成员节点并接上链条。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="serializedObject">底层序列化对象，用于重新取得稳定的属性实例。</param>
        /// <param name="targetType">目标对象的运行时类型。</param>
        /// <param name="serializedProperty">遍历器当前指向的序列化属性，**仅用于读取名字与路径**。</param>
        /// <remarks>
        /// <b>不能把遍历器交出去。</b> <see cref="SerializedObject.GetIterator"/> 返回的是
        /// **同一个实例**，<c>NextVisible</c> 只是就地改写它。若把它存进节点，
        /// 所有节点会共享这一个对象，走完遍历后它们全都指向最后一个属性——
        /// 症状是「Inspector 里每个字段显示的都是同一个值」。
        /// 故这里只用它读名字与路径，随后用 <see cref="SerializedObject.FindProperty"/>
        /// 取一份**独立的**实例交给值入口。
        /// </remarks>
        private static void AddMember(
            InspectorProperty root,
            SerializedObject serializedObject,
            Type targetType,
            SerializedProperty serializedProperty)
        {
            var path = serializedProperty.propertyPath;
            var name = serializedProperty.name;
            var field = FindField(targetType, path);
            var valueType = field != null ? field.FieldType : typeof(object);
            var stableProperty = serializedObject.FindProperty(path);

            var node = new InspectorProperty(
                name,
                path,
                valueType,
                InspectorPropertyKind.Member,
                new PropertyAttributes(CollectMemberAttributes(field)))
            {
                ValueEntry = new SerializedPropertyValueEntry(stableProperty, valueType),
            };

            root.AddChild(node);
            AttachChain(node, MemberTerminal);
        }

        /// <summary>
        /// 装配并挂上绘制器链。
        /// </summary>
        /// <param name="property">目标节点。</param>
        /// <param name="terminal">该节点的末端绘制器。</param>
        private static void AttachChain(InspectorProperty property, XInspectorDrawer terminal)
        {
            property.Chain = DrawerChainBuilder.Build(property, terminal);
        }

        /// <summary>
        /// 沿继承链查找字段。
        /// </summary>
        /// <param name="type">起始类型。</param>
        /// <param name="name">字段名。</param>
        /// <returns>找到的字段；未找到返回 <c>null</c>。</returns>
        /// <remarks>
        /// 必须逐层 <c>DeclaredOnly</c> 上溯：<see cref="Type.GetField(string, BindingFlags)"/>
        /// 不返回基类的私有字段，而 <c>[SerializeField]</c> 的私有字段恰恰都在各自类里声明。
        /// 找不到不是错误——<c>m_Script</c> 这类由 Unity 注入的属性就没有对应的托管字段，
        /// 此时节点仍需存在（否则渲染结果会与原生 Inspector 不一致），只是拿不到特性。
        /// </remarks>
        private static FieldInfo FindField(Type type, string name)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, flags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        /// <summary>
        /// 收集成员字段上的特性。
        /// </summary>
        /// <param name="field">成员字段，可为 <c>null</c>。</param>
        /// <returns>特性列表；无字段时为空列表。</returns>
        /// <remarks>
        /// 反射每次调用都返回**新实例**，故这里天然满足「每个属性一份独立特性」——
        /// 不会出现多个属性共享同一个特性实例、改一个串一片的问题。
        /// 类级特性的分发才需要显式克隆，见特性处理器。
        /// </remarks>
        private static List<Attribute> CollectMemberAttributes(FieldInfo field)
        {
            var result = new List<Attribute>();
            if (field == null)
            {
                return result;
            }

            foreach (var attribute in field.GetCustomAttributes(true))
            {
                if (attribute is Attribute typed)
                {
                    result.Add(typed);
                }
            }

            return result;
        }

        /// <summary>
        /// 收集类型上的特性。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <returns>特性列表。</returns>
        private static List<Attribute> CollectTypeAttributes(Type type)
        {
            var result = new List<Attribute>();

            foreach (var attribute in type.GetCustomAttributes(true))
            {
                if (attribute is Attribute typed)
                {
                    result.Add(typed);
                }
            }

            return result;
        }

        #endregion
    }
}
