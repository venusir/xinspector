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
        /// 创建场景：默认摄像机与平行光，外加一个挂着演示组件的对象。
        /// </summary>
        /// <remarks>
        /// 供 <c>-executeMethod</c> 调用，故必须是 public static 且无参。
        /// </remarks>
        public static void CreateSandboxScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var demo = new GameObject("Demo");
            demo.AddComponent<DemoComponent>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        #endregion
    }
}
