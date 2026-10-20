using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DollWorkshop
{
    /// <summary>一塊要裁的布片。P 是以布片中心為原點、娃娃實際大小的外形；排在布上時縮成一半（LS）。</summary>
    public class Unit
    {
        public string name, slot, main;
        public int mult, rot, bolt;
        public List<Vector2> P;
        public Vector2 off;
        public float size, x, y;
    }

    /// <summary>一捲布（同一種布料＋顏色）。上面有兩個隨機的污漬。</summary>
    public class Bolt
    {
        public MatDef mat; public ColorDef color;
        public readonly List<int> units = new List<int>();
        public readonly List<Vector2> flaws = new List<Vector2>();
    }

    /// <summary>
    /// 排版：把布片拖到布上，越省布越便宜。布紋方向要對、不能重疊、不能超出布邊，壓到污漬會留在娃娃身上。
    /// （網頁版 initLayout / layoutCheck / commitLayout）
    /// </summary>
    public class LayoutGame : CraftGame
    {
        public static readonly Rect FAB = new Rect(40, 110, 720, 230);
        public const float LS = .5f;

        public readonly List<Unit> units;
        public readonly List<Bolt> bolts = new List<Bolt>();
        public int cur, sel = -1;
        int dragI = -1; Vector2 dragOff;

        public class UInfo { public bool inside, ov, grain; public int flaws; }
        public class BInfo { public bool allIn, ov, valid; public int len, cost; }
        public List<List<Vector2>> polys;
        public List<UInfo> uInfo;
        public List<BInfo> bInfo;
        public bool Valid;
        public int Cost;

        public LayoutGame(Session ss) : base(ss)
        {
            units = ss.IsGarment ? GarmentUnits(ss) : BuildUnits(D);
            var byKey = new Dictionary<string, Bolt>();
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i]; var f = ss.FabOfSlot(u.slot); string k = f.mat.id + "_" + f.color.id;
                if (!byKey.TryGetValue(k, out var b)) { b = new Bolt { mat = f.mat, color = f.color }; byKey[k] = b; bolts.Add(b); }
                u.bolt = bolts.IndexOf(b); b.units.Add(i);
            }
            foreach (var b in bolts)
            {
                float x = 50;
                foreach (int i in b.units)
                {
                    var u = units[i]; var bb = SM.Bbox(u.P); float w = Mathf.Max(bb.width * LS, 70);
                    u.x = x + w / 2; u.y = 445; x += w + 18;
                }
                for (int i = 0; i < 2; i++) b.flaws.Add(new Vector2(FAB.x + 50 + Random.value * 420, FAB.y + 25 + Random.value * (FAB.height - 50)));
            }
            Check();
        }

        /// <summary>依設計列出要裁的布片（主體前後片、頭髮、耳朵、手腳、肚子……），名稱與份數和網頁版一樣。</summary>
        public static List<Unit> BuildUnits(DollState d)
        {
            var U = new List<Unit>();
            List<Vector2> Sc(List<Vector2> P, float k) => P.Select(p => p * SM.R * k).ToList();
            void Add(string name, string slot, int mult, List<Vector2> Ppx, string mid = null)
            {
                var b = SM.Bbox(Ppx); var off = b.center;
                U.Add(new Unit { name = name, slot = slot, mult = mult, main = mid, P = Ppx.Select(p => p - off).ToList(), off = off, size = Mathf.Max(b.width, b.height) });
            }
            foreach (var m in Enumerable.Reverse(d.Mains())) Add(m.name, "body", 2, SM.Outline(m.sh, 1.06f * m.r / SM.R, 120, 0, 0), m.id);
            var hg = d.GetHair();
            if (hg != null)
            {
                var c = new Vector2(hg.head.cx, hg.head.cy);
                List<Vector2> Rel(List<Vector2> P) => P.Select(p => p - c).ToList();
                Add("瀏海", "hair", 1, Rel(hg.cap));
                if (hg.back != null) Add("後髮", "hair", 1, Rel(hg.back));
                if (hg.tails != null) Add("馬尾", "hair", 2, Rel(hg.tails[0]));
            }
            float Pk(string key) => Mathf.Max(d.AdjOf(key, -1).scale, d.AdjOf(key, 1).scale) * d.GetMain(d.HostOf(key)).r / SM.R;
            var e = d.parts["ears"];
            if (!e.IsNone)
            {
                float k = Pk("ears");
                Add(e.rname == "角" ? "小角" : "耳朵", "ears", 4, Sc(e.P, 1.12f * k));
                if (e.inner) { var c = SM.Centroid(e.P); Add("耳朵內側", "earInner", 2, e.P.Select(p => new Vector2((c.x + (p.x - c.x) * .6f) * SM.R * k, (c.y + (p.y - c.y) * .66f) * SM.R * k)).ToList()); }
            }
            var a = d.parts["arms"]; if (!a.IsNone) Add(a.rname, "arms", 4, Sc(a.P, 1.12f * Pk("arms")));
            var f = d.parts["feet"]; if (!f.IsNone) Add("腳", "feet", 4, Sc(f.P, 1.12f * Pk("feet")));
            var bl = d.parts["belly"]; if (!bl.IsNone) Add("肚子", "belly", 1, Sc(bl.P, 1.06f * d.belly.scale));
            return U;
        }

        /// <summary>衣服的布片：依衣服的尺寸，用人台量出來的版型。</summary>
        public static List<Unit> GarmentUnits(Session ss)
        {
            var g = ss.g; var P = Garments.PolyOn(g, ss.meta.CraftModel(g)); var ce = SM.Centroid(P);
            var R = CutGame.Resample(P, 160).Select(p => p - ce).ToList(); var b = SM.Bbox(R);
            return new List<Unit> { new Unit { name = g.T.piece, slot = "garment", mult = g.T.mult, P = R, off = Vector2.zero, size = Mathf.Max(b.width, b.height) } };
        }

        public static bool GrainOk(MatDef mat, int r) => mat.grain == "none" || (mat.grain == "two" && r % 2 == 0) || (mat.grain == "one" && r == 0);

        public List<Vector2> UnitPoly(Unit u)
        {
            float a = u.rot * Mathf.PI / 2;
            return u.P.Select(p => new Vector2(u.x, u.y) + SM.RotV(p.x * LS, p.y * LS, a)).ToList();
        }

        static bool Overlap(List<Vector2> P, List<Vector2> Q)
        {
            Rect A = SM.Bbox(P), B = SM.Bbox(Q);
            float x0 = Mathf.Max(A.xMin, B.xMin), x1 = Mathf.Min(A.xMax, B.xMax), y0 = Mathf.Max(A.yMin, B.yMin), y1 = Mathf.Min(A.yMax, B.yMax);
            if (x0 >= x1 || y0 >= y1) return false;
            for (float y = y0; y < y1; y += 3) for (float x = x0; x < x1; x += 3) { var q = new Vector2(x, y); if (SM.Pip(P, q) && SM.Pip(Q, q)) return true; }
            return false;
        }

        public void Check()
        {
            polys = units.Select(UnitPoly).ToList();
            uInfo = units.Select((u, i) =>
            {
                var P = polys[i];
                return new UInfo
                {
                    inside = P.All(q => q.x >= FAB.xMin && q.x <= FAB.xMax && q.y >= FAB.yMin && q.y <= FAB.yMax),
                    flaws = bolts[u.bolt].flaws.Count(fl => SM.Pip(P, fl)),
                    grain = GrainOk(bolts[u.bolt].mat, u.rot),
                };
            }).ToList();
            bInfo = bolts.Select(b =>
            {
                for (int i = 0; i < b.units.Count; i++)
                    for (int j = i + 1; j < b.units.Count; j++)
                    {
                        int a = b.units[i], c = b.units[j];
                        if (Overlap(polys[a], polys[c])) uInfo[a].ov = uInfo[c].ov = true;
                    }
                bool allIn = b.units.All(i => uInfo[i].inside), ov = b.units.Any(i => uInfo[i].ov);
                int len = allIn ? Mathf.CeilToInt((b.units.SelectMany(i => polys[i]).Max(q => q.x) - FAB.xMin + 4) / 10f) * 10 : 0;
                return new BInfo { allIn = allIn, ov = ov, valid = allIn && !ov, len = len, cost = (int)(len / 10 * b.mat.p10) };
            }).ToList();
            Valid = bInfo.All(b => b.valid);
            Cost = bInfo.Sum(b => b.cost);
        }

        int UnitAt(Vector2 p)
        {
            var ids = bolts[cur].units;
            for (int k = ids.Count - 1; k >= 0; k--) if (SM.Pip(polys[ids[k]], p)) return ids[k];
            return -1;
        }

        public void Rotate()
        {
            if (sel < 0) return;
            var u = units[sel]; u.rot = (u.rot + 1) % 4; Check();
        }

        /// <summary>確定排版：記下布料費、布紋不對的片數；主體壓到的污漬會留在娃娃身上，其他布片壓到的只扣分。</summary>
        public void Commit()
        {
            ss.fabricCost = Cost;
            ss.offGrain = uInfo.Count(x => !x.grain);
            D.stains.Clear(); ss.otherFlaws = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i]; var fl = bolts[u.bolt].flaws.Where(f => SM.Pip(polys[i], f)).ToList();
                if (u.slot == "body" && u.main != null)
                {
                    float a = -u.rot * Mathf.PI / 2;
                    foreach (var f in fl)
                    {
                        var v = SM.RotV((f.x - u.x) / LS, (f.y - u.y) / LS, a);
                        D.stains.Add(new Stain { mid = u.main, dx = (v.x + u.off.x) / 1.06f, dy = (v.y + u.off.y) / 1.06f });
                    }
                }
                else ss.otherFlaws += fl.Count;
            }
            D.Touch();
        }

        /// <summary>自動把布片一排排擺好（測試用；遊戲裡由玩家自己排）。</summary>
        public void AutoPack()
        {
            foreach (var b in bolts)
            {
                float x = FAB.xMin + 4, y = FAB.yMin + 4, rowH = 0;
                foreach (int i in b.units)
                {
                    var u = units[i]; u.rot = 0; var bb = SM.Bbox(UnitPolyAt(u, 0, 0));
                    if (y + bb.height > FAB.yMax - 4) { x += rowH + 6; y = FAB.yMin + 4; rowH = 0; }
                    u.x = x - bb.xMin; u.y = y - bb.yMin;
                    y += bb.height + 6; rowH = Mathf.Max(rowH, bb.width);
                }
            }
            Check();
        }

        List<Vector2> UnitPolyAt(Unit u, float x, float y) { var keep = (u.x, u.y); u.x = x; u.y = y; var P = UnitPoly(u); (u.x, u.y) = keep; return P; }

        // ---------- 操作 ----------
        public override string Hint => "排版：把布片排到布上，越省布越便宜";
        public override bool Done => Valid;
        public override bool ShowDoll => false;

        public override void Down(Vector2 p)
        {
            int i = UnitAt(p); sel = i;
            if (i >= 0 && ptr.right) { Rotate(); ptr.down = false; return; }
            if (i >= 0) { dragI = i; dragOff = p - new Vector2(units[i].x, units[i].y); }
        }

        public override void Move(Vector2 p, Vector2 prev)
        {
            if (dragI < 0) return;
            var u = units[dragI];
            u.x = Mathf.Clamp(p.x - dragOff.x, 0, SM.W); u.y = Mathf.Clamp(p.y - dragOff.y, 0, SM.H);
            Check();
        }

        public override void Up(Vector2 p) { if (dragI >= 0) { dragI = -1; Check(); } }

        public override void Tick(float dt)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) Rotate();
        }

        // ---------- 畫面 ----------
        public override void Draw(Sketch under, Sketch over)
        {
            var b = bolts[cur]; var bi = bInfo[cur];
            var g = under.P;
            for (int x = 0; x <= FAB.width; x += 10) g.Seg(new Vector2(FAB.x + x, FAB.y - 2), new Vector2(FAB.x + x, FAB.y - (x % 50 != 0 ? 5 : 10)), 1, Hex("#bba895"));
            g.RoundRect(FAB.x + 3, FAB.y + 5, FAB.width, FAB.height, 6, new Color(0, 0, 0, .12f));
            under.Tex(b.mat, b.color).Rect(FAB.x, FAB.y, FAB.width, FAB.height, Color.white);
            g = under.P;
            var edge = b.color.color * .7f; edge.a = 1;
            g.Seg(new Vector2(FAB.xMin, FAB.yMin), new Vector2(FAB.xMax, FAB.yMin), 3, edge);
            g.Seg(new Vector2(FAB.xMin, FAB.yMax), new Vector2(FAB.xMax, FAB.yMax), 3, edge);
            var bc = b.color.color; bool light = .299f * bc.r + .587f * bc.g + .114f * bc.b > .5f;
            var ac = light ? new Color(.24f, .16f, .12f, .3f) : new Color(1, 1, 1, .4f);
            if (b.mat.grain != "none")
                for (float x = FAB.x + 45; x < FAB.xMax; x += 90) foreach (float y in new[] { FAB.y + 30, FAB.yMax - 30 }) g.Arrow(x, y, 0, 24, ac, 1.5f);
            foreach (var f in b.flaws) g.Stain(f.x, f.y);
            if (bi.valid)
            {
                float x = FAB.x + bi.len;
                g.Line(new[] { new Vector2(x, FAB.y - 4), new Vector2(x, FAB.yMax + 4) }, 2, Accent, false, 6, 5);
                g.Rect(x, FAB.y, FAB.xMax - x, FAB.height, new Color(.992f, .973f, .945f, .55f));
            }
            foreach (int i in b.units)
            {
                var u = units[i]; var P = polys[i]; var x = uInfo[i];
                under.Piece(P, b.mat, b.color, 2);
                g = under.P;
                if (x.ov) g.Fill(P, new Color(.82f, .33f, .29f, .28f));
                if (i == sel) g.Line(P, 2, Accent, true, 5, 4);
                bool big = u.size * LS > 60;
                if (b.mat.grain != "none") g.Arrow(u.x, u.y, u.rot * Mathf.PI / 2, big ? 30 : 16, x.grain ? Hex("#2f7d52") : Bad, 2.2f);
            }
        }

        public override void CanvasGUI(GameScreen ui)
        {
            var b = bolts[cur]; var bi = bInfo[cur];
            for (int x = 50; x <= FAB.width; x += 50) ui.CanvasLabel(new Vector2(FAB.x + x, FAB.y - 11), x + "cm", 11, Hex("#8a7565"), TextAnchor.LowerCenter);
            if (bi.valid) ui.CanvasLabel(new Vector2(FAB.x + bi.len + 6, FAB.yMax + 6), $"✂ {bi.len}cm・${bi.cost}", 14, Hex("#c9603f"), TextAnchor.UpperLeft, true);
            ui.CanvasLabel(new Vector2(40, FAB.yMax + 22), "布片（拖到上面的布上）", 14, Hex("#9a8778"), TextAnchor.UpperLeft);
            foreach (int i in b.units)
            {
                var u = units[i]; var bb = SM.Bbox(polys[i]);
                ui.CanvasLabel(new Vector2(u.x, bb.yMax + 3), $"{u.name}×{u.mult}", 12, new Color(.24f, .16f, .12f, .85f), TextAnchor.UpperCenter, true, true);
            }
        }

        public override void Panel(GameScreen ui)
        {
            ui.H2("排版");
            for (int i = 0; i < bolts.Count; i++)
            {
                var b = bolts[i];
                if (ui.Chip($"布捲 {i + 1}：{b.mat.name}・{b.color.name} {(bInfo[i].valid ? "✔" : "")}\n{string.Join("、", b.units.Select(u => units[u].name))}", i == cur, b.color.color))
                { cur = i; sel = -1; }
            }
            if (ui.Btn("↻ 旋轉選取的布片（R 或右鍵）", sel >= 0)) Rotate();
            var bi = bInfo[cur];
            if (!bi.allIn) ui.Small("還有布片沒放到布上（或超出布邊）。");
            else if (bi.ov) ui.Small("有布片重疊了（紅色）。");
            if (uInfo.Any(x => !x.grain) && bolts.Any(b => b.mat.grain != "none")) ui.Small("紅色箭頭的布片布紋方向不對，做出來會怪怪的。");
            if (ss.order.free && Cost > ss.meta.coins)
            {
                ui.MainBtn($"錢不夠（需要 ${Cost}，你有 ${ss.meta.coins}）", false);
                ui.Small("先接幾張訂單賺錢，或換便宜的布、排緊一點。");
            }
            else if (ui.MainBtn($"確定排版，買布 ${Cost} →", Valid)) ui.Advance();
        }
    }
}
