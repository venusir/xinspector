using UnityEditor;
using UnityEngine;

namespace XInspector.Editor.AutoEditor
{
    /// <summary>
    /// 自动接管编辑器的公共实现：用到了 XInspector 就接管，否则原样交给 Unity。
    /// <para>
    /// <b>这个程序集由脚本宏 <c>XINSPECTOR_AUTO_EDITOR</c> 门控。</b>
    /// 宏未定义时它根本不参与编译，「全局接管」这件事在项目里就不存在；
    /// 使用方在 Player Settings 里加上宏即启用，删掉即完全恢复 Unity 默认行为。
    /// 按项目生效、可逆——这是它相比无条件全局替换 Inspector 的全部价值所在。
    /// </para>
    /// <para>
    /// <b>为什么这两个子类是 public 而不是 internal：</b> Unity 通过类型反射实例化
    /// 自定义编辑器，internal 类型不能可靠地构造出来。这是本包「默认 internal」规则的
    /// 一处刻意例外，不是疏漏。
    /// </para>
    /// </summary>
    public abstract class XInspectorAutoEditor : XInspectorEditor
    {
        #region Private Fields

        private bool _takeOver;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// 判定是否接管；不接管时连属性树都不建，避免白白做一遍反射遍历。
        /// </summary>
        protected override void OnEnable()
        {
            _takeOver = ShouldTakeOver();

            if (_takeOver)
            {
                base.OnEnable();
            }
        }

        /// <summary>
        /// 接管时走 XInspector 管线，否则走 Unity 原生绘制。
        /// </summary>
        public override void OnInspectorGUI()
        {
            if (!_takeOver)
            {
                // 与接管前外观一致：这是「开了宏也不影响没用到本插件的类型」这句承诺的实现。
                DrawDefaultInspector();
                return;
            }

            base.OnInspectorGUI();
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 判断本次检视是否需要接管。
        /// </summary>
        /// <returns>需要接管返回 <c>true</c>。</returns>
        /// <remarks>
        /// 多选时只要**任一**目标用到了 XInspector 就接管：否则同一批选中里
        /// 有的对象能看到特性效果、有的看不到，行为会显得随机。
        /// </remarks>
        private bool ShouldTakeOver()
        {
            var inspected = targets;
            if (inspected == null || inspected.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < inspected.Length; i++)
            {
                var target = inspected[i];
                if (target != null && AutoEditorDetection.ShouldTakeOver(target.GetType()))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }

    /// <summary>
    /// 所有 MonoBehaviour 的兜底编辑器。更具体的 <c>[CustomEditor]</c> 仍然优先。
    /// </summary>
    [CustomEditor(typeof(MonoBehaviour), true)]
    [CanEditMultipleObjects]
    public sealed class MonoBehaviourAutoEditor : XInspectorAutoEditor
    {
    }

    /// <summary>
    /// 所有 ScriptableObject 的兜底编辑器。更具体的 <c>[CustomEditor]</c> 仍然优先。
    /// </summary>
    [CustomEditor(typeof(ScriptableObject), true)]
    [CanEditMultipleObjects]
    public sealed class ScriptableObjectAutoEditor : XInspectorAutoEditor
    {
    }
}
