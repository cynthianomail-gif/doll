using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 塞棉花：在棉花袋按住拉出棉花（按越久越大團），拖到要塞的部位放開。
    /// 手腳耳朵只塞得進小團，而且要在主體塞滿前先塞；大團會凹凸不平，在娃娃上來回揉可以揉平。
    /// 縫線沒回針的話，塞太滿棉花會從線頭跑出來。最後點縫點把開口縫起來。（網頁版 regionsFor / dropClump）
    /// </summary>
    public class StuffGame : CraftGame
    {
        public static readonly Vector2 BAG = new Vector2(100, 470);
        public class Clump { public float size; public Vector2 p; }
        public Clump clump;
        public bool closing, closed; public int closeIdx;
        bool kneading;
        class Puff { public Vector2 p, v; public float r, l; }
        readonly List<Puff> puffs = new List<Puff>();

        public List<StuffRegion> Regs => D.regs;

        public StuffGame(Session ss) : base(ss)
        {
            D.regs = D.RegionsFor(); D.Touch();
        }

        public bool AllBackOk => D.seams.All(s => s.BackOk);
        public bool CanClose => Regs.Where(r => r.IsMainBody).All(r => r.fill >= .15f);

        public List<Vector2> ClosePoints() { var op = D.OpeningPos(); return Enumerable.Range(0, 5).Select(i => new Vector2(op.x - 24 + i * 12, op.y)).ToList(); }

        /// <summary>把一團棉花放到 p。回傳是否塞進去了。</summary>
        public bool DropClump(Vector2 p, float size)
        {
            int ri = D.RegionAt(p);
            if (ri < 0) { Flash("棉花掉到桌上了。"); return false; }
            var r = Regs[ri]; var host = D.MainReg(r.part != null ? r.host : r.main);
            if (r.small && size > 1.4f) { Flash($"太大團了，塞不進{r.name}！按短一點，拉小團的。"); return false; }
            if (r.early && host.fill >= .55f) { Flash($"{host.name}已經塞太滿，棉花推不到{r.name}了。末端要先塞！"); return false; }
            var f = r.part != null ? D.FabOf(r.part) : D.FabOf("body");
            float gain = size * .08f * f.mat.stretch * (r.small ? 1.7f : 1), lm = f.mat.id == "knit" && ss.offGrain > 0 ? 1.4f : 1;
            r.fill = Mathf.Min(1.35f, r.fill + gain);
            r.lump = Mathf.Min(1, r.lump + Mathf.Max(0, size - 1) * .14f * lm);
            D.squish = 1;
            for (int i = 0; i < 8; i++) puffs.Add(new Puff { p = p, v = new Vector2(Random.value - .5f, Random.value - .5f) * 3, r = 4 + Random.value * 4, l = 1 });
            D.Touch();
            return true;
        }

        public void Unstuff() { foreach (var b in Regs.Where(r => r.IsMainBody)) b.fill = Mathf.Max(0, b.fill - .06f); D.Touch(); }
        public void StartClosing() { if (!CanClose) return; closing = true; closeIdx = 0; }

        public void ClickClose(Vector2 p)
        {
            var cp = ClosePoints()[closeIdx];
            if (Vector2.Distance(p, cp) < 18) { closeIdx++; if (closeIdx >= 5) closed = true; }
            else Flash("點亮起來的那個縫點");
        }

        // ---------- 操作 ----------
        public override string Hint => closed ? "縫好了！" : closing ? "點縫點縫合" : "從棉花袋拉出棉花，拖到娃娃身上";
        public override bool Done => closed;

        public override void Down(Vector2 p)
        {
            if (closing && !closed) { ClickClose(p); return; }
            if (closed) return;
            if (Vector2.Distance(p, BAG) < 55) { clump = new Clump { size = .4f, p = p }; return; }
            if (D.RegionAt(p) >= 0) kneading = true;
        }

        public override void Move(Vector2 p, Vector2 prev)
        {
            if (clump != null) { clump.p = p; return; }
            if (!kneading) return;
            int ri = D.RegionAt(p); if (ri < 0) return;
            float d = Vector2.Distance(p, prev);
            var r = Regs[ri]; r.lump = Mathf.Max(0, r.lump - d * .0012f);
            D.squish = Mathf.Min(1, D.squish + d * .01f); D.Touch();
        }

        public override void Up(Vector2 p)
        {
            if (clump != null) { var c = clump; clump = null; DropClump(p, c.size); }
            kneading = false;
        }

        public override void Tick(float dt)
        {
            if (clump != null && ptr.down && Vector2.Distance(ptr.p, BAG) < 60) clump.size = Mathf.Min(3, clump.size + dt * 1.5f);
            if (!closed)
                foreach (var s in D.seams)
                {
                    if (s.BackOk) continue;
                    var r = D.MainReg(s.mid);
                    if (r != null && r.fill > .7f) { r.fill -= dt * .05f; ss.leaked = true; D.Touch(); }
                }
            if (D.squish > 0) { D.squish = Mathf.Max(0, D.squish - dt * 2.5f); D.squishTime = Now; D.Touch(); }
            foreach (var p in puffs) { p.p += p.v * dt * 60; p.l -= dt * 2.5f; }
            puffs.RemoveAll(p => p.l <= 0);
        }

        // ---------- 畫面 ----------
        static readonly Color Cotton = Hex("#fffdf8"), CottonEdge = Hex("#e2d8ca");

        void CottonBall(MeshBuilder g, Vector2 c, float r) { g.Circle(c, r + 1.2f, CottonEdge, 18); g.Circle(c, r, Cotton, 18); }

        public override void Draw(Sketch under, Sketch over)
        {
            float now = Now; var g = over.P; var op = D.OpeningPos();
            if (!closed)
            {
                var bc = D.FabOf("body").color.color;
                g.Ellipse(op, 30, 7, new Color(bc.r * .5f, bc.g * .5f, bc.b * .5f, 1));
                float f = Regs.Where(r => r.IsMainBody).Average(r => r.fill);
                if (f > .1f) { float s = Mathf.Min(1, f * 1.5f); foreach (var (dx, dy, r) in new[] { (-10f, 4f, 9f), (9f, 5f, 8f), (0f, 9f, 11f) }) CottonBall(g, op + new Vector2(dx, dy) * s, r * s); }
            }
            if (closing)
            {
                var cps = ClosePoints(); var tc = DollRenderer.ThreadColor(D.FabOf("body").color.color);
                for (int i = 0; i < closeIdx; i++) g.Seg(cps[i] + new Vector2(-4, -7), cps[i] + new Vector2(4, 7), 2.5f, tc);
                if (!closed) g.Ring(cps[closeIdx], 8 + Mathf.Sin(now / 120) * 2, 2.5f, Accent);
            }
            if (ss.leaked && !closed && !AllBackOk)
                foreach (var s in D.seams.Where(x => !x.BackOk))
                {
                    var m = D.GetMain(s.mid); var p0 = s.path[0].p; var p = D.ToDoll(m, p0.x - m.cx, p0.y - m.cy);
                    CottonBall(g, p + new Vector2(6, 4), 7 + Mathf.Sin(now / 200) * 1.5f);
                }
            g.Ellipse(BAG + new Vector2(0, 8), 54, 42, Hex("#d6c2a6")); g.Ellipse(BAG + new Vector2(0, 8), 52, 40, Hex("#efe2cf"));
            foreach (var (dx, dy, r) in new[] { (-22f, -8f, 16f), (0f, -16f, 19f), (22f, -8f, 16f), (-8f, 4f, 15f), (12f, 4f, 15f) }) CottonBall(g, BAG + new Vector2(dx, dy), r);
            if (clump != null)
            {
                float r = 7 + clump.size * 7;
                foreach (var (dx, dy, k) in new[] { (-.5f, 0f, .8f), (.5f, 0f, .8f), (0f, -.4f, 1f), (0f, .35f, .85f) }) CottonBall(g, clump.p + new Vector2(dx, dy) * r, r * k);
            }
            foreach (var p in puffs) { var c = Cotton; c.a = Mathf.Clamp01(p.l); g.Circle(p.p, p.r, c, 12); }
        }

        public static string ClumpLabel(float s) => s < 1 ? "小團" : s <= 1.4f ? "中小團" : s < 2.2f ? "大團" : "超大團";

        public override void CanvasGUI(GameScreen ui)
        {
            ui.CanvasLabel(BAG + new Vector2(0, 62), "棉花袋（按住拉）", 13, Ink, TextAnchor.MiddleCenter);
            if (clump != null) ui.CanvasLabel(clump.p - new Vector2(0, 7 + clump.size * 7 + 14), ClumpLabel(clump.size), 13, Hex("#8a5a3a"), TextAnchor.MiddleCenter, true);
            if (Flashing) ui.CanvasLabel(new Vector2(SM.W / 2, SM.H - 24), flash, 16, Bad, TextAnchor.MiddleCenter, true, true);
        }

        public override void Panel(GameScreen ui)
        {
            ui.H2("塞棉花");
            ui.Small("各部位手感");
            foreach (var r in Regs)
                ui.Bar(r.name, Mathf.Min(1, r.fill / 1.35f), r.fill > 1.05f ? Bad : r.fill < .3f ? Hex("#e0a35f") : Hex("#6aa88a"), Evaluator.SoftLabel(r.fill) + (r.lump > .3f ? "・凹凸" : ""));
            ui.Body($"整體手感：{Evaluator.SoftLabel(D.OverallSoft())}");
            if (closed) { if (ui.MainBtn("下一步：裝飾 →")) ui.Advance(); return; }
            if (closing) { ui.Small("依序點亮起來的縫點，把開口縫起來。"); return; }
            if (ui.Btn("掏出一點棉花")) Unstuff();
            if (ui.MainBtn(D.IsHuman ? "塞好了，縫合頭和身體" : "塞好了，縫合開口", CanClose)) StartClosing();
            if (!CanClose) ui.Small("主體還太扁，先多塞一點。");
        }
    }
}
