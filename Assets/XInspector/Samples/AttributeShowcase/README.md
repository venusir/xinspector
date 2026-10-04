# AttributeShowcase 示例

逐个演示 XInspector 提供的特性。想看**最小可用形态**（三行接入 + 分组嵌套）请看
`Samples/Overview/`；想看**每个特性长什么样**就是这里。

## 怎么用

1. 场景里新建一个空物体（或直接用任意现有物体）；
2. 把 `AttributeShowcase` 挂上去；
3. 选中它，看 Inspector。字段按主题分成三段，逐条目视即可。

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

### 值绘制

| 特性 | 预期看到 |
|---|---|
| `[DisplayAsString]` | 值画成只读文本，可选中复制，不带可编辑控件 |
| `[DisplayAsString(true)]` | 文本折行显示全，而不是裁成一行 |
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

### 结构与门控

| 特性 | 预期看到 |
|---|---|
| `[ReadOnly]` + `[EnableGUI]` | 字段仍**可编辑**——EnableGUI 排在只读之后，它赢 |
| `[DrawWithUnity]` | 该字段由 Unity 原生绘制；叠在它内侧的 `[Indent]` **不生效**（这正是「交给 Unity」的含义） |
| `[ChildGameObjectsOnly]` | 拖入非子物体时出现警告框（只提示，不拦赋值） |
| `[Toggle("Enabled")]` | 字段前的开关关掉时字段变灰；**开关本身永远可点**（否则关掉就开不回来） |
| `[TypeInfoBox]`（类级） | Inspector 最顶部一条信息框 |
| `[HideMonoScript]`（类级） | 脚本槽位（Script 字段）消失 |

### 范围与滑块

| 特性 | 预期看到 |
|---|---|
| `[MinMaxSlider(0f, 100f)]` | 双滑块：拖左把手改 `x`、右把手改 `y`，两个把手不许交叉 |
| `[MinMaxSlider(-10f, 10f, true)]` | 同一条滑块，左右各多一个可输入的数值框 |
| `[MinMaxSlider("dynamicRange", true)]` | 量程**取自另一个成员**（序列化 `Vector2`，x/y 即上下限）——改 `dynamicRange` 的值，这条滑块的可拖范围立刻跟着变 |

> 边界也可以是**成员名**：Odin 那边这个字符串是 resolved string（支持 `@` 表达式与方法调用），
> 本包只认序列化成员名——与条件族同一条边界。

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
