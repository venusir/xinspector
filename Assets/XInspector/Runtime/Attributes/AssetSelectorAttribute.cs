using System;

namespace XInspector
{
    /// <summary>
    /// 给对象字段**前置一个小按钮**，点开是工程里的资产列表，选一个直接填进去。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 与其它值绘制器不同，它是**透传型**：画完按钮后**照常调用下一个绘制器**，
    /// 所以对象字段本身仍是 Unity 原生的那个（类型限制、拖拽、预制体覆盖全都在）。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异（两处）：</b>
    /// </para>
    /// <para>
    /// 其一，<b>弹出层是编辑器自带的菜单，不是自建窗口</b>（故这里不能写出那个类型名——
    /// Runtime 侧不引用 UnityEditor，写了会编译不过）。
    /// Odin 那个带搜索框、图标与多选；本包没有，且**不声明**它那几个只为那个窗口存在的选项
    /// （<c>DropdownTitle</c> 等）——声明成静默 no-op 是本包最想避免的现象。
    /// </para>
    /// <para>
    /// 其二，<b>只作用于单值字段</b>。Odin 那几个只对列表有意义的选项
    /// （<c>IsUniqueList</c>、<c>DrawDropdownForListElements</c>、<c>ExcludeExistingValuesInList</c>、
    /// <c>DisableListAddButtonBehaviour</c>）同样**不声明**——本包不支持数组形态，
    /// 数组要按元素画，属集合自绘那一层。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [AssetSelector]
    /// public Material anyMaterial;
    ///
    /// [AssetSelector(Paths = "Assets/Art/Materials|Assets/Art/Shared")]
    /// public Material materialFromTwoFolders;
    ///
    /// [AssetSelector(Paths = "Assets/Prefabs", Filter = "t:GameObject")]
    /// public GameObject prefab;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class AssetSelectorAttribute : Attribute
    {
        /// <summary>
        /// 在哪些目录里找。多个目录用 <c>|</c> 分隔；<c>null</c> 或空白表示整个工程。
        /// </summary>
        public string Paths { get; set; }

        /// <summary>
        /// AssetDatabase 的搜索过滤串（如 <c>"t:Material"</c>、<c>"l:MyLabel"</c>）。
        /// <c>null</c> 或空白表示不额外过滤。
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// <c>true</c> 时把树形列表**拍平成一层**（只显示资产文件名）。
        /// 默认为 <c>false</c>——与官方一致，默认是树形。
        /// </summary>
        public bool FlattenTreeView { get; set; }
    }
}
