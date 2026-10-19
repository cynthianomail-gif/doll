using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DollWorkshop.EditorTools
{
    /// <summary>
    /// 不進 Play 模式，直接把幾種範例娃娃畫成 PNG（存到專案根目錄的 Captures/）。
    /// 批次模式：-executeMethod DollWorkshop.EditorTools.DollPreview.Capture
    /// </summary>
    public static class DollPreview
    {
        [MenuItem("Doll/輸出範例娃娃截圖")]
        public static void Capture()
        {
            Directory.CreateDirectory("Captures");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 2.8f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.992f, .973f, .945f);
            camGo.transform.position = new Vector3(0, 0, -10);
            var r = new GameObject("Doll").AddComponent<DollRenderer>();

            Shot(r, cam, "animal", s =>
            {
                s.fab["body"] = new Fab(DB.Mat("minky"), DB.Col("pink"));
                Part(s, "ears", "long"); s.fab["earInner"] = new Fab(DB.Mat("cotton"), DB.Col("cream"));
                Part(s, "arms", "nub"); Part(s, "feet", "round"); s.fab["feet"] = new Fab(DB.Mat("felt"), DB.Col("brick"));
                Part(s, "belly", "heart"); s.fab["belly"] = new Fab(DB.Mat("cotton"), DB.Col("cream"));
            });
            Shot(r, cam, "human", s =>
            {
                s.kind = "human"; s.fab["body"] = new Fab(DB.Mat("cotton"), DB.Col("skin"));
                s.hair = DB.Hairs.Find(h => h.id == "twin");
                Part(s, "ears", "pointy"); s.fab["ears"] = new Fab(DB.Mat("minky"), DB.Col("cocoa")); s.fab["earInner"] = new Fab(DB.Mat("cotton"), DB.Col("pink"));
            });
            Shot(r, cam, "vinyl", s =>
            {
                s.material = "vinyl"; s.body = DB.Bodies.Find(b => b.id == "ghost");
                s.fab["body"] = new Fab(DB.Mat("vinyl"), DB.Col("coal"));
                Part(s, "ears", "horn"); Part(s, "arms", "wing"); s.fab["arms"] = new Fab(DB.Mat("vinyl"), DB.Col("grape"));
            });
            Shot(r, cam, "wood", s =>
            {
                s.material = "wood"; s.kind = "human"; s.fab["body"] = new Fab(DB.Mat("wood"), DB.Col("pine"));
                s.hair = DB.Hairs.Find(h => h.id == "hime"); s.fab["hair"] = new Fab(DB.Mat("wood"), DB.Col("walnut"));
                s.shp["torso"]["w"] = 1.4f; s.shp["head"]["size"] = 1.15f;
            });
            Debug.Log("[Doll] 範例截圖已輸出到 Captures/");
        }

        static void Part(DollState s, string key, string id) { s.parts[key] = DB.Parts[key].Find(id); s.ResetAdj(key); }

        static void Shot(DollRenderer r, Camera cam, string name, System.Action<DollState> setup)
        {
            r.state = new DollState();
            setup(r.state);
            r.state.Rebuild();
            r.highlight = null;
            r.Build();
            var rt = new RenderTexture(1600, 1120, 24);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            File.WriteAllBytes($"Captures/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }
    }
}
