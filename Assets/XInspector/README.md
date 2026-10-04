# XInspector

特性驱动的可编程 Inspector 管线，用于 Unity 6。

> **状态：** `0.1.0-preview.1` — 已实现 **69 个特性**（分组与条件、状态与门控、标签与外观、
> 值绘制、校验与钳制、按钮、回调、反射成员，另有自建分组的基类与编辑器窗口基类）。
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

- **两套值后端，反射那套是只读的。** 主后端是 `SerializedObject`（只画 Unity 会序列化的东西）；
  另有 `[ShowInInspector]` 那条反射通道，把普通属性、非序列化私有字段、静态成员也画出来。
  反射成员**一律只读**：它们按定义不在 Unity 的序列化里，写进去既不可撤销，也不会随存档保存。
  仍然画不了的：嵌套 `[Serializable]` 类型里的成员、`[SerializeReference]` 的多态引用。
  反射成员的其余边界见下一条。
- **没有样式系统。** 一律用 `EditorStyles` 与 `GUI.skin.box` 的默认外观，没有 Odin 那样的配色与图标。
- **`[ShowInInspector]` 有四条边界，都是刻意的：**
  - **只读。** 见上一条。值照常每帧现读，但没有任何可编辑控件。
  - **多选时各目标值不一致就显示「—」**；成员在一部分目标上不存在时同样按「—」处理
    ——给不出值就不拿其中一个目标的值冒充。
  - **集合只显示摘要**（形如 `List<Int32>（3 项）`），不展开。
  - **标在方法上编译不过**——方法请用 `[Button]`。它与 `[ShowInInspector]` 一起用时，
    需要序列化后端的特性（如 `[PropertyRange]`）**对它无效并会在 Console 说明一句**，
    不会静默失效。
- **编辑器窗口可以检视任意对象了。** 基类 `XInspectorEditorWindow` 默认画窗口自身的字段，
  覆写 `protected virtual object GetTarget()` 就能检视别的对象——不必可序列化，
  甚至不必是 `UnityEngine.Object`（那种目标只收带 `[ShowInInspector]` 的成员，且不可重置）。
  **仍然没有** Odin 那种带对象选择器的浮空 Inspector、字段拖拽重排、窗口内 Undo
  （窗口里的编辑不可撤销，用「重置」补偿，目标不是 Unity 对象时该按钮置灰）。
- **没有数组 / 列表展开。** 数组整个交给 Unity 的 `PropertyField(includeChildren: true)`，
  因而**本包的特性作用不到数组元素上**。也正因如此，本轮的值绘制器
  （`[FilePath]` `[ValueDropdown]` `[AssetSelector]` `[PreviewField]` 等）
  **一律只作用于单个成员值**，数组形态不支持。
- **按钮族有几处边界，都是查证后写死的，不是还没做：**
  - **按钮一律排在字段之后**，不像 Odin 那样紧跟相关字段。字段顺序来自 `SerializedObject`，
    方法顺序只能靠反射，而两者的元数据令牌分属两张表（字段 `0x04`、方法 `0x06`）各自编号，
    跨表比大小没有意义；`GetMembers` 也不按声明顺序返回——**拿不到「声明在哪两个字段之间」**。
  - **只对正在检视的那个对象生效**（含继承链）。嵌套 `[Serializable]` 类型里的 `[Button]` 不生效。
  - **多选时对每个目标各调用一次**，参数值所有目标共用；静态方法只调一次。Inspector 里会把所有目标
    记进一步 Undo；**窗口路径下不记**（窗口内编辑不进 Undo 是本包对窗口的一贯约定）。
  - **带参方法的参数只支持** `bool`／`int`／`float`／`double`／`string`／枚举／`UnityEngine.Object`
    派生／`Vector2-4`／`Color`／`Rect`；`ref`／`out`／数组／泛型方法**给出原因并把按钮画成禁用**，
    不会静默什么都不发生。参数区固定为 Odin 的 `CompactBox` 形态（按钮与折叠箭头同一行）。
  - **`[InlineButton]` 只标在字段上**，方法必须无参；解析失败时按钮禁用并把原因挂在 Tooltip 上，
    **字段本身照常绘制**。
  - 方法抛出的异常被**捕获后打进 Console**，不打断 Inspector 的绘制。
- **回调族有六条边界：**
  - **方法名一律只认本类型上的方法名**（含继承链），不认 Odin 的 `$`／`@` 表达式与带参调用。
  - **`[OnStateUpdate]` 的时机是本包自定的**：每趟 GUI **布局**跑一次。Odin 跑在它自己的
    state update 循环里，本包没有那个循环。
  - **`[OnValueChanged]` 只在「用户在这一趟里改动了这个值」时触发**，不监听程序侧或别的对象的
    改动；参数类型覆盖整型、浮点、布尔、字符串、枚举、对象引用、`Vector2/3/4`、`Color`、`Rect`、
    `Quaternion`，**其余类型（数组、`Bounds`、动画曲线……）告警一次且不触发**
    ——本包不肯为它们每帧装箱，也不肯假装检测得到。
  - **`[CustomContextMenu]` 的菜单出现在字段自己那一行**，不是 Unity 头部的右键菜单
    （头部由 Unity 掌管，没有公开注入点）；只做标在字段上的形式。
  - **`[OnInspectorGUI]` 只做标在方法上的无参形式**，且**只画一次、用第一个目标**
    （多选时按目标各画一遍只会把同一段界面叠 N 次）。
  - **`[OnInspectorInit]` / `[OnInspectorDispose]` 不是对象生命周期**——它们说的是
    「这个 Inspector 开始/停止看这个对象了」，与对象本身是否被销毁无关。
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
- **条件族还差两族。** 有 `[ShowIf]` `[HideIf]` `[EnableIf]` `[DisableIf]`、四个模式变体
  （`[HideInEditorMode]` `[HideInPlayMode]` `[DisableInEditorMode]` `[DisableInPlayMode]`）、
  三个内嵌环境条件（`[ShowInInlineEditors]` `[HideInInlineEditors]` `[DisableInInlineEditors]`），
  以及四个**预制体上下文**条件（`[ShowIn]` `[HideIn]` `[EnableIn]` `[DisableIn]`，接 `PrefabKind`）。
  **没有** `[ShowIfGroup]` / `[HideIfGroup]`。
- **预制体上下文族（含 `[RequiredIn]` `[DisallowModificationsIn]`）有几处自定的边界：**
  - **模型预制体归入 `Regular`。** Odin 的 `PrefabKind` 里没有模型对应的成员；归成「不匹配」
    会让 `[ShowIn(PrefabKind.PrefabAsset)]` 在模型资产上**静默隐藏**。
  - **普通 C# 对象与非预制体资产（`ScriptableObject`、材质……）没有上下文**——既不满足
    `[ShowIn]` 也不满足 `[HideIn]`。`PrefabKind.NonPrefabInstance` 说的是**场景里**不属于
    任何预制体的组件与 GameObject，不是「什么预制体都不是」的所有东西。
  - **多选时要求全部目标都匹配**（已销毁的目标不参与判定；一个存活目标都没有时不匹配）。
    同时选中一个预制体资产与一个场景对象，两边的字段都不会显示——预制体上下文是
    整个选择的性质，不是某一个目标的事。
  - **`[RequiredIn]` 的 `ErrorMessage` 只做纯文本**（Odin 支持它的表达式语法，本包不做）；
    层级固定为错误，没有级别参数（官方就没有）。
  - **`[DisallowModificationsIn]` 的「已经改过」用 `SerializedProperty.prefabOverride` 判定**，
    因此**多选时不报**（多选下它反映的是谁，官方未写明），`[ShowInInspector]` 的反射成员
    也不报（没有值入口，会在 Console 说明一句）。**只读那一半照常生效。**
- **条件可以指向三类成员**，按这个顺序找：序列化成员（public 字段或 `[SerializeField]`
  私有字段）→ 普通字段 / 属性 → **无参、非泛型、返回 `bool`** 的方法。
  名字不存在、类型不是 `bool`、方法带参数时一律**保持可见并记一条告警**，不会抛异常；
  写在别的对象上的 `"@other.field"` 语法**不支持**。
- **同一个成员上挂多个条件时，后装入者覆盖前者**（`PropertyState` 只有一个求值器槽）。
  覆盖次序是确定的（处理器按优先级、同优先级按类型名排），但**不是「全部满足」语义**
  ——要表达合取请写一个返回 `bool` 的条件成员。

## 依赖

**无。** Runtime 程序集刻意保持零第三方依赖——这是它能被任何项目安全引入的前提。

## 许可

MIT，见 [LICENSE.md](LICENSE.md)。
