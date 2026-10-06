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

> **2026-10-05（L4）复核：仍然推迟。** `[InlineProperty]` 走的是**观感派**——只把子字段
> 摊平画出来（不画折叠头），子字段仍由原生绘制、不进本包管线；展开子节点的基建一条没动。
> 本轮把这里当成了**对照**：正因为不展开，才敢承诺「加了这个特性之后嵌套字段的行为一个字
> 没变」。要让特性作用于嵌套类型内部，仍然得走本条记的那条路（见 §三 的未决项）。
>
> **2026-10-05（L6 第一批）再复核：仍然推迟，且现在有了正式的名字——「元素节点化」。**
> 集合容器与表格落地了，但元素仍由原生 `PropertyField` 逐个画。要让**集合元素**（而不是
> 嵌套类型的成员）变成真节点，同样落在这条上，且多一层难点：**数组长度随时可变**，
> 与「树的形状在构建结束后冻结」正面冲突（理由与代价见 §十三）。
>
> **2026-10-06（第十二批）三复核：这条路的「固定形状那一半」已经走完。**
> 嵌套类型的成员节点化（§十四）与嵌套层的分组装配（§十五）相继落地；本条仍未动的是
> **集合元素**那一半——它的两条难点（动态形状、元素路径身份不稳定）原样成立。
> 注意「嵌套类型的成员」与「集合元素」是这条推迟项下的两件事，别把前者的完成读成后者。
>
> **2026-10-06（第十五批）四复核：两半都走完了，本条结案。**
> 集合元素节点化落地（§十八）——`ExpandableCompositeDrawer` 这条推迟项到此没有剩余。
> **动态形状没走「动态节点」那条路**：元素层是 `arraySize` 的同步投影，绘制前对账、
> 对不上整层重建，于是「路径身份不稳定」这个问题**不存在**（不是被解决，是没让它发生）。
> 代价是新增一条契约：元素节点不跨结构变更。

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
| ~~嵌套 `[Serializable]` 类型里的反射成员~~ | ✅ **已结案（2026-10-06，§十六）**：读路径落地，`[ShowInInspector]`、条件族反射两级、`[Button]` 一族与按名回调族同时在嵌套层生效。当初那句「它一旦有了会同时受益」兑现了 |
| ~~嵌套类型**整个进管线**（含 `[InlineProperty]` 的完全体）~~ | ✅ **已结案**（2026-10-06）：§二 第 7 条那条路走了两批——§十四 节点化（建树期展开子成员、换末端、路径与成员过滤的嵌套语义）、§十五 分组装配（嵌套层的路径前缀与装配落点）。仍未动的只剩**数组子字段**的处置，那属 L6 的元素节点化 |
| `[ToggleGroup]` 一族在反射树上的解析 | 它们按序列化路径找「开关字段」，POCO 树上找不到。现状是告警失效；要与条件族那样三级解析，得先把「按名找成员」这件事整个收成一层 |
| 反射成员的值每帧读一次 | 只读展示每帧现读是刻意的（缓存会「该变不变」），但用户 getter 有副作用或开销时没有退路。要不要给一个「手动刷新」的开关，等真有抱怨再说 |
| 折叠状态的持久化落点 | `EditorPrefs`（跨项目共享）、`SessionState`（不跨会话）、序列化进场景（污染资产）三者各有问题。等 `[FoldoutGroup]` 来了再定 |
| `[OnValueChanged]` 的触发时机 | 判断「值变了」要每帧比对旧值，旧值该放 `PropertyState`；但触发时机（绘制前后？`Update` 前后？）未定 |
| 数组/集合展开的边界 | **部分落地（2026-10-05，L6 第一批）**：容器（行、增删、表格）已做；**元素节点化未做**——元素仍由原生 `PropertyField` 逐个画，元素级特性照旧不生效。要兑现「让特性作用于元素」得先做元素节点化（树的形状不再冻结、元素路径身份不稳定），是独立一轮的量级。**2026-10-06 追记（§十七）**：`[OnCollectionChanged]` 与 `[Searchable]` 两条**绕开了**元素节点化（前者落在既有的增删施加点上，后者过滤的是行与节点），故 L6 剩下的缺口里只有元素节点化仍卡在这条上 |
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

## 十一、第八批核对与收尾（2026-10-05）——L2 收尾之二：分组条件族

**特性**：`[ShowIfGroup]` / `[HideIfGroup]`（2 个）。家底 75 → 77，缺口 21 → 19，
**L2 整层清完**。这是 OdinGap 推荐顺序的第 7 条（「L2 剩下的两族」），本轮把两族各自收口：
一族落地，一族判不做。

### 这两个特性要什么

条件挂在**分组节点**上——条件为假时整组连同子成员一起消失（不是逐个成员各判一次）。
官方语义里**组路径的末段兼作条件成员名**（`[ShowIfGroup("Box/Toggle")]` 判的是成员
`Toggle`），可用 `Condition` 覆盖。签名在此前的核对里记过，但**「末段兼条件名」这条用法
是本轮对着官方页面的样例核的**——只核构造与属性名会漏掉它。

### 路线决定：加「分组装配之后」的第二趟处理器

它们此前卡在一个结构问题上：判据要挂在分组节点上，而分组节点由分组装配创建，
**构建期没有「分组装配之后」那个阶段**（`Editor/README` 早把这个口子记下，并写着
「若将来出现**第二个**同类需求，再考虑给构建期加一个后置阶段」——本轮就是那个第二个）。两条路：

| 路 | 代价 |
|---|---|
| **加第二趟处理器**（本轮选的） | 顺序契约多一条：第二趟在分组装配之后跑，**只对分组节点、只有自身钩子、只许改 `PropertyState`**（链条此时已挂好，注入特性需像 `MergeGroupAttribute` 那样重挂链，第二趟不提供这条路） |
| 沿用 `[ToggleGroup]` 的绘制期解析 | 与「解析在构建期、求值在绘制期」的分界相抵；解析失败的告警推迟到首次绘制（可能永不出现）；可见性不可无头断言 |

选前者是因为它保住了那条分界：分组节点的 `IsVisible` 自建树起就正确、告警时机与条件族一致、
可以无头测试。**`[ToggleGroup]` 不迁移**——它的门必须让开关控件与框保持可见，
整节点隐藏会把开关自己也藏掉，「画者自己决定调不调下一个」本来就是绘制期的事。

**路由判据是推导出来的，不是开关**：处理器的 `HandledAttributeType` 派生自
`PropertyGroupAttribute` 即走第二趟。理由是同义反复——分组特性只存在于分组节点上，
而分组节点到第一趟时还不存在。做成显式开关的话，漏开关的症状是「静默地什么都不做」。
**现存的泛型处理器无一命中这条判据**，而 `ClassLevelGroupProcessor` 是非泛型的，
稳定留在第一趟（它注入的正是分组装配要看到的东西）。顺手修正了 `PropertyTree` 构造注释里
「挂进来的节点不再跑处理器」一句——本轮起不再为真。

### 落地时撞上的四个坑（都不是分组装配本身的缺陷）

1. **`CloneForPath` 同时服务两处，对条件的要求恰好相反。** 合成祖先时它表达「祖先继承
   后代的呈现设定」，条件**不该**继承——`[ShowIfGroup("Box/Toggle")]` 会同时造出 `Box` 与
   `Box/Toggle` 两个节点，祖先挂了条件会把同祖先下的兄弟分组一起藏掉；类级分组把路径加
   前缀时它表达「这条声明换了路径」，条件**该**跟着走。修法是让特性自己带上**声明路径**，
   用「目标路径是不是当前路径的祖先」反推调用者——两个调用方的意图都还原得出来。
2. **`Combine` 的「先声明者优先」在这里会压掉条件。** 节点可能先由更深声明的祖先克隆创建
   （那份不生效），后由真正声明在该路径上的成员归并——先到者恰恰是没条件的那份。
   覆写为「真正声明在本路径上的那份赢」（两份都生效、或都不生效时，仍取先到者）。
3. **泛型处理器基类的 `CanProcessSelfAttributes` 是 `sealed` 的**，判据是「节点带该特性」。
   而类级声明在成员**全部**自带分组时整份到不了节点（`ClassLevelGroupProcessor` 只改写
   自有分组的路径前缀），那种情形恰恰要照样生效。于是处理器改继承**非泛型**
   `AttributeProcessor`，自己判「只看分组节点」，并加一条**根上回退**——按**具体类型**匹配，
   免得多类型并存时 A 的条件被 B 的处理器取走。
4. **组名与某个成员名相同时，树里会出现两个同路径的节点。** 本包以 `Path` 当节点身份
   （`InspectorProperty.Path` 的注释里写着它「同时充当身份标识，因此树内唯一」），
   而官方那种「组名兼条件名」的写法恰好会撞上它。
   已在包 README 的已知限制里写明处置（用 `Condition` 显式指定、或让组名与成员名不同），
   展示台与测试夹具也都避开了重名。

### 三处与官方的偏差

- **不画任何东西**：纯条件载体，单独用时分组节点链上只有末端；想要框就用同一个路径再配一个
  `[BoxGroup]`（官方样例全是这么配的）。官方页面对渲染只有一张截图，**兜底取的是
  「配错时失败得软」**：若官方其实默认画框，代价是单独用时少个框；反过来若我们画了框而
  官方没画，官方样例的每个配对都会**双框**。
- **不做 `Value` 值比较**（本包 `[ShowIf]` 也没有，两边一致地收窄）、**不做 `Animate`**
  （没有动画系统）、**不做 `CombineValuesWith`**（同路径多次声明走既有 `Combine`：
  先声明者优先）。
- `AttributeUsage` 收窄为 `Field | Property | Class`（与分组族一致，方法上不生效）。

### `"@other.field"` 的正式结论：不做

- `"@this.*"` 那一半**已经被覆盖**——条件名本来就可以是 `a/b` 这样的嵌套路径
  （`SerializedProperty.FindProperty` 支持相对路径，`ShowIfAttribute` 的 XML doc 也这么写着）。
- 真正剩下的是「借成员持有的对象实例去读它的字段」：本包没有那条读路径（值后端是
  `SerializedObject`，反射后端只读、且只对树自己的目标），多对象语义也未定
  （取哪个目标的引用？引用为空时算不算满足？）。
- 归档为 **⛔**，不再挂在「缺」里——「缺」意味着「做得了、只是还没做」。

### 落地时的既有痕迹

- `OdinGap` 的分层表 L2 行与总账、`Roadmap` §二、`CLAUDE.md`「明确不在本轮范围」段、
  `Editor/README` 的顺序契约与 `[ToggleGroup]` 例外说明，本轮都跟着改了。
- 包 README 的特性计数此前停在 69（同包 `Documentation/index.md` 记的 75 才是对的），
  本轮连同新增的两个一起修正为 **77**。

---

## 十二、第九批核对与收尾（2026-10-05）——L4：顺序与内联

**特性**：`[PropertyOrder]` 与 `[InlineProperty]`（2 个）。家底 77 → 79，缺口 19 → 17，
**L4 整层清完**。两条签名早在 §七末 记过，本轮照着 OdinGap 记的两处落点落地。

### `[PropertyOrder]`：一次稳定排序 + 一个判据

- 落点与 OdinGap 记的一致：**三段成员收集之后、分组装配之前**，对 `members` 做一次稳定插入排序
  （照 `SortGroupNodesAtLevel` 的手法；`List.Sort` 不稳定，这正是两处都不用它的理由）。
- **`0` 是合法排序值**，不是「未指定」——与分组 `Order` 的「最小非零」刻意相反：分组的规则是
  为了不与声明顺序耦合，而成员排序本就以声明顺序为基准。
- **放宽到方法**（判据同条件族：方法会产生节点，排序对它们同样生效）：于是「按钮一律排在字段
  之后」变成「**默认**排在之后」，给方法一个负顺序即可插到字段之间。三处旧表述（包 README、
  `ButtonAttribute` 的注释、`MethodNodeTests` 的用例名与注释）同步校正——**结论过期了就要改，
  不能只在新文档里说新话**。
- **判据的第三种漏法**：它既没有绘制器也没有处理器，只标它的类型会被自动接管判据漏掉，
  排序**静默失效**。这是「条件族漏过『处理器』、`[Button]` 漏过方法、`[ShowInInspector]` 漏过
  属性」之后的同类坑再次显形。对策仍是标记接口：新增 `ITreeOrderingAttribute`，与
  `ITreeLifecycleAttribute`（不产生节点）、`ITreeMembershipAttribute`（产生节点但不画）并列，
  `IsUsedBy` 加一条 `is` 判定 + 一条「只挂排序特性的类型为真」的用例。

### `[InlineProperty]`：观感派，与 §二第 7 条的关系

官方语义是「把类型的内容画在标签旁，而不是折进 foldout」。两种形态：

| 形态 | 代价 |
|---|---|
| **观感派**（本轮选的） | 只改绘制：子字段由原生 `PropertyField` 逐个画、摊平在本层。零建树改动；**子字段不进管线**（它们身上的本包特性照旧不生效） |
| 完全体（建树期展开） | 要复活 §二第 7 条的可展开复合绘制器：建树期展开子成员、换末端、路径与成员过滤的嵌套语义、数组子字段的处置（L6）。子字段从此能带本包特性 |

选观感派的理由：它是「不画折叠头」这半个缺口的完整答案，而且**承诺得起**——不展开就没有
「嵌套字段行为悄悄变了」的风险。完全体的触发条件与 §二第 7 条写的一样，仍未触发。

- **类级形态的判据落在字段的声明类型上**（`field.FieldType`），不是被检视类型——本包此前没有
  任何读「字段类型特性」的代码。走非泛型处理器的「父级注入」钩子（照类级分组分发的先例），
  注入的是**每字段一份新实例**（类型上那一份会被该类型的所有字段共享）。
- **接管判据补一处收窄的扫描**：只认字段声明类型上的 `[InlineProperty]`。不泛化成「字段类型上
  有任何本包特性」——那些特性在嵌套层是惰性的，算进来是**过度接管**（与漏接管方向相反、
  同属静默）。两条控制项测试钉着（数组元素类型不算、嵌套类型上的其它特性不算）。
- **降级必告警**：没有序列化后端 / 数组与列表 / 没有可见子字段，三种情形退回末端并各报一次
  （`DrawerWarnings.Once`），不静默。判定抽成 `InlinePropertyLayout.CanInline` 纯函数，可无头测试。
- **画法（兜底必须选、选完写下来）**：官方只有一句「contents next to the label」，本包定为
  「父标签照常画在标签列（`LabelWidth > 0` 时临时覆盖、画完还原），子字段缩进一级逐个画在下面」。
  两条依据：其一，官方样例把 `[HorizontalGroup]` 标在结构体字段上才得到单行效果——若内联本身
  就是单行，那个 Group 就是多余的；其二，竖直堆叠对任意字段数的类型都成立（单行会被挤爆）。
  若官方实为单行，代价只是观感差异，修一个方法即可。

### 落地时的两处细节

- **与同档替换型绘制器（`[DisplayAsString]` 等）并存**：谁生效由**声明先后**决定（同一格不会两条都跑）。
- **向量与内联**：`Vector2Int` 这类原生就有 x/y 子属性的类型走同一条路（`hasVisibleChildren`），
  测试里有一条断言钉着这条假设——若不成立，收窄到「只内联 `[Serializable]` 自定义类型」即可。

---

## 十三、第十批核对与收尾（2026-10-05）——L6 第一批：集合容器与表格

**特性**：`[ListDrawerSettings]` `[TableList]` `[TableColumnWidth]` `[HideInTables]`
`[RequiredListLength]`（5 个）。家底 79 → 84，缺口 17 → 12。

### 动因对账：自绘的是**容器**，不是元素

L6 的立项理由一直写着「自己做展开的**唯一理由**是让本包的特性作用于元素」。真做时发现：
自绘**容器**（行、增删按钮、索引标签、表格列）**不带来任何元素级特性**——那要**元素节点化**，
是另一件事。本轮如实把动因改述成「容器行为 + 表格呈现」，并把元素节点化写进 §二「已否决的形状」
（见下）。**一份过期的动因比没有动因更误导**：不改的话，下一轮会读成「L6 做了，元素特性怎么
还不生效」。

### 一条绘制器拥有集合绘制，`[TableList]` 靠处理器接入

- `ListDrawerSettingsDrawer`（值绘制带、替换型）：有 Unity 后端且是真的数组/List 时自绘，
  否则告警一次 + 退回末端。**不建元素节点**——树的形状在构建结束后不可变（
  `InspectorProperty.RawChildren` 的契约），而数组长度随时可变。
- **`TableListProcessor` 必须继承泛型基类**，这是本轮最容易踩死的地方：自动接管的判据只认
  「`HandledAttributeType` 不为 null」的处理器，非泛型处理器的那个属性恒为 `null`。
  注入的先例（类级 `[InlineProperty]`）能是非泛型，是因为判据里为它开了特判——这里**不照抄**，
  改为泛型，并配两条判据用例钉着（第七条经验的直接应用）。
- 处理器同时负责**注入一份新的 `[ListDrawerSettings]`**：官方样例显示 `[TableList]` 是单独用在
  字段上的，而集合绘制由后者那一格绘制器拥有；不注入就没人画（静默失效）。
- 列模型在**构建期**建（反射只在这里发生）：元素的可序列化字段、基类在前、层内按元数据令牌，
  有元素时再按第一个元素的实际序列化顺序校一遍；列宽与隐藏来自 `[TableColumnWidth]` /
  `[HideInTables]`。单元格取 `FindPropertyRelative`，取不到或遇到复合类型**画占位并告警**，
  不静默藏数据。

### 三处 Unity 语义：照官方对齐，不发明规则

1. **新增元素复用上一个元素的值**——官方手册明写（「the Unity Editor reuses the values of the
   previous element」），原生「+」亦然。Odin 有一个「不复制」的旋钮，本包不做：要它就得给任意
   元素类型造一台默认值写入器（`boxedValue` 对复合类型不通用），代价大于收益。
   **第一版实现按「新槽位是默认值」写了测试，被实测打红**——这正是「实测优先于推理」的又一例。
2. **删除只删一次**。老文档里「引用类型要删两次」的兼容写法在 Unity 2021.2 之后已不适用
   （本包下限 6000.3），照抄会**多删一个元素**。删不掉（长度不可变的数组）时返回 `false`
   由调用方告警，**不猜着再删**：多删是静默改数据。
3. **结构性修改攒到趟末统一施加**：循环里改会让同一个 Layout 与随后的事件看到不同的行数
   （症状是 GUILayout 的「control 位置」异常）。

### 另外两条纪律

- **多选且各目标的列表不一致时禁止增删**——`arraySize` 的改动会把主目标的整份列表铺到所有
  目标上，那是静默改数据；长度校验同样跳过（读到的不是任何目标的真值）。
- **行画法不用水平布局**（复合元素展开时子字段会被水平组挤成半宽——`[SuffixLabel]` 那里踩过），
  改用 `GetControlRect` 切矩形；行尾按钮单独切一块。**判据要求 `Generic && hasVisibleChildren`**
  ——只看后者会把 `Vector3` 画成「折叠头 + x/y/z 三行」，与原生不一致。

### 已否决的形状（追加两条）

- **元素节点化**（本轮**没有**做）：让嵌套/集合的元素成为真正的 `InspectorProperty` 节点。
  它是「让特性作用于元素」的唯一路径，但**冻结的树形是既有地基**（`RawChildren` 的契约、
  `Kind.Member` 的「路径可交给序列化系统」不变量、`PropertyTreeReset` 的按路径重置），
  而数组长度随时可变——元素路径（`items.Array.data[0]`）在重排/删除后**身份不稳定**。
  要做必须先答「节点与真实元素数不一致时怎么办」（重建树？动态节点？），是独立一轮的量级。
  **触发条件**：真有「元素要带条件/特性」的需求时。
  > **2026-10-06（第十五批）已做**——答案见 §十八：**让不一致不存在**（同步投影 + 整层重建），
  > 于是「动态节点」与「重建树」两条候选都没有被采纳，冻结的树形也只在两个时刻松动。
- **单列表格**（`[TableList]` 遇到非复合元素类型）：本轮选择**降级 + 告警**，不做单列表格——
  官方对那种形态没有可核实的描述，发明它只会多一条无人验证的规则。

### 落地时的既有痕迹

- `OdinGap` 的 L6 节改述了动因并标注「容器已做、元素节点化未做」；`Roadmap` §五把过期的那句
  动因对账了一遍；`CLAUDE.md` 的「明确不在本轮范围」把「数组/列表展开」换成「元素节点化」。
- **新增第八条经验**（见 OdinGap）：动因要逐轮对账。

---

## 十四、第十一批：嵌套类型成员节点化（2026-10-06）

**做了什么**：`[Serializable]` 嵌套类型的成员**按需**成为真正的属性树节点——README 里
「本包的特性作用不到嵌套字段上」那句第一次被兑现。顺手把 `[InlineProperty]` 升到**完全体**
（它的子字段从「自己迭代 `SerializedProperty` 画」变成真节点）。**特性计数 +0**：这是一轮
**能力轮**。

### 范围与安全阀

- **按需展开**：只给「嵌套成员里有本包支持的特性」或「字段/类型标了 `[InlineProperty]`」的
  复合成员展开，其余整份照旧交给 `UnityFallbackDrawer`。**已有 599 条用例一条没红**——
  「没用到本包的类型外观不变」这条契约因此是**被证明**的，不是被声称的。
- **只做固定形状那一半**：集合**元素**不节点化（个数随时可变，与「树的形状冻结」正面冲突，
  且元素路径 `items.Array.data[i]` 在重排/删除后身份不稳定）；`[SerializeReference]` 不展开。
- **嵌套层的分组装配没做**：它要动 `ApplyGrouping` 的路径前缀（节点路径要带父前缀，否则
  同一个嵌套类型用在两处时 `Path` 不再唯一）与「已挂载成员的重排」，是下一轮的**第一项**。
  本轮用一条**构建期告警**兜住——标了却不生效的 `[BoxGroup]` 不许静默。

### 五处必修（都是探索与设计复核逮出来的，其中两处会静默失效）

1. **`Owner` 回填要递归**：嵌套子节点是收集期挂到父节点上的，那时父节点的 `Owner` 还是
   `null`，`AddChild` 传播不过去；`Build` 里那段只覆盖顶层。漏了的话**嵌套层的条件与
   序列化对象解析全部落空**——而树看起来完全正常。
2. **挂链要递归**：子节点不在顶层 `members` 列表里（那列表只服务顶层装配），漏了它们的链，
   绘制时直接抛「链为空」。这条是**测试先红的**（断言末端时 NRE）。
3. **嵌套层的条件停在序列化成员**：反射那两级是在**被检视对象**上找成员——嵌套层里回落过去
   会拿到根上的同名成员，**「条件看错了对象」，静默且极难归因**。现在改为给一句专门的告警
   （与「嵌套层的 `[Button]`/`[ShowInInspector]` 不生效」同源：都缺一条嵌套实例的读路径）。
4. **条件解析给嵌套层加「先同级、后根」**：嵌套层里写 `[ShowIf("flag")]` 指同层的 `flag`；
   顶层成员的父节点是根而不是成员，**两者恒等 → 对既有行为零变化**。
5. **成员过滤器只作用于顶层**：`WindowMemberFilter` 的判据是「字段的声明类型可赋给窗口基类」，
   套到嵌套层会把整层**静默滤掉**（窗口里那些节点根本不出现，零告警）。

### 另两条纪律

- **`Kind` 不新开**：嵌套成员的路径 `nested.field` 是有效序列化路径，`Kind.Member` 的
  「路径可交给序列化系统」不变量成立（`Method`/`ReflectedMember` 单列的理由在这里不沾边）。
  末端改按**节点形状**二分（有子节点 → 复合末端），链在挂链期装配、子节点在收集期已就位。
- **末端要自己接三件 Unity 白送的事**：父级的只读罩（`[ReadOnly]` 在复合字段上照旧整块变灰）、
  子节点缩进一级、以及「谁画标签」（`[InlineProperty]` 的节点由构建期定案
  `FoldoutSuppressed`，绘制期只读）。

### 已否决 / 未决

- **元素节点化**（集合元素）：仍不做，理由与 §十三 记的相同（动态形状 + 路径身份不稳定）。
- ~~**嵌套层的分组装配**：下一轮第一项。本轮以告警兜底。~~
  ✅ **已做**（2026-10-06，§十五）——同时撤掉了那条告警兜底。
- **嵌套层的类级特性**（除 `[InlineProperty]`）：不生效——没有地方收集它们，不为此开半扇门。
  **判据也照此收窄**（类级特性不触发展开），并有用例钉着。

---

## 十五、第十二批：嵌套层的分组装配（2026-10-06）

**做了什么**：嵌套 `[Serializable]` 类型里的分组特性**第一次生效**——上一轮「嵌套类型成员
节点化」明确推迟的那一半（§十四 末、[Roadmap §五](../Roadmap.md) 记的「下一轮第一项」）。
特性计数 **+0**，又一轮**能力轮**。上一轮那条构建期告警兜底（`WarnAboutInertGroups`）撤除，
换成一条更窄的（嵌套类型的**类级**分组特性仍是死信）。

### 路径方案：前缀即身份

- **分组节点的路径 = «父字段的序列化路径» / «组内路径»**（`stats/外框`），前缀**一并写进
  `GroupID`**——走 `CloneForPath`，与类级分组的分发同一套写法。
- **被否决的方案**：`GroupID` 保持用户写的原样、只给节点 `Path` 加前缀。它更省事，但会破掉
  「分组节点恒有 `GroupID == node.Path`」这条不变量——审计手册把它标成承重（「破了这条，
  绘制器就无从判断自己在画哪一层」），而 `TabGroupAttribute` 恰恰就是靠 `GroupID` 判自己在
  画容器还是页。要按那条路走，还得在 Runtime 给 `PropertyGroupAttribute` 开一个
  「不改 `GroupID` 的克隆」，那是公开面新增，只换来「`GroupID` 保持用户原样」这点收益。
- **前缀恰好一段**：成员序列化路径**永不含 `/`**（字段名不可能含，数组是 `items.Array.data[0]`），
  故整条父路径是一个**不透明段**。`EnsureGroupChain` 只跳过前 `prefixSegments` 段——
  取 `DepthOf(container.Path)` 而**不写死 1**：按段数表达，将来前缀里真出现 `/` 也不会塌。
- **副产品（有守卫用例）**：前缀让嵌套分组节点与根上的类级条件声明**永不误配**
  （`GroupConditionSource` 的第二来源判据是 `rootAttr.GroupID == property.Path`）。
  这不是当初设计前缀的主要理由，但确实是它买下的一条。

### 装配落点与「已挂载成员的重排」

- **落在建树末尾**（`ApplyGrouping` 内部：先顶层、再整树深度优先装嵌套层、最后一次全树排序）。
  **不能提前到收集期**：第一趟处理器会给成员注入特性（类级分组就是这么分发到成员上的），
  装配看不见它们就等于那个特性像没写一样——这条顺序契约（CLAUDE.md 架构要点第 7 条）
  在嵌套层同样成立，而 `RunNestedProcessors` 确认嵌套层的两个钩子本来就都在跑。
- **重排 = 快照 → 清空 → 按原顺序重挂**。`AddChild` 只追加、不从旧父节点摘除，不清空就会留下
  重复；重挂依据的是 `[PropertyOrder]` **已排好序**的那份快照，于是「分组落在其首个成员出现的
  位置」这条语义在嵌套层**逐字继承**，不必重新定义。
- **无分组特性的一层早退**（不 clear、不重挂）：「没用到本包的类型外观不变」这条契约因此
  不只靠推理，还靠一条分支。
- **`SortGroupNodesAtLevel` 的递归放宽到「无条件进所有子节点」**：嵌套层的分组挂在**成员节点**
  之下，只认 `Kind == Group` 会让它们永远轮不到 `Order` 排。判据少一条，代价只是构建期多走
  一遍树（没有分组的层是空操作）。
- **`RunGroupProcessors` 一行没改**：它本来就是整树遍历，嵌套分组节点自动进第二趟——
  `[ShowIfGroup]` / `[HideIfGroup]` 随之生效。

### 两处既有洞，先修再做

本仓的老规矩（「说『纯加法』之前先确认既有基座没有洞」）又适用了一次，两处都是核出来的：

1. **`TabGroupAttribute.IsContainer` 只能扛住零前缀。** 判据原是 `GroupID == TabsGroupID`，
   而 `TabsGroupID` 是构造期算出的常量、`CloneForPath` 只改 `GroupID`。**类级分组 + `[TabGroup]`
   今天就已经中招**（`ClassLevelGroupProcessor` 会给路径加前缀），症状是页签栏整个不画、
   各页内容顺次摊开——静默的视觉故障，不丢数据，且**当时无任何测试覆盖**。
   先写红的 Runtime 用例坐实（`CloneForPath("外层/设置")` 判成页），再改成「路径以组名**按段**
   收尾 **且** 不以声明页路径收尾」——第二个条件兜住 `[TabGroup("T", "T")]` 这种自同名。
   新增 `internal string DeclaredPagePath`，不新增公开成员。
2. **`SerializedMemberResolver` 的 `Scope.Object` 在嵌套层按根解析。** 条件族当初专门加过
   「先同级、后根」，而 `[ToggleGroup]` 的开关名、`[ValueDropdown]` 的数据源、
   `[MinMaxSlider]` 的边界名没有——嵌套层里写 `[ValueDropdown("options")]` 会**静默地**
   绑定到根上的同名成员（取的是另一个对象的值）。后两者自上一轮起就已能在嵌套层跑，
   也就是说这是上一轮带进来的洞。统一到同一份实现（`SerializedMemberResolver.FindNestedScope`，
   `ConditionResolver` 自己的那份删掉）。
   **它还要沿父链上溯、跳过分组节点**：嵌套分组节点的父节点往往是**另一个分组节点**
   （`stats/组/条件组`），只看直接父节点会返回 null 并静默回落到根上。

两条都是先写红的用例再修；`Fix` 的提交在 `feat` 之前，与「先修基座」一致。

### 与官方的差异 / 明确的边界

- **嵌套类型上的类级分组特性仍不生效**（本包只在被检视的最外层类型上收集类级特性），
  维持上一轮「不为此开半扇门」的决定；但**展开过而带它时会告警**——那是本包的静默。
- **集合元素里的分组特性**照旧不生效（元素不节点化），无告警，写进 README 已知限制。
- **路径可能撞的边角**：根上若有人手写组段带 `.` 的 `[BoxGroup("stats.inner")]`，
  会与嵌套组 `stats.inner/G` 撞路径字符串。与既有那条「组名与成员名重名会撞」同级，
  出口也一样（改个名字）；本轮**不加运行期查重**——那是只报一行日志的新机制，收益有限。

---

## 十六、第十三批：嵌套层的读路径（2026-10-06）

**做了什么**：把「拿不到嵌套实例」这条限制在嵌套层彻底消掉——`[ShowInInspector]`、
条件族指向反射成员/方法的两级、`[Button]` 一族、按名回调族全部在嵌套层生效。
**能力轮，特性计数 +0。** 这是 §三「嵌套 `[Serializable]` 类型里的反射成员」那条未决项的结案。

### 技术核心：唯一可行的一条路

**Unity 没有公开 API 从 `SerializedProperty` 拿到嵌套托管实例**——用本机 Unity 6000.4.5f1 的
程序集元数据与官方文档逐条比过：

| 接口 | 结论 |
|---|---|
| `objectReferenceValue` | 只对对象引用有效（嵌套的 `stats` 是 `Generic`） |
| `boxedValue` | 给的是**序列化数据的装箱快照**：读不到非序列化成员（那正是 `[ShowInInspector]` 存在的理由），给不出可调用的活实例，每次还分配一个箱子 |
| `managedReferenceValue` | 只对 `[SerializeReference]`（本包刻意不展开） |

于是唯一可行的是 **`targetObjects` + 按 `propertyPath` 逐段编译式下钻**
（`ReflectedAccessor.TryCreatePath`）：构建期合成一条委托链，绘制期只剩委托调用——
与「反射仅限构建期」那条硬规则同构。

**链每帧现读，不是绑死实例。** 用户把父字段重新赋值（`nested = new …`、Undo、
预制体 revert）之后必须跟着走；绑死的实例会**静默陈旧**。这条纪律对**延迟回调**尤其要紧：
`[CustomContextMenu]` 的菜单还挂在屏幕上时用户就能改字段，所以 `MenuPayload` 里存的是
**访问器**、选中那一刻才现读。

### 一个原语，五处接线

`ReflectedAccessor.TryCreatePath` 是唯一的新东西，其余全是接线。为免「构建 scopes」的代码
长成四份，收了 `NestedInstanceScope` 一处；容器出口统一走
`SerializedMemberResolver.FindNestedScopeNode`——它沿父链**跳过分组节点**。
（直接看 `Parent` 会踩错：嵌套装配分组之后，方法节点的父节点可能是个**分组节点**、
`Type` 为 null。）

### 落地时逮到的既有缺陷（都是先写红的再修）

1. **按名回调会静默调错对象。** `NamedMethodResolver` 取 `Owner.Targets`（根对象）按名找，
   而嵌套字段自上一轮起就进树了——嵌套字段上写 `[OnValueChanged("Bump")]`，根类型恰好有
   同名方法时会被调走：**按钮有反应，只是反应发生在另一个对象上**，比「找不到」难查得多。
2. **路径访问器对 null 中间段会抛。** `stats.inner.flag` 而 `inner` 为 null 时每帧抛
   NullReferenceException——**托管对象与序列化数据不是一回事**（后者 Unity 总会补出实例），
   这条只有真的写过才想得到。改成逐段空传播。

### 判据：这次要补的是**第三道闸**

上一轮记的「判据看不见的展开」这次有两处：

- `HasNonSerializedNodeMember` 要能看见 `[ShowInInspector]` 的字段/属性**与会生成方法节点的
  方法**（本次分两批放：先 `[ShowInInspector]`、后方法——**判据放开而消费者没到位，
  等于造出「展开了却什么都画不出来」的新静默失效**）。顺带补了 `Static`：收集通道与
  自动接管判据都收静态成员，漏了会造出「只放静态按钮的嵌套类型」判据与收集不一致。
- **一道此前没被点名的闸**：`IsCompositeCandidate` 要求 `property.hasVisibleChildren`。
  而「只放一个 `[ShowInInspector]` 属性」的嵌套类型**一个可见的序列化子字段都没有**——
  判据修好了照样进不了门。这条是设计复核时逮到的。
- 顺手把两条判据（`[ShowInInspector]` 与「哪些方法生成节点」）提到 `MemberNodeCriteria`
  共用——本仓在这类判据上已经吃过四次同类亏。

### 边界与差异

- **值类型（struct）实例上的方法调用一律拒绝**，并把原因画出来（`[Button]` 是禁用 + HelpBox、
  `[InlineButton]` 是 Tooltip）。链上会装箱，调用改的是副本——**改动静默丢弃**，
  而「点了没反应且不告警」正是本包最想避免的。
- **嵌套实例为空**时读值显示「—」、方法跳过该目标（与「某个目标上没有这个成员」同款处置）。
- **只读承诺要分开说**：反射**值后端**仍然只读（`SetValue` 恒抛）；但嵌套层的 `[Button]` 一族是
  **方法调用**，会写、Inspector 路径记撤销。README 与特性注释都按这个口径改了。
- **Undo 仍记根 Unity 对象**（嵌套数据本就在它的序列化数据里）。由此有一条硬纪律：
  **绝不把实例数组当 `targets` 传**——那数组不是 `Object[]`，零分配判据会静默失效。

### 遗留

- ~~**嵌套类型的类级特性**仍不生效（只在被检视的最外层类型上收集），展开过而带它时告警。~~
  **2026-10-06（第十八批）已结案**：**分组族**（`PropertyGroupAttribute` 及其条件子类）分发到
  它的成员，两处告警撤除；其余类级特性仍只对被检视的最外层类型生效（不告警，口径写进 README）。
  见 §二十一。
- **集合元素节点化**仍未做——它与本轮无关，仍是「让特性作用于元素」仅剩的那一半。

---

## 十七、第十四批：L6 收尾两条（2026-10-06）

**做了什么**：`[OnCollectionChanged]`（集合在 Inspector 里被增删时回调）与 `[Searchable]`
（按输入过滤子成员与列表行）。**特性计数 84 → 86**，缺口 12 → 10。
两条彼此独立，共同点是**都不需要元素节点化**——它们过滤/响应的是既有的行与节点。

### 决定一：回调由集合绘制器在**施加点前后**触发，不另设绘制器

增删的**唯一落点**是 `ListDrawerSettingsDrawer` 趟末的统一施加（循环里只记意图）。
一格外档绘制器只能在 `CallNextDrawer` **返回之后**动，那时改动早已落地——
「改动前」会名不副实。故两个回调紧挨着写进序列化数据那一步触发，
这正是官方那句 *through the inspector*。

**成对与否的判据是「长度真的变了」**：改动前照触发（它是「记下改动前的样子」的钩子，
多调一次无害），改动没落地时不触发改动后（它是「改动发生了，去做后续」的钩子，多调一次有害）。
顺带补掉一个既有洞：`CollectionMutation.Add` 此前不检查成败，长度不可变数组的「+」是静默失败的。

**代价如实写进文档**：回调触发时目标对象上的**托管集合仍是旧的**——绘制路径只改
`SerializedObject` 的内存副本，落盘由宿主在这一帧绘制之后做，本包没有「落盘之后」的挂点。
窗口工具栏的「重置为默认值」整属性复制，不经过集合绘制器，故不触发。

### 决定二：值不猜 `boxedValue`

`CollectionChangeInfo.Value` 的装箱类型定为**元素的声明类型**（`List<int>` 给 `int`、
`List<MyEnum>` 给枚举值本身）。实现走 Unity 的类型化取值器再对齐声明类型，**不用 `boxedValue`**：
它在复合类型上的行为没有权威依据、整型给 `long`、枚举给的是 `enumValueIndex`（**下标**）——
后两者都是用户回调里 `(int)value` / `(MyEnum)value` 会抛异常并被吞掉的陷阱。
读不出来的（复合元素、多态引用）报 `null` + 一次告警，**回调照常触发**。

### 决定三：搜索是**策略**，不是可见性

`PropertyState.VisibilityResolver` 是**覆盖式**写入的（条件族无条件赋值），搜索若每帧写它，
会把 `[ShowIf]` 装的闭包**永久顶掉**。故照 `GroupChildrenLayout` 的既有先例：
策略对象进 `PropertyState`，渲染子节点的绘制器**读它并跳过 `Draw()`**——
被筛掉的节点 `IsVisible` 保持为真（有用例钉着）。

**四个读取点、一份策略**：复合成员的末端、`ChildrenDrawer`（分组与根）、集合绘制器、
表格行循环。`ChildrenDrawer` 那一处最容易被漏掉——**嵌套层的分组装配会把成员节点的子节点
换成分组节点**（`EnsureGroupChain` 给分组节点接 `ChildrenTerminal`），不认策略的话，
一个靠后代命中活下来的分组会把**不命中的成员一起画出来**。原方案里「`ChildrenDrawer`
已经认」那句是错的，核过装配代码后改判。

### 决定四：匹配素材是**标签 + 值**，不是标签

原方案写的是「行级过滤用 `SerializedProperty.displayName`」。核过之后不成立：
数组元素的 `displayName` 是 `Element 3` 这种索引名、表格行根本没有标签——
只比标签等于列表与表格上**搜不出东西**。改判为：

- 叶子按「标签命中 或 值文本命中」；值文本复用 `ReflectedValueFormatter`（枚举走**显示名**，
  `3` 与 `Bitter` 之间只有后者认得出）。
- 容器三档：自己标签命中 → **整棵子树保留**（不然搜分组名会得到一个空框）；
  只有后代命中 → 自己画、子节点逐个再筛；否则不画。**宿主自己的标签不参与**
  （搜「items」把整个字段全显示等于没搜）。
- 列表/表格的行**只按值**匹配（复合元素递归到任意一层的叶子；表格按任一单元格）。
  被筛掉的行仍以**真实下标**进 `−` 与索引标签。

**命中集在查询或数组长度变化时重算一次**，绘制路径只做集合查表（无反射、无分配）。
已知代价写进 README：搜索框没动时改字段值不会立刻刷新命中集。

### 决定五：解析器加「按名 + 期望参数表」

`[OnCollectionChanged]` 收两种形状：`()` 与 `(CollectionChangeInfo, object)`。
`MethodResolver.ByName` 与 `NamedMethodResolver.Resolve` 各加一条形状重载，**旧签名行为与文案逐字不变**
（既有调用方与用例不受影响）；`BySignature` 未动。
形状表**在外层、类型层次在内层**——反过来的话「谁赢」由 `GetMethods` 的返回顺序决定，
同名重载并存时结果不可预期。**逐目标的形状必须一致**：调用侧只有一份实参数组，
形状不同的那个目标会抛 `TargetParameterCountException` 并被吞掉（表现为「静默地没跑」）。

### 触碰到的既有判据

- `NestedMemberExpansion.ShouldExpand` 加了一条特例：**字段自己标了 `[Searchable]` 就展开**。
  这是继 `[InlineProperty]` 之后第二条「字段自己的特性也参与展开判据」的口子，
  代价是外观改变（从「整份交给 Unity」变成本包的折叠头 + 缩进）——它是用户显式写下的，写进文档。
  **类型侧的 `WouldExpand` 刻意不跟**：让类型进管线的是字段上的特性，而字段自己会被 `IsUsedBy` 扫到。
- `EnsureListSettings` 从 `TableListProcessor` 提到 `CollectionDrawerLayout` 共用
  （`[TableList]` / `[OnCollectionChanged]` / `[Searchable]` 三处都要它）。
- 元素类型的两处算法（表格建模、回调取值）收成 `CollectionElement.TypeOf` 一份。
- **多态引用的判据原先是一条恒为假的检查**：展开判据里那句「`[SerializeReference]` 不展开」
  看的是字段的**声明类型**，而那个特性标在**字段**上——类型上永远找不到它。
  今天挡住这类字段的是「序列化属性必须是 Generic」那一关（测量：这类字段报
  `ManagedReference`，有用例钉着）。本轮改成看字段（`NestedMemberExpansion.IsPolymorphicReference`），
  并与 `[Searchable]` 的构建期告警、`ReflectedAccessor` 的路径拒绝**共用同一份判据**；
  其中 `ReflectedAccessor` 那句「数组与多态段响亮拒绝」此前只是文档里的承诺
  （数组那条有实现有用例，多态那条两样都没有），本轮才补上。
  **教训与既有的「判据与注入必须看同一处」同源**——一条判据写在没人会走到的位置上，
  它与真话的区别要等到出事那天才看得出来。这条已固化进
  [CLAUDE.md](../../CLAUDE.md) 的「已知的坑」（判据要看**标特性的那一处**）。

### 遗留

- ~~**集合元素节点化**仍未做~~——**2026-10-06（第十五批）已做**（§十八）：元素成为真节点，
  搜索的行过滤与元素层并存（节点级命中集**跳过**元素子树，行掩码照旧）。
  ~~仍留下的是**元素里的读路径**（`Array.data[i]` 段取实例）与深度 > 1。~~
  **读路径当日晚些（第十六批）也已做**（§十九）；**元素层深度 > 1 于第十九批做掉**（§二十二）；
  仍留下的是「搜索按元素内的反射值匹配」。
- **类型级的 `[Searchable]`** 不做（本包类级特性只对被检视的最外层类型收集，那是另一条通道）。
- **`Recursive` 与 `FilterOptions` / `ISearchFilterable`** 不声明（官网未核到，不猜形状）。
- **长度不可变数组那条分支**（删不掉／加不进）仍无用例：夹具要 `allowUnsafeCode`，
  本轮不动 asmdef，靠 `ApplyAdd` / `ApplyRemove` 的长度比对 + 追加侧用例守着时序。

---

## 十八、第十五批：集合元素节点化（2026-10-06）

**落点**：数组 / `List` 的**元素**成为真节点（`Kind.Member`、路径 `items.Array.data[i]`），
元素类型里写的条件、分组、顺序、内联随之生效。**能力轮，特性计数 +0**（86 → 86）。
包 README 那条「元素类型里的本包特性照旧不生效」到此消掉——§五 的立项动因
（「让特性作用于元素」）在固定形状（§十四/§十五）与动态形状两半上都兑现了。

### 先决问题的答案：让不一致不存在

三处文档点名要先答「节点数与真实元素数不一致时怎么办（重建树？动态节点？）」。答案：
**元素层是 `arraySize` 的同步投影**——每趟绘制之前对账一次（`CollectionElementSync`），
对不上就整层丢弃重建（`PropertyTreeBuilder.RebuildElementLayer`）。

- **不做增量节点**：`InspectorProperty.Path` 是只读身份，删中间元素后所有下标位移 =
  所有路径必须改写 = 节点必须换对象，增量方案连「改名」都表达不出来；而消费者按**引用**记账
  （搜索命中集是 `HashSet<InspectorProperty>`、状态袋挂在节点上），换对象与它们天然相容，
  增量重排只会让它们静默指向错位的节点。
- **只比长度就够**：元素路径是**位置**的投影，长度不变时「路径 → 元素」的对应不变，
  而 `SerializedProperty` 是按路径的活句柄（改值、换序都不需要重建）。另加一条**脏标记**，
  罩住「净长度没变、但增删真的落地过」的情形——旧句柄不再可信，换一批新的
  （由 `CollectionChangeInvoker` 在施加成功后打上）。
- **对账落在 `PropertyTree.Draw` 的最前面**，不是集合绘制器入口：搜索过滤会从任意一个
  `ShouldDraw` 惰性触发、**整棵子树**走树，可能先于集合绘制器读到元素节点；
  「元素层在本趟内有效」必须是先于**所有**消费者的前置条件（窗口路径与 Inspector 路径共用这一处）。
- **一致性窗口的正式表述**（写进 `RawChildren` 与 `Kind.Member` 的注释）：
  树的形状只在**两个时刻**变——构建期一次、每趟绘制前的元素层对账一次；其余时刻形状不变，
  因此一趟绘制之内读到的树是稳定的。推论：**元素节点不跨结构变更**，旧节点一律作废——
  （**2026-10-06 第十九批按层重述**：元素层可递归之后，「两个时刻」这条**逐层**成立，
  **同步点从 1 处变成每层各 1 处**——外层重建会连带作废**并重建**内层（§二十二 决定二），
  作废面随之扩大；「排除元素子树」的两处跨趟消费者不受影响，因为排除发生在最外层元素节点处、
  整棵子树一次剪掉。）
  搜索的节点命中集与按路径重置的成员名单因此都把元素子树**排除在外**
  （顺带地，重建因此**不必失效命中集**——元素子树从不进集合，没有陈旧引用可言；
  行掩码仍按原有的「查询或长度变化」重算）。

### 安全阀：七项合取；用到了本包却被挡住的一律告警

| 项 | 判据 | 为什么 |
|---|---|---|
| 形状 | 是数组 / `List` 且非 `string`（与集合绘制器**同一份**判据 `CollectionDrawerLayout.CanDraw`） | 与本包的接管面逐字一致 |
| 元素类型 | 可解析、非标量 / 枚举 / 字符串 / `UnityEngine.Object` 派生 | 那些没有可生效的成员 |
| **元素阀** | 元素类型的**可序列化字段**（递归到 `MaxDepth`）上有本包支持的特性 | 见下 |
| **容器** | 字段在**第一趟处理器之后**有 `[ListDrawerSettings]` | 元素行只有集合绘制器会画；判据必须读**注入之后**的事实（`[Searchable]` 一族会补一份） |
| 非表格 | 不是 `[TableList]` | 表格按单元格画，本轮不节点化 |
| 深度 | 祖先链里没有别的元素层 | **只做一层**（内层递归是**数据规模**的爆炸，`MaxDepth` 挡不住） |
| 多态 | 非 `[SerializeReference]`（由 `ShouldExpand` 逐节点把关） | L7 那条产品线 |

**元素阀刻意只数「序列化字段」那一半**（`ContainsSupportedFields(..., includeNonSerializedMembers: false)`）：
元素里的读路径（`[ShowInInspector]` / `[Button]`，需要按 `Array.data[i]` 段取实例）**本轮不做**，
把那一半算进来等于「展开了却什么都画不出来」——比不展开更糟（§十六 的教训）。
被挡住的四种情形各**告警一次**（报在**集合**节点上，不按元素刷屏）：没容器 / 表格形态 /
在元素层里面 / 元素类型里用到的**只有**读路径。真没用到的什么都不说——那是「外观不变」的正常路径。

> **2026-10-06（第十六批）更新：这一半也已放开。** 读路径落地后元素阀两条腿都数
> （`includeNonSerializedMembers` 参数随之删掉，`ReadPathOnly` 一族与第二类扫描一并删），
> 被挡住的情形只剩「没容器 / 表格形态 / 在元素层里面」三种；**外观变化**（只带
> `[ShowInInspector]`/`[Button]` 的元素类型从此被本包接管）写进了 CHANGELOG 与包 README。
> 见 §十九。

### 元素节点与四处刻意的选择

- **`Kind` 仍是 `Member`**（`Member == null`）：路径 `items.Array.data[i]` 对 `FindProperty` 合法，
  `Kind.Member` 的不变量照旧；且按名解析容器（`SerializedMemberResolver.FindNestedScopeNode`）
  靠这一条认容器——单列新 Kind 会让元素成员里的条件**静默看错对象**。
- **值入口按路径独立取**（`FindProperty`，与成员节点同款）：`GetArrayElementAtIndex` 的句柄
  随结构变更作废，路径不会。
- **元素子树要补跑第一趟处理器**：顶层那一趟跑在元素节点出生**之前**。这是落地时第一处踩到的
  地方——漏了的症状是「元素成员的条件静默不跟随」（真红过一条用例）。
- **末端固定为值末端**：绘制期一旦降级（`CanDraw` 为假），整份交回 Unity 的原生数组画法，
  而不是把元素节点当折叠头逐个画。

### 重建流水线（顺序与构建期逐条对应）

构建期：树构造 → Owner → 第一趟 → **元素层（新阶段）** → 挂链 → 分组装配 → 第二趟 → Init。
新阶段的位置由两条约束夹出来：在第一趟**之后**（判据看注入后的特性）、在挂链**之前**
（注入会改变链的构成）；元素**内部**那一层的分组由原有的整树递归自动覆盖（前缀 = 元素路径）。

重建期：旧子树 `DisposeNode`（`IDisposable` 状态按销毁语义释放）→ 建节点 →
`RunNestedProcessors`（**只对新子树**，父 / 根钩子不重跑）→ `AttachChainRecursive` →
`ApplyNestedGrouping` + `SortGroupNodesAtLevel` → `RunGroupProcessors`。
**一次性动作，不是每帧动作**——「反射仅限构建期」按此豁免（见 `CollectionElementSync` 的类注释）。

### 规模

元素层的节点数是「元素个数 × 元素字段数」——**按需**才发生，但一旦发生就是乘性的。
**实测**（2026-10-06，本机）：1000 个元素、约 5000 个节点，建树 **574 ms**——一次性的成本
（每次选中重建一次），用例在 `CollectionElementNodeTests.一千个元素照样建得出来`，
耗时由 `TestContext` 打出（数量级变了能一眼看见）。
**不设规模上限**：悄悄只节点化前 N 个正是本包最忌讳的「静默」；对账是 O(1)（只比长度），
绘制路径上也没有非线性的事。

### 判据的第五次核对：这次的结论是「不用改」

元素节点化会不会又漏一次「判据看不见」？核过之后**不用改**：安全阀的容器项凭的是
**字段自己的**特性，而 `IsUsedBy` 一直在扫字段。因此**不需要**沿字段类型解包集合——
原计划里那条「解包」是多余的，做了反而会让「没被接管的集合」被判成「用到了本包」
（与安全阀反着来）。两条用例把这条镜像钉住（`XInspectorUsageDetectionTests`）。
**这是历次核对里第一次「结论是不用改」**——前四次（方法、属性、字段声明类型、嵌套类型内部）
各补了一处遗漏。

### 未做（留下一轮）

- ~~**元素里的实例读路径**（下一轮第一项）~~——**2026-10-06（第十六批）已做**，见 §十九：
  `ReflectedAccessor.TryCreatePath` 认 `Array.data[i]` 索引段，元素阀随之放宽到那一半
  （判据与消费者**同批**落地）。
- ~~元素层深度 > 1~~——**2026-10-06（第十九批）已做**（§二十二）；仍不做：
  `[SerializeReference]` 元素、表格内的元素特性。

---

## 十九、第十六批：元素层的读路径（2026-10-06）

**做了什么**：把元素里的最后一块能力补齐——`[ShowInInspector]`、条件族指向元素实例的
反射成员/方法/非序列化字段、`[Button]` 一族与按名回调**全部生效**，取/调的都是**那个元素实例**。
**能力轮，特性计数 +0。** 这是 §十八「未做（留下一轮）」清单里第一项的结案，
也是 §十四 → §十六 那套两步节奏在元素层的复刻。

### 一个原语：`Array.data[i]` 是路径转义，不是类型下钻

`ReflectedAccessor.TryCreatePath` 原先对 `Array` 与含 `[` 的段**一律拒绝**（注释与用例都写着
「那是给元素节点化那天留的接口」）。本轮把它接上：

- **判据落在前置类型上，不落在段名上**：当前类型是数组 / `List<T>` 时 `Array` + `data[i]`
  才是索引对；字段真叫 `Array` 时它就是普通字段（`Array.Array.data[0]` 靠这条正确）。
  数组补 **rank == 1** 检查（多维数组上 `Expression.ArrayIndex` 会抛，兜底出来的原因答非所问）。
- **表达式**：数组走 `ArrayIndex` + `ArrayLength`，`List<T>` 走索引器 `Item` + `Count`；
  守卫是 `AndAlso` 短路的「非空**且**在范围内」——集合为 null 时不去读 `Length`/`Count`。
  与逐段空传播同一条纪律：链在绘制路径上每帧求值，**绝不抛**。
- **越界落点分两档**：**末段**索引给 `null`（元素节点路径全是这种）——多选下各目标长度不一致时
  值类型元素给 `Default` 会表现为「显示 0」，那是静默错值的近亲；给 `null` 则三处消费者
  （读值 / 方法调用 / 条件）都落到既有的「取不到实例」语义。**中间段**给 `default(元素类型)`。
- **深度只数字段段**：索引对不占 `MaxDepth` 预算——预算挡的是自引用类型，
  而索引段是**路径转义**，不是类型下钻。
- 末段是索引时 **`Member` 为 `null`**（核过：全仓无生产消费者）、**`ValueType` = 元素类型**
  （三个消费者——`ResolveAccessors` / `ButtonProcessors` / `NamedMethodResolver`——恰好都要它）。

### 判据与消费者同批（§十六 的教训）

| 闸 | 动作 |
|---|---|
| 元素阀（`CollectionElementExpansion`） | 两条腿都数：删掉配套的 `ReadPathOnly`（枚举 / `Decide` 分支 / 告警 case）与 `FindUnsupportedInElement` 的第二类扫描；`ContainsSupportedFields` 那个从此恒为 true 的 `includeNonSerializedMembers` 参数一并删掉 |
| `ExpandChildren` 的 `withReflectedMembers` | 删掉（元素层与嵌套层同一条路） |
| `WarnAboutInertTypeGroups` | 加元素层祖先守卫——元素类型上的类级分组由集合级扫描报一次，按元素报会刷屏（**2026-10-06 第十八批追记：两者连同元素侧的 `FindUnsupportedInElement`/`Scan` 一并撤除**——类级分组在嵌套 / 元素层真的生效了，见 §二十一） |
| `AppendNestedReflectedMembers` | 逐目标循环改调 `NestedInstanceScope.Compile`（两处近乎逐字相同，是漂移点）；**一格都编译不出来时不再静默**——类型上确有节点成员却取不到实例时告警一次 |

### 落地时确认的边界（与嵌套层同款，各有用例）

- **值类型元素上的方法调用一律拒绝**——三族拒绝点都补了元素版用例（`[Button]` 禁用 +
  HelpBox、`[InlineButton]` 原因进 Tooltip、按名回调的构建期告警）；`List<结构体>` 在元素侧
  比嵌套侧常见得多。判据与文案同时**收口**到 `NestedInstanceScope.ValueTypeContainerReason`
  （此前两处各写一遍，逐字相同）。
- **元素为空**（null 元素 / 越界 / 空集合）时读值「—」、条件算假、按钮跳过该目标，全程不抛。
- 反射值后端仍只读（`SetValue` 恒抛）；Undo 仍记根 Unity 对象；增删重建之后元素反射成员仍正确。
- **外观变化**：只带 `[ShowInInspector]` / `[Button]` 的元素类型此前整份交给 Unity，
  从这一刻起会被本包接管（折叠头 + 元素行）——这正是 §十八 明写在案的那一半，
  写进了 CHANGELOG 与包 README。

### 明确不做

- **搜索按元素内的反射值匹配**：元素反射成员不进节点命中集（跨趟消费者一律排除元素子树），
  行掩码又只比序列化值——只带 `[ShowInInspector]` 的元素**搜不到**。改它要给行掩码加一条
  「按活值匹配」的通道，是独立一轮的量级；现状**不静默**（用户看到的是明确的「无命中」提示），
  写进已知限制。
- ~~元素层深度 > 1~~——**2026-10-06（第十九批）已做**（§二十二）；仍不做：
  表格内元素特性、`[SerializeReference]` 元素。
- 条件被根上同名**序列化**成员遮走，是嵌套层也有的既有次序（文档写明「先同级序列化、
  再根上绝对名、再反射」），本轮不修。

---

## 二十、第十七批：`[AssetList]`（2026-10-06）

**做了什么**：把数组/列表（**或单个 Unity 对象字段**）画成资产列表。这是 §六 判它 L6 时留下的
判据（「只做单元素那半会得到一个**语义随目标类型而变的半成品**」）的兑现——**两半都做**。
**特性计数 86 → 87**，L6 的最后一个非 L7 前置项；至此 **L6 只剩字典与矩阵（前置是 L7）**。

### 核对到的事实（官方只公开到哪一步）

`[AssetList]` = 「replaces the default list drawer with a list of all possible assets with the
specified filter」；官方两个绘制器 `AssetListAttributeDrawer<TList, TElement>` 与
`AssetListAttributeOnSingleObjectDrawer<TElement>` 印证「两半行为不同」。
**声明体（构造器 / `AttributeUsage` / 字段类型）官方未公开**——只有具名实参示例：
`Path`(string)、`AutoPopulate`(bool)、`Tags`(string)、`LayerNames`(string)、
`AssetNamePrefix`(string)、`CustomFilterMethod`(string 方法名)。

### 只声明两个（有真行为、形状可核）

- `Path`：与 `[AssetSelector].Paths` 同语义（`|` 分隔、`Assets/` 相对）；**兼容官方样例的前导
  `/`**（`"/Plugins/Sirenix/"` → `Assets/Plugins/Sirenix`）——这是本包对那句样例的**解释**，
  写进注释、README 与用例。
- `AssetNamePrefix`：**本包自定语义**——`AssetDatabase` 搜索语法里没有名字前缀过滤器，
  故这是取回路径后的纯函数过滤（文件名去扩展名、`OrdinalIgnoreCase`）。

**四个不声明**（写了会编译不过，而不是静默无效）：`AutoPopulate`（官方语义是「被检视时填充
列表」= 绘制即搜工程 + 绘制即改数据，撞两条硬规矩）、`Tags`/`LayerNames`（说的是 GameObject
的标签与层，而 `l:` 是**资产标签**——没有忠实的对应物）、`CustomFilterMethod`（resolved string，
方法一律不做）。

### 实现（与 `[TableList]` 逐字同构）

- **构建期**（`AssetListProcessor`，**泛型基类必须是**）：形态判定一份（`ObjectReference` →
  单元素；数组 / `List<T>` 且元素是 `UnityEngine.Object` 派生 → 列表；**接口元素类型直接拒**）、
  建模型（`Folders` / `TypeFilter` / `NamePrefix` 预计算——绘制路径不碰字符串）、列表形态
  `EnsureListSettings` 注入容器、降级（是非对象元素的列表就仍注入、退回普通列表绘制）、
  **与 `[TableList]` 同现时让位**（本包自定规则：不动既有表格行为）并告警一次。
- **列表形态**（`AssetListLayout`）：行 = 16px 缩略图（**本包自定值**）+ 原生对象字段 + 「−」；
  标题行「+」左侧多一个「选择」（`HideAddButton` 时不画——那是「往列表里加」的入口）。
  **缩略图自绘**，不走 `PreviewFieldGUI.DrawBox`（它在真预览与图标都取不到时会
  `new GUIContent(name)`，逐行画就破了「绘制路径零分配」）；**每行一份 `PreviewFieldState`**
  （那份缓存只记一个「当前对象」，N 行共用会互相顶掉）。
- **单元素形态**（`AssetListDrawer`）：替换型；复用 `[PreviewField]` 的全部几何（方块 64、在左）；
  「选择」从字段右端切出（纯函数）。**列表形态在这里静默放行**——`AttributeDrawer.CanDraw`
  是 sealed 的「有特性即命中」，这个绘制器也会挂在列表字段的链上。
- **拖放**：判定抽成 `AssetListDrop.Build`（纯函数，可无头测）；事件只留在绘制器一处；
  拖放区 = 画出来的那些行的并集；**行内的对象字段优先**（它吃掉事件后类型变成
  `EventType.Used`，我们那道「只认 DragUpdated / DragPerform」的判据天然让开）。
  被拒的给**带计数**的告警（拖放是用户动作、不是每帧，故不走 `DrawerWarnings.Once`）。
- **批量施加**：`CollectionMutation.AddRange` + `CollectionChangeInvoker.ApplyAddRange`——
  一次拖入 / 一次多选 = **一对**回调（`Index` = **第一个**新元素、`Value` 仍为 null）；
  槽先按 Unity 的「副本」语义长出、随即被逐个覆盖（注释里写明这条与单元素 Add 的差别）。

### 三处本包自定语义（进 README 差异清单）

拖放**去重**；**只收工程资产**（`EditorUtility.IsPersistent`——场景对象不是本特性的语义）；
`t:` 只是**启发式收窄**——抽象类型可能一个都搜不到（菜单会空并**告警**，不静默），
**正确性由落值前的类型校验保证**（`AssetListWrite`，菜单与拖放两条路共用）。

### 不做

`AutoPopulate` 一族、多选删除、行内拖放替换（原生对象字段自带）、预览块上的拖放
（方块是预览不是控件，与 `[PreviewField]` 同一立场）、GameObject → 组件的降级查找
（官方没写这种语义，不发明）。

---

## 二十一、第十八批：类级分组进嵌套层与元素层（2026-10-06）

**做了什么**：类型自己带的 `[BoxGroup]` 一族（含 `[ShowIfGroup]` / `[HideIfGroup]`）分发到
它的成员——嵌套类型与集合元素类型都算。**能力轮，特性计数 +0**；它是「嵌套 / 元素类型
成为一等公民」这条线的**最后一个欠账**（§十四 → §十五 → §十六 → §十八 → §十九 之后的收官）。
两处「类级特性只在被检视的最外层类型上收集」的构建期告警随之撤除。

### 决定一：来源分两档，复合容器读**容器的声明类型**

根读根节点自身的特性列表（既有收集通道 `CollectTypeAttributes`，不动）；嵌套 / 元素的
复合成员节点读 `parentProperty.Type.GetCustomAttributes(typeof(PropertyGroupAttribute), true)`。

**绝不读 `parentProperty.Attributes`**——那装的是**字段自己的**特性：字段上写一个
`[BoxGroup("X")]`，会被误当成这个类型的类级分组再分发给它的孩子。这与
`ClassLevelInlinePropertyProcessor` 读 `field.FieldType` 是同一条纪律（**判据要看「标特性的
那一处」**，CLAUDE.md 已固化）。`inherit: true` 与根同口径（根就是 `GetCustomAttributes(true)`，
分组族的 `AttributeUsage` 也都是 `Inherited = true`）；「只取第一个」照旧，且只承诺「恰好一份」
——`GetCustomAttributes` 的顺序不作承诺。

**处理器只注入、不加前缀。** 前缀的唯一来源是装配期的
`AssembleNestedLevel → PrefixGroupAttributes`；两处都加会叠成 `stats/stats/组`。于是最终路径是
`stats/类级组/成员自有组`——「类级恒在最外层」这条语义**逐字继承**自根上，不需要重新定义。

**一条编译期边界**：分组族的 `AttributeUsage` 只到 `AttributeTargets.Class`——标到 struct 上
**编译不过**，因此值类型元素不会被类级分组光顾。这是好事（响亮拒绝而不是静默），写进文档即可。

### 决定二：判据一份读，且**这次第三道闸不在传导链上**

新增 `NestedMemberExpansion.FindClassLevelGroup` / `HasEffectiveClassLevelGroup`
（= 标了，**且「有成员可分」**——后半挡的是「标了分组却没有成员的类型」被元素阀放行或触发
接管：那会建出一层什么都画不出来的东西；过度接管与漏接管方向相反、同属静默）。

三处加腿，**镜像 `IsClassLevelInlineMarked` 的既有放置**：

| 落点 | 加什么 | 为什么不能省给递归 |
|---|---|---|
| `ShouldExpand` | `HasEffectiveClassLevelGroup(member.Type)` | 直接腿 |
| `HasSupportedMember` | `HasEffectiveClassLevelGroup(field.FieldType)` | 属性版递归**有条件**（`hasVisibleChildren` 不满足就跳过），跳过的情形靠这条兜 |
| `HasSupportedField` | `HasEffectiveClassLevelGroup(type)`（**自检腿**） | 纯反射递归**无条件**，自检腿一条就够；且 `WouldExpand` 问的是类型**自己**，逐字段腿根本够不着 |

**`IsUsedBy` 与元素阀不改代码、只改注释**：它们分别经 `WouldExpand` 与
`ContainsSupportedFields` 到达上面那条自检腿——一份读，不新增第二处定义。
`IsCompositeCandidate` 的 `hasVisibleChildren` 闸**不动**，只补注释：它在**收集期**跑、早于
第一趟处理器，读的是 Unity 的序列化形状，与注入无关。§十六 教训里的「第三道闸」这次**本来
就不在传导链上**——但要知道它在哪（顺着调用链从入口问到出口，每一处提前返回都是一个潜在的闸）。

### 决定三：分组条件的容器链回退（不做则半条能力静默）

类级 `[ShowIfGroup]` 在成员**全部**自带分组时只当路径前缀、特性本身到不了任何节点。
根上早有专门回退，但判据 `rootAttr.GroupID == property.Path` 只对根成立——嵌套层里条件会
整份丢失且**零告警**。本轮把来源二由「根」推广为「**父链就近到远**」：根读节点特性列表
（处理器可能改写过它，改成反射会漏掉那类注入），嵌套 / 元素容器读容器的声明类型；匹配写成
「容器路径 + 声明路径 == 本节点路径」——根的空路径让它**退化成原判据，根上行为逐字不变**；
就近优先（嵌套声明压过根上同名）。

**被否决的替代方案**：让分发处理器在「成员有自有分组」分支里额外注入条件特性本身。
它更省事，但会改掉根上已被用例钉住的语义（`类级声明经根上回退生效` 断言「类级特性本身
到不了节点」），且改变 `[TabGroup]` 这类祖先节点的特性构成。

### 决定四：拆两处告警、**不加新的**

- `WarnAboutInertTypeGroups`（嵌套侧）与 `FindUnsupportedInElement` / `Scan` / `IsOpaque`
  （元素侧）整组删除——后者只扫「类级分组」这一类，删后恒为空，**变空即删**。
- **不新增替代告警**：其余类级特性（`[Title]`、`[InfoBox]` 之类）仍只在被检视的最外层类型上
  生效、依旧不告警——一个类型同时用于根与嵌套字段是常规写法，一律告警会成噪音。
  口径写进包 README：**类级分发的只有分组族**（`[InlineProperty]` 是另一条既有通道）。
- 记录在案的近似：`[SerializeReference]` 字段的声明类型带类级分组时，`IsUsedBy` 会判真
  而展开被多态闸挡住——与 `[InlineProperty]` 同款的既有近似，不修。

### 元素层与规模

注入随 `CreateElementLayer` 与 `RebuildElementLayer` 的第一趟处理器重跑，在挂链与装配之前
（与构建期逐条对应）——重建后类级分组自然还在，仍补了专门用例。每个元素各是各的组
（`items.Array.data[i]/组`）。元素节点自己不会被搬进分组（`Attributes` 恒空、`Member == null`，
`AssembleNestedLevel` 的早退成立）——补了守卫用例。`FindClassLevelGroup` 是每（容器 × 子成员）
一次反射调用，构建期与重建路径各一次；1000 元素规模用例照旧通过。

### 一处流程教训：**「待翻转的用例」要先读夹具**

探索期把三条既有用例记成「本轮要翻转」，核过之后发现夹具用的是 `[Title]`（**非分组族**）——
本轮只让分组族生效，那三条**行为未变**，改法是**留作对照组**并把措辞从「类级特性」收窄成
「类级**非分组**特性」，另补分组族的正向用例。**「看起来该翻转」与实际翻转之间隔着
「夹具到底标了什么」**——只看用例名与所在文件会得出错误的改动清单。

---

## 二十二、第十九批：元素层深度 > 1（2026-10-06）

**做了什么**：元素**里面**的集合也按需节点化（`List<Room>` 里 `Room` 的 `List<Enemy>`），
**能力轮、特性计数 +0**。§十八 当初「只做一层」的理由（乘性放大）**没有被推翻，而是换成了
两道显式守卫**（用户拍板）：

- **类型链去重**：候选集合的元素类型已出现在祖先元素层的**元素类型链**上 → 不建层。
  `Node{List<Node>}` 与互递归 `A{B} B{List<A>}` 都在类型重现的那一层被挡——再展开就是无限递归。
- **层数预算**（`MaxElementLayerDepth = 4`，本包自定值，与三处 `MaxDepth` 同档）：带层状态的
  祖先数达到上限 → 不建层。**它挡的是类型链，不挡数据规模**（每层宽度不受限，4 层 × N 仍是乘性）。

两道都**响亮拒绝**（构建期告警），不是静默截断。

### 决定一：判据换成一次走完的 `LayerNestingBlock`，`NoContainer` 提前

`Decide` 的新次序：形状 → 元素阀 → 表格 → **容器** → 两道守卫 → Build。
`NoContainer` 提前是因为内层集合「既在元素层里、又没写容器」是常态，报「加
`[ListDrawerSettings]`」才可行动；两守卫同时成立时先报容器（用户照做后看到第二条——
两步揭示，接受）。`HasElementLayerAncestor`（唯一消费者就是这个旧闸）删除，换成
`LayerNestingBlock`：沿父链**一次**走完，数带层状态的祖先（层深）并逐个比对
`ElementTypeOf(祖先)`（类型链）。`ElementLayerDecision.Nested` 拆成
`RepeatedElementType` / `TooDeep`。

### 决定二：重建期**必须递归**——它是能力的正确性前提，不是优化

`RebuildElementLayer` 原样只重建自己这一层：外层一次增删 → 新元素子树里的内层集合
**既不建层也不登记**（登记唯一入口在 `CreateElementLayer`），内层元素层**静默消失**；
被 Dispose 的旧内层条目在 `ElementCollections` 里还会成为强引用作废子树的僵尸。两处都补：

- **释放之前先注销**：`tree.UnregisterElementLayersIn(children[i])` 必须紧挨在
  `PropertyTree.DisposeNode` **之前**——释放会 `State.Reset()` 清袋，之后认不出谁带过层。
  注销判据是**结构遍历**而非路径前缀（`data[1]` 与 `data[10]` 的前缀比较会误伤）。
- **第一趟处理器之后重走展开**：`ExpandCollectionElementsIn(tree, children[i])`——内层容器项
  可能是处理器注入的（须在其后），而挂链与分组装配的递归会一并覆盖新内层节点（须在其前）。
  复用同一入口让「构建与重建逐条对应」多一条对称性，代价只有对元素节点自身的一次冗余判定
  （`CanDraw` 对 Generic 必然 Inert——构建期本来就判过一次）。

### 决定三：登记簿的两条写路径与 `ReconcileAll` 的循环不变量

`PropertyTree.AddElementCollection`（幂等）与 `UnregisterElementLayersIn`（递归摘）是名单
**仅有的**两条写路径。`ReconcileAll` 的 `for (i < Count)` **不快照、不提前缓存 Count**——
这是契约，靠三条不变量成立：① 登记是 DFS 先序，祖先下标恒小于后代；② 重建摘掉的恒是自己
的后代且恒为死条目（下标 > i）；③ 追加到表尾的新内层条目恒新鲜（同趟 Reconcile 是廉价 no-op）。
误改成快照/缓存 Count 的症状是「偶尔漏对账一个集合」——难归因，所以写进注释而不是只靠用例；
快照还违反「每帧路径禁分配」。

### 决定四：被挡告警的收件人上溯到**最外层**集合

深度 > 1 之前，「被挡」告警按**集合节点**去重——N 个外层元素各含一个被挡内层就是 N 条
只差路径的同一句话（§十八 自己写的「报在集合节点上、不按元素刷屏」在深度 1 就已经破了，
只是没人在意）。现在：锚点上溯到**最外层**带层状态的祖先（不是最近——最近的会随外层元素
各是一个），键里带**归一化路径**（`data[3]` → `data[*]`）——同一字段的 N 个实例一条、
不同字段各一条；深度 0 的被挡集合锚在自己头上，行为逐字不变。锚点在外层自己的重建中存活
（账本保留），被上层重建才重报（结构变了，值得再说一次）；重建期新判出的被挡集合照常发声
——顺带消掉了「构建期告警不重跑」的那个静默缺口。

**一处预期落空，如实记下**：原打算「删掉 `default:`，将来新增枚举成员会在变量定值处触发
CS0165（编译期红）」——**不成立**：C# 把没有 `default` 的 switch 一律视为**非穷尽**，于是
`switch` 之后使用该变量**恒**报 CS0165，代码根本编译不过。改为显式
`default: throw new ArgumentOutOfRangeException(...)`，并在注释里写明「不要改成
`default: return`」。

### 顺带修掉的既有缺陷：搜索行掩码**串掩码**

`SearchFilterState` 的行掩码缓存键原来只有「查询 + 长度」——同一搜索宿主下两个**同尺寸**
集合会**串掩码**（静默筛错行）。这是今天就能复现的缺陷（复合 `[Searchable]` 宿主里并排两个
同尺寸列表），深度 > 1 让「外层查询落到内层集合」成了常态、必然放大。修法：键加**集合节点
引用**（零分配；重建后是新节点、自然重算），表格键再补列集合。**不比 `SerializedProperty`**
——它是按路径重取的独立实例，每次都会 miss。

### 读路径：一行没改，只补用例与注释

探索核实：`ReflectedAccessor.TryCreatePath` 的索引对判据每轮按**前置类型**重算，**天然支持
多组**；`NestedInstanceScope` / `FindNestedScopeNode` 取**最近**的 `Kind.Member` 祖先，
内层元素节点比外层近。于是本轮把承诺变成用例：两组索引对（值 / 末段 null / 中间段默认值 /
空集合 / 内层 null / 值类型元素）与**三层同名陷阱**（拥有者 / 外层元素 / 内层元素——
反射成员、按钮、条件各一条，值或极性相反）。并**明确保持**「按名解析是两级语义
（最近容器 → 根绝对名，**不查中间祖先**）」——补了一条专门的用例钉住，不扩语义。
注释记了两笔账：`NestedMemberExpansion.ResolveField` **不认索引段**（防误用）；
`ReflectedAccessor` 的**表达式树 ×3 增长**（每多一对索引，前缀子链在树里出现次数约 ×3
——守卫双拼 + Condition 内联；预算 4 层内可接受）。

### 规模与成本

- 节点数上界 = 各层元素个数之积 × 字段数（预算只管类型深度、不管宽度）——写进常量注释、
  包 README 与本节。
- 重建成本：外层改一次 = 重建外层全部元素子树（含内层）+ 旧子树两趟递归（注销 + 释放）+
  新子树两趟（展开 + 处理器/挂链/装配）。O(子树)，一次性动作，与深度 1 同阶、常数变大。
- 新用例覆盖：40 × 20 的规模测量（打耗时）、连改 5 次登记名单恒定、内层增删只脏内层。

### 遗留

- **表格内的元素特性**（表格不节点化）与 **`[SerializeReference]` 元素**：维持不做
  （各自有独立理由，见 §十八）。
- **搜索按元素内的反射值匹配**：仍不做（行过滤只比序列化值）。
- OdinGap「仍欠」至此只剩**按名找成员收成一层**。

---

## 二十三、审计记忆

**2026-10-04（第七轮）：「没有公开无参构造函数」的告警打错了收件人。**

- **症状**：正常打开窗口时 Console 出现「以下特性处理器没有公开无参构造函数，已跳过：
  `XInspector.Tests.Editor.NoDefaultConstructorProcessor`」，每次域重载后首次建树各一次
  （绘制器与处理器各一条）。全量 EditMode 跑一遍，这条文案在日志里出现 **529 次**
  ——每个 fixture 复位注册表就重扫一次、重报一次。
- **根因**：`EditorTypeScanner` 扫**所有已加载的编辑器程序集**（这是刻意的，
  「使用方零注册扩展」的前提），而测试程序集里有两个**故意**不可实例化的夹具
  （`NoDefaultConstructorProcessor`、`NoDefaultConstructorDrawer`，各只有一个带参构造，
  用来测「跳过」那条路径）。于是「你的扩展写坏了」这句说给使用方的话，
  说到了测试夹具头上。**不是开发仓独有**：`Tests/` 随包分发，使用方装了 Test Framework
  就同样会看到。
- **修法：只收窄告警，不收窄扫描。** 判据是「该程序集引用了 `nunit.framework`」
  （Unity 自己也是这么分辨测试程序集的），收成 `EditorTypeScanner.ShouldWarnAbout` 这个纯函数。
  跳过的**行为**一个字没改，改的只是要不要说话。
- **为什么不去动扫描范围**（认真评估后否决）：两条契约用例
  （`Registry_Discovers*FromThisAssembly`，失败消息就是「扫描范围被收窄了」）
  守的正是「所有程序集都扫」；而且判据一旦被用来决定**注册范围**，
  误判的后果是使用方的绘制器**静默不注册**——那比多一行日志糟得多。
  这次判据只决定一行日志，误判的代价也就只是一行日志。
- **顺带的生产教训**：「告警文案的收件人是谁」要写清楚。同一条 `LogWarning` 对
  「使用方写坏了扩展」是帮助，对「测试故意违规」是噪音——而**两者在类型系统里长得一模一样**。

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
