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
        DollState builtState;
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
            if (state.version != builtVersion || highlight != builtHighlight || state != builtState) Build();
        }

        public void Build()
        {
            builtVersion = state.version; builtHighlight = highlight; builtState = state;
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
            bool fx = s.regs != null;
            if (hg != null)
            {
                if (hg.back != null) DrawPoly(hg.back, hf, 1, false, .35f, false);
                if (hg.tails != null) foreach (var t in hg.tails) DrawPoly(t, hf, 1, false, .35f, false);
            }
            bool clothed = s.IsHuman;
            if (clothed) DrawGarment(s.Worn("outer"), 5);
            var mains = s.Mains();
            for (int i = 0; i < mains.Count; i++)
            {
                var m = mains[i]; int baseOrder = 10 + i * 10;
                foreach (var k in s.ActiveParts("back").Where(k => s.HostOf(k) == m.id)) DrawPartPair(k, baseOrder, fx);
                var outline = s.MainOutline(m);
                bool hl = (highlight == "body" && m.id != "head") || (highlight == "head" && m.id == "head");
                float puff = Mathf.Min(1, s.MainFill(m.id));
                DrawPoly(outline, bf, baseOrder + 3, true, puff, hl, new Vector2(m.cx, m.cy));
                DrawGloss(new Vector2(m.cx, m.cy), m.r, bf.mat, baseOrder + 4, puff);
                if (m.id == "body" && !s.parts["belly"].IsNone)
                    DrawPoly(s.BellyPoly(), s.FabOf("belly"), baseOrder + 5, true, .3f, highlight == "belly");
                foreach (var st in s.stains.Where(x => x.mid == m.id)) { var p = s.ToDoll(m, st.dx, st.dy); mb.Stain(p.x, p.y, 1.6f * m.r / SM.R); }
                DrawStitches(m);
                if (fx) MainFX(m);
                Emit(baseOrder + 6);
                if (clothed && m.id == "torso" && !s.IsHard) { DrawGarment(s.Worn("bottom"), 17); DrawGarment(s.Worn("top"), 18); }
            }
            if (hg != null) DrawPoly(hg.cap, hf, 40, false, .35f, highlight == "hair");
            if (clothed && !s.IsHard) { DrawGarment(s.Worn("band"), 41); DrawGarment(s.Worn("hat"), 42); DrawGarment(s.Worn("clip"), 43); }
            foreach (var k in s.ActiveParts("front")) DrawPartPair(k, 50, fx);
            if (clothed && s.IsHard) { DrawGarment(s.Worn("bottom"), 51); DrawGarment(s.Worn("top"), 52); DrawGarment(s.Worn("band"), 53); DrawGarment(s.Worn("hat"), 54); DrawGarment(s.Worn("clip"), 55); }
            if (clothed) DrawGarment(s.Worn("bag"), 57);
            foreach (var it in s.decor) DecorArt.Draw(mb, it);
            Emit(60);
        }

        void DrawPartPair(string k, int order, bool fx)
        {
            var s = state; var o = s.parts[k];
            foreach (int side in new[] { -1, 1 })
            {
                float fill = s.PartFill(k, side), puff = Mathf.Min(1, fill);
                var P = s.PartPoly(k, side);
                DrawPoly(P, s.FabOf(k), order, true, puff, highlight == k);
                if (o.inner) DrawPoly(s.InnerPoly(k, side), s.FabOf("earInner"), order + 1, true, puff, highlight == "earInner");
                if (fx && !o.nostuff)
                {
                    var X = s.PartXform(k, side); var c = SM.Centroid(P);
                    var ax = SM.RotV(0, -1, X.rot); var nx = new Vector2(-ax.y, ax.x);
                    if (fill < .28f) foreach (int sg in new[] { -1, 1 }) mb.Seg(c + nx * sg * 5 - ax * 10, c + nx * sg * 3 + ax * 10, 1.5f, Flat);
                    if (fill > 1.05f) foreach (int sg in new[] { -1, 1 }) mb.Seg(c + nx * sg * 14, c + nx * sg * 22, 2, Strain);
                }
            }
            Emit(order + 2);
        }

        // ---------- 縫線、污漬、棉花的痕跡 ----------
        readonly MeshBuilder mb = new MeshBuilder();
        static readonly Color Flat = new Color(.27f, .16f, .12f, .3f), Strain = new Color(.82f, .33f, .29f, .7f);

        void Emit(int order, int sub = 0)
        {
            if (mb.v.Count == 0) return;
            var pc = Next(); mb.Upload(pc.mesh); mb.Clear();
            pc.mr.sharedMaterial = FabricTextures.Plain;
            pc.mr.sortingOrder = order * 3 + sub;
            pc.line.positionCount = 0;
        }

        /// <summary>穿在身上的一件衣服：布片、完成後的車縫線、斗篷領口、毛球、帽梗、髮夾結、衣服上的裝飾。（網頁版 drawGarment）</summary>
        void DrawGarment(Garment g, int order)
        {
            if (g == null || g.cls == null) return;
            var s = state;
            var f = g.fab.mat != null && g.fab.color != null ? g.fab : new Fab(DB.Mat("felt"), DB.Col("cream"));
            var P = Garments.PolyOn(g, s); if (P.Count < 3) return;
            var ce = SM.Centroid(P); var a = Garments.AnchorOn(g, s); var bb = SM.Bbox(P);
            var c = f.color.color; var dark = new Color(c.r * .6f, c.g * .6f, c.b * .6f, 1);
            if (g.type == "bag")
            {
                var t = s.GetMain("torso"); var strap = new List<Vector2> { new Vector2(t.cx - t.r * .3f, t.cy - t.r * .42f) };
                var p0 = strap[0]; var pc = new Vector2(ce.x, bb.yMin - t.r * .5f); var p1 = new Vector2(ce.x + bb.width * .3f, bb.yMin + 2);
                for (int i = 1; i <= 12; i++) { float u = i / 12f, w = 1 - u; strap.Add(w * w * p0 + 2 * w * u * pc + u * u * p1); }
                mb.Line(strap, 2, dark); Emit(order - 1, 2);
            }
            DrawPoly(P, f, order, false, .3f, false);
            if (g.done)
            {
                float lum = .299f * c.r + .587f * c.g + .114f * c.b;
                mb.Line(P.Select(p => ce + (p - ce) * .9f).ToList(), 1.4f, lum > .55f ? new Color(c.r * .5f, c.g * .5f, c.b * .5f, 1) : new Color(1, .953f, .878f, 1), true, 3, 4);
            }
            if (g.type == "cape")
            {
                var m = s.GetMain("torso"); var p0 = new Vector2(m.cx - m.r * .28f, m.cy - m.r * .44f); var pc = new Vector2(m.cx, m.cy - m.r * .25f); var p1 = new Vector2(m.cx + m.r * .28f, m.cy - m.r * .44f);
                var L = new List<Vector2>(); for (int i = 0; i <= 12; i++) { float u = i / 12f, w = 1 - u; L.Add(w * w * p0 + 2 * w * u * pc + u * u * p1); }
                mb.Line(L, 2.5f, dark);
            }
            var h = s.GetMain("head");
            if (g.type == "beanie" && g.p["pom"] > .5f)
            {
                var pp = new Vector2(bb.center.x, bb.yMin - 4); float r = h.r * .16f;
                mb.Circle(pp, r + 1.5f, new Color(c.r * .7f, c.g * .7f, c.b * .7f, 1), 20); mb.Circle(pp, r, Color.Lerp(c, Color.white, .35f), 20);
            }
            if (g.type == "beret") mb.RoundRect(bb.center.x - 3, bb.yMin - 7, 6, 9, 2, new Color(c.r * .7f, c.g * .7f, c.b * .7f, 1));
            if (g.type == "clip") mb.Circle(ce, h.r * .05f, new Color(c.r * .7f, c.g * .7f, c.b * .7f, 1), 14);
            foreach (var t in g.trims) DecorArt.Draw(mb, new DecorItem { type = t.type, x = a.cx + t.x, y = a.cy + t.y, s = t.s, rot = t.rot });
            Emit(order, 2);
        }

        public static Color ThreadColor(Color h)
        {
            float lum = .299f * h.r + .587f * h.g + .114f * h.b;
            return lum > .55f ? new Color(h.r * .45f, h.g * .45f, h.b * .45f, 1) : new Color(1f, .953f, .878f, 1);
        }

        void DrawStitches(MainPiece m)
        {
            var s = state; var sw = s.seams.FirstOrDefault(x => x.mid == m.id); if (sw == null) return;
            var st = sw.stitches.Where(x => !x.bad).Select(x => s.ToDoll(m, x.dx, x.dy)).ToList(); if (st.Count < 2) return;
            var col = ThreadColor(s.FabOf("body").color.color);
            for (int i = 0; i < st.Count - 1; i++) { if ((i / 3) % 2 == 1) continue; mb.Seg(st[i], st[i + 1], 2.2f, col); }
            if (sw.backStart) mb.Line(st.Take(Mathf.Min(8, st.Count)).ToList(), 3.2f, col);
            if (sw.backEnd) mb.Line(st.Skip(Mathf.Max(0, st.Count - 8)).ToList(), 3.2f, col);
        }

        void MainFX(MainPiece m)
        {
            var s = state; var b = s.MainReg(m.id); if (b == null) return; float r = m.r;
            var bumps = s.regs.Where(x => x.main == m.id && x.bump != null).ToList();
            Vector2 Edge(float t, float k) => s.ToDoll(m, Mathf.Cos(t) * r * SM.ShapeR(m.sh, t) * k, Mathf.Sin(t) * r * SM.ShapeR(m.sh, t) * k);
            if (b.fill < .28f)
                foreach (float dy in new[] { -.25f, .05f, .32f })
                {
                    var a = new Vector2(m.cx - .32f * r, m.cy + dy * r); var c = new Vector2(m.cx, m.cy + dy * r + 8); var e = new Vector2(m.cx + .32f * r, m.cy + dy * r);
                    var P = new List<Vector2>(); for (int i = 0; i <= 10; i++) { float u = i / 10f, w = 1 - u; P.Add(w * w * a + 2 * w * u * c + u * u * e); }
                    mb.Line(P, 1.6f, Flat);
                }
            if (b.lump > .12f)
                foreach (var (rr, t) in new[] { (.45f, -.6f), (.5f, .9f), (.35f, 2.2f), (.5f, 3.6f), (.4f, 5f) })
                {
                    var p = Edge(t, rr); float sz = (10 + b.lump * 10) * r / SM.R;
                    mb.Blob(p, sz, new Color(.24f, .12f, .08f, 0), new Color(.24f, .12f, .08f, .22f * b.lump), 16);
                    mb.Blob(p - new Vector2(.3f, .3f) * sz, sz * .55f, new Color(1, 1, 1, .25f * b.lump), new Color(1, 1, 1, 0), 16);
                }
            void StrainAt(float t) { var q = Edge(t, 1); var d = (q - new Vector2(m.cx, m.cy)).normalized; mb.Seg(q + d * 4, q + d * 12, 2, Strain); }
            if (b.fill > 1.05f) foreach (float t in new[] { 0f, 1.2f, 2.4f, 3.6f, 4.8f }) StrainAt(t);
            foreach (var g in bumps)
            {
                float t = g.bump.Value.deg * SM.D;
                if (g.fill > 1.05f) StrainAt(t);
                if (g.fill < .28f)
                {
                    var p = Edge(t, .78f); var ax = new Vector2(Mathf.Cos(t), Mathf.Sin(t)); var nx = new Vector2(-ax.y, ax.x);
                    foreach (int sg in new[] { -1, 1 }) mb.Seg(p + nx * sg * 5 - ax * 8, p + nx * sg * 3 + ax * 8, 1.5f, Flat);
                }
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
            void Add(Vector2 p, float k) { verts.Add(W(p)); uvs.Add(new Vector2(p.x / 48f, -p.y / 48f)); cols.Add(FabricTextures.Lin(new Color(k, k, k, 1))); }
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
            line.startColor = line.endColor = FabricTextures.Lin(lc);
            line.widthMultiplier = hl ? .045f : .028f;
            line.positionCount = P.Count;
            line.SetPositions(P.Select(W).ToArray());
            line.sortingOrder = order * 3 + 1;
        }

        /// <summary>主體左上方的柔和光澤（白色、透明度往外遞減）。搪膠特別亮。</summary>
        void DrawGloss(Vector2 center, float r, MatDef mat, int order, float puff = .55f)
        {
            if (mat == null) return;
            float a = Mathf.Clamp01(.05f + .16f * puff * mat.gloss);
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
        internal static List<int> Triangulate(IList<Vector2> P)
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
