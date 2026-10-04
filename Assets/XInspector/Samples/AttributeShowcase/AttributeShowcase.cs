using System;
using UnityEngine;
using XInspector;

namespace XInspector.Samples
{
    /// <summary>
    /// 逐特性展示台：每个特性在这里放一个，便于逐条目视。
    /// <para>
    /// 与 <see cref="OverviewComponent"/> 的分工：那个演示**最小可用形态**
    /// （类级标题 + 嵌套分组，架构的垂直切片），这个演示**逐个特性本身**，
    /// 字段按批次成组，一组一个 <c>#region</c>。
    /// </para>
    /// <para>
    /// 新增特性时在这里加一行——这是 CLAUDE.md「如何新增一个特性」的第 5 步。
    /// </para>
    /// </summary>
    [Title("XInspector 特性展示", Subtitle = "逐个特性，按批次分组")]
    [TypeInfoBox("这条信息框来自类级 [TypeInfoBox]——与 [Title] 同一机制：类级特性落在根节点，根节点的链照样包住子节点。")]
    [HideMonoScript]
    public class AttributeShowcase : MonoBehaviour
    {
        #region 状态与标签

        /// <summary>恒只读：照常显示，不可编辑。</summary>
        [ReadOnly]
        public int computedScore = 42;

        /// <summary>标签文本被替换成中文。</summary>
        [LabelText("玩家生命")]
        public int health = 100;

        /// <summary>标签文本做可读化：<c>playerScore</c> → <c>Player Score</c>。</summary>
        [LabelText("playerScore", true)]
        public int playerScore = 7;

        /// <summary>悬停标签时显示提示。</summary>
        [PropertyTooltip("每秒恢复的生命值")]
        public float regenRate = 1f;

        /// <summary>文本与提示并存——两者各管一段，互不覆盖。</summary>
        [LabelText("法力")]
        [PropertyTooltip("施放技能消耗的值")]
        public float mana = 10f;

        #endregion

        #region 布局与外观

        /// <summary>给整块染一层暖色。（用在类上会染整页——同一个绘制器落在根节点上而已。）</summary>
        [GUIColor(1f, 0.85f, 0.6f)]
        public int tinted = 1;

        /// <summary>缩进一级。</summary>
        [Indent]
        public int indented = 2;

        /// <summary>前置 12 像素的间距。</summary>
        [PropertySpace(12f)]
        public int spaced = 3;

        /// <summary>标签占 200 像素宽。</summary>
        [LabelWidth(200f)]
        public string wideLabel = "宽标签的值";

        /// <summary>撤掉标签，值占满整行。</summary>
        [HideLabel]
        public string noLabel = "撤掉标签后值会占满整行";

        /// <summary>值控件右侧画后缀。</summary>
        [SuffixLabel("秒")]
        public float duration = 1.5f;

        /// <summary>后缀叠在控件上。</summary>
        [SuffixLabel("×100%", true)]
        public float ratio = 0.75f;

        #endregion

        #region 值绘制

        /// <summary>值画成只读文本（可选中复制），不带可编辑控件。</summary>
        [DisplayAsString]
        public int computedId = 20261004;

        /// <summary>文本允许溢出：长文本折行显示全，而不是裁成一行。</summary>
        [DisplayAsString(true)]
        public string longDescription = "这一行刻意写得比较长，用来对比「裁到一行」与「折行显示全」两种模式在展示台里的差别。";

        /// <summary>bool 画成「开关在左、标签在右」——比 Unity 默认的排布更贴近一列复选框的读法。</summary>
        [ToggleLeft]
        public bool enableTracing = true;

        /// <summary>数值画成可拖动的进度条（点击/拖动条子改值）。</summary>
        [ProgressBar(0, 100)]
        public float stamina = 65f;

        /// <summary>进度条：自定义填充色 + 四段刻度 + 显示数值文本。</summary>
        [ProgressBar(0f, 1f, 0.9f, 0.45f, 0.2f, Segmented = true)]
        public float charge = 0.5f;

        /// <summary>枚举画成一排按钮（单选），而不是下拉框。</summary>
        [EnumToggleButtons]
        public DamageType damageType = DamageType.Fire;

        /// <summary><c>[Flags]</c> 枚举逐位多选。</summary>
        [EnumToggleButtons]
        public StatusFlags status = StatusFlags.Poisoned;

        /// <summary>5 行文本域（行数可调，标签画在文本域上方）。</summary>
        [MultiLineProperty(5)]
        public string notes = "多行文本示例：\n第二行。";

        /// <summary>延迟提交：输入过程中不写回，回车或失焦才提交。</summary>
        [DelayedProperty]
        public string searchFilter = "";

        /// <summary>枚举下拉框 + 前后翻页按钮（末尾自动绕回开头）。</summary>
        [EnumPaging]
        public DamageType pagedType = DamageType.Physical;

        /// <summary>滑块——只换控件，**不钳数据**（要钳请配 [MinValue]/[MaxValue]）。</summary>
        [PropertyRange(0, 100)]
        public float rangedValue = 30f;

        /// <summary>双滑块：x 是下限、y 是上限，两个把手不许交叉。</summary>
        [MinMaxSlider(0f, 100f)]
        public Vector2 hpRange = new Vector2(20f, 80f);

        /// <summary>同一条滑块 + 两个可输入的数值框（<c>showFields</c>）。</summary>
        [MinMaxSlider(-10f, 10f, true)]
        public Vector2 signedRange = new Vector2(-3f, 4f);

        /// <summary>边界取自成员（序列化 Vector2 字段，x/y 即上下限）。</summary>
        [MinMaxSlider("dynamicRange", true)]
        public Vector2 dynamicRangeValue = new Vector2(25f, 50f);

        /// <summary>上一条的边界来源——改它的 x/y，上面那条滑块的量程立刻跟着变。</summary>
        public Vector2 dynamicRange = new Vector2(0f, 60f);

        /// <summary>回绕：初始的 400 在绘制后被绕成 40（区间按半开处理）。</summary>
        [Wrap(0f, 360f)]
        public float angle = 400f;

        #endregion

        #region 校验与钳制

        /// <summary>空串 → 字段上方出现一条错误框（默认文本）。</summary>
        [Required]
        public string playerId = "";

        /// <summary>自定义消息与级别。</summary>
        [Required("必须填一个昵称", InfoMessageType.Warning)]
        public string nickname = "";

        /// <summary>纯空白串**不算**空——与上一条对比就是那条语义。</summary>
        [Required]
        public string spacesOnly = "   ";

        /// <summary>绘制后被抬到 0：初始值故意给了 -5（打开 Inspector 就会看到它被改掉）。</summary>
        [MinValue(0)]
        public int level = -5;

        /// <summary>绘制后被压到 100：初始值故意给了 150。</summary>
        [MaxValue(100f)]
        public float heat = 150f;

        /// <summary>拖一个**场景**物体进来就会看到警告（这里需要工程资产）。</summary>
        [AssetsOnly]
        public GameObject assetRef;

        /// <summary>拖一个**工程资产**进来就会看到警告（这里需要场景对象）。</summary>
        [SceneObjectsOnly]
        public GameObject sceneRef;

        #endregion

        #region 信息框

        /// <summary>恒显示的信息框。</summary>
        [InfoBox("这条信息框始终显示。", InfoMessageType.Info)]
        public int withInfoBox = 1;

        /// <summary>只有勾上开关才显示的信息框——字段本身照常绘制。</summary>
        [InfoBox("勾上开关才看得到这条警告。", InfoMessageType.Warning, nameof(showWarning))]
        public int conditionalInfoBox = 2;

        /// <summary>上一条信息框的显示条件。</summary>
        public bool showWarning;

        /// <summary>摘要一行，详情折起来。</summary>
        [DetailedInfoBox("伤害计算公式", "基础伤害 × (1 + 力量加成) × 暴击系数", InfoMessageType.None)]
        public float damage = 10f;

        #endregion

        #region 分组族

        /// <summary>竖直分组：只有容器与内边距，不画框。</summary>
        [VerticalGroup("竖列")]
        public int verticalA = 1;

        /// <summary>同分组的第二个成员——两者合进同一个容器。</summary>
        [VerticalGroup("竖列")]
        public int verticalB = 2;

        /// <summary>标题组：加粗标题 + 分隔线 + 副标题。</summary>
        [TitleGroup("标题组", "副标题画在标题下方")]
        public int titledValue = 3;

        /// <summary>折叠组：点三角收起；收起时组内内容**不画**（不是变灰）。</summary>
        [FoldoutGroup("折叠组", true)]
        public int foldoutValue = 4;

        /// <summary>折叠组的第二个成员（同组）。</summary>
        [FoldoutGroup("折叠组", true)]
        public int foldoutValue2 = 5;

        /// <summary>同路径两种分组：折叠在外、框在内——档位不是审美而是功能。</summary>
        [FoldoutGroup("折叠与框")]
        [BoxGroup("折叠与框")]
        public int foldoutOnBox = 6;

        /// <summary>标题组 + 框同路径：标题在框之外。</summary>
        [TitleGroup("标题与框")]
        [BoxGroup("标题与框")]
        public int titleOnBox = 7;

        /// <summary>水平分组：与下一个字段排成一行，各占 70% / 30%。</summary>
        [HorizontalGroup("一行", 0.7f)]
        public int wideCell = 70;

        /// <summary>同一行的第二格。</summary>
        [HorizontalGroup("一行", 0.3f)]
        public int narrowCell = 30;

        /// <summary>页签组：同组不同页签的成员分别落进各自的页，一次只显示一页。</summary>
        [TabGroup("页签", "基础")]
        public int tabHealth = 100;

        /// <summary>第一页的第二个成员。</summary>
        [TabGroup("页签", "基础")]
        public int tabMana = 50;

        /// <summary>第二页的成员。</summary>
        [TabGroup("页签", "高级")]
        public int tabDebugLevel;

        /// <summary>开关分组：标题前的复选框关掉时，组内内容**不画**。</summary>
        [ToggleGroup("showAdvanced", groupTitle: "高级选项")]
        public int advancedValue = 1;

        /// <summary>同组的第二个成员。</summary>
        [ToggleGroup("showAdvanced")]
        public int advancedTuning = 2;

        /// <summary>上面那一组的开关——**组 ID 就是它的名字**（勾上才看得到组内成员）。</summary>
        public bool showAdvanced;

        /// <summary>三个未指定宽度的格子：均分整行。</summary>
        [HorizontalGroup("三格")]
        public int cellA = 1;

        /// <summary>三格之一。</summary>
        [HorizontalGroup("三格")]
        public int cellB = 2;

        /// <summary>三格之一。</summary>
        [HorizontalGroup("三格")]
        public int cellC = 3;

        #endregion

        #region 结构与门控

        /// <summary>只读 + 强制可编辑：EnableGUI 排在只读之后，它赢。</summary>
        [ReadOnly]
        [EnableGUI]
        public int forcedEditable = 3;

        /// <summary>交回 Unity 绘制：内侧的 [Indent] **不会运行**（本条更外且不调下一个）。</summary>
        [DrawWithUnity]
        [Indent]
        public int unityDrawn = 7;

        /// <summary>只允许引用本物体之下的子物体——拖别的物体进来会看到警告。</summary>
        [ChildGameObjectsOnly]
        public GameObject childRef;

        /// <summary>字段前的开关门控它能否编辑：关掉就变灰；开关本身**永远可点**。</summary>
        [Toggle("Enabled")]
        public ToggleableSettings toggleable;

        #endregion

        #region 下拉选择

        /// <summary>选项来源——改这个数组，上面那两个下拉框的选项跟着变。</summary>
        public string[] difficultyOptions = { "简单", "普通", "困难", "噩梦" };

        /// <summary>选项里的 "/" 会**分子菜单**（与官方一致，默认就是树形）。</summary>
        public string[] treeOptions = { "武器/剑", "武器/斧", "防具/盾" };

        /// <summary>下拉框占满整行，显示当前值。</summary>
        [ValueDropdown("difficultyOptions")]
        public string difficulty = "普通";

        /// <summary>树形下拉：悬停「武器」会展开子菜单。</summary>
        [ValueDropdown("treeOptions")]
        public string equipment;

        /// <summary>排过序的下拉（按名字的序数比较）。</summary>
        [ValueDropdown("difficultyOptions", SortDropdownItems = true)]
        public string sortedDifficulty;

        /// <summary>小按钮形态：左边的 ▼ 弹下拉，右边照常是普通输入框（「选 + 填」并存）。</summary>
        [ValueDropdown("difficultyOptions", AppendNextDrawer = true)]
        public string appendedDifficulty = "简单";

        #endregion

        #region 资产选择

        /// <summary>对象字段前多一个小 ▼：点开是整个工程的资产树，选一个填进来。</summary>
        [AssetSelector]
        public Material anyMaterial;

        /// <summary>限定目录与类型：只列这两个目录下的材质，且拍平成一层（只显示文件名）。</summary>
        [AssetSelector(Paths = "Assets/XInspector", Filter = "t:Material", FlattenTreeView = true)]
        public Material scopedMaterial;

        #endregion

        #region 预览

        /// <summary>预览方块（默认 64 像素）+ 右侧可编辑的对象字段。拖个贴图/模型进来就能看到预览。</summary>
        [PreviewField]
        public Texture2D previewTexture;

        /// <summary>指定边长与对齐：方块贴右边，对象字段在左。</summary>
        [PreviewField(80f, ObjectFieldAlignment.Right)]
        public GameObject previewModel;

        /// <summary>居中摆放；<c>Height</c> 也可用具名赋值（官方样例就是这么写的）。</summary>
        [PreviewField(ObjectFieldAlignment.Center)]
        public Material previewMaterial;

        #endregion

        #region 路径选择

        /// <summary>路径输入框 + 「浏览…」按钮；默认存**工程相对**路径（以 Assets/ 开头）。</summary>
        [FilePath]
        public string projectRelativePath = "Assets/XInspector/README.md";

        /// <summary>只允许这两类扩展名（点可选）——只过滤对话框，不拦手填。</summary>
        [FilePath(Extensions = "cs, unity")]
        public string scriptPath;

        /// <summary>相对 Assets/Resources 存档；选中的文件在它之下时只存文件名。</summary>
        [FilePath(ParentFolder = "Assets/Resources")]
        public string resourcePath;

        /// <summary>存绝对路径。</summary>
        [FilePath(AbsolutePath = true)]
        public string absolutePath;

        /// <summary>路径不存在就报错——初始值故意是编的，打开 Inspector 就能看到那条红框。</summary>
        [FilePath(RequireExistingPath = true)]
        public string mustExist = "Assets/这个路径不存在.txt";

        /// <summary>目录形态：浏览打开的是文件夹面板，且没有扩展名过滤。</summary>
        [FolderPath]
        public string outputFolder = "Assets/Sandbox";

        #endregion

        #region 内嵌编辑器

        /// <summary>
        /// 默认模式：只画被引用对象自己的 Inspector。
        /// <para>建一个示例资产拖进来（见 <see cref="InlineEditorSampleTarget"/>）——下面会直接长出它的字段。</para>
        /// </summary>
        [InlineEditor]
        public InlineEditorSampleTarget inlineTarget;

        /// <summary>头 + 界面在左 + 小预览在右，三样都画。</summary>
        [InlineEditor(InlineEditorModes.FullEditor)]
        public Material inlineFull;

        /// <summary>只画大预览，不画界面。</summary>
        [InlineEditor(InlineEditorModes.LargePreview)]
        public Mesh inlineMesh;

        /// <summary>对象字段收进折叠头里；内嵌区超过 200 像素出滚动条。</summary>
        [InlineEditor(InlineEditorModes.GUIOnly, InlineEditorObjectFieldModes.Foldout, MaxHeight = 200f)]
        public GameObject inlineFolded;

        /// <summary>对象字段恒藏——这一格有值，所以整行只剩内嵌内容。</summary>
        [InlineEditor(InlineEditorObjectFieldModes.CompletelyHidden)]
        public InlineEditorSampleTarget inlineHidden;

        /// <summary>值为空时的对照：恒藏模式下会留一行灰字提示，而不是一片空白。</summary>
        [InlineEditor(InlineEditorObjectFieldModes.CompletelyHidden)]
        public InlineEditorSampleTarget inlineEmpty;

        /// <summary>预览在左（默认在右）；两列并排，谁先谁后由对齐方式定。</summary>
        [InlineEditor(InlineEditorModes.GUIAndPreview, PreviewAlignment = PreviewAlignment.Left)]
        public Texture2D inlinePreviewLeft;

        #endregion

        #region 调试

        /// <summary>把本字段的绘制器链摊开成一张表——展开后第 0 格就是它自己。</summary>
        [ShowDrawerChain]
        [Indent]
        [DisplayAsString]
        public int traced = 42;

        #endregion
    }

    /// <summary>展示 <c>[Toggle]</c> 用的设置块——开关在值对象内部。</summary>
    [Serializable]
    public struct ToggleableSettings
    {
        /// <summary>开关本身。</summary>
        public bool Enabled;

        /// <summary>被门控的值。</summary>
        public int value;
    }

    /// <summary>
    /// <c>[InlineEditor]</c> 的示例目标：它自己带本管线的特性，所以被内嵌画出来时
    /// 是**一棵 XInspector 树**——递归成立，本包不需要为此做任何事。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用法：Project 窗口右键 → Create → XInspector → 内嵌编辑器示例，然后把建出来的资产
    /// 拖进展示台的 <c>[InlineEditor]</c> 字段。
    /// </para>
    /// <para>
    /// <b>想看递归守卫：</b>把 <c>self</c> 拖成它自己（或让两个示例资产互相引用）——
    /// 控制台出告警、那一层退回普通对象字段，不会无限递归。
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "XInspector/内嵌编辑器示例", fileName = "InlineEditorSample")]
    [Title("内嵌编辑器示例目标", Subtitle = "被内嵌时它是一棵 XInspector 树")]
    [HideMonoScript]
    public class InlineEditorSampleTarget : ScriptableObject
    {
        /// <summary>普通字段——被内嵌时照常可编辑（改动进 Undo）。</summary>
        [BoxGroup("基础")]
        public string displayName = "示例";

        /// <summary>带范围条，用来确认内嵌里的特性照样生效。</summary>
        [BoxGroup("基础")]
        [PropertyRange(0f, 100f)]
        public int level = 5;

        /// <summary>只在内嵌编辑器里露面——在外层自己的 Inspector 里它根本不出现。</summary>
        [BoxGroup("按内嵌环境变化")]
        [ShowInInlineEditors]
        public int onlyInside = 1;

        /// <summary>反过来：单独看时在，被内嵌时藏起来。</summary>
        [BoxGroup("按内嵌环境变化")]
        [HideInInlineEditors]
        public int hiddenInside = 2;

        /// <summary>被内嵌时变灰但仍可见；单独看时可编辑。</summary>
        [BoxGroup("按内嵌环境变化")]
        [DisableInInlineEditors]
        public int readonlyInside = 3;

        /// <summary>指回自己就成环——这是递归守卫的现场。</summary>
        [BoxGroup("递归守卫")]
        [InlineEditor]
        public InlineEditorSampleTarget self;
    }

    /// <summary>展示 <c>[EnumToggleButtons]</c> 用的普通枚举。</summary>
    public enum DamageType
    {
        /// <summary>物理。</summary>
        Physical,

        /// <summary>火焰。</summary>
        Fire,

        /// <summary>冰霜。</summary>
        Ice,
    }

    /// <summary>展示 <c>[EnumToggleButtons]</c> 用的位标志枚举。</summary>
    [Flags]
    public enum StatusFlags
    {
        /// <summary>无状态。</summary>
        None = 0,

        /// <summary>中毒。</summary>
        Poisoned = 1,

        /// <summary>燃烧。</summary>
        Burning = 2,

        /// <summary>冰冻。</summary>
        Frozen = 4,
    }
}
