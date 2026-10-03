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
| 用在**类**上 | ⚠️ 目前会把**整个 Inspector** 框起来，而**不是**让所有成员归属该分组。原因见下 |

#### 已知不足：类级 `[BoxGroup]`

把分组特性标在类型上、期望「该类所有成员都进这个分组」是常见的直觉，
但本版做不到——分组特性是按成员收集的，类上的那一个只会落在根节点上。

要它生效需要一层「特性处理器」：读类上的分组特性，克隆后分发到各成员。
该层尚未实现（它会和 `[ShowIf]` 那类「按条件改属性状态」的特性一起到来）。
在那之前，请把 `[BoxGroup]` 写在每个成员上。

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
  C# 的限制：跨程序集覆写 `protected internal` 成员时，覆写方必须声明为 `protected`
  （不能再写 `internal`）。
- **`CloneForPath` 默认用 `MemberwiseClone`**，因此子类字段会被自动带到合成的祖先节点上
  （语义是「祖先继承后代的呈现设定」）。若子类维护了额外的引用型状态，记得一并深拷贝。

绘制是分组**绘制器**的职责，与特性无关——换一种视觉呈现不需要动特性。
