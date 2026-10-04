# Workflow

流程与命令。**规则**在 [CLAUDE.md](../CLAUDE.md)，本文件不重复规则，只写怎么执行。

---

## 一、三条门禁

阶段收尾三条都要过。日常开发按需跑前两条。

```powershell
# 1. 全量测试，双平台，0 失败即通过
pwsh -File Tools/run-tests.ps1

# 2. 离线测试（Runtime 侧逻辑，约 20 毫秒）
dotnet test Tests.Native/Tests.Native.csproj --nologo

# 3. 文档门禁：包内 XML 告警 0 + 所有源文件参与编译 + 所有资源有 .meta + 沙盒顶层组件与场景一一对应
pwsh -File Tools/check-docs.ps1 -Enforce
```

### 第 1 条：Unity 测试

```powershell
pwsh -File Tools/run-tests.ps1 -Setup                      # 新机器一次，建 XInspector.TestRun 壳
pwsh -File Tools/run-tests.ps1 -Fixture DrawerChainTests   # 日常定向
pwsh -File Tools/run-tests.ps1 -Filter "PropertyGroup"     # 按正则（不是子串）
pwsh -File Tools/run-tests.ps1                             # 门禁：双平台
```

- **不带 `-Filter`/`-Fixture` 时跑双平台**，任一平台有失败即非 0 退出。
- **带 `-Filter` 且未显式给 `-Platform` 时只跑 PlayMode**——这是为了保持日常定向跑的耗时手感。
  代价是 `Tests/Editor/` 下的用例**不会跑**，看起来「绿了」其实是假绿。
  要跑编辑器侧的过滤器请显式加 `-Platform EditMode`。
- **`-Fixture` 用转义的点锚定类名**（内部生成 `\.类名\.`）。想当然的写法会误捞：
  `PoolTests`、`PoolTests.`、`.PoolTests.` 实测都会连 `CollectionPoolTests` 一起捞。
- **测试壳**：脚本默认用仓库旁的 `<仓库名>.TestRun`（通过 junction 共享
  Assets/Packages/ProjectSettings 而拥有独立 Library），因此**跑测试不需要关闭编辑器**。
  壳不存在时回退到仓库本体，此时必须先关编辑器，否则争 Library 锁。
- 全量跑会与上次的用例总数比较、骤降时告警（防「测试集静默缩水」）。
  基线记在 `TestResults/last-count-<Platform>.txt`，有意删用例时删掉该文件即可重置。

### 第 2 条：离线测试

`Tests.Native/` 是脱离 Unity 的 .NET NUnit 工程，覆盖 Runtime 侧全部逻辑
（47 例，约 20 毫秒）。它把「Runtime 零 Unity 依赖」这条契约变成**编译期强制**——
往 Runtime 里写 `using UnityEngine;` 会让它直接编译失败。

**不覆盖** Editor 侧：属性树、绘制器链、分组装配、标题绘制全部依赖
`SerializedObject`/`ScriptableObject`，原生宿主下跑不了。别把它当成第 1 条的替代品。

改了 Runtime 代码时先跑这条——它比 Unity 快三个数量级，能立刻给出反馈。

### 第 3 条：文档门禁

**前置条件：需要 `.csproj`，而它只在 GUI 编辑器里生成。** 批处理（`-batchmode`）
不写出 csproj——反射调用 `SyncVS.SyncSolution` 确实会执行（日志里有
`SyncVS.PostprocessSyncProject`），但文件不落盘。因此**新克隆的仓库、以及纯批处理
环境里这条门禁跑不起来**，脚本会因「有 N 个源文件未参与编译」判失败。

这**不是脚本坏了，恰恰是它拒绝给出假的绿色**：没有 csproj 时「0 条告警」毫无意义。
处置：在 Unity 里打开一次工程，csproj 即生成，之后门禁可跑。

门禁守四件事：包内 XML 文档告警为 0、所有源文件都已参与编译、所有资源都有 `.meta`、
沙盒顶层组件与 `Sandbox.unity` 一一对应（见下）。
它是**独立的一条通道**——Unity 生成的 csproj 没设 `DocumentationFile`，
**默认编译根本不检查文档注释**，不开这一枪则写坏文档不会有任何反馈。

三处本包特有的处理，改脚本时别删：

- `Editor/AutoEditor/` 被排除出「是否参与编译」检查——它是宏门控的，宏关掉时
  那里的源文件本来就不该被编译。
- 「缺 `.meta`」检查跳过**点开头**的条目——Unity 不给它们生成 meta。
  包在 `Packages/` 时还有一条处理 `~` 结尾目录的规则（`-Recurse` 会钻进 `Samples~/`
  内部，那里的文件同样没有 meta）。搬到 `Assets/` 后波浪号已去掉，那条成了永不触发的
  死代码，已删。
- **沙盒对齐检查的作用域是 `Assets/Sandbox/`，与上面三项的包内作用域分开写。**
  沙盒不随包分发，但仓库内的一致性一样是门禁的事。只做单向检查（顶层脚本 → 场景）：
  重建后的场景里可能有来自包内的组件（如 `Samples/Overview` 的 `OverviewComponent`），
  它不是沙盒顶层的脚本，反向查会误报。也**不做整场景逐字节比对**——Unity 给场景对象
  分配的 fileID 依赖构建顺序与会话，逐字节比对太脆，改一次就红。

### 沙盒场景怎么重建

`Assets/Sandbox/Sandbox.unity` 是**生成物**：它由 `SandboxSceneBuilder` 建，不是手改的
（手写 `.unity` 的 YAML 容易写出「能打开但设置怪异」的文件，让 Unity 自己创建格式永远是对的）。

**什么时候要重建**：在 `Assets/Sandbox/` 顶层加/删了对照组组件之后。忘了重建会被上面第 3 条
门禁拦下——2026-10 就发生过一次：场景里的对象比组件少两个，而 `Documentation/OdinGap.md`
的目视验证步骤正指着其中一个不存在的对象，照文档做的人会得出「原生装饰器没流经管线」
这种反向结论。

```powershell
# 菜单入口（编辑器里）
Tools/XInspector/重建 Sandbox 场景

# 批处理等价入口（走测试壳，编辑器可保持开启）
& "D:\Program Files\Unity\<版本>\Editor\Unity.exe" -batchmode -nographics -quit `
  -projectPath "<仓库>.TestRun" `
  -executeMethod XInspector.Sandbox.EditorTools.SandboxSceneBuilder.CreateSandboxScene `
  -logFile "<绝对路径>.log"
```

**对象名不要改**：文档（OdinGap 等）按名引用 `Demo 1` … `Demo 4`。

---

## 二、提交拆分与授权

- **计划阶段即定原子提交边界**，并写明每个提交的验证命令。实现按提交逐个完成、
  跑绿验证后再提交——避免「先全量实现、提交前再拆分」，那样混合文件需反复编辑还原，易出错。
- 拆分依据是**逻辑边界与可独立编译**；同源小改动可合并为一个提交。
- **计划批准即授权本阶段的全部提交**：每个提交跑绿计划里写明的验证命令后**直接提交**，
  不必逐次请示。
- **两类例外仍须单独确认**：公开 API 变更、破坏性变更（含仅对仓内的）。
- 提交信息除代码关键字外一律中文，**不加署名尾注**，**不自动推送**。

一个实测教训：**`git add A B` 在 B 失败时，A 仍可能已被暂存。**
当时 `git add <被忽略的 csproj> .gitignore` 报错说 csproj 被忽略，以为整条命令没生效，
改完 .gitignore 就直接 commit 了——结果提交进去的是修正前的版本。
`git add` 后若报错，**回头确认哪些已进暂存区**（`git diff --cached --name-only`），
别假设它整体失败了。

---

## 三、改前必红

修缺陷或改行为时，先写一个**能复现的失败**，再动生产代码。理由：

- 它证明你真的定位到了原因，而不是改了一处让现象消失；
- 它顺带产出一个回归守卫——将来同样的错误会被挡住。

本包已有几处这样的守卫，都是这么来的：
`Build_各节点的序列化属性互相独立`（`GetIterator` 实例复用）、
`CloneForPath_保留子类字段`（防止有人「顺手改成 new」）、
`Draw_重入后游标被恢复`（游标保存/恢复）。

**区分实测与推理。** 写进提交信息或文档的结论要标明是哪种。本包踩过的两类：

- **实测**：`Assembly.GetReferencedAssemblies()` 返回的是编译器**实际发出**的引用，
  未被使用的会被裁掉——这条是把测试跑红之后才确认的，不是从文档读来的。
- **实测**：Unity 会跳过没有任何脚本的 asmdef（日志原话 `will not be compiled,
  because it has no scripts associated with it`）。

---

## 四、Rules 与 Workflow 的分工

| 文件 | 写什么 |
|---|---|
| `CLAUDE.md` | 规则与约束：「不得」「一律」「必须」。改了会出事的那些 |
| `Documentation/Workflow.md`（本文件） | 流程与命令：怎么跑、什么顺序、踩过什么坑 |
| `Documentation/Modules/<模块>.md` | 维护向记录：沿革、已否决形状、未决项 |
| `Documentation/Roadmap.md` | 还没做的候选：该不该做、边界画在哪 |
| `Documentation/ModuleAudit.md` | 审计手册：判据清单、报告格式、结论归档落点 |
| `<包>/README.md` | 引入包的第三方要看的：快速开始、已知限制 |
| `<包>/Runtime|Editor/README.md` | 使用方的行为契约与扩展指南 |

**一处只写一份真相。** 若发现同一件事在两处都有，删掉一处留指针。
