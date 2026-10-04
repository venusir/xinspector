# Changelog

本文件记录 XInspector 的所有值得注意的变更。

格式参照 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Added — L1b：开关分组（ToggleGroup）

- **`[ToggleGroup("showAdvanced", groupTitle: "高级选项")]`**：组标题前一个复选框，关掉时
  组内内容**不画**（整组消失）——与 `[Toggle]` 的「变灰但仍可见」刻意不同。
  **组 ID 就是开关成员名**（照 Odin 的语义）：同组成员必须写同一个 bool 成员名。
- 开关是同一个对象上的序列化 bool（相对路径），从**第一个带值入口的后代**取序列化对象再解析
  ——不能假定 `Children[0]`，页签容器的第一个孩子是页节点，同样是分组。
- **本包唯一一处绘制期解析**：分组节点在构建期的处理器阶段还不存在，装不上去。
  解析结果缓存进 `PropertyState`（`SerializedProperty` 是活句柄，解析一次就够），
  失败时告警一次并**恒显示内容**——拼错的名字不该让一整组字段消失。
  这条例外连同理由写进了 Editor/README 的「构建期的顺序是契约」一节。
- 混合态走 `EditorGUI.showMixedValue`（`try/finally` 还原）；档位 -180：在折叠（-190）之内、
  框与标题之外——决定内容存在与否的恒最外。
- `CollapseOthersOnExpand` 只留字段、不做行为（跨组协调没有明确语义），
  与 `[Toggle]` 的同名参数、`[DrawWithUnity]` 的 `PreferImGUI` 同一条处置。

### Added — L1b：页签（TabGroup）

- **`[TabGroup("设置", "基础")]`**：把成员分进页签，一次只显示一页。
  分组路径是 `"设置/基础"`——**点分路径本身就是子分组机制**，构建期一行未改。
  与 Odin 的**实现**差异（行为等价）：官方靠 `ISubGroupProviderAttribute` 让每个页签派生子分组，
  本包用点分路径天然表达，没有那条缝（README 里写明）。
- 容器与页的判定靠一个字段：容器的特性是 `CloneForPath` 改写路径而来，
  `TabsGroupID` 因 MemberwiseClone 保持原值——`GroupID == TabsGroupID` 即容器。
  这条判定在 Runtime 侧逐条钉住（`TabGroupAttributeTests`），包括「克隆之后才成为容器」。
- 选中页存在每棵属性树上（域重载回到第一页，不跨会话持久化）；**越界回退到第一页并告警一次**
  ——不静默什么都不画。「只画选中页」复用了上一个提交装进末端的那条能力。
- 档位 -170：页签栏与「选哪一页」必须在页内容的一切装饰之外，否则框会跟着每一页各画一次。
- `UseFixedHeight` 是**保留参数、不产生行为**（官方的固定高度模式是为滚动内容准备的，
  本包还没有那套布局）；图标重载、`TextColor` 表达式、`TabLayouting`、`Paddingless`
  与 `Tabs` 列表不照搬——后两者是 Odin 子分组派生机制的产物，本包不需要。

### Added — L1b：水平分组（HorizontalGroup）

- **`[HorizontalGroup("一行", 0.7f)]`**：把成员排成一行，每格按宽度分数分配。
  分数语义：显式之和 ≤ 1 时未指定者**均分剩余**；之和大于 1 时按总和**等比缩放**；
  非正/NaN/未写都算「未指定」。可用宽度取**当前布局组的实际内宽**（不是窗口宽度），
  嵌套在框、缩进里也准；挤不下时整行退化为「不加约束」的自动排布，而不是硬塞。
- 具名参数：`Gap`、`MarginLeft`/`MarginRight`、`PaddingLeft`/`PaddingRight`、
  `MinWidth`/`MaxWidth`、`Title`、`LabelWidth`、`DisableAutomaticLabelWidth`。
  **格内标签宽度会自动按格宽折算**（可覆盖、可关掉）——Inspector 宿主按整页宽设的
  `labelWidth` 放进 1/3 宽的格子会把值控件挤成一条缝。
- **没有让分组绘制器去驱动子节点**，而是引入一处分组与末端之间的显式约定：
  分组绘制器把逐格宽度装进 `GroupChildrenLayout`（存在节点状态上），
  由末端 `ChildrenDrawer` 消费。理由是那个被否决的做法会跳过同节点上更内层的绘制器
  （多类型并存之后那是常规场景），还把「末端必画」降级成分支逻辑。
  决策本身（分数 → 像素）仍是纯函数 `HorizontalGroupWeights`，跑在离线测试通道里。
- 连带把「只画选中页」的通用能力也放进末端（越界/空选择**回退为画全部**并告警一次）——
  下一页签要用它。

### Added — L1b：分组族三件（竖直 / 标题 / 折叠）

- **`[VerticalGroup]`**：不画框的竖直容器（`PaddingTop`/`PaddingBottom`）。无参写法落进
  默认分组 `_DefaultVerticalGroup`（照 Odin 的取值），多个 `[VerticalGroup]` 因此归进同一组。
- **`[TitleGroup]`**：标题 + 分隔线 + 可选副标题、四种对齐。**标题即分组路径**
  （`[TitleGroup("战斗属性")]` 不必另起组名）。`TitleAlignments` 是自建枚举——官方只公开了
  成员名与字母序，数值未核实，本包按该顺序从 0 起排。
- **`[FoldoutGroup]`**：可折叠，收起时组内内容**不画**（不是变灰）——用的正是
  「不调下一个绘制器 = 把内侧藏起来」这条既有能力，没有特例机制。展开状态存在**每棵属性树**上
  （域重载、重开 Inspector 回到初值），**不跨会话持久化**（既定边界，见 Roadmap）。
  `HasDefinedExpanded`（照官方同名属性）让同组多次声明的合并能实现「显式者优先」——
  布尔量区分不了「未设置」与「显式设为 false」。
- 档位表（分组带内，越小越外）：折叠 −190 ＞ 标题 −150 ＞ 框 −130 ＞ 竖直 −110 ＞ 水平 −100。
  规则：**决定内容存在与否的恒最外**（折叠若排在框内侧，收起后会留下一个空框），
  **决定横向排布的恒最内**。
- 三个特性**只加了 Runtime 类与 Editor 绘制器，构建期一行未改**——上一提交的多类型并存
  与前缀归属让它们能落在同一路径上各占一格（`[FoldoutGroup] + [BoxGroup]` 同路径即演示）。

### Changed — 分组装配：多类型并存与前缀归属

- **同路径的不同分组类型现在并存**：此前节点只留先创建者那份特性，第二种被**静默丢弃**，
  且祖先节点带哪种类型取决于**声明顺序**（`[TitleGroup("T")]` 与 `[BoxGroup("T/B")]`
  谁先声明，会决定 T 节点有没有标题绘制器）。现在每种类型各留一份、各配一格绘制器；
  同类型仍走 `Combine`（先声明者优先）。
- **成员按前缀归属**：成员的每个「属于目标路径链」的分组特性各贡献自己那段路径。
  此前只有最深的那个参与装配，`[HorizontalGroup("H")] [BoxGroup("H/Box")]` 里的 H
  拿不到 Horizontal 那份特性——行会**静默不开**。不相关的分组仍被忽略（成员只归属最深一条链）。
- **类级分发不再漏掉第二个分组特性**：此前给第一个加完前缀就 `return`，
  成员身上的第二种会留在类级分组外面。
- **同层排序取最小的非零 `Order`**（全零则 0）：多份特性并存时若取「第一个的 Order」，
  排序会重新依赖声明顺序——而摆脱顺序依赖正是这次改动要买的东西。
- 不变量加强：节点上**每一份**分组特性的 `GroupID` 都必须等于节点路径
  （测试从「取第一份」改成逐份检查）。组合规则收在 Runtime README 的
  「分组特性的组合规则」一节。
- 实测修正一处文档：Runtime README 此前写「跨程序集覆写 `Combine` 必须声明为 `protected`」，
  实际相反——写 `protected` 报 `CS0507`，写 `protected internal` 通过。

### Fixed — 分组绘制器落在成员与根节点上

- **每个分组成员都在画第二个框，类级分组也在继续框住整个页。** 链装配按「特性实例 ×
  绘制器」配对、**不看节点种类**，而成员与根都保留着分组特性（装配只把它们搬进分组节点、
  从不删特性），于是那一格 `BoxGroupDrawer` 也配到了成员与根上——症状是双重框与重复标题。
  此前 CHANGELOG 宣称类级分组「框住整页」已修，实际只修了**归属**那一半。
- 修法是一处过滤：**分组特性只在分组节点上配绘制器**，根与成员携带它只是为了归属。
  先写成红的回归守卫（`GroupDrawerPlacementTests`，4 例）再改——那两条用例当时
  报的是 `Expected: -1, But was: 0`，即绘制器确在链上第 0 格。
- 顺带给 `BoxGroupDrawer` 补上显式 `[DrawerPriority(-130d)]`：这族要靠权重排序
  （节点上可能有多种分组特性），让它停在「普通特性默认值」上会误导后来者。

### Added — L1a：字段门控（Toggle）

- **`[Toggle("Enabled")]`**：字段前挂一个开关门控它能否编辑（开关在**值对象内部**，相对路径；
  官方示例 `[Toggle("Enabled")] public MyToggleable t;` 指的是 `t.Enabled`）。
- 门控走 `PropertyState.ReadOnlyResolver`：**解析在构建期一次**（处理器拿得到成员节点的值入口，
  `FindPropertyRelative` 在构建期就能做完），求值每帧只剩「读一个 bool」——与条件族同一分工。
  这是本包第一处**跨成员读写**：特性指向的不是自己，而是值对象里的另一个成员。
- 三处刻意的取舍：
  - **开关永远可点**（不在禁用罩里）——否则关掉就再也开不回来，门控成了单向闸门；
  - 解析失败**告警一次并保持可编辑**——拼错的名字不该让字段变得不可用（条件族同一条规矩）；
  - 叠加顺序写死为 条件族（0）< `[Toggle]`（50）< `[ReadOnly]`（100）< `[EnableGUI]`（110）：
    与 `[DisableIf]` 并存时开关赢，与 `[ReadOnly]` 并存时后者赢（恒只读比条件门控更具体）。
- `CollapseOthersOnExpand` 只留字段、不做行为（跨成员协调没有明确语义）——
  与 `[DrawWithUnity]` 的 `PreferImGUI`、`[PropertyRange]` 的 getter 字段同一条处置。

### Added — L1a：结构与门控

- **`[EnableGUI]`**：强制可编辑，处理器显式排在条件族与 `[ReadOnly]` 之后——它叫 Enable，
  就该能开（两者并存时谁赢由「谁更晚生效」决定，而不是「谁更具体」那种说不清的判断）。
- **`[TypeInfoBox(string)]`**：类级信息框。落在根节点、链照样包住子节点，
  与 `[Title]` 用在类上同一机制、零特例代码。
- **`[DrawWithUnity]`**：把属性交回 Unity 的绘制路径。权重 `SuperPriority`（-1000），
  画完 `PropertyField` **不调下一个**——链上更内侧的一切（包括 `[Indent]` 这类修饰）都不运行，
  这正是「交给 Unity」的含义。末端那层只读禁用罩被绕过，故自己处理只读。
- **`[ChildGameObjectsOnly]`**：引用不是本物体之下的子物体时告警。
  判定抽成 `ChildObjectValidator`（四个分支：自身 / 非子物体 / 非激活子物体 / 正常，
  另有「引用没有层级可言」的兜底）。与 Odin 的差异：官方还带「从子物体里挑」的选择下拉，
  本包**只做校验**——选择器要接管对象字段的拾取交互，是另一块工作，没有它本特性依然成立。
- **`[HideMonoScript]`**：类级，构建期把 `m_Script` **直接不建节点**。
  这是首个「按类型特性抑制成员」的构建期改动——Inspector 路径刻意保留脚本槽位以与原生一致，
  本特性是那条默认的显式退出；不写它时行为一字未变。

### Added — L1a：值控件

- **`[MultiLineProperty(int lines = 3)]`**：字符串画成多行文本域。标签画在文本域**上方**——
  多行控件与标签并排会被挤成半宽。
- **`[DelayedProperty]`**：值只在回车或失焦时提交。支持 `int`/`float`/`double`/`string`；
  **`long` 超出 `int` 范围时告警并退回普通绘制**——延迟控件只有 `int` 版本，
  硬用会把高位悄悄截掉（「看到的值不是真实的值」比不做更糟）。
- **`[EnumPaging]`**：枚举下拉框 + 前后翻页按钮（末尾绕回开头）。`[Flags]` 位标志没有顺序语义，
  告警并退回普通绘制。
- **`[PropertyRange(double min, double max)]`**：数值画成滑块。写入走 `BeginChangeCheck`，
  只有真的拖动才写回——避免「每帧把 float 精度的值写回 double 字段」这类精度损耗。
  多对象值不一致时退回普通绘制（滑块没有「混合值」形态，Unity 自己会显示「—」）。
  官方的三个 getter 形重载属 `$` 表达式族（推迟），`MinGetter`/`MaxGetter` 照保留、恒为 null。
- **`[Wrap(double min, double max)]`**：绘制后把值**回绕**到 `[min, max)`——等于上限回到下限
  （`[Wrap(0,360)]` 下 360 → 0，正是角度想要的）。跳过规则与钳制族共用同一份判定
  （多对象不一致、只读、非数值类型），理由也一样：不悄悄改数据。
- 回绕数学（含负值的取余归一化）在纯函数 `WrapValues` 里；flags 判定抽成
  `EnumSupport` 的每属性缓存，与 `[EnumToggleButtons]` 共用一份，避免两处各判一次。

### Added — L1a：绘制器链自证

- **`[ShowDrawerChain]`**：把该属性的绘制器链画成一张可展开的表——序号、绘制器名、权重、
  触发它的特性，以及「末端 · 构建期显式追加」。本包的核心就是「谁包住谁」由权重决定，
  这张表把它直接摊开：排查「某个特性怎么没生效」「谁画在外面」时比读代码快得多。
- 权重 `-950`（几乎最外，只让位给 `SuperPriority`），因此**它自己会出现在表里第 0 格**
  ——这不是花絮，正是「表是真的」那部分自证。
- 表内容在首次绘制时构建一次并缓存进 `PropertyState`：链在构建期装配后即冻结，
  逐帧重建字符串纯属浪费（每帧路径禁字符串拼接）。渲染不测（本仓策略），
  行内容由 `DrawerChainReport` 的纯函数产出、可无头断言。

### Added — L1a：校验与钳制

- **`[Required]`**：为空时在字段上方画一条提示框（默认文本「此字段为必填。」，可自定义消息与
  级别）。「空」的判定是本包自定的——null、空串、空集合算空，**纯空白串按非空**；数值与 bool
  无从为空，标在它们上会告警一次。**只提示、不拦保存**：Unity 的序列化层没有「拒绝写入」这个位置。
- **`[MinValue]`** / **`[MaxValue]`**：绘制后把值钳回范围内（官方只确认「钳制」这件事，时机是
  本包定的）。**三种情形跳过钳制**，理由都是「不悄悄改数据」：多对象值不一致、字段当前只读
  （只读意味着这个值不归你改，可能正被游戏逻辑持有）、非数值类型（告警一次）。
  整数边界取整：`[MinValue(2.5)]` 的最小合法整数是 3。
- **`[AssetsOnly]`** / **`[SceneObjectsOnly]`**：引用类型校验，不符时画警告框。
  判据是 `EditorUtility.IsPersistent`（预制体**实例**算场景对象）；空引用不算违反——
  那是 `[Required]` 的职责，两个特性各管一段。
- 判空、引用判定、钳制算术全在静态纯函数里（`RequiredValidator` / `ObjectReferenceWarning` /
  `ValueClamper`），绘制器只做「读值 → 画 → 调下一个」。
- 新增 `DrawerWarnings.Once`：告警按「属性 + 键」只报一次。绘制器每帧跑一遍，
  不设去重的话一次标错位置会刷出满屏告警，真正当回事的反被淹掉。

### Added — L1a：值绘制

- **`[DisplayAsString]`**、**`[ToggleLeft]`**、**`[ProgressBar]`**、**`[EnumToggleButtons]`**：
  四个**替换型**值绘制器——画完自己就结束，不调用链上的下一个。这是绘制器链本就支持的能力
  （「不调用下一个」等于把内侧藏起来），因此「值控件换成文本/开关/进度条/按钮排」没有特例代码。
- 绕过内侧的代价是**绕过末端那层只读禁用罩**（`UnityFallbackDrawer` 上的那圈
  `DisabledScope`），所以三者各自处理 `State.IsReadOnly`——否则 `[ReadOnly]` / `[DisableIf]`
  在这四个特性上会静默失效。`[DisplayAsString]` 除外：它本来就画不出可编辑的东西。
- 三条实现纪律：**类型不符时告警并调下一个**（不静默什么都不画）；**每属性状态进
  `PropertyState`**（枚举的掩码与按钮宽度解析一次就够）；**纯逻辑进静态纯函数**
  （`ValueTextFormatter`、`ProgressBarValues`——归一化、鼠标换算、文本化都可无头断言）。
- 两处与 Odin 的差异：`[ProgressBar]` **不钳制数据**，只把条画到端点（想钳制请配
  `[MinValue]`/`[MaxValue]`——绘制器悄悄改值是钳制族的职责）；`[DisplayAsString]` 只支持简单类型，
  数组与嵌套结构**退回普通绘制**而不是发明一种显示形状。
- `[DisplayAsString]` 的多对象显示沿用 Unity 的「—」占位符：值入口的 `GetValue()` 在这种情形下
  会抛（「没有单一当前值可读」），文本化因此直接读 `SerializedProperty`。

### Changed — 示例归位：展示台并入包内

- **新增 `Samples/AttributeShowcase/`**：逐特性展示台，每个特性一条，挂上组件即可逐条目视。
  原先它只存在于开发工程的 `Assets/Sandbox/`（不随包分发），于是包已发出 15+ 个特性，
  而第三方装完只能从示例里看到 `[Title]` 与 `[BoxGroup]` 两个。
- **`Samples/Overview/` 补上「附加」分组**：未分组字段现在真的夹在两个分组之间，
  「留在原位、不被挤到末尾」这条规则在包内自证，不再需要外部指引。
  原先它的注释指向开发工程里的一个演示文件——那条路径对第三方不存在，属于
  「包内指向包外」，一并修掉。
- 开发工程的 `Assets/Sandbox/` 随之缩为**对照组**：零特性基线、只带 Unity 原生装饰器的组件、
  宏自动接管的组件，都不进包——它们是「值管道有没有改变外观」的对照物，作为示例是噪音。
  沙盒场景的 `Demo 2` 改用包内示例 `OverviewComponent`（对象名不变），于是
  「显式编辑器 vs 自动接管渲染一致」这条对照顺带验证了随包发出的示例本身画得对。

### Added — L1a：信息框

- **`[InfoBox]`**：字段上方的信息框，可用在类上（则出现在整个 Inspector 最上方——
  类级特性落在根节点，绘制器链照样包住子节点，不是特例代码）。
  支持 `visibleIf`：条件名在**构建期**解析一次、绘制期每帧求值，与条件族同一套解析器
  （`ConditionResolver` 的 `Resolve` 提为 `internal TryResolve` 供复用，行为一字未改，
  既有条件族测试是这次重构的安全网）。
- **`[DetailedInfoBox]`**：摘要一行 + 可展开的详情。展开状态按属性隔离，
  **不跨会话持久化**（本包一贯的边界）。
- 条件的登记按**特性实例**为键（不是特性类型）：一个成员可以挂多个信息框，各带各的条件，
  用类型作键会让它们互相覆盖。解析失败时**不登记**，于是走「没有条件 ⇒ 显示」那条路
  ——拼错名字的表现若是「框不见了」，会被当成特性没生效。
- **`InfoMessageType`** 是本包自建的类型（`None`/`Info`/`Warning`/`Error`）：Runtime 零
  Unity 依赖，拿不到 `UnityEditor.MessageType`，Odin 也是同样的理由自己造了一个。

### Added — L1a：布局与外观

- **`[GUIColor]`**、**`[Indent]`**、**`[LabelWidth]`**、**`[HideLabel]`**、**`[PropertySpace]`**、
  **`[SuffixLabel]`**：六件「包住内侧」的修饰。都画完自己再调用下一个绘制器，
  因此可与分组、信息框、值绘制器任意嵌套，互不知晓对方存在。
- 三条实现纪律值得记住：**改全局状态的必须 `try/finally` 还原**（`GUI.color`、
  `EditorGUIUtility.labelWidth`、`EditorGUI.indentLevel`——漏还原会污染别的 Inspector，
  现象出现在别处、极难联想）；**`[HideLabel]` 只换传下去的标签参数**，不碰状态、不建对象；
  **`[SuffixLabel]` 在复合类型上回退为画在字段下方**——数组与嵌套结构进水平布局会被挤成半宽。
- 顺序不是审美而是功能：颜色必须在替换型值绘制器之外（否则染不到它），
  标签宽度必须在撤标签之前（否则作用在一个已经没有标签的字段上）。测试逐条钉住。
- 与 Odin 的一处刻意差异：`[SuffixLabel]` 不允许重复标注（Odin 允许）——
  多个后缀在右侧争同一块宽度没有明确语义，与其发明规则不如让它编译不过。

### Added — L1a：状态与标签

- **`[ReadOnly]`**：恒只读。走**处理器**而非绘制器——只读是状态不是像素，
  状态只有一份真相（`PropertyState.IsReadOnly`，绘制路径末端已按它上禁用罩）。
  与 `[DisableIf]` 并存时它赢：处理器显式排在条件族之后。
- **`[LabelText]`**：替换标签文本，可选地把 `playerScore` 变成 `Player Score`（`NicifyText`）。
  在**构建期**算好放进 `PropertyState.LabelOverride`，绘制期零成本；空白文本构造期报错。
- **`[PropertyTooltip]`**：把提示挂到标签上。与 `[LabelText]` 共用那个槽位，
  故处理器显式排在它之后——顺序反了提示会覆盖掉刚设好的文本。
- 三者都**不产生链格子**（处理器不参与绘制，测试钉住），因此不影响既有链顺序。

### Fixed — 自动接管的判据漏掉了处理器专有特性

- **只用处理器专有特性的类型不会被自动接管，特性于是静默失效。** 判据原本只查
  「有没有对应绘制器」，而条件族（`[ShowIf]` 等）**只有处理器、没有绘制器**——
  这类类型走的是原生 Inspector，特性一次都不会生效，且没有任何提示。
  - 判据现在同时查两张表：新增 `AttributeProcessor.HandledAttributeType`（与绘制器侧
    完全对称）与 `AttributeProcessorRegistry.HasProcessorForAttribute`。
  - 判据类从宏门控的自动接管程序集移进核心 Editor 程序集（`XInspectorUsageDetection`）：
    测试程序集引用不了宏门控程序集，逻辑留在那里这条缺陷就永远测不到。
  - 先写红的测试钉住它（改前必红）：只挂 `[ShowIf]` 的类型必须被判为「用到了本插件」。

### Verified — Unity 原生装饰器照常工作

- Unity 原生装饰器与内置绘制器（`[Header]` `[Space]` `[Range]` `[TextArea]` `[Multiline]` `[Tooltip]`）
  **照常工作**：它们作为**字段自身的特性**流经属性树，不产生额外节点（不存在「幽灵字段」），
  带装饰器的字段链条照旧把值交给 `PropertyField`——本包既不重复画、也不吞掉它们。
  - 这条推断原先没实测过，而 OdinGap 里标 ➖ 的 4 项全建立在它上面，故在补 L1a 之前先验掉。
  - 结构侧由 `NativeDecoratorTests`（4 例）钉住；**渲染侧不做自动化断言**——按本仓策略不测 IMGUI，
    改由开发工程里的 `NativeDecoratorDemo`（Demo 4，**不随包分发**）与无特性基线（Demo 1）目视对照。
    你若想自己复现：给自己某个带 `[Header]`/`[Range]` 的组件写三行编辑器即可，
    照 [`Samples/Overview/Editor/`](Samples/Overview/Editor/OverviewComponentEditor.cs) 那样。

### Added

- **`XInspectorEditorWindow`**：绘制**自身序列化字段**的编辑器窗口基类。继承、声明字段、
  写个 `[MenuItem]` 即可，窗口的 `OnGUI` 不必自己写。字段值随窗口布局持久化，
  域重载与编辑器重启后仍在。
  - 窗口内的编辑**不进 Undo**——窗口字段不属于场景也不属于资产，强登 Undo 会让用户
    按 Ctrl+Z 时撤销到窗口里的一个数字。补偿手段是 `ResetToDefaults()`（工具栏上有按钮）。
  - 用 `WindowMemberFilter` 排除 `EditorWindow` 自带的 7 个 `[SerializeField]` 内部字段
    （`m_MinSize`、`m_Pos`、`m_ViewDataDictionary` 等），以及脚本槽位 `m_Script`。
- `PropertyTreeHost` / `PropertyTreeReset` / `WindowMemberFilter`（均为 `internal`）：
  在非 Inspector 上下文托管属性树的机制，窗口基类与将来的入门窗口预览面板共用。
- `PropertyTree.Create` 的 `internal` 重载，接受成员过滤器。**默认行为一字未改**——
  Inspector 路径刻意保留 `m_Script` 槽位以与原生渲染一致。

### Added — 特性处理器层与条件族

- **`AttributeProcessor` / `AttributeProcessor<TAttribute>`**：构建期改写特性列表或属性状态的阶段，
  两个钩子——「自身带该特性」与「父级带该特性」。**不参与绘制。**
- **条件特性**：`[ShowIf]` `[HideIf]` `[EnableIf]` `[DisableIf]`（接序列化成员名），
  以及 `[HideInEditorMode]` `[HideInPlayMode]` `[DisableInEditorMode]` `[DisableInPlayMode]`。
  条件**每帧求值**，因此可随时跟随；条件名无效时保持可见并告警，不抛异常。
- **类级分组分发**：`[BoxGroup]`（及任何分组特性）标在类型上时，所有成员归入该分组，
  且**类级恒在最外层**——成员自己的分组会嵌在它里面。归属修好了；
  「类级 `[BoxGroup]` 会框住整个 Inspector」那一半当时**并未修掉**（根节点上那一格绘制器还在），
  见「Fixed — 分组绘制器落在成员与根节点上」。

### Changed

- **`PropertyState.IsReadOnly` 从存下来的 bool 改成算出来的**（与 `IsVisible` 同构）：
  新增 `ReadOnlyResolver` 与 `SetReadOnly(bool)`，**原 setter 移除**。
  `[EnableIf]` 需要每帧求值的只读状态，一个 bool 存不下条件。

## [0.1.0-preview.1] - 2026-10-03

首个预览版。本轮只交付**开发模板与核心管线骨架**，以及一条端到端可跑通的垂直切片；
序列化后端、样式系统与编辑器窗口不在本轮范围内。

### Added

- 仓库骨架：完整 Unity 工程 + 包本体（`Assets/XInspector/`）。
- `Venusir.Xinspector` 程序集，及首个特性族：
  - `[Title]`（可用在类与成员上）
  - `[BoxGroup]`，基于点分路径的分组惯例（`"Outer/Inner"` 自动合成祖先节点）
  - `PropertyGroupAttribute`，分组特性的公共基类
- `XInspector.Editor` 程序集，及核心管线：
  - `PropertyTree` / `PropertyTreeBuilder` / `InspectorProperty` / `PropertyState`
  - 值入口 `PropertyValueEntry` / `SerializedPropertyValueEntry`（`SerializedObject` 后端）
  - 绘制器链 `DrawerChain` / `XInspectorDrawer` / `AttributeDrawer<T>` / `DrawerPriority`
  - 绘制器发现 `DrawerTypeRegistry`（扫描全部已加载的编辑器程序集，支持使用方无注册扩展）
  - 分组装配：点分路径、祖先节点自动合成、分组节点落在首个成员处
  - `XInspectorEditor`，Unity 集成入口
- `Venusir.Xinspector.AutoEditor` 程序集：由 `XINSPECTOR_AUTO_EDITOR` 宏门控的自动接管编辑器。
  使用方项目未定义该宏时，该程序集根本不参与编译，行为与没装本插件一致。
- `Venusir.Xinspector.Tests` 与 `Venusir.Xinspector.Editor.Tests` 测试程序集。
- `Samples/Overview` 示例。

### Not included

本预览版**不含**特性处理器层（`AttributeProcessor`）。它原本的用途是把类级特性合成到
其它节点上，但构建期已把类型特性直接放在根节点，这件事不再需要；剩下的潜在用户
（`[ShowIf]` 改属性状态、类级 `[BoxGroup]` 分发到成员）都尚未实现。
等 `[ShowIf]` 到来时会一并补上——那是纯新增，不改动任何既有签名。

其余不在范围内的项见包 README 的「已知限制」。
