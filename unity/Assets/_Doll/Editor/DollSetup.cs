using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DollWorkshop.EditorTools
{
    /// <summary>建立設計場景（也可以在批次模式用 -executeMethod DollWorkshop.EditorTools.DollSetup.Run 執行）。</summary>
    public static class DollSetup
    {
        const string ScenePath = "Assets/_Doll/Scenes/Design.unity";

        [MenuItem("Doll/建立設計場景")]
        public static void Run()
        {
            Directory.CreateDirectory("Assets/_Doll/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2.8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.992f, .973f, .945f);
            camGo.transform.position = new Vector3(0, 0, -10);

            var doll = new GameObject("Doll").AddComponent<DollRenderer>();
            var ui = new GameObject("DesignScreen").AddComponent<DesignScreen>();
            ui.dollRenderer = doll;
            ui.cam = cam;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Doll] 已建立設計場景：" + ScenePath);
        }
    }
}
