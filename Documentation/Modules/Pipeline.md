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

> **2026-10-04 追记：** 这条纪律**成立**，但它的条件是会变的——四个 `[XxxIn]` 的签名
> 后来核到了（§十），于是它们落地了；`[ShowIfGroup]` / `[HideIfGroup]` 的签名同日一并核到
> （构造 `(string path, bool animate = true)` 与 `(string path, object value, bool animate = true)`，
> 另有 `Value` / `Animate` / `Condition` 与 `CombineValuesWith`），**仍没做的原因换了一个**：
> 它们要在**分组装配之后**才能挂判据（分组节点那时才存在），而构建期没有那个阶段。
> **「不做」的理由要跟着复核，不然它会过期成一句空话。**

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

### 17. 反射成员可编辑（第六轮，否决）

这是 L3 最想当然的形状：既然是 `PropertyInfo`/`FieldInfo`，`SetValue` 一行就能写。
**否决的理由不是做不到，是做不到不骗人。** 这些成员按定义不在 Unity 的序列化里：
写进去拿不到 Undo、不会标脏、不随存档保存，下次域重载就回来了。
Odin 自己的文档也写着「`[ShowInInspector]` 不序列化任何东西，改动不会随之保存」
——他选择照写但提示，本包选择**干脆不给写**。

代价是少了一个「快速调参」的用法。换来的是：这个后端的每一条承诺都是真的，
`SetValue` 恒抛这件事在源码里只有一处，读的人不会猜。

### 18. 每帧反射取值（第六轮，否决）

最省事的实现是 `PropertyInfo.GetValue(target)` 直接在绘制器里调——反射成员的值每帧都要读。
但「反射仅限构建期」是本仓的硬规则，而一处例外就足以让整条规则再也说不清。

改法是把成员编译成委托（`ReflectedAccessor`，表达式树 + `Compile()`），建树时一次，
绘制期只剩委托调用加一次装箱。为此先在 Mono 上实测了「表达式树能不能访问私有成员」
（能，11 例 spike 全过），**没有**退回 `DynamicMethod(skipVisibility: true)`。

同一条理由也管条件：`[ShowIf("某属性")]` 的求值在**每帧路径**上，
故它走的是强类型读取器 `TryCreateBooleanReader`（不装箱），方法则绑成委托而**不是每帧 `Invoke`**。

### 19. POCO 目标自动收 public 成员（第六轮，否决）

Odin 的 Static Inspector 会把你给它的对象的成员自动收一批出来。那是建立在他的
Serializer 类型系统上的：他有一套「什么算可序列化」的定义。**本包没有那套东西**，
照搬只能退化成「凡 public 就收」——于是同一个类型在 Inspector 里靠序列化、在窗口里靠可见性，
两套语义并存，而它们对同一个字段的答案可以不同。

改成显式：非 Unity 对象的目标**只收带 `[ShowInInspector]` 的成员**，空树时画一句解释
而不是一片空白。想让它出现，就标上。

### 20. `XInspectorDrawer.NeedsUnityBackend` 虚属性（第六轮，否决）

问题是真的：`[ShowInInspector, PropertyRange]` 是本轮最现实的组合（Odin 官方样例就这么写），
而 `[PropertyRange]` 对反射成员无效——本包不能接受「标了没反应」。

草图是给绘制器基类加一个 `internal virtual bool NeedsUnityBackend`，由需要序列化属性的绘制器覆写，
末端据此画一条行内提示。**清点之后否决**：真正**静默**的只有两处
（`[DelayedProperty]`、`[OnValueChanged]`），其余十几个经 `DrawerWarnings.Once` 早在 Console 有告警。
为一个「行内 vs Console」的差别，加 25 处与绘制器代码重复的声明，还多一类「新绘制器忘了覆写」
的新静默面——不值。

落地的形状是：**补上那两处静默点的告警 + 让 `DrawerWarnings.TypeMismatch` 认后端**
（对反射成员不再说「字段退回普通绘制」——它没有字段可退，措辞错了比不说更糟）。

---

## 三、未决项

还没想清楚、但迟早要面对的：

| 问题 | 卡在哪 |
|---|---|
| 类级 `[BoxGroup]` 分发到成员 | 需要处理器层。且要想清楚：类级分组与成员自己声明的分组**冲突**时谁赢？ |
| 多对象编辑下 `[ShowIf]` 的语义 | 各目标条件值不同时「显示还是不显示」没有显然答案。**第六轮部分落地**：反射条件取**第一个目标**，与序列化条件（`SerializedProperty.boolValue` 读的同样是主目标）保持一致——语义是「不扩大」，不是「解决了」 |
| ~~反射后端如何标脏与撤销~~ | **已结案（第六轮）**：只读。理由与三类否决见 §二 第 17 条 |
| 嵌套 `[Serializable]` 类型里的反射成员 | 拿到嵌套实例需要一条「只读反射路径解析」（`[Button]` 对嵌套类型的老限制同源）。它一旦有了，`[ShowInInspector]` 与 `[Button]` 会同时受益——这正是它值得单独一轮的原因 |
| `[ToggleGroup]` 一族在反射树上的解析 | 它们按序列化路径找「开关字段」，POCO 树上找不到。现状是告警失效；要与条件族那样三级解析，得先把「按名找成员」这件事整个收成一层 |
| 反射成员的值每帧读一次 | 只读展示每帧现读是刻意的（缓存会「该变不变」），但用户 getter 有副作用或开销时没有退路。要不要给一个「手动刷新」的开关，等真有抱怨再说 |
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
  （`PrefabKind` **2026-10-04 已做**，见 §十；`Units` 与 `TabLayouting` 仍不做。）

### 本轮不实现（都记在此，别处不再重复）

| 特性 | 为什么 |
|---|---|
| `[CustomValueDrawer]` | 唯一重载就是 `(string action)`——resolved string（方法/表达式调用）。**属 L5 性质**（调用目标对象） |
| `[ValidateInput]` | `(string condition, string defaultMessage = null, InfoMessageType = Error)`——condition 可为方法，另有 `$value` 具名参数。同样 L5 性质，且需要一个尚不存在的「校验消息层」 |
| `[Unit]` | 6 个重载里 4 个直接吃 `Units`——约 200 个成员的自有枚举，外加换算/显示引擎与右键换单位菜单。**独立大件**，没有便宜的半成品形态 |
| `[RequiredIn]` `[DisallowModificationsIn]` | 都要 `PrefabKind`（**数值未核实**）+ 预制体种类探测（`PrefabUtility`）+ 校验消息层。两者共用同一块尚不存在的基础设施，**成对推迟**（**2026-10-04 已翻案**：那块基础设施随 §十 落地，两个特性一并实现） |
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

## 八、第五批签名核对（2026-10-04）——按钮族与回调族

补 L5 的 `[Button]` 家族与回调族之前，把 4 个按钮特性 + 6 个回调特性 + 3 个支撑枚举的
**Odin 官方签名**逐条抄了下来。取法同前：`Invoke-WebRequest`，别用 `WebFetch`。

**本次两条取法经验**（上一节那条「文档页仍是服务端渲染」成立，但截取方式要改）：

1. `WebFetch` 会因侧边栏过长而**截断**，正文取不到。正解是抓原始 HTML 后**只抽代码块**：

   ```powershell
   $html = (Invoke-WebRequest -UseBasicParsing $url).Content
   [regex]::Matches($html, '(?s)<pre><code class="lang-csharp hljs">(.*?)</code></pre>')
   ```

   类声明、构造重载、字段/属性全在里面，一次抽干净。
2. **枚举成员不在代码块里**，藏在 `<h3 id="fields">Values</h3>` 下的
   `child-header-name` 里（且**按字母序排**——顺序不能当声明顺序用）。每个成员还带一句官方描述，
   本次正是靠它定下了 `ButtonStyle` 的语义与 `Gigantic` 的比例。

### 跨特性共性

1. `[Button]` 与三个 `[On*]` **继承 `ShowInInspectorAttribute`**，因此官方签名里全是
   `AttributeTargets.All`（不是 `Method`）。本包照旧**刻意收窄**到实际会生效的目标
   （`[Button]` 只给 `Method`，`[InlineButton]`/`[OnValueChanged]` 等只给 `Field | Property`），
   也照旧**不带** `[Conditional("UNITY_EDITOR")]`。
2. **三个回调只有 resolved string 形态**（见下），这是本批最重要的一条——
   它们此前被判 ⛔ 的理由是「归 L5 性质」，即卡在「没有按名调方法的能力」；
   本轮把这套能力做出来之后，它们可以落地，但**收窄为「本类型上的方法名」**。
3. `[ButtonGroup]`/`[ResponsiveButtonGroup]` 是 `PropertyGroupAttribute` 的子类，
   且 `AttributeUsage` 是 **`Method`**——基类是 `Field | Property | Class` 且 `AttributeUsage`
   **会被派生类继承**，所以子类不重声明就**编译不过**（响的失败，比静默好）。

### 逐条：官方签名 → 本包决定

| 特性 | 官方签名（3.3.1.2 原样） | 本包本轮实现 | 不实现的部分与理由 |
|---|---|---|---|
| `ButtonAttribute` | `All, AllowMultiple=false, **Inherited=false**`；15 个重载：`()`、`(string name)`、`(ButtonSizes)`、`(int buttonSize)`、`(ButtonStyle)`、`(SdfIconType…)` 与它们的组合 | `()`、`(string name)`、`(ButtonSizes size)`、`(string name, ButtonSizes size)`；`Name`、`Size` | `ButtonStyle`（只管参数区形态，见下）、像素高度重载、`Expanded`/`DisplayParameters`/`DirtyOnClick`/`DrawResult`/`ButtonAlignment`/`Stretch`、`SdfIconType` 一族（第 14 条永久否决） |
| `ButtonGroupAttribute` | `Method, AllowMultiple=true, Inherited=true`；**`(string group = "_DefaultGroup", float order = 0F)`** | 照抄（含默认组名） | `ButtonAlignment`/`Stretch`/`IconAlignment` |
| `ResponsiveButtonGroupAttribute` | `Method, AllowMultiple=true, Inherited=true`；**`(string group = "_DefaultResponsiveButtonGroup")`** | 照抄（含默认组名） | 同右列 |
| `InlineButtonAttribute`（sealed） | `All, AllowMultiple=true, Inherited=true`；`(string action, string label = null)`、`(string action, SdfIconType icon, string label = null)` | `(string methodName)`、`(string methodName, string label = null)`；`MethodName`、`Label` | 图标重载（永久否决）、`ShowIf`/`ButtonColor`/`TextColor`（后两个是颜色解析，本包没有样式系统） |
| `OnInspectorInitAttribute` | `All, AllowMultiple=true, Inherited=false`；`()`、`(string action)` | 只做**标在方法上**的 `()` | `(string action)` 形式 |
| `OnInspectorDisposeAttribute` | 同上 | 同上 | 同右列 |
| `OnInspectorGUIAttribute`（sealed） | `All, AllowMultiple=false, Inherited=true`；`()`、`(string action, bool append = true)`、`(string prepend, string append)` | 只做**标在方法上**的 `()` | `action`/`prepend`/`append` 形式；标在**字段**上的形式（依赖 Odin 的 `OnInspectorGUI`/`Draw*` 命名约定，未核清） |
| `OnValueChangedAttribute`（sealed） | `All, AllowMultiple=true, Inherited=true`；**`(string action, bool includeChildren = false)`** | `(string methodName)` | `includeChildren`（默认 false，不声明即同默认）、`InvokeOnInitialize`、`InvokeOnUndoRedo` |
| `OnStateUpdateAttribute`（sealed） | `All, AllowMultiple=true, Inherited=true`；**`(string action)`——没有无参构造** | `(string methodName)` | 无（时机是本包自定，见下） |
| `CustomContextMenuAttribute`（sealed） | `All, AllowMultiple=true, Inherited=true`；**`(string menuItem, string action)`** | `(string menuItem, string methodName)`，只做**字段**目标 | 标在方法上的形式 |
| `ButtonSizes` | `Small` `Medium` `Large` `Gigantic`（官方描述：Gigantic「两倍于 Large」） | 自建 + **数值自定** | 数值未核实 |
| `ButtonStyle` | `Box`「参数外套折叠盒，按钮在盒底」、`CompactBox`「参数外套折叠盒，按钮在**盒头**——**带参方法的默认**」、`FoldoutButton`「按钮 + 展开参数的折叠」 | **不声明该选项**：本包固定按 `CompactBox` 的形态画（参数折叠在按钮同一行的箭头下） | 三个值都只是参数区的三种摆法；本轮只有一种，声明了就是三个里两个不生效——「可设的旋钮不生效才是骗人的」 |
| `IconAlignment` | `LeftOfText` `RightOfText` `LeftEdge` `RightEdge` | 不做 | 随图标一族永久否决 |

### 三条推翻先前假设的发现

1. **`[ButtonGroup]` 裸用时的默认组名 Odin 有明文**（`"_DefaultGroup"`，
   `[ResponsiveButtonGroup]` 是 `"_DefaultResponsiveButtonGroup"`）。原先准备「自定一个
   不冲突的名字」——照抄即可，自造是多余的。
2. **`[Button]` 是 `Inherited = false`**：覆写方法**不继承**特性。收集器仍要按方法名去重
   （覆写方自己也标了的情况），但不会因为继承而重复。
3. **`[OnValueChanged]` `[OnStateUpdate]` `[CustomContextMenu]` 三个只有 resolved string 形态**——
   本包一贯判 ⛔ 的那种形状。它们能落地，是因为本轮把「按名调方法」这条能力做出来了，
   而不是因为放宽了判据：**只认本类型上的方法名**，不实现 Odin 的 `$`/`@`/表达式/带参调用。

### 本包自定值

| 值 | 取多少 | 理由 / 落在哪 |
|---|---|---|
| `ButtonSizes` 的像素高度 | `Small=20` `Medium=25` `Large=30` `Gigantic=60` | 数值官网核不到；**成员名照抄**，`Gigantic` 按官方描述「两倍于 Large」取 60。落在枚举注释、包 README 与展示台 |
| `[OnStateUpdate]` 的时机 | **每趟 GUI 布局（`EventType.Layout`）调用一次** | 本包没有 Odin 的 state update 循环，只有 IMGUI 的 Layout/Repaint 两趟；取 Layout 恰好每趟一次（取 Repaint 会漏掉纯布局趟）。**这是自定语义，写进包 README** |
| 参数区的形态 | 固定 `CompactBox` 式（按钮与折叠箭头同行，参数在下方缩进） | 与 Odin 的带参默认一致；不提供 `ButtonStyle` 选项 |

### 落地时的两条实测结论（改动前先读）

**其一：按钮排不到字段之间。** 原计划是「按声明顺序把按钮插回字段之间」，
实测做不到——三条测量都有用例钉着（`MethodNodeTests.为什么排不到字段之间` 那一节）：

1. 字段令牌在 FieldDef 表（`0x04`）、方法令牌在 MethodDef 表（`0x06`），**两张表各自编号**。
   实测同一个夹具里字段行号是 94/95、方法行号是 282/283，跨表比大小没有意义。
2. `Type.GetMembers()` 也**不按声明顺序**返回（实测先方法、再构造函数、后字段）。
3. 同一张表内行号递增**确实**等于声明顺序——故「按钮之间」的先后仍然正确，只有跨类不行。

结论：按钮一律排在字段之后，并写进包 README 的已知限制。
**位置不理想是小事，把按钮插到随机位置才是大事。**

**其二：条件族必须放宽到方法，否则按钮配不了条件。** 条件的 `AttributeUsage` 原本是
`Field | Property`，`[Button, DisableIf(...)]` 会**编译不过**。11 个条件特性（四个条件 +
四个模式 + 三个内嵌环境）因此加上 `AttributeTargets.Method`。判据是
**方法会产生属性树节点而普通属性不会**——放宽的只该是真正会生效的那一侧；
`[LabelText]`／`[Indent]`／`[GUIColor]` 那几个**没有**跟着放宽，它们作用于值控件与标签，
而按钮不是值控件。

### 落地时新增的两块基建

| 基建 | 为什么非有不可 |
|---|---|
| **方法节点**（`InspectorPropertyKind.Method`） | 树的成员来自 `SerializedObject` 的迭代器，**方法根本不在候选集里**。于是有了这条构建期反射通道、没有值入口的节点、以及方法专用末端 |
| **折行布局**（`GroupChildrenLayout.CellRows` + 末端开关水平作用域） | `[ResponsiveButtonGroup]` 要按可用宽度折行，而原有的策略**只表达单行**。不走这条路而让分组绘制器自己驱动子节点，就会跳过同节点上更内层的绘制器——那正是这个策略对象当初存在的理由 |

### 本轮不实现（都记在此，别处不再重复）

- `[Button]` 的 `ButtonStyle` / 像素高度 / 布局一族 / 图标一族 / `DrawResult` / `DirtyOnClick`。
- `[InlineButton]` 的图标重载与三个颜色/条件字段。
- 三个回调的 `action` 变体，以及「`[OnInspectorGUI]` 标在字段上」的形式。
- `[OnValueChanged]` 的 `includeChildren` / `InvokeOnInitialize` / `InvokeOnUndoRedo`。
- `[CustomContextMenu]` 标在方法上的形式。
- **嵌套 `[Serializable]` 类型里的按钮**：本轮只对 Inspector 检视的根对象生效
  （拿到嵌套实例需要一条本包没有的「只读反射路径解析」，见 Roadmap 的 L3）。

### 回调族的三处形状收窄（与副作用）

| 特性 | Odin 的形状 | 本包的做法 | 为什么 |
|---|---|---|---|
| `[OnStateUpdate]` | `(string action)`，跑在 Odin 自己的 state update 循环里 | 裸标在方法上，**每趟 GUI 布局**跑一次 | 本包没有那个循环；只有 IMGUI 的 Layout/Repaint 两趟，取 Layout 恰好每趟一次（取 Repaint 会漏掉纯布局趟）。方法名也不必再写一遍字符串 |
| `[OnValueChanged]` | `(string action, bool includeChildren = false)`，比对旧值 | 判据是「绘制这一趟里值前后不一致」 | 不必跨帧记旧值，于是「旧值该放哪」这个难题自然消失；也没有第一帧误报 |
| `[CustomContextMenu]` | 标在成员上，菜单进 Inspector | 同上，但**菜单在字段自己那一行** | Unity 的头部右键菜单由它自己掌管，没有公开注入点 |

**一处必须记住的副作用：生命周期钩子既无绘制器也无处理器。**
`XInspectorUsageDetection` 那条判据按「有没有绘制器或处理器」判断，本会漏掉它们——
后果与条件族当年一样：**类型不被自动接管、特性静默不生效、零告警**。
故新增 `ITreeLifecycleAttribute` 标记接口，让这两个地方都认得它们。

---

## 九、第六批签名核对（2026-10-04）——L3 反射值后端

L3 只有两个新面孔，但两个都得核——它们各自的形状直接决定整层怎么做。

### 逐条：官方原文 → 本包决定

| 项 | 官方事实 | 本包决定 |
|---|---|---|
| `[ShowInInspector]` | 「used on any member, and shows the value in the inspector」；**无构造参数**（样例一律裸写）；可写性**由成员自己决定**（get-only 就只读、有 setter 就能写）；**静态成员也在样例里** | 收；`AttributeUsage` 收窄到 `Field \| Property`，**不含 Method**（见下） |
| `[ShowInInspector]` 的序列化语义 | 官方原文：「will not serialize anything; meaning that any changes you make will not be saved」 | **照做，但推到底**：既然改动不保存，本包干脆不给写（§二 第 17 条）。Odin 是「能改但不保存」，本包是「不给改」——这条差异写进了 README |
| `GetTarget()` | `protected override object GetTarget()`；「give it any instance of any type to render」，**不必可序列化、不必是 Unity 对象** | 收，`protected virtual object GetTarget() => this` |

### 两处收窄

**其一，`[ShowInInspector]` 不标方法。** Odin 的特性页**没有**记录方法用法——它样例里的方法
用的是 `[Button]` 与 `[OnInspectorInit]`。而本包已有 `[Button]`，「显示一个方法的返回值」
没有既定语义。于是 `AttributeUsage` 不含 `Method`：标错**编译期就报错**（响的），
而不是标了没反应。哪天核到官方语义，放宽 `AttributeUsage` 是纯加法。

**其二，`GetTarget()` 不接收「浮空 Inspector」那整套。** 目标可以是任意对象，但**没有**
对象选择器、没有 target 槽位的工具栏 UI——那是第四轮就否决过的另一种产品形态
（§二 第 12 条），本轮的 `GetTarget()` 只解决「检视谁由子类说了算」。

### 落地时的三条实测结论（改动前先读）

**一、表达式树在本仓环境里能访问私有成员。** 这是整层最大的未知数：编译期委托要读
`private` 字段与属性，而 .NET 的 `LambdaCompiler` 在某些信任级别下会拒绝。
先写了 11 例 spike（私有字段、私有属性、值类型、静态成员各覆盖），全过——
**没有**退回 `DynamicMethod(skipVisibility: true)`。那条退路仍写在代码注释里。

**二、`Object[]` 变 `object[]` 会静默弄丢一条语义。** 形参类型一旦写成 `object`，
裸写 `target != null` 就退化成引用比较，而 Unity 的已销毁对象恰恰是「引用不为 null、
语义为空」。以前这是白送的（数组是 `Object[]`，`!= null` 自动走 Unity 的重载）。
**改型时顺手新增 `TargetObjects.IsAlive` 把这层语义收成一处**，逐点补回——
不补的症状是「多选里混了已销毁对象时按钮对它照调不误」，不致命，但是静默的行为退化。

**三、`Undo.GetCurrentGroupName()` 不能当「记没记 Undo」的判据。** 组名只在显式
`SetCurrentGroupName` 之后才有，`RecordObjects` 不给它命名——于是 `断言 != 我们给的名字`
在任何情况下都成立。本仓有一条这样的断言活了很久（`不记Undo时撤销栈无此步`），
本轮顺带修掉。可观测的判据是：**先记一步已知可撤销的**（撤销栈因此非空、行为确定），
再来一步不记的，撤一次看收回的是哪一步。

### 分类也要核对（第三次）

第五轮记过「分层本身会错」。本轮又验证了一次，只是方向相反：
`[TypeDrawerSettings]` 挂在 L3 下，本轮**没有做**——因为核过签名之后发现它要的不是反射后端，
而是一整套 `System.Type` 的绘制（`TypeDrawerSettings(BaseType = typeof(...), Filter = TypeInclusionFilter.IncludeAll)`，
靠一个名为 `TypeInclusionFilter` 的枚举 + 类型选择器）。它**借** `[ShowInInspector]` 的样例出场，
但那只是示范场所，不是依赖。L3 因此在这一轮**没有全部清完**，而这是核对出来的，不是漏掉的。

---

## 十、第七批签名核对（2026-10-04）——预制体上下文一族

补 L2 剩余里的预制体上下文族之前，把 6 个特性 + 1 个支撑枚举的 **Odin 官方签名**逐条抄了下来。
取法同前：**`Invoke-WebRequest` 抓原始 HTML，再抽 `<pre><code class="lang-csharp hljs">` 代码块**，
别用 `WebFetch`（它会被侧边栏挤到截断）。这一批**一次就中**，没有新的取法坑。

**这一批的直接意义：** `OdinGap.md` 把这一族记在「签名未核——猜一个形状写下去比不做更糟」下，
本批把那个前提消掉了。同时它也是**第一次**出现「同一块基础设施解锁两个 ⛔ 特性」的情形
——`[RequiredIn]` `[DisallowModificationsIn]` 当初判 ⛔ 的理由写着「共用同一块尚不存在的基础设施，
成对推迟」，那块基础设施就是这一族的 `PrefabKind` + 种类探测。

### 逐条：官方签名 → 本轮实现

| 特性 | 官方签名（逐字） | 本轮实现 | 不实现的部分与理由 |
|---|---|---|---|
| `[ShowIn]` `[HideIn]` `[EnableIn]` `[DisableIn]` | `public XxxInAttribute(PrefabKind prefabKind)`；属性 `public PrefabKind PrefabKind` | 全部 | 无参数可收窄。`[EnableIn]`/`[DisableIn]` 还带 `[DontApplyToListElements]`——那是 Odin 列表元素特性体系的标记，本包没有对应物，不做 |
| `[RequiredIn]` | `public RequiredInAttribute(PrefabKind kind)`；`public string ErrorMessage`；`AllowMultiple = false, Inherited = true` | 全部 | `ErrorMessage` 在 Odin 支持它的表达式语法，本包**只做纯文本** |
| `[DisallowModificationsIn]` | `public DisallowModificationsInAttribute(PrefabKind kind)`；属性 `public PrefabKind PrefabKind` | 全部 | 无 |
| `PrefabKind` | `[Flags] public enum PrefabKind` | 10 个成员的名字与语义照抄，**数值自定** | 见下 |

官方那 10 个成员（按文档站的字母序）：`All`、`InstanceInPrefab`、`InstanceInScene`、`None`、
`NonPrefabInstance`、`PrefabAsset`、`PrefabInstance`、`PrefabInstanceAndNonPrefabInstance`、
`Regular`、`Variant`。**与 §五 早已记下的「`[Flags]`，10 成员」一致**——那次核对只记了成员数与
标志位，这次把名字与逐条说明抄全了。

### 跨特性共性

1. 六个都在 `Sirenix.OdinInspector`（程序集 `Sirenix.OdinInspector.Attributes`），都带
   `[Conditional("UNITY_EDITOR")]`，`AttributeUsage` 都是 `AttributeTargets.All`。
   本包照旧**刻意收窄**，也照旧**不带** `[Conditional]`。
2. **`AttributeUsage` 收窄分两档**：四个条件收窄到 `Field | Property | Method`——它们对按钮
   节点**确实生效**（`[Button, DisableIn(PrefabKind.PrefabAsset)]` 是常见用法），
   与那 11 个条件特性同一条判据；两个校验只到 `Field | Property`——判空要 `SerializedProperty`、
   「已改过」也要，照 `[Required]` 的既有收窄。放宽的只该是真正会生效的那一侧。
3. **官方枚举里的模型预制体缺口。** `PrefabKind` 没有和 `PrefabAssetType.Model` 对应的成员，
   而模型预制体确确实实是一种预制体资产。本包**把 Model 归入 `Regular`**（见下），
   不发明第 11 个成员。

### 本包自定的值

| 值 | 取什么 | 落在哪 |
|---|---|---|
| 十个成员的数值 | 5 个具体位（`1<<0`…`1<<4`）+ 4 个复合成员（具体位的并集）+ `None = 0` | `PrefabKind.cs`（注释里写明本包自定） |
| 模型预制体 | 归 `Regular` | `PrefabKindResolver.KindOf` |
| 非 Unity 对象与非预制体资产 | 解析为 `None`（没有上下文） | 同上 |
| 多选语义 | 全部目标都匹配才算匹配 | `PrefabContextProbe.MatchesAll` |

**为什么数值要是一套干净的位分解：** 匹配算法就是求交集。解析出恰好一个具体位，
与特性给的位求 `&`，非空即匹配——于是 `PrefabInstance` / `PrefabAsset` /
`PrefabInstanceAndNonPrefabInstance` / `All` 这些复合成员天然可用，不需要任何特判。

### 判定阶梯（次序本身是设计）

```
0. 不是 GameObject/组件（POCO、SO、材质…）                        → None
A. 预制体隔离编辑模式（必须最先判：那里 GetPrefabAssetType 不可靠）
   A1 内容里的嵌套实例                                            → InstanceInPrefab
   A2 其余（内容根与普通子物体）：种类取自 stage.assetPath 的资产  → Regular / Variant
B. 属于预制体实例
   B1 在资产内部：变体资产自身 → Variant；其余 → InstanceInPrefab
   B2 在场景里：嵌套实例（最近根 ≠ 最外层根）→ InstanceInPrefab；其余 → InstanceInScene
C. 不属于实例的预制体资产（按 GetPrefabAssetType）                  → Regular / Variant / None
D. 场景里的非预制体对象                                            → NonPrefabInstance
E. 其余（预览场景里的非 stage 对象等）                             → None
```

**核对时纠了两处想当然：**

- `PrefabInstanceStatus` 在 6000.x **只有 3 个取值**（`NotAPrefab` / `Connected` / `MissingAsset`）
  ——旧预制体时代的 `Disconnected` 早已不在 API 里。
- **隔离编辑模式里 `GetPrefabAssetType` 不可靠**（常规预制体的内容根会报 `NotAPrefab`，
  变体的内容根报 `Regular`）。故 stage 那一支**绕道 `stage.assetPath` 加载资产**再判种类，
  不依赖那个行为。这也是阶梯次序的由来：stage 必须最先判，否则变体的内容根会被判成实例。

**两处不确定处，一律往「不会误判成资产」的一侧兜底：** 缺资产实例的两个实例根查询行为未实证
→ 落 `InstanceInScene`；「加进实例但未应用」的对象（官方只保证实例根查询返回 `null`）
→ 落 `NonPrefabInstance`。

### 一处刻意的语义差异与三条收窄

- **多选取「全部匹配」**。既有条件族（`[ShowIf]`/`[DisableIf]`/内嵌三兄弟）取的是
  「首个存活目标」——两者口径不同，且**不是疏漏**：成员**值**读一个目标就够，
  预制体**上下文**是整个选择的性质，混选一个资产与一个场景对象时说「这个字段可见」
  对其中一半目标是错的。
- **`[RequiredIn].ErrorMessage` 只做纯文本**（Odin 支持表达式）。
- **`[DisallowModificationsIn]` 的「已经改过」判据是 `SerializedProperty.prefabOverride`**，
  因此多选时不报（多选下它反映的是谁官方未写明），反射成员也不报（没有值入口，
  绘制器会说明一句）。**只读那一半照常生效。**
- **同一成员上多个条件仍是后装入者覆盖**（`PropertyState` 只有一个求值器槽）。
  这是既有事实，本轮把它写进了 README 与 Roadmap 的未决项，没有引入组合机制。

### 落地时的两处既有痕迹

- §五 的「本轮不实现」表里 `[RequiredIn]` `[DisallowModificationsIn]` 一行、以及
  「需要自建的类型」里 `PrefabKind` 那句「**本轮不做**」，都已被本节翻案。
- §二「已否决的形状」里那条以「**具体签名没从 Odin 官网核对到，就不写**」为由不做的记录，
  理由已不成立（签名在本节）。

---

## 十一、审计记忆

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
