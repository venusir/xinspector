# 与 Odin 的缺口

**对照口径：** Odin 官网 [attributes 页](https://odininspector.com/attributes)
（12 个分类、**108 个不重复特性**）与
[editor-windows 页](https://odininspector.com/editor-windows)，
抓取日期 **2026-10-03**。Odin 会变，这份对照至少每半年该重核一次。

> 页面上的分类条目数是 110+，但**多处重复计数**（`[Required]` 同时在 Essentials 与
> Validation 下，`[TableList]` 同时在 Type Specifics 与 Collections 下）。
> 下面所有数字都用**去重后**的 108。
>
> **2026-10-04 复核：** 这两处原写 109，去重后实际是 **108**——本轮把官网 attributes 页的
> 特性链接去重后逐个与下面 12 张表比对，**108 对 108，一个不多一个不少**（唯一的两处字符串
> 差异是 Odin 自己的 URL 拼写：`enable-guiattribute`、`on-inspector-guiattribute` 各少一个连字符，
> 对应的类名仍是 `[EnableGUI]`/`[OnInspectorGUI]`）。故「109」是当初的笔误，
> 下面的表一直是对的。

**本项目的家底（2026-10-05，L4 之后）：**

- 公开特性 **79 个**（清单见文末「总账」），分十族：分组与条件、状态与门控、标签与外观、
  值绘制、校验与钳制、**预制体上下文**、按钮、回调、**反射成员**、**结构与顺序**，另有调试 1 个
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
| **L3** | `[ShowInInspector]`、窗口的 `GetTarget()` | 反射值后端（第二套 `PropertyValueEntry`）✅ **已做**（2026-10-04）；`[TypeDrawerSettings]` 经核实独立，仍缺 |
| **L4** | `[PropertyOrder]` `[InlineProperty]` | ✅ **整层已清完**（2026-10-05）：排序落在构建期的一次稳定排序上；`[InlineProperty]` 走**观感派**（只改画法，嵌套字段仍不进管线——「自己展开嵌套类型」那条推迟项因此仍然推迟着） |
| **L5** | `[Button]` 家族、回调族、`[CustomContextMenu]` | 拿到目标对象并调用方法 |
| **L6** | `[ListDrawerSettings]` `[DictionaryDrawerSettings]` `[TableList]` `[TableMatrix]` `[OnCollectionChanged]` | 集合自绘 |
| **L7** | 多态引用、`[TypeRegistryItem]`、`[PolymorphicDrawerSettings]` `[SerializeReference]` 类型切换 | **Odin 的另一半产品（Serializer）** |

---

## 逐条对照

**两张视图回答两个不同的问题。** 上面的分层回答「要多少成本」，这张表回答「具体是哪些」。
前者用于排序，后者用于查漏——只看这张表会得出「缺 94 个」这种没有信息量的结论，
只看分层则可能漏掉某个具体特性。

**表的组织：** 按 Odin 自己的分类，但**每个特性只登记一次**（记在它首次出现的分类下），
否则 Odin 的重复计数会让总账对不上。Odin 也把它归入其它类时在「另见」列注明。

状态四种：**✅ 已实现**、**❌ 缺**、**⛔ 不做**（已评估并记下理由，见「本轮不实现」一节）、
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
| `[AssetList]` | ❌ 缺 | **L6**（替换列表绘制器；2026-10-04 由 L1b 改判） |
| `[AssetSelector]` | ✅ 已实现 | 透传型：小按钮 + 编辑器自带菜单（无搜索框/图标/多选，见 Pipeline §六） |
| `[ChildGameObjectsOnly]` | ✅ 已实现 | — |
| `[ColorPalette]` | ❌ 缺 | **卡在设计**（不再是「缺一层」：需先定「命名调色板存在哪、谁来编辑」，见 Pipeline §六） |
| `[DisplayAsString]` | ✅ 已实现 | — |
| `[EnumPaging]` | ✅ 已实现 | — |
| `[EnumToggleButtons]` | ✅ 已实现 | — |
| `[FilePath]` | ✅ 已实现 | 只作用单个 `string`；`$` 成员引用不做（另见 Validation） |
| `[FolderPath]` | ✅ 已实现 | 与 `[FilePath]` 同形（另见 Validation） |
| `[HideInInlineEditors]` | ✅ 已实现 | 被动随 `[InlineEditor]` 落地 |
| `[HideInTables]` | ❌ 缺 | 依赖 `[TableList]` |
| `[HideMonoScript]` | ✅ 已实现 | — |
| `[HideNetworkBehaviourFields]` | ⛔ 不做 | 目标类型（UNet `NetworkBehaviour`）在 Unity 6 已不存在，只能做成静默 no-op |
| `[HideReferenceObjectPicker]` | ❌ 缺 | L7 |
| `[InlineEditor]` | ✅ 已实现 | 六模式 + 四对象字段模式 + 预览；递归上限等自定值见 Pipeline §七 |
| `[MultiLineProperty]` | ✅ 已实现 | — |
| `[PreviewField]` | ✅ 已实现 | 默认高度/默认对齐由本包定；两个 `FilterMode` 重载永久否决 |
| `[PolymorphicDrawerSettings]` | ❌ 缺 | L7 |
| `[TypeDrawerSettings]` | ❌ 缺 | **独立性已核实**：它**借** `[ShowInInspector]` 的 `System.Type` 字段出场，但依赖的是一整套类型选择器绘制（`BaseType` + `TypeInclusionFilter` 枚举），不是反射后端。L3 落地后它仍在，见 Pipeline §九末 |
| `[SceneObjectsOnly]` | ✅ 已实现 | — |
| `[TableList]` | ❌ 缺 | L6（另见 Collections） |
| `[TableMatrix]` | ❌ 缺 | L6（另见 Collections） |
| `[Toggle]` | ✅ 已实现 | — |
| `[ToggleLeft]` | ✅ 已实现 | — |

**小计：已实现 15 / 缺 8 / 不做 1**（2026-10-04：`[AssetSelector]` `[FilePath]` `[FolderPath]`
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
| `[Searchable]` | ❌ 缺 | **L6**（过滤的是字段/类型的**子成员**，不拥有子绘制权就无从过滤；2026-10-04 由 L1b 改判） |
| `[ShowInInspector]` | ✅ 已实现 | 第二套值后端：**只读**展示（`SetValue` 恒抛），标在方法上编译不过 |
| **`[Title]`** | **✅ 已实现** | — |
| `[TypeFilter]` | ⛔ 不做 | 唯一构造是 resolved string（样例里是方法），且被标注字段是抽象/接口类型——还需 L7 的类型切换。2026-10-04 核过签名后判定 |
| `[TypeInfoBox]` | ✅ 已实现 | — |
| `[ValidateInput]` | ⛔ 不做 | 同 `[CustomValueDrawer]`：resolved string + 校验消息层，归 L5 |
| `[ValueDropdown]` | ✅ 已实现 | 数据源只收序列化数组/List；只声明有真行为的选项（另见 Collections） |

**小计：已实现 15 / 缺 1 / 不做 3**（2026-10-05：`[PropertyOrder]` 转已实现；
2026-10-04：`[ValueDropdown]` 转已实现、
`[TypeFilter]` 由「缺」改判「不做」；同日 L3 把 `[ShowInInspector]` 转已实现、
L2 收尾之一把 `[RequiredIn]` 从「不做」翻成已实现）

### Validation（15）

> Odin 在这一类下列 15 项，其中 8 项已在别处登记。这里是**本类独有**的 7 项。

| 特性 | 状态 | 层 |
|---|---|---|
| `[DisallowModificationsIn]` | ✅ 已实现 | 接 `PrefabKind`；「已经改过」用 `prefabOverride` 判（多选与反射成员不报） |
| `[MaxValue]` | ✅ 已实现 | — |
| `[MinMaxSlider]` | ✅ 已实现 | 只作用 `Vector2`；边界可取自序列化成员名（另见 Numbers） |
| `[MinValue]` | ✅ 已实现 | — |
| `[PropertyRange]` | ✅ 已实现 | — |
| `[Range]` | ➖ 不需要 | Unity 自己的（另见 Unity） |
| `[RequiredListLength]` | ❌ 缺 | L6 |

**小计：已实现 5 / 缺 1 / 不做 0 / 不需要 1**（2026-10-04：`[MinMaxSlider]` 转已实现；
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
| `[ToggleGroup]` | ✅ 已实现 | — |
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
| `[HideDuplicateReferenceBox]` | ❌ 缺 | L7 |
| **`[Indent]`** | **✅ 已实现** | — |
| **`[InfoBox]`** | **✅ 已实现** | — |
| `[InlineProperty]` | ✅ 已实现 | 观感派：只摊平子字段，不进管线；类级形态标在**字段的声明类型**上（另见 Essentials/Layout） |
| **`[LabelText]`** | **✅ 已实现** | — |
| **`[LabelWidth]`** | **✅ 已实现** | — |
| `[OnCollectionChanged]` | ❌ 缺 | L6 |
| `[OnInspectorDispose]` | ✅ 已实现 | 不产生节点 |
| `[OnInspectorGUI]` | ✅ 已实现 | 只做标在方法上的无参形式 |
| `[OnInspectorInit]` | ✅ 已实现 | 不产生节点 |
| `[OnStateUpdate]` | ✅ 已实现 | 时机是本包自定：每趟 GUI 布局 |
| `[OnValueChanged]` | ✅ 已实现 | 只认本类型上的方法名；不支持的类型告警且不触发 |
| `[TypeSelectorSettings]` | ❌ 缺 | L7 |
| `[TypeRegistryItem]` | ❌ 缺 | L7 |
| **`[PropertyTooltip]`** | **✅ 已实现** | — |
| **`[SuffixLabel]`** | **✅ 已实现** | — |

**小计：已实现 14 / 缺 4 / 不做 1**（2026-10-05：`[InlineProperty]` 转已实现）

### Collections（6）

> 本类 6 项中 3 项已在别处登记，独有 3 项。

| 特性 | 状态 | 层 |
|---|---|---|
| `[DictionaryDrawerSettings]` | ❌ 缺 | L6 |
| `[ListDrawerSettings]` | ❌ 缺 | L6 |
| `[TableColumnWidth]` | ❌ 缺 | L6 |

**小计：已实现 0 / 缺 3**

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
| `[ShowPropertyResolver]` | ⛔ 不做 | 本包只有一个值后端，没有「property resolver」这个概念，做了是编造。等反射后端出现再说 |

**小计：已实现 1 / 缺 0 / 不做 1**

### Meta（1）

| 特性 | 状态 | 层 |
|---|---|---|
| `[SuppressInvalidAttributeError]` | ⛔ 不做 | 当前没有「特性用在不该用的类型上」的告警层可抑制，声明它等于静默 no-op |

**小计：已实现 0 / 缺 0 / 不做 1**

### 总账

```
108 个不重复特性 = 79 已实现 + 17 缺 + 8 不做 + 4 不需要（Unity 自己的）
```

已实现的 79 个：

- **分组与条件**（21）：`[Title]` `[BoxGroup]` `[FoldoutGroup]` `[HorizontalGroup]` `[TabGroup]`
  `[TitleGroup]` `[ToggleGroup]` `[VerticalGroup]`、`[ShowIf]` `[HideIf]` `[EnableIf]` `[DisableIf]`、
  `[HideInEditorMode]` `[HideInPlayMode]` `[DisableInEditorMode]` `[DisableInPlayMode]`、
  `[ShowInInlineEditors]` `[HideInInlineEditors]` `[DisableInInlineEditors]`、
  `[ShowIfGroup]` `[HideIfGroup]`（2026-10-05：判据挂在**分组节点**上，整组一起消失）
- **状态与门控**（6）：`[ReadOnly]` `[EnableGUI]` `[Toggle]` `[HideMonoScript]`
  `[TypeInfoBox]` `[DrawWithUnity]`
- **标签与外观**（10）：`[LabelText]` `[LabelWidth]` `[HideLabel]` `[PropertyTooltip]`
  `[GUIColor]` `[Indent]` `[PropertySpace]` `[SuffixLabel]` `[InfoBox]` `[DetailedInfoBox]`
- **值绘制**（16）：`[DisplayAsString]` `[ToggleLeft]` `[ProgressBar]` `[EnumToggleButtons]`
  `[MultiLineProperty]` `[DelayedProperty]` `[EnumPaging]` `[PropertyRange]` `[Wrap]`、
  `[MinMaxSlider]` `[PreviewField]` `[ValueDropdown]` `[AssetSelector]`（2026-10-04 L1b）、
  `[FilePath]` `[FolderPath]`、`[InlineEditor]`（2026-10-04 L1b 收尾）
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
- **调试**（1）：`[ShowDrawerChain]`

（另有 `PropertyGroupAttribute`——它是自定义分组的**抽象基类**，不能直接标注，故不计入。
同样不计入的还有 `ITreeLifecycleAttribute` / `ITreeMembershipAttribute` / `ITreeOrderingAttribute`
三个**内部**标记接口。）

**标 ⛔ 的 8 项**（`[CustomValueDrawer]` `[ValidateInput]` `[Unit]`
`[HideNetworkBehaviourFields]` `[ShowPropertyResolver]` `[SuppressInvalidAttributeError]`
`[DisableContextMenu]` `[TypeFilter]`）**不是「还没做」，
是「核对过签名、评估后不做」**——前 9 条理由见 [Modules/Pipeline.md](Modules/Pipeline.md) 第五节，
`[TypeFilter]` 见第六节。它们与「缺」分开计，因为「缺」意味着「做得了、只是还没做」。
（`[RequiredIn]` `[DisallowModificationsIn]` 已从这一列移出：2026-10-04 基础设施落地，
它们做得了、也做了。）

**「缺 17 个」也不等于「17 份工作量」**：其中真正需要新层的集中在
L6（集合自绘）与 L7（Odin 的另一条产品线）——L1a、L1b 两族、`[InlineEditor]` 一族、
**L5 的按钮与回调两批**、**L3 的反射后端**、以及 **L2 的预制体上下文族**（都 2026-10-04）
这几块已经清完。剩下的缺口里，**L6 是最重的一块**（它比原先估计的更重，见推荐顺序）。

**2026-10-04 改判的 5 项**（同一轮逐个核过签名）：`[Searchable]`→L6、
`[AssetList]`→L6、`[TypeDrawerSettings]`→L3、`[TypeFilter]`→⛔、
`[ColorPalette]`→卡在设计。它们原先都记在 L1b 下，是**层判错了**，不是「还没排到」。

### 窗口的 1:1

| Odin 的能力 | 状态 | 依赖 |
|---|---|---|
| `OdinEditorWindow`：画**字段** | ✅ 已实现（`XInspectorEditorWindow`） | — |
| 画**属性与方法** | ✅ 已实现（2026-10-04 L3）：`[ShowInInspector]` 收普通属性与非序列化字段、方法节点收 `[Button]` 一族 | — |
| `GetTarget()`：渲染**任意**对象（不必可序列化、不必是 `UnityEngine.Object`） | ✅ 已实现（2026-10-04 L3）：`protected virtual object GetTarget()` | — |
| `[OnInspectorGUI]`：混入自定义 IMGUI | ✅（方法上的无参形式，L5 已做） | — |
| `Initialize()` / `WindowPadding` | ❌ 缺 | 无（轻量） |
| `DrawEditors`：整段编辑器混入 | ❌ 缺 | 与 `[OnInspectorGUI]` 同源，缺的只是一个理由 |
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

## L4 · 需要构建期支持　✅ 整层已清完（2026-10-05）

- ✅ `[PropertyOrder]`——影响**成员顺序**，不是绘制。落地在 `PropertyTreeBuilder` 的成员收集
  之后、分组装配之前（与本节当初记的落点一致），一次**稳定**排序；`[PropertyOrder]` 排成员、
  分组 `Order` 排分组。它顺带解锁了「按钮插到字段之间」——见 [Pipeline §十二](Modules/Pipeline.md)。
- ✅ `[InlineProperty]`——当初记的落点是「要在建树时展开子成员」，**本轮没有走那条路**：
  选了**观感派**（只把子字段摊平画出来，不建子节点、不进管线），理由是它对外承诺得起
  （不展开就没有「嵌套字段行为悄悄变了」的风险）。完全体的触发条件与 Pipeline §二第 7 条
  写的一样——「要让特性作用于嵌套类型内部时」，仍未触发。

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

## L6 · 需要集合自绘

`[ListDrawerSettings]` `[DictionaryDrawerSettings]` `[TableList]` `[TableMatrix]`
`[TableColumnWidth]` `[OnCollectionChanged]` `[RequiredListLength]`。

现状：数组与列表交给 `PropertyField(includeChildren: true)`，Unity 已经画得和原生一样。
自己做展开的**唯一理由**是让本包的**特性作用于元素**（`[ShowIf]` 标在元素字段上之类）。

一旦自己展开，增删元素、拖拽排序、多选、Undo 全都要自己处理——Unity 内部实现都不薄。

## L7 · 性质不同：那是 Odin 的另一个产品

多态引用、`[TypeRegistryItem]`、`[PolymorphicDrawerSettings]`、`[HideReferenceObjectPicker]`、
`[SerializeReference]` 类型切换。

**Odin 是 Inspector + Serializer 两个产品。** 上面 L0–L6 全是「Inspector」那半边；
L7 要求自己实现一套**序列化器**与**多态引用解析**（类型注册表、引用的弱值字典、
跨程序集的类型解析、与 Unity 序列化的互操作）。

**这不是「再补几个特性」的量级**，而是另一条产品线。本项目**完全没有**这一块，
也不该把它与 L0–L6 并列看待。

---

## 窗口缺的

| Odin 有 | 本项目 | 依赖 |
|---|---|---|
| `OdinEditorWindow`：画**字段** | ✅ `XInspectorEditorWindow` | — |
| 画**属性与方法** | ✗ | L3 |
| `GetTarget()`：渲染**任意**对象（不必可序列化、甚至不必是 `UnityEngine.Object`） | ✗ | L3 |
| `[OnInspectorGUI]` / `DrawEditors`：混入自定义 IMGUI | ✗ | L5 |
| `Initialize()` / `WindowPadding` | ✗ | 轻 |
| `OdinMenuEditorWindow` + `OdinMenuTree`：菜单树窗口（`AddAllAssetsAtPath`、图标、多选、样式） | ✗ | **独立大件** |

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
   **下一步是 L6**——它的清单此前因改判长了三项（`[Searchable]` `[AssetList]`
   以及 `[AssetList]` 的列表绘制），比原先估计的更重；文档建议先只做「只读展示 + 元素级特性」。
9. **L7**——要么不做，要么当成独立产品立项。

**判据是「一次投入换来多少个特性」**：L1a、L1b、L5 都是高杠杆（架构已就位或只需一块基建），
已兑现；L3 是**低杠杆但清掉了一类能力**——它只添了一个特性，却让「画 Unity 不序列化的东西」
这件事从「做不到」变成「做得到（只读）」，顺带解锁了条件族与窗口；
预制体那一族是**中杠杆高复用**——一块探测换来六个特性，而且把 Unity 原生的一类上下文
（预制体资产 / 实例 / 嵌套 / 隔离编辑）第一次接进了条件体系。
L7 是另一条产品线。

**四条经验留给下一轮**：其一，**核对签名之后再动手**——四轮共核过 47 个特性，
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
