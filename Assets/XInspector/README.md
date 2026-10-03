# XInspector

特性驱动的可编程 Inspector 管线，用于 Unity 6。

> **状态：** `0.1.0-preview.1` — 只有骨架与一条垂直切片。API 尚未稳定，可能随时变更。

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
- **没有编辑器窗口。** 只有 Inspector。
- **没有数组 / 列表展开**，没有 `[ShowIf]` / `[FoldoutGroup]` / `[Button]` / `[OnValueChanged]`。
- **没有 `[SerializeReference]` 类型切换。**
- 折叠 / 展开状态**不跨会话持久化**。
- 使用方自己写的 `[CustomPropertyDrawer]` 在可展开类型上**会被绕过**。
- 只支持 IMGUI，不支持 UI Toolkit。
- **没有特性处理器层。** 因此「把类级分组特性分发到各个成员」不生效——
  在类型上写 `[BoxGroup]` 会把**整个 Inspector** 框起来，而不是让所有成员归属该分组。
  类级 `[Title]` 不受影响（那是它该有的行为）。
  这一层会在 `[ShowIf]` 那类「按条件改属性状态」的特性到来时一并补上。

## 依赖

**无。** Runtime 程序集刻意保持零第三方依赖——这是它能被任何项目安全引入的前提。

## 许可

MIT，见 [LICENSE.md](LICENSE.md)。
