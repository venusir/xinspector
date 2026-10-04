using System;

namespace XInspector
{
    // 这两个都标在**字段**上，都按名字指向本对象上的一个无参方法。
    // 名字的解析在构建期完成（编辑器侧的 CallbackProcessors + MethodResolver），
    // 与 [InlineButton] 共用同一套「只认本类型上的方法名」的规矩。

    /// <summary>
    /// 字段的值**变了**时调用指定的方法。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 判据是「绘制这个字段的那一趟里，值前后不一致」——也就是**用户在这一趟里改动了它**。
    /// 因此不必跨帧记旧值，也就没有「第一帧误报一次」的问题。
    /// </para>
    /// <para>
    /// 参数的数值类型覆盖面：整型、浮点、布尔、字符串、枚举、对象引用、
    /// <c>Vector2/3/4</c>、<c>Color</c>、<c>Rect</c>、<c>Quaternion</c>。
    /// 其余类型（数组、<c>Bounds</c>、动画曲线……）**告警一次且不触发**——
    /// 本包不肯为它们每帧装箱，也不肯假装检测得到。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异：</b>不声明 <c>includeChildren</c>（默认就是 <c>false</c>，
    /// 而 <c>true</c> 需要深度比对子值）、<c>InvokeOnInitialize</c>、<c>InvokeOnUndoRedo</c>；
    /// 方法名同样只认本类型上的方法，不认 <c>$</c>／<c>@</c> 表达式。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [OnValueChanged(nameof(ClampHealth))]
    /// public int health = 100;
    ///
    /// private void ClampHealth()
    /// {
    ///     health = Mathf.Clamp(health, 0, maxHealth);
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class OnValueChangedAttribute : Attribute
    {
        /// <summary>
        /// 以要调用的方法名构造。
        /// </summary>
        /// <param name="methodName">本对象上的无参方法名。</param>
        /// <exception cref="ArgumentException"><paramref name="methodName"/> 为 null 或空白。</exception>
        public OnValueChangedAttribute(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName))
            {
                throw new ArgumentException("值变化时要调用的方法名不能为空。", nameof(methodName));
            }

            MethodName = methodName.Trim();
        }

        /// <summary>值变化时要调用的方法名。</summary>
        public string MethodName { get; }
    }

    /// <summary>
    /// 在字段上**右键**时，右键菜单里多出这一项。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 菜单位置是**字段自己那一行**：右键落在该字段的矩形里才弹。菜单项文本就是
    /// <see cref="MenuItem"/>，选中后调用 <see cref="MethodName"/> 指定的无参方法。
    /// </para>
    /// <para>
    /// 同一个字段可以挂多项，按声明顺序进菜单。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异：</b>不做标在方法上的形式（那要接管整个 Inspector 的右键菜单，
    /// 而 Unity 的头部菜单没有公开注入点）。方法名同样只认本类型上的方法。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [CustomContextMenu("重新随机", nameof(Reroll))]
    /// public int damage = 10;
    ///
    /// private void Reroll()
    /// {
    ///     damage = UnityEngine.Random.Range(1, 20);
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public sealed class CustomContextMenuAttribute : Attribute
    {
        /// <summary>
        /// 以菜单项文本与要调用的方法名构造。
        /// </summary>
        /// <param name="menuItem">菜单里显示的文本。</param>
        /// <param name="methodName">本对象上的无参方法名。</param>
        /// <exception cref="ArgumentException">两个参数有一个为 null 或空白。</exception>
        public CustomContextMenuAttribute(string menuItem, string methodName)
        {
            if (string.IsNullOrWhiteSpace(menuItem))
            {
                throw new ArgumentException("菜单项文本不能为空。", nameof(menuItem));
            }

            if (string.IsNullOrWhiteSpace(methodName))
            {
                throw new ArgumentException("菜单项要调用的方法名不能为空。", nameof(methodName));
            }

            MenuItem = menuItem.Trim();
            MethodName = methodName.Trim();
        }

        /// <summary>菜单里显示的文本。</summary>
        public string MenuItem { get; }

        /// <summary>选中后要调用的方法名。</summary>
        public string MethodName { get; }
    }
}
