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

### 信息框

| 特性 | 预期看到 |
|---|---|
| `[InfoBox(..., Info)]` | 恒显示的信息框 |
| `[InfoBox(..., Warning, nameof(showWarning))]` | 勾上 `showWarning` 才出现——**字段本身照常绘制**，条件只作用于信息框 |
| `[DetailedInfoBox]` | 摘要一行，详情折起来 |

## 这些特性是怎么画出来的

每个特性都是「特性类 + 绘制器」的普通配对：绘制器做点事，然后调用链上的下一个。
`[LabelText]` 改标签、`[Indent]` 推进缩进、然后交给下一个绘制器画值——
**新增一个特性是纯加法**，不需要修改任何既有绘制器，也没有「类级特例」这回事。
`[GUIColor]` 用在类上能染整页，只是因为类级特性也落在根节点上，走的是同一条路径。

「值绘制」那一组是另一类：`[DisplayAsString]`、`[ToggleLeft]`、`[ProgressBar]`、`[EnumToggleButtons]`
**不调用下一个绘制器**——它们把值控件整个换掉。这也是链条本来就支持的能力
（不调用下一个就等于把内侧藏起来），同样没有特例代码。
代价写在实现里：绕过内侧的代价是**绕过末端那层只读禁用罩**，所以这几个绘制器各自处理只读。
