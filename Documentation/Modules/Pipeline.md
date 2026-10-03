# Pipeline（管线）

维护向记录。收件人是维护者与下一轮审计者——**使用方要看的在
`Assets/XInspector/Runtime|Editor/README.md`**，规则约束在 [CLAUDE.md](../../CLAUDE.md)。

本包只有一个「模块」：那条把属性树与绘制器链串起来的管线。它同时横跨
Runtime（特性）与 Editor（树、链、绘制器）。

---

## 一、沿革

### 2026-10-03 第一轮：骨架与垂直切片

从空仓建起。九个提交，边界如下：

| 提交 | 内容 |
|---|---|
| 1 | Unity 工程与包骨架（当时在 `Packages/com.xinspector/`，Unity 6000.4.5f1） |
| 2 | 四个程序集 + 两条结构性守卫测试 |
| 3 | 特性层：`[Title]`、`PropertyGroupAttribute`、`[BoxGroup]`、路径解析 |
| 4 | 绘制器链：`DrawerChain`、`DrawerTypeRegistry`、`DrawerPriority`、`InspectorProperty` |
| 5 | 值后端、属性树、`XInspectorEditor`、沙盒 |
| 6 | 分组装配与 `BoxGroupDrawer` |
| 7 | `TitleAttributeDrawer` |
| 8 | 宏门控的自动编辑器程序集 |
| 9 | 示例、工具、工程规则 |

**未做**：特性处理器层、数组展开、`[ShowIf]` 及同类、样式系统、编辑器窗口、
UI Toolkit、序列化后端。判断与边界见 [Roadmap.md](../Roadmap.md)。

**第二轮**：结构对齐 XFramework——程序集命名改为「公司.产品[.Editor]」式、
补 `Tests.Native` 离线测试通道、补模块 README 与维护向文档。

**第三轮（当前）：包从 `Packages/com.xinspector/` 搬到 `Assets/XInspector/`。**
放弃 UPM 的可安装性，换回与 XFramework 一致的布局。理由与代价见下面第 11 条。

第三轮的连带改动，都是**不去 grep 就会漏掉**的那类：

- 包内 `Samples~/` → `Samples/`、`Documentation~/` → `Documentation/`。`~` 的语义是
  「Unity 别扫我」，离开 UPM 后它只剩坏处——示例代码不参与编译、用户也看不到。
- `Tools/check-docs.ps1` 四处与 `Tests.Native.csproj` 三处硬编码路径。
  **其中一处最危险**：脚本里那个「从编译告警中筛出本包」的正则。不改的话它永远匹配不到，
  门禁会报「0 条告警」——**假绿**，比报错更坏。
- `package.json` 去掉 `samples` 数组：它描述的是 UPM 布局，搬出来后那个路径已不成立。

---

## 二、已否决的形状

「考虑过、但没这么做」的记录。**每条都写清楚为什么**——否则下一轮会有人重新论证一遍，
或者更糟：照着看起来更简单的那个改回去。

### 1. 纯反射的值后端（否决，改用 `SerializedObject`）

纯反射（`FieldInfo.GetValue/SetValue`）看起来更直接，也不受「只能画 Unity 会序列化的
成员」这条限制。但 `SerializedObject` 白送五件事：Undo/Redo、预制体覆盖、场景标脏、
多对象编辑、域重载后取值。纯反射路线要自己重实现一遍序列化，且这五件一件也拿不回来。

代价（已写进包 README 的已知限制）：只能画 public 字段与 `[SerializeField]` 私有字段。
要画普通属性需要**另一套后端**，不是改现有后端——`PropertyValueEntry` 就是为这条缝存在的。

### 2. 末端绘制器进注册表（否决，改为构建期显式追加）

末端是结构性的（根/分组接 `ChildrenDrawer`、成员接 `UnityFallbackDrawer`），
不是启发式的。放进注册表意味着一个写错的 `CanDraw` 就能让某属性链为空——
症状是「它静默地什么都不画」，属于最难归因的一类问题。显式追加让链条永不为空，
并在链尾多了一个可断言的守卫（`CallNext` 越界即抛）。

### 3. 有状态的绘制器（否决，改为无状态共享单例）

每属性一个绘制器实例更符合直觉，也不必把状态挪来挪去。但代价是内存：
500 字段的 Inspector × 40 种绘制器 = 两万个对象。

改成共享单例后，纪律是「绘制器不得有可变字段，每属性状态进 `PropertyState`」。
这条不是风格偏好而是架构前提——违反它的症状是「展开一个、全都展开了」，
很难联想到原因，所以 CLAUDE.md 把它列为硬约束。

### 4. 特性处理器层（推迟，不是否决）

原以为「类级 `[Title]`」需要它把特性合成到根节点上。但构建期已经把**类型上的特性
直接放在根节点**，这条路不需要处理器——类级标题就是同一个绘制器出现在了根节点上。

于是处理器在 v0 里**一个调用方都没有**。为一个没有调用方的扩展点引入抽象基类加
发现注册表，正是这一轮一直在砍的那类臆测性 API。**触发条件：开始做 `[ShowIf]`
或类级分组分发时一并加**，届时是纯新增。

### 5. 泛型 `PropertyValueEntry<T>`（推迟）

v0 没有任何地方需要强类型取值——绘制器直接把 `SerializedProperty` 交给
`PropertyField`，那条路连装箱都不经过。引入泛型要配 `MakeGenericType` 那套机械
（因为绘制器类型在运行时才知道），而收益还不存在。非泛型基类已经把「后端可替换」
这条缝表达清楚了。

### 6. `PropertyTree.Update()` 与 `IDisposable`（推迟）

v0 的树结构在编辑器存活期间不变（没有数组、没有 `[SerializeReference]` 切换），
因此没有「需要更新」或「需要释放」的东西。等数组展开到来时再加——那是纯新增。

### 7. `ExpandableCompositeDrawer`（推迟）

原计划里有一个「自己展开嵌套类型」的末端绘制器。实际做的时候发现
`EditorGUILayout.PropertyField(..., includeChildren: true)` 已经把嵌套类型画得
和 Unity 原生一模一样——而这正是第 5 步验收标准（「渲染结果与原生一致」）所需要的。
自己的可展开绘制器只有在「要让特性作用于嵌套类型内部」时才有意义，那时再加。

### 8. 测试程序集用旧式 `optionalUnityReferences`（否决）

XFramework 用的是 `optionalUnityReferences: ["TestAssemblies"]`。本包改用
`overrideReferences` + `precompiledReferences` + `defineConstraints: ["UNITY_INCLUDE_TESTS"]`。
差别不止风格：**只有 `UNITY_INCLUDE_TESTS` 能保证测试程序集不被编进玩家构建**，
而本包是要分发给第三方的，这一条不能含糊。

### 9. 示例带场景（否决）

示例只给组件 + 编辑器 + README，**不含 `.unity`**。场景里对脚本的引用是 GUID，
拷贝分发时最容易断链——用户拿到示例看到一堆 Missing 是最糟糕的第一印象。
组件加文档在任何项目里都能用。（当时目录名是 `Samples~/Overview/`，第三轮去掉了波浪号。）

### 10. asmdef 名带 `.Runtime` 后缀（否决，第二轮改回）

第一轮用 `XInspector.Runtime` / `XInspector.Editor`。优点是引用列表里一眼看出
哪个是运行时程序集。但 XFramework 用「公司.产品[.Editor]」式，跨仓不一致的代价
大于这点便利，第二轮统一为 `Venusir.Xinspector` / `Venusir.Xinspector.Editor`。

改的时候是免费的（包未发布）；**发布后对自带 asmdef 的使用方就是破坏性变更**。

### 11. 包放 `Assets/` 而不是 `Packages/`（第三轮改为此，代价明确）

第一轮把包放在 `Packages/com.xinspector/`——`Assets/` 里带 `package.json` 的目录在 Package
Manager 眼里只是普通资源（实测：XFramework 的 manifest 与 lock 里匹配其包名 0 处）。
第三轮搬到 `Assets/XInspector/`。

**真实的代价只有三条：**

| 实际影响 | 后果 |
|---|---|
| **开发工程里本包不再是内嵌包** | 它不再出现在**本工程**的 Package Manager 里。与使用方无关 |
| **示例目录没有波浪号** | **唯一影响使用方的损失**：`Samples/` 缺 `~`，经 git 分发时示例会被一并导入，并在对方工程里编译 |
| `package.json` 的 `dependencies` 被解析 | 丢了，但本包零依赖，无实际影响 |

> **更正（2026-10-03）。** 本条最初把代价写成「Package Manager 不认识本包、**不能经 git URL
> 安装**、无法发布到 registry」，并列表格逐项声称丢失。**那几行是错的**，起因是把
> 「使用方工程里的内嵌包」与「git 依赖的 `?path=`」混为一谈：
> `?path=` 的参数只是**相对仓库根的路径**，官方要求仅两条——路径相对仓库根、该子目录含
> `package.json`（[Git URLs](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-git.html)）。
> `Packages/` 那条约束管的是**使用方的工程布局**，与包在源仓库里放哪无关。
>
> 反例就在隔壁：XFramework 的 manifest 用
> `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` 装 UniTask，
> 而那个路径连顶层 `Assets/` 都不在，在 `src/` 下——它工作正常。所以
> `?path=/Assets/XInspector` 可用，git URL 安装**没有丢**；使用方在 Package Manager 里
> 同样看得到版本（git 依赖也会显示）。registry / OpenUPM 发布也不是被这个搬家挡住的，
> 只是本包没做。

**换来的**：代码在 `Assets/` 下与 `Assets/Sandbox/` 平级，Project 窗口一眼可见；
与 XFramework 的布局心智一致。

**另一项不显眼的代价：边界没了。** 原先靠「在不在 `Packages/`」就能区分包本体与工程壳，
现在两者都在 `Assets/` 下，只能靠目录名与约定守。CLAUDE.md 里为此专门写了一节。

**可逆**：移动时连 `.meta` 一起挪则 GUID 不变，asmdef 名与 C# 命名空间也不受影响，
搬回 `Packages/` 是纯路径操作。`package.json` 保留标识字段正是为此——
搬回去时还需恢复 `Samples~/` 的波浪号并补回 `samples` 数组。

---

## 三、未决项

还没想清楚、但迟早要面对的：

| 问题 | 卡在哪 |
|---|---|
| 类级 `[BoxGroup]` 分发到成员 | 需要处理器层。且要想清楚：类级分组与成员自己声明的分组**冲突**时谁赢？ |
| 多对象编辑下 `[ShowIf]` 的语义 | 各目标条件值不同时「显示还是不显示」没有显然答案。倾向「任一满足即显示」，与自动编辑器的取舍一致 |
| 反射后端如何标脏与撤销 | `[ShowInInspector]` 那类走反射写入，拿不到 `SerializedObject` 的 Undo/标脏。倾向「明确只读或明确提示不可撤销」，不假装能撤销 |
| 折叠状态的持久化落点 | `EditorPrefs`（跨项目共享）、`SessionState`（不跨会话）、序列化进场景（污染资产）三者各有问题。等 `[FoldoutGroup]` 来了再定 |
| `[OnValueChanged]` 的触发时机 | 判断「值变了」要每帧比对旧值，旧值该放 `PropertyState`；但触发时机（绘制前后？`Update` 前后？）未定 |
| 数组展开的边界 | 全自己做就要自己处理增删/拖拽/多选/Undo。倾向先只做「只读展示 + 元素级特性」 |

---

## 四、审计记忆

**2026-10-03（第二轮）**：结构对齐期间顺带核对了几件事，结论如下——

- **XFramework 的 `Tests.Native/` 是失效的。** 目录里只剩 csproj，引用的
  `UnityEngineStubs/`、`Tests/` 都不存在，glob 的 `Runtime\Node\`、`Runtime\Loader\`
  两个模块也早已不在。glob 匹配不到不报错，于是它静默地零测试。本包的同类工程
  是新写的，不是移植。
- **XInspector 的 Runtime 侧完全不依赖 Unity**（实测：全部源文件只用 `System`、
  `System.Text`、`XInspector.Internal`）。这条以前只是 CLAUDE.md 里的一句声明，
  现在由 `Tests.Native` 编译期强制。
- **`.gitignore` 的反选规则必须写在通配规则之后**（最后匹配者胜出）。写反了不报错、
  只是不生效，很难发现。

后续审计按 [ModuleAudit.md](../ModuleAudit.md) 的清单走，结论回流到本文件。
