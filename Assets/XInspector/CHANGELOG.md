# Changelog

本文件记录 XInspector 的所有值得注意的变更。

格式参照 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

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
