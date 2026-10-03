# Changelog

本文件记录 XInspector 的所有值得注意的变更。

格式参照 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [0.1.0-preview.1] - 2026-10-03

首个预览版。本轮只交付**开发模板与核心管线骨架**，以及一条端到端可跑通的垂直切片；
序列化后端、样式系统与编辑器窗口不在本轮范围内。

### Added

- 仓库骨架：完整 Unity 工程 + 内嵌 UPM 包（`Packages/com.xinspector/`）。
- `XInspector.Runtime` 程序集，及首个特性族：
  - `[Title]`（可用在类与成员上）
  - `[BoxGroup]`，基于点分路径的分组惯例（`"Outer/Inner"` 自动合成祖先节点）
  - `PropertyGroupAttribute`，分组特性的公共基类
- `XInspector.Editor` 程序集，及核心管线：
  - `PropertyTree` / `PropertyTreeBuilder` / `InspectorProperty` / `PropertyState`
  - 值入口 `IPropertyValueEntry` / `PropertyValueEntry<T>`（`SerializedObject` 后端）
  - 绘制器链 `DrawerChain` / `XInspectorDrawer` / `AttributeDrawer<T>` / `DrawerPriority`
  - 绘制器发现 `DrawerTypeRegistry`（扫描全部已加载的编辑器程序集，支持使用方无注册扩展）
  - 特性处理器 `AttributeProcessor` / `AttributeProcessorRegistry`
  - `XInspectorEditor`，Unity 集成入口
- `XInspector.AutoEditor` 程序集：由 `XINSPECTOR_AUTO_EDITOR` 宏门控的自动接管编辑器，
  默认休眠。未定义该宏时该程序集根本不参与编译。
- `XInspector.Tests.Runtime` 与 `XInspector.Tests.Editor` 测试程序集。
- `Samples~/Overview` 示例。
