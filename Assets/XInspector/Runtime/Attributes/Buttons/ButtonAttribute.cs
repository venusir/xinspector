using System;

namespace XInspector
{
    /// <summary>
    /// 把一个**方法**画成按钮，点击即调用它。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 方法可以无参，也可以带**简单参数**（见下）——带参按钮的参数区默认折叠在按钮同一行的箭头下，
    /// 点箭头展开后逐项填写，再点按钮调用。
    /// </para>
    /// <para>
    /// <b>支持的参数类型：</b> <c>bool</c>、<c>int</c>、<c>float</c>、<c>double</c>、<c>string</c>、
    /// 枚举、<c>UnityEngine.Object</c> 的派生类型、<c>Vector2/3/4</c>、<c>Color</c>。
    /// 出现别的类型（含 <c>ref</c>/<c>out</c>、数组、泛型方法）时**构建期告警**，
    /// 按钮照常画出但不可点，并在下方说明原因——本包不接受「画了个按钮但点了没反应」。
    /// </para>
    /// <para>
    /// <b>只对 Inspector 正在检视的那个对象生效</b>（含它的继承链）。
    /// 嵌套 <c>[Serializable]</c> 类里的 <c>[Button]</c> 不生效——拿到嵌套实例需要一条本包还没有的
    /// 「只读反射路径解析」，那是 L3 反射后端的事。
    /// </para>
    /// <para>
    /// <b>多选时对每个目标各调用一次</b>，参数值所有目标共用；静态方法只调用一次。
    /// Inspector 里会先把所有目标记进 Undo（一次撤销步），窗口路径下不记（窗口内编辑不进 Undo
    /// 是本包的既有契约）。方法抛出的异常被捕获后打进 Console，不会炸掉 Inspector 的绘制。
    /// </para>
    /// <para>
    /// <b>与 Odin 的差异：</b>按钮**一律排在字段之后**，不嵌在字段之间（Odin 会紧跟相关字段）。
    /// 纯反射拿不到「这个方法声明在哪两个字段之间」——字段与方法分属元数据的两张表，各自编号。
    /// 另有几处不做：<c>ButtonStyle</c>（它只管参数区的三种摆法，本包固定一种）、像素高度重载、
    /// 图标一族、<c>DrawResult</c>／<c>DirtyOnClick</c>／布局一族。详见包 README 的「已知限制」。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Button]
    /// private void ResetToDefault() { }
    ///
    /// [Button("重新生成", ButtonSizes.Large)]
    /// private void Rebuild(int seed, bool keepCache) { }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ButtonAttribute : Attribute
    {
        #region Public API

        /// <summary>
        /// 用方法自己的名字作按钮文本。
        /// </summary>
        public ButtonAttribute()
        {
            Size = ButtonSizes.Medium;
        }

        /// <summary>
        /// 用自定义文本作按钮文本。
        /// </summary>
        /// <param name="name">按钮文本；<c>null</c> 或空白表示沿用方法名。</param>
        public ButtonAttribute(string name)
            : this()
        {
            Name = name;
        }

        /// <summary>
        /// 指定按钮高度。
        /// </summary>
        /// <param name="size">高度档位。</param>
        public ButtonAttribute(ButtonSizes size)
        {
            Size = size;
        }

        /// <summary>
        /// 同时指定按钮文本与高度。
        /// </summary>
        /// <param name="name">按钮文本；<c>null</c> 或空白表示沿用方法名。</param>
        /// <param name="size">高度档位。</param>
        public ButtonAttribute(string name, ButtonSizes size)
        {
            Name = name;
            Size = size;
        }

        /// <summary>
        /// 按钮文本；<c>null</c> 或空白表示沿用方法名。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 按钮高度档位，默认 <see cref="ButtonSizes.Medium"/>。
        /// </summary>
        public ButtonSizes Size { get; set; }

        #endregion
    }
}
