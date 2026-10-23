using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 硬材質娃娃表面的上色層（整張畫布大小的貼圖）：焦黃、手繪筆刷、噴漆、搪膠光澤或亮光漆，
    /// 只留下娃娃外形裡面的部分。一直往同一筆加點時只補畫新的那段，其他改變才整張重畫。（網頁版 paintLayer）
    /// </summary>
    public class PaintLayer
    {
        const int W = Raster.W, H = Raster.H;
        public readonly Texture2D tex;
        public readonly Material mat;
        readonly Color32[] px = new Color32[W * H];
        bool[] mask; string maskKey;
        int builtVersion = -1, builtStrokes, builtLastPts; string builtBase;

        PaintLayer()
        {
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "PaintLayer" };
            mat = new Material(FabricTextures.SpriteShader) { mainTexture = tex, name = "PaintLayer" };
        }

        public static PaintLayer For(DollState d)
        {
            if (d.paintLayer == null) d.paintLayer = new PaintLayer();
            d.paintLayer.Refresh(d);
            return d.paintLayer;
        }

        static string MaskKey(DollState d)
        {
            var polys = d.SilhouettePolys(); float sx = 0, sy = 0; int n = 0;
            foreach (var P in polys) foreach (var p in P) { sx += p.x; sy += p.y; n++; }
            return $"{n}:{sx:F1}:{sy:F1}";
        }

        void Refresh(DollState d)
        {
            var p = d.paint;
            if (p.version == builtVersion && maskKey != null && d.version == lastDollVersion) return;
            lastDollVersion = d.version;
            string mk = MaskKey(d);
            bool full = false;
            if (mk != maskKey) { mask = Raster.Mask(d.SilhouettePolys()); maskKey = mk; full = true; }
            string baseKey = $"{mk}|{p.varnish}|{p.tint:F3}|{d.material}|{p.strokes.Count}";
            var last = p.strokes.Count > 0 ? p.strokes[p.strokes.Count - 1] : null;
            if (!full && baseKey == builtBase && last != null && last.pts.Count > builtLastPts && builtLastPts > 0)
            {
                // 同一筆往下畫：只補畫新的那幾段
                for (int i = builtLastPts; i < last.pts.Count; i++) Segment(last, last.pts[i - 1], last.pts[i]);
            }
            else if (full || baseKey != builtBase || last == null || last.pts.Count != builtLastPts) Rebuild(d);
            else { builtVersion = p.version; return; }
            builtBase = baseKey; builtVersion = p.version; builtStrokes = p.strokes.Count; builtLastPts = last?.pts.Count ?? 0;
            tex.SetPixels32(px); tex.Apply(false);
        }
        int lastDollVersion = -1;

        void Rebuild(DollState d)
        {
            System.Array.Clear(px, 0, px.Length);
            var p = d.paint;
            if (p.tint > 0) { var c = new Color(150 / 255f, 105 / 255f, 35 / 255f); float a = Mathf.Min(.5f, p.tint * .45f); for (int i = 0; i < px.Length; i++) if (mask[Flip(i)]) Blend(i, c, a); }
            foreach (var st in p.strokes)
            {
                if (st.spray) foreach (var q in st.pts) Spray(st, q);
                else if (st.pts.Count == 1) Disc(st.pts[0], st.size / 2, st.color);
                else for (int i = 1; i < st.pts.Count; i++) Segment(st, st.pts[i - 1], st.pts[i]);
            }
            if (d.material == "vinyl" || p.varnish)
                foreach (var m in d.Mains())
                {
                    float r = m.r * .55f; var h = new Vector2(m.cx - m.r * .35f, m.cy - m.r * .42f); float a0 = d.material == "vinyl" ? .45f : .32f;
                    ForCircle(h, r, (i, dist) => Blend(i, Color.white, a0 * (1 - dist / r)));
                }
        }

        /// <summary>畫布索引（y 往下）↔ 貼圖索引（y 往上）。</summary>
        static int Flip(int texIdx) { int x = texIdx % W, ty = texIdx / W; return (H - 1 - ty) * W + x; }

        void ForCircle(Vector2 c, float r, System.Action<int, float> fn)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(c.x + r));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(c.y + r));
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                float dx = x + .5f - c.x, dy = y + .5f - c.y, dd = Mathf.Sqrt(dx * dx + dy * dy);
                if (dd > r || !mask[y * W + x]) continue;
                fn((H - 1 - y) * W + x, dd);
            }
        }

        void Disc(Vector2 c, float r, Color col) => ForCircle(c, Mathf.Max(.8f, r), (i, _) => Blend(i, col, 1));

        void Segment(PaintStroke st, Vector2 a, Vector2 b)
        {
            if (st.spray) { Spray(st, b); return; }
            float L = Vector2.Distance(a, b); int n = Mathf.Max(1, Mathf.CeilToInt(L));
            for (int k = 0; k <= n; k++) Disc(Vector2.Lerp(a, b, k / (float)n), st.size / 2, st.color);
        }

        void Spray(PaintStroke st, Vector2 q) { float R = st.size * 1.6f; ForCircle(q, R, (i, dd) => Blend(i, st.color, .16f * (1 - dd / R))); }

        void Blend(int i, Color c, float a)
        {
            if (a <= 0) return;
            var d = px[i]; float da = d.a / 255f, oa = a + da * (1 - a); if (oa <= 0) return;
            float r = (c.r * a + d.r / 255f * da * (1 - a)) / oa, g = (c.g * a + d.g / 255f * da * (1 - a)) / oa, b = (c.b * a + d.b / 255f * da * (1 - a)) / oa;
            px[i] = new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), (byte)(oa * 255));
        }

        /// <summary>貼圖上有畫到的像素數（測試用）。</summary>
        public int PaintedPixels() => px.Count(c => c.a > 0);
    }
}
