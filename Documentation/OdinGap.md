# 与 Odin 的缺口

**对照口径：** Odin 官网 [attributes 页](https://odininspector.com/attributes)
（12 个分类、**109 个不重复特性**）与
[editor-windows 页](https://odininspector.com/editor-windows)，
抓取日期 **2026-10-03**。Odin 会变，这份对照至少每半年该重核一次。

> 页面上的分类条目数是 110+，但**多处重复计数**（`[Required]` 同时在 Essentials 与
> Validation 下，`[TableList]` 同时在 Type Specifics 与 Collections 下）。
> 下面所有数字都用**去重后**的 109。

**本项目的家底：**

- 公开特性 **11 个**：`[Title]`、`[BoxGroup]`、
  `[ShowIf]` `[HideIf]` `[EnableIf]` `[DisableIf]`、
  `[HideInEditorMode]` `[HideInPlayMode]` `[DisableInEditorMode]` `[DisableInPlayMode]`
- 自定义分组的公开基类 `PropertyGroupAttribute`
- 窗口基类 1 个（`XInspectorEditorWindow`，只画自身序列化字段）

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
| **L1a** | 约 21 个十几行的特性，见下 | **零新基础设施** |
| **L1b** | 分组族与重型值绘制器，见下 | 同层，但有实打实的工作量 |
| **L2** | 条件族 + 类级分组分发 | **特性处理器层** ✅ 已做 |
| **L3** | `[ShowInInspector]`、窗口的 `GetTarget()` | 反射值后端（第二套 `PropertyValueEntry`） |
| **L4** | `[PropertyOrder]` `[InlineProperty]` | 构建期的结构支持 |
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

状态三种：**✅ 已实现**、**❌ 缺**、**➖ 不需要**（Unity 自己的，由 `PropertyField` 绘制）。
标 ➖ 的那 4 项曾建立在一条未验证的推断上，已于 **2026-10-03 做过结构侧实测**
（见文末「L0 的验证记录」）。

### Type Specifics（24）

| 特性 | 状态 | 层 |
|---|---|---|
| `[AssetList]` | ❌ 缺 | L1b |
| `[AssetSelector]` | ❌ 缺 | L1b |
| `[ChildGameObjectsOnly]` | ❌ 缺 | L1a（另见 Validation） |
| `[ColorPalette]` | ❌ 缺 | L1b |
| `[DisplayAsString]` | ❌ 缺 | L1a |
| `[EnumPaging]` | ❌ 缺 | L1a（另见 Buttons） |
| `[EnumToggleButtons]` | ❌ 缺 | L1a（另见 Buttons） |
| `[FilePath]` | ❌ 缺 | L1b（另见 Validation） |
| `[FolderPath]` | ❌ 缺 | L1b（另见 Validation） |
| `[HideInInlineEditors]` | ❌ 缺 | 依赖 `[InlineEditor]` |
| `[HideInTables]` | ❌ 缺 | 依赖 `[TableList]` |
| `[HideMonoScript]` | ❌ 缺 | L1a |
| `[HideNetworkBehaviourFields]` | ❌ 缺 | L1a |
| `[HideReferenceObjectPicker]` | ❌ 缺 | L7 |
| `[InlineEditor]` | ❌ 缺 | L1b |
| `[MultiLineProperty]` | ❌ 缺 | L1a |
| `[PreviewField]` | ❌ 缺 | L1b |
| `[PolymorphicDrawerSettings]` | ❌ 缺 | L7 |
| `[TypeDrawerSettings]` | ❌ 缺 | L1b |
| `[SceneObjectsOnly]` | ❌ 缺 | L1a（另见 Validation） |
| `[TableList]` | ❌ 缺 | L6（另见 Collections） |
| `[TableMatrix]` | ❌ 缺 | L6（另见 Collections） |
| `[Toggle]` | ❌ 缺 | L1a |
| `[ToggleLeft]` | ❌ 缺 | L1a |

**小计：已实现 0 / 缺 24**

### Essentials（19）

| 特性 | 状态 | 层 |
|---|---|---|
| `[AssetsOnly]` | ❌ 缺 | L1a（另见 Validation） |
| `[CustomValueDrawer]` | ❌ 缺 | L1a |
| `[DelayedProperty]` | ❌ 缺 | L1a |
| **`[DetailedInfoBox]`** | **✅ 已实现** | — |
| `[EnableGUI]` | ❌ 缺 | L1a |
| **`[GUIColor]`** | **✅ 已实现** | — |
| **`[HideLabel]`** | **✅ 已实现** | — |
| `[PropertyOrder]` | ❌ 缺 | L4 |
| **`[PropertySpace]`** | **✅ 已实现** | — |
| **`[ReadOnly]`** | **✅ 已实现** | — |
| `[Required]` | ❌ 缺 | L1a（另见 Validation） |
| `[RequiredIn]` | ❌ 缺 | L1a（另见 Validation） |
| `[Searchable]` | ❌ 缺 | L1b |
| `[ShowInInspector]` | ❌ 缺 | L3 |
| **`[Title]`** | **✅ 已实现** | — |
| `[TypeFilter]` | ❌ 缺 | L1b |
| `[TypeInfoBox]` | ❌ 缺 | L1a |
| `[ValidateInput]` | ❌ 缺 | L1a（另见 Validation） |
| `[ValueDropdown]` | ❌ 缺 | L1b（另见 Collections） |

**小计：已实现 1 / 缺 18**

### Validation（15）

> Odin 在这一类下列 15 项，其中 8 项已在别处登记。这里是**本类独有**的 7 项。

| 特性 | 状态 | 层 |
|---|---|---|
| `[DisallowModificationsIn]` | ❌ 缺 | L1a |
| `[MaxValue]` | ❌ 缺 | L1a（另见 Numbers） |
| `[MinMaxSlider]` | ❌ 缺 | L1b（另见 Numbers） |
| `[MinValue]` | ❌ 缺 | L1a（另见 Numbers） |
| `[PropertyRange]` | ❌ 缺 | L1a（另见 Numbers） |
| `[Range]` | ➖ 不需要 | Unity 自己的（另见 Unity） |
| `[RequiredListLength]` | ❌ 缺 | L6 |

**小计：已实现 0 / 缺 6 / 不需要 1**

### Groups（12）

| 特性 | 状态 | 层 |
|---|---|---|
| **`[BoxGroup]`** | **✅ 已实现** | 含类级分发 |
| `[Button]` | ❌ 缺 | L5（另见 Buttons） |
| `[ButtonGroup]` | ❌ 缺 | L5（另见 Buttons） |
| `[FoldoutGroup]` | ❌ 缺 | L1b |
| `[HideIfGroup]` | ❌ 缺 | L2 剩余（另见 Conditionals） |
| `[HorizontalGroup]` | ❌ 缺 | L1b |
| `[ResponsiveButtonGroup]` | ❌ 缺 | L5（另见 Buttons） |
| `[ShowIfGroup]` | ❌ 缺 | L2 剩余（另见 Conditionals） |
| `[TabGroup]` | ❌ 缺 | L1b |
| `[TitleGroup]` | ❌ 缺 | L1b |
| `[ToggleGroup]` | ❌ 缺 | L1b |
| `[VerticalGroup]` | ❌ 缺 | L1b |

**小计：已实现 1 / 缺 11**

### Buttons（6）

> 本类 6 项中 5 项已在别处登记，独有 1 项。

| 特性 | 状态 | 层 |
|---|---|---|
| `[InlineButton]` | ❌ 缺 | L5 |

**小计：已实现 0 / 缺 1**

### Misc（19）

| 特性 | 状态 | 层 |
|---|---|---|
| `[CustomContextMenu]` | ❌ 缺 | L5 |
| `[DisableContextMenu]` | ❌ 缺 | L1a |
| `[DrawWithUnity]` | ❌ 缺 | L1a |
| `[HideDuplicateReferenceBox]` | ❌ 缺 | L7 |
| **`[Indent]`** | **✅ 已实现** | — |
| **`[InfoBox]`** | **✅ 已实现** | — |
| `[InlineProperty]` | ❌ 缺 | L4 |
| **`[LabelText]`** | **✅ 已实现** | — |
| **`[LabelWidth]`** | **✅ 已实现** | — |
| `[OnCollectionChanged]` | ❌ 缺 | L6 |
| `[OnInspectorDispose]` | ❌ 缺 | L5 |
| `[OnInspectorGUI]` | ❌ 缺 | L5 |
| `[OnInspectorInit]` | ❌ 缺 | L5 |
| `[OnStateUpdate]` | ❌ 缺 | L5 |
| `[OnValueChanged]` | ❌ 缺 | L5 |
| `[TypeSelectorSettings]` | ❌ 缺 | L7 |
| `[TypeRegistryItem]` | ❌ 缺 | L7 |
| **`[PropertyTooltip]`** | **✅ 已实现** | — |
| **`[SuffixLabel]`** | **✅ 已实现** | — |

**小计：已实现 0 / 缺 19**

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
| `[DisableIn]` | ❌ 缺 | L2 剩余（枚举参数，签名待核） |
| **`[DisableInEditorMode]`** | **✅ 已实现** | — |
| `[DisableInInlineEditors]` | ❌ 缺 | 依赖 `[InlineEditor]` |
| **`[DisableInPlayMode]`** | **✅ 已实现** | — |
| **`[EnableIf]`** | **✅ 已实现** | — |
| `[EnableIn]` | ❌ 缺 | L2 剩余（枚举参数，签名待核） |
| **`[HideIf]`** | **✅ 已实现** | — |
| `[HideIn]` | ❌ 缺 | L2 剩余（枚举参数，签名待核） |
| **`[HideInEditorMode]`** | **✅ 已实现** | — |
| **`[HideInPlayMode]`** | **✅ 已实现** | — |
| **`[ShowIf]`** | **✅ 已实现** | — |
| `[ShowIn]` | ❌ 缺 | L2 剩余（枚举参数，签名待核） |
| `[ShowInInlineEditors]` | ❌ 缺 | 依赖 `[InlineEditor]` |

**小计：已实现 8 / 缺 6**

### Numbers（7）

> 本类 7 项中 4 项已在 Validation 下登记，独有 3 项。

| 特性 | 状态 | 层 |
|---|---|---|
| `[ProgressBar]` | ❌ 缺 | L1a |
| `[Unit]` | ❌ 缺 | L1a |
| `[Wrap]` | ❌ 缺 | L1a |

**小计：已实现 0 / 缺 3**

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
| `[ShowDrawerChain]` | ❌ 缺 | L1a |
| `[ShowPropertyResolver]` | ❌ 缺 | L1a |

**小计：已实现 0 / 缺 2**

### Meta（1）

| 特性 | 状态 | 层 |
|---|---|---|
| `[SuppressInvalidAttributeError]` | ❌ 缺 | L1a |

**小计：已实现 0 / 缺 1**

### 总账

```
108 个不重复特性 = 10 已实现 + 94 缺 + 4 不需要（Unity 自己的）
```

已实现的 10 个：`[Title]`、`[BoxGroup]`、`[ShowIf]`、`[HideIf]`、`[EnableIf]`、`[DisableIf]`、
`[HideInEditorMode]`、`[HideInPlayMode]`、`[DisableInEditorMode]`、`[DisableInPlayMode]`。

（另有 `PropertyGroupAttribute`——它是自定义分组的**抽象基类**，不能直接标注，故不计入。）

**「缺 94 个」不等于「94 份工作量」**：其中约 40 个落在 L1a，每个十几行、零新基础设施。
真正需要新层的只有 L3 / L4 / L5 / L6 / L7 那几块，而它们各自的特性数远少于 L1a。

### 窗口的 1:1

| Odin 的能力 | 状态 | 依赖 |
|---|---|---|
| `OdinEditorWindow`：画**字段** | ✅ 已实现（`XInspectorEditorWindow`） | — |
| 画**属性与方法** | ❌ 缺 | L3 反射后端 |
| `GetTarget()`：渲染**任意**对象（不必可序列化、不必是 `UnityEngine.Object`） | ❌ 缺 | L3 |
| `Initialize()` / `WindowPadding` | ❌ 缺 | 无（轻量） |
| `[OnInspectorGUI]` / `DrawEditors`：混入自定义 IMGUI | ❌ 缺 | L5 |
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

## L1b · 同层，但有工作量

- **分组族**：`[FoldoutGroup]` `[TabGroup]` `[HorizontalGroup]` `[VerticalGroup]` `[TitleGroup]`
  ——享受**架构红利**：继承 `PropertyGroupAttribute` + 写一个 `AttributeDrawer<T>`，
  **构建期一行都不用改**。代价在别处：折叠状态要进 `PropertyState`，分页布局要自己管。
- **重型值绘制器**：`[MinMaxSlider]` `[ValueDropdown]` `[InlineEditor]` `[PreviewField]` `[AssetSelector]` `[AssetList]`
  ——`[InlineEditor]` 尤其重（内嵌编辑器 + 预览宿主 + 递归深度控制）。
- **路径选择器**：`[FilePath]` `[FolderPath]`——要自己做文件/文件夹选择器。

## L2 · 需要特性处理器层 ✅ 已做

条件族与类级分组分发。**这一层性价比最高**：一次投入换来十几个特性。

本包已做：

| 特性 | 语义 |
|---|---|
| `[ShowIf("cond")]` / `[HideIf("cond")]` | 装 `PropertyState.VisibilityResolver` |
| `[EnableIf("cond")]` / `[DisableIf("cond")]` | 装 `PropertyState.ReadOnlyResolver` |
| `[HideInEditorMode]` `[HideInPlayMode]` `[DisableInEditorMode]` `[DisableInPlayMode]` | 零参数，谓词是 `Application.isPlaying` |
| 类级 `[BoxGroup]`（及任何分组特性） | 分发到成员；成员自己的分组作为它的**子路径**，故类级恒在最外层 |

**这一层的实现成本落在两处，都不在「加特性」上**：一是处理器层本身（基类 + 注册表 +
构建期集成点），二是条件的**求值与解析分离**——解析在构建期做一次（找成员、校类型、
失败时告警），求值在绘制期每帧做（读一个 bool）。后者正是「不碰 GUI 就能测」的前提。

**仍缺**（不是遗漏，是刻意未做）：

| 缺口 | 为什么 |
|---|---|
| `[ShowIn]` `[HideIn]` `[EnableIn]` `[DisableIn]` | 它们接的是 `PrefabKind` 之类的枚举参数，**具体签名未从官网核对到**。猜一个形状写下去比不做更糟——它会被当成已有能力 |
| `[ShowIfGroup]` `[HideIfGroup]` | 同上，分组变体的签名待核 |
| 条件为**方法**或**普通属性** | 需要 L3 的反射后端；本包的条件对象必须是序列化成员 |
| 条件写在**别的对象**上（Odin 的 `"@other.field"`） | 同上，且需要跨对象引用解析 |
| `[ShowInInlineEditors]` `[HideInInlineEditors]` `[DisableInInlineEditors]` | 依赖 L1b 的 `[InlineEditor]`，那东西还不存在 |

## L3 · 需要反射值后端

`[ShowInInspector]`——画**属性、方法、非序列化成员**。它开启的是「画 Unity 不序列化的东西」
这整类能力。

做法已经在架构里留了口子：`PropertyValueEntry` 就是那条缝，新增一个反射后端的派生类即可，
树的其余部分不动。代价是**拿不到** `SerializedObject` 白送的那五件事
（Undo、预制体覆盖、场景标脏、多对象编辑、域重载后取值）——那五件是 `SerializedObject` 给的，
反射后端一件也没有。所以它的成员应当**明确只读或明确提示不可撤销**，不假装能撤销。

窗口的 `GetTarget()`（渲染任意对象，不必可序列化）也依赖这一层。

## L4 · 需要构建期支持

- `[PropertyOrder]`——影响**成员顺序**，不是绘制。要在 `PropertyTreeBuilder` 的成员收集之后、
  分组装配之前重排。注意与分组交互：`[PropertyOrder]` 是排成员，`Order` 是排分组。
- `[InlineProperty]`——把嵌套类型的字段**提到本层**（不画折叠头）。要在建树时展开子成员。

## L5 · 需要拿到目标对象并调用

`[Button]` `[ButtonGroup]` `[InlineButton]` `[ResponsiveButtonGroup]`、
`[OnValueChanged]` `[OnInspectorInit]` `[OnInspectorGUI]` `[OnInspectorDispose]` `[OnStateUpdate]`、
`[CustomContextMenu]` `[DisableContextMenu]`。

难点不在绘制而在**调用目标**：要在一个 `SerializedObject` 之外拿到真实对象引用并调用方法。
这是 `PropertyValueEntry` 之外的信息（值入口只认序列化属性）。

**边界：不要为此把目标对象塞进值入口**——那会污染「值后端可替换」这条缝。
倾向做法是让树持有目标对象列表（`PropertyTree` 已经从 `SerializedObject` 拿得到
`targetObject`，多对象时是 `targetObjects`）。

`[OnValueChanged]` 另有一处要想清楚：「值变了」需要每帧比对旧值，旧值该放 `PropertyState`；
而触发时机（绘制前后？`Update` 前后？）未定。

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

1. **L1a**——最便宜、见效最快，且**不需要任何新机制**。一次把日常观感拉上一个台阶。
2. **L1b 的分组族**——架构红利最大的一块：继承基类 + 写 drawer，构建期不动。
3. **L3 反射后端**——它是 L2 剩余项、L5 全部、以及窗口一半能力的前置。
4. **L5**——`[Button]` 是使用方最常问「为什么没有」的一个。
5. **L4 / L6**——按需。
6. **L7**——要么不做，要么当成独立产品立项。

**判据是「一次投入换来多少个特性」**：L1a 与 L1b 的分组族是高杠杆（架构已就位，纯加法）；
L3 是低杠杆但**卡着后面三层**；L7 是另一条产品线。

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
