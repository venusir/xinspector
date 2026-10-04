using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XInspector.Sandbox;
using XInspector.Samples;

namespace XInspector.Sandbox.EditorTools
{
    /// <summary>
    /// 生成 Sandbox 场景。属于工程壳，不随包发布。
    /// <para>
    /// 之所以用代码生成而不是手写 <c>.unity</c> 的 YAML：场景文件里那些
    /// RenderSettings / LightmapSettings 区块对版本很敏感，手写容易写出
    /// 「能打开但设置怪异」的文件。让 Unity 自己创建，格式永远是对的。
    /// </para>
    /// </summary>
    public static class SandboxSceneBuilder
    {
        #region Private Fields

        private const string ScenePath = "Assets/Sandbox/Sandbox.unity";

        #endregion

        #region Public API

        /// <summary>
        /// 菜单入口。
        /// </summary>
        [MenuItem("Tools/XInspector/重建 Sandbox 场景")]
        public static void RebuildFromMenu()
        {
            CreateSandboxScene();
            Debug.Log($"[XInspector] 已重建 {ScenePath}");
        }

        /// <summary>
        /// 创建场景：默认摄像机与平行光，外加六个演示对象。
        /// </summary>
        /// <remarks>
        /// 供 <c>-executeMethod</c> 调用，故必须是 public static 且无参。
        /// 六个对象各代表一条集成路径，逐一选中即可对照。
        /// <para>
        /// Demo 1 与 Demo 4 是一组对照：前者**刻意不带任何 XInspector 特性**，
        /// 后者只带 Unity 原生装饰器，两者外观都应与原生 Inspector 一致
        /// ——差别只在「值管道」与「原生装饰器是否流经管线」这两件事上。
        /// </para>
        /// <para>
        /// Demo 2 与 Demo 3 是另一组对照：**同一个渲染结果**，Demo 2 走显式编辑器
        /// （用的就是随包发出去的示例 <see cref="OverviewComponent"/>，
        /// 故顺带验证它本身画得对），Demo 3 靠宏自动接管。
        /// </para>
        /// <para>
        /// Demo 5 与 Demo 6 是自动接管判据的两侧对照：Demo 5 只有 <c>[Button]</c> **方法**、
        /// 一个字段特性都不带，Demo 6 只有 <c>[ShowInInspector]</c> **属性**、同样不带字段特性。
        /// 判据漏扫哪一侧，对应的那个就会静默地退回原生外观（按钮不见 / 属性不出现、都是零告警）。
        /// </para>
        /// <para>
        /// 演示对象的名字被 <c>OdinGap.md</c> 等文档按名引用，**改名会打断那些验证步骤**。
        /// 顶层组件与场景的一一对应由 <c>Tools/check-docs.ps1</c> 守卫。
        /// </para>
        /// </remarks>
        public static void CreateSandboxScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            AddDemo<DemoComponent>("Demo 1 - Value Pipeline Baseline");
            AddDemo<OverviewComponent>("Demo 2 - Explicit Editor");
            AddDemo<AutoTakeoverDemo>("Demo 3 - Auto Takeover");
            AddDemo<NativeDecoratorDemo>("Demo 4 - Native Decorators (L0)");
            AddDemo<ButtonTakeoverDemo>("Demo 5 - Button Takeover");
            AddDemo<ShowInInspectorTakeoverDemo>("Demo 6 - Reflected Member Takeover");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 建一个挂着指定组件的对象。
        /// </summary>
        /// <typeparam name="T">要挂的组件类型。</typeparam>
        /// <param name="name">对象名，同时充当该对象演示什么的提示。</param>
        private static void AddDemo<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.AddComponent<T>();
        }

        #endregion
    }
}
