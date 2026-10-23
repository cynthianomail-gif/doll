using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 上色（搪膠和木頭）：選顏料、筆刷大小，在娃娃身上按住拖曳自由畫；噴漆適合畫腮紅這類漸層；木頭可以上亮光漆。
    /// 只畫得到娃娃身上。每用一種顏色 $2、亮光漆 $5。（網頁版 paint 步驟）
    /// </summary>
    public class PaintGame : CraftGame
    {
        /// <summary>顏料：白、黑，加上布料的 12 種顏色。</summary>
        public static readonly List<ColorDef> Paints = new List<ColorDef>
        {
            new ColorDef { id = "white", name = "白", color = Color.white, tags = new string[0] },
            new ColorDef { id = "black", name = "黑", color = new Color(.165f, .165f, .18f), tags = new[] { "酷" } },
        }.Concat(DB.Colors).ToList();

        public ColorDef color = DB.Col("pink");
        public float size = 9;
        public bool spray;

        public PaintGame(Session ss) : base(ss) { }

        PaintData P => D.paint;
        void Changed() { P.Touch(); D.Touch(); }

        public void Undo() { if (P.strokes.Count > 0) { P.strokes.RemoveAt(P.strokes.Count - 1); Changed(); } }
        public void ClearAll() { P.strokes.Clear(); Changed(); }

        public override string Hint => "上色：在娃娃身上自由畫";
        public override bool Done => true;

        public override void Down(Vector2 p)
        {
            var st = new PaintStroke { colorId = color.id, color = color.color, size = size, spray = spray };
            st.pts.Add(p); P.strokes.Add(st); Changed();
        }

        public override void Move(Vector2 p, Vector2 prev)
        {
            if (P.strokes.Count == 0) return;
            var st = P.strokes[P.strokes.Count - 1];
            if (Vector2.Distance(st.pts[st.pts.Count - 1], p) > (st.spray ? 3 : 2)) { st.pts.Add(p); Changed(); }
        }

        public override void Draw(Sketch under, Sketch over)
        {
            if (ptr.inside) over.P.Ring(ptr.p, size / 2 * (spray ? 3 : 1), 1, new Color(.24f, .16f, .12f, .5f), 24);
        }

        public override void Panel(GameScreen ui)
        {
            ui.H2("上色");
            ui.Small("顏料");
            for (int i = 0; i < Paints.Count; i += 4)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(Paints.Count, i + 4); j++) if (ui.Chip(Paints[j].name, color == Paints[j], Paints[j].color)) color = Paints[j];
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            foreach (var (n, l) in new[] { (4f, "細"), (9f, "中"), (16f, "粗") }) if (ui.Chip(l, Mathf.Approximately(size, n))) size = n;
            if (ui.Chip("🖌 筆刷", !spray)) spray = false;
            if (ui.Chip("💨 噴漆", spray)) spray = true;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (ui.Btn("↶ 復原", P.strokes.Count > 0)) Undo();
            if (ui.Btn("全部清掉", P.strokes.Count > 0)) ClearAll();
            GUILayout.EndHorizontal();
            if (D.material == "wood" && ui.Chip($"✨ 亮光漆：{(P.varnish ? "有" : "沒有")}（$5）", P.varnish)) { P.varnish = !P.varnish; Changed(); }
            ui.Small($"顏料費 ${ss.PaintCost()}（每種顏色 $2）");
            if (ui.MainBtn("上色好了，去裝飾 →")) ui.Advance();
        }
    }
}
