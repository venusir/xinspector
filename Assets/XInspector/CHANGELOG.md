# Changelog

本文件记录 XInspector 的所有值得注意的变更。

格式参照 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Added — L1a（三）：状态与标签

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
    改由沙盒的 `NativeDecoratorDemo`（Demo 4）与无特性基线（Demo 1）目视对照。

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
  且**类级恒在最外层**——成员自己的分组会嵌在它里面。这修掉了此前
  「类级 `[BoxGroup]` 会框住整个 Inspector」那条已知限制。

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
