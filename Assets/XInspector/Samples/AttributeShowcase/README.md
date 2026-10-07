# AttributeShowcase 示例

逐个演示 XInspector 提供的特性。想看**最小可用形态**（三行接入 + 分组嵌套）请看
`Samples/Overview/`；想看**每个特性长什么样**就是这里。

## 怎么用

1. 场景里新建一个空物体（或直接用任意现有物体）；
2. 把 `AttributeShowcase` 挂上去；
3. 选中它，看 Inspector。字段按主题分成若干组（每组一个 `#region`），逐条目视即可。

## 这个示例在演示什么

### 状态与标签

| 特性 | 预期看到 |
|---|---|
| `[ReadOnly]` | 值照常显示，但不可编辑 |
| `[LabelText("玩家生命")]` | 标签被替换成给定文本 |
| `[LabelText("playerScore", true)]` | 标签做可读化：`playerScore` → `Player Score` |
| `[PropertyTooltip]` | 悬停标签时出现提示 |
| `[LabelText]` + `[PropertyTooltip]` 同时 | 两者各管一段，互不覆盖 |

### 布局与外观

| 特性 | 预期看到 |
|---|---|
| `[GUIColor]` | 整块染上一层暖色。**用在类上会染整页**——同一个绘制器落在根节点上而已，没有「类级特例」 |
| `[Indent]` | 缩进一级 |
| `[PropertySpace(12)]` | 字段前置 12 像素间距 |
| `[LabelWidth(200)]` | 标签列固定 200 像素宽 |
| `[HideLabel]` | 撤掉标签，值占满整行 |
| `[SuffixLabel("秒")]` | 值控件右侧画后缀 |
| `[SuffixLabel("×100%", true)]` | 后缀叠在控件上 |
| `[InlineProperty]` | 嵌套类型的子字段**摊平画出来**，没有折叠箭头；与下面同样类型但未标的那一个直接对照 |
| `[InlineProperty(LabelWidth = 60)]` + `[HideLabel]` | 父标签整个撤掉、标签列收窄到 60——子字段看起来就像本层的字段 |
| `[PropertyOrder(-1)]` / `[PropertyOrder(1)]` | 一个排到最前、一个排到最后；其余未标注的字段保持声明顺序 |
| `[Button, PropertyOrder(-0.5)]` | 按钮**插在字段之间**——默认它排在字段之后，显式顺序开了这个口子 |
| 嵌套字段 `stats` | 展开后**里面的特性第一次生效**：`[Title]` 画出来、`[ShowIf]` 跟随同层的开关、`[PropertyOrder]` 在那一层生效、`[BoxGroup]` 框住它那一组、`[ShowInInspector]` 的只读属性画出来、`[Button]` 点的是**这个嵌套实例**上的方法 |
| 嵌套字段 `nativeNested` | 对照：只带原生 `[Range]` 的嵌套类型**不展开**，整份仍由 Unity 画（外观与从前逐字一致） |
| 嵌套字段 `grouped` | **类级分组标在类型上**（`[BoxGroup]` 写在类型自己上）：成员一律归入它，成员自己的分组嵌在里面——路径是「类级组 / 成员自己的分组」，类级恒在最外层 |

> **嵌套层的分组**框在这个复合字段**里面**（路径是 `stats/嵌套层里的分组`），
> 不会被挤到 Inspector 末尾；同一个嵌套类型用在两个字段上时，两处的分组各是各的。
> **类级分组**（`grouped`）同款：类型上的 `[BoxGroup]` 一族在嵌套层照样生效，路径带父字段前缀。

### 值绘制

| 特性 | 预期看到 |
|---|---|
| `[DisplayAsString]` | 值画成只读文本，可选中复制，不带可编辑控件 |
| `[DisplayAsString(true)]` | 文本折行显示全，而不是裁成一行 |
| `[DisplayAsString(20)]` | 字号 20（`0` ＝ 编辑器默认字号）；行高跟着长，但**不撑开留白** |
| `[DisplayAsString(16, true)]` | 富文本：`<b>`、`<color>`、`<i>` 这些标签真的会被解析 |
| `[ToggleLeft]` | bool 的开关在左、标签在右（与 Unity 默认相反） |
| `[ProgressBar(0, 100)]` | 数值画成进度条，点击或拖动条子即可改值 |
| `[ProgressBar(..., Segmented = true)]` | 四段刻度 + 自定义填充色；数值文本画在条上 |
| `[EnumToggleButtons]` | 枚举画成一排按钮（单选），替代下拉框 |
| `[EnumToggleButtons]`（`[Flags]`） | 逐位多选，每按一次翻转一位 |
| `[MultiLineProperty(5)]` | 5 行文本域；标签画在文本域上方 |
| `[DelayedProperty]` | 输入过程中不写回，回车或失焦才提交 |
| `[EnumPaging]` | 枚举下拉框 + 前后翻页按钮（末尾自动绕回开头） |
| `[PropertyRange(0, 100)]` | 滑块，取值被限制在范围内（只换控件，**不钳数据**） |
| `[Wrap(0, 360)]` | 初始的 400 在绘制后被绕成 40 |

### 校验与钳制

| 特性 | 预期看到 |
|---|---|
| `[Required]`（空串） | 字段上方一条错误框，默认文本「此字段为必填。」 |
| `[Required("...", Warning)]` | 自定义消息与级别 |
| `[Required]`（纯空白串） | **没有**错误框——空白串按非空（本包自定的语义） |
| `[MinValue(0)]` | 越界的初始值在绘制后被抬到 0 |
| `[MaxValue(100)]` | 越界值被压到 100 |
| `[AssetsOnly]` | 拖入场景对象时出现警告框（只提示，不拦赋值） |
| `[SceneObjectsOnly]` | 拖入工程资产时出现警告框 |

### 信息框

| 特性 | 预期看到 |
|---|---|
| `[InfoBox(..., Info)]` | 恒显示的信息框 |
| `[InfoBox(..., Warning, nameof(showWarning))]` | 勾上 `showWarning` 才出现——**字段本身照常绘制**，条件只作用于信息框 |
| `[DetailedInfoBox]` | 摘要一行，详情折起来 |

### 分组族

| 特性 | 预期看到 |
|---|---|
| `[VerticalGroup("竖列")]` | 两个成员合进一个**不画框**的竖直容器 |
| `[TitleGroup("标题组", "副标题")]` | 加粗标题 + 分隔线 + 副标题（标题即分组路径） |
| `[FoldoutGroup("折叠组", true)]` | 可折叠；收起时组内内容**不画**（不是变灰） |
| `[FoldoutGroup]` + `[BoxGroup]` 同路径 | 两格并存：折叠在外、框在内（档位决定谁包住谁） |
| `[TitleGroup]` + `[BoxGroup]` 同路径 | 标题在框之外 |
| `[HorizontalGroup("一行", 0.7f)]` | 与下一个字段排成一行，各占 70% / 30% |
| `[HorizontalGroup("三格")]` × 3 | 未指定宽度的格子**均分**整行 |
| `[TabGroup("页签", "基础")]` | 页签栏 + 只显示选中页；写同一个组名的成员自动分页 |
| `[ToggleGroup("showAdvanced")]` | 组标题前的复选框关掉时**组内内容不画**；开关是同一个对象上的 bool 字段，它的名字就是组 ID |
| `[ToggleGroup(nameof(AdvancedUnlocked))]` | 开关是**非序列化属性**（2026-10-06 起）：复选框画成**禁用**、标题带一句说明，门控照常生效——本包对反射成员一律不给写 |
| `[ShowIfGroup("条件组")]` | 条件为真才显示**整组**（两个成员一起出现、一起消失）；组名兼条件名，也可用 `Condition` 显式指定 |
| `[ShowIfGroup]` + `[BoxGroup]` 同路径 | 条件照判、框照画——本特性本身不画任何东西，视觉交给分组绘制器 |
| `[HideIfGroup("调试组")]` | 取反用法：勾上开关时整组消失（条件名用 `Condition` 指定） |

### 结构与门控

| 特性 | 预期看到 |
|---|---|
| `[ReadOnly]` + `[EnableGUI]` | 字段仍**可编辑**——EnableGUI 排在只读之后，它赢 |
| `[DrawWithUnity]` | 该字段由 Unity 原生绘制；叠在它内侧的 `[Indent]` **不生效**（这正是「交给 Unity」的含义） |
| `[ChildGameObjectsOnly]` | 拖入非子物体时出现警告框（只提示，不拦赋值） |
| `[Toggle("Enabled")]` | 字段前的开关关掉时字段变灰；**开关本身永远可点**（否则关掉就开不回来） |
| `[TypeInfoBox]`（类级） | Inspector 最顶部一条信息框 |
| `[HideMonoScript]`（类级） | 脚本槽位（Script 字段）消失 |

### 资产选择

| 特性 | 预期看到 |
|---|---|
| `[AssetSelector]` | 对象字段**左侧多一个小 ▼**：点开是整个工程的资产树（按文件夹分层），选一个直接填进字段 |
| `[AssetSelector(Paths = "…\|…", Filter = "t:Material", FlattenTreeView = true)]` | 只列限定目录下、符合过滤串的资产，且**拍平成一层**（只显示文件名） |
| `[AssetList]`（单元素） | **资产列表的单元素形态**：预览块（64 像素、在左，与 `[PreviewField]` 同款）+ 原生对象字段 + 右侧「▼」。点「▼」弹出**按字段类型过滤**的编辑器自带菜单；拖贴图到字段上照常赋值（那是原生控件自己的事）。**列表形态**在「集合与表格」一段 |

> `Paths` 多个目录用 `|` 分隔；`Filter` 用的是 **AssetDatabase 的搜索语法**（`t:` 类型、`l:` 标签）。
> 它是**透传型**绘制器——画完按钮照常画原生的对象字段，所以拖拽赋值、类型限制、预制体覆盖一样不少。
> 与 Odin 的差异：弹出层是编辑器自带菜单，**没有搜索框、图标与多选**；它那几个只为那个窗口存在的
> 选项（以及只对列表有意义的选项）因此**不声明**——写了会编译不过。

### 类型选择

| 特性 | 预期看到 |
|---|---|
| `[SerializeReference, TypeDrawerSettings]`（`anyType`） | 一行「当前类型名」的**假字段按钮**：点开是本包自绘的候选菜单——**按命名空间分层**、当前值带勾、有值时最上面多一项「（无）」（点它清空）。不设约束时候选是「除接口以外的所有类型」——分层是唯一能用的导航 |
| `TypeDrawerSettings(BaseType = typeof(IShowcaseShape))`（`shapeType`） | 候选收窄到上面多态那段那个接口的派生（`ShowcaseCircle` / `ShowcaseSquare`）——菜单小得多，一眼看完 |
| `TypeDrawerSettings(BaseType = …, Filter = IncludeConcreteTypes \| IncludeInterfaces)`（`shapeTypeNarrowed`） | 与上一条同一个基类型，但**只要具体的类与接口**——抽象类与泛型不进菜单。（过滤位的差异在 `anyType` 那种不设约束的场景里最明显：候选是整个工程的类型，分层菜单才显出用处。） |

> **字段必须是托管引用的 `System.Type`**（`[SerializeReference]`）——裸的 `System.Type` 字段
> 不在序列化数据里、进不了 Inspector；忘了加会在 Console 里得到一条**构建期告警**，直接点名
> 「要加 `[SerializeReference]`」。**这是本包与 Odin 的一处差异**（Odin 用自己的序列化器兜住了
> 这一层）。`TypeInclusionFilter` 的成员名照官方、**数值本包自定**；一个类型可同时命中多位
> （泛型接口＝泛型＋接口，静态类归抽象）。
> **多选（各目标不一致）退回 Unity 原生那一行**；类型写回**不进撤销栈**——`System.Type` 的
> 托管引用撤销恢复不出来（实测），本包宁可让 Ctrl+Z 跳过这一步。
> 多态字段自己的「换具体类型」**默认仍由 Unity 原生那一行承担**；标了
> `[PolymorphicDrawerSettings]` 的字段（下面「多态选择器」一段）改用本包自绘的选择器。

### 多态选择器

| 特性 | 预期看到 |
|---|---|
| `[SerializeReference, PolymorphicDrawerSettings]`（`picked`） | 多态字段的那一行换成**本包自绘的「当前类型名」按钮**（不再是 Unity 原生那一行）：点开是候选菜单——只列**装得进这个槽位、且造得出实例**的实现。清空（菜单里的「（无）」）再点它，选一个类型——**子字段立刻出现**（对账即展开） |
| `PolymorphicDrawerSettings(ShowBaseType = true)`（`outlined`） | 行上带**基类型**：显示成「ShowcaseSquare （IShowcaseShape）」（格式本包自定）。对照上面 `picked`（只显示具体类型名） |
| `PolymorphicDrawerSettings(ReadOnlyIfNotNullReference = true)`（`lockedIn`） | **有值之后那一行变灰**（不许再换类型），而**子字段照常可编辑**——这个旋钮只锁「改类型」那一行。想再点按钮：先把槽位清空（取消勾选不行它没勾选框，直接改代码或换回别的状态） |
| `PolymorphicDrawerSettings(NonDefaultConstructorPreference = LogWarning)`（`guardedPick`） | 菜单里选 `ShowcaseParameterized`（**只有带参构造**）：Console 里出现一条「按 LogWarning 档不构造」的说明，槽位**原样不动**。对照：`picked` 用默认档（`ConstructIdeal`）选它就会挑那个带参构造、参数填 0 |
| 对照：上面的 `shape`（**没标特性**） | 那一行仍是 **Unity 原生**的多态 UI——标与不标的差别一眼可见 |

> **回退**：槽位有值、但那个类型**用不到本包**时这一行退回 Unity 原生（Console 里一条说明）——
> 本包只接管「用得到本包」的类型。**点当前类型 = 无操作**（不会拿一个同类型的新实例把你的值换掉）。
> **换类型这一下能撤销**（与上面类型选择器相反）。官方的 `CreateInstanceFunction` 还没做
> （要单参解名通道，留下一批）。

### 调色板

| 特性 | 预期看到 |
|---|---|
| `[ColorPalette("ShowcasePalette")]` | 字段上方一行色块（本目录下的 `ShowcasePalette.asset`；**资产文件名就是调色板名**）。点一格填进字段，当前值命中的那一格**描一圈白边**；原生颜色字段照常在下面 |
| `[ColorPalette]`（无参） | 演示**多份时的兜底**：无参形态要求工程里恰好一份调色板，示例里有多份 → Console 一条告警、字段退回普通绘制（不猜） |
| `[ColorPalette("这个调色板不存在")]` | 演示**找不到时的兜底**：同样一条告警 + 普通绘制——特性配置不对**绝不让字段消失** |

> 调色板是**工程内的一份资产**（右键 `Create/XInspector/Color Palette`），不是编辑器偏好——
> 那样能进版本控制、能团队共享。**透传型**：色块行画完照常画原生颜色字段（精确值、alpha、
> 吸管都在那儿），所以调色板是快捷入口而不是替代品。
> **只作用单个 `Color` 字段**：数组与 `List<Color>` 不做（那要按元素画，而「给每个元素套同一份
> 调色板」的语义没定）。多选值不一致时**整行色块不画**（点一下会把主目标的颜色铺到全部目标，
> 那是静默改数据）。

### 下拉选择

| 特性 | 预期看到 |
|---|---|
| `[ValueDropdown("difficultyOptions")]` | 下拉框占满整行、显示当前值；选项来自同一对象上的**序列化数组**字段 |
| `[ValueDropdown("treeOptions")]` | 选项里带 `/` 就**分子菜单**（悬停「武器」展开）——官方默认就是树形 |
| `[ValueDropdown(..., SortDropdownItems = true)]` | 选项按名字排序（序数比较，跨平台稳定） |
| `[ValueDropdown(..., AppendNextDrawer = true)]` | 只画一个小 ▼ 按钮，**右边照常是普通输入框**（「选 + 填」并存） |
| `[ValueDropdown(nameof(MakeDifficulties))]` | 选项来自**无参方法**——方法返回的列表不必序列化，每次弹出菜单时现算一遍 |
| `[ValueDropdown(nameof(LevelOptions))]` | 选项来自**普通属性**（只有 `get`，Unity 不序列化它） |

> 数据源有两个形态：**序列化的数组 / List**（选项就是它的元素），或声明类型**实现 `IList`** 的
> 普通字段 / 属性 / 无参方法（Odin 里最常见的 `[ValueDropdown(nameof(GetOptions))]` 就是这个）。
> 名字解析走与条件族同一条阶梯——嵌套层里先找**同层**的，再回落根上的绝对名，最后才看反射成员。
>
> 其余四条边界：**只实现 `IEnumerable` 的源不收**（`HashSet`、LINQ、`Dictionary.Values`…）并明确告警
> ——`string` 也只实现 `IEnumerable<char>`，放宽会静默变出字符选项表；声明成 `IList<T>` 的成员同样被拒
> （泛型接口不继承非泛型 `IList`），改成 `List<T>` 或数组即可；**被标注的字段必须是单值**
> （数组形态要按元素画，属集合自绘那一层）——因此 Odin 那几个只对列表有意义的选项
> （`IsUniqueList` 等）**不声明**，写了会编译不过；**源与目标类型必须一致**（枚举还要求成员名与顺序
> 一致），不符则拒绝这次选择并告警，绝不按索引硬写。无参方法在**每次弹出菜单时**被调用一次
> （不是每帧），有副作用的方法请自重。

### 预览

| 特性 | 预期看到 |
|---|---|
| `[PreviewField]` | 64×64 的预览方块 + 右侧可编辑的对象字段。**拖一个贴图/模型进来**，方块里会变成它的预览（资产预览是异步生成的，可能要一两帧） |
| `[PreviewField(80f, ObjectFieldAlignment.Right)]` | 方块 80 像素、贴右，对象字段在左 |
| `[PreviewField(ObjectFieldAlignment.Center)]` | 方块居中；`[PreviewField(对齐)] { Height = 150 }` 这种具名写法也支持（官方样例就这么写） |

> 与 Odin 的三处差异：**方块是预览、不是控件**（可编辑的是旁边那个对象字段；Odin 让方块本身
> 既是预览又是字段，还带 Ctrl+点击清空、Ctrl+拖拽替换——那些不做）；
> **默认高度（64）与默认对齐（Left）是本包定的**，Odin 的默认值存在它的偏好设置里、核不到；
> 含 `UnityEngine.FilterMode` 的两个重载**永久不做**（Runtime 零 Unity 依赖是编译期强制的）。

### 范围与滑块

| 特性 | 预期看到 |
|---|---|
| `[MinMaxSlider(0f, 100f)]` | 双滑块：拖左把手改 `x`、右把手改 `y`，两个把手不许交叉 |
| `[MinMaxSlider(-10f, 10f, true)]` | 同一条滑块，左右各多一个可输入的数值框 |
| `[MinMaxSlider("dynamicRange", true)]` | 量程**取自另一个成员**（序列化 `Vector2`，x/y 即上下限）——改 `dynamicRange` 的值，这条滑块的可拖范围立刻跟着变 |
| `[MinMaxSlider(nameof(ComputedRange), true)]` | 量程取自一个**非序列化属性**（2026-10-06 起）：按名找成员与条件族同款，可以是普通字段/属性或无参方法 |

> 边界也可以是**成员名**：Odin 那边这个字符串是 resolved string（支持 `@` 表达式与方法调用），
> 本包认**成员**（序列化成员、普通字段/属性、无参方法）而不认表达式语言——
> 与条件族同一条阶梯。

### 路径选择

| 特性 | 预期看到 |
|---|---|
| `[FilePath]` | 路径输入框 + 右侧「浏览…」按钮；默认存**工程相对**路径（以 `Assets/` 开头） |
| `[FilePath(Extensions = "cs, unity")]` | 「浏览…」的对话框只列这两类文件。**只过滤对话框**——手填别的扩展名照收，不报错 |
| `[FilePath(ParentFolder = "Assets/Resources")]` | 选中的文件若在 `Assets/Resources` 之下，字段里只存**相对它**的路径 |
| `[FilePath(AbsolutePath = true)]` | 字段存的是绝对路径（形如 `E:/…`） |
| `[FilePath(RequireExistingPath = true)]` | 初始值是编的，故字段下方常驻一条红框；手填成一个真存在的路径它立刻消失 |
| `[FolderPath]` | 与 `[FilePath]` 同形，但「浏览…」打开的是**文件夹**面板，且没有扩展名过滤 |

> 两条刻意的边界：**不支持 `string[]`**（数组要按元素画，属于集合自绘那一层），
> **参数只认字面量**（Odin 的 `$DynamicParent` 那类成员引用不做）。

### 内嵌编辑器

| 特性 | 预期看到 |
|---|---|
| `[InlineEditor]` | 字段下方直接长出被引用对象的 Inspector。先建一个示例资产（Project 窗口右键 → Create → XInspector → 内嵌编辑器示例）拖进来——它自己带本管线的特性，故内嵌出来是**一棵 XInspector 树** |
| `[InlineEditor(InlineEditorModes.FullEditor)]` | 编辑器头 + 界面在左、小预览在右（材质球有预览） |
| `[InlineEditor(InlineEditorModes.LargePreview)]` | 只有一张大预览（默认 128 高），没有界面 |
| `[InlineEditorObjectFieldModes.Foldout]` + `MaxHeight = 200` | 对象字段收进折叠头；展开后内嵌区超过 200 像素时出滚动条 |
| `[InlineEditorObjectFieldModes.CompletelyHidden]`（有值） | 整行只有内嵌内容，对象字段不出现 |
| `[InlineEditorObjectFieldModes.CompletelyHidden]`（空值） | 一行灰字提示「隐藏了对象字段」——**本包不接受静默空白**（与 Odin 的一处差异） |
| `PreviewAlignment = Left` | 预览列在**左**、编辑器界面在右（默认在右） |

> **想看递归守卫**：把示例资产的 `self` 字段拖成它自己。控制台出告警（引用成环），
> 那一层退回普通对象字段——不会无限递归；对象图很深时由 **4 层**的深度上限兜底（本包自定值）。
>
> 三条本包自定的默认值：嵌套上限 4、预览默认尺寸（并排时宽 64、单独时高 64，大预览 128）、
> 默认预览位置在右。内嵌里的编辑**会进 Undo**（Inspector 路径上外层本来就进）。

### 内嵌环境条件

示例资产里那三个字段（`onlyInside` / `hiddenInside` / `readonlyInside`）专门演示这一族。
**要看出差别，得对比两处**：把示例资产单独选中看一次（外层），再把它拖进展示台看一次（内嵌）。

| 特性 | 外层单独看 | 被 `[InlineEditor]` 内嵌时 |
|---|---|---|
| `[ShowInInlineEditors]` | 不出现 | 出现 |
| `[HideInInlineEditors]` | 出现 | 不出现 |
| `[DisableInInlineEditors]` | 可编辑 | 变灰（仍可见） |

> 判据是**绘制期的嵌套深度**：这三个特性没有绘制器，只有处理器装的求值器，每帧现读。
> 因此 `[InlineEditor(IncrementInlineEditorDrawerDepth = false)]` 的那一层算「不算内嵌」——
> 在里面这三个特性一律不生效（用途是「画整个编辑器，但让它们当没看见」）。

### 按钮

| 特性 | 预期看到 |
|---|---|
| `[Button]` | 一个按钮，文本就是方法名；点一下执行方法 |
| `[Button("随机生命", ButtonSizes.Large)]` | 自定义文本 + 大号（本包自定值：`Small` 20 / `Medium` 25 / `Large` 30 / `Gigantic` 60 像素） |
| `[Button]` + 带参方法 | 按钮左侧多一个折叠箭头；展开后逐项填参数，再点按钮执行。默认收起 |
| `[Button]` + 支持不了的参数 | 按钮照画但变灰，下方写明原因（**不会静默消失**） |
| `[InlineButton]` | 字段**右侧**的小按钮；同一个字段挂两个就有两个，各调各的方法 |
| `[ButtonGroup]` | 裸用即归入同一行、等分宽度。按钮**一律排在字段之后**（见下） |
| `[ResponsiveButtonGroup]` | 按标签宽度排布，**把 Inspector 面板拉窄**就会折行；`UniformLayout = true` 时等宽 |

> **按钮的位置是本包与 Odin 的一处差异。** Odin 把按钮插在相关字段旁边，本包一律排在字段之后。
> 原因是实测拿不到那个信息：字段在 `SerializedObject` 里，方法只能靠反射，而两者的元数据令牌
> 分属**两张表**各自编号（字段 0x04、方法 0x06），跨表比大小没有意义；`GetMembers` 也不按声明顺序
> 返回。位置不理想是小事，把按钮插到随机位置才是大事。

### 回调

| 特性 | 预期看到 |
|---|---|
| `[CustomContextMenu("重新随机", …)]` | 在 `damage` 字段上**右键**，菜单里多出一项「重新随机」 |
| `[OnValueChanged(…)]` | 给 `observedHealth` 输个 500，它会自己弹回 100——回调真的跑了 |
| `[OnInspectorInit]` | 每次开 Inspector 跑一次（切走再切回来会重跑） |
| `[OnStateUpdate]` | **每趟 GUI 布局**跑一次；本包没有 Odin 的 state update 循环，这是自定语义 |
| `[OnInspectorGUI]` | 方法自己画一段界面（给 `health` 划一根进度条）。方法体里的 `UnityEditor` 调用要包在 `#if UNITY_EDITOR` 里 |

> 后四者的时机各不相同，共通点是**都标在方法上、都不产生字段**。
> `[OnInspectorInit]` / `[OnInspectorDispose]` / `[OnStateUpdate]` 连节点都不产生
> （它们不在某个位置上画东西），只有 `[OnInspectorGUI]` 拿一个方法节点、占一个位置。

### 反射成员

这一组打开的是「画 Unity **不会**序列化的东西」那类能力：普通属性、没有 `[SerializeField]`
的私有字段、静态成员。它们由**第二套值后端**（反射）供给，因此**一律只读**。

| 特性 | 预期看到 |
|---|---|
| `[ShowInInspector]`（私有字段） | 那个没有 `[SerializeField]` 的字段出现了——Unity 自己不会画它 |
| `[ShowInInspector]`（普通属性） | 以只读文本出现，且**每帧现读**：改上面的私有字段，它立刻跟着变 |
| `[ShowInInspector]`（静态成员） | 静态字段/属性同样能画；显示的是全局值，不随实例走 |
| `[ShowIf]` 指向普通属性 | `onlyWhenHealthy` 跟着 `IsHealthy`（一个**普通属性**）显隐——条件不再必须是序列化成员 |
| `[ShowInInspector]` + `[PropertyRange]` | 值照常画出，但**控件换不掉**：`[PropertyRange]` 需要序列化后端。**Console 里会有一条说明**——这是故意的，本包不做「标了没反应」 |

> **只读不是偷懒。** 这些成员按定义不在 Unity 的序列化里：写进去既不可撤销，也不会随存档保存
> （Odin 的文档同样写着「它不序列化任何东西，改动不会随之保存」）。本包把这句话落实成
> 「干脆不给写」，而不是做一个看起来能改、改完就丢的控件。
>
> **已被序列化通道收走的成员不会重复。** `[ShowInInspector]` 标在一个 public 字段上什么也不会多画
> ——那个字段本来就画着，而且**可编辑**。反过来，`[HideInInspector] public int x` 会被收进来
> （它被 Unity 序列化，却不在 Inspector 里）。
>
> **集合只显示摘要**（`List<Int32>（3 项）`），不展开；**嵌套 `[Serializable]` 类型里的成员**
> 也收不到；**标在方法上编译不过**（方法请用 `[Button]`）。

### 预制体上下文

这一组按**被检视对象所处的预制体上下文**开关，与字段的值无关。判据每帧现取，
所以进出隔离编辑模式、把预制体拖进拖出场景，字段都会立刻跟着变，不必重开 Inspector。

| 特性 | 预期看到 |
|---|---|
| `[ShowIn(PrefabKind.NonPrefabInstance)]` | 组件挂在场景里的普通对象上时，只有 `sceneOnly` 一行在 |
| `[ShowIn(PrefabKind.PrefabAsset)]` | 把组件**放进一个预制体**再选中那个资产，`assetOnly` 一行才出现 |
| `[HideIn(PrefabKind.PrefabInstance)]` | 把预制体**拖进场景**，`notInInstances` 一行消失 |
| `[EnableIn(PrefabKind.PrefabInstance)]` | 同一个实例里 `tuning` 可编辑；回到资产或普通对象上它是灰的 |
| `[DisallowModificationsIn(PrefabKind.PrefabAsset)]` | 资产上 `bakedRadius` 是灰的；若它在加上本特性之前就被改过，还会多一条错误提示 |
| `[RequiredIn(PrefabKind.PrefabAsset)]` | 资产上 `iconName` 留空会画一条错误提示；场景对象上这一行根本不出现 |

> **要三种视图才看得全，这不是坏了。** 挂在场景对象上时只会看到说明行与 `sceneOnly`
> ——其余几行的条件都不满足。看全它们请：① 选中一个 `.prefab` 资产；② 把预制体拖进场景；
> ③ 双击预制体进隔离编辑模式。本示例**刻意不带 `.prefab` 资产**（包内只放脚本与文档），
> 自己造一个即可：把本组件挂到一个空物体上，再拖进 Project 窗口。

> **多选时要求全部目标都匹配。** 同时选中一个预制体资产与一个场景对象，两边的字段都不满足条件
> ——预制体上下文是整个选择的性质，不是某一个目标的事。

### 集合与表格

| 特性 | 预期看到 |
|---|---|
| （无特性，`nativeNumbers`） | 原生对照：数组由 Unity 自己画，自带增删与拖拽 |
| `[ListDrawerSettings(ShowIndexLabels = true)]` | 自绘列表：每行一个「−」、标题行右端一个「+」、行标签是下标 |
| `[ListDrawerSettings(IsReadOnly = true, HideAddButton = true)]` | 增删按钮变灰、且没有「+」；**元素本身照常可编辑**——这正是与 `[ReadOnly]` 的差别 |
| `[ListDrawerSettings(ShowFoldout = false)]` | 恒展开：没有折叠三角 |
| `[RequiredListLength(3)]` | 只有一项 → 字段上方一条错误提示（说清要求与现状） |
| `[TableList(ShowIndexLabels = true)]` | 表格：列头一行 + 每行一个元素；`level` 定宽 45 像素、`name` 弹性、`memo` 因 `[HideInTables]` 不进表 |
| `[Searchable]` + `[ListDrawerSettings]` | 标题行下面多一行搜索框。输入「哥布」只留下命中那一行；清空输入恢复全部。**行按值比**——搜 `Element 1` 没用，搜 `史莱姆` 才有用 |
| `[OnCollectionChanged(…)]` | 点 `scores` 的「+」或「−」，Console 里出现成对的两条日志（改动前 / 改动后）。删一行时改动前那条会报出**被删掉的值** |
| （无特性，`party` 的元素类型） | **元素节点化**：这个列表的元素类型用到了本包，于是每个元素成为真节点——展开任意一行，`[ShowIf]`（第二行已取消勾选，`hp` 那行不出现）、`[BoxGroup]`（框在元素**里面**）、`[PropertyOrder]`（`name` 排到最前）都跟顶层字段一样工作 |
| （同上，元素里的反射成员与按钮） | **元素层的读路径**：每一行里还会多出 `Power`（`[ShowInInspector]`，只读、每帧现读——改 `hp`/`level` 它立刻变）与「元素按钮：满血」（点的是**这一行**的元素实例上的方法：`hp` 变 100、`alive` 变真） |
| `[Searchable]` + 元素层（`searchableParty`） | 与 `party` 同一个元素类型，只是宿主开了搜索。两件事一起看：输入内容时**元素里的分组框与里面的字段照常画**（不会被筛空），以及**行匹配同时比序列化值与元素里反射成员的当前值**——搜 `140` 试试，只有 `Power = hp + level × 10` 恰为 140 的那一行留下，而 140 **没有存在任何字段里** |
| `teams`（元素类型里又嵌着集合） | **元素层深度 > 1**：展开任意一行外层元素，里面的 `members` 也照常节点化——内层每一行自己的 `[ShowIf]`（第二行已取消勾选，`title` 不出现）与「内层治疗」按钮（改的是**那一行内层元素**的 `hp`）都生效；路径是两组 `Array.data[i]` |
| `[ReadOnly]` + `[ListDrawerSettings]` | 整块（**含每个元素**）变灰——与上面那个 `IsReadOnly` 旋钮的差别正在这里：旋钮只关增删按钮，元素照常可编辑 |
| `[AssetList]` | **资产列表**：每行一个缩略图 + 原生对象字段 + 「−」；标题行有「+」与「选择」。**从工程窗口拖几个材质进来**（一次拖多个也行）；点「选择」弹按类型过滤的菜单；空列表时有一行灰字提示。拖**场景对象**、**重复项**或**类型不符**的进来会被拒，并在 Console 里各说一句 |
| `[AssetList(Path = …, AssetNamePrefix = "pack")]` | 限定本包根目录、且文件名以 `pack` 开头——点「选择」看菜单只剩 `package.json` 那个（`Path` 与 `AssetNamePrefix` 的对照） |

> **元素是「按需」节点化的**：元素类型用到本包**且**集合被本包接管才建——原生对照
> `nativeNumbers` 与标量元素照旧整份交给 Unity。四条刻意的边界：**元素层按需递归**
> （元素**里面**的集合也节点化，最多 4 层；自引用由类型链去重挡、两道守卫都会构建期告警）；
> **元素里的反射成员与按钮生效**（取/调的是那一行的元素实例，不是本组件上的同名成员；
> `List<结构体>` 上的方法一律拒绝）；
> **增删的新元素是上一个元素的副本**（Unity 自己的语义，与原生「+」一致）；
> **集合回调触发时目标对象上的托管集合仍是旧的**（绘制只改序列化数据的内存副本，落盘在这一帧之后）。

### 调试

| 特性 | 预期看到 |
|---|---|
| `[ShowDrawerChain]` | 可展开的「绘制器链」表：序号、绘制器名、权重、触发它的特性。第 0 格是它自己（权重 -950，几乎最外） |

## 这些特性是怎么画出来的

每个特性都是「特性类 + 绘制器」的普通配对：绘制器做点事，然后调用链上的下一个。
`[LabelText]` 改标签、`[Indent]` 推进缩进、然后交给下一个绘制器画值——
**新增一个特性是纯加法**，不需要修改任何既有绘制器，也没有「类级特例」这回事。
`[GUIColor]` 用在类上能染整页，只是因为类级特性也落在根节点上，走的是同一条路径。

「值绘制」那一组是另一类：`[DisplayAsString]`、`[ToggleLeft]`、`[ProgressBar]`、`[EnumToggleButtons]`
**不调用下一个绘制器**——它们把值控件整个换掉。这也是链条本来就支持的能力
（不调用下一个就等于把内侧藏起来），同样没有特例代码。
代价写在实现里：绕过内侧的代价是**绕过末端那层只读禁用罩**，所以这几个绘制器各自处理只读。
