# Roadmap

候选特性：**该不该做、边界画在哪、依赖什么**。收件人是维护者（选型时看）。

判据与文档落点见 [ModuleAudit.md](ModuleAudit.md)。已做完的东西及其理由**不在这里**
——那属于 [Modules/Pipeline.md](Modules/Pipeline.md)。

排序大致按「先做哪个」，但没写死版本号：本包还在 `0.1.0-preview`，API 稳定前不谈版本规划。

---

## 一、特性处理器层（AttributeProcessor）

**是什么。** 一个在构建期改写属性特性列表或属性状态、但不参与绘制的阶段。
两个钩子：`ProcessSelfAttributes`（改这个节点自己的特性）与
`ProcessChildMemberAttributes`（改子成员的）。

**该不该做：该做，但等它的第一个真实用户。** 它现在一个调用方都没有：

- 原以为「类级 `[Title]`」需要它把特性合成到根节点上，但构建期已经把类型特性
  直接放在根节点，这条路不需要处理器；
- 剩下的用途是 `[ShowIf]` 那类「按条件改状态」与「类级 `[BoxGroup]` 分发到成员」，
  两者都还没做。

为一个没有调用方的扩展点引入抽象基类加发现注册表，是本包一直在砍的那类臆测性 API。
**触发条件：开始做 `[ShowIf]` 或类级分组分发时一并加。** 届时是纯新增，不改任何既有签名。

**边界。** 处理器的两种合法产出：改特性列表、设 `State` 上的可见性/只读/Label。
**处理器不得绘制**——一旦它能画东西，链条的顺序语义就被绕过了。

---

## 二、`[ShowIf]` / `[HideIf]` 及同类条件特性

**该不该做：该做，且是下一个最该做的。** 它是处理器层的第一个用户，也是本包目前
最容易被问「为什么没有」的特性。

**边界与设计要点。**

- 条件求值**不碰 GUI**：处理器装一个 `Func<bool>` 到 `PropertyState.VisibilityResolver`，
  绘制路径每帧调用。这样「要不要显示」可以无头单测——本包的测试策略依赖这一点。
- 条件来源：先支持同类型的**字段名**（`[ShowIf("isAlive")]`），再考虑表达式或方法。
  字段名查找要走 `InspectorProperty` 树而不是反射，否则嵌套与分组内的字段会找不到。
- **多对象编辑下的语义要想清楚**：各目标的条件值不同时，「显示还是不显示」没有显然答案。
  倾向于「任一目标满足即显示」，与自动编辑器那边的取舍一致。

---

## 三、剩余的分组特性

`[FoldoutGroup]`、`[TabGroup]`、`[HorizontalGroup]` 等。

**该不该做：`[FoldoutGroup]` 该做，其余按需。** 它们**不需要改构建期**——
分组装配对具体分组类型一无所知，新增一个分组特性只需：

1. 继承 `PropertyGroupAttribute`（Runtime 侧），覆写 `Combine`；
2. 写一个 `AttributeDrawer<T>`（Editor 侧）画框。

**边界。** `[FoldoutGroup]` 需要**每属性的展开状态**，那是 `PropertyState` 的用途；
它还需要跨会话持久化吗？见第七节。

---

## 四、`[Button]` 与 `[OnValueChanged]`

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

## 五、数组与列表展开

**该不该做：该做，但它是**本表里最重的一项**。**

Unity 的 `PropertyField(includeChildren: true)` 已经把数组画得和原生一样了
——本包现在正是靠它。自己做展开的唯一理由是**让 XInspector 的特性作用于数组元素**
（`[BoxGroup]` 标在元素字段上、`[ShowIf]` 作用于元素）。

**边界。** 一旦自己展开，就要自己处理增删元素、拖拽排序、多选、Undo——
这些 Unity 内部实现都不薄。建议先只做「只读展示 + 元素级特性」，把编辑操作仍交给
Unity 的 `PropertyField`，验证需求真实存在再往下走。

---

## 六、自定义序列化后端与 `[ShowInInspector]`

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

**该不该做：暂缓。**

现状：`[FoldoutGroup]` 尚不存在，所以还没有状态要持久化。等它来了再说，
且要先想清楚存哪——`EditorPrefs`（按键路径，跨项目共享）、`SessionState`（不跨会话）、
还是序列化进场景（会污染资产）。Odin 的做法是前者。

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

## 十一、已评估但未采纳

见 [Modules/Pipeline.md](Modules/Pipeline.md) 的「已否决的形状」一节——
那里记的是**已经做过、但换过形状**的决定（无状态绘制器、末端显式追加、
`SerializedObject` 而非纯反射等）及各自的理由。本文件只放**还没做**的候选。
