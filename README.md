# XInspector（开发仓库）

Unity 6 的**特性驱动型可编程 Inspector 管线**，模仿 [Odin Inspector](https://odininspector.com/) 的体验。

## 这个仓库是什么

一个**完整 Unity 工程**，包本体位于 [Assets/XInspector/](Assets/XInspector/)，与工程壳
`Assets/Sandbox/` 平级。用工程而不是裸包仓库，是为了让开发期的迭代闭环最短：改完代码
直接在编辑器里看效果、直接跑 Test Runner、示例就在工程里。

| 路径 | 会不会随包分发 |
|---|---|
| `Assets/XInspector/` | **会**。这就是包本体 |
| `Assets/Sandbox/` | 不会。开发用演示组件与场景 |
| `Tools/` | 不会。测试与文档门禁脚本 |
| `ProjectSettings/`、`Packages/` | 不会。开发工程的壳 |

> ⚠️ **包放在 `Assets/` 下意味着它不是一个可安装的 UPM 包。** Unity 的 Package Manager
> 只认 `Packages/` 里的内嵌包与 registry/git 来源，`Assets/` 里带 `package.json` 的目录
> 在它眼里只是普通资源。因此本包**不能**经 git URL 安装、不出现在 Package Manager、
> 也无从发布到 registry。分发方式是**拷贝文件夹**。
>
> 这是刻意的取舍：换来的是代码在 `Assets/` 下一眼可见，与姊妹工程 XFramework 的布局一致。
> `package.json` 保留着，日后若要搬回 `Packages/`（移动时连 `.meta` 一起挪，GUID 不变，
> 不会有断链），它就是现成的。

## 快速开始

```powershell
# 新机器上跑一次，建立测试壳（通过 junction 共享 Assets/Packages/ProjectSettings，
# 但拥有独立 Library——因此跑测试不需要关闭编辑器）
pwsh -File Tools/run-tests.ps1 -Setup

# 日常：改完一块定向跑
pwsh -File Tools/run-tests.ps1 -Fixture DrawerChainTests

# 门禁一：全量双平台，0 失败
pwsh -File Tools/run-tests.ps1

# 门禁二：离线测试（只覆盖 Runtime 逻辑，约 20 毫秒）
dotnet test Tests.Native/Tests.Native.csproj

# 门禁三：文档（包内 XML 告警 0 + 所有源文件参与编译 + 所有资源有 .meta）
# 需先在 Unity 里开一次工程生成 .csproj
pwsh -File Tools/check-docs.ps1 -Enforce
```

具体命令、参数手感与踩过的坑见 [Documentation/Workflow.md](Documentation/Workflow.md)。

## 文档在哪

| 文档 | 收件人 |
|---|---|
| [Assets/XInspector/README.md](Assets/XInspector/README.md) | 引入包的第三方——设计哲学、快速开始、已知限制 |
| [CLAUDE.md](CLAUDE.md) | 本仓的工程规则与约定 |
| [Documentation/](Documentation/) | 维护向记录：流程、路线图、已否决形状、审计手册 |

## 使用方怎么装上

把 `Assets/XInspector/` 整个目录**拷进你的工程的 `Assets/` 下**即可。没有 Package Manager
流程，也没有依赖需要先装——本包 Runtime 侧零第三方依赖。

拷进去之后：

1. 代码在 `Assets/XInspector/` 下参与编译（asmdef 会把它编成独立程序集）；
2. 给类型加 `[Title]` / `[BoxGroup]` 等特性；
3. 为类型写编辑器：`[CustomEditor(typeof(X))] class XEditor : XInspectorEditor { }`；
4. 示例在 `Assets/XInspector/Samples/Overview/`，直接挂组件就能看。

> **升级时注意：** 拷贝方式意味着升级要覆盖整个目录。若你改过包内任何文件，覆盖会丢掉
> 那些改动——这是放弃 Package Manager 的直接代价。

## 许可

MIT，见 [Assets/XInspector/LICENSE.md](Assets/XInspector/LICENSE.md)。
