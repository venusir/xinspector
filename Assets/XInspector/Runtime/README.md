# XInspector.Runtime

Runtime 侧：公开特性，以及它们的行为契约。

**这一侧零 Unity 依赖。** 目录下所有源文件只用到 `System`、`System.Text` 与 `XInspector.Internal`
——不引用 `UnityEngine`，也不引用 `UnityEditor`。因此：

- 特性可以标在**与 Unity 无关的纯 C# 层**上（例如被服务端与客户端共用的领域模型），
  那些程序集不必引用 UnityEngine；
- 这条契约是**编译期强制**的：`Tests.Native/Tests.Native.csproj` 在没有 Unity 的
  .NET 环境下编译本目录，任何一句 `using UnityEngine;` 都会让 `dotnet build` 直接失败。

> `Runtime/` 与 `Editor/` 是**目录分层**，程序集边界另有一套命名（对齐 XFramework 的
> 「公司.产品」式）：`Runtime/` → `Venusir.Xinspector`，`Editor/` → `Venusir.Xinspector.Editor`。
> C# 命名空间仍是 `XInspector` / `XInspector.Editor`，**与程序集名不同**——
> 自带 asmdef 的使用方按程序集名引用，写代码时用的是命名空间。

## 特性

### `[Title]`

```csharp
[Title("玩家档案", Subtitle = "只读展示")]
public class PlayerProfile : MonoBehaviour
{
    [Title("身份")]
    public string playerName;
}
```

| 行为 | 说明 |
|---|---|
| 用在**类**上 | 标题出现在整个 Inspector 最上方 |
| 用在**成员**上 | 标题紧贴该字段上方，字段本身照常绘制在标题下方 |
| `Subtitle` | 可选；为 null 或空白时只画标题 |
| 重复标注 | **不允许**（`AllowMultiple = false`）。一个成员两个标题没有明确语义，允许它只会引出「哪个赢」这种无谓的规则 |
| 空白标题 | **构造期抛 `ArgumentException`**。`[Title("")]` 几乎必然是笔误，而它的表现是「什么都没画」——那是最难归因的一类现象，故选择在构建期明确报错 |

类级与成员级走的是**同一条代码路径**：构建期把类型上的特性直接放在根节点上，
于是根节点的绘制器链上自然出现了同一个标题绘制器。没有「类级特例」这回事，
这也意味着类级标题与成员级标题的绘制行为完全一致。

### `[BoxGroup]`

```csharp
[BoxGroup("基础")]
public string playerName;

[BoxGroup("基础/属性")]      // 只写深层路径即可，外层的「基础」会自动合成
public int health;
```

分组特性与普通特性的根本差别：普通特性作用于**它标注的那个成员**，
而分组特性会把该成员**搬进一个分组节点**里。多个成员写同一个 `GroupID` 就归并到同一分组，
于是「声明式的分组」不需要任何集中登记——每个字段各自声明自己属于谁，构建期负责装配。

| 行为 | 说明 |
|---|---|
| 路径分隔符 | `/`。**路径即嵌套**：`"Outer/Inner"` 表示 `Inner` 是 `Outer` 的子分组 |
| 规范化 | 每段去除首尾空白，空段丢弃（`"Outer//Inner"`、`"Outer/"` 都能工作） |
| 无效路径 | 整条路径不含有效段（`""`、`"/"`）时**构造期抛 `ArgumentException`**——笔误到无法猜测意图，不静默返回空串 |
| 祖先合成 | **不必**为外层再写一遍 `[BoxGroup("Outer")]`，缺失的祖先由构建期合成 |
| 位置 | 分组节点落在**其首个成员出现的位置**。夹在分组字段之间的未分组字段因此留在原地，而不是被挤到 Inspector 末尾 |
| `Order` | 同层分组之间按它升序重排，**但只重排分组彼此之间的先后，不动未分组成员的相对位置** |
| 同组多次声明 | 呈现设定（`Order`、`Label`）取**先声明者**的值，**不累加**。累加会让「给分组多加一个字段」意外改变该分组的排序位置 |
| 用在**类**上 | 该类的所有成员归入这个分组。**类级分组恒在最外层**——成员自己声明的分组会嵌在它里面，而不是与之并列 |
| 多个类级分组 | **只取声明顺序的第一个**，其余忽略。一个类型上挂多个顶层分组没有明确语义（并列还是嵌套？），与其发明一条规则，不如取第一个 |

#### 类级分组的语义

```csharp
[BoxGroup("外层")]
public class Player : MonoBehaviour
{
    public int plain;                    // 落进「外层」

    [BoxGroup("内层")]
    public int nested;                   // 落进「外层/内层」，即嵌在外层里面
}
```

「外层」框住两个字段，而 `nested` 外面还多一层「内层」的框。成员的路径是被**改写**过的
（`内层` → `外层/内层`），所以类级分组永远不会和成员自己的分组并列。

### 条件特性

```csharp
public bool isAlive = true;

[ShowIf(nameof(isAlive))]     public int health;      // 条件为真才显示
[HideIf(nameof(isAlive))]     public int deathReason; // 条件为真则隐藏
[EnableIf(nameof(isAlive))]   public int regen;       // 条件为真才可编辑
[DisableIf(nameof(isAlive))]  public int respawnDelay;// 条件为真则只读

[HideInPlayMode]      public int debugOnly;   // 判据是 Application.isPlaying
[DisableInEditorMode] public int runtimeOnly;
```

| 行为 | 说明 |
|---|---|
| 条件对象 | **必须是序列化成员**（public 字段或 `[SerializeField]` 私有字段），且为 `bool`。名字可以是 `a/b` 这样的嵌套路径 |
| 求值时机 | **每帧重新求值**，所以被条件的字段可以随时跟着切换，不需要重建属性树 |
| 条件名不存在 / 类型不对 | **保持可见并记一条告警**，不抛异常。一个拼错的名字不该让整个 Inspector 白屏 |
| 隐藏 vs 禁用 | 禁用（变灰但仍可见）保留了「这个字段存在、只是现在不能改」的信息，通常比直接藏掉更有用 |

**不支持的**：条件为方法或普通属性（那需要一套反射值后端）、条件写在别的对象上
（Odin 的 `"@other.field"` 语法）、`[ShowIn]` / `[HideIn]` 那类接 `PrefabKind` 的枚举参数、
以及 `[ShowIfGroup]` / `[HideIfGroup]`。

### 值绘制特性

```csharp
[DisplayAsString]                 public int computedId;      // 只读文本
[DisplayAsString(true)]           public string json;         // 允许折行溢出
[ToggleLeft]                      public bool enableTracing;  // 开关在左
[ProgressBar(0, 100)]             public float health;        // 可拖动的进度条
[EnumToggleButtons]               public DamageType damage;   // 一排按钮
[EnumToggleButtons]               public StatusFlags status;  // [Flags] 逐位多选
```

| 行为 | 说明 |
|---|---|
| 替换而非包裹 | 这四个特性**不调用下一个绘制器**——它们把值控件整个换掉。外层的 `[Indent]` `[GUIColor]` 等照常包住它们 |
| 只读 | 与 `[ReadOnly]` / `[DisableIf]` 照常共存（各自处理禁用，不依赖末端那层罩） |
| 类型不符 | **告警并退回普通绘制**，字段不会消失。每种特性的支持类型见上表 |
| `[ProgressBar]` 的越界值 | **不钳制数据**，只把条画到端点。要钳制请配 `[MinValue]`/`[MaxValue]` |
| `[DisplayAsString]` 的复合类型 | 数组与嵌套结构退回普通绘制——显示成什么形状没有显然的答案 |
| 多对象编辑 | `[DisplayAsString]` 值不一致时显示 `—`（与 Unity 一致）；`[ProgressBar]` 值不一致时不可拖动；`[PropertyRange]` 值不一致时退回普通绘制（滑块没有「混合值」形态） |
| `[DelayedProperty]` 的类型面 | 支持 `int`/`float`/`double`/`string`；`long` 仅在值处于 `int` 范围内时可画（延迟控件只有 `int` 版本） |
| `[EnumPaging]` 与 `[Flags]` | 位标志没有「上一项/下一项」的顺序语义，告警并退回普通绘制 |
| `[PropertyRange]` 与钳制族 | 两者是**不同的事**：`[PropertyRange]` 只换控件（用户拖不出范围外的值，但脚本改的值不会被管）；`[MinValue]`/`[MaxValue]` 在绘制后钳制数据。要「连数据一起管」就把两个都写上 |
| `[Wrap]` 的区间 | 半开 `[min, max)`：等于上限时回到下限（`[Wrap(0,360)]` 下 360 → 0） |

### 重型值绘制特性

```csharp
[FilePath]                                     public string configPath;   // 路径 + 浏览按钮
[FilePath(Extensions = "cs, unity")]           public string scriptPath;   // 只过滤对话框
[FilePath(ParentFolder = "Assets/Resources")]  public string resourcePath;
[FolderPath]                                   public string outputDir;

[MinMaxSlider(0f, 100f)]                       public Vector2 hpRange;     // 双滑块
[MinMaxSlider("dynamicRange", true)]           public Vector2 ranged;      // 边界取自成员
[PreviewField]                                 public Texture2D icon;      // 预览方块
[PreviewField(80f, ObjectFieldAlignment.Right)] public GameObject model;

[ValueDropdown("options")]                     public string difficulty;   // 选项来自数组
[ValueDropdown("paths", AppendNextDrawer = true)] public string picked;    // 小按钮 + 普通框
[AssetSelector]                                public Material anyMaterial; // 资产下拉
[AssetSelector(Paths = "Assets/Art", Filter = "t:Material")] public Material scoped;
```

| 行为 | 说明 |
|---|---|
| **只作用于单个成员值** | 数组/`List` 形态**一律不支持**——数组整个交给 Unity 展开，按元素画要先接管数组绘制（未做）。`[ValueDropdown]` 与 `[AssetSelector]` 那几个只对列表有意义的选项因此**不声明**（写了编译不过） |
| **参数只认字面量与序列化成员名** | `[ValueDropdown("x")]` 的 `x`、`[MinMaxSlider("r")]` 的 `r` 必须是**序列化字段**；Odin 的 `$` 成员引用、`@` 表达式、方法调用**都不做**。名字解析失败时**告警并退回普通绘制** |
| 路径怎么存 | `[FilePath]`/`[FolderPath]` 默认存**工程相对**路径（`Assets/…` 开头）；`ParentFolder` 之下则存相对它的路径；`AbsolutePath = true` 存绝对路径。选中的文件若不在基准目录之下（工程外），存绝对路径而不是悄悄改成别的 |
| `Extensions` 只过滤对话框 | 不校验手填的值，也不拦已选的值——标错了不该让字段用不了 |
| `[MinMaxSlider]` 的边界 | 可以是字面量、一个 `Vector2` 成员、两个 `float` 成员或混搭。**只作用 `Vector2`**（`Vector2Int` 不做：值后端不支持它）。边界出现 NaN/无穷时退回普通绘制；动态成员的值被改成倒置时**自动换序** |
| `[PreviewField]` 的方块 | **方块是预览、不是控件**——可编辑的是旁边那个对象字段（原生控件，拖拽赋值照常）。宽度不够时字段排到下一行。默认高度 64、默认对齐 `Left`，都由本包定 |
| `[ValueDropdown]` 的树形 | 选项里带 `/` 就**分子菜单**（与 Odin 一致，默认就是树形）；`FlattenTreeView = true` 拍平成一层 |
| `[ValueDropdown]` 的类型判定 | 源与目标 `propertyType` 必须相同；**枚举还要求成员名与顺序完全一致**——不符则**拒绝这次选择并告警**，绝不按索引硬写 |
| `[AssetSelector]` 是透传型 | 它只画一个小按钮然后**照常调用下一个绘制器**，所以对象字段仍是原生那个。全工程搜索只在**菜单弹出时**发生 |
| 只读与多对象 | 与 `[ReadOnly]` / `[DisableIf]` 照常共存。`[MinMaxSlider]` 在多对象值不一致时退回普通绘制（双滑块没有混合值形态） |

### 校验与钳制特性

```csharp
[Required]                     public string playerId;   // 为空 → 错误框（默认文本）
[Required("必须填", InfoMessageType.Warning)] public string nickname;
[MinValue(0)]                  public int level;         // 绘制后钳到下限
[MaxValue(100f)]               public float heat;
[AssetsOnly]                   public GameObject prefab; // 只提示、不拦赋值
[SceneObjectsOnly]             public Transform target;
```

| 行为 | 说明 |
|---|---|
| 「空」的判定 | null、空串、空集合算空；**纯空白串按非空**。数值与 bool 恒视为有值（标上会告警一次） |
| 只提示不拦 | Unity 的序列化层没有「拒绝写入」的位置，硬拦只会变成悄悄改数据；请在业务层校验 |
| 钳制时机 | **每次绘制之后**——控件里填了越界值，下一次绘制被拉回范围内。官方未说明时机，这条是本包自定的 |
| 钳制的三种跳过 | 多对象值不一致、字段当前只读（`[ReadOnly]`/`[DisableIf]`）、非数值类型（告警一次）。理由都是「不悄悄改数据」 |
| 整数边界取整 | `[MinValue(2.5)]` 的最小合法整数是 3（向上取整），`[MaxValue(2.5)]` 是 2（向下取整） |
| 引用判定 | 工程资产 vs 场景对象（预制体**实例**算场景对象）。空引用不算违反——那是 `[Required]` 的职责 |

### 结构与门控特性

```csharp
[ReadOnly] [EnableGUI]   public int forcedEditable;  // EnableGUI 赢
[DrawWithUnity]          public MyFancyType value;   // 交回 Unity 绘制
[ChildGameObjectsOnly]   public Transform muzzle;    // 只允许本物体之下的子物体
[TypeInfoBox("说明")]    public class Player : MonoBehaviour { }   // 类级信息框
[HideMonoScript]         public class Player : MonoBehaviour { }   // 隐藏 Script 槽位
```

| 行为 | 说明 |
|---|---|
| `[EnableGUI]` 与只读 | 处理器显式排在 `[ReadOnly]` 与条件族**之后**——它叫 Enable，就该能开 |
| `[DrawWithUnity]` | 画完 `PropertyField` 就结束、不调下一个：链上更内侧的绘制器（含 `[Indent]` 这类修饰）都不运行 |
| `[ChildGameObjectsOnly]` | **只做校验、不提供选择下拉**（与 Odin 的差异）；空引用不算违反——那是 `[Required]` 的职责；多对象编辑以第一个目标的层级为准 |
| `[HideMonoScript]` | 构建期把 `m_Script` **直接不建节点**；不写它时该槽位照旧保留（与原生渲染一致），两个行为各有用途 |
| `[Toggle]` | 开关指向**值对象内部**的 bool（相对路径）；**开关永远可点**——否则关掉就开不回来 |
| `[Toggle]` 的叠加顺序 | 条件族（0）< `[Toggle]`（50）< `[ReadOnly]`（100）< `[EnableGUI]`（110）。与 `[DisableIf]` 并存时开关赢，与 `[ReadOnly]` 并存时后者赢 |
| `[Toggle]` 解析失败 | 告警一次并**保持可编辑**——拼错的名字不该让字段变得不可用（与条件族「失败即放行」同一规矩） |

### 其余分组特性

```csharp
[VerticalGroup("左列")]           public int a;   // 不画框的竖直容器
[VerticalGroup]                   public int a2;  // 无参落进默认分组 _DefaultVerticalGroup
[TitleGroup("战斗属性", "副标题")] public int b;   // 标题即路径，不必另起组名
[FoldoutGroup("高级", true)]      public int c;   // 可折叠，初值展开
```

| 行为 | 说明 |
|---|---|
| 折叠状态 | 存在**每个属性树**上：域重载、重开 Inspector 都回到特性的初值，**不跨会话持久化** |
| 折叠时 | 组内内容**不画**（不是变灰）——用「不调下一个绘制器 = 把内侧藏起来」这条既有能力 |
| 折叠初值 | `[FoldoutGroup("G", true)]` 显式；`[FoldoutGroup("G")]` 收起。同组多次声明时取先出现的**显式**值 |
| 标题组的标题 | 就是它的分组路径末段（`GroupID`），`Subtitle` 可空 |
| 档位 | 折叠（最外）→ 页签 → 标题 → 框 → 竖直容器 → **水平**（最内）。决定内容存在与否的恒最外，决定排布的恒最内 |

`[TabGroup]` 把成员分进页签：

```csharp
[TabGroup("设置", "基础")] public int health;    // 落在「设置/基础」页
[TabGroup("设置", "高级")] public int debugLevel; // 落在「设置/高级」页
```

| 行为 | 说明 |
|---|---|
| 路径 | 分组路径是 `"组名/页签名"`——**点分路径本身就是子分组机制**（与 Odin 靠 `ISubGroupProviderAttribute` 的实现不同、行为等价），构建期零新增 |
| 页序 | 即子分组节点的顺序（按 `Order`，同权重按声明先后） |
| 选中页 | 存在每棵属性树上：域重载、重开 Inspector 回到第一页，**不跨会话持久化** |
| 选中页越界 | 回退到第一页并告警一次——不静默什么都不画 |
| `UseFixedHeight` | 保留参数、不产生行为（官方的固定高度模式是为滚动内容准备的，本包还没有那套布局） |

`[ToggleGroup]` 用一个 bool 成员门控整组：

```csharp
public bool showAdvanced;

[ToggleGroup("showAdvanced", groupTitle: "高级选项")] public int debugLevel;
[ToggleGroup("showAdvanced")]                          public int traceFlags;
```

| 行为 | 说明 |
|---|---|
| 组 ID | **就是开关成员名**——同组必须写同一个 bool 成员名（照 Odin 的语义） |
| 关掉时 | 组内内容**不画**（整组消失），与 `[Toggle]` 的「变灰但仍可见」不同 |
| 解析失败 | 告警一次并**恒显示内容**——拼错的名字不该让一整组字段消失 |
| 与 `[FoldoutGroup]` 的档位 | 开关 -180 在折叠 -190 之内；两者都**决定内容存在与否**，故都排在框与标题之外 |

`[HorizontalGroup]` 的宽度分数：

```csharp
[HorizontalGroup("一行", 0.7f)] public int wide;    // 占一行的 70%
[HorizontalGroup("一行", 0.3f)] public int narrow;  // 30%
[HorizontalGroup("一行")]       public int rest;    // 未指定 → 均分剩余
```

| 行为 | 说明 |
|---|---|
| 分数语义 | 显式之和 ≤ 1 时未指定者均分剩余；之和大于 1 时按总和**等比缩放**（确定性） |
| 可用宽度 | 取**当前布局组的实际内宽**（不是窗口宽度），嵌套在框、缩进里也准；但仍是近似，极窄窗口下会挤压 |
| 挤不下时 | 整行退化为「不加宽度约束」的自动排布，而不是硬塞 |
| 嵌套分组 | 水平组里的嵌套分组节点没有分数，按「未指定」参与均分 |
| 标签宽度 | 窄格子里自动按格宽折算（可被 `LabelWidth` 覆盖、被 `DisableAutomaticLabelWidth` 关掉）——否则整页宽的 `labelWidth` 会把值控件挤成一条缝 |

### 分组特性的组合规则

多条分组特性落在一起时的规则。**与具体分组类型无关**——装配对类型一无所知，
所以新增一种分组特性不需要改构建期，下面这些规则自动适用：

| 情形 | 规则 |
|---|---|
| 同路径、同类型，多个成员各声明一次 | 压成一份，呈现设定**先声明者优先**（`Combine`） |
| 同路径、**不同**类型（如标题 + 框） | **并存**：节点上各留一份、链上各配一格绘制器 |
| 成员声明了 `A` 与 `A/B` 两条 | 外层节点 `A` 拿到成员为它**自己**声明的那份，成员落在 `A/B` |
| 成员声明了两条互不相干的分组 | 只有最深的那条生效（成员只归属一条链），另一条被忽略 |
| 类级分组 + 成员的多个分组 | 成员的**每个**分组特性都加上类级前缀 |
| 同层分组排序 | 节点上有多份特性时取**最小的非零 Order**；全零则 0。**与声明顺序无关** |

## 自定义分组特性

继承 `PropertyGroupAttribute` 即可，**构建期不需要改**——分组装配对具体分组类型一无所知：

```csharp
public sealed class MyGroupAttribute : PropertyGroupAttribute
{
    public MyGroupAttribute(string groupID, float order = 0f) : base(groupID, order) { }

    public string Label { get; set; }

    protected internal override void Combine(PropertyGroupAttribute other) { /* 合并规则 */ }
}
```

两点注意：

- **`Combine` 定义「同一分组被多次声明时怎么压成一份」。** 覆写时先调 `base.Combine`。
  跨程序集覆写它时**照样写 `protected internal`**——写 `protected` 会报
  `CS0507: cannot change access modifiers`（2026-10 在测试程序集里实测：改成
  `protected internal` 即通过）。
- **`CloneForPath` 默认用 `MemberwiseClone`**，因此子类字段会被自动带到合成的祖先节点上
  （语义是「祖先继承后代的呈现设定」）。若子类维护了额外的引用型状态，记得一并深拷贝。

绘制是分组**绘制器**的职责，与特性无关——换一种视觉呈现不需要动特性。
