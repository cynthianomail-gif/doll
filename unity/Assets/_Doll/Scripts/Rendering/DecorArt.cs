using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 配件與刺繡五官的圖案（和網頁版 ID 物件一一對應）。圖案用配件自己的座標畫（中心在 0,0），
    /// 再依配件的位置、縮放、角度放到娃娃身上。
    /// </summary>
    public static class DecorArt
    {
        class G
        {
            public MeshBuilder g; public Vector2 o; public float s, rot;
            public List<Vector2> Map(IEnumerable<Vector2> pts) => pts.Select(p => o + SM.RotV(p.x * s, p.y * s, rot)).ToList();
            public void Fill(IEnumerable<Vector2> pts, Color c) => g.Fill(Map(pts), c);
            public void Stroke(IEnumerable<Vector2> pts, float w, Color c, bool closed = false, float dash = 0, float gap = 0) => g.Line(Map(pts), w * s, c, closed, dash * s, gap * s);
            public void Seg(float x0, float y0, float x1, float y1, float w, Color c) => Stroke(new[] { new Vector2(x0, y0), new Vector2(x1, y1) }, w, c);
            public void FillStroke(List<Vector2> pts, Color fill, Color line, float w) { Fill(pts, fill); Stroke(pts, w, line, true); }
        }

        static Color H(string hex, float a = 1) { ColorUtility.TryParseHtmlString(hex, out var c); c.a = a; return c; }

        static List<Vector2> Ell(float x, float y, float rx, float ry, int n = 24)
        {
            var o = new List<Vector2>(n);
            for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2; o.Add(new Vector2(x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry)); }
            return o;
        }

        static List<Vector2> Circ(float x, float y, float r, int n = 20) => Ell(x, y, r, r, n);

        /// <summary>圓弧（和 canvas 的 arc 一樣：y 往下、角度從 a0 走到 a1）。</summary>
        static List<Vector2> Arc(float x, float y, float r, float a0, float a1, int n = 16)
        {
            var o = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++) { float a = Mathf.Lerp(a0, a1, i / (float)n); o.Add(new Vector2(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r)); }
            return o;
        }

        static void QuadTo(List<Vector2> o, Vector2 c, Vector2 b, int n = 10)
        {
            var a = o[o.Count - 1];
            for (int i = 1; i <= n; i++) { float t = i / (float)n, u = 1 - t; o.Add(u * u * a + 2 * u * t * c + t * t * b); }
        }

        static void CubicTo(List<Vector2> o, Vector2 c1, Vector2 c2, Vector2 b, int n = 12)
        {
            var a = o[o.Count - 1];
            for (int i = 1; i <= n; i++) { float t = i / (float)n, u = 1 - t; o.Add(u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b); }
        }

        static List<Vector2> StarPoly(float ro, float ri)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < 10; i++) { float a = i / 10f * Mathf.PI * 2 - Mathf.PI / 2, r = i % 2 == 1 ? ri : ro; o.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r)); }
            return o;
        }

        public static List<Vector2> HeartPoly(float k)
        {
            var o = new List<Vector2> { new Vector2(0, 12 * k) };
            CubicTo(o, new Vector2(-18 * k, 0), new Vector2(-12 * k, -14 * k), new Vector2(0, -5 * k));
            CubicTo(o, new Vector2(12 * k, -14 * k), new Vector2(18 * k, 0), new Vector2(0, 12 * k));
            o.RemoveAt(o.Count - 1);
            return o;
        }

        static List<Vector2> Move(List<Vector2> P, float dx, float dy, float a = 0) => P.Select(p => SM.RotV(p.x, p.y, a) + new Vector2(dx, dy)).ToList();

        public static void Draw(MeshBuilder mb, DecorItem it)
        {
            var g = new G { g = mb, o = it.Pos, s = it.s, rot = it.rot };
            var ink = H("#3a2a2a");
            switch (it.type)
            {
                case "eyes_button":
                    foreach (int s in new[] { -1, 1 })
                    {
                        g.FillStroke(Circ(s * 26, 0, 10), H("#5a3a28"), H("#3b2418"), 2);
                        foreach (var (a, b) in new[] { (-3, -3), (3, -3), (-3, 3), (3, 3) }) g.Fill(Circ(s * 26 + a, b, 1.6f, 10), H("#2a170e"));
                    }
                    break;
                case "eyes_stitch":
                    foreach (int s in new[] { -1, 1 }) g.Stroke(Arc(s * 26, 5, 9, Mathf.PI * 1.15f, Mathf.PI * 1.85f), 3.5f, ink);
                    break;
                case "eyes_dot":
                    foreach (int s in new[] { -1, 1 }) { g.Fill(Circ(s * 24, 0, 6), H("#222222")); g.Fill(Circ(s * 24 - 2, -2, 1.8f, 10), Color.white); }
                    break;
                case "eyes_sparkle":
                    foreach (int s in new[] { -1, 1 })
                    {
                        float x = s * 27;
                        g.Fill(Ell(x, 0, 11, 14), H("#2b2140")); g.Fill(Ell(x, 4, 7, 8), H("#7a63c9"));
                        g.Fill(Circ(x - 3, -6, 4, 14), Color.white); g.Fill(Circ(x + 4, 4, 2, 10), Color.white);
                    }
                    break;
                case "eyes_x":
                    foreach (int s in new[] { -1, 1 }) { g.Seg(s * 26 - 7, -7, s * 26 + 7, 7, 3.5f, H("#222222")); g.Seg(s * 26 + 7, -7, s * 26 - 7, 7, 3.5f, H("#222222")); }
                    break;
                case "nose":
                    g.FillStroke(new List<Vector2> { new Vector2(-7, -4), new Vector2(7, -4), new Vector2(0, 5) }, H("#4a2f2f"), H("#4a2f2f"), 3);
                    break;
                case "mouth_w":
                    g.Stroke(Arc(-6, 0, 6, 0, Mathf.PI), 3, ink); g.Stroke(Arc(6, 0, 6, 0, Mathf.PI), 3, ink);
                    break;
                case "mouth_smile":
                case "mouth_fang":
                    g.Stroke(Arc(0, -8, 13, .22f * Mathf.PI, .78f * Mathf.PI), 3, ink);
                    if (it.type == "mouth_fang")
                        foreach (float x in new[] { -6f, 6f })
                            g.FillStroke(new List<Vector2> { new Vector2(x - 3.5f, 3.5f), new Vector2(x + 3.5f, 3.5f), new Vector2(x, 10) }, Color.white, ink, 1.5f);
                    break;
                case "blush":
                    foreach (int s in new[] { -1, 1 }) g.Fill(Ell(s * 46, 0, 12, 7), new Color(1, .43f, .51f, .42f));
                    break;
                case "bow":
                    foreach (int s in new[] { -1, 1 })
                    {
                        var P = new List<Vector2> { Vector2.zero, new Vector2(s * 22, -13) };
                        QuadTo(P, new Vector2(s * 28, 0), new Vector2(s * 22, 13));
                        g.FillStroke(P, H("#e8577a"), H("#b23a5a"), 2);
                    }
                    g.FillStroke(Circ(0, 0, 5.5f, 14), H("#e8577a"), H("#b23a5a"), 2);
                    break;
                case "flower":
                    for (int i = 0; i < 5; i++) { float a = i / 5f * Mathf.PI * 2 - Mathf.PI / 2; g.FillStroke(Circ(Mathf.Cos(a) * 8, Mathf.Sin(a) * 8, 7, 16), H("#ffd3e0"), H("#e79bb0"), 1.5f); }
                    g.Fill(Circ(0, 0, 5, 14), H("#ffc94a"));
                    break;
                case "star":
                    g.FillStroke(StarPoly(17, 8), H("#ffd54a"), H("#d4a017"), 2);
                    g.Stroke(StarPoly(12.5f, 5.8f), 1.2f, H("#a8780f"), true, 3, 3);
                    break;
                case "heart":
                    g.FillStroke(HeartPoly(1), H("#ff7b9c"), H("#d5546f"), 2);
                    g.Stroke(HeartPoly(.7f), 1.2f, Color.white, true, 3, 3);
                    break;
                case "scar":
                    g.Seg(-22, 0, 22, 0, 2.5f, ink);
                    foreach (float x in new[] { -16f, -8f, 0f, 8f, 16f }) g.Seg(x - 2, -7, x + 2, 7, 2.5f, ink);
                    break;
                case "pin":
                    {
                        var P = new List<Vector2> { new Vector2(-14, 4), new Vector2(14, 4) };
                        P.AddRange(Arc(14, 0, 4, Mathf.PI / 2, -Mathf.PI / 2, 8).Skip(1));
                        P.Add(new Vector2(-12, -4));
                        g.Stroke(P, 2.5f, H("#8a8f98"));
                        g.FillStroke(MeshBuilder.RoundRectPoly(-22, -7, 9, 14, 3), H("#c9cdd3"), H("#8a8f98"), 1.5f);
                    }
                    break;
                case "scarf":
                    {
                        const float a = .12f;
                        g.Fill(Move(MeshBuilder.RoundRectPoly(0, 0, 20, 44, 4), 24, 4, a), H("#a8302a"));
                        var wl = new Color(1, 1, 1, .35f);
                        g.Stroke(Move(new List<Vector2> { new Vector2(10, 0), new Vector2(10, 44) }, 24, 4, a), 2, wl);
                        g.Stroke(Move(new List<Vector2> { new Vector2(0, 22), new Vector2(20, 22) }, 24, 4, a), 2, wl);
                        for (int x = 2; x < 20; x += 4) g.Stroke(Move(new List<Vector2> { new Vector2(x, 44), new Vector2(x, 51) }, 24, 4, a), 2, H("#a8302a"));
                        g.Fill(MeshBuilder.RoundRectPoly(-62, -11, 124, 22, 10), H("#c0392b"));
                        for (int x = -50; x < 62; x += 14) g.Seg(x, -11, x, 11, 2, wl);
                        g.Seg(-62, 0, 62, 0, 2, wl);
                        for (int x = -43; x < 62; x += 14) g.Seg(x, -11, x, 11, 1, new Color(0, 0, 0, .18f));
                    }
                    break;
            }
        }
    }
}
