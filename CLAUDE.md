# XInspector 项目规则

## 项目定位

- 本项目是 Unity 的特性驱动型可编程 Inspector 管线（`Assets/XInspector/`），
  以**拷贝文件夹**的方式供其他项目引入，模仿 Odin Inspector 的体验。
  **它不是 UPM 包**——见「仓库布局与边界规则」
- Unity 版本：`6000.4.5f1`（开发环境）；`package.json` 最低要求 `6000.3`（与 XFramework 一致）
- 命名空间根：`XInspector`（Runtime）/ `XInspector.Editor` / `XInspector.Editor.AutoEditor`
- 程序集名**与命名空间不同**，规则对齐 XFramework 的「公司.产品[.Editor]」：

  | 程序集 | 命名空间 |
  |---|---|
  | `Venusir.Xinspector` | `XInspector` |
  | `Venusir.Xinspector.Editor` | `XInspector.Editor` |
  | `Venusir.Xinspector.AutoEditor` | `XInspector.Editor.AutoEditor` |
  | `Venusir.Xinspector.Tests` | `XInspector.Tests` |
  | `Venusir.Xinspector.Editor.Tests` | `XInspector.Tests.Editor` |

  改程序集名时，`InternalsVisibleTo`、asmdef 的 `references`、以及测试里按字符串查程序集的常量
  都要同步——漏改的表现分别是「测试程序集编译不过」与「运行时找不到类型」。
  包尚未发布时改是零成本；发布后对**自带 asmdef 的使用方**就是破坏性变更了。
- **Runtime 侧零第三方依赖**——这是它能被任何项目安全引入的前提，不要在 Runtime 引第三方包
- 当前版本 `0.1.0-preview.1`，只有骨架与一条垂直切片，API 尚未稳定

## 仓库布局与边界规则

包本体在 `Assets/XInspector/`，**会**随包分发；其余都是工程壳，**不会**：

| 路径 | 会不会分发 |
|---|---|
| `Assets/XInspector/` | **会** |
| `Assets/Sandbox/` | 不会。开发用演示组件与场景 |
| `Tools/` | 不会。测试与文档门禁脚本 |
| `ProjectSettings/`、`Packages/` | 不会。开发工程的壳 |

推论：**开发用代码绝不进包**；**包文件绝不 gitignore**；**包下所有 `.meta` 一律提交且绝不手改**
（`.meta` 由 Unity 生成，不是手写的）。

### 边界靠约定，不靠位置

包本体与工程壳**都在 `Assets/` 下平级**，因此不能再靠「在不在 `Packages/`」区分——只能靠
目录名与这条约定：**`Assets/XInspector/` 之外的一切都是工程壳**。往包里加东西前先问一句：
这东西是给使用方的吗？

这是把包从 `Packages/com.xinspector/` 搬到 `Assets/` 的**直接代价**：原先位置上自带边界，
现在边界得靠人守。搬家的理由与放弃的能力见
[Documentation/Modules/Pipeline.md](Documentation/Modules/Pipeline.md) 的「已否决的形状」。

### 为什么不在 Packages/

Unity 的 Package Manager 只认 `Packages/` 里的内嵌包与 registry/git 来源。搬出来意味着
**本包在开发工程里不再是内嵌包**，分发方式改为**拷贝文件夹**——升级要覆盖整个目录，
使用方改过包内文件就会丢。

**别把影响说过头：`?path=` 与包放在哪无关。** 它只要求「路径相对仓库根」且该子目录含
`package.json`，所以 `?path=/Assets/XInspector` 是可用的 git 依赖——`Packages/` 那条约束
管的是使用方工程里的**内嵌包**。搬家的真实代价与「哪些其实没丢」见
[Documentation/Modules/Pipeline.md](Documentation/Modules/Pipeline.md) 第 11 条。

换来的是代码在 `Assets/` 下一眼可见，与姊妹工程 XFramework 布局一致。

**可逆：** 移动时连 `.meta` 一起挪则 GUID 不变，asmdef 名与 C# 命名空间也不受影响，
搬回 `Packages/` 是纯路径操作。`package.json` 保留标识字段正是为此。

## 架构要点

管线是「属性树 + 绘制器链」，与 Unity 原生 `PropertyDrawer`（首个匹配者胜出、一个绘制器画完一切）
的根本差别在于：**所有匹配的绘制器依次叠加，每个都可以「做点事，然后调用下一个」**。
`[BoxGroup]` 包住 `[Title]`、`[Title]` 再包住字段，是链条顺序的自然结果而非特例代码，
所以新增特性是纯加法。

六个定盘决定，改动其中任何一个前先读完理由：

1. **值后端是 `SerializedObject`**（`PropertyValueEntry` 是那条缝）。它买下 Undo/Redo、
   预制体覆盖、场景标脏、多对象编辑、域重载后取值这五件事。代价是只能画 Unity 会序列化的
   成员。「画普通属性」需要另一套后端，届时新增一个派生类即可。
2. **绘制器是无状态共享单例**。每种类型全工程只实例化一个（500 字段的 Inspector 不会因此
   产生两万个对象）。代价是**绘制器里不得有可变字段**，每属性状态一律进 `PropertyState`。
   违反这条的症状是「展开一个、全都展开了」，很难联想到原因。
3. **链条末端由构建期显式追加，不进注册表**。末端是结构性的（根/分组接 `ChildrenDrawer`、
   成员接 `UnityFallbackDrawer`）。交给注册表意味着一个写错的匹配条件就能让某属性链为空，
   症状是「它静默地什么都不画」——最难归因的一类问题。
4. **绘制器链的排序是「权重升序 + 序号兜底」**（小 = 外层）。序号兜底必不可少：
   `List.Sort` 是不稳定排序，只比权重的话同权重格子的顺序会随元素个数变化。
5. **分组用点分路径，祖先节点自动合成**。不变量：**分组节点上的分组特性恒满足
   `GroupID == node.Path`**。节点落在其首个成员出现的位置，故夹在分组字段之间的未分组字段
   留在原地。同层分组之间按 `Order` 重排，但**只重排分组彼此之间的先后，不动未分组成员的位置**。
6. **每个属性一份独立的特性实例**。成员特性靠反射天然如此（每次调用返回新实例）；
   类级特性若将来要分发到多个成员，必须显式克隆，否则一个实例被几十个成员共享、改一处串一片。

### 包结构

```
Runtime/                     Venusir.Xinspector
  Attributes/                公开特性（Title、Groups/BoxGroup…）
  Internal/                  内部工具（PropertyGroupPath）
Editor/                      Venusir.Xinspector.Editor
  PropertyTree.cs            树；绘制入口
  PropertyTreeBuilder.cs     遍历成员 → 分组装配 → 装配链条
  InspectorProperty.cs       节点（纯数据 + 一次派发）
  PropertyState.cs           每属性的可变状态
  Values/                    值后端
  Drawers/                   绘制器：基础类、链、注册表、BuiltIn/、Terminals/
  Windows/                   XInspectorEditorWindow（公开）+ 托管机制（internal）
Editor/AutoEditor/           Venusir.Xinspector.AutoEditor（宏门控，见下）
Tests/Runtime/               PlayMode
Tests/Editor/                EditMode
Samples/Overview/            示例（随工程直接存在，挂上组件即可看）
```

## 自动接管与 `XINSPECTOR_AUTO_EDITOR` 宏

`Venusir.Xinspector.AutoEditor` 程序集由脚本宏 `XINSPECTOR_AUTO_EDITOR` 门控：宏未定义时它**根本不参与
编译**，全局接管这件事在项目里就不存在。

**本开发工程刻意把该宏开着**（`ProjectSettings.asset` 的 `scriptingDefineSymbols`）。
理由值得记住：**门控代码若从不参与编译，就会静默腐烂**。实测踩过一次——该程序集一直没被编译，
直到首次开宏才暴露出 `InternalsVisibleTo("Venusir.Xinspector.AutoEditor")` 漏写、编译不过。
开着宏，全量测试这道门禁就覆盖得到它。

即便如此，它仍是**按项目生效、可逆**的：使用方项目没有这个宏，行为与没装本插件一致。

自动编辑器用 `DrawDefaultInspector()` 回退：`OnEnable` 里判断目标类型是否真的用到了本插件
（判据是「有没有对应绘制器」，见 `DrawerTypeRegistry.HasDrawerForAttribute`），
没有就走 Unity 原生绘制。因此**即使开了宏，没用到本插件的类型外观也不变**。

## 编码规范

- **命名：** 接口 `I` 前缀；私有/受保护字段 `_camelCase`；常量 PascalCase；方法 `TryXxx(out T)`、
  `GetOrCreateXxx`；bool 属性 `IsXxx`。**标识符一律英文**（含测试方法名），中文只出现在注释与文档里
- **风格：** Allman 大括号；`#region` 按功能分区；using 按 System → 第三方 → XInspector 排序
- **注释：** 全中文 XML doc，公开 API 必带 `<summary>`
  - **XML 必须能解析：** 泛型尖括号与 `&` 一律转义（`&lt;T&gt;`、`&amp;`）。未转义触发 CS1570
    会使**整条注释被编译器丢弃**，且块内 `cref` 一律不再被检查——死链因此隐形
    （它表现为「**没有**告警」而不是「有告警」）
  - cref 的四个坑（带类型限定的 cref 不找继承成员；参数列表要写全含默认值；有重载必须写全形；
    参数类型不可见时写全名）见 `Tools/check-docs.ps1` 头部，那里写得最全
- **可见性：** 默认 `internal`；测试经 `AssemblyInfo.cs` 的 `InternalsVisibleTo` 访问。
  **例外：** Unity 通过类型反射实例化的类（自定义编辑器、ScriptableObject）必须 `public`，
  否则构造不出来——`XInspectorAutoEditor` 的两个子类即属此类

## 性能与 GC 约定

- **每帧路径禁 LINQ**、禁闭包分配、禁字符串拼接。构建期（一次性）不受此限，可读性优先
- **反射仅限构建期**：成员遍历、特性收集、绘制器扫描都在建树时发生一次，
  不进绘制路径。`DrawerTypeRegistry` 用 `TypeCache` 并在静态字段里缓存
- **复用 `GUIContent`**：默认标签缓存在节点上，不要每帧新建
- **绘制器共享实例不得持有可变字段**（同「架构要点」第 2 条，这是硬约束不是建议）

## 测试与门禁

**三条门禁**：`Tools/run-tests.ps1`（全量双平台）、
`dotnet test Tests.Native/Tests.Native.csproj`（离线，约 20 毫秒，只覆盖 Runtime 逻辑）、
`Tools/check-docs.ps1 -Enforce`（文档 + .meta + 源文件全参与编译）。

**具体命令、参数手感与踩过的坑见 [Documentation/Workflow.md](Documentation/Workflow.md) §一**
——那里是流程的唯一真相，本节只写规则。

- **门禁是「全量 0 失败」，不写固定例数**——例数随开发增长，写进规则必然定期过期
- **每个 fixture 必须复位它触碰的静态门面**（本仓主要是 `DrawerTypeRegistry.Reset()`）。
  PlayMode 下所有用例共享一个 player 实例，不复位即互相污染
- **不测 IMGUI 绘制**——伪造 GUI 上下文只会得到「测试断言了自己的 mock」。对策是架构性的：
  绘制器只做「画 + 调下一个」，一切决策（可见性、排序、路径解析、分组归属）都放在可无头测试的
  代码里。测试因此断言**链上有没有它、在第几位**，而不是文字长什么样
- **测试程序集与 `defineConstraints`：** 测试程序集用 `UNITY_INCLUDE_TESTS` 门控
  （不同于 XFramework 的旧式 `optionalUnityReferences`）——只有它才能保证测试不被编进玩家构建

## 文档门禁要守的三件事

`Tools/check-docs.ps1 -Enforce` 三项都须满足：包内 XML 文档告警为 0、所有源文件都已参与编译、
所有资源都有 `.meta`。它是独立的一条通道：csproj 未设 `DocumentationFile`，
**默认编译根本不检查文档注释**，不开这一枪则写坏文档不会有任何反馈。

**前置条件：需要 `.csproj`，而它只在 GUI 编辑器里生成**——纯批处理环境下这条门禁跑不起来，
脚本会判失败。那不是脚本坏了，是它拒绝给假绿。细节见
[Documentation/Workflow.md](Documentation/Workflow.md) §一。

两处本包特有的处理，改动时别删：

- `Editor/AutoEditor/` 在「是否参与编译」检查中被排除——它是宏门控的，宏关掉时那里的源文件
  本来就不该被编译，不排除会长期误报
- 「缺 `.meta`」检查跳过**点开头**的条目（Unity 不给它们生成 meta）。包在 `Packages/` 时
  还有一条处理 `~` 结尾目录的规则，搬到 `Assets/` 后波浪号已去掉，那条成了永不触发的
  死代码，已删——若日后又出现 `~` 目录，记得加回来

## 如何新增一个特性

1. Runtime 侧加特性类（`Runtime/Attributes/`），声明正确的 `AttributeUsage`
2. 若要参与绘制，写 `AttributeDrawer<TAttribute>`（`Editor/Drawers/BuiltIn/`），
   按需加 `[DrawerPriority]`。**只画东西、把决策留给别处**，且不得有可变字段
3. 若它影响**别的**属性（可见性、分组归属、标签），那需要一个能改写属性特性列表或
   `PropertyState` 的阶段——目前尚未建该层，加它时是纯新增
4. 补无头测试（链装配顺序、分组归属、路径解析这类）
5. 在 `Assets/Sandbox/` 里加一行演示
6. 更新 `CHANGELOG.md` 与包 README 的「已知限制」（若边界有变）

**分组特性**另需：继承 `PropertyGroupAttribute`（`Runtime/Attributes/Groups/`）并覆写 `Combine`，
再加一个 `AttributeDrawer<T>` 画框。**构建期不需要改**——分组装配对具体分组类型一无所知。

## 已知的坑（都踩过，别重踩）

- **`SerializedObject.GetIterator()` 返回的是同一个实例**，`NextVisible` 只是就地改写它。
  把它存进节点会让所有节点共享一个对象、走完遍历后全部指向最后一个属性——症状是
  「Inspector 里每个字段显示的都是同一个值」。只能用它读名字与路径，
  值入口要用 `FindProperty` 取**独立**实例。`PropertyTreeBuilderTests` 有回归守卫
- **命名空间以 `.Editor` 结尾时，`Editor` 这个名字会被解析成命名空间自己**，
  而不是 `UnityEditor.Editor`：C# 先查外层命名空间成员、后查 using 指令。
  故基类必须写全名，使用方把编辑器类放进 `Xxx.Editor` 命名空间时同样要写全名
- **Unity 会跳过没有任何脚本的 asmdef**（日志原话 "will not be compiled, because it has no
  scripts associated with it"），故测试程序集里必须有真实文件
- **`Assembly.GetReferencedAssemblies()` 返回的是编译器实际发出的引用**，未用到的会被裁掉。
  它只适合断言「不存在某依赖」（用了就必然发出），不适合断言「存在某依赖」（会误报）
- **`EditorWindow` 自带 7 个 `[SerializeField]` 内部字段**（`m_MinSize`、`m_MaxSize`、
  `m_TitleContent`、`m_Pos`、`m_SerializedDataModeController`、`m_ViewDataDictionary`、
  `m_OverlayCanvas`），`new SerializedObject(window)` 会把它们全翻出来，后三个还会各自
  展开成一整棵子树。窗口路径必须传成员过滤器（`WindowMemberFilter`：只收声明在
  `XInspectorEditorWindow` 及其派生类型上的字段）。**Inspector 路径不能加这个过滤**——
  那会连 MonoBehaviour 的 `m_Script` 一起跳掉，破坏与原生 Inspector 的一致性
- **同一个文件里同时 `using System` 与 `using UnityEngine` 时，裸写 `Object` 是 CS0104 二义**
  （`System.Object` vs `UnityEngine.Object`）。用 `using Object = UnityEngine.Object;` 消歧

## 文档在哪

**一处只写一份真相**，按收件人分层：

| 文档 | 收件人 | 写什么 |
|---|---|---|
| `Assets/XInspector/README.md` | 引入包的第三方 | 包级入口：设计哲学、快速开始、已知限制 |
| `Assets/XInspector/Runtime\|Editor/README.md` | 引入包的第三方 | 行为契约与扩展指南 |
| 本文件 | 维护者（每次会话都读） | 规则与约束：「不得」「一律」「必须」 |
| `Documentation/Workflow.md` | 维护者 | 流程与命令：怎么跑、什么顺序、踩过什么坑 |
| `Documentation/Modules/Pipeline.md` | 维护者 + 下一轮审计者 | 沿革、**已否决形状及理由**、未决项 |
| `Documentation/Roadmap.md` | 维护者（选型时） | 还没做的候选：该不该做、边界画在哪 |
| `Documentation/ModuleAudit.md` | 维护者 | 审计手册：判据清单、报告格式、结论落点 |
| `Assets/XInspector/Documentation/` | 随包分发 | 包内文档（跟包一起被拷走） |

**包内不指向包外：** 包内 README 不引用仓库根 `Documentation/` 下的维护向文档
——第三方装了包却打不开它。

## Git

- 提交信息除代码关键字外一律中文，**不加 `Co-Authored-By` 之类署名尾注**，**不自动推送**
- **计划阶段即定原子提交边界与每个提交的验证命令**；**计划批准即授权本阶段的全部提交**，
  每个提交跑绿后直接提交，不必逐次请示
- 两类例外仍须单独请用户确认：**公开 API 变更**、**破坏性变更**（含仅对仓内的）
- **规则存档：** 讨论中若产生可固化为长期约定的规则，先向用户提示拟写入的文本，
  经确认后方可写进本文件；未确认不擅自修改

## 明确不在本轮范围

自定义序列化后端与 `[ShowInInspector]`（反射成员）、样式/调色板系统、
数组/列表展开、`[ShowIf]` / `[FoldoutGroup]` / `[Button]`、`[SerializeReference]` 类型切换、
折叠状态的跨会话持久化、UI Toolkit、特性处理器层。

**编辑器窗口基类已做**（`Editor/Windows/XInspectorEditorWindow`，绘制窗口自身的序列化字段）。
**仍不做**：多 target 检视、字段拖拽重排、窗口内 Undo、`[ShowInInspector]` 那类非序列化成员。

**其中「特性处理器层」是刻意推迟的：** 它原本的用途是把类级特性合成到别的节点上，
但构建期已把类型特性直接放在根节点，这件事不再需要；剩下的潜在用户（`[ShowIf]` 改状态、
类级 `[BoxGroup]` 分发）都不在范围内，于是它在 v0 里一个调用方都没有。
为一个没有调用方的扩展点引入抽象基类加发现注册表，正是本轮一直在砍的那类臆测性 API。
等 `[ShowIf]` 到来时再加，是纯新增，不改动任何既有签名。
