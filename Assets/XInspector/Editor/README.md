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

## 写一个特性处理器

绘制器决定「这个属性怎么画」；**处理器**决定「画之前发生什么」——它改的是特性列表或
`PropertyState`，不产出任何像素。凡是不需要画东西就成立的判断，都该放在这里。

```csharp
internal sealed class MyProcessor : AttributeProcessor<MyAttribute>
{
    protected override void ProcessSelf(
        InspectorProperty property, MyAttribute attribute, IList<Attribute> attributes)
    {
        // 装一个每帧求值的可见性解析器——不碰 GUI，因此可以无头测试
        property.State.VisibilityResolver = () => /* … */ true;
    }
}
```

放进任意编辑器程序集即可，**不需要注册**（与绘制器同样扫全部程序集）。

### 两个钩子

| 钩子 | 触发者 | 典型用途 |
|---|---|---|
| `ProcessSelf` | 属性**自身**带该特性 | `[ShowIf]` 装可见性解析器 |
| `ProcessChildMember` | **父属性**带该特性 | 类级 `[BoxGroup]` 分发到各成员 |

两个都继承 `AttributeProcessor<TAttribute>` 就都有；若需要**一次看到全部实例**
（例如「多个分组只取第一个」），则继承非泛型的 `AttributeProcessor` 并覆写
`ProcessChildMemberAttributes`——泛型基类会逐个实例回调，那正是两种基类都留着的理由。

### 三条纪律

1. **不得绘制。** 一旦它能画东西，绘制器链的顺序语义就被绕过了——「谁包住谁」将不再只由
   `DrawerPriority` 决定。
2. **无状态共享单例。** 与绘制器一样，每种类型全工程只实例化一个，不得持有可变字段；
   跨调用要留的东西放进 `PropertyState`。
3. **注入特性必须克隆。** 把父级那个实例直接塞给子成员，会让几十个成员共享它，
   后续任何一处改写都串到所有人身上。用 `CloneForPath`。

### 构建期的顺序是契约

处理器在**分组装配之前**、**挂链之前**跑。前者是因为类级分组特性是处理器注入的，
装配必须看到它；后者是因为注入的特性会改变链条的构成。顺序反过来，症状是
**静默地不生效**——所以这条写在 `PropertyTreeBuilder` 的注释里，别改。

**一处刻意的例外：`[ToggleGroup]` 的开关解析发生在首次绘制时**（结果缓存进
`PropertyState`）。原因是分组节点在这个阶段**还不存在**（它由分组装配创建），
处理器没有可以挂上去的节点。这不是疏忽：`SerializedProperty` 是活句柄（跨 `Update()`
有效），解析一次就够，与条件族的用法一致。若将来出现第二个同类需求，再考虑给构建期
加一个「分组装配之后」的后置阶段。

## 在窗口里复用 `PropertyTree`

管线本身与 Inspector 无关：给它一个 `SerializedObject`，它就能画。窗口基类
`XInspectorEditorWindow` 就是这件事的成品，你不必自己接线：

```csharp
internal sealed class MyWindow : XInspectorEditorWindow
{
    [Title("设置")] [BoxGroup("基础")] public int health = 100;

    [MenuItem("Tools/我的窗口")]
    private static void Open() => GetWindow<MyWindow>("我的窗口");
}
```

但如果你要在**自己的容器**里嵌一块属性树（窗口里的一个面板、一个自定义 Editor 的某一段），
下面几条契约得自己守。它们就是 `PropertyTreeHost` 在替你做的事。

### 1. 三个调用必须配对

```csharp
var so = tree.SerializedObject;
so.Update();                                  // 把磁盘上的值拉进内存副本
tree.Draw();                                  // 绘制
so.ApplyModifiedPropertiesWithoutUndo();      // 把内存副本写回
```

这是 `XInspectorEditor.OnInspectorGUI` 的同一套配对，**只有最后一步不同**：窗口用不带 Undo 的版本。
带 Undo 的版本会往**全局** Undo 栈写记录，而窗口字段既不属于场景也不属于资产——
用户按 Ctrl+Z 想撤销场景操作，撤销到的却是窗口里的一个数字。**在 Inspector 里注册 Undo 是特性，
在窗口里是污染。**

代价是窗口内的编辑不可撤销，用「重置」补偿（`PropertyTreeHost.ResetToDefaults`）。

### 2. `labelWidth` / `wideMode` 要自己设，而且要还原

Inspector 里这两项由宿主设好，窗口里不会自动来。不设的话标签会挤成一列、或者跑到字段上方。

```csharp
var savedLabel = EditorGUIUtility.labelWidth;
var savedWide = EditorGUIUtility.wideMode;
try
{
    EditorGUIUtility.labelWidth = Mathf.Clamp(width * 0.38f, 90f, 220f);
    EditorGUIUtility.wideMode = width >= 320f;
    // …绘制…
}
finally
{
    EditorGUIUtility.labelWidth = savedLabel;   // 必须还原
    EditorGUIUtility.wideMode = savedWide;
}
```

**还原是硬要求**：这两个是全局状态，同一帧里还有别的窗口与 Inspector 要用它们。

### 3. 临时对象用 `HideFlags.HideAndDontSave`

为预览之类目的造的一次性 `ScriptableObject`，标志要设成 `HideFlags.HideAndDontSave`：
不进 Hierarchy、不进 Inspector、不被保存，且**不被 `Resources.UnloadUnusedAssets` 回收**
（它含 `DontUnloadUnusedAsset`）——最后一条是必需的，否则对象可能在你脚下消失。
释放时在编辑模式下用 `DestroyImmediate`，`Destroy` 会报「may not be called from edit mode」。

### 4. `SerializedObject` 也要释放

`SerializedObject` 实现了 `IDisposable`，它持有原生句柄。自己 `new` 出来的要自己 `Dispose`，
并且**绝不能把它存进可序列化字段**——域重载之后它会指向已失效的东西。

### 5. 成员过滤：画窗口自身时要传

`EditorWindow` 是 `ScriptableObject`，自带 7 个带 `[SerializeField]` 的内部字段
（`m_MinSize`、`m_MaxSize`、`m_TitleContent`、`m_Pos`、`m_SerializedDataModeController`、
`m_ViewDataDictionary`、`m_OverlayCanvas`）。不管它们，你的窗口就是「一堆 Unity 内部状态
+ 自己那几个字段」，其中后三个还会各自展开成一整棵子树。

用 `WindowMemberFilter.For(typeof(你的窗口基类))` 产出的过滤器——规则是**字段的声明类型必须
可赋给那个基类**，Unity 的内部字段声明在 `EditorWindow`/`ScriptableObject` 上，一句挡掉；
脚本槽位 `m_Script` 同样被排除。

### 6. 建树失败要变成可见的错误

一个写坏的目标（例如 `[BoxGroup("")]` 会在构建期抛 `ArgumentException`）不该让你的窗口白屏。
`PropertyTreeHost` 把异常转成可读的 `Error` 并画成 `HelpBox`——**显式可见，不是静默吞掉**。

但**绘制期的异常不该被捕获**：那类要修在绘制器里，吞掉只会让「画错了」变成「什么都没画」。

### 别自己重写这些

`PropertyTreeHost` 已经把上面六条都实现了，且是 `internal` 的——它就在
`Editor/Windows/PropertyTreeHost.cs`，直接读比照着重写快。窗口基类与（将来的）入门窗口
预览面板共用它。

## 释放：谁建谁销

树上会出现**必须显式销毁**的资源——典型的是内嵌编辑器为被引用对象创建的嵌套 `Editor` 实例
（绘制器不得持有可变字段，它只能待在 `PropertyState` 的附加状态袋里）。它们沿一条链释放：

```
状态的 Dispose                释放自己持有的原生对象（如 DestroyImmediate(Editor)）
  ← PropertyState.Reset()      释放袋里实现 IDisposable 的那些，然后清空袋
    ← PropertyTree.Dispose()   递归复位每个节点的状态
      ← 宿主：XInspectorEditor.OnDisable、PropertyTreeHost（换目标 / Reload / Dispose 都经 Clear）
```

三条约定：

1. **可释放的附加状态必须把释放写进自己的 `Dispose`。** `PropertyState.Reset()` 不认识任何具体
   状态类型，它只负责调用 `IDisposable.Dispose`——不写进那里就没人会替你释放。
2. **`PropertyTree.Dispose()` 只管节点状态，不管 `SerializedObject`。** 后者的所有权在调用方：
   Inspector 路径上它是 Unity 给的，宿主路径上由 `PropertyTreeHost` 自己建、自己释放。
3. **继承 `XInspectorEditor` 覆写 `OnDisable` 时必须调 `base.OnDisable()`。** 不调就会泄漏，
   而症状（原生对象随每次选中/关闭累积）与覆写处看起来毫无关系。

释放是幂等的：`Reset()` 之后袋已清空，再 `Dispose()` 一次无事可做。另外**销毁一个 `Editor`
实例会触发它自己的 `OnDisable`**（已实测，守卫在 `Tests/Editor/PropertyTreeDisposalTests.cs`），
所以嵌套那一层的属性树会顺着同一条链连带释放——这也是内嵌编辑器不需要额外清理机制的原因。

## 值的读写

`PropertyValueEntry` 是树与序列化之间的那条缝，当前唯一实现是
`SerializedPropertyValueEntry`（`SerializedObject` 后端）。这个选择买下了
Undo/Redo、预制体覆盖、场景标脏、多对象编辑、域重载后取值这五件事。
代价是只能画 Unity 会序列化的成员——「画普通属性」需要另一套后端，
届时新增一个 `PropertyValueEntry` 派生类即可，树的其余部分不动。

绝大多数绘制器**不需要**碰值入口：把 `property.ValueEntry.SerializedProperty`
交给 `EditorGUILayout.PropertyField` 即可，那条路不经过装箱。
`GetValue`/`SetValue` 会装箱，只服务于少数「要先读到值再决定怎么画」的绘制器。
