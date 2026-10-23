using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>木頭鋸外形：沿著鉛筆線慢慢鋸，太快鋸子會卡住。（網頁版 initSaw / processSaw）</summary>
    public class SawGame : CraftGame
    {
        public readonly List<Vector2> pts;
        public readonly bool[] cov;
        float devSum; int devN;
        public int jams; float jamUntil;
        public bool done;
        readonly Rect box;
        class Dust { public Vector2 p, v; public float l; }
        readonly List<Dust> dust = new List<Dust>();

        public SawGame(Session ss) : base(ss)
        {
            pts = D.SilhouettePts(5); cov = new bool[pts.Count]; box = SM.Bbox(pts);
        }

        public float Coverage => cov.Count(x => x) / (float)cov.Length;

        public void Process(Vector2 q)
        {
            float now = Now;
            if (done || now < jamUntil) return;
            if (ptr.speed > .55f) { jamUntil = now + 500; jams++; Flash("鋸子卡住了！慢一點"); return; }
            float bd = 1e9f;
            for (int k = 0; k < pts.Count; k++) { float d = Vector2.Distance(q, pts[k]); if (d < 18) cov[k] = true; if (d < bd) bd = d; }
            if (bd < 18) { devSum += bd; devN++; if (Random.value < .5f) dust.Add(new Dust { p = q, v = new Vector2((Random.value - .5f) * 2, Random.value * 2), l = 1 }); }
            if (Coverage >= .95f) { done = true; ss.hardAccs["saw"] = Mathf.Clamp01(1 - devSum / Mathf.Max(1, devN) / 18 - jams * .03f); }
        }

        public override string Hint => done ? "外形鋸好了！" : "鋸外形：沿著鉛筆線慢慢鋸";
        public override bool Done => done;
        public override bool ShowDoll => done;
        public override bool HideCursor => !done;
        public override void Down(Vector2 p) => Process(p);
        public override void Drag(Vector2 q) => Process(q);

        public override void Tick(float dt)
        {
            float k = dt * 60;
            foreach (var d in dust) { d.p += d.v * k; d.v.y += .15f * k; d.l -= dt * 1.2f; }
            dust.RemoveAll(d => d.l <= 0);
        }

        public override void Draw(Sketch under, Sketch over)
        {
            var f = D.FabOf("body");
            if (!done)
            {
                under.P.RoundRect(box.xMin - 23, box.yMin - 20, box.width + 52, box.height + 52, 4, new Color(0, 0, 0, .18f));
                under.Tex(f.mat, f.color).Rect(box.xMin - 26, box.yMin - 26, box.width + 52, box.height + 52, Color.white);
                var g = under.P;
                for (int k = 0; k < pts.Count; k++) g.Rect(pts[k].x - 1.2f, pts[k].y - 1.2f, 2.4f, 2.4f, cov[k] ? new Color(1, 1, 1, .85f) : new Color(.24f, .16f, .12f, .55f));
            }
            var o = over.P;
            foreach (var d in dust) o.Rect(d.p.x, d.p.y, 2, 2, new Color(.91f, .83f, .68f, Mathf.Clamp01(d.l)));
            if (ptr.inside && !done)
            {
                // 鋸子：鋸片＋鋸齒＋木頭握把
                var c = ptr.p + new Vector2(ptr.down ? Mathf.Sin(Now / 45) * 4 : 0, 0);
                o.Rect(c.x - 16, c.y - 4, 26, 9, Hex("#c9cdd3"));
                for (int i = 0; i < 6; i++) o.Fill(new List<Vector2> { new Vector2(c.x - 16 + i * 4.3f, c.y + 5), new Vector2(c.x - 13.8f + i * 4.3f, c.y + 8.5f), new Vector2(c.x - 11.7f + i * 4.3f, c.y + 5) }, Hex("#9aa0a8"));
                o.RoundRect(c.x + 9, c.y - 7, 12, 15, 4, Hex("#a0683c"));
            }
        }

        public override void CanvasGUI(GameScreen ui)
        {
            if (Flashing) ui.CanvasLabel(new Vector2(Mathf.Clamp(ptr.p.x, 160, 640), ptr.p.y - 40), flash, 18, Bad, TextAnchor.MiddleCenter, true, true);
        }

        public override void Panel(GameScreen ui)
        {
            ui.H2("鋸外形");
            ui.Body("沿著鉛筆線鋸出娃娃的外形。太快鋸子會卡住。");
            ui.Small($"鋸了 {Mathf.RoundToInt(Coverage * 100)}%・卡住 {jams} 次");
            if (done && ui.MainBtn("下一步：雕刻 →")) ui.Advance();
        }
    }

    /// <summary>木頭雕刻：刮掉外圍多餘的木頭，鑿進娃娃本體會留下鑿痕。（網頁版 initCarve / processCarve）</summary>
    public class CarveGame : CraftGame
    {
        readonly HardGrid G;
        public readonly bool[] R;
        public readonly int total;
        public HardGrid Grid => G;
        public int removed;
        public readonly List<(Vector2 p, float a)> gouges = new List<(Vector2, float)>();
        Vector2? last;
        public bool done;
        readonly Texture2D tex; readonly Color32[] px; bool dirty;
        class Chip { public Vector2 p, v; public float r, l; }
        readonly List<Chip> chips = new List<Chip>();

        public CarveGame(Session ss) : base(ss)
        {
            G = new HardGrid(D, 26); R = new bool[G.T.Length]; const int rc = 5;
            for (int j = 0; j < G.ny; j++) for (int i = 0; i < G.nx; i++)
            {
                if (!G.T[j * G.nx + i]) continue;
                for (int dj = -rc; dj <= rc; dj++) for (int di = -rc; di <= rc; di++)
                {
                    if (di * di + dj * dj > rc * rc) continue;
                    int a = i + di, b = j + dj; if (a < 0 || b < 0 || a >= G.nx || b >= G.ny) continue;
                    int k = b * G.nx + a; if (!G.T[k]) R[k] = true;
                }
            }
            total = R.Count(x => x);
            // 多餘的木頭畫成一張貼圖（每格一個像素），刮掉就變透明
            var f = D.FabOf("body"); var wood = FabricTextures.Texture(f.mat.id, f.color.color);
            tex = new Texture2D(G.nx, G.ny, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            px = new Color32[G.nx * G.ny];
            for (int k = 0; k < R.Length; k++)
            {
                if (!R[k]) continue; int i = k % G.nx, j = k / G.nx;
                var c = wood.GetPixel((G.x0 + i * G.cs) % 48, (G.y0 + j * G.cs) % 48) * .88f; c.a = 1;
                px[(G.ny - 1 - j) * G.nx + i] = c;
            }
            dirty = true;
        }

        public void Process(Vector2 q)
        {
            if (done) return; int n = 0;
            G.CellsNear(q.x, q.y, 11, k => { if (R[k]) { R[k] = false; removed++; n++; int i = k % G.nx, j = k / G.nx; px[(G.ny - 1 - j) * G.nx + i] = new Color32(0, 0, 0, 0); } });
            if (n > 0) { dirty = true; if (Random.value < .6f) chips.Add(new Chip { p = q, v = new Vector2((Random.value - .5f) * 4, -Random.value * 3), l = 1, r = Random.value * Mathf.PI }); }
            if (G.DeepInside(q.x, q.y, 2) && (last == null || Vector2.Distance(q, last.Value) > 14)) { gouges.Add((q, Random.value * Mathf.PI)); last = q; }
            if (removed / (float)total >= .96f)
            {
                for (int k = 0; k < R.Length; k++) R[k] = false;
                System.Array.Clear(px, 0, px.Length); dirty = true;
                done = true; ss.hardAccs["carve"] = Mathf.Clamp01(1 - gouges.Count * .06f); ss.carveGouges = gouges.Count;
            }
        }

        public override string Hint => done ? "雕好了！" : "雕刻：刮掉外圍多餘的木頭";
        public override bool Done => done;
        public override bool HideCursor => !done;
        public override void Down(Vector2 p) => Process(p);
        public override void Drag(Vector2 q) => Process(q);

        public override void Tick(float dt)
        {
            float k = dt * 60;
            foreach (var c in chips) { c.p += c.v * k; c.v.y += .3f * k; c.r += .2f * k; c.l -= dt * 1.5f; }
            chips.RemoveAll(c => c.l <= 0);
        }

        public override void Draw(Sketch under, Sketch over)
        {
            if (dirty) { tex.SetPixels32(px); tex.Apply(false); dirty = false; }
            over.Image(tex, G.CanvasRect);
            var g = over.P; var wc = D.FabOf("body").color.color;
            foreach (var (p, a) in gouges) g.Arc(p, 5, a, a + 2, 3, new Color(.27f, .14f, .06f, .55f), 8);
            foreach (var c in chips)
            {
                var col = wc; col.a = Mathf.Clamp01(c.l);
                var q = new[] { new Vector2(-3, -1), new Vector2(3, -1), new Vector2(3, 1.5f), new Vector2(-3, 1.5f) }.Select(v => c.p + SM.RotV(v.x, v.y, c.r)).ToArray();
                g.Quad(q[0], q[1], q[2], q[3], col);
            }
            if (ptr.inside && !done)
            {
                List<Vector2> K(float x0, float y0, float x1, float y1) => new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) }.Select(v => ptr.p + SM.RotV(v.x, v.y, -.6f)).ToList();
                g.Fill(K(-2, -16, 2, 0), Hex("#a9adb3")); g.Fill(K(-3.5f, -38, 3.5f, -16), Hex("#8a5a3a"));
            }
        }

        public override void Panel(GameScreen ui)
        {
            ui.H2("雕刻");
            ui.Body("用雕刻刀刮掉娃娃外圍多餘的木頭。不要鑿進娃娃本體，會留下鑿痕。");
            ui.Small($"刮掉了 {Mathf.RoundToInt(removed * 100f / Mathf.Max(1, total))}%・鑿痕 {gouges.Count} 道");
            if (done && ui.MainBtn("下一步：砂磨 →")) ui.Advance();
        }
    }

    /// <summary>木頭砂磨：按住來回磨，粗砂紙磨得快但會留刮痕，最後要換細砂紙磨光。（網頁版 initSand / processSand）</summary>
    public class SandGame : CraftGame
    {
        readonly HardGrid G;
        public readonly float[] r;
        public float sum; public readonly int count;
        public HardGrid Grid => G;
        public bool coarse = true, done;
        readonly Texture2D tex; readonly Color32[] px; readonly int tw, th; bool dirty;

        public SandGame(Session ss) : base(ss)
        {
            G = new HardGrid(D, 16); r = new float[G.T.Length];
            for (int k = 0; k < r.Length; k++) if (G.T[k]) { r[k] = 1; count++; }
            sum = count;
            tw = G.nx * G.cs; th = G.ny * G.cs;
            tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            px = new Color32[tw * th];
            for (int k = 0; k < r.Length; k++) if (r[k] > 0) DrawCell(k, r[k]);
            dirty = true;
        }

        public float Rough => sum / Mathf.Max(1, count);

        void Set(int x, int y, Color32 c) { if (x < 0 || y < 0 || x >= tw || y >= th) return; px[(th - 1 - y) * tw + x] = c; }

        /// <summary>一格的木屑和毛刺：越粗糙越明顯。</summary>
        void DrawCell(int k, float v)
        {
            int i = k % G.nx, j = k / G.nx, x = i * G.cs, y = j * G.cs;
            for (int dy = 0; dy < G.cs; dy++) for (int dx = 0; dx < G.cs; dx++) Set(x + dx, y + dy, new Color32(0, 0, 0, 0));
            if (v < .04f) return;
            uint h = (uint)k * 2654435761u;
            var dark = new Color32(70, 40, 20, (byte)(v * .5f * 255));
            int ox = (int)(h % 3), oy = (int)((h >> 3) % 3);
            Set(x + ox, y + oy, dark); Set(x + ox + 1, y + oy, dark); Set(x + ox, y + oy + 1, dark);
            if (v > .4f) Set(x + (int)((h >> 6) % 3), y + (int)((h >> 9) % 3), new Color32(255, 240, 220, (byte)(v * .35f * 255)));
        }

        public void Process(Vector2 q, float d)
        {
            if (done || d <= 0) return;
            G.CellsNear(q.x, q.y, 14, k =>
            {
                float v = r[k]; if (v <= 0) return;
                float nv = coarse ? Mathf.Max(.35f, v - d * .02f) : Mathf.Max(0, v - d * (v > .36f ? .004f : .012f));
                if (nv < v) { r[k] = nv; sum -= v - nv; DrawCell(k, nv); dirty = true; }
            });
            if (Rough < .06f) { done = true; ss.hardAccs["sand"] = Mathf.Clamp01(1 - Rough); }
        }

        public override string Hint => done ? "磨得很光滑了！" : "砂磨：來回磨，先粗後細";
        public override bool Done => done;
        public override bool HideCursor => true;
        public override void Move(Vector2 p, Vector2 prev) => Process(p, Vector2.Distance(p, prev));

        public override void Draw(Sketch under, Sketch over)
        {
            if (dirty) { tex.SetPixels32(px); tex.Apply(false); dirty = false; }
            over.Image(tex, G.CanvasRect);
            if (ptr.inside)
            {
                var c = coarse ? new Color(.75f, .59f, .39f, .7f) : new Color(.92f, .84f, .71f, .75f);
                var P = MeshBuilder.RoundRectPoly(ptr.p.x - 14, ptr.p.y - 10, 28, 20, 4);
                over.P.Fill(P, c); over.P.Line(P, 1, new Color(.35f, .24f, .12f, .5f), true);
            }
        }

        public override void Panel(GameScreen ui)
        {
            ui.H2("砂磨");
            ui.Body("按住在娃娃上來回磨。粗砂紙磨得快，但磨到一個程度就磨不下去了；最後換細砂紙磨光。");
            if (ui.Chip("粗砂紙（磨得快，會留刮痕）", coarse)) coarse = true;
            if (ui.Chip("細砂紙（磨得慢，磨得光滑）", !coarse)) coarse = false;
            ui.Small($"光滑度 {Mathf.RoundToInt((1 - Rough) * 100)}%");
            if (done && ui.MainBtn("下一步：上色 →")) ui.Advance();
        }
    }
}
