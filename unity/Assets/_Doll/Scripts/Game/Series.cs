using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>稀有度：名稱、抽中權重、光芒顏色（隱藏款是彩虹）。</summary>
    public class Rarity
    {
        public string id, name; public float w; public Color color; public bool rainbow;
        public static readonly List<Rarity> All = new List<Rarity>
        {
            new Rarity { id = "common", name = "普通", w = 1, color = new Color(1f, .945f, .812f) },
            new Rarity { id = "rare", name = "稀有", w = .35f, color = new Color(1f, .835f, .29f) },
            new Rarity { id = "secret", name = "隱藏款", w = .08f, color = Color.white, rainbow = true },
        };
        public static Rarity Of(string id) => All.First(r => r.id == id);
    }

    /// <summary>盒子的外觀：底色、圖案、圖案顏色。</summary>
    public class BoxStyle
    {
        public string color = "pink", pattern = "dots", pcolor = "white";
        public static readonly (string id, string name)[] Patterns = { ("none", "素面"), ("dots", "圓點"), ("stripes", "斜紋"), ("stars", "星星"), ("checker", "格子") };
    }

    public class SeriesEntry { public DollState doll; public string rarity = "common"; }

    /// <summary>一個盲盒系列：2–7 隻自己做的娃娃，各有稀有度；開盒時依權重抽一隻。（網頁版 meta.series）</summary>
    public class BlindSeries
    {
        public string name;
        public readonly List<SeriesEntry> dolls = new List<SeriesEntry>();
        public BoxStyle box = new BoxStyle();
        public readonly Dictionary<DollState, int> owned = new Dictionary<DollState, int>();
        public int opened;
        public const int Max = 7, Price = 10;

        public bool CanOpen => dolls.Count >= 2;

        public float Odds(SeriesEntry e)
        {
            float tot = dolls.Sum(x => Rarity.Of(x.rarity).w);
            return tot > 0 ? Rarity.Of(e.rarity).w / tot : 0;
        }

        /// <summary>依機率抽一隻（r 是 0–1 的亂數）。</summary>
        public SeriesEntry Pick(float r)
        {
            foreach (var e in dolls) { r -= Odds(e); if (r <= 0) return e; }
            return dolls[dolls.Count - 1];
        }

        /// <summary>放進或拿出系列。回傳 null 表示成功，否則是原因。</summary>
        public string Toggle(DollState d)
        {
            var e = dolls.FirstOrDefault(x => x.doll == d);
            if (e != null) { dolls.Remove(e); return null; }
            if (dolls.Count >= Max) return $"一個系列最多 {Max} 款";
            dolls.Add(new SeriesEntry { doll = d });
            return null;
        }

        /// <summary>設定稀有度；隱藏款只能有一款，原本的隱藏款會變成稀有。</summary>
        public void SetRarity(SeriesEntry e, string r)
        {
            if (r == "secret") foreach (var x in dolls) if (x.rarity == "secret") x.rarity = "rare";
            e.rarity = r;
        }

        public int Collected => dolls.Count(e => owned.TryGetValue(e.doll, out var n) && n > 0);
    }

    /// <summary>盒子的圖案貼圖（48×48 重複）。</summary>
    public static class BoxArt
    {
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        public static Material MaterialFor(BoxStyle b)
        {
            string key = b.color + b.pattern + b.pcolor;
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            const int S = 48; var bg = DB.Col(b.color).color; var pc = PaintGame.Paints.First(x => x.id == b.pcolor).color;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                var c = bg; float fx = x + .5f, fy = y + .5f;
                switch (b.pattern)
                {
                    case "dots": if ((fx - 12) * (fx - 12) + (fy - 12) * (fy - 12) <= 30 || (fx - 36) * (fx - 36) + (fy - 36) * (fy - 36) <= 30) c = pc; break;
                    case "stripes": if (Mathf.Repeat(fx - fy, 16) < 6.5f) c = pc; break;
                    case "stars": if (InStar(fx, fy, 12, 13) || InStar(fx, fy, 36, 36)) c = pc; break;
                    case "checker": if ((x < 24) == (y < 24)) c = Color.Lerp(bg, pc, .55f); break;
                }
                px[(S - 1 - y) * S + x] = c;
            }
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            t.SetPixels(px); t.Apply();
            return mats[key] = new Material(FabricTextures.SpriteShader) { mainTexture = t, name = "Box_" + key };
        }

        static bool InStar(float x, float y, float cx, float cy)
        {
            var P = new List<Vector2>();
            for (int i = 0; i < 10; i++) { float a = i / 10f * Mathf.PI * 2 - Mathf.PI / 2, r = i % 2 == 1 ? 3.2f : 7.3f; P.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r)); }
            return SM.Pip(P, new Vector2(x, y));
        }

        static Color Shade(Color c, float amt) => amt < 0 ? new Color(c.r * (1 + amt), c.g * (1 + amt), c.b * (1 + amt), 1) : Color.Lerp(c, Color.white, amt);

        /// <summary>
        /// 立體的盒子：正面（圖案、名牌、問號）、右側面、盒蓋。x,y 是正面左上角；lid 0–1 是盒蓋掀開的程度（≥1.5 表示飛走了）。
        /// 文字另外由 Labels 畫。（網頁版 drawBox）
        /// </summary>
        public static void Draw(Sketch sk, float x, float y, float w, float h, float d, BoxStyle b, float lid = 0, bool open = false, float shake = 0)
        {
            x += shake; var col = DB.Col(b.color).color; float dx = d * .6f, dy = d * .5f;
            var g = sk.P;
            g.Ellipse(new Vector2(x + w / 2 + dx / 2, y + h + 6), w * .62f, 14, new Color(0, 0, 0, .18f), 32);
            g.Fill(new List<Vector2> { new Vector2(x + w, y), new Vector2(x + w + dx, y - dy), new Vector2(x + w + dx, y + h - dy), new Vector2(x + w, y + h) }, Shade(col, -.28f));
            sk.With(MaterialFor(b)).Rect(x, y, w, h, Color.white);
            g = sk.P;
            g.Line(new List<Vector2> { new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h) }, 2, Shade(col, -.4f), true);
            g.RoundRect(x + 12, y + 16, w - 24, 34, 8, new Color(1, 1, 1, .88f));
            g.Circle(new Vector2(x + w / 2, y + h * .58f), Mathf.Min(w, h) * .22f, new Color(1, 1, 1, .85f), 32);
            if (open) g.Fill(new List<Vector2> { new Vector2(x, y), new Vector2(x + dx, y - dy), new Vector2(x + w + dx, y - dy), new Vector2(x + w, y) }, new Color(.165f, .133f, .149f));
            if (lid < 1.5f)
            {
                var pivot = new Vector2(x + w / 2, y); float rot = -lid * .5f; var up = new Vector2(0, -lid * 90);
                List<Vector2> L(params Vector2[] v) => v.Select(p => pivot + SM.RotV(p.x - pivot.x, p.y - pivot.y, rot) + up).ToList();
                var top = L(new Vector2(x, y), new Vector2(x + dx, y - dy), new Vector2(x + w + dx, y - dy), new Vector2(x + w, y));
                g.Fill(top, Shade(col, .18f)); g.Line(top, 2, Shade(col, -.4f), true);
                g.Fill(L(new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + 10), new Vector2(x, y + 10)), Shade(col, -.08f));
            }
        }

        public static void Labels(GameScreen ui, float x, float y, float w, float h, BoxStyle b, string name, float shake = 0)
        {
            x += shake; var col = DB.Col(b.color).color;
            ui.CanvasLabel(new Vector2(x + w / 2, y + 33), name, Mathf.RoundToInt(Mathf.Min(18, w / 9)), new Color(.29f, .23f, .2f), TextAnchor.MiddleCenter, true);
            ui.CanvasLabel(new Vector2(x + w / 2, y + h * .6f), "?", Mathf.RoundToInt(Mathf.Min(w, h) * .26f), Shade(col, -.45f), TextAnchor.MiddleCenter, true);
            ui.CanvasLabel(new Vector2(x + w / 2, y + h - 14), "BLIND BOX・盲盒", 11, new Color(1, 1, 1, .95f), TextAnchor.MiddleCenter, true);
        }

        /// <summary>鋁箔袋：上下壓紋、撕開線，tear 0–1 是撕開的程度。clipY 以下不畫（從盒子裡升起來時用）。</summary>
        public static void Bag(Sketch sk, float x, float y, float w, float h, float tear, float now, float shake, float clipY)
        {
            x += shake; var g = sk.P;
            List<Vector2> C(List<Vector2> P) => Garments.ClipY(P, clipY, true);
            var body = C(MeshBuilder.RoundRectPoly(x, y, w, h, 8));
            if (body.Count < 3) return;
            g.Fill(body, new Color(.84f, .86f, .89f));
            var shine = C(new List<Vector2> { new Vector2(x + w * .3f, y), new Vector2(x + w * .62f, y), new Vector2(x + w * .45f, y + h), new Vector2(x + w * .13f, y + h) });
            if (shine.Count >= 3) g.Fill(shine, new Color(.97f, .98f, .99f, .75f));
            g.Line(body, 1.5f, new Color(.56f, .59f, .64f), true);
            for (float i = 0; i <= w; i += 6)
            {
                if (y + 14 < clipY) g.Seg(new Vector2(x + i, y + 2), new Vector2(x + i, y + 14), 1, new Color(.6f, .63f, .67f));
                if (y + h - 2 < clipY) g.Seg(new Vector2(x + i, y + h - 14), new Vector2(x + i, y + h - 2), 1, new Color(.6f, .63f, .67f));
            }
            float ty = y + 30;
            if (ty < clipY)
            {
                g.Line(new[] { new Vector2(x + w * tear, ty), new Vector2(x + w, ty) }, 1.5f, new Color(.878f, .478f, .373f), false, 5, 4);
                if (tear > 0) g.Seg(new Vector2(x, ty), new Vector2(x + w * tear, ty + Mathf.Sin(now / 40) * 1.5f), 2, new Color(.29f, .23f, .2f));
            }
        }
    }
}
