using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 交件：娃娃放上展示台，客人看了之後的表情、台詞和星等；4 星以上撒彩紙、冒愛心，2 星以下冒冷汗。
    /// 右側面板揭曉客人在意的項目、各項分數、帳目和評語。（網頁版 enterDeliver 與交件畫面）
    /// </summary>
    public class DeliverStage : CraftGame
    {
        class Bit { public Vector2 p, v; public Color c; public float r, l; }
        readonly List<Bit> confetti = new List<Bit>(), hearts = new List<Bit>();
        static readonly Vector2 Face = new Vector2(670, 430);
        float heartT;

        public Result R => ss.result;

        public DeliverStage(Session ss) : base(ss)
        {
            if (R != null && (R.stars >= 4 || R.free))
            {
                var cols = new[] { "#f4b6c6", "#f6d96b", "#a9d3ee", "#a6dcc0", "#e07a5f" }.Select(Hex).ToArray();
                for (int i = 0; i < (R.free ? 60 : 90); i++)
                    confetti.Add(new Bit { p = new Vector2(SM.CX - 90 + (Random.value - .5f) * 80, SM.CY - 40), v = new Vector2((Random.value - .5f) * 9, -Random.value * 9 - 3), c = cols[i % 5], r = Random.value * Mathf.PI, l = 1 });
            }
        }

        public int ConfettiCount => confetti.Count;
        public string Mood => R == null || R.free ? "" : R.stars >= 4 ? "happy" : R.stars <= 2 ? "sad" : "ok";
        DollState V => ss.ViewDoll;

        public override string Hint => null;
        public override bool Done => true;
        public override Vector2 DollOffset => new Vector2(-90, 0);

        public override void Tick(float dt)
        {
            float k = dt * 60;
            foreach (var p in confetti) { p.v.y += .25f * k; p.p += p.v * k; p.r += .1f * k; if (p.p.y > SM.H - 40) p.l -= dt * 2; }
            confetti.RemoveAll(p => p.l <= 0);
            if (Mood == "happy" && (heartT -= dt) <= 0) { heartT = .6f; hearts.Add(new Bit { p = Face + new Vector2((Random.value - .5f) * 50, -40), v = new Vector2((Random.value - .5f) * .3f, -.8f), l = 1 }); }
            foreach (var h in hearts) { h.p += h.v * k; h.l -= dt * .5f; }
            hearts.RemoveAll(h => h.l <= 0);
        }

        static Color Pastel(string who)
        {
            int h = 0; foreach (char c in who) h = h * 31 + c;
            return Color.HSVToRGB(Mathf.Abs(h % 360) / 360f, .28f, .97f);
        }

        public override void Draw(Sketch under, Sketch over)
        {
            float now = Now; var g = under.P;
            g.Rect(-600, -400, 2000, 1400, Hex("#e9cfb5"));
            g.Blob(new Vector2(SM.CX - 90, SM.CY - 20), 560, Hex("#fff6ea"), Hex("#e9cfb5"), 64);
            float b = V.Bounds().yMax, cx = SM.CX - 90;
            g.Ellipse(new Vector2(cx, b + 18), 150, 26, Hex("#c98b5e"), 40);
            g.Ellipse(new Vector2(cx, b + 8), 150, 24, Hex("#d9a173"), 40);

            var o = over.P;
            foreach (var p in confetti)
            {
                var c = p.c; c.a = Mathf.Clamp01(p.l);
                var q = new[] { new Vector2(-4, -2.5f), new Vector2(4, -2.5f), new Vector2(4, 2.5f), new Vector2(-4, 2.5f) }.Select(v => p.p + SM.RotV(v.x, v.y, p.r)).ToArray();
                o.Quad(q[0], q[1], q[2], q[3], c);
            }
            if (R == null || R.free) return;
            // 客人：依星等變表情
            var f = Face + new Vector2(0, Mathf.Sin(now / 500) * 4);
            o.Circle(f + new Vector2(0, 50), 34, Pastel(ss.order.who) * new Color(.85f, .85f, .85f, 1), 32);
            o.Circle(f, 46, Hex("#5a4636"), 40); o.Circle(f, 44, Hex("#ffe3cc"), 40);
            var ink = Hex("#4a3b32");
            if (Mood == "happy")
            {
                foreach (int s in new[] { -1, 1 }) o.Arc(f + new Vector2(s * 15, -2), 6, Mathf.PI * 1.15f, Mathf.PI * 1.85f, 3, ink, 10);
                o.Arc(f + new Vector2(0, 8), 13, .15f * Mathf.PI, .85f * Mathf.PI, 3, ink, 14);
                foreach (int s in new[] { -1, 1 }) o.Ellipse(f + new Vector2(s * 28, 10), 8, 5, new Color(1, .43f, .51f, .45f), 16);
            }
            else
            {
                foreach (int s in new[] { -1, 1 }) o.Circle(f + new Vector2(s * 15, -4), 4, ink, 12);
                if (Mood == "ok") o.Seg(f + new Vector2(-9, 16), f + new Vector2(9, 16), 3, ink);
                else
                {
                    o.Arc(f + new Vector2(0, 30), 12, 1.25f * Mathf.PI, 1.75f * Mathf.PI, 3, ink, 12);
                    var drop = f + new Vector2(32, -22 + (now / 25 % 20));
                    o.Circle(drop, 5, new Color(.55f, .78f, .95f, .9f), 14);
                    o.Fill(new List<Vector2> { drop + new Vector2(-4.6f, -2), drop + new Vector2(0, -11), drop + new Vector2(4.6f, -2) }, new Color(.55f, .78f, .95f, .9f));
                }
            }
            foreach (var h in hearts) o.Fill(DecorArt.HeartPoly(.8f).Select(p => p + h.p).ToList(), new Color(1, .48f, .61f, Mathf.Clamp01(h.l)));
        }

        public override void CanvasGUI(GameScreen ui)
        {
            var r = R; if (r == null) return;
            var brown = Hex("#8a5a3a");
            bool gm = ss.IsGarment; var d = D;
            ui.CanvasLabel(new Vector2(24, 24), r.free ? (gm ? ss.g.name : d.name) : $"給 {ss.order.who} 的{(gm ? "衣服" : "娃娃")}", 15, brown, TextAnchor.UpperLeft, true);
            string kind = d.IsHuman ? "人形娃娃・" + d.BodyClass() : d.body.name;
            ui.CanvasLabel(new Vector2(24, 46), gm ? $"{ss.g.T.name}・{ss.g.cls}・{ss.g.fab.mat.name}" : $"{kind}・{d.FabricCount()} 種布・{Evaluator.SoftLabel(d.OverallSoft())}", 13, Hex("#9a8778"), TextAnchor.UpperLeft);
            if (r.free)
            {
                ui.CanvasLabel(new Vector2(640, 200), "完成！", 22, brown, TextAnchor.MiddleCenter, true);
                ui.CanvasLabel(new Vector2(640, 232), gm ? $"收進衣櫃（{ss.g.cls}）" : "收進展示櫃", 15, brown, TextAnchor.MiddleCenter);
                return;
            }
            ui.CanvasLabel(new Vector2(645, 130), new string('★', r.stars) + new string('☆', 5 - r.stars), 30, Hex("#e0a020"), TextAnchor.MiddleCenter);
            ui.CanvasBubble(new Vector2(510, 170), 270, r.line, 17);
            ui.CanvasLabel(Face + new Vector2(0, 96), ss.order.who, 14, Ink, TextAnchor.MiddleCenter, true, true);
        }

        public override void Panel(GameScreen ui)
        {
            var r = R; var green = Hex("#6aa88a");
            if (r.free)
            {
                ui.H2("完成！");
                ui.Body(ss.IsGarment ? "這件衣服收進衣櫃了，可以拿給同身形的娃娃穿。" : "這隻娃娃收進展示櫃了，可以到衣櫃幫它換衣服。");
                ui.Bar("手工品質", r.craft, green, $"{Mathf.RoundToInt(r.craft * 100)}%");
                ui.Body($"材料費 -${r.cost}（自由創作用自己的錢）");
                foreach (var c in r.comments) ui.Small("・" + c);
                if (ui.MainBtn("回到工作室 →")) ui.NextCustomer();
                if (ui.Btn("🧺 去衣櫃")) ui.OpenCloset();
                return;
            }
            ui.H2("交件");
            ui.Small($"交件後才知道：{ss.order.who}在意的是——");
            foreach (var (text, ok) in r.review) ui.Body($"{(ok ? "✔" : "✘")} {text}");
            ui.Bar("符合期待", r.tagS, green, $"{Mathf.RoundToInt(r.tagS * 100)}%");
            ui.Bar(r.fitLabel ? "合身" : "柔軟度", r.softS, green, $"{Mathf.RoundToInt(r.softS * 100)}%");
            ui.Bar("手工品質", r.craft, green, $"{Mathf.RoundToInt(r.craft * 100)}%");
            ui.Body($"成本 ${r.cost} ／ 預算 ${ss.order.budget}　收到 ${r.pay}　利潤 {(r.profit >= 0 ? "+" : "")}${r.profit}　聲望 {(r.stars - 3 >= 0 ? "+" : "")}{r.stars - 3}");
            foreach (var c in r.comments) ui.Small("・" + c);
            if (ui.MainBtn("下一位客人 →")) ui.NextCustomer();
        }
    }
}
