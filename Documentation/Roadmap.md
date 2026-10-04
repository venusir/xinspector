# Roadmap

候选特性：**该不该做、边界画在哪、依赖什么**。收件人是维护者（选型时看）。

判据与文档落点见 [ModuleAudit.md](ModuleAudit.md)。已做完的东西及其理由**不在这里**
——那属于 [Modules/Pipeline.md](Modules/Pipeline.md)。

排序大致按「先做哪个」，但没写死版本号：本包还在 `0.1.0-preview`，API 稳定前不谈版本规划。

> **与 Odin 的完整对照见 [OdinGap.md](OdinGap.md)。** 那份文档有两张视图：
> **分层**（L0–L7，按「需要什么基础设施」，用于排序）与**逐条清单**
> （108 个特性逐个标状态与所属层，用于查漏）。**找缺口先翻那里**，本文件保留的是
> **逐条的「该不该做」判断**——那边回答「缺什么、要多少成本」，这边回答「做不做、边界在哪」。
>
> 每条候选下面标了它属于哪一层，便于对照。

---

## 一、特性处理器层（AttributeProcessor）—— L2　✅ 已做

**已实现**：`Editor/Processors/`。两个钩子（自身 / 父级注入）、注册表、以及构建期的
集成点。**契约写在 `Assets/XInspector/Editor/README.md` 的「写一个特性处理器」一节**，
不在这里重复。

当初的触发条件写的是「等 `[ShowIf]` 或类级分组分发到来时一并加」——两者都在同一轮里做了，
故这一层不是被「提前」建的，而是与它的第一批用户一起落地的。

**遗留**：`AttributeProcessorRegistry` 是 `internal`（`DrawerTypeRegistry` 是 public）。
若使用方需要「我的处理器到底被扫到了没」这样的诊断面，把它改成 public 是纯新增。

---

## 二、条件族 —— L2　✅ 大部分已做

**已实现**：`[ShowIf]` `[HideIf]` `[EnableIf]` `[DisableIf]` 与四个模式变体
（`[HideInEditorMode]` `[HideInPlayMode]` `[DisableInEditorMode]` `[DisableInPlayMode]`）。
用法与边界见 `Assets/XInspector/Runtime/README.md` 的「条件特性」一节。

**仍缺，且理由各不相同**：

| 缺口 | 为什么没做 |
|---|---|
| `[ShowIn]` `[HideIn]` `[EnableIn]` `[DisableIn]` | 接 `PrefabKind` 之类的枚举参数，**签名未从 Odin 官网核对到**。猜一个形状写下去比不做更糟——它会被当成已有能力 |
| `[ShowIfGroup]` `[HideIfGroup]` | 同上，分组变体的签名待核 |
| 条件为方法或普通属性 | 需要 L3 的反射值后端 |
| `"@other.field"` 跨对象条件 | 同上，且需要跨对象引用解析 |

**一处未决的语义**：多对象编辑时各目标的条件值不同，`condition.boolValue` 现在取的是
第一个目标的值——而 Odin 那边倾向于「任一目标满足即显示」。本包尚未就此做决定，
现状是「跟随第一个目标」。要改的话是个小改动，但**得先定语义**。

---

## 三、分组族 —— L1b　✅ 已做（2026-10-04）

`[FoldoutGroup]` `[TabGroup]` `[HorizontalGroup]` `[VerticalGroup]` `[TitleGroup]` `[ToggleGroup]`
六个全部落地。**「不需要改构建期」这条判断兑现了**——六个特性只加了 Runtime 类与 Editor 绘制器；
`[TabGroup]` 也用点分路径 `Tabs/Tab1` 表达子分组，没有引入 Odin 的
`ISubGroupProviderAttribute` 机制。

但落地过程中发现两处**既有缺陷**必须先修，它们与分组有关、与具体类型无关：

1. **分组绘制器配到了成员与根节点上**（链装配按特性配对、不看节点种类）——
   症状是每个成员画两层框、类级分组继续框住整页；
2. **同路径多类型分组被静默丢弃**，且祖先节点带哪种类型取决于声明顺序。

两处都已修（先写红的测试再修）。**教训：说「纯加法」之前先确认既有基座没有洞**——
六个新特性会各自把同一个洞放大一遍。

**剩下的 L1b 不是分组**：重型值绘制器（`[InlineEditor]` `[PreviewField]` `[AssetSelector]`
`[Searchable]` `[ValueDropdown]`）与路径选择器（`[FilePath]` `[FolderPath]`）——
它们各自是独立工作量，没有「加个类」那么便宜。

---

## 四、`[Button]` 与 `[OnValueChanged]` —— L5

**该不该做：`[Button]` 该做，`[OnValueChanged]` 存疑。**

`[Button]` 的难点不在绘制而在**调用目标**：它要在一个 `SerializedObject` 之外
拿到真实对象引用并调用方法。这需要记录「本属性属于哪个目标对象」，
而那是 `PropertyValueEntry` 之外的信息（值入口只认序列化属性）。
**边界：不要为此把目标对象塞进值入口**——那会污染「值后端可替换」这条缝。
倾向做法是让树持有目标对象列表。

`[OnValueChanged]` 存疑的理由：它要在「值变了」时回调，而判断「变了」需要每帧比对旧值，
这与「绘制器不得持有可变字段」的纪律需要调和（旧值该放 `PropertyState`）。
先想清楚触发时机（每帧比对？`SerializedObject.Update` 前后？），再决定做不做。

---

## 五、数组与列表展开 —— L6

**该不该做：该做，但它是**本表里最重的一项**。**

Unity 的 `PropertyField(includeChildren: true)` 已经把数组画得和原生一样了
——本包现在正是靠它。自己做展开的唯一理由是**让 XInspector 的特性作用于数组元素**
（`[BoxGroup]` 标在元素字段上、`[ShowIf]` 作用于元素）。

**边界。** 一旦自己展开，就要自己处理增删元素、拖拽排序、多选、Undo——
这些 Unity 内部实现都不薄。建议先只做「只读展示 + 元素级特性」，把编辑操作仍交给
Unity 的 `PropertyField`，验证需求真实存在再往下走。

---

## 六、自定义序列化后端与 `[ShowInInspector]` —— L3

**该不该做：该做，但优先级低于上面几项。**

**这是 `PropertyValueEntry` 存在的理由。** 目前唯一实现是 `SerializedPropertyValueEntry`
（`SerializedObject` 后端），代价是只能画 Unity 会序列化的成员。要画**普通属性**
（`[ShowInInspector]` 那类），需要第二个派生类：反射读写。

**边界。** 反射后端**拿不到** Undo、预制体覆盖、场景标脏、多对象编辑、域重载后取值
——那五件事是 `SerializedObject` 给的。所以：

- 反射后端标注的成员应当**只读或明确提示不可撤销**，不要假装它能撤销；
- 写入路径要显式标脏（`EditorUtility.SetDirty`），否则改了不保存；
- 别试图让它也支持多对象编辑——语义不成立。

---

## 七、折叠/展开状态的跨会话持久化

**该不该做：暂缓（2026-10-04 复核，结论不变）。**

现状：`[FoldoutGroup]` `[TabGroup]` `[ToggleGroup]` 的展开/选中状态都**只活在每棵属性树上**
（域重载、重开 Inspector 回到初值），`[DetailedInfoBox]` 的展开状态同样如此。

要跨会话就得先定存哪：`EditorPrefs`（按键路径，跨项目共享——Odin 的做法）、
`SessionState`（不跨会话）、还是序列化进场景（会污染资产）。
**键怎么构成**是另一道坎：分组路径在不同类型间会重名（两个类都有「基础」分组），
得带上类型名甚至程序集名，而后者在重命名后就会失配。

---

## 八、样式系统

**该不该做：优先级低，且要克制。**

现状一律用 `EditorStyles` 与 `GUI.skin.box`，没有 Odin 那样的配色与图标。
好处是**外观跟着 Unity 主题走**，深浅色切换、Pro 皮肤都不需要额外处理。

**边界。** 若要做，先定「哪些必须自绘」。Odin 的自绘很大程度上是为了品牌辨识度，
而本包的定位是复用 Unity 的画法（`UnityFallbackDrawer` 直接把值交给 `PropertyField`）。
自绘得越多，「和原生长得不一样」的问题就越多——那正是本包目前没有的一类问题。

---

## 九、编辑器窗口与 UI Toolkit

**编辑器窗口基类已做**（`Editor/Windows/XInspectorEditorWindow`，绘制窗口自身的序列化字段），
当年「`PropertyTree` 将来要复用到窗口里应该不难」的判断成立——机制抽成了
`PropertyTreeHost`，窗口基类与之后的入门窗口预览面板共用它。那份契约现在写在
`Assets/XInspector/Editor/README.md` 的「在窗口里复用 `PropertyTree`」一节。

**仍未做**：检视任意对象的浮空 Inspector、字段拖拽重排、窗口内 Undo、窗口布局的自定义持久化。

**UI Toolkit：不做。** 整套管线是 IMGUI 的（`EditorGUILayout`、`DrawerChain` 的即时模式语义）。
改成 UI Toolkit 是重写而不是移植。除非 Unity 弃用 IMGUI。

---

## 十、git 分发（已评估，暂不做）

**该不该做：暂不做。** 但它**技术上现在就能用**，所以把查证结论记下来，
免得下一轮重新研究一遍。

### 查证到的事实（2026-10-03）

| 事实 | 出处 |
|---|---|
| 官方列了**四种并列**的分发方式（压缩包 / tarball / git URL / scoped registry），**没有任何排序或推荐** | [Sharing your package](https://docs.unity3d.com/6000.0/Documentation/Manual/cus-share.html) |
| `?path=` 只要求「路径相对仓库根」且该子目录含 `package.json`——**与包放在哪无关** | [Git URLs](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-git.html) |
| **`?path=` 必须写在 `#revision` 之前**，反了会失败 | 同上 |
| git 依赖**只能**写在工程的 `manifest.json`，**不能**写进包的 `package.json`（对本包无影响：零依赖） | 同上 |
| 必须给**完整** commit hash，不支持短 SHA | 同上 |
| 示例目录须叫 `Samples~`——「波浪号告诉 Unity 忽略该目录的内容」，且这类目录不生成 `.meta` | [Samples](https://docs.unity3d.com/6000.0/Documentation/Manual/cus-samples.html) |

**「专门的发布分支」不在官方文档里**，是社区惯例：`git subtree split --prefix=<包目录> -b upm`，
使用方装 `repo.git#upm`。Mirror 等包这么做。**但 `git subtree split` 不能重命名文件**，
而发布分支恰恰需要把 `Samples/` 转成 `Samples~/`——真要做得写脚本（复制 → 改波浪号 →
补回 `package.json` 的 `samples` 数组 → 提交 → 打 tag），不是一条 subtree 命令的事。

### 两条路线

| 路线 | 使用方怎么装 | 代价 |
|---|---|---|
| **`?path=` 直连**（零维护） | `https://github.com/venusir/xinspector.git?path=/Assets/XInspector#v0.1.0` | 示例会被一并导入，并在对方工程里编译（`Samples/` 无波浪号） |
| **`upm` 发布分支** | `https://github.com/venusir/xinspector.git#v0.1.0` | 多一个发布脚本，每次发版跑一次（可 CI 化）；示例变成按需 Import，最干净 |

**触发条件：** 出现本仓库之外的使用者，且「拷贝文件夹升级会丢改动」真的开始造成麻烦时。
两条路线不冲突——先上 `?path=` 是零成本的，日后加发布分支是**纯新增**，不改包内任何东西。

**前置：** 本仓库的 remote 目前是空的，还没推送过。

---

## 十一、沙盒目录改名　（已评估，暂不做）

2026-10 沙盒缩为纯对照组（展示台并入 `Samples/`）后，`Assets/Sandbox/` 这个名字就有点偏了
——它现在是「诊断台」而不是「什么都往里扔的沙盒」，且 `Samples/` 与 `Sandbox/`
两个名字在 Project 窗口里容易看岔。

**暂不做**：改名要连 `.meta` 一起挪（GUID 才不变），还要跟着改 `Sandbox.unity` 的路径、
`SandboxSceneBuilder.ScenePath` 常量、菜单文案、`check-docs.ps1` 的作用域路径，
以及文档里的多处按名引用。收益只是名字更贴切，与「沙盒到底该装什么」这件事无关
——等有第二个理由要动这块时一并做。

**若要做**：候选名 `Diagnostics/`。`.meta` 跟着走则 GUID 不变、场景内的对象引用不断；
真正要改的是 `ScenePath`、菜单项文本、`check-docs.ps1` 的作用域路径。
注意场景里的**对象名**（`Demo 1` … `Demo 4`）不要跟着改——文档按名引用它们。

---

## 十二、已评估但未采纳

见 [Modules/Pipeline.md](Modules/Pipeline.md) 的「已否决的形状」一节——
那里记的是**已经做过、但换过形状**的决定（无状态绘制器、末端显式追加、
`SerializedObject` 而非纯反射等）及各自的理由。本文件只放**还没做**的候选。
