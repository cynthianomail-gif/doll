using System.Collections.Generic;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 逐格重畫的 2D 圖形累積器（座標用畫布像素，800×560、y 往下）：線、虛線、圓、矩形、填色多邊形。
    /// 小遊戲的畫面（布、虛線、珠針、棉花袋……）都用它畫，相當於網頁版的 canvas 繪圖。
    /// </summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<Color> c = new List<Color>();
        public readonly List<int> t = new List<int>();

        public void Clear() { v.Clear(); uv.Clear(); c.Clear(); t.Clear(); }

        int Vert(Vector2 p, Color col) { v.Add(DollRenderer.W(p)); uv.Add(new Vector2(p.x / 48f, -p.y / 48f)); c.Add(FabricTextures.Lin(col)); return v.Count - 1; }

        void Tri(int a, int b, int d) { t.Add(a); t.Add(b); t.Add(d); t.Add(a); t.Add(d); t.Add(b); } // 兩面都畫

        public void Quad(Vector2 a, Vector2 b, Vector2 d, Vector2 e, Color col)
        {
            int i = Vert(a, col), j = Vert(b, col), k = Vert(d, col), l = Vert(e, col);
            Tri(i, j, k); Tri(i, k, l);
        }

        public void Seg(Vector2 a, Vector2 b, float w, Color col)
        {
            var d = b - a; float L = d.magnitude; if (L < 1e-4f) return;
            var n = new Vector2(-d.y, d.x) / L * (w / 2);
            Quad(a + n, b + n, b - n, a - n, col);
        }

        /// <summary>折線。dash &gt; 0 時畫虛線（實線長 dash、空白長 gap）。</summary>
        public void Line(IList<Vector2> P, float w, Color col, bool closed = false, float dash = 0, float gap = 0)
        {
            int n = P.Count; if (n < 2) return;
            int segs = closed ? n : n - 1;
            if (dash <= 0)
            {
                for (int i = 0; i < segs; i++) Seg(P[i], P[(i + 1) % n], w, col);
                if (w >= 3) for (int i = 0; i < n; i++) Circle(P[i], w / 2, col, 8);
                return;
            }
            float pos = 0; bool on = true; float left = dash;
            for (int i = 0; i < segs; i++)
            {
                Vector2 a = P[i], b = P[(i + 1) % n]; float L = Vector2.Distance(a, b), s = 0;
                while (s < L)
                {
                    float step = Mathf.Min(left, L - s);
                    if (on) Seg(Vector2.Lerp(a, b, s / L), Vector2.Lerp(a, b, (s + step) / L), w, col);
                    s += step; left -= step; pos += step;
                    if (left <= 1e-4f) { on = !on; left = on ? dash : gap; }
                }
            }
        }

        public void Fill(IList<Vector2> P, Color col)
        {
            if (P.Count < 3) return;
            int b0 = v.Count; foreach (var p in P) Vert(p, col);
            var tri = DollRenderer.Triangulate(P);
            for (int i = 0; i < tri.Count; i += 3) Tri(b0 + tri[i], b0 + tri[i + 1], b0 + tri[i + 2]);
        }

        /// <summary>從中心往外變暗的填色（星形的形狀用，像布片鼓起來的樣子）。</summary>
        public void Shaded(IList<Vector2> P, Vector2 center, Color inner, Color outer)
        {
            int n = P.Count; if (n < 3) return;
            int c0 = Vert(center, inner);
            int b0 = v.Count; foreach (var p in P) Vert(p, outer);
            for (int i = 0; i < n; i++) Tri(c0, b0 + i, b0 + (i + 1) % n);
        }

        public void Ellipse(Vector2 ce, float rx, float ry, Color col, int n = 24)
        {
            int c0 = Vert(ce, col), b0 = v.Count;
            for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2; Vert(ce + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry), col); }
            for (int i = 0; i < n; i++) Tri(c0, b0 + i, b0 + (i + 1) % n);
        }

        public void Circle(Vector2 ce, float r, Color col, int n = 24) => Ellipse(ce, r, r, col, n);

        /// <summary>放射狀漸層的圓（中心色 → 邊緣色）。</summary>
        public void Blob(Vector2 ce, float r, Color inner, Color outer, int n = 24)
        {
            int c0 = Vert(ce, inner), b0 = v.Count;
            for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2; Vert(ce + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, outer); }
            for (int i = 0; i < n; i++) Tri(c0, b0 + i, b0 + (i + 1) % n);
        }

        public void Arc(Vector2 ce, float r, float a0, float a1, float w, Color col, int n = 32)
        {
            var P = new List<Vector2>();
            int k = Mathf.Max(2, Mathf.CeilToInt(n * Mathf.Abs(a1 - a0) / (Mathf.PI * 2)));
            for (int i = 0; i <= k; i++) { float a = Mathf.Lerp(a0, a1, i / (float)k); P.Add(ce + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
            Line(P, w, col);
        }

        public void Ring(Vector2 ce, float r, float w, Color col, int n = 32) => Arc(ce, r, 0, Mathf.PI * 2, w, col, n);

        public void Rect(float x, float y, float w, float h, Color col) =>
            Quad(new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h), col);

        public static List<Vector2> RoundRectPoly(float x, float y, float w, float h, float r)
        {
            r = Mathf.Min(r, w / 2, h / 2); var P = new List<Vector2>();
            void Corner(float cx, float cy, float a0) { for (int i = 0; i <= 5; i++) { float a = a0 + i / 5f * Mathf.PI / 2; P.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r)); } }
            Corner(x + w - r, y + h - r, 0); Corner(x + r, y + h - r, Mathf.PI / 2); Corner(x + r, y + r, Mathf.PI); Corner(x + w - r, y + r, Mathf.PI * 1.5f);
            return P;
        }

        public void RoundRect(float x, float y, float w, float h, float r, Color col)
        {
            var P = RoundRectPoly(x, y, w, h, r);
            Shaded(P, new Vector2(x + w / 2, y + h / 2), col, col);
        }

        /// <summary>箭頭（和網頁版 drawArrow 一樣：角度 0 時朝下）。</summary>
        public void Arrow(float x, float y, float a, float len, Color col, float w = 2)
        {
            var d = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
            var p = new Vector2(x, y); var h = p + d * len / 2;
            Seg(p - d * len / 2, h - d * 3, w, col);
            var nn = new Vector2(d.y, -d.x);
            int i = Vert(h, col), j = Vert(h - d * 6 + nn * 4, col), k = Vert(h - d * 6 - nn * 4, col); Tri(i, j, k);
        }

        public void Stain(float x, float y, float s = 1)
        {
            Ellipse(new Vector2(x, y), 7 * s, 5 * s, new Color(.43f, .31f, .16f, .45f), 16);
            Ellipse(new Vector2(x + 4 * s, y + 3 * s), 5 * s, 4 * s, new Color(.43f, .31f, .16f, .3f), 16);
        }

        public void Upload(Mesh m)
        {
            m.Clear();
            m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(c); m.SetTriangles(t, 0, true);
        }
    }

    /// <summary>一層逐格重畫的圖。不同材質（純色、各種布）會分成依序疊上的批次。</summary>
    public class Sketch : MonoBehaviour
    {
        public int baseOrder;

        class Batch { public GameObject go; public MeshFilter mf; public MeshRenderer mr; public Mesh mesh; public readonly MeshBuilder mb = new MeshBuilder(); public Material mat; }
        readonly List<Batch> batches = new List<Batch>();
        int used;

        public static Sketch Create(string name, int order)
        {
            var s = new GameObject(name).AddComponent<Sketch>(); s.baseOrder = order; return s;
        }

        public void Begin() { used = 0; foreach (var b in batches) b.mb.Clear(); }

        public MeshBuilder With(Material mat)
        {
            if (used > 0 && batches[used - 1].mat == mat) return batches[used - 1].mb;
            if (used == batches.Count)
            {
                var go = new GameObject("batch"); go.transform.SetParent(transform, false);
                var b = new Batch { go = go, mf = go.AddComponent<MeshFilter>(), mr = go.AddComponent<MeshRenderer>(), mesh = new Mesh() };
                b.mesh.MarkDynamic(); b.mf.sharedMesh = b.mesh; batches.Add(b);
            }
            var bt = batches[used++]; bt.mat = mat; bt.mb.Clear(); return bt.mb;
        }

        /// <summary>純色圖層（顏色由頂點色決定）。</summary>
        public MeshBuilder P => With(FabricTextures.Plain);

        /// <summary>布料紋理圖層。</summary>
        public MeshBuilder Tex(MatDef m, ColorDef col) => With(FabricTextures.MaterialFor(m, col.color));

        public void End()
        {
            for (int i = 0; i < batches.Count; i++)
            {
                var b = batches[i]; bool on = i < used;
                if (b.go.activeSelf != on) b.go.SetActive(on);
                if (!on) continue;
                b.mb.Upload(b.mesh);
                b.mr.sharedMaterial = b.mat;
                b.mr.sortingOrder = baseOrder + i;
            }
        }

        public void Clear() { Begin(); End(); }

        /// <summary>畫一塊布片：布料紋理＋邊緣稍暗＋外框。</summary>
        public void Piece(IList<Vector2> P, MatDef mat, ColorDef col, float lineW = 2, float puff = .05f)
        {
            if (P == null || P.Count < 3) return;
            float k = 1f - (.06f + .34f * puff) * .3f;
            Tex(mat, col).Fill(P, new Color(k, k, k, 1)); // 布片不一定是星形（例如瀏海），用耳切三角化
            var lc = col.color * .62f; lc.a = 1;
            this.P.Line(P, lineW, lc, true);
        }
    }
}
