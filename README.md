# XInspector（开发仓库）

Unity 6 的**特性驱动型可编程 Inspector 管线**，模仿 [Odin Inspector](https://odininspector.com/) 的体验，
以内嵌 UPM 包的形式开发。

## 这个仓库是什么

一个**完整 Unity 工程**，包本体位于 [Packages/com.xinspector/](Packages/com.xinspector/)。
之所以用工程而不是裸包仓库，是为了让开发期的迭代闭环最短：改完代码直接在编辑器里看效果、
直接跑 Test Runner、直接导入示例——不需要另开一个工程来引用。

| 路径 | 会不会随包发布 |
|---|---|
| `Packages/com.xinspector/` | **会**。这就是包本体 |
| `Assets/Sandbox/` | 不会。开发用演示组件与场景 |
| `Tools/` | 不会。测试与文档门禁脚本 |
| `ProjectSettings/`、`Packages/manifest.json` | 不会。开发工程的壳 |

## 快速开始

```powershell
# 新机器上跑一次，建立测试壳（通过 junction 共享 Assets/Packages/ProjectSettings，
# 但拥有独立 Library——因此跑测试不需要关闭编辑器）
pwsh -File Tools/run-tests.ps1 -Setup

# 日常：改完一个模块定向跑
pwsh -File Tools/run-tests.ps1 -Fixture DrawerChainTests

# 门禁：全量双平台，0 失败
pwsh -File Tools/run-tests.ps1
```

文档门禁（包内 XML 注释告警为 0 + 所有源文件参与编译 + 所有资源有 `.meta`）：

```powershell
pwsh -File Tools/check-docs.ps1 -Enforce
```

## 文档在哪

| 文档 | 收件人 |
|---|---|
| [Packages/com.xinspector/README.md](Packages/com.xinspector/README.md) | 引入包的第三方——设计哲学、快速开始、已知限制 |
| [CLAUDE.md](CLAUDE.md) | 本仓的工程规则与约定 |
| `Documentation/` | 维护向记录 |

## 使用方怎么装

```
https://github.com/venusir/xinspector.git?path=/Packages/com.xinspector
```

`?path=` 是必需的——包在仓库的子目录里。这是「完整工程 + 内嵌包」布局的代价，换来的是
最短的开发闭环。

## 许可

MIT，见 [Packages/com.xinspector/LICENSE.md](Packages/com.xinspector/LICENSE.md)。
