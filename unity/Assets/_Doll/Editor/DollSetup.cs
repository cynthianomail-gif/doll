using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DollWorkshop.EditorTools
{
    /// <summary>建立場景（也可以在批次模式用 -executeMethod DollWorkshop.EditorTools.DollSetup.Run / RunMain 執行）。</summary>
    public static class DollSetup
    {
        const string DesignPath = "Assets/_Doll/Scenes/Design.unity";
        const string MainPath = "Assets/_Doll/Scenes/Main.unity";

        static Camera NewCamera()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2.8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.992f, .973f, .945f);
            camGo.transform.position = new Vector3(0, 0, -10);
            return cam;
        }

        static void SetBuildScenes()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var p in new[] { MainPath, DesignPath })
                if (File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        [MenuItem("Doll/建立設計場景")]
        public static void Run()
        {
            Directory.CreateDirectory("Assets/_Doll/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = NewCamera();
            var doll = new GameObject("Doll").AddComponent<DollRenderer>();
            var ui = new GameObject("DesignScreen").AddComponent<DesignScreen>();
            ui.dollRenderer = doll;
            ui.cam = cam;

            EditorSceneManager.SaveScene(scene, DesignPath);
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Doll] 已建立設計場景：" + DesignPath);
        }

        /// <summary>完整遊戲流程場景：接單 → 設計 → 製作 → 交件。</summary>
        [MenuItem("Doll/建立主場景（完整流程）")]
        public static void RunMain()
        {
            Directory.CreateDirectory("Assets/_Doll/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = NewCamera();
            var doll = new GameObject("Doll").AddComponent<DollRenderer>();
            var design = new GameObject("DesignScreen").AddComponent<DesignScreen>();
            design.dollRenderer = doll;
            design.cam = cam;
            design.enabled = false; // 由 GameScreen 在設計步驟打開
            var game = new GameObject("GameScreen").AddComponent<GameScreen>();
            game.dollRenderer = doll;
            game.design = design;
            game.cam = cam;

            EditorSceneManager.SaveScene(scene, MainPath);
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[Doll] 已建立主場景：" + MainPath);
        }
    }
}
