using System.Collections.Generic;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>用程式產生布料、搪膠、木頭的重複紋理（48×48），和網頁版的花紋對應。</summary>
    public static class FabricTextures
    {
        const int S = 48;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Shader shader;

        public static Shader SpriteShader
        {
            get
            {
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                return shader;
            }
        }

        public static Material MaterialFor(MatDef mat, Color color)
        {
            string key = (mat != null ? mat.id : "plain") + ColorUtility.ToHtmlStringRGB(color);
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(SpriteShader) { mainTexture = Texture(mat?.id ?? "plain", color), name = "Fabric_" + key };
            mats[key] = m;
            return m;
        }

        static Material plain;
        /// <summary>純白材質，用在外框線與光澤（顏色由頂點色決定）。</summary>
        public static Material Plain => plain != null ? plain : (plain = new Material(SpriteShader) { mainTexture = Texture2D.whiteTexture, name = "PlainWhite" });

        public static Texture2D Texture(string id, Color c)
        {
            string key = id + ColorUtility.ToHtmlStringRGB(c);
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = key };
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            var rnd = new System.Random(7);
            void Blend(int x, int y, Color col, float a)
            {
                x = ((x % S) + S) % S; y = ((y % S) + S) % S;
                int i = y * S + x; px[i] = Color.Lerp(px[i], col, a);
            }
            switch (id)
            {
                case "felt":
                    for (int i = 0; i < 260; i++) { int x = rnd.Next(S), y = rnd.Next(S); bool light = rnd.NextDouble() < .5; Blend(x, y, light ? Color.white : Color.black, light ? .12f : .08f); Blend(x + 1, y, light ? Color.white : Color.black, light ? .12f : .08f); }
                    break;
                case "cotton":
                    foreach (var (cx, cy, r, a) in new[] { (12, 12, 4.5f, .6f), (36, 36, 4.5f, .6f), (36, 12, 2f, .3f), (12, 36, 2f, .3f) })
                        for (int y = -5; y <= 5; y++) for (int x = -5; x <= 5; x++) if (x * x + y * y <= r * r) Blend(cx + x, cy + y, Color.white, a);
                    break;
                case "knit":
                    for (int y0 = 0; y0 < S; y0 += 8) for (int x0 = 0; x0 < S; x0 += 8) for (int k = 0; k <= 6; k++) { Blend(x0 + k * 4 / 6, y0 + k, Color.black, .13f); Blend(x0 + 8 - k * 4 / 6, y0 + k, Color.black, .13f); }
                    break;
                case "minky":
                    for (int i = 0; i < 420; i++) { int x = rnd.Next(S), y = rnd.Next(S); bool light = rnd.NextDouble() < .5; Blend(x, y, light ? Color.white : Color.black, light ? .1f : .05f); Blend(x, y + 1, light ? Color.white : Color.black, light ? .1f : .05f); }
                    break;
                case "wood":
                    for (int y0 = 3; y0 < S; y0 += 7) for (int x = 0; x < S; x++) { int yy = y0 + Mathf.RoundToInt(Mathf.Sin((x + y0 * 3) / 9f) * 1.6f); Blend(x, yy, new Color(.31f, .18f, .08f), .2f); }
                    break;
            }
            // Unity 的貼圖 y 往上，網頁版 y 往下；花紋左右對稱，上下翻轉不影響外觀。
            t.SetPixels(px);
            t.Apply();
            cache[key] = t;
            return t;
        }
    }
}
