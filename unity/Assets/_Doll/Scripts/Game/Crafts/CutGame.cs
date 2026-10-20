using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 裁布：按住沿著虛線剪，一塊一塊剪。離線越遠、剪歪越多，精準度越低。（網頁版 resetCut / processCut）
    /// </summary>
    public class CutGame : CraftGame
    {
        readonly LayoutGame L;
        public readonly List<int> order;
        public int which;
        public List<Vector2> pts;
        public bool[] cov;
        float devSum; int devN, off;
        readonly List<(Vector2 p, bool ok)?> trail = new List<(Vector2, bool)?>();
        public bool done;
        float doneAt, z;
        public readonly float?[] accs;
        public float acc;
        List<Vector2> flaws;

        public CutGame(Session ss, LayoutGame layout) : base(ss)
        {
            L = layout;
            order = Enumerable.Range(0, L.units.Count).ToList();
            accs = new float?[order.Count];
            ResetPiece();
        }

        public Unit Cur => L.units[order[which]];
        public bool Last => which >= order.Count - 1;

        public void ResetPiece()
        {
            var u = Cur; float a = u.rot * Mathf.PI / 2;
            z = Mathf.Clamp(330 / u.size, 1, 3);
            pts = Resample(u.P, 240).Select(p => new Vector2(SM.CX, SM.CY) + SM.RotV(p.x * z, p.y * z, a)).ToList();
            cov = new bool[pts.Count];
            devSum = 0; devN = 0; off = 0; trail.Clear(); done = false; doneAt = 0;
            flaws = L.bolts[u.bolt].flaws.Select(f => new Vector2(SM.CX + (f.x - u.x) / LayoutGame.LS * z, SM.CY + (f.y - u.y) / LayoutGame.LS * z))
                .Where(f => f.x > 130 && f.x < 670 && f.y > 80 && f.y < 510).ToList();
        }

        public void NextPiece() { which++; ResetPiece(); }

        /// <summary>沿周長等距重新取樣（讓每段虛線一樣長）。</summary>
        public static List<Vector2> Resample(IList<Vector2> P, int n)
        {
            var segs = new List<(Vector2 a, Vector2 b, float l)>(); float tot = 0;
            for (int i = 0; i < P.Count; i++) { var a = P[i]; var b = P[(i + 1) % P.Count]; float l = Vector2.Distance(a, b); segs.Add((a, b, l)); tot += l; }
            var o = new List<Vector2>(n); int si = 0; float acc = 0;
            for (int k = 0; k < n; k++)
            {
                float d = k / (float)n * tot;
                while (acc + segs[si].l < d && si < segs.Count - 1) { acc += segs[si].l; si++; }
                var s = segs[si]; float t = s.l > 0 ? (d - acc) / s.l : 0;
                o.Add(Vector2.Lerp(s.a, s.b, t));
            }
            return o;
        }

        public float Coverage => cov.Count(x => x) / (float)cov.Length;

        public void Process(Vector2 q)
        {
            if (done) return;
            float bd = 1e9f;
            for (int k = 0; k < pts.Count; k++) { float d = Vector2.Distance(q, pts[k]); if (d < 15) cov[k] = true; if (d < bd) bd = d; }
            if (bd < 15) { devSum += bd; devN++; } else if (bd < 60) off++;
            trail.Add((q, bd < 15));
            if (Coverage >= .97f)
            {
                done = true; doneAt = Now;
                accs[which] = Mathf.Clamp01(1 - devSum / Mathf.Max(1, devN) / 15f - Mathf.Min(.3f, off * .002f));
                acc = accs.Where(x => x != null).Average(x => x.Value);
                ss.cutAcc = acc;
            }
        }

        // ---------- 操作 ----------
        public override string Hint => $"裁布 {which + 1}/{order.Count}：{Cur.name} ×{Cur.mult}{(done ? " 剪好了！" : "")}";
        public override bool Done => done && Last;
        public override bool ShowDoll => false;
        public override bool CuttingMat => true;
        public override bool HideCursor => !done;

        public override void Down(Vector2 p) { trail.Add(null); Process(p); }
        public override void Drag(Vector2 q) => Process(q);

        // ---------- 畫面 ----------
        public override void Draw(Sketch under, Sketch over)
        {
            var u = Cur; var bf = L.bolts[u.bolt]; float now = Now;
            var g = under.P;
            g.Rect(-600, -400, 2000, 1400, Hex("#6fa98b"));
            for (int x = -600; x < 1400; x += 25) g.Seg(new Vector2(x, -400), new Vector2(x, 1000), 1, new Color(1, 1, 1, .18f));
            for (int y = -400; y < 1000; y += 25) g.Seg(new Vector2(-600, y), new Vector2(1400, y), 1, new Color(1, 1, 1, .18f));
            if (!done || now - doneAt < 600)
            {
                float a = done ? 1 - (now - doneAt) / 600 : 1;
                g.RoundRect(133, 86, 540, 430, 6, new Color(0, 0, 0, .18f * a));
                under.Tex(bf.mat, bf.color).Rect(130, 80, 540, 430, new Color(1, 1, 1, a));
                g = under.P;
                if (u.mult > 1) g.Rect(136, 510, 540, 6, new Color(0, 0, 0, .08f * a));
                foreach (var f in flaws) g.Stain(f.x, f.y, z);
            }
            if (done)
            {
                float t = Mathf.Clamp01((now - doneAt) / 400);
                var bb = SM.Bbox(pts);
                g.Ellipse(new Vector2(bb.center.x, bb.yMax + 6), bb.width * .4f, 13, new Color(.24f, .16f, .12f, .16f));
                for (int k = u.mult - 1; k >= 0; k--)
                {
                    var o = new Vector2(k * 8 * t, k * 8 * t);
                    under.Piece(pts.Select(p => p + o).ToList(), bf.mat, bf.color, 2.5f, .05f * t);
                }
                g = under.P;
                foreach (var f in flaws) if (SM.Pip(pts, f)) g.Stain(f.x, f.y, z);
            }
            else
            {
                var c = bf.color.color; bool light = .299f * c.r + .587f * c.g + .114f * c.b > .5f;
                var dash = light ? new Color(c.r * .4f, c.g * .4f, c.b * .4f, 1) : Color.Lerp(c, Color.white, .7f);
                g.Line(pts, 2.5f, dash, true, 9, 7);
                for (int k = 0; k < pts.Count; k++) { int k2 = (k + 1) % pts.Count; if (cov[k] && cov[k2]) g.Seg(pts[k], pts[k2], 4, new Color(1, 1, 1, .9f)); }
                var run = new List<Vector2>();
                void Flush() { if (run.Count > 1) g.Line(run, 1.5f, new Color(.82f, .33f, .29f, .8f)); run.Clear(); }
                foreach (var tr in trail) { if (tr == null || tr.Value.ok) { Flush(); continue; } run.Add(tr.Value.p); }
                Flush();
            }
        }

        public override void CanvasGUI(GameScreen ui)
        {
            if (ptr.inside && !done)
                ui.CanvasLabel(ptr.p, "✂", 30, Ink, TextAnchor.MiddleCenter, false, false, ptr.down ? Mathf.Sin(Now / 40) * 14 : 0);
        }

        public override void Panel(GameScreen ui)
        {
            var u = Cur;
            ui.H2($"裁布 {which + 1}/{order.Count}：{u.name} ×{u.mult}");
            ui.Small(done ? $"這一塊的精準度：{Mathf.RoundToInt((accs[which] ?? 0) * 100)}%" : $"已經剪了 {Mathf.RoundToInt(Coverage * 100)}%");
            if (done)
            {
                if (Last) { if (ui.MainBtn("下一步：車縫 →")) ui.Advance(); }
                else if (ui.MainBtn($"剪下一塊：{L.units[order[which + 1]].name} →")) NextPiece();
            }
            if (ui.Btn("重剪這一塊")) ResetPiece();
        }
    }
}
