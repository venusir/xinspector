# XInspector.Editor

编辑器侧：属性树、绘制器链、特性与绘制器的配对。本文件是**扩展指南**——
写给要往这套管线上加特性的人。

程序集名 `Venusir.Xinspector.Editor`，命名空间仍是 `XInspector.Editor`。

## 管线长什么样

```
PropertyTree            一棵树，对应一次检视
 └ InspectorProperty    节点：根 / 分组 / 成员
    └ DrawerChain       该节点的绘制器链，构建期装配后冻结
       ├ 特性绘制器…    每个特性一格，按权重由外到内
       └ 末端绘制器     结构性的一格，必画
```

绘制时从链的第 0 格开始，它若调用 `CallNextDrawer` 就前进一格。于是形成**层层包裹**：
`[BoxGroup]` 画框 → 调下一个 → `[Title]` 画标题 → 调下一个 → 值的绘制器画字段 →
依次返回，框在最外层闭合。整个过程中没有任何一处代码知道「子节点」的存在，
包裹是链条顺序的自然结果。

## 写一个特性绘制器

```csharp
internal sealed class MyDrawer : AttributeDrawer<MyAttribute>
{
    protected override void DrawPropertyLayout(
        InspectorProperty property, MyAttribute attribute, GUIContent label)
    {
        // 画点东西……
        EditorGUILayout.LabelField(attribute.Text);

        // ……然后包住内侧。不调用它就等于把内侧藏起来（这是有意支持的用法）。
        CallNextDrawer(property, label);
    }
}
```

放进任意编辑器程序集即可，**不需要注册**：`DrawerTypeRegistry` 扫描所有已加载的
编辑器程序集，使用方在自己项目里写的绘制器同样会被发现。

### 三条硬性纪律

1. **绘制器不得有可变字段。** 每种绘制器全工程只实例化一个，供所有属性复用
   （500 字段的 Inspector 不会因此产生两万个对象）。**每属性的可变数据一律放
   `property.State`**（`PropertyState` 提供类型化的附加状态袋）。违反这条的症状是
   「展开一个、全都展开了」，而且很难联想到原因。
   不可变的 `readonly` 字段（如一个标识串）不违反这条。

2. **只做「画 + 调下一个」，把决策挪出去。** 可见性、排序、路径解析、分组归属
   这些判断都应放在可无头测试的代码里，而不是绘制器里。IMGUI 的渲染结果无法有意义地
   断言——伪造 GUI 上下文只会得到「测试断言了自己的 mock」。这条不只是风格：
   它决定了这套代码有没有测试可言。

3. **末端绘制器不要自定义。** 末端由构建期按节点种类显式追加（根与分组接
   `ChildrenDrawer`、成员接 `UnityFallbackDrawer`）。它之所以不进注册表，是因为
   一个写错的匹配条件就能让某属性链为空，症状是「它静默地什么都不画」——最难归因的
   一类问题。显式追加让链条永不为空。

### `[DrawerPriority]` 怎么选

值越小越**靠外层**（越早被调用，能包住后面的）。内置档位：

| 档位 | 值 | 用途 |
|---|---|---|
| `SuperPriority` | -1000 | 需要包住一切的（整页背景、全局禁用遮罩） |
| `AttributePriority` | -100 | 普通特性绘制器的默认值 |
| `ValuePriority` | 0 | 绘制「值本身」的 |
| `FallbackPriority` | `double.MaxValue` | 必须最后执行的 |

用 `double` 而非 `int` 是为了能在档位之间插值（如 -50 落在 Super 与 Attribute 之间），
不必重新编号既有档位。同权重时按特性声明顺序，**这个兜底不能省**——
`List.Sort` 是不稳定排序，只比权重的话同权重格子的顺序会随元素个数变化，
而链条顺序正是可组合性的全部依据。

## 接入方式

XInspector **不自动接管**任何类型。为一个类型启用需要显式写它的编辑器：

```csharp
[CustomEditor(typeof(PlayerProfile))]
[CanEditMultipleObjects]
public class PlayerProfileEditor : XInspectorEditor { }
```

想让带特性的类型自动接管，定义脚本宏 `XINSPECTOR_AUTO_EDITOR`——它启用一个独立的
门控程序集（`Venusir.Xinspector.AutoEditor`），删掉宏即完全恢复 Unity 默认行为。
那套编辑器用 `DrawDefaultInspector()` 回退，因此**没用到本插件的类型外观不变**。

> 本开发工程刻意把这个宏开着，好让门禁覆盖得到那个程序集——见 CLAUDE.md。

## 值的读写

`PropertyValueEntry` 是树与序列化之间的那条缝，当前唯一实现是
`SerializedPropertyValueEntry`（`SerializedObject` 后端）。这个选择买下了
Undo/Redo、预制体覆盖、场景标脏、多对象编辑、域重载后取值这五件事。
代价是只能画 Unity 会序列化的成员——「画普通属性」需要另一套后端，
届时新增一个 `PropertyValueEntry` 派生类即可，树的其余部分不动。

绝大多数绘制器**不需要**碰值入口：把 `property.ValueEntry.SerializedProperty`
交给 `EditorGUILayout.PropertyField` 即可，那条路不经过装箱。
`GetValue`/`SetValue` 会装箱，只服务于少数「要先读到值再决定怎么画」的绘制器。
