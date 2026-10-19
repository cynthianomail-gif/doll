using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 把 DollState 畫成一組程序產生的網格（布料紋理＋頂點色陰影）和外框線。
    /// 設計資料有變（version 改變）或選取的分頁改變時才重建。
    /// </summary>
    public class DollRenderer : MonoBehaviour
    {
        public DollState state = new DollState();
        [Tooltip("目前選取的分頁，該部位會畫成虛線外框")] public string highlight;

        int builtVersion = -1;
        string builtHighlight;

        class Piece { public GameObject go; public MeshFilter mf; public MeshRenderer mr; public Mesh mesh; public LineRenderer line; }
        readonly List<Piece> pool = new List<Piece>();
        int used;

        static readonly Color Accent = new Color(.878f, .478f, .373f);

        // 畫布像素（800×560，y 往下）↔ 世界座標（1 單位 = 100 像素，畫布中心在原點）
        public static Vector3 W(Vector2 p) => new Vector3((p.x - SM.CX) / 100f, -(p.y - SM.H / 2) / 100f, 0);
        public static Vector2 Px(Vector3 w) => new Vector2(w.x * 100f + SM.CX, -w.y * 100f + SM.H / 2);

        void LateUpdate()
        {
            if (state.version != builtVersion || highlight != builtHighlight) Build();
        }

        public void Build()
        {
            builtVersion = state.version; builtHighlight = highlight;
            used = 0;
            if (state.DesignReady) DrawDoll();
            for (int i = used; i < pool.Count; i++) pool[i].go.SetActive(false);
        }

        void DrawDoll()
        {
            var s = state;
            var bf = s.FabOf("body");
            var hg = s.GetHair();
            var hf = s.FabOf("hair");
            if (hg != null)
            {
                if (hg.back != null) DrawPoly(hg.back, hf, 1, false, .35f, false);
                if (hg.tails != null) foreach (var t in hg.tails) DrawPoly(t, hf, 1, false, .35f, false);
            }
            var mains = s.Mains();
            for (int i = 0; i < mains.Count; i++)
            {
                var m = mains[i]; int baseOrder = 10 + i * 10;
                foreach (var k in s.ActiveParts("back").Where(k => s.HostOf(k) == m.id)) DrawPartPair(k, baseOrder);
                var outline = s.MainOutline(m);
                bool hl = (highlight == "body" && m.id != "head") || (highlight == "head" && m.id == "head");
                DrawPoly(outline, bf, baseOrder + 3, true, .55f, hl, new Vector2(m.cx, m.cy));
                DrawGloss(new Vector2(m.cx, m.cy), m.r, bf.mat, baseOrder + 4);
                if (m.id == "body" && !s.parts["belly"].IsNone)
                    DrawPoly(s.BellyPoly(), s.FabOf("belly"), baseOrder + 5, true, .3f, highlight == "belly");
            }
            if (hg != null) DrawPoly(hg.cap, hf, 40, false, .35f, highlight == "hair");
            foreach (var k in s.ActiveParts("front")) DrawPartPair(k, 50);
        }

        void DrawPartPair(string k, int order)
        {
            var s = state; var o = s.parts[k];
            foreach (int side in new[] { -1, 1 })
            {
                DrawPoly(s.PartPoly(k, side), s.FabOf(k), order, true, o.nostuff ? .5f : .55f, highlight == k);
                if (o.inner) DrawPoly(s.InnerPoly(k, side), s.FabOf("earInner"), order + 1, true, .55f, highlight == "earInner");
            }
        }

        Piece Next()
        {
            if (used < pool.Count) { var p = pool[used++]; p.go.SetActive(true); return p; }
            var go = new GameObject("piece");
            go.transform.SetParent(transform, false);
            var pc = new Piece { go = go, mf = go.AddComponent<MeshFilter>(), mr = go.AddComponent<MeshRenderer>(), mesh = new Mesh() };
            pc.mesh.MarkDynamic();
            pc.mf.sharedMesh = pc.mesh;
            var lineGo = new GameObject("outline");
            lineGo.transform.SetParent(go.transform, false);
            pc.line = lineGo.AddComponent<LineRenderer>();
            pc.line.loop = true; pc.line.useWorldSpace = false; pc.line.numCornerVertices = 2; pc.line.numCapVertices = 0;
            pc.line.sharedMaterial = FabricTextures.Plain;
            pool.Add(pc); used++;
            return pc;
        }

        /// <summary>
        /// 畫一塊布片。star = 從中心往外是「星形」（主體、部件），可以用放射狀網格做中間亮、邊緣暗的陰影；
        /// 其他形狀（瀏海、後髮）改用耳切三角化。
        /// </summary>
        void DrawPoly(List<Vector2> P, Fab f, int order, bool star, float puff, bool hl, Vector2? centerOverride = null)
        {
            if (P == null || P.Count < 3 || f.color == null) return;
            var pc = Next();
            var mesh = pc.mesh; mesh.Clear();
            var c = centerOverride ?? SM.Centroid(P);
            float edgeK = 1f - (.06f + .34f * puff) * .6f;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var cols = new List<Color>(); var tris = new List<int>();
            void Add(Vector2 p, float k) { verts.Add(W(p)); uvs.Add(new Vector2(p.x / 48f, -p.y / 48f)); cols.Add(new Color(k, k, k, 1)); }
            if (star)
            {
                int n = P.Count;
                Add(c, 1f);
                for (int i = 0; i < n; i++) Add(c + (P[i] - c) * .55f, Mathf.Lerp(1f, edgeK, .35f));
                for (int i = 0; i < n; i++) Add(P[i], edgeK);
                for (int i = 0; i < n; i++)
                {
                    int a = 1 + i, b = 1 + (i + 1) % n, a2 = 1 + n + i, b2 = 1 + n + (i + 1) % n;
                    tris.Add(0); tris.Add(b); tris.Add(a);
                    tris.Add(a); tris.Add(b); tris.Add(b2);
                    tris.Add(a); tris.Add(b2); tris.Add(a2);
                }
            }
            else
            {
                foreach (var p in P) Add(p, Mathf.Lerp(1f, edgeK, .5f));
                tris.AddRange(Triangulate(P));
            }
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0, true);
            // 不管頂點順序，兩面都要看得到：再加一份反向三角形
            var back = new List<int>(tris.Count);
            for (int i = 0; i < tris.Count; i += 3) { back.Add(tris[i]); back.Add(tris[i + 2]); back.Add(tris[i + 1]); }
            tris.AddRange(back); mesh.SetTriangles(tris, 0, true);
            pc.mr.sharedMaterial = FabricTextures.MaterialFor(f.mat, f.color.color);
            pc.mr.sortingOrder = order * 3;

            var line = pc.line;
            var lc = hl ? Accent : f.color.color * .62f; lc.a = 1;
            line.startColor = line.endColor = lc;
            line.widthMultiplier = hl ? .045f : .028f;
            line.positionCount = P.Count;
            line.SetPositions(P.Select(W).ToArray());
            line.sortingOrder = order * 3 + 1;
        }

        /// <summary>主體左上方的柔和光澤（白色、透明度往外遞減）。搪膠特別亮。</summary>
        void DrawGloss(Vector2 center, float r, MatDef mat, int order)
        {
            if (mat == null) return;
            float a = Mathf.Clamp01(.05f + .16f * .55f * mat.gloss);
            var pc = Next(); var mesh = pc.mesh; mesh.Clear();
            var hc = center + new Vector2(-.26f * r, -.32f * r);
            r *= .7f; // 光澤收在主體內側，不要超出輪廓
            int n = 32; var verts = new List<Vector3> { W(hc) }; var cols = new List<Color> { new Color(1, 1, 1, a) }; var uvs = new List<Vector2> { Vector2.zero }; var tris = new List<int>();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n * 2 * Mathf.PI;
                verts.Add(W(hc + new Vector2(Mathf.Cos(t) * r * .5f, Mathf.Sin(t) * r * .38f))); cols.Add(new Color(1, 1, 1, 0)); uvs.Add(Vector2.zero);
                tris.Add(0); tris.Add(1 + i); tris.Add(1 + (i + 1) % n);
                tris.Add(0); tris.Add(1 + (i + 1) % n); tris.Add(1 + i);
            }
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0, true);
            pc.mr.sharedMaterial = FabricTextures.Plain;
            pc.mr.sortingOrder = order * 3;
            pc.line.positionCount = 0;
        }

        /// <summary>簡單多邊形的耳切三角化（瀏海這類不是星形的形狀用）。</summary>
        static List<int> Triangulate(List<Vector2> P)
        {
            var res = new List<int>(); int n = P.Count; if (n < 3) return res;
            float area = 0; for (int i = 0, j = n - 1; i < n; j = i++) area += P[j].x * P[i].y - P[i].x * P[j].y;
            var idx = new List<int>(); for (int i = 0; i < n; i++) idx.Add(area > 0 ? i : n - 1 - i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = P[ia], b = P[ib], c = P[ic];
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 0) continue;
                    bool inside = false;
                    for (int k = 0; k < idx.Count && !inside; k++)
                    {
                        int ip = idx[k]; if (ip == ia || ip == ib || ip == ic) continue;
                        if (InTri(P[ip], a, b, c)) inside = true;
                    }
                    if (inside) continue;
                    res.Add(ia); res.Add(ib); res.Add(ic); idx.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break; // 形狀自我交錯時就停下，避免無限迴圈
            }
            if (idx.Count == 3) { res.Add(idx[0]); res.Add(idx[1]); res.Add(idx[2]); }
            return res;
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
    }
}
