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
- 当前版本 `0.1.0-preview.1`，已实现 **91 个特性**；**L1a、L1b、L2、L4、L5 已清完**
  （L3 已做；`[TypeDrawerSettings]` 已落地——第二十八批，自绘选择器一族的第一块：
  「可写的 `System.Type` 通道」一直在，只差字段加 `[SerializeReference]` 这个约定；
  `[PolymorphicDrawerSettings]` 亦已落地——第二十九批，多态字段的自绘选择器 + 三个旋钮；
  `[TypeSelectorSettings]`（三个显示旋钮 + 单参过滤器）与 `CreateInstanceFunction`
  亦已落地——第三十批，本包第一次编译**带参**调用，见 Pipeline §三十四）。
  L2 收尾之二收了分组条件族与跨对象条件的结论；L4 收了顺序与内联两条；
  **L6 第一批**收了集合容器与表格五条——**只接管容器，元素仍由原生绘制**；
  **L6 收尾两条**（2026-10-06，第十四批）：**集合回调** `[OnCollectionChanged]`
  （两个方向夹住「写进序列化数据」那一步）与**搜索** `[Searchable]`（按标签或值过滤
  子成员与行；过滤是**策略不是可见性**）——两条都**绕开了**元素节点化。
  **嵌套层能力**由两条**能力轮**补齐（2026-10-06，特性计数 +0）：**嵌套类型成员节点化**
  （`[Serializable]` 的成员按需成为真节点，`[InlineProperty]` 随之升到完全体）与
  **嵌套层的分组装配**（分组节点的路径以父字段的序列化路径为前缀）；
  第三条能力轮**嵌套层的读路径**（`[ShowInInspector]`/`[Button]`/条件指向反射成员）
  也已完成；第四条**集合元素节点化**（2026-10-06，第十五批）——元素按需成为真节点，
  元素类型里的条件、分组、顺序、内联随之生效；元素层是 `arraySize` 的**同步投影**
  （绘制前对账、对不上整层重建），见 Pipeline §十八；第五条**元素层的读路径**
  （2026-10-06，第十六批）——元素里的 `[ShowInInspector]`/`[Button]`/条件指向反射成员
  全部生效（路径访问器认 `Array.data[i]` 索引段），见 Pipeline §十九。
  **L6 的最后一个非 L7 前置项 `[AssetList]` 也已落地**（2026-10-06，第十七批，特性计数
  86 → 87）——列表与单元素两形态；**L6 至此只剩字典与矩阵（前置 L7）**。
  第六条能力轮**类级分组进嵌套层与元素层**（2026-10-06，第十八批，特性计数 +0）——
  类型自己带的 `[BoxGroup]` 一族分发到成员（元素层每个元素各是各的），两处「只在最外层
  类型上收集」的告警撤除；**「嵌套/元素类型成为一等公民」这条线至此收官**，见 Pipeline §二十一。
  第七条能力轮**元素层深度 > 1**（2026-10-06，第十九批，特性计数 +0）——元素**里面**的集合
  也按需节点化，两道守卫（类型链去重挡自引用 / 层数预算 4 挡过大类型链）取代「只做一层」，
  见 Pipeline §二十二。第八条能力轮**成员引用收成一层**（2026-10-06，第二十批，特性计数 +0）——
  「按名找成员」的四级阶梯收进 `Editor/Internal/MemberReferenceResolver`，
  `[ToggleGroup]` 的开关与 `[MinMaxSlider]` 的边界随之可指向反射成员，
  **OdinGap 的「仍欠」至此清零**，见 Pipeline §二十三。第九条能力轮
  **`[ValueDropdown]` 的反射数据源**（2026-10-07，第二十一批，特性计数 +0）——数据源多了
  「声明类型**实现 `IList`** 的字段/属性/无参方法」这一形态（只实现 `IEnumerable` 的不收，
  `string` 会静默变出字符选项表），并写下本包第一条 **object → `SerializedProperty`** 的
  写回通道；**L0–L6 至此连最后的遗留也收口**，见 Pipeline §二十五。第十条能力轮
  **搜索按反射成员的活值匹配**（2026-10-07，第二十二批，特性计数 +0）——元素里的
  `[ShowInInspector]` 成员按当前值参与行匹配，顺带修掉两条**没人报过的静默缺陷**
  （搜索生效时元素里的分组/复合成员被筛空、行掩码缓存键缺元素层身份），见 Pipeline §二十六。
  第十一条 **`[ColorPalette]`**（2026-10-07，第二十三批，特性计数 87 → 88）——卡了很久的是
  **设计**不是实现：调色板存**工程内资产**（不是编辑器偏好，那样不进版本控制）、
  **按资产名查找**、无参形态用工程里**唯一**那份，见 Pipeline §二十七。
  第十二批（第二十四批）是**窗口两个切口与小件三件**（`DrawEditors` / `WindowPadding` /
  `[DisplayAsString]` 重载 / `[PreviewField]` 方块落点，`Initialize()` **判不做**——
  `OnEnable` 就是那个钩子），见 Pipeline §二十八。
  **第二十五批是 L7 核验轮**（特性计数 +0，**生产代码零改动**）——把剩下的 8 个缺口逐个核清，
  结论**改了一半**：**Serializer 那半（字典 / 矩阵）判不作为**（要自研序列化器，
  与「值后端是 `SerializedObject`」正面冲突），**Inspector 那半是一条能走的能力轮**
  （多态引用进管线 + 自绘选择器一族）——**8 个特性没有一个「必须自研序列化器」**，
  而且 `[TypeDrawerSettings]` 要的那条可写 `System.Type` 通道**已经有了**（代价是字段加
  `[SerializeReference]`）。**OdinGap 的「推荐顺序」至此全部有了结论**，
  「卡在设计」那一列早已归零，见 Pipeline §二十九。API 尚未稳定

## 仓库布局与边界规则

包本体在 `Assets/XInspector/`，**会**随包分发；其余都是工程壳，**不会**：

| 路径 | 会不会分发 |
|---|---|
| `Assets/XInspector/` | **会** |
| `Assets/Sandbox/` | 不会。维护用**对照组**组件与场景（诊断台） |
| `Tools/` | 不会。测试与文档门禁脚本 |
| `ProjectSettings/`、`Packages/` | 不会。开发工程的壳 |

推论：**开发用代码绝不进包**；**包文件绝不 gitignore**；**包下所有 `.meta` 一律提交且绝不手改**
（`.meta` 由 Unity 生成，不是手写的）。

### 边界靠约定，不靠位置

包本体与工程壳**都在 `Assets/` 下平级**，因此不能再靠「在不在 `Packages/`」区分——只能靠
目录名与这条约定：**`Assets/XInspector/` 之外的一切都是工程壳**。往包里加东西前先问一句：
这东西是给使用方的吗？

**判据是收件人，不是「是不是演示」。** 包内有 `Samples/`（示例，收件人是第三方），
沙盒里也有演示组件（收件人是维护者）——同样写着演示代码，一边进包一边不进，差别只在谁看：
示例回答「这插件长什么样」，沙盒的对照组回答「值管道有没有改变外观」。
后者作为示例是噪音（有个组件刻意一个特性都不带），前者作为对照物不成立。
2026-10 按这条判据分过一次家：展示台并入 `Samples/`，对照组留在沙盒。

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
   成员。**那套「画普通属性」的后端已经落地**：`ReflectedValueEntry`（`IsUnityBacked` 恒
   `false`、`SerializedProperty` 恒 `null`、**只读**——`SetValue` 恒抛），配一个节点种类
   `InspectorPropertyKind.ReflectedMember`。它一件也拿不到那五件事，所以**刻意不给写**：
   与其做一个改完就丢的控件，不如不给。
   嵌套层的**值**同样只读（反射值后端不变），但嵌套层的 `[Button]` 一族与按名回调是
   **方法调用**——会写、Inspector 路径记**根对象**的 Undo。两者别混成一句「反射只读」。
2. **绘制器是无状态共享单例**。每种类型全工程只实例化一个（500 字段的 Inspector 不会因此
   产生两万个对象）。代价是**绘制器里不得有可变字段**，每属性状态一律进 `PropertyState`。
   违反这条的症状是「展开一个、全都展开了」，很难联想到原因。
3. **链条末端由构建期显式追加，不进注册表**。末端是结构性的（根/分组接 `ChildrenDrawer`、
   成员接 `UnityFallbackDrawer`、方法节点接 `MethodTerminalDrawer`）。
   交给注册表意味着一个写错的匹配条件就能让某属性链为空，
   症状是「它静默地什么都不画」——最难归因的一类问题。
4. **绘制器链的排序是「权重升序 + 序号兜底」**（小 = 外层）。序号兜底必不可少：
   `List.Sort` 是不稳定排序，只比权重的话同权重格子的顺序会随元素个数变化。
5. **分组用点分路径，祖先节点自动合成**。不变量：**分组节点上的分组特性恒满足
   `GroupID == node.Path`**——多类型并存之后是**每一份**都要满足。节点落在其首个成员出现的
   位置，故夹在分组字段之间的未分组字段留在原地。同层分组之间按 `Order` 重排，但**只重排
   分组彼此之间的先后，不动未分组成员的位置**。三条组合规则：同路径上**不同类型**的分组特性
   并存（各配一格绘制器），同类型才走 `Combine`；成员按**前缀归属**（每个「是目标路径链」的
   分组特性各贡献自己那段路径）；节点排序取**最小的非零 `Order`**。
   **分组绘制器只在分组节点上配**——根与成员携带分组特性只是为了归属；让它们也画一遍的症状
   是双重框与重复标题（`GroupDrawerPlacementTests` 守着这条）。
   **嵌套层同样装配**：分组节点的路径以**父字段的序列化路径为前缀**（`stats/基础`），
   前缀一并写进 `GroupID`——于是上面那条不变量在嵌套层照旧成立；且**前缀不是分组段**，
   构建期跳过它、不为它造节点。装配落点在**建树末尾**（处理器与挂链之后、第二趟处理器
   之前），逐层深度优先，只对「有子节点的成员节点」装它自己那一层。
6. **每个属性一份独立的特性实例**。成员特性靠反射天然如此（每次调用返回新实例）；
   类级特性分发到成员时**必须显式克隆**（`CloneForPath`），否则一个实例被几十个成员共享、
   改一处串一片——类级分组分发就是这么做的。
7. **处理器在分组装配之前跑**。处理器可以往成员的特性列表里注入分组特性（类级 `[BoxGroup]`
   分发就是这么做的），而分组装配必须看到它们。顺序反过来，类级分组会**静默地不生效**。
   同理，处理器也必须在**挂链之前**跑——注入的特性会改变链条的构成。
   **一条补充：处理分组特性的处理器走「第二趟」**——在分组装配之后、只对分组节点跑
   （分组节点在那之前不存在，这类处理器没有更早的落点），且只许改状态。
   判据由 `HandledAttributeType` 是否派生自 `PropertyGroupAttribute` 推导，不设开关。

### 包结构

```
Runtime/                     Venusir.Xinspector
  Attributes/                公开特性（Title、Groups/BoxGroup、Buttons/、Callbacks/…）
  Internal/                  内部工具（PropertyGroupPath、两个标记接口）
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
Samples/Overview/            示例：最小可用形态，挂上组件即可看（面向第三方）
Samples/AttributeShowcase/   示例：逐特性展示台（面向第三方）
```

包外的对照组（不随包分发，见「仓库布局与边界规则」）：

```
Assets/Sandbox/              零特性基线 / 原生装饰器 / 自动接管，各一个组件 + Sandbox.unity
  Editor/                    场景生成器等工具（不进场景）
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
（判据是「有没有能处理其特性的**绘制器或处理器**」——只看绘制器会漏掉条件族这类处理器专有特性，
症状是「类型不被接管、特性静默失效」，见 `XInspectorUsageDetection.IsUsedBy`），
没有就走 Unity 原生绘制。因此**即使开了宏，没用到本插件的类型外观也不变**。

## 编码规范

- **命名：** 接口 `I` 前缀；私有/受保护字段 `_camelCase`；常量 PascalCase；方法 `TryXxx(out T)`、
  `GetOrCreateXxx`；bool 属性 `IsXxx`。**测试方法名用中文**（它描述的是「这组断言在说什么」，
  如 `只声明支持的选项`），**其余标识符一律英文**；中文只另外出现在注释与文档里
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
`Tools/check-docs.ps1 -Enforce`（文档 + .meta + 源文件全参与编译 + 沙盒场景对齐）。

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

## 文档门禁要守的四件事

`Tools/check-docs.ps1 -Enforce` 四项都须满足：包内 XML 文档告警为 0、所有源文件都已参与编译、
所有资源都有 `.meta`、**沙盒顶层组件与 `Sandbox.unity` 一一对应**。它是独立的一条通道：
csproj 未设 `DocumentationFile`，**默认编译根本不检查文档注释**，不开这一枪则写坏文档不会有任何反馈。

**前置条件：需要 `.csproj`，而它只在 GUI 编辑器里生成**——纯批处理环境下这条门禁跑不起来，
脚本会判失败。那不是脚本坏了，是它拒绝给假绿。细节见
[Documentation/Workflow.md](Documentation/Workflow.md) §一；没有 GUI 时也可以单独核
XML 文档告警（同一节的「变通检查」），但它验不了「源文件都参与了编译」那一项。

三处本包特有的处理，改动时别删：

- `Editor/AutoEditor/` 在「是否参与编译」检查中被排除——它是宏门控的，宏关掉时那里的源文件
  本来就不该被编译，不排除会长期误报
- 「缺 `.meta`」检查跳过**点开头**的条目（Unity 不给它们生成 meta）。包在 `Packages/` 时
  还有一条处理 `~` 结尾目录的规则，搬到 `Assets/` 后波浪号已去掉，那条成了永不触发的
  死代码，已删——若日后又出现 `~` 目录，记得加回来
- 沙盒对齐检查的作用域是 `Assets/Sandbox/`（工程壳），与上面三条的包内作用域分开写。
  沙盒不随包分发，但仓库内的一致性一样是门禁的事——2026-10 场景曾漂移过，
  [Documentation/OdinGap.md](Documentation/OdinGap.md) 的验证步骤指着场景里不存在的 `Demo 4`；
  照文档做的人会得出「原生装饰器没流经管线」这种反向结论

## 如何新增一个特性

1. Runtime 侧加特性类（`Runtime/Attributes/`），声明正确的 `AttributeUsage`
2. 若要参与绘制，写 `AttributeDrawer<TAttribute>`（`Editor/Drawers/BuiltIn/`），
   按需加 `[DrawerPriority]`。**只画东西、把决策留给别处**，且不得有可变字段
3. 若它影响**别的**属性（可见性、分组归属、标签），写一个 `AttributeProcessor<TAttribute>`
   （`Editor/Processors/`）：它在构建期跑，能改写成员的 `PropertyState`（装每帧求值的求值器）
   或往特性列表里注入特性。两个钩子与三条纪律见 `Editor/README.md`
4. 补无头测试（链装配顺序、分组归属、路径解析这类）
5. 在包内展示台 `Assets/XInspector/Samples/AttributeShowcase/` 里加一行（第三方看的就是这里）；
   若该特性会改变渲染、需要一个对照基准，再按需在 `Assets/Sandbox/` 加对照组
   （顶层加组件后**必须重建场景**，否则门禁会拦下你——见 Workflow.md）
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
- **`PropertyState.IsReadOnly` 是算出来的，不是存下来的**（与 `IsVisible` 同构）。
  处理器装 `ReadOnlyResolver`，绘制路径每帧求值。写 `State.IsReadOnly = true` 编译不过，
  要用 `SetReadOnly(true)`。同理 `VisibilityResolver` / `ReadOnlyResolver` **都有非 null 的
  哨兵默认值**（恒可见 / 恒可编辑），因此「为 null」不表示「未安装」——
  想判断装没装要看行为，别判 null
- **条件求值器读的是**树所属的 `SerializedObject`。通过**另一个** `SerializedObject` 改值后，
  必须对树那个调 `Update()` 才看得到——真实绘制路径每帧开头本来就会 Update，
  但测试里不补这一下就会得到「条件不跟随」的假失败
- **`m_Script` 这类 Unity 注入成员没有对应的 `MemberInfo`**，因此不参与处理器的
  「父级注入」钩子（守卫在 `PropertyTreeBuilder.RunProcessors` 里）。让它们触发的话，
  处理器拿到 null 极易 NRE，且「对每个成员各触发一次」这条契约会多算一条
- **元数据令牌的高字节是表号**：字段在 FieldDef（`0x04`）、方法在 MethodDef（`0x06`），
  两张表**各自编号**，跨表比大小没有意义（实测同一夹具里字段行号 94/95、方法行号 282/283）；
  `Type.GetMembers()` 也不按声明顺序返回。故「把方法节点插到它声明所在的字段之间」做不到
  ——按钮默认排在字段之后（`[PropertyOrder]` 提供显式出口，方法也收）。想按声明顺序排方法，
  只能用**同一张表内**的行号（低 24 位）。
  三条测量都有用例钉着（`MethodNodeTests`），Unity 哪天换了行为会先红
- **既无绘制器也无处理器的特性会被自动接管判据漏掉**。生命周期钩子
  （`[OnInspectorInit]` / `[OnInspectorDispose]` / `[OnStateUpdate]`）就是这样一类：
  它们不产生节点、不配绘制器，漏掉的症状是「类型不被接管、特性静默不生效、零告警」。
  新增这类特性时，让它实现 `XInspector.Internal.ITreeLifecycleAttribute`；
  「产生节点但不画也不改别人」的那一类（`[ShowInInspector]`）实现
  `XInspector.Internal.ITreeMembershipAttribute`；「不产生节点但改变排列」的那一类
  （`[PropertyOrder]`）实现 `XInspector.Internal.ITreeOrderingAttribute`。**同时**：
  `IsUsedBy` 的扫描范围要与成员收集的范围对齐——它漏过一次方法（`[Button]`），
  又漏过一次属性（`[ShowInInspector]`），还漏过一次「标在**字段类型**上」（类级
  `[InlineProperty]`，判据与注入必须看同一处）
- **`Object[]` 改 `object[]` 会静默弄丢一条白送的语义**。形参类型一旦是 `object`，
  裸写 `target != null` 就退化成引用比较，而 Unity 的已销毁对象恰恰是「引用不为 null、
  按它自己的语义却是空」——以前这是 `Object[]` 白送的（`!= null` 自动走 Unity 的重载）。
  属性树的目标列表正是这么改型的，故判空一律走 `TargetObjects.IsAlive`。
  **改型时要问的不只是「哪里编译不过」，还有「哪些语义是那种类型免费给的」**
- **`Undo.GetCurrentGroupName()` 不能当「记没记 Undo」的判据**。组名只在显式
  `SetCurrentGroupName` 之后才有，`RecordObjects` 不给它命名——于是
  「断言 != 我们给的名字」在任何情况下都成立，是个恒真的空断言（本仓曾有一条）。
  可观测的判据是：先记一步已知可撤销的（撤销栈因此非空、行为确定），再来一步不记的，
  撤一次看收回的是哪一步
- **表达式树在本仓的运行环境里能访问私有成员**（11 例 spike 实测过，不必退到
  `DynamicMethod(skipVisibility: true)`）。故反射成员与反射条件的取值都编译成委托，
  构建期一次、绘制期只剩委托调用——`MethodInfo.Invoke` 出现在每帧路径上同样是违规的
- **`Editor/` 目录下的 MonoBehaviour 挂不成组件**（原话：it is an editor script… it needs to be
  outside the 'Editor' folder），而脚本要**进资产**又非得有 MonoScript（文件名 ↔ 类名对得上）。
  测试程序集是 editor-only，两条一起命中时只能二选一：本仓的夹具一律让类名与文件名**不一致**
  ——那样能 `AddComponent`，但进不了预制体资产（夹具注释里写着「别把它抽成同名文件」）
- **预制体测试夹具要按创建顺序倒着删资产**：变体引用基预制体、外层预制体嵌着内层预制体，
  先删被依赖的那个会让 Unity 立刻重导入引用方并报 Missing Prefab **错误**，
  而测试框架把错误日志算成失败——报错那条用例看着像断言失败，其实断言早过了
- **测试夹具不得在注册表面前「故意违规」，除非它在测试程序集里**——类型发现走
  `EditorTypeScanner`，它扫**所有**已加载程序集（那是「使用方零注册扩展」的前提，两条
  `Registry_Discovers*FromThisAssembly` 用例守着），但**对测试程序集里不可实例化的类型静默跳过**
  （判据是引用了 `nunit.framework`）：告警的收件人是写坏了自己扩展的使用方，不是故意违规的夹具。
  **只收窄告警、不收窄扫描**——判据若被用来决定注册范围，误判会让使用方的绘制器静默不注册
- **「这个节点属于哪个对象」不要默认成根。** 嵌套层的取值/调用走一条构建期编译的字段链
  （`NestedInstanceScope`），容器沿父链上溯、**跳过分组节点**取父字段的实例；顶层才是根。
  默认成根的症状是「取值/调用看的是另一个对象」——静默且极难归因（嵌套字段上的按名回调曾
  因此在根上找到了同名方法）。产出的必须是**每帧现读的访问器**而不是绑死的实例：
  延迟回调（右键菜单还挂在屏幕上时用户可以改字段）尤其如此。
  **集合元素节点也是容器**（`Kind.Member`、路径可含 `items.Array.data[i]` 索引段——
  访问器自 2026-10-06 起认它；越界/空集合/空元素时给出「取不到实例」而不是默认值）。
  路径访问器还要对 **null 中间段做空传播**——托管对象与序列化数据不同，前者真的可以是 null，
  漏了会**每帧**抛 NRE。
- **`CloneForPath` 只改写 `GroupID`，构造期从它算出来的常量不会跟着走**。给路径**加前缀**
  是本包的常规操作（类级分组分发、嵌套层分组装配都加），于是凡把「路径的派生量」存成只读
  字段的地方都要问一句「加前缀后它还成立吗」。`TabGroupAttribute.IsContainer` 就栽在这里
  ——它比的是 `GroupID == TabsGroupID`，而后者是构造期常量；症状是**页签栏整个不画**、各页
  内容顺次摊开，且**潜伏了很久**（类级分组 + `[TabGroup]` 早就中招，只是没人试过）。
  判据要写成对前缀免疫的形式（按段收尾），或干脆别存派生常量
- **判据要看「标特性的那一处」，不是「看着像」的那一处。** `[SerializeReference]` 标在**字段**上
  （用法声明就是 `AttributeTargets.Field`），而展开判据里那句「多态引用不展开」写的是
  「字段的**声明类型**上有没有它」——**恒为假**。它长年没出事只是因为另一道闸顺手挡着
  （这类字段在序列化属性上报 `ManagedReference`，过不了「要求 Generic」那一关；
  2026-10-06 用一条**测量**用例把这条事实钉下来，Unity 换行为时它先红）。
  症状是「判据看起来在、实际从没生效过」——它是「判据漏一半」的另一面：
  漏一半造出静默失效，**恒为假**则会在另一道闸改掉的那天突然开始出效果。
  一处判据被两处以上问到（展开判据、构建期告警、路径拒绝）时，**收成一份读**，
  别各写一遍——各写一遍的迟早有一处先漂
- **`ApplyModifiedProperties` 必须用「设值的那一个」`SerializedObject` 调。** 在另一个
  `SerializedObject` 的句柄上设了值、却拿原来那个去 Apply，改动**不会落盘**——而这个失败
  是静默的，很容易被读成「Unity 的这个 API 清不掉值」。（2026-10-07 写 L7 探针时真踩了
  一次，差点把夹具 bug 记成一条 Unity 行为。）推论：**测量用例先要怀疑自己**——
  「实测」这两个字的分量，取决于那一次测量有没有先排除掉夹具本身。
- **访问器的 `ValueType` 是末段的「静态类型」，不保证是实例的「运行时类型」。** 多态引用
  （`[SerializeReference]`）收尾的路径上，声明类型常常是接口/抽象类——拿它去按名找成员/方法，
  症状是「按钮报找不到方法、反射成员静默算『不一致』」。要实例类型走
  `NestedInstanceScope.InstanceTypeOf`（只在末段确是多态引用时构建期探一次）。
  **同款第二条：别把「三处一样的代码」顺手合并成一句。** 按名解析的三个调用点顶层口径并**不**
  一致（`ButtonProcessors` 走 `TargetObjects.IsAlive`，另两处走 `targets[i]?.GetType()`）——
  合并进一个函数会悄悄改掉「已销毁目标」的判活语义，而没有任何编译器提醒。

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

**包内不指向包外：** 包内 README **与源码注释**都不引用 `Assets/XInspector/` 之外的东西
——第三方装了包却打不开它。反方向（沙盒 → 包内）不受限，维护者两边都看得到。

曾踩过一次：`OverviewComponent` 的注释写着「更直观的演示在 `Assets/Sandbox/AttributeDemo.cs`」，
那个路径在第三方那里根本不存在。它的成因是同一件事被两个文件夹各讲了一遍——
**重复的演示迟早会互相引用**，所以遇到这类注释，先看是不是该把重复消掉。

**同一件事写在两层文档里，迟早互相矛盾。** 包内 README（使用方）与维护者文档各写一遍时，
两句话在当时都对；等其中一处的前提变了（添了一条能力、收窄了一条边界），就会出现
「Runtime/README 说着会回落到反射成员，Editor/README 说着不走反射那两级」这种**面对面相反**
的两句，且**两边都没有告警**。发现矛盾时先问**「哪一句是现在的实现」**，再问
**「另一句当初为什么那么写」**——只改一边会把另一边的理由弄丢。改完把这一处的来历写进
Pipeline（哪一句写于哪个前提之下），否则下一轮还会按同样两条线索推一遍。

## Git

- 提交信息除代码关键字外一律中文，**不加 `Co-Authored-By` 之类署名尾注**，**不自动推送**
- **计划阶段即定原子提交边界与每个提交的验证命令**；**计划批准即授权本阶段的全部提交**，
  每个提交跑绿后直接提交，不必逐次请示
- 两类例外仍须单独请用户确认：**公开 API 变更**、**破坏性变更**（含仅对仓内的）
- **规则存档：** 讨论中若产生可固化为长期约定的规则，先向用户提示拟写入的文本，
  经确认后方可写进本文件；未确认不擅自修改

## 明确不在本轮范围

样式系统（配色与图标那一层；**与 `[ColorPalette]` 无关**——那个已做）、
折叠状态的跨会话持久化、UI Toolkit。

**自研序列化（以及一切要求它的东西）不做**——这是 2026-10-07（第二十五批）核验后的规则：
字典与矩阵**不随资产存档**（`[DictionaryDrawerSettings]` `[TableMatrix]` 判 ⛔），
因为 Unity 根本不序列化它们；要做得先有一套自己的序列化器，而那会与六个定盘决定之首
（值后端是 `SerializedObject`）正面冲突——它买下的五件事（Undo、预制体覆盖、场景标脏、
多对象编辑、域重载后取值）全都要重做。**若哪天要做，先立项**（范围 / 边界 / 验收 / 第一步）。

**L7 的 Inspector 半边不属于这一条**：多态引用进管线 + 自绘选择器一族是**一条能走的
能力轮**（`[SerializeReference]` 字段今天已经由 Unity 原生画着，缺的只是本包的特性作用进去；
子字段有独立句柄、`managedReferenceValue` 是可写的活实例，**实测**）。
`[TypeDrawerSettings]` 也随之**不再记在「缺通道」上**——那条通道已经有了，
代价是使用方要在字段上加一个 `[SerializeReference]`（裸 `System.Type` 字段进不了树）。
该不该做、边界与第一步见 Roadmap §十二；核验证据见 Pipeline §二十九。
（`[InlineEditor]` / `[PreviewField]` / `[FilePath]` 这类重型绘制器**已做**——L1b 整层清完；
**L5 的按钮族与回调族**、**L3 的反射值后端**、**L2 的收尾**、**L4 的顺序与内联**、
**L6 第一批的集合容器与表格**、**嵌套类型成员节点化**、**嵌套层的分组装配**均已做完
——分别见 Pipeline §八/§九/§十一/§十二/§十三/§十四/§十五。
**嵌套层的读路径也已做完**（2026-10-06，第三条能力轮）：`[ShowInInspector]`、条件族指向
反射成员/方法的两级、`[Button]` 一族与按名回调族**在嵌套层全部生效**，见 Pipeline §十六。
**L6 收尾两条也已做完**（2026-10-06，第十四批，特性计数 84 → 86）：`[OnCollectionChanged]`
与 `[Searchable]`——两条都**不需要元素节点化**（前者落在既有的增删施加点上、后者过滤的是
行与节点），见 Pipeline §十七。
**集合元素节点化也已做完**（2026-10-06，第十五批，第四条能力轮）：元素按需成为真节点，
元素类型里的条件、分组、顺序、内联随之生效；元素层是 `arraySize` 的同步投影
（绘制前对账、对不上整层重建）——「节点数与元素数不一致」这个问题**不存在**，
新契约是**元素节点不跨结构变更**，见 Pipeline §十八。
**元素层的读路径也已做完**（2026-10-06，第十六批，第五条能力轮）：元素里的
`[ShowInInspector]`、条件族指向元素实例的反射成员/方法、`[Button]` 一族与按名回调全部生效
（路径访问器认 `Array.data[i]` 索引段；值类型元素上的方法一律拒绝），见 Pipeline §十九。
**类级分组进嵌套层与元素层也已做完**（2026-10-06，第十八批，第六条能力轮，特性计数 +0）：
类型自己带的 `[BoxGroup]` 一族（含 `[ShowIfGroup]`/`[HideIfGroup]`）分发到它的成员
（元素层每个元素各是各的，路径带 `items.Array.data[i]` 前缀），两处「只在最外层类型上收集」
的构建期告警撤除，见 Pipeline §二十一。
**元素层深度 > 1 也已做完**（2026-10-06，第十九批，第七条能力轮，特性计数 +0）：两道守卫
（类型链去重挡自引用、层数预算 4 挡过大类型链）取代「只做一层」，重建期递归与登记簿注销
补齐正确性，顺带修掉搜索行掩码串掩码的既有缺陷，见 Pipeline §二十二。
**成员引用收成一层也已做完**（2026-10-06，第二十批，第八条能力轮，特性计数 +0）：四级阶梯
（嵌套同层 → 根绝对名 → 反射字段/属性 → 无参方法）从条件族里搬进 `MemberReferenceResolver`，
`[ToggleGroup]` 的开关与 `[MinMaxSlider]` 的边界随之可指向反射成员（开关是禁用复选框 +
标题说明），见 Pipeline §二十三。**OdinGap 的「仍欠」至此清零**；
**仍欠**：无——剩下的缺口全部是 L7 前置或卡在设计。）

**特性处理器层已做**（`Editor/Processors/`），条件族做了 `[ShowIf]` `[HideIf]` `[EnableIf]`
`[DisableIf]`、四个模式变体（`[HideInEditorMode]` `[HideInPlayMode]` `[DisableInEditorMode]`
`[DisableInPlayMode]`）、三个内嵌环境条件（`[ShowInInlineEditors]` `[HideInInlineEditors]`
`[DisableInInlineEditors]`），外加类级分组分发。
**这 11 个条件特性的 `AttributeUsage` 含 `AttributeTargets.Method`**——判据是
「方法会产生属性树节点而普通属性不会」，放宽的只该是真正会生效的那一侧
（`[Button, DisableIf(nameof(alive))]` 靠它才编译得过）。

**分组条件已做**（`[ShowIfGroup]` / `[HideIfGroup]`，2026-10-05）：它当初卡在「分组节点在
处理器阶段之后才创建」——本轮给构建期补上了那个阶段（见「架构要点」第 7 条的第二趟）。
**跨对象条件 `"@other.field"` 核过之后判不做**：`"@this.*"` 那半已被「条件名可以是
`a/b` 嵌套路径」覆盖，剩下那半（借成员持有的对象实例去读它的字段）本包没有读路径，
多对象语义也未定。
（**条件为方法或普通属性已做**——L3 起按「序列化成员 → 反射字段/属性 → 无参返回 bool
的方法」三级解析，构建期绑委托、绘制期不反射。）
（**预制体上下文四条件已做**——`[ShowIn]` / `[HideIn]` / `[EnableIn]` / `[DisableIn]` 接
`PrefabKind` 位标志，判据每帧现读；同一块探测还解锁了两个原先判 ⛔ 的校验特性
`[RequiredIn]` / `[DisallowModificationsIn]`。两处与官方的偏差：**模型预制体归 `Regular`**、
**普通 C# 对象与非预制体资产没有上下文**。）

**编辑器窗口基类已做**（`Editor/Windows/XInspectorEditorWindow`，默认画窗口自身的序列化
字段，外加带 `[ShowInInspector]` 的成员）。**`GetTarget()` 已做**（L3）：可返回任意类型
实例——不必可序列化、不必是 `UnityEngine.Object`，那种目标只收带标记的成员且不可重置。
**仍不做**：带对象选择器的浮空 Inspector、字段拖拽重排、窗口内 Undo。
（窗口上的 `[Button]` 方法**能**进树——成员过滤只管字段。）

**按钮族与回调族已做**（`Runtime/Attributes/Buttons/`、`Callbacks/`），共 10 个特性 + 1 个枚举。
**仍不做**：`[Button]` 的 `ButtonStyle`／像素高度／图标一族／布局一族；参数里的 `ref`/`out`、
数组与泛型方法；嵌套 `[Serializable]` 类型里的按钮（只对根目标对象生效）；
`[OnInspectorGUI]` 标在字段上的形式；三个回调的 `action` 变体（本包只认本类型上的方法名）。
