# 与 Odin 的缺口

**对照口径：** Odin 官网 [attributes 页](https://odininspector.com/attributes)
（12 个分类、**108 个不重复特性**）与
[editor-windows 页](https://odininspector.com/editor-windows)，
抓取日期 **2026-10-03**（**2026-10-07 复核过一次**：属性页仍列出 108 个链接，去重后数目不变；
那 8 个缺口的类签名与语义原文于当日逐个重抓过）。Odin 会变，这份对照至少每半年该重核一次。

> 页面上的分类条目数是 110+，但**多处重复计数**（`[Required]` 同时在 Essentials 与
> Validation 下，`[TableList]` 同时在 Type Specifics 与 Collections 下）。
> 下面所有数字都用**去重后**的 108。
>
> **2026-10-04 复核：** 这两处原写 109，去重后实际是 **108**——本轮把官网 attributes 页的
> 特性链接去重后逐个与下面 12 张表比对，**108 对 108，一个不多一个不少**（唯一的两处字符串
> 差异是 Odin 自己的 URL 拼写：`enable-guiattribute`、`on-inspector-guiattribute` 各少一个连字符，
> 对应的类名仍是 `[EnableGUI]`/`[OnInspectorGUI]`）。故「109」是当初的笔误，
> 下面的表一直是对的。

**本项目的家底（2026-10-07，第二十五批之后）：**

- 公开特性 **88 个**（清单见文末「总账」），分十一族：分组与条件、状态与门控、标签与外观、
  值绘制、校验与钳制、**预制体上下文**、按钮、回调、**反射成员**、**结构与顺序**、
  **集合与表格**，另有调试 1 个
- 自定义分组的公开基类 `PropertyGroupAttribute`（外加自建枚举 `TitleAlignments`、`ButtonSizes`、
  **`PrefabKind`**）
- 特性处理器层（`AttributeProcessor`）与条件求值分离（构建期解析、绘制期求值）；
  **处理器分两趟跑**——处理分组特性的那些在**分组装配之后**（只对分组节点）跑，
  这是 2026-10-05 为此类需求补上的那个阶段
- **方法节点**：树上第一次出现没有值的节点（`InspectorPropertyKind.Method`）
- **反射成员节点**：树上第一次出现值不来自 Unity 序列化的节点（`InspectorPropertyKind.ReflectedMember`），
  配套**第二套值后端** `ReflectedValueEntry`（只读），以及把成员编译成委托的 `ReflectedAccessor`
- **预制体种类探测**（`PrefabKindResolver` 纯映射 + `PrefabContextProbe` 薄探测）：
  第一次出现「按被检视对象所处的 Unity 上下文开关成员」的判据，也是第一次由一块基础设施
  同时解锁条件族与校验族（`[RequiredIn]` `[DisallowModificationsIn]` 当初正是判在
  「共用一块尚不存在的基础设施」上）
- 三个标记接口各司其职：`ITreeLifecycleAttribute`（不产生节点）、`ITreeMembershipAttribute`
  （产生节点但不画也不改别人）、自定义分组的 `PropertyGroupAttribute`（抽象基类）
- 窗口基类 1 个（`XInspectorEditorWindow`，默认画自身字段，覆写 `GetTarget()` 可检视任意对象）
- **嵌套层能力**（2026-10-06，两条**能力轮**、特性计数 +0）：`[Serializable]` 嵌套类型的成员
  **按需**成为真节点（安全阀是「没用到本包的类型整份照旧交给 Unity」），嵌套层里的条件、
  顺序、内联与**分组**随之全部生效——分组节点的路径以父字段的序列化路径为前缀
  （`stats/基础`），同一个类型用在两处时两处各是各的。
- **嵌套层的读路径**（2026-10-06，第三条能力轮、特性计数 +0）：`[ShowInInspector]`、
  条件族指向反射成员/方法的两级、`[Button]` 一族与按名回调族**在嵌套层全部生效**——
  取/调的是**那个嵌套实例**（由一条构建期编译的字段链每帧现读）。技术核心是
  「Unity 拿不到嵌套托管实例，只能用 `targetObjects` + 按 `propertyPath` 编译式下钻」。
  两条边界：**值类型实例上不调用方法**、嵌套实例为空时读值显示「—」。
- **集合元素节点化**（2026-10-06，第四条能力轮、特性计数 +0）：集合的**元素**成为真节点
  （`Kind.Member`、路径 `items.Array.data[i]`），元素类型里写的条件、分组、顺序、内联随之生效。
  先决问题（「节点数与真实元素数不一致时怎么办」）的答案是**让不一致不存在**：元素层是
  `arraySize` 的**同步投影**，每趟绘制前对账、对不上整层重建——「元素路径身份不稳定」因此
  没有发生。安全阀是七项合取（元素类型用到本包 + 集合被本包接管 + 非表格 + 深度 1 …），
  **用到了本包却被挡住的一律构建期告警**（不静默）。新契约：**元素节点不跨结构变更**。
- **元素层的读路径**（2026-10-06，第五条能力轮、特性计数 +0）：元素里的 `[ShowInInspector]`、
  条件族指向元素实例的反射成员/方法/非序列化字段、`[Button]` 一族与按名回调**全部生效**——
  取/调的是**那个元素实例**。技术核心是 `ReflectedAccessor.TryCreatePath` 认 `Array.data[i]`
  **索引段**（判据落前置类型、越界守卫、末段给 null、不占深度预算）；元素阀随之放宽到
  「非序列化那一半」——只带 `[ShowInInspector]`/`[Button]` 的元素类型也会被接管（**外观变化**）。
  边界：值类型元素上的方法一律拒绝、元素为空时读值「—」/条件算假/按钮跳过、
  ~~搜索按元素内的反射值匹配不做~~——**2026-10-07（第二十二批）已做**，见 Pipeline §二十六。
  这本是这一段里最后一条边界，故第五条能力轮的边界至此清零。
- **类级分组进嵌套层与元素层**（2026-10-06，第六条能力轮、特性计数 +0）：类型自己带的
  `[BoxGroup]` 一族（含 `[ShowIfGroup]`/`[HideIfGroup]`）分发到它的成员，路径带容器的序列化
  路径前缀（`stats/类级组/成员自有组`——类级恒在最外层），同一类型两处各是各的；判据与注入
  同源（`HasEffectiveClassLevelGroup` 一份读，罩住展开判据、两条递归判据、自动接管与元素阀）；
  成员全部自带分组时，分组条件的类级声明经**容器链回退**照样生效。**两处构建期告警撤除**；
  **行为变化**：带类级分组的嵌套/元素类型从「整份交回 Unity」变为被本包接管。
  边界：只有**分组族**走这条（`[Title]` 之类仍只在被检视类型上生效、不告警）；
  值类型标不了类级分组（`AttributeUsage` 只到 `Class`，**编译不过**、不是静默）。
- **元素层深度 > 1**（2026-10-06，第七条能力轮、特性计数 +0）：元素**里面**的集合也按需
  节点化（`List<Room>` 里 `Room` 的 `List<Enemy>`），路径每层一对 `Array.data[i]`（读路径
  对多组索引对天然支持）；**两道守卫**（构建期告警、不静默）：**类型链去重**挡自引用/互递归、
  **层数预算 4**（本包自定值）挡过大类型链——预算挡的是**类型链**、不挡数据规模；
  重建期递归（外层增删 → 内层跟着重建并重新登记）、被挡告警按字段去重。
  **行为变化**：元素类型里的集合从「整份交回 Unity」变为按需递归节点化。
- **成员引用收成一层**（2026-10-06，第八条能力轮、特性计数 +0）：「按名找成员」的四级阶梯
  （嵌套同层 → 根绝对名 → 反射字段/属性 → 无参方法）从条件族里搬进
  `Editor/Internal/MemberReferenceResolver`，`[ToggleGroup]` 的开关与 `[MinMaxSlider]` 的
  动态边界随之**可指向反射成员**（开关画禁用的复选框 + 标题说明，门控照常）。
  当时留下的两处例外里，`[Toggle]` 的开关**仍在值对象内部**（那一格没有实例句柄），
  另一处 `[ValueDropdown]` 的数据源**已于下条结案**。
- **`[ValueDropdown]` 的反射数据源**（2026-10-07，第二十一批、特性计数 +0）：数据源多了
  第二种形态——声明类型**实现 `IList`** 的**普通字段 / 属性 / 无参方法**（Odin 里最常见的
  `[ValueDropdown(nameof(GetOptions))]`），序列化的数组 / List 照旧；名字解析走条件族那条
  四级阶梯。只实现 `IEnumerable` 的源（`HashSet`、LINQ 查询…）**不收并明确告警**——
  `string` 也只实现 `IEnumerable<char>`，放宽会静默变出「字符选项表」。配套写了一条
  **object → `SerializedProperty` 的写回通道**（`ReflectedValueCopier`），枚举判据与既有的
  「句柄 → 句柄」那条**物理共用一份**（`EnumIdentity.SameNames`）。
  **L0–L6 至此连最后的遗留也收口**：剩下的缺口不是 L7 本体就是它的前置
  （「卡在设计」那一列已归零，见下文 L7 段）。

---

## 一张表不够：为什么要分两层来看

**只按 Odin 的 12 个分类罗列，会得到「缺 94 个特性」这种没有信息量的结论**——
它把「加一个类」和「加一层基础设施」混为一谈。真正决定成本的是**每个缺口需要什么前提**，
所以先按那个分层：

**最大的那一层（L1a，约 40 个特性）零新基础设施**，全是 `AttributeDrawer<T>` +
`CallNextDrawer`，`PropertyState` 与 `PropertyValueEntry` 已经够用。换句话说，
**Od​in 近一半的「特性」在本项目里是「加个类」而不是「加个层」**——
这反过来验证了绘制器链那套架构的取舍。

分层用于**排序**，逐条表用于**查漏**。两张都在，各回答各的。

---

## 分层

| 层 | 缺口 | 前提 |
|---|---|---|
| **L0** | Unity 原生装饰器：`[Range]` `[Space]` `[TextArea]` `[Multiline]` `[Header]` `[Tooltip]` | **无需工作**——由 `PropertyField` 绘制。✅ 结构侧已实测（2026-10-03），见文末「L0 的验证记录」 |
| **L1a** | 21 个十几行的特性 | **零新基础设施** ✅ **已清完**（2026-10-04） |
| **L1b** | 分组族与重型值绘制器 | **✅ 整层已清完**（2026-10-04）：分组族 6 个、值绘制器 6 个，收尾的 `[InlineEditor]` 一族（含三个内嵌环境条件）也落地了 |
| **L2** | 条件族 + 类级分组分发 | ✅ **整层已清完**（2026-10-05）：分组条件族（`[ShowIfGroup]` / `[HideIfGroup]`）落地，构建期补上「分组装配之后」的第二趟处理器；跨对象条件核过之后判 ⛔ |
| **L3** | `[ShowInInspector]`、窗口的 `GetTarget()` | 反射值后端（第二套 `PropertyValueEntry`）✅ **已做**（2026-10-04）；~~`[TypeDrawerSettings]` 经核实独立，仍缺~~ **2026-10-06 追记：改判 L7 前置**——它要的是「可写的 `System.Type` 通道」，本包只有只读反射后端（见 L7 行与逐条表） |
| **L4** | `[PropertyOrder]` `[InlineProperty]` | ✅ **整层已清完**（2026-10-05；`[InlineProperty]` 于 2026-10-06 升到**完全体**——复合类型的子字段成为真节点，当初那条推迟项就此复活了「固定形状」那一半） |
| **L5** | `[Button]` 家族、回调族、`[CustomContextMenu]` | 拿到目标对象并调用方法 |
| **L6** | `[ListDrawerSettings]` `[DictionaryDrawerSettings]` `[TableList]` `[TableMatrix]` `[OnCollectionChanged]` `[Searchable]` `[AssetList]` | 🟡 **容器、元素与资产列表都做了**：容器（自绘列表 + 表格 + 长度校验，2026-10-05）、集合回调与搜索（2026-10-06）、**元素节点化 + 元素层的读路径 + 元素层深度 > 1**（三条能力轮）、**`[AssetList]`**（第十七批）已落地；**仍缺**：字典与矩阵（前置是 L7——Unity 根本不序列化它们，字段进不了树） |
| **L7** | **2026-10-07 核验后拆成两半**（见 L7 一节与 [Pipeline §二十九](Modules/Pipeline.md)）：**Inspector 半边**（多态引用进管线 + 自绘选择器一族，含 `[TypeDrawerSettings]`——那条「可写的 `System.Type` 通道」**已经有了**，代价是字段要加 `[SerializeReference]`）是**一条能走的能力轮**；**Serializer 半边**（字典、矩阵要随资产存档）**判不作为**——Unity 根本不序列化它们，要做得先有自研序列化器 | **Inspector 半边：本包的活；Serializer 半边：Odin 的另一半产品** |

---

## 逐条对照

**两张视图回答两个不同的问题。** 上面的分层回答「要多少成本」，这张表回答「具体是哪些」。
前者用于排序，后者用于查漏——只看这张表会得出「缺 94 个」这种没有信息量的结论，
只看分层则可能漏掉某个具体特性。

**表的组织：** 按 Odin 自己的分类，但**每个特性只登记一次**（记在它首次出现的分类下），
否则 Odin 的重复计数会让总账对不上。Odin 也把它归入其它类时在「另见」列注明。

状态四种：**✅ 已实现**、**❌ 缺**、**⛔ 不做**（已评估并记下理由，见
[Modules/Pipeline.md](Modules/Pipeline.md) 第五、六节与 §二十九）、
**➖ 不需要**（Unity 自己的，由 `PropertyField` 绘制）。
标 ➖ 的那 4 项曾建立在一条未验证的推断上，已于 **2026-10-03 做过结构侧实测**
（见文末「L0 的验证记录」）。

> **2026-10-04 更新：** L1a 与 L1b 分组族已整块落地（那一轮实现了 27 个特性：
> L1a 剩余 21 个 + 分组族 6 个）。
> 标 ⛔ 的 9 项是那一轮**核对签名后决定不做**的，理由逐条记在
> [Modules/Pipeline.md](Modules/Pipeline.md) 第五节——「不做」也是结论，不写下来
>就会被下一轮重新猜一遍。
>
> **同日晚些的第三批核对**（[Pipeline.md](Modules/Pipeline.md) §六）逐条核了 L1b 剩余的
> 值绘制器，结果：`[Searchable]` `[AssetList]` 实为 **L6**、`[TypeDrawerSettings]` 实为 **L3**、
> `[TypeFilter]` 实为 **⛔**（resolved string + 抽象类型字段）、
> `[ColorPalette]` **卡在「调色板从哪来」的设计上**。这 5 项原先记在 L1b 下，是**层判错了**。
> （2026-10-06 再核：`[TypeDrawerSettings]` 由 **L3 改判 L7 前置**——见 L3 段末追记。）
>
> **同日的第四批核对与收尾**（[Pipeline.md](Modules/Pipeline.md) §七）：`[InlineEditor]` 一族
> （4 个特性）落地，**L1b 整层清完**。本批有两处「第一次」：签名里**第一次没有 resolved string**；
> 也是第一次出现**本包自定值**（嵌套上限、预览默认尺寸与位置）与**与 Odin 的语义差异**
> （`CompletelyHidden` 空值时给一行灰字提示而不是留白）。三条自定值都写进了 README 与展示台。
>
> **同日的第五、六批**（[Pipeline.md](Modules/Pipeline.md) §八/§九）：L5 按钮与回调两批、
> 随后是 **L3 反射值后端**（`[ShowInInspector]`、窗口 `GetTarget()`，外加 L2 剩余的
> 「条件指向普通属性/方法」）。家底 68 → 69，缺口 26 → 25。
> 三处与 Odin 的差异在这一轮固定下来：反射成员**只读**、静态成员会显示、
> `[ShowInInspector]` **不标方法**（编译期报错而不是静默）。
>
> **同日的第七批**（[Pipeline.md](Modules/Pipeline.md) §十）：**预制体上下文族** 6 个特性落地
> ——四个条件 `[ShowIn]` `[HideIn]` `[EnableIn]` `[DisableIn]` 加两个原先判 ⛔ 的校验
> `[RequiredIn]` `[DisallowModificationsIn]`。家底 69 → 75，缺口 25 → 21，⛔ 10 → 8，
> **Conditionals 一类的缺口清零**。它把「签名未核」这个前提消掉之后，又暴露出三件当初没料到的事：
> 种类探测本身是真的难（隔离编辑模式里资产类型判定失效，得排到阶梯第一位并绕道资产路径）、
> 官方有三处没写明只能选兜底、以及**预制体夹具是本仓第一处往盘上写资产的测试**。

### Type Specifics（24）

| 特性 | 状态 | 层 |
|---|---|---|
| `[AssetList]` | ✅ 已实现 | 2026-10-06（L6，第十七批）：**两形态都做**（列表 + 单元素）；只声明 `Path`/`AssetNamePrefix` 两个旋钮，官方另外四个不声明（见 Pipeline §二十） |
| `[AssetSelector]` | ✅ 已实现 | 透传型：小按钮 + 编辑器自带菜单（无搜索框/图标/多选，见 Pipeline §六） |
| `[ChildGameObjectsOnly]` | ✅ 已实现 | — |
| `[ColorPalette]` | ✅ 已实现 | 2026-10-07（第二十三批）：调色板来源定为**工程内 ScriptableObject 资产**（`XInspectorColorPalette`），按**资产名**查找；无参形态用工程里**唯一**那份，多份或零份时告警并退回；**透传型**绘制器（色块行 + 原生颜色字段） |
| `[DisplayAsString]` | ✅ 已实现 | — |
| `[EnumPaging]` | ✅ 已实现 | — |
| `[EnumToggleButtons]` | ✅ 已实现 | — |
| `[FilePath]` | ✅ 已实现 | 只作用单个 `string`；`$` 成员引用不做（另见 Validation） |
| `[FolderPath]` | ✅ 已实现 | 与 `[FilePath]` 同形（另见 Validation） |
| `[HideInInlineEditors]` | ✅ 已实现 | 被动随 `[InlineEditor]` 落地 |
| `[HideInTables]` | ✅ 已实现 | 空标记；让成员不进 `[TableList]` 的表格（单独用时惰性） |
| `[HideMonoScript]` | ✅ 已实现 | — |
| `[HideNetworkBehaviourFields]` | ⛔ 不做 | 目标类型（UNet `NetworkBehaviour`）在 Unity 6 已不存在，只能做成静默 no-op |
| `[HideReferenceObjectPicker]` | ❌ 缺 | **L7-Inspector，可做**（2026-10-07 核验）：官方原话是「hides the polymorphic object-picker shown above the properties of non-Unity serialized reference types」——**它是挂在 Odin 自己的绘制器上的开关**，本包要它得先有那个绘制器。**不需要自研序列化器** |
| `[InlineEditor]` | ✅ 已实现 | 六模式 + 四对象字段模式 + 预览；递归上限等自定值见 Pipeline §七 |
| `[MultiLineProperty]` | ✅ 已实现 | — |
| `[PreviewField]` | ✅ 已实现 | 默认高度/默认对齐由本包定；两个 `FilterMode` 重载永久否决 |
| `[PolymorphicDrawerSettings]` | ❌ 缺 | **L7-Inspector，可做**：官方原话「Provides options for **Polymorphic Fields rendered using Odin**」——旋钮挂在原生多态绘制器上（`CreateInstanceFunction` / `ReadOnlyIfNotNullReference` / `NonDefaultConstructorPreference` / `ShowBaseType`）。**不需要自研序列化器**，需要**自绘选择器** |
| `[TypeDrawerSettings]` | ❌ 缺 | **L7-Inspector，可做——2026-10-07 第三次改判**。这一项挪过三次层（L3 → L7 前置 → 这里），前两次问的都是「**有没有**一条可写的 `System.Type` 通道」，**本轮实测给出了第三种答案：通道在，只是要求使用方在字段上加一个 `[SerializeReference]`**——裸 `System.Type` 字段进不了序列化数据（`FindProperty` 为 null），加了之后就是 `ManagedReference`，**写得住、清得掉、进撤销栈**。原来的判断不是错，是**问题问窄了**：把「要改个声明才能写」与「写不进去」归成了同一格。真实前提仍是「约束候选集」，而候选集没有公开入口（`TypeSelectionList` 是 internal）⇒ 要**自绘选择器** |
| `[SceneObjectsOnly]` | ✅ 已实现 | — |
| `[TableList]` | ✅ 已实现 | 表格呈现；**不配绘制器、配处理器**（构建期建列模型并补一份 `[ListDrawerSettings]`） |
| `[TableMatrix]` | ⛔ **不作为** | **Serializer 半边**（2026-10-07 核验）：Unity 根本不序列化多维数组（**实测**：`FindProperty` 为 null、零告警）。Odin 自己的原话是「继承 `SerializedMonoBehaviour` 只是为了让它替你序列化」——**要做得先有一套自己的序列化器**。本包判不作为（另见 Collections） |
| `[Toggle]` | ✅ 已实现 | — |
| `[ToggleLeft]` | ✅ 已实现 | — |

**小计：已实现 19 / 缺 3 / 不做 2**（2026-10-07 逐行重算——此前写着 18/5/1，
是 `[ColorPalette]` 转已实现后没跟着改；同日 `[TableMatrix]` 由「缺」改判**不作为**，
缺 4→3、不做 1→2。2026-10-06：`[AssetList]` 转已实现（两形态）；
2026-10-05：`[TableList]` `[HideInTables]` 转已实现；
2026-10-04：`[AssetSelector]` `[FilePath]` `[FolderPath]`
`[PreviewField]` 四项转已实现；同日 `[InlineEditor]` `[HideInInlineEditors]` 转已实现）

### Essentials（19）

| 特性 | 状态 | 层 |
|---|---|---|
| `[AssetsOnly]` | ✅ 已实现 | — |
| `[CustomValueDrawer]` | ⛔ 不做 | 唯一形态是 resolved string（方法调用）——归 L5 性质 |
| `[DelayedProperty]` | ✅ 已实现 | — |
| **`[DetailedInfoBox]`** | **✅ 已实现** | — |
| `[EnableGUI]` | ✅ 已实现 | — |
| **`[GUIColor]`** | **✅ 已实现** | — |
| **`[HideLabel]`** | **✅ 已实现** | — |
| `[PropertyOrder]` | ✅ 已实现 | 构建期稳定排序；`0` 是合法值；可标方法（按钮因此能插到字段之间） |
| **`[PropertySpace]`** | **✅ 已实现** | — |
| **`[ReadOnly]`** | **✅ 已实现** | — |
| `[Required]` | ✅ 已实现 | — |
| `[RequiredIn]` | ✅ 已实现 | 接 `PrefabKind`；`ErrorMessage` 只做纯文本（Odin 支持表达式） |
| `[Searchable]` | ✅ 已实现 | 2026-10-06（L6）：按**标签或值**过滤子成员与列表行；**不做**类型级形态、`Recursive`、`FilterOptions` |
| `[ShowInInspector]` | ✅ 已实现 | 第二套值后端：**只读**展示（`SetValue` 恒抛），标在方法上编译不过 |
| **`[Title]`** | **✅ 已实现** | — |
| `[TypeFilter]` | ⛔ 不做 | 唯一构造是 resolved string（样例里是方法），且被标注字段是抽象/接口类型——还需 L7 的类型切换。2026-10-04 核过签名后判定 |
| `[TypeInfoBox]` | ✅ 已实现 | — |
| `[ValidateInput]` | ⛔ 不做 | 同 `[CustomValueDrawer]`：resolved string + 校验消息层，归 L5 |
| `[ValueDropdown]` | ✅ 已实现 | 数据源两形态：**序列化的数组/List**，或声明类型**实现 `IList`** 的字段/属性/无参方法（2026-10-07 起；只实现 `IEnumerable` 的不收并告警）；只声明有真行为的选项（另见 Collections） |

**小计：已实现 16 / 缺 0 / 不做 3**（2026-10-07 重算——本节已无 ❌ 行；
2026-10-05：`[PropertyOrder]` 转已实现；
2026-10-04：`[ValueDropdown]` 转已实现、
`[TypeFilter]` 由「缺」改判「不做」；同日 L3 把 `[ShowInInspector]` 转已实现、
L2 收尾之一把 `[RequiredIn]` 从「不做」翻成已实现）

### Validation（15）

> Odin 在这一类下列 15 项，其中 8 项已在别处登记。这里是**本类独有**的 7 项。

| 特性 | 状态 | 层 |
|---|---|---|
| `[DisallowModificationsIn]` | ✅ 已实现 | 接 `PrefabKind`；「已经改过」用 `prefabOverride` 判（多选与反射成员不报） |
| `[MaxValue]` | ✅ 已实现 | — |
| `[MinMaxSlider]` | ✅ 已实现 | 只作用 `Vector2`；边界可取自成员——序列化成员、普通字段/属性或无参方法都行（2026-10-06 起，另见 Numbers） |
| `[MinValue]` | ✅ 已实现 | — |
| `[PropertyRange]` | ✅ 已实现 | — |
| `[Range]` | ➖ 不需要 | Unity 自己的（另见 Unity） |
| `[RequiredListLength]` | ✅ 已实现 | 只读 `arraySize` 的长度校验；表达式 getter 构造与 `PrefabKind` 不声明 |

**小计：已实现 6 / 缺 0 / 不做 0 / 不需要 1**（2026-10-05：`[RequiredListLength]` 转已实现；
2026-10-04：`[MinMaxSlider]` 转已实现；
同日 L2 收尾之一把 `[DisallowModificationsIn]` 从「不做」翻成已实现）

### Groups（12）

| 特性 | 状态 | 层 |
|---|---|---|
| **`[BoxGroup]`** | **✅ 已实现** | 含类级分发 |
| `[Button]` | ✅ 已实现 | 参数支持简单类型；参数区固定 CompactBox 形态 |
| `[ButtonGroup]` | ✅ 已实现 | 一行等分；默认组名照抄官方 |
| `[FoldoutGroup]` | ✅ 已实现 | — |
| `[HideIfGroup]` | ✅ 已实现 | 纯条件载体（不画东西）；组名兼条件名，可覆盖（另见 Conditionals） |
| `[HorizontalGroup]` | ✅ 已实现 | — |
| `[ResponsiveButtonGroup]` | ✅ 已实现 | 按标签宽度折行；本层唯一需要新布局基建的一个 |
| `[ShowIfGroup]` | ✅ 已实现 | 纯条件载体（不画东西）；组名兼条件名，可覆盖（另见 Conditionals） |
| `[TabGroup]` | ✅ 已实现 | — |
| `[TitleGroup]` | ✅ 已实现 | — |
| `[ToggleGroup]` | ✅ 已实现 | 开关可以是反射成员（2026-10-06 起）：那时复选框画成**禁用** + 标题一句说明，门控照常生效 |
| `[VerticalGroup]` | ✅ 已实现 | — |

**小计：已实现 12 / 缺 0**（2026-10-05：`[ShowIfGroup]` `[HideIfGroup]` 转已实现）

### Buttons（6）

> 本类 6 项中 5 项已在别处登记，独有 1 项。

| 特性 | 状态 | 层 |
|---|---|---|
| `[InlineButton]` | ✅ 已实现 | 只标字段；方法必须无参 |

**小计：已实现 1 / 缺 0**

### Misc（19）

| 特性 | 状态 | 层 |
|---|---|---|
| `[CustomContextMenu]` | ✅ 已实现 | 菜单在字段那一行；方法名收窄为本类型上的方法名 |
| `[DisableContextMenu]` | ⛔ 不做 | 右键菜单由 `PropertyField` 掌管、没有现成开关，需先原型验证可拦截 |
| `[DrawWithUnity]` | ✅ 已实现 | — |
| `[HideDuplicateReferenceBox]` | ❌ 缺 | **L7-Inspector，可做**（2026-10-07 核验）：官方原话「hide the reference box, if this property would otherwise be drawn as a reference to another property, **due to duplicate reference values being encountered**」，递归自引用时**照画不误**。判据是「值相同」——而 Unity 的 `managedReferenceId`（**public**）正好给出那份身份。**不需要自研序列化器**，需要那个绘制器 |
| **`[Indent]`** | **✅ 已实现** | — |
| **`[InfoBox]`** | **✅ 已实现** | — |
| `[InlineProperty]` | ✅ 已实现 | **完全体**（2026-10-06）：复合类型的子字段成为真节点；向量这类原生复合类型仍走观感派；类级形态标在**字段的声明类型**上（另见 Essentials/Layout） |
| **`[LabelText]`** | **✅ 已实现** | — |
| **`[LabelWidth]`** | **✅ 已实现** | — |
| `[OnCollectionChanged]` | ✅ 已实现 | 2026-10-06（L6）：两个方向夹住「写进序列化数据」那一步；只覆盖 Inspector 内的改动 |
| `[OnInspectorDispose]` | ✅ 已实现 | 不产生节点 |
| `[OnInspectorGUI]` | ✅ 已实现 | 只做标在方法上的无参形式 |
| `[OnInspectorInit]` | ✅ 已实现 | 不产生节点 |
| `[OnStateUpdate]` | ✅ 已实现 | 时机是本包自定：每趟 GUI 布局 |
| `[OnValueChanged]` | ✅ 已实现 | 只认本类型上的方法名；不支持的类型告警且不触发 |
| `[TypeSelectorSettings]` | ❌ 缺 | **L7-Inspector，可做**：官方原话「Provides options for **Type Selectors rendered using Odin**」（`FilterTypesFunction` 是 `bool f(Type)` 的 resolved string，单参名 `type`；另有 `PreferNamespaces`/`ShowCategories`/`ShowNoneItem`）。**不需要自研序列化器**，需要**自绘选择器**；且那个过滤器是**单参方法**，与既有解名器的无参四级阶梯**形态不匹配**，要另开一条 |
| `[TypeRegistryItem]` | ❌ 缺 | **L7-Inspector，可做但撞一条独立线**：`(name, categoryPath, SdfIconType icon, light/dark 颜色, priority)`——**图标那一项撞上 Pipeline §三 的 `SdfIconType` 未决项**（~1536 个成员的 Sirenix 自有枚举）。先做注册表与分类是可行的，图标要么裁子集、要么单独立项 |
| **`[PropertyTooltip]`** | **✅ 已实现** | — |
| **`[SuffixLabel]`** | **✅ 已实现** | — |

**小计：已实现 15 / 缺 3 / 不做 1**（2026-10-07 重算；2026-10-05：`[InlineProperty]` 转已实现）

### Collections（6）

> 本类 6 项中 3 项已在别处登记，独有 3 项。

| 特性 | 状态 | 层 |
|---|---|---|
| `[DictionaryDrawerSettings]` | ⛔ **不作为** | **Serializer 半边**（2026-10-07 核验）：Unity 根本不序列化字典（**实测**：`FindProperty` 为 null、零告警），要做得先有一套自己的序列化器。`sealed`，旋钮是 `DisplayMode`/`IsReadOnly`/`KeyColumnWidth`/`KeyLabel`/`ValueLabel` |
| `[ListDrawerSettings]` | ✅ 已实现 | 自绘容器（行、增删、索引标签）；**元素仍由原生绘制**；旋钮只取五个有真行为的 |
| `[TableColumnWidth]` | ✅ 已实现 | 表格列宽（不标则弹性均分）；官方的 `resizable` 参数不声明 |

**小计：已实现 2 / 缺 0 / 不做 1**（2026-10-07：`[DictionaryDrawerSettings]` 由「缺」改判
**不作为**——它要的是自研序列化器；2026-10-05：`[ListDrawerSettings]` `[TableColumnWidth]` 转已实现）

### Conditionals（16）

> 本类 16 项中 2 项（`[ShowIfGroup]` `[HideIfGroup]`）已在 Groups 下登记，独有 14 项。

| 特性 | 状态 | 层 |
|---|---|---|
| **`[DisableIf]`** | **✅ 已实现** | — |
| `[DisableIn]` | ✅ 已实现 | 接 `PrefabKind`；多选要求全部目标匹配 |
| `[DisableInEditorMode]` | ✅ 已实现 | — |
| `[DisableInInlineEditors]` | ✅ 已实现 | 同族三兄弟之一 |
| `[DisableInPlayMode]` | ✅ 已实现 | — |
| **`[EnableIf]`** | **✅ 已实现** | — |
| `[EnableIn]` | ✅ 已实现 | 接 `PrefabKind`；多选要求全部目标匹配 |
| **`[HideIf]`** | **✅ 已实现** | — |
| `[HideIn]` | ✅ 已实现 | 接 `PrefabKind`；多选要求全部目标匹配 |
| **`[HideInEditorMode]`** | **✅ 已实现** | — |
| **`[HideInPlayMode]`** | **✅ 已实现** | — |
| **`[ShowIf]`** | **✅ 已实现** | — |
| `[ShowIn]` | ✅ 已实现 | 接 `PrefabKind`；多选要求全部目标匹配 |
| `[ShowInInlineEditors]` | ✅ 已实现 | 同族三兄弟之一 |

**小计：已实现 14 / 缺 0**（2026-10-04：`[ShowIn]` `[HideIn]` `[EnableIn]` `[DisableIn]`
四项转已实现——签名核到后先阻塞的那条前提消失了）

### Numbers（7）

> 本类 7 项中 4 项已在 Validation 下登记，独有 3 项。

| 特性 | 状态 | 层 |
|---|---|---|
| `[ProgressBar]` | ✅ 已实现 | — |
| `[Unit]` | ⛔ 不做 | 需自建约 200 成员的 `Units` 枚举与换算引擎，独立大件 |
| `[Wrap]` | ✅ 已实现 | — |

**小计：已实现 2 / 缺 0 / 不做 1**

### Unity（4）

| 特性 | 状态 | 层 |
|---|---|---|
| `[Multiline]` | ➖ 不需要 | Unity 自己的 |
| `[Space]` | ➖ 不需要 | Unity 自己的 |
| `[TextArea]` | ➖ 不需要 | Unity 自己的 |

> `[Range]` 已登记在 Validation 下。这 3 项（连同 `[Range]`）标 ➖ 的理由是
> 「Unity 原生装饰器由 `PropertyField` 绘制」——这条推断已于 **2026-10-03** 做过结构侧实测，
> 见文末「L0 的验证记录」。

**小计：已实现 0 / 缺 0 / 不需要 3**

### Debug（2）

| 特性 | 状态 | 层 |
|---|---|---|
| `[ShowDrawerChain]` | ✅ 已实现 | — |
| `[ShowPropertyResolver]` | ⛔ 不做 | 没有「property resolver」这个概念可展示，做了是编造。**2026-10-07 重述**：原句「等反射后端出现再说」已过期——反射后端 L3 就落地了，但它是**另一条**通道（`[ShowInInspector]`），不是 Odin 那种「值后端可插拔」的解析器栈；本包没有可插拔这件事，故结论不变、理由换掉 |

**小计：已实现 1 / 缺 0 / 不做 1**

### Meta（1）

| 特性 | 状态 | 层 |
|---|---|---|
| `[SuppressInvalidAttributeError]` | ⛔ 不做 | 当前没有「特性用在不该用的类型上」的告警层可抑制，声明它等于静默 no-op |

**小计：已实现 0 / 缺 0 / 不做 1**

### 总账

```
108 个不重复特性 = 88 已实现 + 6 缺 + 10 不做 + 4 不需要（Unity 自己的）
```

已实现的 88 个：

- **分组与条件**（21）：`[Title]` `[BoxGroup]` `[FoldoutGroup]` `[HorizontalGroup]` `[TabGroup]`
  `[TitleGroup]` `[ToggleGroup]` `[VerticalGroup]`、`[ShowIf]` `[HideIf]` `[EnableIf]` `[DisableIf]`、
  `[HideInEditorMode]` `[HideInPlayMode]` `[DisableInEditorMode]` `[DisableInPlayMode]`、
  `[ShowInInlineEditors]` `[HideInInlineEditors]` `[DisableInInlineEditors]`、
  `[ShowIfGroup]` `[HideIfGroup]`（2026-10-05：判据挂在**分组节点**上，整组一起消失）
- **状态与门控**（6）：`[ReadOnly]` `[EnableGUI]` `[Toggle]` `[HideMonoScript]`
  `[TypeInfoBox]` `[DrawWithUnity]`
- **标签与外观**（10）：`[LabelText]` `[LabelWidth]` `[HideLabel]` `[PropertyTooltip]`
  `[GUIColor]` `[Indent]` `[PropertySpace]` `[SuffixLabel]` `[InfoBox]` `[DetailedInfoBox]`
- **值绘制**（17）：`[DisplayAsString]` `[ToggleLeft]` `[ProgressBar]` `[EnumToggleButtons]`
  `[MultiLineProperty]` `[DelayedProperty]` `[EnumPaging]` `[PropertyRange]` `[Wrap]`、
  `[MinMaxSlider]` `[PreviewField]` `[ValueDropdown]` `[AssetSelector]`（2026-10-04 L1b）、
  `[FilePath]` `[FolderPath]`、`[InlineEditor]`（2026-10-04 L1b 收尾）、
  `[ColorPalette]`（2026-10-07，**卡在设计的那一层补上了**：工程内调色板资产 + 按名查找）
- **校验与钳制**（6）：`[Required]` `[MinValue]` `[MaxValue]` `[AssetsOnly]` `[SceneObjectsOnly]`
  `[ChildGameObjectsOnly]`
- **预制体上下文**（6，2026-10-04 L2 收尾之一）：四个条件 `[ShowIn]` `[HideIn]` `[EnableIn]`
  `[DisableIn]`（接 `PrefabKind`），两个校验 `[RequiredIn]` `[DisallowModificationsIn]`
  ——后两个原先判 ⛔，理由是「共用一块尚不存在的基础设施」，那块基础设施就是这一族
- **按钮**（4，2026-10-04 L5）：`[Button]` `[InlineButton]` `[ButtonGroup]` `[ResponsiveButtonGroup]`
- **回调**（6，2026-10-04 L5）：`[OnInspectorInit]` `[OnInspectorDispose]` `[OnStateUpdate]`
  `[OnInspectorGUI]` `[OnValueChanged]` `[CustomContextMenu]`
- **反射成员**（1，2026-10-04 L3）：`[ShowInInspector]`
- **结构与顺序**（2，2026-10-05 L4）：`[PropertyOrder]`（构建期稳定排序，可标方法）、
  `[InlineProperty]`（观感派：摊平子字段、不画折叠头）
- **集合与表格**（8）：`[ListDrawerSettings]`（自绘容器）、`[TableList]`（表格呈现）、
  `[TableColumnWidth]`、`[HideInTables]`、`[RequiredListLength]`（2026-10-05 L6 第一批）、
  `[OnCollectionChanged]`（增删前后回调）、`[Searchable]`（按标签或值过滤子成员与行）
  ——2026-10-06 L6 收尾两条；`[AssetList]`（2026-10-06 第十七批，**列表与单元素两形态**）。
  容器、元素（节点化 + 读路径）与资产列表都做完了；**仍缺**字典与矩阵（前置 L7）
- **调试**（1）：`[ShowDrawerChain]`

（另有 `PropertyGroupAttribute`——它是自定义分组的**抽象基类**，不能直接标注，故不计入。
同样不计入的还有 `ITreeLifecycleAttribute` / `ITreeMembershipAttribute` / `ITreeOrderingAttribute`
三个**内部**标记接口。）

**标 ⛔ 的 10 项**（`[CustomValueDrawer]` `[ValidateInput]` `[Unit]`
`[HideNetworkBehaviourFields]` `[ShowPropertyResolver]` `[SuppressInvalidAttributeError]`
`[DisableContextMenu]` `[TypeFilter]`，以及 **2026-10-07 新判的** `[DictionaryDrawerSettings]`
`[TableMatrix]`）**不是「还没做」，是「核对过签名、评估后不做」**——理由见
[Modules/Pipeline.md](Modules/Pipeline.md) 第五、六、§二十九。它们与「缺」分开计，
因为「缺」意味着「做得了、只是还没做」。
（`[RequiredIn]` `[DisallowModificationsIn]` 已从这一列移出：2026-10-04 基础设施落地，
它们做得了、也做了。）

**「缺 6 个」也不等于「6 份工作量」**：2026-10-07 核验之后（[Pipeline §二十九](Modules/Pipeline.md)），
剩下的 6 个缺口**全部是可做的**，且全部是**同一件事的不同侧面**——它们都要**先有一个
本包自己的类型选择器 / 多态绘制器**：

| 缺口 | 真实前提 |
|---|---|
| `[PolymorphicDrawerSettings]` `[TypeSelectorSettings]` | 自绘选择器（旋钮挂在「Odin 的选择器」上） |
| `[TypeRegistryItem]` | 选择器 + 注册表（**图标那项撞 `SdfIconType` 独立线**） |
| `[TypeDrawerSettings]` | 自绘选择器；`System.Type` 的通道**已有**，代价是字段加 `[SerializeReference]` |
| `[HideReferenceObjectPicker]` `[HideDuplicateReferenceBox]` | 「有一个引用框可抑制」⇒ 同样先得有那个绘制器 |

**「卡在设计」那一列早已归零**（`[ColorPalette]`，2026-10-07）；
**字典与矩阵已判不作为**（要自研序列化器，见 L7 一节）。
换句话说：**L0–L6 里「做得了、只是还没做」的已归零**（L1a、L1b 两族、`[InlineEditor]` 一族、
L5 的按钮与回调两批、L3 的反射后端与窗口、L2 的预制体上下文族、L4 的顺序与内联、
L6 的容器 / 元素 / 回调 / 搜索 / 资产列表，以及两条「嵌套 / 元素层」能力轮，全部清完）。
见「推荐顺序」。

**2026-10-04 改判的 5 项**（同一轮逐个核过签名）：`[Searchable]`→L6、
`[AssetList]`→L6、`[TypeDrawerSettings]`→L3、`[TypeFilter]`→⛔、
`[ColorPalette]`→卡在设计。它们原先都记在 L1b 下，是**层判错了**，不是「还没排到」。
（`[ColorPalette]` 那一项**已于 2026-10-07 落地**——设计那一层定成「工程内资产、按名查找」，
见 Pipeline §二十七。）
（2026-10-06 又一次改判：`[TypeDrawerSettings]` 由 L3 → **L7 前置**——见 L3 段末追记。
**这是同一项第三次挪层**，也是「核对签名之后再动手」这条纪律里最曲折的一条：它的签名
第一次核就被抄对了，错的是**对「它能干什么」的判断**。）

### 窗口的 1:1

| Odin 的能力 | 状态 | 依赖 |
|---|---|---|
| `OdinEditorWindow`：画**字段** | ✅ 已实现（`XInspectorEditorWindow`） | — |
| 画**属性与方法** | ✅ 已实现（2026-10-04 L3）：`[ShowInInspector]` 收普通属性与非序列化字段、方法节点收 `[Button]` 一族 | — |
| `GetTarget()`：渲染**任意**对象（不必可序列化、不必是 `UnityEngine.Object`） | ✅ 已实现（2026-10-04 L3）：`protected virtual object GetTarget()` | — |
| `[OnInspectorGUI]`：混入自定义 IMGUI | ✅（方法上的无参形式，L5 已做） | — |
| `Initialize()` | ❌ **本包不做**（`OnEnable` 就是那个钩子） | — |
| `WindowPadding` | ✅ 已实现（2026-10-07）：本包自定形状（单值四边同宽、默认 0） | — |
| `DrawEditors`：整段编辑器混入 | ✅ 已实现（2026-10-07）：语义是**本包的「内容区」**，与 Odin 同名不同义 | — |
| `OdinMenuEditorWindow` + `OdinMenuTree`（`AddAllAssetsAtPath`、图标、多选、菜单样式） | ❌ 缺 | **独立大件** |

**一处刻意的差异，不是缺口：** Odin **不让你覆写 `OnGUI`**（要求覆写 `DrawEditors`
或用 `[OnInspectorGUI]`），而本包的基类允许覆写 `OnGUI`。
理由是我们的窗口没有 Odin 那套内部绘制循环，多一层间接没有意义。
把它混进上面的 ❌ 里会误导——它不是「还没做」，是「不打算那么做」。

---

## L0 · 无需工作

`[Range]` `[Space]` `[TextArea]` `[Multiline]` `[Header]` `[Tooltip]` 是 Unity 自己的特性，
绘制由 `PropertyField` 完成——而 `UnityFallbackDrawer` 正是把值交给 `PropertyField` 的
（`includeChildren: true`），所以它们应当照常工作。

**⚠️ 未经实测。** 沙盒里一个都没放。若 `[Header]`/`[Space]` 这类**装饰器**没有出现，
说明装饰器条目没有流经我们的属性树——那 L0 要从「无需工作」改判，并成为一条新缺陷。
见「未验证的假设」。

## L1a · 零新基础设施（最便宜的一大块）

```
[ReadOnly] [GUIColor] [LabelText] [LabelWidth] [HideLabel] [PropertySpace] [Indent]
[PropertyTooltip] [InfoBox] [DetailedInfoBox] [DisplayAsString] [SuffixLabel]
[ToggleLeft] [ProgressBar] [EnumToggleButtons]
[Required] [MinValue] [MaxValue] [AssetsOnly] [SceneObjectsOnly]
[ShowDrawerChain]
```

每个都是一个 `AttributeDrawer<T>`：做点事，然后 `CallNextDrawer`。少数几个
（`[DisplayAsString]` `[ProgressBar]` `[ToggleLeft]`）**不调下一个**，自己画完——
「不调用就等于把内侧藏起来」这条能力本就在架构里。

`[ShowDrawerChain]` 对本项目格外贴切：核心就是绘制器链，把它画出来几乎零成本，
且是极有说服力的自证。

## L1b · 同层，但有工作量　✅ 整层已清完（2026-10-04）

- **分组族**：`[FoldoutGroup]` `[TabGroup]` `[HorizontalGroup]` `[VerticalGroup]` `[TitleGroup]`
  ——享受**架构红利**：继承 `PropertyGroupAttribute` + 写一个 `AttributeDrawer<T>`，
  **构建期一行都不用改**。代价在别处：折叠状态要进 `PropertyState`，分页布局要自己管。
- **重型值绘制器**：`[MinMaxSlider]` `[ValueDropdown]` `[InlineEditor]` `[PreviewField]` `[AssetSelector]`
  （`[AssetList]` 原先记在这里，2026-10-04 改判 **L6**——它要替换列表绘制器）
  ——`[InlineEditor]` 最重：它不止是绘制器，还得先有「内嵌 `Editor` 实例的释放通道」与
  「绘制期深度上下文」两样基建（见 [Pipeline.md](Modules/Pipeline.md) §二.15/16 与 §七）。
- **路径选择器**：`[FilePath]` `[FolderPath]`——要自己做文件/文件夹选择器。

## L2 · 需要特性处理器层 ✅ 已做

条件族与类级分组分发。**这一层性价比最高**：一次投入换来十几个特性。

本包已做：

| 特性 | 语义 |
|---|---|
| `[ShowIf("cond")]` / `[HideIf("cond")]` | 装 `PropertyState.VisibilityResolver` |
| `[EnableIf("cond")]` / `[DisableIf("cond")]` | 装 `PropertyState.ReadOnlyResolver` |
| `[HideInEditorMode]` `[HideInPlayMode]` `[DisableInEditorMode]` `[DisableInPlayMode]` | 零参数，谓词是 `Application.isPlaying` |
| `[ShowIn(PrefabKind)]` `[HideIn(PrefabKind)]` `[EnableIn(PrefabKind)]` `[DisableIn(PrefabKind)]` | 接 `PrefabKind` 位标志，谓词是「目标所处的预制体上下文」（2026-10-04 落地，见 §十） |
| 类级 `[BoxGroup]`（及任何分组特性） | 分发到成员；成员自己的分组作为它的**子路径**，故类级恒在最外层 |

**这一层的实现成本落在两处，都不在「加特性」上**：一是处理器层本身（基类 + 注册表 +
构建期集成点），二是条件的**求值与解析分离**——解析在构建期做一次（找成员、校类型、
失败时告警），求值在绘制期每帧做（读一个 bool）。后者正是「不碰 GUI 就能测」的前提。

预制体那一族把这条分工又验证了一次：**构建期一行没改**，四个特性各自只是一个
「装求值器」的处理器；唯一的新东西是那块探测（见 §十）。

**仍缺**（不是遗漏，是刻意未做）：

| 缺口 | 为什么 |
|---|---|
| ~~`[ShowIn]` `[HideIn]` `[EnableIn]` `[DisableIn]`~~ | ✅ 已随预制体上下文族落地（2026-10-04）：签名核到后原来的拦阻条件消失 |
| ~~`[ShowIfGroup]` `[HideIfGroup]`~~ | ✅ 已做（2026-10-05，L2 收尾之二）：选了「给构建期加一个『分组装配之后』的第二趟处理器」那条路。三处偏差（不做 `Value` / `Animate` / `CombineValuesWith`）与「本身不画东西」的兜底理由见 [Pipeline §十一](Modules/Pipeline.md) |
| ~~条件为**方法**或**普通属性**~~ | ✅ 已随 L3 落地（2026-10-04）：三级解析「序列化成员 → 反射字段/属性 → 无参返回 bool 的方法」 |
| 条件写在**别的对象**上（Odin 的 `"@other.field"`） | ⛔ **不做**（2026-10-05 结论）：`"@this.*"` 那一半已被「条件名可以是 `a/b` 嵌套路径」覆盖；剩下的是「借成员持有的对象实例去读它的字段」——本包没有那条读路径，多对象语义也未定。理由归档见 [Pipeline §十一](Modules/Pipeline.md) |
| ~~`[ShowInInlineEditors]` `[HideInInlineEditors]` `[DisableInInlineEditors]`~~ | ✅ 已随 `[InlineEditor]` 一族落地（2026-10-04） |

**同层内的一处口径不一致，刻意留着**：既有的 `[ShowIf]` 一系取「首个存活目标」的值，
预制体那一族取「全部目标都匹配」。理由见 §十——成员**值**读一个目标就够，
预制体**上下文**是整个选择的性质。这条不一致连同「同一成员上多个条件取后装入者」
一起记进了 `Roadmap.md` 的未决项。

## L3 · 需要反射值后端　✅ 已做（2026-10-04）

`[ShowInInspector]`——画**属性、非序列化字段、静态成员**。它开启的是「画 Unity 不序列化的东西」
这整类能力。窗口的 `GetTarget()` 同在这一次落地。

**当初那条判断兑现了：**「`PropertyValueEntry` 就是那条缝，新增一个反射后端的派生类即可，
树的其余部分不动」——`ReflectedValueEntry` 确实只加了一个派生类，
末端按节点种类选一格（照 `Method` 的先例），树的其余部分真的没动。

**代价也如当初所料：** 那五件事（Undo、预制体覆盖、场景标脏、多对象编辑、域重载后取值）
一件也拿不到。故本包把「只读」推到底——`SetValue` 恒抛。Odin 是「能改但不保存」，
本包是「不给改」，理由见 Pipeline §二 第 17 条。

**本轮另外清掉的**：L2 剩余项里的「条件指向普通属性/方法」也一并做了（三级解析）。
L2 表当时还剩 `[ShowIn]` 一族、`[ShowIfGroup]` 与跨对象条件——它们卡在**签名未核**，
与反射后端无关。

> **2026-10-05 追记：** 上面这句在本轮已全部收口——`[ShowIn]` 一族随预制体那一轮落地，
> `[ShowIfGroup]` / `[HideIfGroup]` 与跨对象条件在 L2 收尾之二里各自有了结论
> （前两个落地，后一个判 ⛔）。见 [Pipeline §十一](Modules/Pipeline.md)。

**没清掉的**：`[TypeDrawerSettings]` 原记在 L3，核过签名后确认它依赖的是
一整套 `System.Type` 的绘制（类型选择器 + `TypeInclusionFilter` 枚举），
只是**借** `[ShowInInspector]` 的样例出场。它是独立的一批。

> **2026-10-06 追记（第三次核对，结论又变了）：改判 L7 前置。** 上一段只对了一半——
> 绘制确实能与反射后端分家，但这套东西**存在的意义是让用户挑一个类型**：`BaseType` 与
> `Filter` 的语义全是「约束候选集」（官方唯一的语义出口是
> `TypeInclusionFilterExtensions.IsValidType`）。而本包的 `System.Type` 只能经**只读**的
> 反射后端出场，**选中后写回没有落点**——今天实现出来必然是静默 no-op，正是本包点名
> 拒绝的形态。真实前置是「**可写的 `System.Type` 通道**」，只有自研序列化（L7）能给；
> 到那时它是可做的，故不判 ⛔、判 L7 前置（与字典/矩阵同类）。
> 官方签名也已抄全：`TypeDrawerSettingsAttribute()`（只有无参构造）+ 两个 public 字段
> `Type BaseType` / `TypeInclusionFilter Filter`；`TypeInclusionFilter` 是 `[Flags]`、
> 6 个成员（`None`/`IncludeAll`/`IncludeAbstracts`/`IncludeConcreteTypes`/`IncludeGenerics`/
> `IncludeInterfaces`，除 `IncludeConcreteTypes` 外描述为空，**数值官方未公布**——
> 将来要做需照 `PrefabKind` 的先例自定并注明）。

## L4 · 需要构建期支持　✅ 整层已清完（2026-10-05）

- ✅ `[PropertyOrder]`——影响**成员顺序**，不是绘制。落地在 `PropertyTreeBuilder` 的成员收集
  之后、分组装配之前（与本节当初记的落点一致），一次**稳定**排序；`[PropertyOrder]` 排成员、
  分组 `Order` 排分组。它顺带解锁了「按钮插到字段之间」——见 [Pipeline §十二](Modules/Pipeline.md)。
- ✅ `[InlineProperty]`——当初记的落点是「要在建树时展开子成员」。它先走了两步：
  2026-10-05 以**观感派**落地（只摊平画子字段、不建子节点），理由是那时候展开意味着
  复活一条已推迟的形状，而观感派承诺得起；**2026-10-06 升到完全体**——同一批里
  「嵌套类型成员节点化」与「嵌套层分组装配」相继落地，Pipeline §二第 7 条那条推迟项
  （`ExpandableCompositeDrawer`）的**固定形状那一半**就此走完，`[InlineProperty]` 的
  子字段于是成了真节点、绘制器从替换型变成穿过型。向量这类原生复合类型仍走观感派。
  （本条原先写着「完全体的触发条件仍未触发」——那句话在完全体落地的那一刻就过期了，
  2026-10-06 校正。）

## L5 · 需要拿到目标对象并调用　✅ 整层已清完（2026-10-04）

`[Button]` `[ButtonGroup]` `[InlineButton]` `[ResponsiveButtonGroup]`、
`[OnValueChanged]` `[OnInspectorInit]` `[OnInspectorGUI]` `[OnInspectorDispose]` `[OnStateUpdate]`、
`[CustomContextMenu]` **十个全部落地**；`[DisableContextMenu]` 仍判 ⛔（理由见第五节）。

当初的判断被两处实测修正，都记在 Pipeline §八：

- **「树持有目标对象列表」是对的**，而且比预想的更关键：它必须在**处理器之前**就位
  （`Owner`），否则需要目标对象的处理器全盘落空。节点到树的反向引用原本一条都没有。
- **「按声明顺序把按钮插回字段之间」做不到**。字段与方法分属元数据的两张表（`0x04`/`0x06`）
  各自编号，跨表比大小没有意义；`GetMembers` 也不按声明顺序返回。故按钮一律排在字段之后
  ——**位置不理想是小事，把按钮插到随机位置才是大事**。

另外三处形状收窄（都写进了 README 与 Pipeline）：三个「只有 resolved string 形态」的特性
（`[OnValueChanged]` `[OnStateUpdate]` `[CustomContextMenu]`）**只认本类型上的方法名**；
`[OnStateUpdate]` 的时机改成「每趟 GUI 布局」（本包没有 Odin 的 state update 循环）；
`[OnValueChanged]` 的判据改成「绘制这一趟里值前后不一致」，于是不必跨帧记旧值、
也没有第一帧误报。

## L6 · 需要集合自绘　🟡 大部分已做（2026-10-05 容器与表格；2026-10-06 回调、搜索与**元素节点化**）

`[ListDrawerSettings]` `[DictionaryDrawerSettings]` `[TableList]` `[TableMatrix]`
`[TableColumnWidth]` `[OnCollectionChanged]` `[Searchable]` `[RequiredListLength]` `[AssetList]`。

现状：数组与列表交给 `PropertyField(includeChildren: true)`，Unity 已经画得和原生一样。
自己做展开的**唯一理由**是让本包的**特性作用于元素**（`[ShowIf]` 标在元素字段上之类）。

一旦自己展开，增删元素、拖拽排序、多选、Undo 全都要自己处理——Unity 内部实现都不薄。

> **2026-10-05 第一批落地，并把上面那句理由改掉。** 做了 `[ListDrawerSettings]`（容器：行、
> 增删、索引标签）、`[TableList]`（表格，含 `[TableColumnWidth]` / `[HideInTables]`）与
> `[RequiredListLength]`（只读 `arraySize`，根本不需要自绘）。
> **但「让特性作用于元素」没有兑现**：元素仍由原生 `PropertyField` 逐个画、不进本包管线——
> 自绘的收益是**容器行为与表格呈现**。要兑现那句话得先做**元素节点化**（见 §十二 与
> Pipeline 的已否决形状）。
> **2026-10-06 收尾两条**：`[OnCollectionChanged]`（增删前后回调，夹住「写进序列化数据」
> 那一步）与 `[Searchable]`（按标签或值过滤子成员与列表行）。两条都不需要元素节点化——
> 回调落在既有的增删施加点上，搜索按**行**与**节点**过滤，元素本身仍由原生绘制。
> **2026-10-06 元素节点化落地（第四条能力轮）。** 那句最初的动因**终于兑现**：元素类型里写的
> 条件、分组、顺序、内联随之生效。先决问题的答案不是「重建树」也不是「动态节点」，而是
> **让不一致不存在**——元素层是 `arraySize` 的同步投影，绘制前对账、对不上整层重建
> （见 Pipeline §十八）。安全阀七项合取，被挡住的一律构建期告警。
> **2026-10-06 元素层的读路径落地（第五条能力轮）。** 元素里的 `[ShowInInspector]`、条件族
> 指向元素实例的反射成员/方法、`[Button]` 一族与按名回调全部生效（取/调的是**那个元素实例**）；
> 核心是路径访问器认 `Array.data[i]` 索引段（见 Pipeline §十九）。**外观变化**：只带
> `[ShowInInspector]`/`[Button]` 的元素类型从此也会被本包接管。
> **2026-10-06 `[AssetList]` 落地（第十七批）。** 它是 L6 里最后一个非 L7 前置项：
> **两形态都做**（列表 + 单元素——官方明说两半「行为不同」，只做一半是半成品），
> 只声明 `Path` / `AssetNamePrefix` 两个旋钮（官方另外四个各有不做的理由，见 Pipeline §二十）。
> **L6 至此只剩**：`[DictionaryDrawerSettings]` `[TableMatrix]`（前置是 L7——Unity 根本不
> 序列化字典与矩阵，字段进不了树）。
> （**2026-10-06 追记**：**元素层深度 > 1** 已于第十九批落地——见家底与 §二十二。）
> （**2026-10-07 追记**：搜索按元素内的反射值匹配已于第二十二批落地——见 Pipeline §二十六；
> 另两批是 `[ColorPalette]`（§二十七）与窗口两个切口（§二十八）。**L6 至此真的只剩
> 字典与矩阵这两条 L7 前置**。）

## L7 · 性质不同：那是 Odin 的另一个产品　🟡 **已核验（2026-10-07，第二十五批）**

多态引用、`[TypeRegistryItem]`、`[PolymorphicDrawerSettings]`、`[HideReferenceObjectPicker]`、
`[SerializeReference]` 类型切换。

**Odin 是 Inspector + Serializer 两个产品。** 上面 L0–L6 全是「Inspector」那半边；
L7 原先记的是「要求自己实现一套**序列化器**与**多态引用解析**（类型注册表、引用的弱值字典、
跨程序集的类型解析、与 Unity 序列化的互操作）」，并判「这不是『再补几个特性』的量级，
而是另一条产品线」。

> **2026-10-07 逐条核过签名与实测之后，上面那个判断要改一半。**
> 证据全在 [Pipeline §二十九](Modules/Pipeline.md)，这里只放结论：
>
> 1. **「画」这半边 Unity 已经给了。** `[SerializeReference]` 字段今天就在序列化数据里
>    （`ManagedReference`），由本包末端交给 `PropertyField`——**Unity 原生的多态 UI
>    今天就能显示**。缺的不是「画」，是「本包的特性作用进去」。
> 2. **里面的子字段有独立句柄**（路径就是 `shape.hp`，与嵌套类型**同一条点分约定**）
>    ⇒ 让它们进树**不必新建值后端**；`managedReferenceValue` 给的是**活实例、可写**，
>    写回落盘、而且**进撤销栈**（那五件事里拿到了两件）。
> 3. **8 个特性里没有一个「必须自研序列化器才能做」。** 拦路的是另外两件事：
>    **自绘类型选择器**（Unity 没有公开的候选集入口，`TypeSelectionList` 是 internal）
>    与**新的可写成员来源**（字典/矩阵——Unity 根本不序列化它们，实测零告警）。
> 4. `[TypeDrawerSettings]` 的「可写 `System.Type` 通道」**已经有了**——代价是使用方要在
>    字段上加一个 `[SerializeReference]`（裸 `System.Type` 字段进不了树，**实测**）。
>
> **于是 L7 该拆成两半：**
>
> | 半边 | 内容 | 判断 |
> |---|---|---|
> | **Inspector** | 多态引用进管线（展开 + 读路径）＋ 自绘选择器一族（`[PolymorphicDrawerSettings]` `[TypeSelectorSettings]` `[TypeRegistryItem]` `[TypeDrawerSettings]`）＋ 两个「hide」 | 🟡 **一条能走的能力轮**，不再是「另一条产品线」 |
> | **Serializer** | 字典与矩阵随资产存档（`[DictionaryDrawerSettings]` `[TableMatrix]`） | ⛔ **本包不作为**——Unity 不序列化它们，要做得先有一套自己的序列化器；Odin 自己的原话是「继承 `SerializedMonoBehaviour`」 |
>
> **该不该做、若做分几批、边界画在哪**，见 [Roadmap](Roadmap.md) 的 L7 一节。

---

## 窗口缺的

| Odin 有 | 本项目 | 依赖 |
|---|---|---|
| `OdinEditorWindow`：画**字段** | ✅ `XInspectorEditorWindow` | — |
| 画**属性与方法** | ✅（L3 起；本表此前漏更新） | — |
| `GetTarget()`：渲染**任意**对象（不必可序列化、甚至不必是 `UnityEngine.Object`） | ✅（L3 起；本表此前漏更新） | — |
| `[OnInspectorGUI]`：混入自定义 IMGUI | ✅ 方法上的无参形式（L5 起；本表此前漏更新） | — |
| `DrawEditors`：混入自定义 IMGUI | ✅（2026-10-07）：**与 Odin 同名不同义**——那边是「逐个 `Editor` 调 `OnInspectorGUI`」，本包只有一个目标，语义是**内容区**；留名字是为了可搜索性 | — |
| `WindowPadding` | ✅（2026-10-07）：**本包自定形状**，单值四边同宽、默认 0 | — |
| `Initialize()` | ❌ **本包不做**：`OnEnable` 就是那个钩子（树刻意是惰性的），再加一个同义虚方法只会造出「两个都该覆写」的困惑。见 Pipeline §二十八 决定三 | — |
| `OdinMenuEditorWindow` + `OdinMenuTree`：菜单树窗口（`AddAllAssetsAtPath`、图标、多选、样式） | ❌ | **独立大件** |

另外 Odin 的窗口**不让你覆写 `OnGUI`**（要你覆写 `DrawEditors` 或用 `[OnInspectorGUI]`）——
本包的基类允许覆写 `OnGUI`，这是有意的差异（我们的窗口没有 Odin 那套内部绘制循环）。

---

## 推荐顺序

1. ~~**L1a**~~——✅ 已清完（2026-10-04）。
2. ~~**L1b**~~——✅ **整层清完**（2026-10-04）：
   分组族六个、值绘制器六个（`[FilePath]` `[FolderPath]` `[MinMaxSlider]` `[PreviewField]`
   `[ValueDropdown]` `[AssetSelector]`），最后一块 `[InlineEditor]` 一族
   （含 `[ShowIn/HideIn/DisableInInlineEditors]` 三个条件族）同日收尾。
3. ~~**`[InlineEditor]` 一族**~~——✅ 已落地。当初「刻意留作独立一轮」的两条判断都兑现了：
   其一，它确实是本层唯一需要**新基建**的——包内此前一条释放路径都没有（`PropertyTree`
   没有 `IDisposable`、`PropertyState.Reset()` 零调用方），而嵌套 `Editor` 实例必须显式销毁；
   外加一个绘制期的深度上下文。其二，三个依赖它的条件族是「加个类」，那份基建不是。
4. ~~**L5 `[Button]`**~~——✅ **整层清完**（2026-10-04，两批）。当初把它排到 L3 之前的那条判断
   兑现了：`[Button]` 确实**不需要反射值后端**，构建期反射加树持有目标对象就够。
   两批各付出一块新基建——**方法节点**（树上第一次出现没有值的节点）与
   **分组子节点的折行布局**（原有策略只表达单行）；其余全是加法。
   落地的同时修掉两处会静默的缺陷：`IsUsedBy` 漏扫方法（只挂 `[Button]` 的类型不被自动接管，
   按钮完全不出现且零告警）、`Dispose` 重复调用会重跑生命周期钩子。
5. ~~**L3 反射后端**~~——✅ **已做**（2026-10-04）。当初把它排在这里的两条判断都兑现了：
   其一，它确实**只加了一个 `PropertyValueEntry` 派生类**，树的其余部分没动；
   其二，条件族指向普通属性/方法确实跟着它一起落地。
   它当初「卡着一串东西」的说法也**部分落空**——`[TypeDrawerSettings]` 核过签名后
   确认是独立的一批（依赖 `System.Type` 的绘制，不依赖反射后端），
   这是本表第二次因为「一个『等』字」而估错层（第一次见本轮的经验三）。
   **2026-10-06 追记：那份「独立」只对了一半——第三次核对把它改判为 L7 前置**
   （真实前置是「可写的 `System.Type` 通道」，只读反射后端下写回无落点），
   见 L3 段末追记与逐条表。
6. ~~**L2 剩余：预制体上下文族**~~——✅ **已做**（2026-10-04，六个特性）。当初把它列在这里的
   前提是「**签名未核**」，那一轮把签名核了（[Pipeline.md](Modules/Pipeline.md) §十），
   前提消失它就成了。**这是本表第三次验证「核对签名之后再动手」这条纪律划算**：
   核完才知道它接的是 `PrefabKind` 位标志、一核就顺带发现它还能解锁两个判 ⛔ 的校验特性
   （`[RequiredIn]` `[DisallowModificationsIn]` 当初的理由正是「共用一块尚不存在的基础设施」）。
   一处当初没料到的成本：**种类探测是真的难**——隔离编辑模式里 `GetPrefabAssetType` 不可靠，
   判定阶梯得把它排在第一位并绕道资产路径；还有三处官方没写明的地方只能给兜底。
   一处当初也没料到的**测试代价**：预制体夹具必须往盘上写资产，这是本仓头一遭。
7. ~~**L2 剩下的两族**~~——✅ **已收口**（2026-10-05，L2 收尾之二）。
   `[ShowIfGroup]` / `[HideIfGroup]` 选了「给构建期加一个『分组装配之后』的第二趟处理器」
   那条路（另一条是沿用 `[ToggleGroup]` 的绘制期解析）：它保住了「解析在构建期、求值在
   绘制期」的分界，代价是顺序契约多一条。跨对象条件 `"@other.field"` 核过之后**判不做**
   ——`"@this.*"` 那半已被嵌套路径覆盖，剩下那半本包没有读路径，多对象语义也未定。
   「性价比要单独评估」这条评估完了：分组条件族值得做（2 个特性 + 一块可复用的第二趟基建），
   跨对象条件不值得。
8. ~~**L4**~~——✅ **整层已清完**（2026-10-05，两条一起）。`[PropertyOrder]` 是「加一次
   稳定排序 + 一个标记接口」；`[InlineProperty]` 走了**观感派**——它把 OdinGap 当初记的
   落点（「要在建树时展开子成员」）**换掉了**：那要复活一条已推迟的形状，而观感派是
   「不画折叠头」这半个缺口的完整答案、且承诺得起。两条都顺带各踩出一个判据坑
   （见新的第七条经验）。
   **L6 第一批也已落地**（2026-10-05，同日）：容器与表格五条。它的**下一步不是「再做几条」，
   而是先答一个问题**——要不要做**元素节点化**（让嵌套/集合的元素成为真正的树节点）。
   做了它，「让特性作用于元素」这句最初的动因才兑现；不做，剩下的 L6 条目（字典、矩阵、
   搜索、资产列表）都只是又一种「容器画法」。见 §十二 与 Pipeline 的已否决形状。
9. ~~**两条能力轮**（2026-10-06）~~——✅ **已落地**：**嵌套类型成员节点化**（Pipeline §十四）
   与**嵌套层的分组装配**（§十五）。两条都是**特性计数 +0** 的能力轮，判据不是「换来几个
   特性」而是「清掉一类能力」——README 那句「本包的特性作用不到嵌套字段上」直到这两条
   做完才真正消掉。上一轮把这两条连起来看的那个判断（「固定形状」与「动态形状」分家）
   兑现了：先做形状固定的那一半，安全阀是**按需展开**，既有用例因此一条没红。
   **剩下的分岔仍是那一条**：集合元素节点化（动态形状 + 路径身份不稳定）。
10. ~~**集合元素节点化**（2026-10-06）~~——✅ **已落地**（Pipeline §十八），上述「剩下的分岔」
    走完了。先决问题的答案见新的第十二条经验：**让问题不存在**。
    触发条件当初写的是「真有『元素要带条件/特性』的需求时」——这一轮把它当成了**盘点欠账**
    来做。
11. ~~**元素层的读路径**（2026-10-06，次日同批）~~——✅ **已落地**（Pipeline §十九）：
    元素里的 `[ShowInInspector]`、条件族指向元素实例的反射成员/方法、`[Button]` 一族与
    按名回调全部生效；`Array.data[i]` 索引段进了路径访问器。§十八 留下的第一项结案。
12. ~~**类级分组进嵌套层与元素层**（2026-10-06，第十八批）~~——✅ **已落地**（Pipeline §二十一）。
    「嵌套 / 元素类型成为一等公民」这条线的收官（特性计数 +0）：类型自己带的 `[BoxGroup]` 一族
    分发到成员，两处「只在最外层类型上收集」的告警撤除。**这一步走完之后，「仍欠」只剩
    元素层深度 > 1 与按名找成员收成一层**——两条都不再属于「类型进不进管线」这一族。
13. ~~**元素层深度 > 1**（2026-10-06，第十九批）~~——✅ **已落地**（Pipeline §二十二）。
    最后一个**结构**欠账：两道守卫（类型链去重 / 层数预算 4）换掉「只做一层」，
    重建期递归 + 登记簿注销补齐正确性，顺带修掉搜索串掩码的既有缺陷。
    **「仍欠」至此只剩「按名找成员收成一层」**——那是一条卫生/整理线，不再是结构边界。
14. ~~**成员引用收成一层**（2026-10-06，第二十批）~~——✅ **已落地**（Pipeline §二十三）。
    那条躺在未决项里的处方（「要与条件族那样三级解析，得先把『按名找成员』这件事整个收成
    一层」）兑现了：阶梯搬进一层，`[ToggleGroup]` 与 `[MinMaxSlider]` 接上反射那一级。
    顺带修掉一处**文档与代码互相矛盾**——两份包内 README 对「嵌套层走不走反射」各说各话，
    是分层写作的必然产物（写这句时那条腿还没有）。**「仍欠」至此清零**。
15. ~~**L7**——要么不做，要么当成独立产品立项~~——🟡 **2026-10-07 已核验（第二十五批）**，
    结论**改了一半**：**Serializer 那半判不作为**（字典/矩阵要自研序列化器，本包不做）；
    **Inspector 那半是一条能走的能力轮**（多态引用进管线 + 自绘选择器一族），
    8 个特性里没有一个「必须自研序列化器」。
    这一条是**「推荐顺序」里最后一条**——L0–L6 与本条至此全部有了结论。
    该不该做、若做分几批，见 [Roadmap](Roadmap.md) 的 L7 一节；
    核验过程与证据见 [Pipeline §二十九](Modules/Pipeline.md)。

**判据是「一次投入换来多少个特性」**：L1a、L1b、L5 都是高杠杆（架构已就位或只需一块基建），
已兑现；L3 是**低杠杆但清掉了一类能力**——它只添了一个特性，却让「画 Unity 不序列化的东西」
这件事从「做不到」变成「做得到（只读）」，顺带解锁了条件族与窗口；
预制体那一族是**中杠杆高复用**——一块探测换来六个特性，而且把 Unity 原生的一类上下文
（预制体资产 / 实例 / 嵌套 / 隔离编辑）第一次接进了条件体系。
**L7 核验之后不再是「另一条产品线」**：Serializer 那半（字典/矩阵）本包判不作为，
Inspector 那半与 L3 同型——**低杠杆但清掉一类能力**（让「多态引用里的成员」从
「进不了树」变成「进得了」，顺带解锁选择器一族）。

**经验留给下一轮（逐轮累积）**：其一，**核对签名之后再动手**——四轮共核过 47 个特性，
才敢把 8 个判成「不做」；其二，**分组族落地时先修了两处既有缺陷**
（分组绘制器落在成员与根节点上、同路径多类型被静默丢弃），它们是设计评审读源码时发现的，
不修的话六个新特性会各自把它放大一遍；其三，**分层本身会错**——上一轮把五个特性从 L1b
挪到 L6/L3/⛔，说明「一个「等」字」足以让整层的成本估算失真，**分类也是要核对的结论**；
其四，**改一个类型会弄丢一条白送的语义**——目标列表从 `Object[]` 变 `object[]`，
`!= null` 就从「Unity 的重载」退化成「引用比较」，而没有任何编译器提醒。
**改型时要问的不只是「哪里编译不过」，还有「哪些语义是那种类型免费给的」**。

**第五条（2026-10-04 新增）：官方文档没写的地方，先问「错了会怎样」，再选兜底。**
预制体那一族有三处查不到权威结论（缺资产实例的实例根查询、未应用的新增对象、
隔离编辑模式里的资产类型）。三处都不是靠猜，而是**各自挑一个「不会误判成另一类」的落点**
并写进注释：宁可少报，不可错报。这一条与「不猜 API 形状」是一对——形状不许猜，
**兜底必须选**，选完要写下来。

**第六条（2026-10-05 新增）：共享机制里的「谁在调用」不会写在数据上。**
`CloneForPath` 同时服务两处，对条件特性而言要求恰好相反：合成祖先时**不该**继承条件
（否则同祖先下的兄弟分组会被一起藏掉），类级分组改写路径时**该**跟着走。
修法是让特性自己带上「声明路径」，用「目标路径是不是当前路径的祖先」反推调用者。
**改动共享机制前先问：同一条路径上的每个调用方，要的是不是同一件事。**

**第七条（2026-10-05 新增）：判据的漏法有三种，不是两种。**
自动接管的判据一直按「有没有绘制器 / 有没有处理器」问问题，于是漏过三次：
`[Button]`（只标在方法上）、`[ShowInInspector]`（只标在属性上）——这两次是**扫的范围**漏了；
`[PropertyOrder]` 是**性质**漏了：它既不画也不处理，由构建期直接消费，两张注册表永远问不到它。
前两次的对策是「判据要覆盖成员收集真会去看的每一处」，第三次的对策是标记接口
（`ITreeOrderingAttribute`）——**对「注册表之外」的特性，让判据认得它的存在本身**。
每次都要问一遍：这个新特性，判据看得见吗？

**第八条（2026-10-05 新增）：「做这一层」的动因要逐轮对账，它会过期。**
L6 的立项理由一直写着「自己做展开的**唯一理由**是让本包的特性作用于元素」。
真做的时候发现：自绘**容器**（行、增删、表格）并不带来任何元素级特性——那要**元素节点化**，
是另一件事。本轮如实把动因改述成「容器行为 + 表格呈现」，并把元素节点化留在未做列。
**不这么做，下一轮会读成「L6 做了，元素特性怎么还不生效」**——一份过期的动因比没有动因更误导。

**第九条（2026-10-06 新增）：能力轮的「影响面」要能被证明，不只是被声称。**
嵌套类型成员节点化兑现了「让特性作用于嵌套字段」——但一个「把所有嵌套类型都展开」的实现会
让**每一处**嵌套结构的外观都变。选「**按需展开**」（只有用到了本包的类型才展开）之后，
既有 599 条用例**一条没红**——「没用到本包的类型外观不变」这条契约因此是被**证明**的。
**给能力轮选一个「opt-in 的安全阀」，是让大改动可回退、可验收的关键**；
否则你只能靠肉眼去判断「别的类型有没有被牵连」。

**第十条（2026-10-06 新增）：路径会被改写，而「从路径推导出来的常量」不会跟着走。**
`PropertyGroupAttribute.CloneForPath` 只改写 `GroupID`。凡是**在构造期从 `GroupID` 算出来
再存成只读字段**的东西（`TabGroupAttribute.TabsGroupID` 就是），在路径被加前缀时都会失配——
而路径被加前缀是**常事**：类级分组的分发会改，嵌套层的分组装配还会再改一次。
判据要写成对前缀免疫的形式（`[TabGroup]` 改成「按段收尾」的比对），或者干脆别存派生常量。
**加前缀这一轮把这条一直潜伏的缺陷照了出来**（它今天就已经中招，只是没人试过
「类级分组 + `[TabGroup]`」）。推论：**给一处引入「改写路径」这种全局操作时，先搜一遍
「谁把路径的某个派生量记住了」**——那是改动面里最难靠编译器发现的一类。
（**2026-10-07**：这一段原先**逐字重复了两遍**且第二份从行中间直接续上，
本批删掉重复的那一份——它是个排版事故，不是两条经验。）

**第十一条（2026-10-06 新增）：「让判据看见」与「让消费者到位」必须同批，而闸门往往不止一道。**
嵌套层的读路径这一轮要让展开判据看见新的成员种类（`[ShowInInspector]` 的字段/属性、
`[Button]` 的方法）。**分两批放行**是刻意的：判据放开而消费者没到位，等于造出
「类型被展开了、里面却什么都画不出来」——比不展开**更糟**，因为不展开至少外观与从前一致。
另一面：这一轮真正难找的不是主判据，而是**没人点过名的第三道闸**
（`IsCompositeCandidate` 的 `hasVisibleChildren`——「只放一个 `[ShowInInspector]` 属性」的类型
连门都进不了）。**问「判据看得见吗」时，要顺着调用链从入口一路问到出口，
每一处提前返回都是一个潜在的闸**。

**第十二条（2026-10-06 新增）：先决问题的最短答案，可能是「让那个问题不存在」。**
「节点数与真实元素数不一致时怎么办」在文档里躺了两轮，两条候选（重建树 / 动态节点）都贵，
且都能各自论证。真正的出路是第三条：**不做增量**——把元素层定义成数组的**同步投影**，
每趟绘制前对账一次，不一致就整层重来。于是**增量方案才有的那堆问题**（路径改名、下标错位、
按引用记账的消费者指向旧对象）一个都不发生：路径身份不是「被稳定住了」，是**根本没被移动过**。
代价是一条新契约（元素节点不跨结构变更）——而它比增量语义**更好解释、也更好测**。
**遇到「两个方案都贵」的先决问题时，先问一句：这个问题一定要存在吗？**
（同一轮里还有一条小号的同款：界面上「谁先读到元素节点」这个顺序问题，靠把对账提到
树级绘制入口——**先于所有消费者**——消掉，而不是在集合绘制器里再维护一套。）

**第十三条（2026-10-07 新增）：「缺一条通道」与「通道要求改一个约定」是两种结论，报价差一个量级。**
`[TypeDrawerSettings]` 这一项挪过三次层：前两次问的都是「**有没有**一条可写的 `System.Type`
通道」，答案一直是「没有」——直到本轮把问题换成「**把它写进去需要谁改什么**」，才量出第三种
答案：**通道在，只是要求使用方在字段上加一个 `[SerializeReference]`**（裸字段进不了树，
加了就进得了、写得住、清得掉，**实测**）。一次实测把「不可能」变成「多写一个特性」。
**判据写成「能不能写进去」时，记得把「要谁改什么」一并量出来**；否则「写不进去」与
「要改个声明才能写」会被归成同一格，而这两格背后的工作量差着一个数量级。

---

## L0 的验证记录

这份对照里曾有**一条推断从未实测**，而**逐条表里标 ➖ 的那 4 项完全建立在它上面**：

> Unity 的原生装饰器（`[Header]`、`[Space]`、`[TextArea]`、`[Multiline]`、`[Range]`）
> 由 `PropertyField` 绘制，因此照常工作。

**2026-10-03 已做结构侧实测**，落点是 `Assets/Sandbox/NativeDecoratorDemo.cs`（沙盒验证台）
与 `Tests/Editor/NativeDecoratorTests.cs`（4 例）。实测钉住三条性质：

1. **装饰器不产生额外节点**——根下的节点恰好是字段本身（含 Inspector 路径刻意保留的
   `m_Script`），既没有「幽灵字段」，两个 `[Header]` 也没有撞上树的身份契约（`Path` 树内唯一）；
2. **原生特性实例到达节点**——装饰器没有被特性收集环节滤掉；
3. **带装饰器的字段，链上只有末端绘制器**——我们不重复画、也不吞掉，一切交给 `PropertyField`。

**还剩一条没被自动化覆盖**：把值画出来的是 `EditorGUILayout.PropertyField`，
渲染结果按本仓策略不测 IMGUI（伪造 GUI 上下文只会得到「测试断言了自己的 mock」）。
「画得出来」那一半靠沙盒**目视对照**：选中场景里的
`Demo 4 - Native Decorators (L0)`，与 `Demo 1`（无特性基线）逐字段比对
段头、间距、滑块、多行框、Tooltip 是否都在。

（这段曾一度是**空头支票**：`Demo 4` 在场景里根本不存在——场景是生成物，加了这个组件之后
没人重建它。2026-10 修好，并加了 `check-docs.ps1 -Enforce` 的沙盒对齐检查：
顶层每个组件都必须在 `Sandbox.unity` 里被引用恰好一次。门禁过了，这段才是可执行的。）

留着这一段而不是删掉，是因为**「结构对了」与「画出来了」是两件事**：
它一旦被当成「已完全验证」写进表里，后面的人就不会再去看渲染那半。
