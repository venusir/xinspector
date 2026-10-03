# 与 Odin 的缺口

**对照口径：** Odin 官网 [attributes 页](https://odininspector.com/attributes)（约 110 个特性）
与 [editor-windows 页](https://odininspector.com/editor-windows)，
抓取日期 **2026-10-03**。Odin 会变，这份对照至少每半年该重核一次。

**本项目的家底：** 公开特性 **2 个**（`[Title]`、`[BoxGroup]`）+ 窗口基类 1 个
（`XInspectorEditorWindow`，只画自身序列化字段）。

---

## 为什么不按 Odin 的分类罗列

照搬它的 12 个分类会得到「缺 108 个特性」这种**没有信息量**的结论——真正决定成本的是
**每个缺口需要什么基础设施**。按那个分层之后，结论完全变了：

**最大的那一层（L1a，约 21 个特性）零新基础设施**，全是 `AttributeDrawer<T>` +
`CallNextDrawer`，`PropertyState` 与 `PropertyValueEntry` 已经够用。换句话说，
**Od​in 一半以上的「特性」在本项目里是「加个类」而不是「加个层」**——
这反过来验证了绘制器链那套架构的取舍。

---

## 分层

| 层 | 缺口 | 前提 |
|---|---|---|
| **L0** | Unity 原生装饰器：`[Range]` `[Space]` `[TextArea]` `[Multiline]` `[Header]` `[Tooltip]` | **无需工作**——由 `PropertyField` 绘制。⚠️ 未实测，见「未验证的假设」 |
| **L1a** | 约 21 个十几行的特性，见下 | **零新基础设施** |
| **L1b** | 分组族与重型值绘制器，见下 | 同层，但有实打实的工作量 |
| **L2** | 条件族 + 类级分组分发 | **特性处理器层** ✅ 已做 |
| **L3** | `[ShowInInspector]`、窗口的 `GetTarget()` | 反射值后端（第二套 `PropertyValueEntry`） |
| **L4** | `[PropertyOrder]` `[InlineProperty]` | 构建期的结构支持 |
| **L5** | `[Button]` 家族、回调族、`[CustomContextMenu]` | 拿到目标对象并调用方法 |
| **L6** | `[ListDrawerSettings]` `[DictionaryDrawerSettings]` `[TableList]` `[TableMatrix]` `[OnCollectionChanged]` | 集合自绘 |
| **L7** | 多态引用、`[TypeRegistryItem]`、`[PolymorphicDrawerSettings]` `[SerializeReference]` 类型切换 | **Odin 的另一半产品（Serializer）** |

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

## 未验证的假设

这份对照里有**一条推断从未实测**，它会影响 L0 的判断：

> Unity 的原生装饰器（`[Header]`、`[Space]`）由 `PropertyField` 绘制，因此照常工作。

沙盒的 `DemoComponent` 里没放这几个特性，所以从未验证过。**要验它**：往沙盒组件的字段上加
`[Header("段")]`、`[Space(20)]`、`[Range(0, 10)]`，看是否照常绘制。
若装饰器**不**出现，说明它们没有流经我们的属性树（`NextVisible` 的遍历方式或
`CreateMember` 的字段解析把它们滤掉了），L0 就要从「无需工作」改判为一条缺陷。

写在这里而不是默认它成立：**一条没验证过的推断，和一个错误结论的破坏力是一样的。**
