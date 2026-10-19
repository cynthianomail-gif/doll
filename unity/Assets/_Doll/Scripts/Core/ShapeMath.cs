using System.Collections.Generic;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>形狀上的一個凸起（耳朵、手腳、幽靈下襬等），角度以「正右方 = 0、往下為正」計算，和網頁版一樣。</summary>
    public struct Bump
    {
        public float deg, amp, w;
        public bool sharp;
        public string name; // 有名字的凸起會變成可以分開塞棉花的部位（例如人形素體的手腳）

        public Bump(float deg, float amp, float w, bool sharp = false, string name = null)
        {
            this.deg = deg; this.amp = amp; this.w = w; this.sharp = sharp; this.name = name;
        }
    }

    /// <summary>極座標外形：超橢圓（ax, ay, p）加上一組凸起。</summary>
    public class ShapeDef
    {
        public float ax, ay, p;
        public readonly List<Bump> bumps = new List<Bump>();

        public ShapeDef(float ax, float ay, float p = 2f, params Bump[] b)
        {
            this.ax = ax; this.ay = ay; this.p = p;
            if (b != null) bumps.AddRange(b);
        }
    }

    /// <summary>
    /// 形狀數學。座標系沿用網頁版的「畫布像素」：800×560、y 往下為正，
    /// 繪製時再由 DollRenderer 轉成 Unity 世界座標。
    /// </summary>
    public static class SM
    {
        public const float D = Mathf.PI / 180f;
        public const float R = 108f, CX = 400f, CY = 320f, W = 800f, H = 560f;

        public static float AngDiff(float a, float b)
        {
            float d = a - b;
            while (d > Mathf.PI) d -= 2 * Mathf.PI;
            while (d < -Mathf.PI) d += 2 * Mathf.PI;
            return d;
        }

        public static float BumpProf(Bump b, float t)
        {
            float d = Mathf.Abs(AngDiff(t, b.deg * D));
            return b.sharp ? Mathf.Exp(-d / b.w) : Mathf.Exp(-(d / b.w) * (d / b.w));
        }

        public static float ShapeR(ShapeDef s, float t)
        {
            float p = s.p;
            float bs = 1f / Mathf.Pow(Mathf.Pow(Mathf.Abs(Mathf.Cos(t) / s.ax), p) + Mathf.Pow(Mathf.Abs(Mathf.Sin(t) / s.ay), p), 1f / p);
            float k = 1f;
            foreach (var b in s.bumps) k += b.amp * BumpProf(b, t);
            return bs * k;
        }

        /// <summary>沿著外形取 n 個點（像素座標）。r = R × scale × ShapeR × extraK。</summary>
        public static List<Vector2> Outline(ShapeDef s, float scale, int n, float cx, float cy, float extraK = 1f)
        {
            var L = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n * 2 * Mathf.PI;
                float r = R * scale * ShapeR(s, t) * extraK;
                L.Add(new Vector2(cx + Mathf.Cos(t) * r, cy + Mathf.Sin(t) * r));
            }
            return L;
        }

        public static List<Vector2> EllPoly(float cx, float cy, float rx, float ry, int n = 40)
        {
            var o = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n * 2 * Mathf.PI;
                o.Add(new Vector2(cx + Mathf.Cos(t) * rx, cy + Mathf.Sin(t) * ry));
            }
            return o;
        }

        public static void Qbez(Vector2 a, Vector2 c, Vector2 b, int n, List<Vector2> o)
        {
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                o.Add(u * u * a + 2 * u * t * c + t * t * b);
            }
        }

        public static List<Vector2> PointyPoly(float w, float h)
        {
            var o = new List<Vector2>();
            Qbez(new Vector2(-w, .05f), new Vector2(-w * .9f, -h * .55f), new Vector2(0, -h), 14, o);
            Qbez(new Vector2(0, -h), new Vector2(w * .9f, -h * .55f), new Vector2(w, .05f), 14, o);
            Qbez(new Vector2(w, .05f), new Vector2(0, .12f), new Vector2(-w, .05f), 8, o);
            return o;
        }

        public static List<Vector2> HornPoly(float w, float h)
        {
            var o = new List<Vector2>();
            Qbez(new Vector2(-w, .04f), new Vector2(-w * .7f, -h * .6f), new Vector2(w * .35f, -h), 12, o);
            Qbez(new Vector2(w * .35f, -h), new Vector2(w * .15f, -h * .45f), new Vector2(w, .04f), 12, o);
            Qbez(new Vector2(w, .04f), new Vector2(0, .1f), new Vector2(-w, .04f), 6, o);
            return o;
        }

        public static List<Vector2> WingPoly(float k)
        {
            var o = new List<Vector2>();
            Qbez(new Vector2(-.08f, .02f) * k, new Vector2(-.26f, -.36f) * k, new Vector2(.06f, -.44f) * k, 12, o);
            Qbez(new Vector2(.06f, -.44f) * k, new Vector2(.26f, -.32f) * k, new Vector2(.13f, -.2f) * k, 8, o);
            Qbez(new Vector2(.13f, -.2f) * k, new Vector2(.25f, -.1f) * k, new Vector2(.08f, -.01f) * k, 8, o);
            Qbez(new Vector2(.08f, -.01f) * k, new Vector2(0, .07f) * k, new Vector2(-.08f, .02f) * k, 6, o);
            return o;
        }

        public static List<Vector2> HeartPoly(float s, float dy)
        {
            var o = new List<Vector2>(60);
            for (int i = 0; i < 60; i++)
            {
                float t = i / 60f * 2 * Mathf.PI;
                float x = 16 * Mathf.Pow(Mathf.Sin(t), 3);
                float y = -(13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t));
                o.Add(new Vector2(x / 17 * s, y / 17 * s + dy));
            }
            return o;
        }

        public static Rect Bbox(IList<Vector2> P)
        {
            float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            foreach (var p in P) { x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        public static Vector2 Centroid(IList<Vector2> P) => Bbox(P).center;

        public static bool Pip(IList<Vector2> P, Vector2 q)
        {
            bool c = false;
            for (int i = 0, j = P.Count - 1; i < P.Count; j = i++)
            {
                Vector2 a = P[i], b = P[j];
                if ((a.y > q.y) != (b.y > q.y) && q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y) + a.x) c = !c;
            }
            return c;
        }

        public static Vector2 RotV(float x, float y, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(x * c - y * s, x * s + y * c);
        }
    }
}
