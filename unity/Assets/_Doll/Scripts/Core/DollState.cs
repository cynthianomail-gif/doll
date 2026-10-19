using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    public class Adj { public float a, inset, tilt, scale = 1f; public Adj Clone() => (Adj)MemberwiseClone(); }

    public class AdjPair
    {
        public bool sym = true;
        public Adj left, right;
        public Adj Get(int side) => side < 0 ? left : right;
    }

    public class BellyAdj { public float dx, dy, scale = 1f; }

    public class Fab
    {
        public MatDef mat;
        public ColorDef color;
        public Fab(MatDef m, ColorDef c) { mat = m; color = c; }
    }

    /// <summary>要車縫、塞棉花的大塊主體：動物布偶只有「身體」，人形娃娃有「身體素體」和「頭」。</summary>
    public class MainPiece
    {
        public string id, name;
        public ShapeDef sh;
        public float cx, cy, r, gap;
    }

    public class HairGeo
    {
        public List<Vector2> cap, back;
        public List<List<Vector2>> tails;
        public MainPiece head;
    }

    /// <summary>
    /// 一隻娃娃的完整設計資料，以及從設計資料算出外形的方法。
    /// 和網頁原型的 S 物件（設計相關的部分）一一對應。
    /// </summary>
    public class DollState
    {
        public string kind = "animal";      // animal | human
        public string material = "cloth";   // cloth | vinyl | wood
        public ShapeChoice body = DB.Bodies[0];
        public ShapeChoice head = DB.Heads[0];
        public HairDef hair = DB.Hairs[1];
        public readonly Dictionary<string, PartOpt> parts = new Dictionary<string, PartOpt>();
        public readonly Dictionary<string, AdjPair> adj = new Dictionary<string, AdjPair>();
        public BellyAdj belly = new BellyAdj();
        public readonly Dictionary<string, Fab> fab = new Dictionary<string, Fab>();
        public readonly Dictionary<string, Dictionary<string, float>> shp = new Dictionary<string, Dictionary<string, float>>();

        // 由滑桿算出的外形
        public ShapeDef dBody, dHead, dTorso;
        public float rH, HY, BY;

        /// <summary>設計畫面預覽時，主體都當作塞了 55% 的棉花。</summary>
        public const float PreviewK = 1f + .07f * .55f;

        /// <summary>每次修改都 +1，渲染器用它判斷要不要重畫。</summary>
        public int version;

        public DollState()
        {
            foreach (var k in new[] { "ears", "arms", "feet", "belly" }) parts[k] = DB.Parts[k].None;
            foreach (var kv in DB.Sliders) shp[kv.Key] = kv.Value.ToDictionary(s => s.key, s => s.def);
            fab["hair"] = new Fab(DB.Mat("minky"), DB.Col("cocoa"));
            Rebuild();
        }

        public void Touch() => version++;
        public bool IsHuman => kind == "human";
        public bool IsHard => material != "cloth";

        // ---------- 滑桿 → 外形 ----------
        public void Rebuild()
        {
            var s = shp;
            var b = body.shape; var bw = s["body"];
            var pb = new List<Bump>(b.bumps);
            if (Mathf.Abs(bw["pear"]) > .02f)
            {
                bool up = bw["pear"] < 0; float a = Mathf.Abs(bw["pear"]) * .28f;
                pb.Add(new Bump(up ? -40 : 40, a, .5f)); pb.Add(new Bump(up ? -140 : 140, a, .5f));
            }
            dBody = new ShapeDef(b.ax * bw["w"], b.ay * bw["h"], Mathf.Min(5, b.p + bw["sq"] * 3), pb.ToArray());

            var h = head.shape; var hw = s["head"];
            dHead = new ShapeDef(h.ax * hw["w"], h.ay * hw["h"], Mathf.Min(5, h.p + hw["sq"] * 2.5f));

            var t = s["torso"];
            dTorso = new ShapeDef(.62f * t["w"], .6f * t["h"], 2f,
                new Bump(-90, .18f, .22f),
                new Bump(t["armA"], t["armL"], t["armW"], false, "右手"), new Bump(180 - t["armA"], t["armL"], t["armW"], false, "左手"),
                new Bump(90 - t["legSp"], t["legL"], t["legW"], false, "右腳"), new Bump(90 + t["legSp"], t["legL"], t["legW"], false, "左腳"));

            rH = SM.R * .8f * hw["size"];
            float rT = SM.R * .62f, HY0 = 232f;
            float hb = HY0 + rH * SM.ShapeR(dHead, Mathf.PI / 2);
            float BY0 = hb + rT * SM.ShapeR(dTorso, -Mathf.PI / 2) - 14;
            float bottom = -1e9f;
            for (int i = 0; i < 72; i++) { float a = i / 72f * 2 * Mathf.PI; bottom = Mathf.Max(bottom, BY0 + Mathf.Sin(a) * rT * SM.ShapeR(dTorso, a)); }
            float shift = Mathf.Max(0, bottom - 475);
            HY = HY0 - shift; BY = BY0 - shift;
            Touch();
        }

        public string BodyClass()
        {
            var t = shp["torso"];
            return $"{(t["h"] < .9f ? "15cm" : t["h"] <= 1.15f ? "20cm" : "25cm")} {(t["w"] <= 1.15f ? "正常體" : "胖胖體")}";
        }

        public string HeadClass()
        {
            var h = shp["head"];
            float hs = h["size"] * Mathf.Sqrt(h["w"] * h["h"]);
            return hs < .94f ? "頭圍 S" : hs <= 1.1f ? "頭圍 M" : "頭圍 L";
        }

        // ---------- 主體 ----------
        public List<MainPiece> Mains()
        {
            if (IsHuman)
                return new List<MainPiece> {
                    new MainPiece { id = "torso", name = "身體", sh = dTorso, cx = SM.CX, cy = BY, r = SM.R * .62f, gap = -90 },
                    new MainPiece { id = "head", name = "頭", sh = dHead, cx = SM.CX, cy = HY, r = rH, gap = 90 },
                };
            return new List<MainPiece> { new MainPiece { id = "body", name = "身體", sh = dBody, cx = SM.CX, cy = SM.CY, r = SM.R, gap = 90 } };
        }

        public MainPiece GetMain(string id) => Mains().FirstOrDefault(m => m.id == id);
        public string HostOf(string key) => IsHuman ? "head" : "body";

        public List<Vector2> MainOutline(MainPiece m, int n = 200) => SM.Outline(m.sh, m.r / SM.R, n, m.cx, m.cy, PreviewK);

        public Vector2 ToDoll(MainPiece m, float dx, float dy) => new Vector2(m.cx + dx * PreviewK, m.cy + dy * PreviewK);

        // ---------- 部件與位置 ----------
        public IEnumerable<string> ActiveParts(string layer = null) =>
            DB.PartKeys.Where(k => !parts[k].IsNone && (layer == null || parts[k].layer == layer));

        public Adj DefAdj(PartOpt o)
        {
            bool h = IsHuman && o.layer == "back";
            return new Adj { a = o.a, inset = h ? o.inset - .12f : o.inset, tilt = o.tilt, scale = h ? 1.25f : 1f };
        }

        public void ResetAdj(string key)
        {
            if (key == "belly") { belly = new BellyAdj(); Touch(); return; }
            var o = parts[key];
            if (o.IsNone) adj.Remove(key);
            else adj[key] = new AdjPair { left = DefAdj(o), right = DefAdj(o) };
            Touch();
        }

        public Adj AdjOf(string key, int side) => adj.TryGetValue(key, out var A) ? A.Get(side) : DefAdj(parts[key]);

        public void SetAdj(string key, int side, Action<Adj> fn)
        {
            if (!adj.TryGetValue(key, out var A)) return;
            var q = A.Get(side); fn(q);
            if (A.sym) { var c = q.Clone(); if (side < 0) A.right = c; else A.left = c; }
            Touch();
        }

        public struct Xform { public Vector2 pos; public float rot, k; public MainPiece m; }

        public Xform PartXform(string key, int side)
        {
            var o = parts[key]; var q = AdjOf(key, side); var m = GetMain(HostOf(key));
            float th = (-90 + side * q.a) * SM.D;
            float r = m.r * SM.ShapeR(m.sh, th) * PreviewK * (1 - q.inset);
            return new Xform
            {
                pos = new Vector2(m.cx + Mathf.Cos(th) * r, m.cy + Mathf.Sin(th) * r),
                rot = o.down ? Mathf.PI - side * q.tilt * SM.D : th + Mathf.PI / 2 + side * q.tilt * SM.D,
                k = q.scale * m.r / SM.R,
                m = m,
            };
        }

        List<Vector2> XformPoly(List<Vector2> P, string key, int side, float extra)
        {
            var X = PartXform(key, side); float kk = SM.R * X.k * extra;
            var o = new List<Vector2>(P.Count);
            foreach (var p in P) o.Add(X.pos + SM.RotV(p.x * side * kk, p.y * kk, X.rot));
            return o;
        }

        float PartExtra(string key) => 1 + (parts[key].nostuff ? 0 : .12f * .55f);

        public List<Vector2> PartPoly(string key, int side) => XformPoly(parts[key].P, key, side, PartExtra(key));

        public List<Vector2> InnerPoly(string key, int side)
        {
            var P = parts[key].P; var c = SM.Centroid(P);
            var inner = P.Select(p => new Vector2(c.x + (p.x - c.x) * .56f, c.y + (p.y - c.y) * .62f)).ToList();
            return XformPoly(inner, key, side, PartExtra(key));
        }

        public List<Vector2> BellyPoly()
        {
            var m = GetMain("body");
            return parts["belly"].P.Select(p => ToDoll(m, (p.x * belly.scale + belly.dx) * m.r, (p.y * belly.scale + belly.dy) * m.r)).ToList();
        }

        // ---------- 頭髮 ----------
        public HairGeo GetHair()
        {
            var h = hair;
            if (!IsHuman || h == null || h.id == "none") return null;
            var m = GetMain("head"); float r = m.r;
            Vector2 At(float t, float k) { float rr = r * SM.ShapeR(m.sh, t) * PreviewK * k; return new Vector2(m.cx + Mathf.Cos(t) * rr, m.cy + Mathf.Sin(t) * rr); }
            float t0 = (180 - h.side) * SM.D, t1 = (360 + h.side) * SM.D;
            var cap = new List<Vector2>();
            for (int i = 0; i <= 48; i++) { float t = t0 + (t1 - t0) * i / 48f; cap.Add(At(t, 1.08f + (h.fluff > 0 ? h.fluff * Mathf.Abs(Mathf.Sin(t * 9)) : 0))); }
            cap.Add(At(t1, .84f));
            float by = m.cy + h.bang * r, bx = r * .8f;
            for (int i = 0; i <= 24; i++)
            {
                float u = 1 - 2 * i / 24f, y = by;
                if (h.bs == "curve") y += .1f * r * (1 - u * u);
                else if (h.bs == "zig") y += .08f * r * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2.5f));
                else if (h.bs == "part") y -= .16f * r * (1 - Mathf.Abs(u));
                else y += .02f * r;
                cap.Add(new Vector2(m.cx + u * bx, y));
            }
            cap.Add(At(t0, .84f));
            List<Vector2> back = null; List<List<Vector2>> tails = null;
            if (h.back == "long")
            {
                back = new List<Vector2>();
                for (int i = 0; i <= 30; i++) back.Add(At((160 + i / 30f * 220) * SM.D, 1.12f));
                float y2 = m.cy + 1.45f * r;
                back.Add(new Vector2(m.cx + 1.02f * r, y2 - .2f * r)); back.Add(new Vector2(m.cx + .7f * r, y2));
                back.Add(new Vector2(m.cx - .7f * r, y2)); back.Add(new Vector2(m.cx - 1.02f * r, y2 - .2f * r));
            }
            if (h.back == "twin")
            {
                tails = new List<List<Vector2>>();
                foreach (int s in new[] { -1, 1 })
                {
                    var c = new Vector2(m.cx + s * 1.08f * r, m.cy + .42f * r);
                    tails.Add(SM.EllPoly(0, 0, .3f * r, .62f * r, 30).Select(p => c + SM.RotV(p.x, p.y, -s * .25f)).ToList());
                }
            }
            return new HairGeo { cap = cap, back = back, tails = tails, head = m };
        }

        // ---------- 布料 ----------
        public Fab FabOf(string slot)
        {
            fab.TryGetValue(slot, out var f); fab.TryGetValue("body", out var b);
            return new Fab(f?.mat ?? b?.mat, f?.color ?? b?.color);
        }

        public bool DesignReady => fab.TryGetValue("body", out var b) && b?.mat != null && b.color != null;

        public List<string> UsedSlots()
        {
            var u = new List<string> { "body" };
            if (IsHuman && hair.id != "none") u.Add("hair");
            if (!parts["ears"].IsNone) { u.Add("ears"); if (parts["ears"].inner) u.Add("earInner"); }
            if (!parts["arms"].IsNone) u.Add("arms");
            if (!parts["feet"].IsNone) u.Add("feet");
            if (!parts["belly"].IsNone) u.Add("belly");
            return u;
        }

        public int FabricCount() => UsedSlots().Select(FabOf).Where(f => f.mat != null && f.color != null).Select(f => f.mat.id + f.color.id).Distinct().Count();

        // ---------- 點選 ----------
        public struct Hit { public string tab, key; public int side; }

        /// <summary>點畫面上的哪個部位（和網頁版的優先順序一樣：目前分頁的部件最優先）。</summary>
        public Hit? PickAt(Vector2 p, string currentTab)
        {
            if (DB.PartKeys.Contains(currentTab) && !parts[currentTab].IsNone)
                foreach (int s in new[] { -1, 1 }) if (SM.Pip(PartPoly(currentTab, s), p)) return new Hit { tab = currentTab, key = currentTab, side = s };
            foreach (var k in ActiveParts("front")) foreach (int s in new[] { -1, 1 }) if (SM.Pip(PartPoly(k, s), p)) return new Hit { tab = k, key = k, side = s };
            if (!IsHuman && !parts["belly"].IsNone && SM.Pip(BellyPoly(), p)) return new Hit { tab = "belly", key = "belly" };
            var hg = GetHair();
            if (hg != null && SM.Pip(hg.cap, p)) return new Hit { tab = "hair" };
            foreach (var k in ActiveParts("back")) foreach (int s in new[] { -1, 1 }) if (parts[k].inner && SM.Pip(InnerPoly(k, s), p)) return new Hit { tab = "earInner", key = k, side = s };
            foreach (var m in Enumerable.Reverse(Mains())) if (SM.Pip(MainOutline(m, 120), p)) return new Hit { tab = m.id == "head" ? "head" : "body" };
            foreach (var k in ActiveParts("back")) foreach (int s in new[] { -1, 1 }) if (SM.Pip(PartPoly(k, s), p)) return new Hit { tab = k, key = k, side = s };
            if (hg != null && ((hg.back != null && SM.Pip(hg.back, p)) || (hg.tails != null && hg.tails.Any(P => SM.Pip(P, p))))) return new Hit { tab = "hair" };
            return null;
        }
    }
}
