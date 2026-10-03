using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XInspector.Sandbox;

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
        /// 创建场景：默认摄像机与平行光，外加五个演示对象。
        /// </summary>
        /// <remarks>
        /// 供 <c>-executeMethod</c> 调用，故必须是 public static 且无参。
        /// 五个对象各代表一条集成路径，逐一选中即可对照。
        /// <para>
        /// Demo 1 与 Demo 4 是一组对照：前者**刻意不带任何 XInspector 特性**，
        /// 后者只带 Unity 原生装饰器，两者外观都应与原生 Inspector 一致
        /// ——差别只在「值管道」与「原生装饰器是否流经管线」这两件事上。
        /// </para>
        /// </remarks>
        public static void CreateSandboxScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            AddDemo<DemoComponent>("Demo 1 - Value Pipeline Baseline");
            AddDemo<AttributeDemo>("Demo 2 - Explicit Editor");
            AddDemo<AutoTakeoverDemo>("Demo 3 - Auto Takeover");
            AddDemo<NativeDecoratorDemo>("Demo 4 - Native Decorators (L0)");
            AddDemo<AttributeShowcase>("Demo 5 - L1a Showcase");

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
