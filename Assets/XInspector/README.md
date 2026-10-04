# XInspector

特性驱动的可编程 Inspector 管线，用于 Unity 6。

> **状态：** `0.1.0-preview.1` — 已实现 **58 个特性**（分组与条件、状态与门控、标签与外观、
> 值绘制、校验与钳制，另有自建分组的基类与编辑器窗口基类）。
> API 尚未稳定，可能随时变更。

## 设计哲学

Unity 默认的 `PropertyDrawer` 模型是**「首个匹配者胜出」**：一个属性最多被一个绘制器画，
那个绘制器必须独自画完标签、字段、修饰、分组的一切。想给一个字段同时加标题和分组框，
就得写一个知道「标题 + 分组框 + int 字段」三件事的绘制器——组合能力从一开始就不存在。

XInspector 换成**一条绘制器链**：所有匹配的绘制器依次叠加，每个都可以选择「做点事，
然后调用下一个」。于是：

```
[BoxGroupDrawer]          画分组框的开头 → 调用下一个
  [TitleAttributeDrawer]  画标题        → 调用下一个
    [UnityFallbackDrawer] 画真正的字段
                           ← 返回
  [BoxGroupDrawer]        画分组框的结尾
```

`[BoxGroup]` 包住 `[Title]`、`[Title]` 再包住字段——这不是特例，而是链条的自然结果。
加一个新特性是**加法**，不需要修改任何既有绘制器。

## 快速开始

### 1. 装进你的工程

把 `XInspector/` 这个目录**整个拷进你工程的 `Assets/` 下**即可。没有 Package Manager
流程，也没有前置依赖——本包 Runtime 侧零第三方依赖。

> **分发方式是拷贝文件夹。** 本包放在 `Assets/` 下，不是 UPM 包，因此升级要
> **覆盖整个目录**——你若改过包内文件，覆盖会丢掉那些改动。
>
> （说明：`?path=` 与包放在哪无关，git 依赖技术上也能引用它。但 `Samples/` 缺波浪号，
> 走那条路示例会被一并导入并在你工程里编译，所以本仓库没有把它作为支持的分发方式。）
>
> 想改成标准 UPM 包：把整个目录移进 `Packages/`（连 `.meta` 一起挪，GUID 不变，不会有
> 断链），并把 `Samples/` 改回 `Samples~/`、在 `package.json` 里补回 `samples` 数组。
> 包内只留了标识字段（名称、版本、描述、Unity 下限），因为描述布局的字段在迁出 UPM
> 之后就不再成立。

### 2. 给类型加特性

```csharp
using UnityEngine;
using XInspector;

[Title("玩家档案")]                    // 类级：标题出现在整个 Inspector 最上方
public class PlayerProfile : MonoBehaviour
{
    [Title("身份")]                    // 成员级：标题紧贴该字段上方
    [BoxGroup("基础")]
    public string playerName = "Player";

    [BoxGroup("基础/属性")]            // 两段路径：自动合成嵌套分组
    public int health = 100;

    [BoxGroup("基础/属性")]
    public float speed = 5f;

    public int ungrouped = 1;          // 未分组字段留在原位，不会被挤到末尾
}
```

### 3. 接管 Inspector

XInspector **不会自动接管**任何类型的 Inspector。为一个类型启用，需要显式写它的编辑器：

```csharp
using UnityEditor;
using XInspector.Editor;

[CustomEditor(typeof(PlayerProfile))]
[CanEditMultipleObjects]
public class PlayerProfileEditor : XInspectorEditor
{
}
```

三行。之后这个类型的 Inspector 就走 XInspector 管线。

> **想让带特性的类型「自动接管」？** 定义脚本宏 `XINSPECTOR_AUTO_EDITOR`
> （Project Settings → Player → Other Settings → Scripting Define Symbols）即可启用
> 一个独立的门控程序集，它用 `DrawDefaultInspector()` 回退，因此**没用到本插件的类型外观不变**。
> 删掉宏即完全恢复 Unity 默认行为——按项目生效，可逆。

## 示例

包内带两个示例，挂到任意物体上即可看，各自另有一份 README：

| 示例 | 演示什么 |
|---|---|
| [`Samples/Overview/`](Samples/Overview/README.md) | **最小可用形态**：类级与成员级标题、嵌套分组、未分组字段夹在分组之间留在原位 |
| [`Samples/AttributeShowcase/`](Samples/AttributeShowcase/README.md) | **逐个特性**：只读、标签、提示、配色、缩进、间距、标签宽、后缀、信息框 |

两个示例都**不含**自动接管——它们走的是上面第 3 步的显式编辑器那条路。

> 上面说过 `Samples/` 缺波浪号，所以这两个示例会随包一并导入并**在你的工程里编译**。
> 不想要的话，直接删掉 `Samples/` 目录即可，包本体不依赖它。

## 核心概念

| 概念 | 职责 |
|---|---|
| `InspectorProperty` | 树上的一個节点：一个字段、一个分组、或根 |
| `PropertyTree` | 整棵树；`Create` → `Update` → `Draw` |
| `PropertyValueEntry<T>` | 属性值的读写抽象。默认后端是 `SerializedObject` |
| `XInspectorDrawer` / `AttributeDrawer<T>` | 绘制器。**共享无状态单例**，行为由 `DrawerPriority` 决定内外层 |
| `DrawerChain` | 一条属性的绘制器链，`CallNext` 调用链上的下一个 |
| `PropertyState` | **每个属性**的可变状态（可见性、只读、Label 覆盖）。绘制器自身不得持有可变字段 |
| `AttributeProcessor` | 在构建期改写特性列表或属性状态，不参与绘制 |

## 已知限制

本轮（`0.1.0-preview.1`）刻意不包含，**不要假设它们可用**：

- **没有自定义序列化后端。** 值后端是 `SerializedObject`，因此只画 Unity 会序列化的东西
  （public 字段 + `[SerializeField]`），画不了任意属性。`[ShowInInspector]` 那类反射成员
  需要一套独立的值后端，尚未实现。
- **没有样式系统。** 一律用 `EditorStyles` 与 `GUI.skin.box` 的默认外观，没有 Odin 那样的配色与图标。
- **编辑器窗口只做了「画自身序列化字段」这一种形态。** 有基类 `XInspectorEditorWindow`
  （继承后声明字段即可，见 `Editor/README.md`），但**没有** Odin 那种检视任意对象的浮空
  Inspector、字段拖拽重排、窗口内 Undo（窗口里的编辑不可撤销，用「重置」补偿）。
- **没有数组 / 列表展开。** 数组整个交给 Unity 的 `PropertyField(includeChildren: true)`，
  因而**本包的特性作用不到数组元素上**。也正因如此，本轮的值绘制器
  （`[FilePath]` `[ValueDropdown]` `[AssetSelector]` `[PreviewField]` 等）
  **一律只作用于单个成员值**，数组形态不支持。没有 `[Button]` / `[OnValueChanged]` 这几种。
- **成员引用的参数只认序列化成员名。** `[ValueDropdown("options")]` 的 `options`、
  `[MinMaxSlider("range")]` 的 `range`、`[FilePath(ParentFolder = …)]` 的插值，
  在 Odin 那边都是「resolved string」（支持 `@` 表达式、`$` 成员引用与方法调用）；
  本包**只认字面量与序列化字段名**，`$`/`@`/方法一律不做。
- **`[ValueDropdown]` 与 `[AssetSelector]` 的弹出层是编辑器自带菜单**，没有搜索框、
  图标与多选；**只声明有真行为的选项**——Odin 的其余选项要么只对列表有意义，
  要么依赖它自建的弹出层，写了会**编译不过**（而不是静默失效）。
- **`[PreviewField]` 的方块是预览、不是控件。** 可编辑的是旁边那个对象字段；
  Odin 的 Ctrl+点击清空、Ctrl+拖拽替换不做。默认高度（64）与默认对齐（Left）由本包定。
- **`[InlineEditor]` 有几处自定值，另有一条刻意的语义差异。** 嵌套深度上限 **4**、
  预览默认尺寸（并排时宽 64、单独时高 64、大预览 128）、默认预览位置在右——三处都由本包定
  （Odin 的默认值存在它的偏好设置里，官网核不到）；超限或引用成环时**告警并退回普通对象字段**。
  值为空的 `CompletelyHidden` 画一行灰字提示，而不是留一片空白（Odin 留白）。
  内嵌里的编辑**会进 Undo**；字段指向正在被检视的对象时，外层可能晚一帧看到变化。
- **没有 `[SerializeReference]` 类型切换。**
- 折叠 / 展开状态**不跨会话持久化**。
- 使用方自己写的 `[CustomPropertyDrawer]` 在可展开类型上**会被绕过**。
- 只支持 IMGUI，不支持 UI Toolkit。
- **条件族只做了三分之一。** 有 `[ShowIf]` `[HideIf]` `[EnableIf]` `[DisableIf]` 与四个
  模式变体（`[HideInEditorMode]` `[HideInPlayMode]` `[DisableInEditorMode]` `[DisableInPlayMode]`），
  以及三个内嵌环境条件（`[ShowInInlineEditors]` `[HideInInlineEditors]` `[DisableInInlineEditors]`）。
  **没有** `[ShowIn]` / `[HideIn]` / `[EnableIn]` / `[DisableIn]`（它们接 `PrefabKind`
  之类的枚举参数）、也没有 `[ShowIfGroup]` / `[HideIfGroup]`。
- **条件只能指向序列化成员。** `[ShowIf("x")]` 的 `x` 必须是 public 字段或
  `[SerializeField]` 私有字段；普通属性、方法、以及写在别的对象上的 `"@other.field"` 语法
  都不支持。条件名不存在时**保持可见并记一条告警**，不会抛异常。

## 依赖

**无。** Runtime 程序集刻意保持零第三方依赖——这是它能被任何项目安全引入的前提。

## 许可

MIT，见 [LICENSE.md](LICENSE.md)。
