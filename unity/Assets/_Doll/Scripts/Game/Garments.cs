using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>衣服或配件的種類。sizeBy：body = 依身形、head = 依頭圍、none = 通用尺寸；anchor：跟著身體或頭。</summary>
    public class GType
    {
        public string id, name, piece, slot, sizeBy, anchor, cat, desc;
        public int mult;
        public string[] tags;
        public (string key, string name, float min, float max, float def)[] ps;
    }

    /// <summary>一件衣服（或帽子、髮帶……）。完成後收進衣櫃，同尺寸的人形娃娃都能穿。</summary>
    public class Garment
    {
        public string id, type, cls, name;
        public Fab fab = new Fab(null, null);
        public Dictionary<string, float> p = new Dictionary<string, float>();
        /// <summary>衣服上的裝飾，位置相對於掛的位置（身體或頭）的中心。</summary>
        public readonly List<DecorItem> trims = new List<DecorItem>();
        public readonly List<Seam> seams = new List<Seam>();
        public bool done;
        public GType T => Garments.Of(type);
    }

    /// <summary>客人的娃娃（衣服訂單用）：身形、頭圍、頭型、髮型、膚色、五官。</summary>
    public class DollSpec
    {
        public float torsoW = 1, torsoH = 1, headSize = 1;
        public string headId = "round", hair = "none", hairColor = "cocoa", skin = "skin", skinMat = "cotton", name = "";
        public string[] decor = new string[0];
    }

    /// <summary>衣服的資料、版型外形，以及人台、客人的娃娃。（網頁版 GTYPES / garmentPoly / buildDoll）</summary>
    public static class Garments
    {
        static (string, string, float, float, float) P(string k, string n, float a, float b, float d) => (k, n, a, b, d);

        public static readonly List<GType> Types = new List<GType>
        {
            new GType { id = "top", name = "上衣", piece = "衣身", slot = "top", mult = 2, sizeBy = "body", anchor = "torso", cat = "cloth", tags = new string[0], desc = "穿在身上，可以調袖長", ps = new[] { P("sleeve", "袖長", 0, 1, .5f), P("len", "衣長", 0, 1, .45f), P("loose", "寬鬆", 1.02f, 1.25f, 1.08f) } },
            new GType { id = "pants", name = "褲子", piece = "褲片", slot = "bottom", mult = 2, sizeBy = "body", anchor = "torso", cat = "cloth", tags = new[] { "活潑" }, desc = "兩條褲管，跟著腿走", ps = new[] { P("len", "褲長", .15f, 1, .45f), P("loose", "寬鬆", 1.02f, 1.3f, 1.1f) } },
            new GType { id = "skirt", name = "裙子", piece = "裙片", slot = "bottom", mult = 2, sizeBy = "body", anchor = "torso", cat = "cloth", tags = new[] { "可愛" }, desc = "從腰往下張開", ps = new[] { P("len", "裙長", .15f, 1, .4f), P("flare", "蓬度", 0, 1, .5f) } },
            new GType { id = "cape", name = "斗篷", piece = "斗篷", slot = "outer", mult = 1, sizeBy = "body", anchor = "torso", cat = "cloth", tags = new[] { "復古", "高級" }, desc = "披在背後的外衣", ps = new[] { P("len", "長度", 0, 1, .5f), P("width", "寬度", 0, 1, .5f) } },
            new GType { id = "beanie", name = "毛帽", piece = "帽片", slot = "hat", mult = 2, sizeBy = "head", anchor = "head", cat = "acc", tags = new[] { "溫柔" }, desc = "包住頭的針織帽，可以加毛球", ps = new[] { P("depth", "帽深", 0, 1, .5f), P("pom", "毛球", 0, 1, 1) } },
            new GType { id = "beret", name = "貝雷帽", piece = "帽片", slot = "hat", mult = 2, sizeBy = "head", anchor = "head", cat = "acc", tags = new[] { "高級", "復古" }, desc = "扁扁斜斜的帽子", ps = new[] { P("size", "大小", 0, 1, .5f), P("tilt", "歪斜", 0, 1, .75f) } },
            new GType { id = "hairband", name = "髮帶", piece = "髮帶", slot = "band", mult = 1, sizeBy = "none", anchor = "head", cat = "acc", tags = new[] { "可愛" }, desc = "壓在頭髮上的布髮帶", ps = new[] { P("thick", "粗細", 0, 1, .5f) } },
            new GType { id = "clip", name = "蝴蝶結髮夾", piece = "蝴蝶結", slot = "clip", mult = 2, sizeBy = "none", anchor = "head", cat = "acc", tags = new[] { "可愛", "活潑" }, desc = "夾在頭上的蝴蝶結", ps = new[] { P("pos", "位置", 0, 1, .75f), P("size", "大小", 0, 1, .5f) } },
            new GType { id = "bag", name = "小包包", piece = "包身", slot = "bag", mult = 2, sizeBy = "none", anchor = "torso", cat = "acc", tags = new[] { "活潑" }, desc = "掛在手上的小包包", ps = new[] { P("size", "大小", 0, 1, .5f) } },
        };
        public static GType Of(string id) => Types.First(t => t.id == id);

        /// <summary>身形：名稱、身高倍率、寬度倍率。</summary>
        public static readonly (string name, float h, float w)[] Classes =
        { ("15cm 正常體", .82f, 1), ("20cm 正常體", 1, 1), ("25cm 正常體", 1.27f, 1), ("15cm 胖胖體", .82f, 1.38f), ("20cm 胖胖體", 1, 1.38f), ("25cm 胖胖體", 1.27f, 1.38f) };
        public static readonly (string name, float size)[] HClasses = { ("頭圍 S", .88f), ("頭圍 M", 1), ("頭圍 L", 1.18f) };
        public static (float h, float w) ClassDims(string cls) { var c = Classes.FirstOrDefault(x => x.name == cls); return c.name == null ? (1, 1) : (c.h, c.w); }
        public static float SizeOf(string hcls) { var h = HClasses.FirstOrDefault(x => x.name == hcls); return h.name == null ? 1 : h.size; }

        /// <summary>衣服上可以加的裝飾（蕾絲、鈕扣、口袋，加上幾種娃娃配件）。</summary>
        public static readonly string[] Trims = { "lace", "button1", "pocket", "bow", "flower", "star", "heart" };

        public static string DefaultCls(string type, bool free)
        {
            var sb = Of(type).sizeBy;
            return sb == "none" ? "通用" : !free ? null : sb == "head" ? "頭圍 M" : "20cm 正常體";
        }

        public static Garment New(string cls)
        {
            var g = new Garment { type = "top", cls = cls };
            foreach (var x in Of("top").ps) g.p[x.key] = x.def;
            return g;
        }

        public static void SetType(Garment g, string id, bool free)
        {
            string was = g.T.sizeBy; g.type = id;
            if (g.T.sizeBy != was) g.cls = DefaultCls(id, free);
            g.p.Clear(); foreach (var x in g.T.ps) g.p[x.key] = x.def;
        }

        /// <summary>這件衣服在這隻娃娃身上要什麼尺寸。</summary>
        public static string FitClassOf(Garment g, DollState d) => g.T.sizeBy == "head" ? d.HeadClass() : g.T.sizeBy == "body" ? d.BodyClass() : "通用";

        // ---------- 版型外形 ----------
        public static List<Vector2> ClipY(List<Vector2> P, float y, bool keepAbove)
        {
            var o = new List<Vector2>();
            bool In(Vector2 p) => keepAbove ? p.y <= y : p.y >= y;
            for (int i = 0; i < P.Count; i++)
            {
                var a = P[i]; var b = P[(i + 1) % P.Count]; bool ia = In(a), ib = In(b);
                if (ia) o.Add(a);
                if (ia != ib) { float t = (y - a.y) / (b.y - a.y); o.Add(new Vector2(a.x + (b.x - a.x) * t, y)); }
            }
            return o;
        }

        static float SegD(Vector2 q, Vector2 A, Vector2 B)
        {
            var v = B - A; float t = Mathf.Clamp01(Vector2.Dot(q - A, v) / Mathf.Max(1e-6f, v.sqrMagnitude));
            return Vector2.Distance(q, A + v * t);
        }

        /// <summary>
        /// 衣服穿在這隻娃娃（t = 身體主體、h = 頭）身上的外形。版型大小依衣服的尺寸，手腳的角度跟著穿的娃娃。
        /// </summary>
        public static List<Vector2> Poly(Garment g, MainPiece t, MainPiece h)
        {
            var p = g.p; var T = g.T; float D = SM.D, R = SM.R;
            var o = new List<Vector2>();
            if (T.anchor == "head")
            {
                var m = h; float hs = T.sizeBy == "head" ? SizeOf(g.cls) * (R * .8f) / m.r : 1, r = m.r * hs, cx = m.cx, cy = m.cy;
                Vector2 At(float a, float k) { float rr = r * SM.ShapeR(m.sh, a) * k; return new Vector2(cx + Mathf.Cos(a) * rr, cy + Mathf.Sin(a) * rr); }
                switch (g.type)
                {
                    case "beanie":
                        {
                            float dep = (5 + p["depth"] * 30) * D;
                            for (int i = 0; i <= 40; i++) o.Add(At(Mathf.PI - dep + (Mathf.PI + 2 * dep) * i / 40, 1.12f));
                            float yb = cy + Mathf.Sin(dep) * r * .95f - r * .06f, rx = r * SM.ShapeR(m.sh, 0) * .98f;
                            o.Add(new Vector2(cx + rx, yb));
                            for (int i = 1; i < 20; i++) { float u = 1 - 2 * i / 20f; o.Add(new Vector2(cx + u * rx, yb + r * .03f * (1 - u * u))); }
                            o.Add(new Vector2(cx - r * SM.ShapeR(m.sh, Mathf.PI) * .98f, yb));
                            return o;
                        }
                    case "beret":
                        {
                            float tl = (p["tilt"] - .5f) * .6f; var c0 = new Vector2(cx + tl * r * .6f, cy - r * SM.ShapeR(m.sh, -Mathf.PI / 2) * .82f);
                            float rx = r * (.75f + .4f * p["size"]), ry = r * (.26f + .08f * p["size"]);
                            return SM.EllPoly(0, 0, rx, ry, 48).Select(q => c0 + SM.RotV(q.x, q.y, tl * .6f)).ToList();
                        }
                    case "hairband":
                        {
                            float w = .06f + .1f * p["thick"];
                            for (int i = 0; i <= 30; i++) o.Add(At((200 + 140 * i / 30f) * D, 1.04f + w));
                            for (int i = 30; i >= 0; i--) o.Add(At((200 + 140 * i / 30f) * D, 1.02f));
                            return o;
                        }
                    case "clip":
                        {
                            float a = (-90 + (p["pos"] - .5f) * 200) * D, s = r * (.14f + .12f * p["size"]), rot = a + Mathf.PI / 2; var q = At(a, 1);
                            var pts = new List<Vector2>();
                            foreach (int sd in new[] { -1, 1 })
                            {
                                SM.Qbez(Vector2.zero, new Vector2(sd * 1.2f, -.9f), new Vector2(sd * 1.3f, 0), 10, pts);
                                SM.Qbez(new Vector2(sd * 1.3f, 0), new Vector2(sd * 1.2f, .9f), Vector2.zero, 10, pts);
                            }
                            return pts.Select(v => q + SM.RotV(v.x * s, v.y * s, rot)).ToList();
                        }
                }
            }
            if (g.type == "bag")
            {
                var m = t; var b = m.sh.bumps[1]; float a = b.deg * D, rr = m.r * SM.ShapeR(m.sh, a) * .95f;
                float hx = m.cx + Mathf.Cos(a) * rr, hy = m.cy + Mathf.Sin(a) * rr, s = m.r * (.32f + .25f * p["size"]);
                SM.Qbez(new Vector2(hx - s * .5f, hy + s * .15f), new Vector2(hx - s * .55f, hy + s * 1.05f), new Vector2(hx, hy + s * 1.05f), 10, o);
                SM.Qbez(new Vector2(hx, hy + s * 1.05f), new Vector2(hx + s * .55f, hy + s * 1.05f), new Vector2(hx + s * .5f, hy + s * .15f), 10, o);
                o.Add(new Vector2(hx + s * .45f, hy + s * .1f)); o.Add(new Vector2(hx - s * .45f, hy + s * .1f));
                return o;
            }
            {
                var m = t; var d = ClassDims(g.cls); var bp = m.sh.bumps; float r = m.r, cx = m.cx, cy = m.cy;
                switch (g.type)
                {
                    case "top":
                        {
                            float sl = Mathf.Max(.12f, p["sleeve"]);
                            var sh = new ShapeDef(.62f * d.w, .6f * d.h, 2f, bp[0], new Bump(bp[1].deg, bp[1].amp * sl, bp[1].w * 1.25f), new Bump(bp[2].deg, bp[2].amp * sl, bp[2].w * 1.25f));
                            return ClipY(SM.Outline(sh, 1.07f * p["loose"] * r / R, 200, cx, cy), cy + r * (-.05f + .6f * p["len"]), true);
                        }
                    case "pants":
                        {
                            var hip = new ShapeDef(.62f * d.w * 1.07f * p["loose"], .6f * d.h * 1.05f);
                            var legs = new[] { 3, 4 }.Select(i =>
                            {
                                float a = bp[i].deg * D; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                                float tip = r * SM.ShapeR(m.sh, a); var A = dir * r * .2f; float L = Mathf.Max(0, tip - r * .2f) * (.35f + .75f * p["len"]);
                                return (A, B: A + dir * L, w: r * bp[i].w * 1.15f * p["loose"] * (d.w > 1.2f ? 1.2f : 1));
                            }).ToList();
                            var pts = new List<Vector2>();
                            for (int i = 0; i < 200; i++)
                            {
                                float a = i / 200f * 2 * Mathf.PI; var u = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); float rr = r * SM.ShapeR(hip, a);
                                foreach (var l in legs) for (float k = r * 2.2f; k > rr; k -= 1.5f) if (SegD(u * k, l.A, l.B) <= l.w) { rr = k; break; }
                                pts.Add(new Vector2(cx + u.x * rr, cy + u.y * rr));
                            }
                            return ClipY(pts, cy - .08f * r, false);
                        }
                    case "skirt":
                        {
                            float ww = r * .62f * d.w * 1.1f, wy = cy - .05f * r, hy = wy + r * (.3f + .9f * p["len"]) * d.h, hw = ww * (1.05f + p["flare"] * .9f);
                            o.Add(new Vector2(cx - ww, wy));
                            SM.Qbez(new Vector2(cx + ww, wy), new Vector2(cx + ww * 1.05f, (wy + hy) / 2), new Vector2(cx + hw, hy), 14, o);
                            SM.Qbez(new Vector2(cx + hw, hy), new Vector2(cx, hy + r * .14f), new Vector2(cx - hw, hy), 18, o);
                            SM.Qbez(new Vector2(cx - hw, hy), new Vector2(cx - ww * 1.05f, (wy + hy) / 2), new Vector2(cx - ww, wy), 14, o);
                            return o;
                        }
                    default: // cape
                        {
                            float ty = cy - r * .45f, tw = r * .3f * d.w, by = cy + r * (.35f + .9f * p["len"]) * d.h, bw = r * (.85f + .6f * p["width"]) * d.w;
                            o.Add(new Vector2(cx - tw, ty));
                            SM.Qbez(new Vector2(cx + tw, ty), new Vector2(cx + bw * .8f, ty + r * .2f), new Vector2(cx + bw, by), 14, o);
                            SM.Qbez(new Vector2(cx + bw, by), new Vector2(cx, by + r * .18f), new Vector2(cx - bw, by), 18, o);
                            SM.Qbez(new Vector2(cx - bw, by), new Vector2(cx - bw * .8f, ty + r * .2f), new Vector2(cx - tw, ty), 14, o);
                            return o;
                        }
                }
            }
        }

        public static List<Vector2> PolyOn(Garment g, DollState d) => Poly(g, d.GetMain("torso"), d.GetMain("head"));
        public static MainPiece AnchorOn(Garment g, DollState d) => d.GetMain(g.T.anchor == "head" ? "head" : "torso");

        // ---------- 人形娃娃：客人的娃娃、人台 ----------
        /// <summary>做一隻完成的人形娃娃（棉花塞好、五官繡好）。</summary>
        public static DollState BuildDoll(DollSpec spec)
        {
            var d = new DollState { kind = "human", name = spec.name };
            d.shp["torso"]["w"] = spec.torsoW; d.shp["torso"]["h"] = spec.torsoH; d.shp["head"]["size"] = spec.headSize;
            d.head = DB.Heads.FirstOrDefault(x => x.id == spec.headId) ?? DB.Heads[0];
            d.hair = DB.Hairs.FirstOrDefault(x => x.id == spec.hair) ?? DB.Hairs[0];
            d.fab["body"] = new Fab(DB.Mat(spec.skinMat), DB.Col(spec.skin));
            d.fab["hair"] = new Fab(DB.Mat("minky"), DB.Col(spec.hairColor));
            d.Rebuild();
            d.regs = d.RegionsFor(); foreach (var r in d.regs) r.fill = .8f;
            foreach (var id in spec.decor) { var dd = DB.DecorById(id); var p = d.DecorPos(dd.pos); d.decor.Add(new DecorItem { type = id, x = p.x, y = p.y, s = .85f }); }
            d.Touch();
            return d;
        }
    }
}
