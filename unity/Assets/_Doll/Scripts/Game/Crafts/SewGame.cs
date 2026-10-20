using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DollWorkshop
{
    /// <summary>
    /// 車縫：每個主體一條縫線（開口處不車）。先放珠針固定，再沿虛線車：
    /// 轉角和夾著部件的地方要放慢（超過紅線會跳針），珠針到了要先拔，開頭和結尾要回針。（網頁版 makeSeam / processSew）
    /// </summary>
    public class SewGame : CraftGame
    {
        public const int GAP = 50;
        public int cur;
        public bool assist;
        public float backT;
        bool holdButton; public void HoldBack() => holdButton = true;
        public bool backHold;

        public List<Seam> List => D.seams;
        public Seam SW => D.seams[cur];

        public SewGame(Session ss) : base(ss)
        {
            D.seams.Clear();
            foreach (var m in Enumerable.Reverse(D.Mains())) D.seams.Add(MakeSeam(m));
            D.Touch();
        }

        /// <summary>從一圈有順序的點做縫線：開口（gapDeg 方向 ±halfDeg）不車，從開口後面開始車到開口前面。轉彎越急，車速上限越低。</summary>
        public static Seam SeamFromPts(string id, string name, List<Vector2> pts, float cx, float cy, float gapDeg, float halfDeg = 20)
        {
            float g = gapDeg * SM.D, H = halfDeg * SM.D; int n = pts.Count;
            var inc = pts.Select(p => Mathf.Abs(SM.AngDiff(Mathf.Atan2(p.y - cy, p.x - cx), g)) >= H).ToArray();
            int best = 0, bs = 0;
            for (int s = 0; s < n; s++)
                if (inc[s] && !inc[(s - 1 + n) % n]) { int l = 0; while (l < n && inc[(s + l) % n]) l++; if (l > best) { best = l; bs = s; } }
            if (best == 0) { best = n; bs = 0; }
            var sw = new Seam { mid = id, name = name, cx = cx, cy = cy };
            for (int k = 0; k < best; k++) { var p = pts[(bs + k) % n]; sw.path.Add(new SeamPoint { p = p, t = Mathf.Atan2(p.y - cy, p.x - cx) }); }
            var P = sw.path;
            for (int k = 0; k < P.Count; k++)
            {
                var a = P[Mathf.Max(0, k - 3)]; var b = P[k]; var c = P[Mathf.Min(P.Count - 1, k + 3)];
                if (a == b || b == c) { b.lim = .8f; continue; }
                float curv = Mathf.Abs(SM.AngDiff(Mathf.Atan2(c.p.y - b.p.y, c.p.x - b.p.x), Mathf.Atan2(b.p.y - a.p.y, b.p.x - a.p.x)));
                b.lim = Mathf.Clamp(.8f - curv * 1.6f, .25f, .8f);
            }
            return sw;
        }

        public Seam MakeSeam(MainPiece m)
        {
            var sw = SeamFromPts(m.id, m.name, SM.Outline(m.sh, .9f * m.r / SM.R, 320, m.cx, m.cy), m.cx, m.cy, m.gap);
            foreach (var k in D.ActiveParts().Where(k => D.HostOf(k) == m.id))
                foreach (int s in new[] { -1, 1 })
                {
                    float th = (-90 + s * D.AdjOf(k, s).a) * SM.D;
                    foreach (var p in sw.path) if (Mathf.Abs(SM.AngDiff(p.t, th)) < 10 * SM.D) { p.lim = Mathf.Min(p.lim, .4f); p.part = true; }
                }
            return sw;
        }

        /// <summary>沒有珠針固定的長段落（會滑動）。</summary>
        public List<(int a, int b)> PinGaps()
        {
            var sw = SW; int L = sw.path.Count;
            var ix = new List<int> { 0 }; ix.AddRange(sw.pins.Select(p => p.i).OrderBy(i => i)); ix.Add(L - 1);
            var g = new List<(int, int)>();
            for (int i = 0; i < ix.Count - 1; i++) if (ix[i + 1] - ix[i] > GAP) g.Add((ix[i], ix[i + 1]));
            return g;
        }

        bool PinnedNear(int i) { var sw = SW; return i < 8 || i > sw.path.Count - 8 || sw.pins.Any(p => Mathf.Abs(p.i - i) <= GAP / 2); }

        public void PlacePin(Vector2 p)
        {
            var sw = SW; int bi = -1; float bd = 1e9f;
            for (int i = 0; i < sw.path.Count; i++) { float d = Vector2.Distance(p, sw.path[i].p); if (d < bd) { bd = d; bi = i; } }
            if (bd > 16) return;
            int ex = sw.pins.FindIndex(x => Mathf.Abs(x.i - bi) < 6);
            if (ex >= 0) sw.pins.RemoveAt(ex); else sw.pins.Add(new Pin { i = bi });
        }

        public void PullPin()
        {
            var sw = SW;
            var p = sw.pins.Where(x => !x.pulled && x.i > sw.idx - 2 && x.i <= sw.idx + 35).OrderBy(x => x.i).FirstOrDefault();
            if (p != null) p.pulled = true;
        }

        public void StartSewing() => SW.sewing = true;

        public void Process(Vector2 q)
        {
            var sw = SW; float now = Now;
            if (sw.done || now < sw.jamUntil) return;
            int best = -1; float bd = 1e9f;
            for (int j = sw.idx; j < Mathf.Min(sw.path.Count, sw.idx + 14); j++) { float d = Vector2.Distance(q, sw.path[j].p); if (d < bd) { bd = d; best = j; } }
            if (bd > 18) return;
            float lim = sw.path[sw.idx].lim;
            if (ptr.speed > lim)
            {
                if (!assist)
                {
                    sw.jamUntil = now + 400; sw.jams++;
                    Flash(sw.path[sw.idx].part ? "這裡夾著部件，布比較厚，要放慢！" : "跳針了！轉角要放慢");
                    sw.stitches.Add(new Stitch { dx = q.x - sw.cx, dy = q.y - sw.cy, bad = true }); D.Touch();
                    return;
                }
                best = Mathf.Min(best, sw.idx + 1);
            }
            if (best > sw.idx)
            {
                var hit = sw.pins.FirstOrDefault(p => !p.pulled && p.i > sw.idx && p.i <= best);
                if (hit != null) { hit.pulled = true; sw.pinHits++; sw.jamUntil = now + 1200; Flash("撞到珠針，斷針了！要先按空白鍵拔掉"); return; }
                var dev = q - sw.path[best].p;
                for (int k = sw.idx + 1; k <= best; k++)
                {
                    sw.drift = PinnedNear(k) ? Mathf.Max(0, sw.drift - .3f) : Mathf.Min(9, sw.drift + .09f);
                    var p = sw.path[k].p; float nx = p.x - sw.cx, ny = p.y - sw.cy, nl = Mathf.Max(1e-3f, Mathf.Sqrt(nx * nx + ny * ny));
                    sw.stitches.Add(new Stitch { dx = nx + dev.x * .8f + nx / nl * sw.drift, dy = ny + dev.y * .8f + ny / nl * sw.drift, q = Mathf.Clamp01(1 - (bd + sw.drift) / 14) });
                }
                if (sw.drift > 3 && !Flashing) Flash("沒有珠針固定，布在滑動！");
                sw.idx = best; D.Touch();
            }
            if (sw.idx >= sw.path.Count - 1)
            {
                sw.done = true;
                var good = sw.stitches.Where(s => !s.bad).ToList();
                sw.acc = Mathf.Clamp01((good.Count > 0 ? good.Average(s => s.q) : 0) - sw.jams * .05f - sw.pinHits * .06f);
            }
        }

        /// <summary>拆線重來：珠針位置保留。</summary>
        public void Resew()
        {
            var sw = SW; var n = MakeSeam(D.GetMain(sw.mid));
            foreach (var p in sw.pins) n.pins.Add(new Pin { i = p.i });
            n.sewing = true; D.seams[cur] = n; D.Touch();
        }

        public float SewAcc => List.Count > 0 ? List.Average(s => s.acc) : 0;

        // ---------- 操作 ----------
        public override string Hint => !SW.sewing ? $"{SW.name}：先放珠針（點虛線放一根，再點一次拿掉）" : SW.done ? $"{SW.name}車好了！" : $"{SW.name}：從綠點車到紅點・空白鍵拔珠針・B 回針";
        public override bool Done => List.All(s => s.done);
        public override bool HideCursor => SW.sewing && !SW.done;

        public override void Down(Vector2 p)
        {
            if (!SW.sewing) { PlacePin(p); ptr.down = false; return; }
            Process(p);
        }

        public override void Drag(Vector2 q) { if (SW.sewing) Process(q); }

        public override void Tick(float dt)
        {
            var kb = Keyboard.current; var sw = SW;
            backHold = holdButton || (kb != null && kb.bKey.isPressed);
            holdButton = false;
            if (!sw.sewing) return;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) PullPin();
            if (backHold)
            {
                int L = sw.path.Count;
                string zone = sw.idx >= 2 && sw.idx < 16 ? "start" : sw.idx > L - 16 ? "end" : null;
                if (zone == null) { if (!Flashing) Flash(sw.idx < 2 ? "先車幾針，再回針" : "回針要在開頭或結尾做"); backT = 0; }
                else if ((zone == "start" && !sw.backStart) || (zone == "end" && !sw.backEnd))
                {
                    backT += dt;
                    if (backT >= .6f) { if (zone == "start") sw.backStart = true; else sw.backEnd = true; backT = 0; Flash("回針完成 ✔"); D.Touch(); }
                }
            }
            else backT = 0;
        }

        // ---------- 畫面 ----------
        public override void Draw(Sketch under, Sketch over)
        {
            var sw = SW; float now = Now; var g = over.P;
            var bc = D.FabOf("body").color.color; bool light = .299f * bc.r + .587f * bc.g + .114f * bc.b > .5f;
            var rest = sw.path.Skip(sw.idx).Select(p => p.p).ToList();
            g.Line(rest, 2, light ? new Color(.31f, .2f, .16f, .55f) : new Color(1, 1, 1, .7f), false, 4, 5);
            var run = new List<Vector2>();
            void Flush(Color c, float w) { if (run.Count > 1) g.Line(run, w, c); run.Clear(); }
            for (int k = sw.idx; k < sw.path.Count; k++) { if (sw.path[k].part) run.Add(sw.path[k].p); else Flush(new Color(.35f, .55f, .86f, .55f), 6); }
            Flush(new Color(.35f, .55f, .86f, .55f), 6);
            foreach (var (a, b) in PinGaps())
            {
                int s = Mathf.Max(a, sw.idx); if (s >= b) continue;
                for (int k = s; k <= b; k++) run.Add(sw.path[k].p);
                Flush(new Color(.94f, .59f, .24f, .75f), 5);
            }
            foreach (var s in sw.stitches.Where(x => x.bad))
            {
                var c = new Vector2(sw.cx + s.dx, sw.cy + s.dy);
                g.Seg(c + new Vector2(-5, -5), c + new Vector2(5, 5), 2, Bad); g.Seg(c + new Vector2(5, -5), c + new Vector2(-5, 5), 2, Bad);
            }
            foreach (var p in sw.pins)
            {
                if (p.pulled) continue;
                var q = sw.path[p.i].p; bool near = sw.sewing && p.i > sw.idx && p.i <= sw.idx + 35;
                var d = (q - new Vector2(sw.cx, sw.cy)).normalized;
                g.Seg(q - d * 14, q + d * 10, 2, Hex("#9aa0a8"));
                g.Circle(q + d * 12, near ? 7 : 5.5f, near ? (Mathf.FloorToInt(now / 150) % 2 == 1 ? Hex("#ff5a4a") : Hex("#ffd54a")) : Hex("#e8577a"), 14);
            }
            g.Circle(sw.path[0].p, 7, Good, 16); g.Circle(sw.path[sw.path.Count - 1].p, 7, Bad, 16);
            if (sw.sewing && !sw.done) g.Ring(sw.path[sw.idx].p, 11 + Mathf.Sin(now / 150) * 2, 2, new Color(.24f, .6f, .4f, .9f));
            if (sw.sewing)
            {
                float lim = sw.done ? .8f : sw.path[sw.idx].lim, mx = 1.2f, bx = 720, by = 90, bh = 300;
                g.RoundRect(bx - 14, by - 34, 64, bh + 64, 12, new Color(1, 1, 1, .9f));
                g.Rect(bx, by, 22, bh, Hex("#e6efe6"));
                g.Rect(bx, by, 22, bh * (1 - lim / mx), new Color(.82f, .33f, .29f, .25f));
                float sp = Mathf.Clamp(ptr.down ? ptr.speed : 0, 0, mx);
                g.Rect(bx, by + bh * (1 - sp / mx), 22, bh * sp / mx, sp > lim ? Bad : Good);
                g.Seg(new Vector2(bx - 4, by + bh * (1 - lim / mx)), new Vector2(bx + 26, by + bh * (1 - lim / mx)), 2, Bad);
                if (backHold && backT > 0) g.Arc(ptr.p, 22, -Mathf.PI / 2, -Mathf.PI / 2 + Mathf.PI * 2 * backT / .6f, 5, Accent);
            }
            if (ptr.inside && sw.sewing && !sw.done)
            {
                bool jam = now < sw.jamUntil; float bob = ptr.down && !jam ? Mathf.Abs(Mathf.Sin(now / 35)) * 6 : 0, jx = jam ? Mathf.Sin(now / 20) * 3 : 0;
                var o = ptr.p + new Vector2(jx, 0);
                g.RoundRect(o.x - 14, o.y - 6, 28, 10, 4, new Color(.47f, .49f, .53f, .85f));
                g.Seg(o + new Vector2(0, -34), o + new Vector2(0, -6 + bob), 2, Hex("#666666"));
                g.Rect(o.x - 6, o.y - 44, 12, 12, Hex("#555555"));
            }
            if (ptr.inside && !sw.sewing) { g.Seg(ptr.p + new Vector2(0, -14), ptr.p, 2, Hex("#9aa0a8")); g.Circle(ptr.p + new Vector2(0, -16), 5.5f, Hex("#e8577a"), 14); }
        }

        public override void CanvasGUI(GameScreen ui)
        {
            var sw = SW;
            if (sw.sewing)
            {
                ui.CanvasLabel(new Vector2(731, 74), "車速", 13, Ink, TextAnchor.MiddleCenter);
                if (assist) ui.CanvasLabel(new Vector2(731, 406), "輔助中", 13, Ink, TextAnchor.MiddleCenter);
                if (Flashing) ui.CanvasLabel(new Vector2(Mathf.Clamp(ptr.p.x, 180, 620), ptr.p.y - 56), flash, 18, Bad, TextAnchor.MiddleCenter, true, true);
            }
        }

        public override void Panel(GameScreen ui)
        {
            var sw = SW;
            ui.H2($"車縫：{sw.name}（{cur + 1}/{List.Count}）");
            if (!sw.sewing)
            {
                ui.Body("在虛線上點一下放一根珠針，再點一次拿掉。橘色段落代表還沒固定，車的時候布會滑動。");
                ui.Small($"已經放了 {sw.pins.Count} 根珠針。");
                if (ui.MainBtn("珠針放好了，開始車縫 →")) StartSewing();
                return;
            }
            if (sw.done)
            {
                ui.Small($"這條車得 {Mathf.RoundToInt(sw.acc * 100)}%（跳針 {sw.jams} 次、撞到珠針 {sw.pinHits} 次）");
                if (cur >= List.Count - 1) { if (ui.MainBtn("下一步：塞棉花 →")) ui.Advance(); }
                else if (ui.MainBtn($"下一條：車{List[cur + 1].name} →")) cur++;
            }
            ui.Small($"回針：開頭 {(sw.backStart ? "✔" : "—")}　結尾 {(sw.backEnd ? "✔" : "—")}");
            if (ui.Btn("📍 拔珠針（空白鍵）")) PullPin();
            if (ui.HoldBtn("回針（按住，或按住 B）")) HoldBack();
            if (ui.Btn($"車縫輔助：{(assist ? "開" : "關")}")) assist = !assist;
            if (ui.Btn("拆線重來")) Resew();
        }
    }
}
