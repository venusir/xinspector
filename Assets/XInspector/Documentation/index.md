# XInspector

特性驱动的可编程 Inspector 管线，用于 Unity 6。

> **状态：** `0.1.0-preview.1` — 只有骨架与一条垂直切片。API 尚未稳定。

## 这套管线解决什么

Unity 默认的 `PropertyDrawer` 模型是「首个匹配者胜出」：一个属性最多被一个绘制器画完，
那个绘制器必须独自处理标签、字段、修饰与分组的一切。XInspector 换成一条**绘制器链**——
所有匹配的绘制器依次叠加，每个都可以「做点事，然后调用下一个」。于是
`[BoxGroup]` 包住 `[Title]`、`[Title]` 再包住字段，是链条的自然结果而非特例，
而新增特性是纯加法。

## 从这里开始

完整的安装步骤、快速开始、核心概念表与已知限制，见 [README](../README.md)。

要点速览：

- 装：把 `XInspector/` 整个拷进你工程的 `Assets/` 下。**不是 UPM 包**，因此升级要覆盖
  整个目录——改过包内文件就会丢。详见 [README 的安装说明](../README.md)
- 接管方式：**不自动接管**。为类型写 `[CustomEditor]` + 继承 `XInspectorEditor`，三行。
  想自动接管就定义脚本宏 `XINSPECTOR_AUTO_EDITOR`（可逆，按项目生效）。
- Runtime 侧**零第三方依赖**。

## 已知限制

本轮刻意不包含：自定义序列化后端、样式系统、编辑器窗口、数组展开、
`[ShowIf]` / `[FoldoutGroup]` / `[Button]`、`[SerializeReference]` 类型切换、
折叠状态的跨会话持久化、UI Toolkit。

完整清单与说明见 [README 的「已知限制」](../README.md#已知限制)。
