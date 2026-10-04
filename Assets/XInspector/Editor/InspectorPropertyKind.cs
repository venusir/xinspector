namespace XInspector.Editor
{
    /// <summary>
    /// 属性树节点的种类。
    /// <para>
    /// 种类决定**链条末端**接哪个绘制器（根接子节点绘制器、分组接子节点绘制器、
    /// 成员接值绘制器），因为末端是结构性的而非启发式的——见 <c>PropertyTreeBuilder</c>。
    /// </para>
    /// </summary>
    public enum InspectorPropertyKind
    {
        /// <summary>
        /// 树的根，对应被检视的那个对象本身。类级特性（如 <c>[Title]</c>）由处理器合成到它上面。
        /// </summary>
        Root = 0,

        /// <summary>
        /// 分组节点，由分组特性（如 <c>[BoxGroup]</c>）在构建期装配出来。它不对应任何真实成员。
        /// </summary>
        Group = 1,

        /// <summary>
        /// 成员节点，对应一个字段或属性。
        /// </summary>
        Member = 2,

        /// <summary>
        /// 方法节点，对应一个带 <c>[Button]</c> 的方法（<c>[OnInspectorGUI]</c> 一类同理）。
        /// <para>
        /// 它与成员节点的根本差别是**没有值**：<see cref="InspectorProperty.ValueEntry"/> 为
        /// <c>null</c>，末端接方法专用绘制器而不是值绘制器。给它单独一种 Kind 而不是复用
        /// <see cref="Member"/>，是为了让「成员路径 = 可以交给序列化系统的路径」这条不变量不被弄脏
        /// ——按路径重置默认值的逻辑只认 <see cref="Member"/>。
        /// </para>
        /// </summary>
        Method = 3,

        /// <summary>
        /// 反射成员节点，对应一个带 <c>[ShowInInspector]</c> 的普通属性或非序列化字段。
        /// <para>
        /// 它**有值**，但值不来自 Unity 的序列化系统——值入口是反射后端，只读。
        /// 与 <see cref="Method"/> 单列一类的理由相同：<see cref="Member"/> 的路径被当作
        /// 「可以交给序列化系统的路径」使用（按路径重置默认值的逻辑、按名解析另一个成员的逻辑），
        /// 反射成员在这套语义里根本找不到自己，混进去只会让那条不变量多出一堆例外。
        /// </para>
        /// </summary>
        ReflectedMember = 4,
    }
}
