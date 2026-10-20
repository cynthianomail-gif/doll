using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 設計衣服：選種類（上衣、褲子、裙子、斗篷、帽子、髮帶……）、尺寸（身形或頭圍）、版型參數、布料和顏色。
    /// 畫面上是客人的娃娃（自由創作時是人台）試穿中的樣子。尺寸要跟娃娃一樣才穿得下。（網頁版 gdesignPanel）
    /// </summary>
    public class GarmentDesign : CraftGame
    {
        public GarmentDesign(Session ss) : base(ss) { }

        Garment G => ss.g;
        public bool Ready => G.cls != null && G.fab.mat != null && G.fab.color != null;

        public override string Hint => G.cls == null ? "先選尺寸（身形）" : ss.order.free ? $"人台：{G.cls}" : "客人的娃娃試穿中";
        public override bool Done => Ready;

        void Changed() { ss.SyncPreview(); ss.ViewDoll.Touch(); }

        public void SetType(string id) { Garments.SetType(G, id, ss.order.free); Changed(); }
        public void SetCls(string cls) { G.cls = cls; Changed(); }
        public void SetMat(MatDef m) { G.fab.mat = m; Changed(); }
        public void SetColor(ColorDef c) { G.fab.color = c; Changed(); }

        public override void CanvasGUI(GameScreen ui)
        {
            var v = ss.ViewDoll;
            ui.CanvasLabel(new Vector2(24, 24), ss.order.free ? v.name : $"{ss.order.who}的娃娃{(v.name != "" ? "「" + v.name + "」" : "")}", 15, Hex("#8a5a3a"), TextAnchor.UpperLeft, true);
            if (!ss.order.free) ui.CanvasLabel(new Vector2(24, 46), "身形和頭圍要自己從客人的話判斷", 13, Hex("#9a8778"), TextAnchor.UpperLeft);
        }

        public override void Panel(GameScreen ui)
        {
            var g = G; var T = g.T; bool free = ss.order.free;
            ui.H2("設計衣服");
            ui.Small("衣服");
            foreach (var t in Garments.Types.Where(x => x.cat == "cloth")) if (ui.Chip($"{t.name}　{t.desc}", g.type == t.id)) SetType(t.id);
            ui.Small("帽子與配件");
            foreach (var t in Garments.Types.Where(x => x.cat == "acc")) if (ui.Chip($"{t.name}　{t.desc}{(t.sizeBy == "none" ? "（通用尺寸）" : "")}", g.type == t.id)) SetType(t.id);

            if (T.sizeBy == "body")
            {
                ui.Small("尺寸（身形）" + (free ? "" : "——看清楚客人的娃娃是什麼身形，做錯會穿不下"));
                for (int i = 0; i < Garments.Classes.Length; i += 3)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < i + 3; j++) { var c = Garments.Classes[j].name; if (ui.Chip(c, g.cls == c)) SetCls(c); }
                    GUILayout.EndHorizontal();
                }
            }
            else if (T.sizeBy == "head")
            {
                ui.Small("尺寸（頭圍）" + (free ? "" : "——帽子要跟娃娃的頭圍一樣才戴得下"));
                GUILayout.BeginHorizontal();
                foreach (var h in Garments.HClasses) if (ui.Chip(h.name, g.cls == h.name)) SetCls(h.name);
                GUILayout.EndHorizontal();
            }
            else ui.Small("尺寸：通用，每隻人形娃娃都能戴");

            foreach (var x in T.ps)
            {
                float v = ui.Slider(x.name, g.p[x.key], x.min, x.max);
                if (!Mathf.Approximately(v, g.p[x.key])) { g.p[x.key] = v; Changed(); }
            }

            ui.Small("布料");
            foreach (var m in DB.Materials) if (ui.Chip($"{m.name}　每 10cm ${m.p10}", g.fab.mat == m)) SetMat(m);
            ui.Small("顏色");
            for (int i = 0; i < DB.Colors.Count; i += 3)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(DB.Colors.Count, i + 3); j++) { var c = DB.Colors[j]; if (ui.Chip(c.name, g.fab.color == c, c.color)) SetColor(c); }
                GUILayout.EndHorizontal();
            }
            if (ui.MainBtn("設計好了，去排版 →", Ready)) ui.Advance();
        }
    }
}
