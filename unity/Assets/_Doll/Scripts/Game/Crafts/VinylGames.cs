using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 搪膠灌模：按住倒液態搪膠到虛線（太少會有缺洞、太多浪費）→ 烤箱計時看準時機出爐（太早黏黏的、太晚焦黃）
    /// → 按住倒出多餘的膠（倒越久外殼越薄越軟）。（網頁版 initCast / castNext / bakeStop / drainDone）
    /// </summary>
    public class CastGame : CraftGame
    {
        public string phase = "pour";
        public float amount, bake, tilt, thick = 1;
        readonly Rect box;
        readonly List<List<Vector2>> polys;
        class Drop { public Vector2 p; public float vy, l; }
        readonly List<Drop> drops = new List<Drop>();

        public CastGame(Session ss) : base(ss)
        {
            polys = D.SilhouettePolys();
            box = SM.Bbox(polys.SelectMany(p => p).ToList());
        }

        public override string Hint => phase == "pour" ? "灌模：按住畫面倒進液態搪膠" : phase == "bake" ? "烤箱加熱中：看準時機出爐" : phase == "drain" ? "倒膠：按住把多餘的膠倒出來" : "脫模完成！";
        public override bool Done => phase == "done";
        public override bool ShowDoll => phase == "done";

        public void PourDone()
        {
            if (phase != "pour" || amount < .2f) return;
            ss.hardAccs["pour"] = Mathf.Clamp01(1 - Mathf.Abs(amount - 1) * 3);
            ss.holes = amount < .9f ? Mathf.RoundToInt((.9f - amount) * 20) : 0;
            ss.waste = amount > 1.05f ? (amount - 1) * 30 : 0;
            phase = "bake"; bake = 0;
        }

        public void BakeStop()
        {
            if (phase != "bake") return;
            float x = bake;
            ss.hardAccs["bake"] = Mathf.Clamp01(1 - Mathf.Abs(x - .65f) / .35f);
            D.paint.tint = x > .78f ? Mathf.Min(1, (x - .78f) * 3) : 0; D.paint.Touch();
            ss.sticky = x < .5f;
            phase = "drain";
        }

        public void DrainDone()
        {
            if (phase != "drain") return;
            ss.SetHardFill(.4f + .65f * thick);
            phase = "done";
        }

        public override void Tick(float dt)
        {
            float k = dt * 60;
            if (phase == "pour" && ptr.down)
            {
                amount = Mathf.Min(1.3f, amount + dt * .3f);
                if (Random.value < .7f * Mathf.Min(1, k)) drops.Add(new Drop { p = new Vector2(box.center.x + (Random.value - .5f) * 6, box.yMin - 40), vy = 3, l = 1 });
            }
            if (phase == "bake") { bake += dt / 5; if (bake >= 1) { bake = 1; BakeStop(); } }
            if (phase == "drain")
            {
                if (ptr.down) { thick = Mathf.Max(0, thick - dt * .32f); tilt = Mathf.Min(1, tilt + dt * 3); }
                else tilt = Mathf.Max(0, tilt - dt * 3);
            }
            foreach (var d in drops) { d.p.y += d.vy * k; d.vy += .4f * k; d.l -= dt * 1.5f; }
            drops.RemoveAll(d => d.l <= 0 || d.p.y >= box.yMax);
        }

        public override void Draw(Sketch under, Sketch over)
        {
            if (phase == "done") return;
            var g = under.P; float now = Now;
            var pivot = new Vector2(SM.CX, box.center.y); float rot = phase == "drain" ? tilt * .5f : 0;
            List<Vector2> R(IEnumerable<Vector2> P) => P.Select(p => pivot + SM.RotV(p.x - pivot.x, p.y - pivot.y, rot)).ToList();
            var mold = R(MeshBuilder.RoundRectPoly(box.xMin - 40, box.yMin - 30, box.width + 80, box.height + 60, 16));
            g.Fill(mold, Hex("#8d939b")); g.Line(mold, 3, Hex("#6c7178"), true);
            foreach (var P in polys) g.Fill(R(P), Hex("#2a2b30"));
            var bc = D.FabOf("body").color.color;
            float lv = phase == "pour" ? Mathf.Min(1, amount) : 1, ly = box.yMax - lv * box.height;
            var liquid = bc; liquid.a = phase == "pour" ? .85f : .95f;
            if (lv > 0) foreach (var P in polys) { var C = Garments.ClipY(P, ly, false); if (C.Count >= 3) g.Fill(R(C), liquid); }
            if (phase == "pour")
            {
                g.Line(new[] { new Vector2(box.xMin - 30, box.yMin + 2), new Vector2(box.xMax + 30, box.yMin + 2) }, 2, Accent, false, 6, 5);
                var drop = bc; drop.a = .9f;
                foreach (var d in drops) g.Circle(d.p, 4, drop, 10);
                g.RoundRect(box.center.x - 16, box.yMin - 70, 32, 30, 6, Hex("#b0b4ba"));
            }
            if (phase == "bake")
            {
                float bx = 200, by = 500, bw = 400;
                g.RoundRect(bx, by, bw, 16, 8, Hex("#efe6da"));
                g.Rect(bx + bw * .55f, by, bw * .2f, 16, new Color(.24f, .6f, .4f, .5f));
                g.Rect(bx + bw * .78f, by, bw * .22f, 16, new Color(.82f, .33f, .29f, .35f));
                g.Rect(bx + bw * bake - 2, by - 6, 4, 28, Ink);
                g.Rect(-600, -400, 2000, 1400, new Color(1, .47f, .16f, .08f + .06f * Mathf.Sin(now / 120)));
            }
        }

        public override void CanvasGUI(GameScreen ui)
        {
            if (phase == "pour") ui.CanvasLabel(new Vector2(box.xMax + 12, box.yMin - 6), "滿", 12, Color.white, TextAnchor.LowerLeft);
            if (phase == "bake")
            {
                ui.CanvasLabel(new Vector2(300, 532), "生", 12, Ink, TextAnchor.UpperCenter);
                ui.CanvasLabel(new Vector2(460, 532), "剛好", 12, Ink, TextAnchor.UpperCenter);
                ui.CanvasLabel(new Vector2(556, 532), "焦", 12, Ink, TextAnchor.UpperCenter);
            }
        }

        public override void Panel(GameScreen ui)
        {
            ui.H2("灌模");
            switch (phase)
            {
                case "pour":
                    ui.Body("在畫面上按住，把液態搪膠倒進模具，倒到虛線就停。太少會有缺洞，太多會浪費。");
                    ui.Small($"已經倒了 {Mathf.RoundToInt(amount * 100)}%");
                    if (ui.MainBtn("灌好了，送進烤箱 →", amount >= .2f)) PourDone();
                    break;
                case "bake":
                    ui.Body("指針走到綠色區間就出爐！太早會黏黏的，太晚會焦黃。");
                    if (ui.MainBtn("出爐！")) BakeStop();
                    break;
                case "drain":
                    ui.Body("按住畫面把模具倒過來，倒出還沒凝固的膠。倒越久，外殼越薄、越軟。");
                    ui.Small($"外殼厚度：{(thick > .66f ? "厚（硬）" : thick > .33f ? "中等" : "薄（軟）")}");
                    if (ui.MainBtn("倒好了，脫模 →")) DrainDone();
                    break;
                default:
                    ui.Body($"脫模完成！手感：{Evaluator.SoftLabel(D.OverallSoft())}");
                    if (ui.MainBtn("下一步：修毛邊 →")) ui.Advance();
                    break;
            }
        }
    }

    /// <summary>搪膠修毛邊：沿著邊緣削掉凸出來的毛邊，刀子切進娃娃會留下刀痕。（網頁版 initTrim / processTrim）</summary>
    public class TrimGame : CraftGame
    {
        public class Flash { public Vector2 p, n; public float len; public bool on = true; }
        public readonly List<Flash> flashes;
        public readonly List<(Vector2 p, float a)> nicks = new List<(Vector2, float)>();
        Vector2? last;
        public bool done;
        readonly HardGrid grid;

        public TrimGame(Session ss) : base(ss)
        {
            grid = new HardGrid(D, 16);
            flashes = D.SilhouettePts(6).OrderBy(_ => Random.value).Take(14).Select(p =>
            {
                var m = D.NearestMain(p); var n = (p - new Vector2(m.cx, m.cy)).normalized;
                return new Flash { p = p, n = n, len = 7 + Random.value * 8 };
            }).ToList();
        }

        public void Process(Vector2 q)
        {
            if (done) return;
            foreach (var f in flashes) if (f.on && Vector2.Distance(q, f.p + f.n * f.len * .5f) < 12) f.on = false;
            if (grid.DeepInside(q.x, q.y, 2) && (last == null || Vector2.Distance(q, last.Value) > 14)) { nicks.Add((q, Random.value * Mathf.PI)); last = q; }
            if (flashes.All(f => !f.on))
            {
                done = true; ss.hardAccs["trim"] = Mathf.Clamp01(1 - nicks.Count * .08f); ss.trimNicks = nicks.Count;
            }
        }

        public override string Hint => done ? "毛邊修好了！" : "修毛邊：沿著邊緣削掉凸出來的毛邊";
        public override bool Done => done;
        public override bool HideCursor => !done;
        public override void Down(Vector2 p) => Process(p);
        public override void Drag(Vector2 q) => Process(q);

        public override void Draw(Sketch under, Sketch over)
        {
            var g = over.P; var c = D.FabOf("body").color.color; var dark = new Color(c.r * .65f, c.g * .65f, c.b * .65f, 1);
            foreach (var f in flashes)
            {
                if (!f.on) continue;
                var perp = new Vector2(-f.n.y, f.n.x);
                var T = new List<Vector2> { f.p + perp * 4, f.p + f.n * f.len, f.p - perp * 4 };
                g.Fill(T, c); g.Line(T, 1.2f, dark, true);
            }
            foreach (var (p, a) in nicks) { var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6; g.Seg(p - d, p + d, 1.8f, new Color(.35f, .16f, .12f, .6f)); }
            if (ptr.inside && !done)
            {
                List<Vector2> K(params Vector2[] v) => v.Select(x => ptr.p + SM.RotV(x.x, x.y, -.7f)).ToList();
                g.Fill(K(new Vector2(0, 0), new Vector2(6, -14), new Vector2(-2, -14)), Hex("#c9cdd3"));
                g.Fill(K(new Vector2(-3, -36), new Vector2(4, -36), new Vector2(4, -14), new Vector2(-3, -14)), Accent);
            }
        }

        public override void Panel(GameScreen ui)
        {
            ui.H2("修毛邊");
            ui.Body("沿著娃娃的邊緣削掉凸出來的毛邊。刀子不要切進娃娃，會留下刀痕。");
            ui.Small($"還有 {flashes.Count(f => f.on)} 處毛邊・刀痕 {nicks.Count} 道");
            if (done && ui.MainBtn("下一步：上色 →")) ui.Advance();
        }
    }
}
