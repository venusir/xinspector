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

**未做**：特性处理器层、数组展开、`[ShowIf]` 及同类、样式系统、UI Toolkit、序列化后端。
判断与边界见 [Roadmap.md](../Roadmap.md)。

（**编辑器窗口**原在此列，第四轮做了基类形态——见下面第三轮的记录与「已否决的形状」第 12 条。）

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

> **2026-10-04 更新：`IDisposable` 提前到了**，但触发它的不是数组展开，而是内嵌编辑器：
> 它的每属性状态里放着必须显式销毁的嵌套 `Editor` 实例，而包内当时**一条释放路径都没有**。
> 释放链四环（状态的 `Dispose` → `PropertyState.Reset` → `PropertyTree.Dispose` → 宿主
> `OnDisable` / `PropertyTreeHost.Clear`）与三条契约写在
> [Editor/README.md](../../Assets/XInspector/Editor/README.md) 的「释放：谁建谁销」。
> `Update()` 仍然推迟——树结构还是不变的，没有东西需要重建。

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

### 12. 窗口只做「画自身字段」，且窗口内编辑不进 Undo（第四轮）

第四轮做了 `XInspectorEditorWindow`——`PropertyTree` 第一次用在 Inspector 之外。
两处刻意的收窄：

**（a）只做「画窗口自身的序列化字段」，不做「检视任意对象的浮空 Inspector」。**

后者（带 target 槽位与对象选择器）是 Odin 的另一半用法。不做的理由是它要引入一整套
Inspector 才有的语义：多对象、预制体编辑、对象切换时的树重建、target 为 null 时的空态。
那是**另一个产品形态**，与「让使用方用本管线布置自己的窗口」不是一件事。
先把边界清楚的那一半做实，比两边都做一半好。

**（b）窗口内的编辑不进 Undo，用「重置」补偿。**

窗口字段既不属于场景也不属于资产，Unity 的 Undo 体系里没有它的位置。强行登记
（`ApplyModifiedProperties`）会往**全局** Undo 栈写记录，于是用户按 Ctrl+Z 想撤销场景操作、
撤销到的却是窗口里的一个数字。**在 Inspector 里注册 Undo 是特性，在窗口里是污染。**

这条偏离是 `PropertyTreeHost` 与 `XInspectorEditor.OnInspectorGUI` 唯一的实质差别，
代价是窗口内编辑不可撤销，由 `ResetToDefaults()` 补偿——它的做法是取一个同类型的
一次性实例（字段还停在 C# 初始值）按路径把值复制回来。

**（c）成员过滤必须是窗口专用的，不能变成全局默认。**

`EditorWindow` 自带 7 个带 `[SerializeField]` 的内部字段，不过滤就会混进窗口的树里。
但这条规则**不能**下推到 `PropertyTree.Create(SerializedObject)` 的默认路径：那会连
MonoBehaviour 的 `m_Script` 一起跳掉，而 Inspector 路径**刻意留着**它来对齐原生渲染。
所以过滤是 `internal` 重载上的一个可选参数，默认行为一字未改（有回归守卫钉住）。

### 13. 条件只认序列化成员，且不猜 Odin 的枚举参数签名（第五轮）

第五轮做了特性处理器层与条件族。三处刻意的收窄：

**（a）条件只能指向序列化成员，不做反射。**

`[ShowIf("x")]` 的 `x` 必须是 public 字段或 `[SerializeField]` 私有字段。普通属性、
方法、静态成员都读不到——那需要 L3 的那套反射值后端，而它拿不到 `SerializedObject`
白送的五件事（Undo、预制体覆盖、场景标脏、多对象编辑、域重载后取值）。

先做能力范围内那一半，比两边都做一半好。跨对象条件（Odin 的 `"@other.field"`）同理。

**（b）不猜 API 形状。**

`[ShowIn]` `[HideIn]` `[EnableIn]` `[DisableIn]` 接的是 `PrefabKind` 之类的枚举参数，
`[ShowIfGroup]` / `[HideIfGroup]` 同理——**具体签名没从 Odin 官网核对到，就不写**。

写一个猜的形状下去比不做更糟：它会被当成已有能力，而使用方照着写会编译不过或行为不符预期。
这条与「不为没有调用方的扩展点建抽象」是同一种纪律的另一面——
**不为不确定的东西造表面。**

**（c）求值与解析分离。**

解析（找成员、校类型、失败告警）在构建期做一次；求值（读一个 bool）在绘制期每帧做。
这个分工是「不碰 GUI 就能测」的前提：如果解析也放在求值里，每帧的错误路径就没法单测了；
如果求值也放在构建期，条件就不会跟随变化。

**一条未决**：多对象编辑下各目标条件值不同时，`condition.boolValue` 取的是第一个目标的
值。Odin 倾向「任一满足即显示」。本包尚未定这个语义——现状是跟第一个目标。

### 14. 复刻含 `UnityEngine` 类型的 Odin 签名（否决）

`[DisplayAsString]` 的 8 个 `TextAlignment` 重载、`[GUIColor].Color`、`[ProgressBar].ValueLabelAlignment`
都带 `UnityEngine` 类型。**Runtime 零 Unity 依赖是编译期强制的**（`Tests.Native` 用纯 .NET
编译整份 Runtime），这些形态只有两条路：不做，或自建替代枚举。

自建之后**签名就不再与 Odin 兼容**——从 Odin 迁过来的人照样编译不过，只是把「编译不过」
换成了「编译得过但类型不是那个」。等于白做一层，还把「我们与 Odin 同名同形」这句承诺弄脏。
故：不做，写进 README 的「已知限制」。

（注意与「推迟」的区别：`$` 表达式、`SdfIconType` 是**推迟**——它们迟早能做，
只是要连着整个表达式语言/图标系统一起做；这一条是**否决**。）

### 15. `Editor.CreateCachedEditor`（否决）

`CreateCachedEditor` 的语义是**共享**：官方原文说它「要么直接返回已在跟踪这些对象的编辑器，
要么销毁上一个再建一个」。每个属性各自的编辑器不该共享——两个字段指向同一对象时，
两个编辑器会共用一个 `serializedObject`，一处的写回会经另一处再 apply 一次；
而「谁持有、谁销毁」在官方文档里没有答案，引入这种模糊正是本仓一贯要避免的。

`CreateEditor` 的所有权是**单边**的（官方原文「Editors created using this function have to be
destroyed explicitly」），与「状态归 `PropertyState`、清空者负责销毁」的模型同构：
目标切换时自己 `DestroyImmediate` 旧实例再建新的，行为完全确定。

### 16. 每帧创建/销毁内嵌 `Editor`（否决）

代价是原生对象 churn，加上被嵌编辑器的 `OnEnable` 洪水——不少编辑器在 `OnEnable` 里建树，
嵌套的 `XInspectorEditor` 就是其中之一，等于每帧重建一棵属性树。
缓存进 `PropertyState` 才是对的：目标没换就不重建，换了先销毁再建。

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
| `$` 表达式与 getter 字符串 | `GUIColor(string)`、`MinValue`/`MaxValue(string)`、`ProgressBar` 的三个 getter 形都属此类。**做半个（只认单个成员名）比不做更糟**，要做就连同 Odin 的整套表达式语言一起做。签名已在「L1a 签名核对」一节抄好 |
| `SdfIconType` 与图标重载 | ~1536 个成员的 Sirenix 自有枚举，是独立大件：要么生成全部并自绘图标，要么裁一个子集并接受与 Odin 不兼容。`[LabelText]` `[InfoBox]` `[SuffixLabel]` 的图标重载都卡在这 |
| `[DisplayAsString]` 的 `fontSize` / `enableRichText` 重载 | 签名已确认且不依赖 Unity 类型，本轮只做了 `()` 与 `(bool overflow)`。补它是纯加法，缺的只是一个使用它的理由 |
| `[ValueDropdown]` 的选项缓存 | 现在**每帧重建选项表**（选项随来源的值变化才算对）。来源很大时这是每帧 O(n) 的分配——要缓存就得定「什么时候失效」，而那正是「显示陈旧选项」的来源。先正确、后优化 |
| 带标签的选项（`ValueDropdownItem<T>`） | 见「落地时的三处收窄」第 2 条。缺的是「这些类型在 Unity 下到底能不能序列化」的核实，不是设计 |
| `[PreviewField]` 的拖拽交互 | Odin 的 Ctrl+点击清空、Ctrl+拖拽替换要自绘对象字段的拖拽与点击处理。落点若要做，是 `ProgressBarDrawer` 那套手工事件处理的路子 |
| `[AssetSelector]` 弹出层的搜索框 | 现在是编辑器自带菜单，没有搜索框/图标/多选。要做得自建弹出窗口——那是 `[InlineEditor]` 那一档的工作量 |
| `[ColorPalette]` 的调色板来源 | 卡在设计而非实现：得先定「命名调色板存在哪、谁来编辑、怎么进版本控制」。做完这层，特性本身只有几十行 |
| 内嵌编辑器的 Undo 策略 | 内嵌内容经 `ApplyModifiedProperties` 写回，**会进 Undo**；窗口路径那条「窗口内编辑不进 Undo」的约定在此不适用。根因是包内没有绘制上下文对象，绘制器无从知道自己被谁画；要区分就得给全部绘制器签名加一个上下文——波及面太大，已否决。现阶段接受并写进 README |
| 域重载下 `OnDisable` 未调时的兜底 | 正常路径是宿主 `OnDisable` 释放嵌套编辑器（官方文档称域重载会调到）；万一某条路没调到，原生对象会泄漏一次。缓解是 `HideFlags.DontSave`（不产生悬空引用），**不做**后备注册表——那会新增一个静态门面与测试复位负担 |
| 嵌套深度上限的数值 | 现取 `4`，**本包自定**（Odin 的值未核实）。它与预览默认尺寸、默认预览位置同属「本包自定值」，三处都写进了 README 与展示台 |

---

## 四、L1a 签名核对（2026-10-03）

补 L1a 之前把 21 个特性的 **Odin 官方签名**逐条抄了下来。本节两个用途：实现时照它写、
不再凭记忆；以及**明确记下我们有意不实现哪些形态**——不实现也是结论，不写下来就会被下一个人重新猜一遍。

### 怎么核的（下次照做）

- 官网 API 文档页是**服务端渲染**的，直接取即可（给出 `[AttributeUsage]`、类声明、
  全部构造重载、字段与属性）：

  ```powershell
  Invoke-WebRequest https://odininspector.com/documentation/sirenix.odininspector.<小写类名>
  ```

  **`WebFetch` 会被截断成只剩导航**——本次踩过，别再用它抓这个站；
  Bash 工具在本机没有网络出口，用 PowerShell。
- 官网「单特性页」`https://odininspector.com/attributes/<kebab>-attribute` 只有用法示例，
  用来交叉验证具名参数。注意 URL 名不总是类名小写：`[ProgressBar]` 是 `progress-bar-attribute`。
- **文档站只有 3.3.1.2 一个版本**，本表据此抄录。Odin 4.x 是否改过签名**未确认**；
  将来要对齐 4.x 得另找来源重核。

### 三条共性（决定了我们的 `AttributeUsage` 写成什么样）

1. 21 个全在 `Sirenix.OdinInspector` 命名空间，且全带 `[Conditional("UNITY_EDITOR")]`。
2. **20/21 声明 `AttributeTargets.All`**（`[EnumToggleButtons]` 干脆没写 `[AttributeUsage]`）。
   Odin 一律放开，语义只写在文档里。**我们刻意收窄**：默认 `Field | Property`，
   `[InfoBox]` `[DetailedInfoBox]` `[GUIColor]` `[Indent]` 另收 `Class`。
   放宽到方法、枚举、参数上只会得到「编译得过但什么都不发生」——那正是本包最想避免的一类现象，
   收窄的代价（Odin 能编译的写法我们编译不过）是**响的**，比静默强。
3. **除 `[DisplayAsString]` 外，构造签名全是纯 BCL + Sirenix 自有类型**（`string`/`bool`/`int`/`float`/`double`
   + `InfoMessageType` + `SdfIconType`）。这条对我们格外关键：Runtime 零 Unity 依赖是**编译期强制**的。

### 逐条：官方签名 → 本轮实现

| 特性 | 官方构造重载 | 本轮实现 | 不实现的部分与理由 |
|---|---|---|---|
| `[ReadOnly]` | `()` | 全部 | — |
| `[GUIColor]` | `(float r, float g, float b, float a = 1F)`、`(string getColor)` | 前者 | 字符串形属 `$` 表达式族。具名字段 `Color`（`UnityEngine.Color`）永久不做 |
| `[LabelText]` | `(string)`、`(string, bool nicifyText)`、`(string, bool, SdfIconType)`、`(string, SdfIconType)`、`(SdfIconType)` | 前两个 | 三个图标形需要 `SdfIconType` |
| `[LabelWidth]` | `(float width)` | 全部 | — |
| `[HideLabel]` | `()` | 全部 | — |
| `[PropertySpace]` | `()`（默认 8 像素）、`(float before)`、`(float before, float after)` | 全部 | 另提供同名字段 `SpaceBefore`/`SpaceAfter` |
| `[Indent]` | `(int indentLevel = 1)` | 全部 | 官方 `AllowMultiple = true`：多个 `[Indent]` 各加一层——链按特性实例配对，天然如此 |
| `[PropertyTooltip]` | `(string tooltip)` | 全部 | — |
| `[InfoBox]` | `(string, InfoMessageType = Info, string visibleIfMemberName = null)`、`(string, SdfIconType, string = null)`、`(string, string visibleIfMemberName)` | 第一、三个 | 图标形不做；`visibleIf` 走既有 `ConditionResolver`（只认序列化 bool 成员） |
| `[DetailedInfoBox]` | `(string message, string details, InfoMessageType = Info, string visibleIf = null)` | 全部 | — |
| `[DisplayAsString]` | 14 个重载（`bool overflow`/`int fontSize`/`bool enableRichText`/`TextAlignment` 的排列组合） | `()` 与 `(bool overflow)` | 8 个带 `TextAlignment` 的**永久做不了**；`fontSize`/`enableRichText` 的重载推迟 |
| `[SuffixLabel]` | `(string label, bool overlay = false)`、`(SdfIconType)`、`(string, SdfIconType, bool overlay = false)` | 第一个 | 两个图标形不做 |
| `[ToggleLeft]` | `()` | 全部 | — |
| `[ProgressBar]` | `(double min, double max, float r = 0.15F, float g = 0.47F, float b = 0.74F)`、`(double, string maxGetter, …)`、`(string minGetter, double, …)`、`(string, string, …)` | 第一个 | 三个 getter 形属 `$` 表达式族。具名 `Color`/`ValueLabelAlignment` 带 Unity 类型，永久不做；`Height`/`Segmented`/`DrawValueLabel` 提供 |
| `[EnumToggleButtons]` | `()` | 全部 | — |
| `[Required]` | `()`、`(InfoMessageType)`、`(string errorMessage)`、`(string, InfoMessageType)` | 全部 | — |
| `[MinValue]` | `(double minValue)`、`(string expression)` | 第一个 | 表达式形不做 |
| `[MaxValue]` | `(double maxValue)`、`(string expression)` | 第一个 | 同上 |
| `[AssetsOnly]` | `()` | 全部 | — |
| `[SceneObjectsOnly]` | `()` | 全部 | — |
| `[ShowDrawerChain]` | `()` | 全部 | — |

### 两个 Sirenix 自有类型

- **`InfoMessageType`**（`None`/`Info`/`Warning`/`Error`）我们自建。官方原文说它刻意对应
  `UnityEditor.MessageType`，只因后者在 UnityEditor 程序集里才另造了一个——这对我们尤其相关，
  Runtime 同样拿不到 `MessageType`。**声明顺序与数值未从官网确认**（文档站按字母序排），
  本包按 `None = 0`、`Info`、`Warning`、`Error` 排。
- **`SdfIconType`**（约 1536 个成员，Bootstrap Icons 全集）**本轮不做**——它是一个独立大件。
  凡签名里出现它的重载一律不实现。

### 三条语义（官方只确认了一部分）

| 问题 | 官方怎么说 | 我们的决定 |
|---|---|---|
| `[MinValue]`/`[MaxValue]` 是钳制还是只提示 | **官方确认是钳制**：指南页原文「caps value of the field to a minimum value」，API 页并注明「脚本改的值不会被钳」 | 照做。但**钳制时机**（拖动中/提交时）官方未说明，我们取「每次绘制后」并把这条写进 README |
| `[Required]` 的「空」怎么判 | **未确认**。只知校验器是 `RequiredValidator<T> where T : class`（即只覆盖引用类型） | `null`、空串、空集合算「空」，**空白串按非空**。这是我们定的，不冒充 Odin 契约 |
| `[EnumToggleButtons]` 对 `[Flags]` | 官方只确认「两种枚举都支持」；「多选」是第三方说法 | 普通枚举单选工具栏，`[Flags]` 逐位多选 |

### 本轮不实现的形态（都记在此，别处不再重复）

- **`$` 表达式 / getter 字符串**（`GUIColor(string)`、`MinValue`/`MaxValue(string)`、`ProgressBar` 的三个 getter 形）
  ——推迟，见「未决项」。
- **`SdfIconType` 相关重载**——推迟，见「未决项」。
- **含 `UnityEngine` 类型的形态**——**永久否决**，见「已否决的形状」第 14 条。

---

## 五、第二批签名核对（2026-10-04）

补 L1a 第二批与分组族之前，照上一节的办法把 **26 个特性**的 Odin 官方签名逐条抄了下来
（20 个 L1a + 6 个分组），另取 4 个支撑枚举页（`PrefabKind`、`TitleAlignments`、`TabLayouting`、
`Units`）。30 个页面全部命中，取法与注意事项同上一节（**别用 `WebFetch`**）。

### 跨特性共性

1. 26 个全在 `Sirenix.OdinInspector`；除 `UnitAttribute` 外都带 `[Conditional("UNITY_EDITOR")]`。
2. **3 例页面没有 `[AttributeUsage]`**（`ChildGameObjectsOnly`、`Unit`、`ShowPropertyResolver`），
   与 `[EnumToggleButtons]` 同款。我们照旧**刻意收窄**（类级作用的除外）。
3. `AllowMultiple = true`：`TypeInfoBox`、`ValidateInput`，以及**全部 6 个分组特性**。
4. **「含 `UnityEngine` 类型」零命中**——本批没有一个构造签名引用 Unity 类型。
   Runtime 零 Unity 依赖这条契约不用为它们破例。
5. `SdfIconType` 只出现一处（`TabGroupAttribute` 的图标重载与 `Icon` 字段），其余 25 个清白。
6. `$`/resolved string 出现 4 处：`CustomValueDrawer.Action`、`ValidateInput.Condition`、
   `PropertyRange` 的三个 getter 重载、`TabGroup.TextColor`。
7. 需要**调用目标对象的方法**：`ValidateInput`（condition 可为方法，另有 `$value` 具名参数）、
   `CustomValueDrawer`（action 是方法/表达式）。两者都没有纯 BCL 子集。

### 逐条：官方签名 → 本轮决定

| 特性 | 可实现子集（官方原文） | 不实现的部分与理由 |
|---|---|---|
| `[ChildGameObjectsOnly]` | `()`；`IncludeInactive`、`IncludeSelf` | — |
| `[EnumPaging]` | `()` | — |
| `[HideMonoScript]` | `()`；仅 `Class` 目标 | 需**构建期成员抑制**（小块新机制，见下） |
| `[MultiLineProperty]` | `(int lines = 3)`；`Lines` | 注意默认是 3 行，不是无参构造 |
| `[Toggle]` | `(string toggleMemberName)` | 官方示例确认被指的 bool 在**值对象内部**（相对路径，如 `t.Enabled`）；不支持 static；`CollapseOthersOnExpand` **只留字段、不做行为** |
| `[DelayedProperty]` | `()` | 支持类型集由我们定（Delayed\* 控件覆盖的序列化类型），未支持类型**告警并调下一个** |
| `[EnableGUI]` | `()` | — |
| `[TypeInfoBox]` | `(string message)`；`Message` | — |
| `[PropertyRange]` | `(double min, double max)`；`Min`、`Max` | 三个 getter 形属 `$` 族；`MinGetter`/`MaxGetter` 字段照 Odin 保留（未用时为 null） |
| `[Wrap]` | `(double min, double max)`；`Min`、`Max` | 官方注明不支持无符号原始类型 |
| `[DrawWithUnity]` | `()`；`PreferImGUI` | `PreferImGUI` 是 UI Toolkit 时代的开关，IMGUI-only 下只保留字段 |
| `[VerticalGroup]` | `(float order = 0)`、`(string groupId, float order = 0)`；`PaddingTop`、`PaddingBottom` | 默认组名常量 `_DefaultVerticalGroup` |
| `[TitleGroup]` | `(string title, string subtitle = null, TitleAlignments alignment = Left, bool horizontalLine = true, bool boldTitle = true, bool indent = false, float order = 0)`；同名属性 | `TitleAlignments` 自建（见下） |
| `[FoldoutGroup]` | `(string groupName, bool expanded, float order = 0)`、`(string groupName, float order = 0)`；`Expanded`、`HasDefinedExpanded` | 展开态**不跨会话持久化**（既定边界，见「未决项」） |
| `[HorizontalGroup]` | `(float width = 0, int marginLeft = 0, int marginRight = 0, float order = 0)`、`(string group, …)`；`Width`、`Gap`、`MarginLeft`/`MarginRight`（**字段是 float、ctor 参数是 int**）、`PaddingLeft`/`PaddingRight`、`MinWidth`、`MaxWidth`、`Title`、`LabelWidth`、`DisableAutomaticLabelWidth` | — |
| `[TabGroup]` | `(string tab, bool useFixedHeight = false, float order = 0)`、`(string group, string tab, bool useFixedHeight = false, float order = 0)`；`TabName`、`HideTabGroupIfTabGroupOnlyHasOneTab` | 图标重载与 `TextColor` 表达式不做；`TabLayouting`、`Paddingless` 与 `Tabs` 列表**不照搬**——Odin 靠 `ISubGroupProviderAttribute` 让每个 tab 派生子分组，**我们用点分路径天然表达**（`Tabs/Tab1`），构建期零新增。`UseFixedHeight` 保留参数、**不产生行为**（固定高度模式是为滚动内容准备的，本包没有那套布局） |
| `[ToggleGroup]` | `(string toggleMemberName, float order = 0, string groupTitle = null)`、`(string toggleMemberName, string groupTitle)`；`ToggleGroupTitle`、`ToggleMemberName`（只读） | `CollapseOthersOnExpand` 只留字段、不做行为（跨组协调无明确语义） |

### 需要自建的类型

- **`TitleAlignments`**：`TitleGroup` 的 `alignment` 参数用。官方 4 个成员（Centered / Left / Right / Split），
  文档站按字母序排，**数值未核实**——我们按此顺序从 0 起排。
- `PrefabKind`（`[Flags]`，10 成员）、`Units`（约 200 成员）、`TabLayouting`——**本轮不做**，见下表。

### 本轮不实现（都记在此，别处不再重复）

| 特性 | 为什么 |
|---|---|
| `[CustomValueDrawer]` | 唯一重载就是 `(string action)`——resolved string（方法/表达式调用）。**属 L5 性质**（调用目标对象） |
| `[ValidateInput]` | `(string condition, string defaultMessage = null, InfoMessageType = Error)`——condition 可为方法，另有 `$value` 具名参数。同样 L5 性质，且需要一个尚不存在的「校验消息层」 |
| `[Unit]` | 6 个重载里 4 个直接吃 `Units`——约 200 个成员的自有枚举，外加换算/显示引擎与右键换单位菜单。**独立大件**，没有便宜的半成品形态 |
| `[RequiredIn]` `[DisallowModificationsIn]` | 都要 `PrefabKind`（**数值未核实**）+ 预制体种类探测（`PrefabUtility`）+ 校验消息层。两者共用同一块尚不存在的基础设施，**成对推迟** |
| `[HideNetworkBehaviourFields]` | 作用于 UNet 的 `NetworkBehaviour`（Network Channel / Send Interval）——该类型在 Unity 6 已不存在，唯一可能的实现是静默 no-op |
| `[ShowPropertyResolver]` | 本包只有一个值后端，没有「property resolver」这个概念，做出来是编造的调试信息。等反射后端出现再说 |
| `[SuppressInvalidAttributeError]` | 当前没有「特性用在不该用的类型上」的告警层可抑制，声明它等于静默 no-op |
| `[DisableContextMenu]` | 成员绘制交给 `EditorGUI.PropertyField`，右键菜单由它内部掌管、没有现成开关——**要先原型验证可拦截**，否则做出来的是假实现 |

### 两处边界与理由

- **`[Toggle]` 与 `[ToggleGroup]` 一起做。** 两者都要写「flag 那个 bool」（跨成员读写）；
  `[ToggleGroup]` 若只做「读 bool 门控」而不画复选框，是半个特性——观感与语义都对不上 Odin。
  故先落一个共用的小改动（从成员节点取 `SerializedProperty`；分组节点没有值入口，
  从**第一个带值入口的后代**取 `serializedObject`），两个特性同轮实现。
- **`[HideMonoScript]` 需要一小块新机制。** 它要在构建期抑制 `m_Script` 节点，而现有两条路都够不着：
  调用方的 `memberFilter` 是窗口路径专用（Inspector 路径**刻意保留** `m_Script` 以与原生一致），
  处理器的「父级注入」钩子又显式跳过没有 `MemberInfo` 的成员。故这是独立的构建期改动，独立成提交。

---

## 六、第三批签名核对（2026-10-04）——L1b 剩余值绘制器

补 L1b 剩余值绘制器之前，把候选的 **11 个特性 + 1 个支撑枚举**的 Odin 官方签名逐条抄了下来。
**不做的也一并核**——否则「不做」的理由只是印象，下一轮会重新猜一遍。取法同前两节：
**`Invoke-WebRequest`，别用 `WebFetch`**（后者抓这个站会截断成只剩导航，本轮又踩了一次）。

**新增一条取法经验：** API 页的左侧导航极长，正文在**最后一个 `Version 3.3.1.2` 标记之后**。
按这个锚点截取比按类名查找可靠——类名在导航里会先出现一次，`IndexOf` 会停在错误的位置。

### 跨特性共性

1. 11 个全在 `Sirenix.OdinInspector`（`ObjectFieldAlignment` 是同一命名空间下的枚举）。
2. `AllowMultiple = false, Inherited = true`：`MinMaxSlider`、`FilePath`、`FolderPath`、
   `PreviewField`、`ValueDropdown`、`TypeFilter`、`AssetList`、`ColorPalette`。
   **2 例没有 `[AttributeUsage]`**（`AssetSelector`、`Searchable`）——与前两批的
   `EnumToggleButtons`/`ChildGameObjectsOnly`/`Unit`/`ShowPropertyResolver` 同款；
   `TypeDrawerSettings` 有但未声明这两项。我们照旧**刻意收窄**。
3. 除 `TypeDrawerSettings` 外都带 `[Conditional("UNITY_EDITOR")]`（与第二轮的 `UnitAttribute` 同款例外）。
4. **本批第一次出现「`UnityEngine` 类型进了构造签名」**：`PreviewFieldAttribute` 的两个
   `FilterMode` 重载。按「已否决的形状」第 14 条**永久否决**，不是推迟。
5. **字符串参数一律是 Odin 的 resolved string——这是本批最要紧的一条。**
   `MinMaxSlider` 的三个 getter、`ValueDropdown.valuesGetter`、`TypeFilter.filterGetter`
   官方原文都是 "A resolved string that should evaluate to…"；`FilePath`/`FolderPath` 的
   `ParentFolder`/`Extensions` 标注 "Supports member referencing with `$`"。
   **本包对它们的收窄与条件族完全同款**（「已否决的形状」第 13 条）：只认**序列化成员名**，
   `$`/`@`/方法一律不做。不这么做就会得到「签名对了、主要用法用不了」的假象。

### 逐条：官方签名 → 本轮决定

| 特性 | 官方构造重载 | 本轮实现 | 不实现的部分与理由 |
|---|---|---|---|
| `[FilePath]` | `()`；字段 `AbsolutePath`、`Extensions`、`IncludeFileExtension`、`ParentFolder`、`RequireExistingPath`、`UseBackslashes` | 全部 | `$` 成员引用不做（共性 5）。**只作用 `string`**，数组形不做（要自管数组绘制，属 L6） |
| `[FolderPath]` | `()`；字段 `AbsolutePath`、`ParentFolder`、`RequireExistingPath`、`UseBackslashes` | 全部 | 同上 |
| `[MinMaxSlider]` | 5 组：`(float minValue, float maxValue, bool showFields = false)`、`(float, string maxValueGetter, bool)`、`(string minValueGetter, float, bool)`、`(string, string, bool)`、`(string minMaxValueGetter, bool)`；字段 `MinValue`/`MinValueGetter`/`MaxValue`/`MaxValueGetter`/`MinMaxValueGetter`/`ShowFields` | 五组全做 | 三个 getter 收窄为**序列化成员名**：`MinMaxValueGetter` 指向序列化 `Vector2`，`MinValueGetter`/`MaxValueGetter` 指向序列化 `float`。**只作用 `Vector2`**（`Vector2Int` 见下） |
| `[PreviewField]` | `()`、`(ObjectFieldAlignment alignment)`、`(float height)`、`(float height, ObjectFieldAlignment)`、`(string previewGetter, ObjectFieldAlignment, FilterMode)`、`(string, float, ObjectFieldAlignment, FilterMode)` | 前四个 | 后两个含 `UnityEngine.FilterMode`，**永久否决**（第 14 条）。`previewGetter` 本身也是 resolved string |
| `[ValueDropdown]` | `(string valuesGetter)`；字段 18 个：`AppendNextDrawer`、`CopyValues`、`DisableGUIInAppendedDrawer`、`DisableListAddButtonBehaviour`、`DoubleClickToConfirm`、`DrawDropdownForListElements`、`DropdownHeight`、`DropdownTitle`、`DropdownWidth`、`ExcludeExistingValuesInList`、`ExpandAllMenuItems`、`FlattenTreeView`、`HideChildProperties`、`IsUniqueList`、`NumberOfItemsBeforeEnablingSearch`、`OnlyChangeValueOnConfirm`、`SortDropdownItems`、`ValuesGetter` | `valuesGetter` + 展示类选项 | 数据源收窄为**序列化数组/List 成员**（`valuesGetter` 官方语义就是「可赋给 `IList` 的值」）。四个**只对列表有意义**的选项（`IsUniqueList`、`DrawDropdownForListElements`、`ExcludeExistingValuesInList`、`DisableListAddButtonBehaviour`）**不声明**——本包不支持数组形态，声明了只能是静默 no-op。`ValueDropdownItem<T>`/`ValueDropdownList<T>` 是 Odin Serializer 的类型，本包不引入 |
| **`ObjectFieldAlignment`**（枚举） | `Center`/`Left`/`Right` | 自建 | 文档站按字母序排，**数值未核实**——我们按 `Left`/`Center`/`Right` 从 0 起排并写进 README（与 `InfoMessageType`/`TitleAlignments` 同款处理） |

**`[MinMaxSlider]` 只做 `Vector2`，不做 `Vector2Int`**：`SerializedPropertyValueEntry`
支持的类型里没有 `Vector2Int`（只有 `Vector2/3/4`），走值入口会抛 `NotSupportedException`。
绕开值入口直读 `SP.vector2IntValue` 是可行的，但那会让本特性成为「唯一一个不走值后端的值绘制器」，
为半个类型破一条架构缝不值得。记进 README 已知限制。

### 本轮不实现（都记在此，别处不再重复）

| 特性 | 为什么不 |
|---|---|
| `[Searchable]` | 官方原文：加的是「搜索**该字段或类型的子成员**」的过滤器（另有 `Recursive`、`FilterOptions`、`ISearchFilterable`）。本包把嵌套与集合的子成员整个交给 `PropertyField(includeChildren: true)`——**不拥有子绘制权就无从过滤**。移 L6 |
| `[TypeFilter]` | 唯一构造是 `(string filterGetter)`，官方原文同样是 resolved string（要解析成可赋给 `IList` 的值），Odin 样例里它是个**方法**。且被标注的字段是抽象/接口类型——原生 Unity 不配 `[SerializeReference]` 切换根本序列化不了（L7）。与 `[CustomValueDrawer]`/`[ValidateInput]` 同类 |
| `[TypeDrawerSettings]` | 它是「Type Drawer」的选项（`BaseType`、`Filter` 取 `TypeInclusionFilter` 位标志），样例一律挂在 `[ShowInInspector]` 的 `System.Type` 字段上。**Unity 不序列化 `System.Type`**，没有反射后端它根本不进树。移 L3 |
| `[AssetList]` | 官方原文：「**替换默认的列表绘制器**」「对列表与单个元素都有效，但**行为不同**」，且有 `AutoPopulate`、`Tags`、`LayerNames`、`AssetNamePrefix`、`Path`、`CustomFilterMethod`（方法名）。它是列表绘制器（增删/Undo 属 L6），且只做单元素那半会得到一个语义随目标类型而变的半成品。移 L6 |
| `[ColorPalette]` | 构造 `()` 与 `(string paletteName)` 都是字面量，**边界没挡住它**——缺的是数据：官方原文让人去 **Tools > Odin > Inspector > Preferences > Drawers > Color Palettes** 里编辑调色板，那是 Odin 自己的设置存储。本包没有「命名调色板存在哪、谁来编辑」这一层，凭空造一个形状比不做更糟。**先做一个调色板来源的设计**，再谈特性 |
| `[AssetSelector]` | 构造 `()`、字段 `Paths`/`Filter`/`FlattenTreeView` 等**都过得了边界**，起初是被**工作量**挡住的（默认是项目文件夹的树视图弹出层）。**后来做了**，见下 |

### 落地时的三处收窄（比上表更细，都写进了特性的类注释）

上表只写到「实现哪几组重载」。真正落地时又收了三处，理由同源——**不猜、不造表面**：

1. **只声明有真行为的选项。** `[ValueDropdown]` 与 `[AssetSelector]` 各有一批 Odin 选项
   （`DropdownTitle`、`DropdownHeight`、`DropdownWidth`、`ExpandAllMenuItems`、
   `NumberOfItemsBeforeEnablingSearch`、`CopyValues`、`DoubleClickToConfirm` 等，以及四个
   只对列表有意义的），本包**一个都不声明**。它们的共同点是：在编辑器自带菜单上**没有对应物**，
   声明了只能是静默 no-op——而本包最忌讳的就是「编译得过但什么都不发生」。
   两条守卫测试（`只声明支持的选项`）把这个约定钉住。
2. **不引入 `ValueDropdownItem` / `ValueDropdownItem<T>` / `ValueDropdownList<T>`。**
   它们是 Odin Serializer 时代的「标签/值对」类型。本包承诺的边界是「**序列化数组/List**
   作数据源」，纯值数组已经够用；加它们等于新增一组「不确定 Unity 能不能序列化」的公开类型。
   **代价是真的**：Odin 代码里用 `ValueDropdownList<T>` 的写法在本包编译不过。
3. **`[FilePath]` 不声明 `IncludeFileExtension`。** 官方只给了它一句话
   （"If true the file path will include the file's extension."），**默认值与确切语义都没核对到**。
   一个「设了也不产生行为」的开关，宁可让它编译不过。

**这三处的共同判据**：`[PropertyRange].MinGetter` 那种「只读的残留成员」是**形式**的残留
（不可设，恒为 null），而上面这些是**可设的旋钮**——旋钮不生效才是骗人的。

### 两处数值由本包自定（与 `InfoMessageType`/`TitleAlignments` 同款）

- `ObjectFieldAlignment` 的成员**顺序**（文档站按字母序排，数值未核实）→ 按
  `Left`/`Center`/`Right` 从 0 起排，并有守卫测试钉住（改顺序是破坏性变更）。
- `[PreviewField]` 的**默认高度（64）与默认对齐（`Left`）**——Odin 的默认值存在它的偏好设置里，
  官网核不到。两条都写进了包 README 的已知限制，不藏在行为里。

---

## 七、第四批签名核对（2026-10-04）——内嵌编辑器一族

补 `[InlineEditor]` 一族之前，把 1 个特性 + 3 个支撑枚举 + 3 个内嵌环境条件族的
**Odin 官方签名**逐条抄了下来。取法同前：**`Invoke-WebRequest`，别用 `WebFetch`**。

**新增一条取法经验：** 站点改版后「单特性页」（`/attributes/<kebab>-attribute`）已变成
**客户端渲染**——原始 HTML 里根本没有类定义（按类名 `IndexOf` 全为 -1，本轮先踩了一次）。
**API 文档页仍是服务端渲染**，照旧可用：

```powershell
Invoke-WebRequest https://odininspector.com/documentation/sirenix.odininspector.<小写类名>
```

上一节记的「正文在最后一个 `Version 3.3.1.2` 标记之后」那条锚点也已失效——但那是因为
抓错了页型；文档页不必截取，直接按 `public class` / `public enum` 定位即可。

### 跨特性共性

1. 全部在 `Sirenix.OdinInspector`（程序集 `Sirenix.OdinInspector.Attributes`），
   都带 `[Conditional("UNITY_EDITOR")]`，签名里全是 `AttributeTargets.All`。
   我们照旧**刻意收窄**到 `Field | Property`，也照旧**不带** `[Conditional]`。
2. **三个内嵌环境条件是零参数空标记**，判据在编辑器侧——与本包那四个模式条件同款
   （那个判据是 `Application.isPlaying`，这个是绘制期的嵌套深度）。
3. **本批第一次没有出现 resolved string**：`[InlineEditor]` 的签名里一个字符串都没有
   （前一批「字符串参数一律是 resolved string」那条共性在这批不适用）。

### 逐条：官方签名 → 本轮实现

| 特性 | 官方构造重载与成员 | 本轮实现 | 不实现的部分与理由 |
|---|---|---|---|
| `[InlineEditor]` | `(InlineEditorModes = GUIOnly, InlineEditorObjectFieldModes = Boxed)`、`(InlineEditorObjectFieldModes)`；字段 `DisableGUIForVCSLockedAssets`（默认 true）、`DrawGUI`、`DrawHeader`、`DrawPreview`、`IncrementInlineEditorDrawerDepth`、`MaxHeight`、`ObjectFieldMode`、`PreviewAlignment`、`PreviewHeight`、`PreviewWidth`；属性 `Expanded`、`ExpandedHasValue` | 全部 | 无收窄——六个模式、四种对象字段模式、预览、两个安全选项一次做完 |
| `InlineEditorModes` | `FullEditor` `GUIAndHeader` `GUIAndPreview` `GUIOnly` `LargePreview` `SmallPreview` | 自建 + 数值自定 | 文档站按字母序排、数值未核实 → 默认成员排 0，其余按「画得越来越多」 |
| `InlineEditorObjectFieldModes` | `Boxed` `CompletelyHidden` `Foldout` `Hidden` | 自建 + 数值自定 | 按「字段露出多少」由多到少；默认 `Boxed` 排 0 |
| `PreviewAlignment` | `Bottom` `Left` `Right` `Top` | 自建 + 数值自定 | 本包的默认（在右）排 0，其余先左右后上下 |
| `[ShowInInlineEditors]`、`[HideInInlineEditors]`、`[DisableInInlineEditors]` | 各自只有 `()` | 全部 | 无参数可收窄 |

**模式 → 三面旗的映射据官方对每种模式的描述文字推导**（不是猜数值）：`FullEditor`
「编辑器头 + 界面在左、小预览在右」、`GUIAndHeader`「界面与头」、`GUIAndPreview`
「界面在左、小预览在右」、`GUIOnly`「只有界面」、`LargePreview`/`SmallPreview`
「只有预览（无界面）」。**Odin 的公开面上没有存模式的地方**，本包照此在构造期把模式
拆成三面旗——于是具名实参可以事后覆盖任何一面旗（`[InlineEditor(GUIOnly, DrawPreview = true)]`）。

### 本包自定的值（Odin 存在它的偏好设置里，官网核不到）

| 值 | 取多少 | 落在哪 |
|---|---|---|
| 嵌套深度上限 | `4` | `InlineEditorDrawContext.MaxDepth`（注释里写明本包自定） |
| 预览默认尺寸 | 并排时宽 `64`、单独时高 `64`、大预览 `128` | `InlineEditorLayout` 与 `[InlineEditor].DefaultLargePreviewHeight` |
| 默认预览位置 | 在右 | `PreviewAlignment.Right = 0`（`default` 即默认行为） |

### 一处刻意的语义差异

`CompletelyHidden` 且值为空时，本包画**一行灰字提示**（「隐藏了对象字段」）而不是留一片空白
（Odin 留白）。理由是一以贯之的那条：**不接受静默地什么都不画**。有值时不提示——
内嵌内容就在下面，那一行不是空白。这条差异写进了特性注释、包 README 与展示台 README。

### 顺手记下的两条（L4，本轮未做）

核对时顺手取了下一轮候选的签名，免得下轮重核：`PropertyOrderAttribute`——
构造 `()` 与 `(float order)`，字段 `Order`，`AllowMultiple = false, Inherited = true`；
`InlinePropertyAttribute`——构造只有 `()`，唯一字段 `LabelWidth`（`int`），
`Inherited = false`，官方样例明说**类与成员都能标**（标在类上时该类型的字段一律内联）。

---

## 八、审计记忆

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
