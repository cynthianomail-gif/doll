using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 盲盒系列：把自由創作的娃娃（2–7 款）組成系列，設定稀有度（隱藏款只能一款）、系列名稱、盒子顏色和圖案，然後開盒。
    /// 畫面左邊是盒子，右邊是系列一覽（隱藏款畫成剪影）。（網頁版 seriesPanel / renderSeries）
    /// </summary>
    public class SeriesScreen : CraftGame
    {
        readonly Meta meta;
        public SeriesScreen(Session ss) : base(ss) { meta = ss.meta; }
        BlindSeries Se => meta.CurSeries;

        public BlindSeries NewSeries()
        {
            var s = new BlindSeries { name = $"我的系列 {meta.series.Count + 1}" };
            meta.series.Add(s); meta.seriesSel = meta.series.Count - 1;
            return s;
        }

        public override string Hint => Se == null ? "🎁 按右邊的「＋ 新系列」建立第一個盲盒系列" : null;
        public override bool Done => true;
        public override bool ShowDoll => false;

        static Vector2 Slot(int i) => new Vector2(300 + (i % 4) * 118 + 59, 40 + (i / 4) * 200 + 82);

        public override IEnumerable<(DollState doll, Vector2 center, float scale, bool silhouette)> Gallery()
        {
            if (Se == null) yield break;
            for (int i = 0; i < Se.dolls.Count; i++) yield return (Se.dolls[i].doll, Slot(i), .3f, Se.dolls[i].rarity == "secret");
        }

        public override void Draw(Sketch under, Sketch over)
        {
            if (meta.chroma) under.P.Rect(-600, -400, 2000, 1400, Hex("#00b140"));
            if (Se == null) return;
            BoxArt.Draw(under, 60, 210, 180, 210, 55, Se.box);
            var g = under.P;
            for (int i = 0; i < Se.dolls.Count; i++)
            {
                var p = Slot(i); var r = Se.dolls[i].rarity;
                g.RoundRect(p.x - 54, p.y - 78, 108, 188, 12, r == "secret" ? new Color(.31f, .24f, .39f, .12f) : r == "rare" ? new Color(1, .84f, .29f, .18f) : new Color(1, 1, 1, .6f));
            }
        }

        public override void CanvasGUI(GameScreen ui)
        {
            if (Se == null) return;
            BoxArt.Labels(ui, 60, 210, 180, 210, Se.box, Se.name);
            ui.CanvasLabel(new Vector2(40, 40), Se.name, 22, Ink, TextAnchor.UpperLeft, true);
            ui.CanvasLabel(new Vector2(40, 72), $"盲盒系列・共 {Se.dolls.Count} 款{(Se.dolls.Any(e => e.rarity == "secret") ? "（含隱藏款）" : "")}・開過 {Se.opened} 盒", 13, Hex("#9a8778"), TextAnchor.UpperLeft);
            if (Se.dolls.Count == 0) ui.CanvasLabel(new Vector2(540, 260), "從右邊選娃娃放進系列（2–7 款）", 15, Hex("#9a8778"), TextAnchor.MiddleCenter);
            for (int i = 0; i < Se.dolls.Count; i++)
            {
                var e = Se.dolls[i]; var p = Slot(i);
                ui.CanvasLabel(p + new Vector2(0, 70), e.rarity == "secret" ? "???" : e.doll.name, 13, Ink, TextAnchor.UpperCenter, true);
                ui.CanvasLabel(p + new Vector2(0, 88), $"{Rarity.Of(e.rarity).name}・{Mathf.RoundToInt(Se.Odds(e) * 100)}%", 12,
                    e.rarity == "rare" ? Hex("#c98a00") : e.rarity == "secret" ? Hex("#7a5aa0") : Hex("#9a8778"), TextAnchor.UpperCenter);
                if (e.rarity == "secret") ui.CanvasLabel(p, "?", 60, Color.white, TextAnchor.MiddleCenter, true);
            }
        }

        public override void Panel(GameScreen ui)
        {
            ui.H2("🎁 盲盒系列");
            ui.Small("我的系列");
            for (int i = 0; i < meta.series.Count; i++)
            {
                var s = meta.series[i];
                if (ui.Chip($"{s.name}　{s.dolls.Count} 款・開過 {s.opened} 盒", i == meta.seriesSel)) meta.seriesSel = i;
            }
            if (ui.Btn("＋ 新系列")) NewSeries();
            var se = Se;
            if (se != null)
            {
                ui.Small("系列名稱");
                var n = GUILayout.TextField(se.name, 12); if (n != se.name) se.name = n.Trim() == "" ? "未命名系列" : n;
                ui.Small($"放進系列的娃娃（2–{BlindSeries.Max} 款，點一下放進／拿出）");
                if (meta.dolls.Count == 0) ui.Body("還沒有娃娃。回工作室用「自由創作：做娃娃」做幾隻，就能放進系列。");
                foreach (var d in meta.dolls)
                {
                    var e = se.dolls.FirstOrDefault(x => x.doll == d);
                    if (ui.Chip($"{d.name}　{(e != null ? Rarity.Of(e.rarity).name + "・" + Mathf.RoundToInt(se.Odds(e) * 100) + "%" : "沒放進系列")}", e != null))
                    { var why = se.Toggle(d); if (why != null) Flash(why); }
                }
                if (se.dolls.Count > 0)
                {
                    ui.Small("稀有度（隱藏款只能有一款）");
                    foreach (var e in se.dolls)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(e.doll.name, GUILayout.Width(70));
                        foreach (var r in Rarity.All) if (ui.Chip(r.name, e.rarity == r.id)) se.SetRarity(e, r.id);
                        GUILayout.EndHorizontal();
                        ui.Small($"　抽中機率 {Mathf.RoundToInt(se.Odds(e) * 100)}%・已開到 {(se.owned.TryGetValue(e.doll, out var c) ? c : 0)} 隻");
                    }
                }
                ui.Small("盒子顏色");
                for (int i = 0; i < DB.Colors.Count; i += 4)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < Mathf.Min(DB.Colors.Count, i + 4); j++) if (ui.Chip(DB.Colors[j].name, se.box.color == DB.Colors[j].id, DB.Colors[j].color)) se.box.color = DB.Colors[j].id;
                    GUILayout.EndHorizontal();
                }
                ui.Small("圖案");
                GUILayout.BeginHorizontal();
                foreach (var (id, name) in BoxStyle.Patterns) if (ui.Chip(name, se.box.pattern == id)) se.box.pattern = id;
                GUILayout.EndHorizontal();
                ui.Small("圖案顏色");
                var P = PaintGame.Paints;
                for (int i = 0; i < P.Count; i += 4)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < Mathf.Min(P.Count, i + 4); j++) if (ui.Chip(P[j].name, se.box.pcolor == P[j].id, P[j].color)) se.box.pcolor = P[j].id;
                    GUILayout.EndHorizontal();
                }
                if (ui.MainBtn($"🎁 開一盒（${BlindSeries.Price}）", se.CanOpen && meta.coins >= BlindSeries.Price)) ui.OpenUnbox(se);
                if (!se.CanOpen) ui.Small("至少要放 2 款娃娃才能開盒。");
                else if (meta.coins < BlindSeries.Price) ui.Small("錢不夠開盒，先接幾張訂單吧。");
                if (ui.Btn("📺 直播模式")) ui.ToggleStream();
            }
            if (Flashing) ui.Body(flash);
            if (ui.Btn("← 回到工作室")) ui.CloseOverlay();
        }
    }

    /// <summary>
    /// 開箱演出：盒子晃動 → 按住盒蓋往上拖撕開 → 鋁箔袋升起 → 左右拖撕開袋子 → 懸疑（稀有度越高越久）→ 光芒中揭曉 → 收集卡。
    /// 隱藏款有彩虹光芒和閃爍（可以在直播列打開「減少閃爍與晃動」）。（網頁版 openBox / unboxUpdate / renderUnbox）
    /// </summary>
    public class UnboxStage : CraftGame
    {
        readonly Meta meta;
        public readonly BlindSeries se;
        public DollState doll;
        public string rar, phase;
        public float t, tear, bagTear;
        public bool streamMode;
        class Bit { public Vector2 p, v; public Color c; public float r, l; }
        readonly List<Bit> confetti = new List<Bit>();
        const float BX = SM.CX - 110, BY = 280, BW = 220, BH = 200, BD = 70;

        public UnboxStage(Session ss, BlindSeries se) : base(ss) { meta = ss.meta; this.se = se; }

        /// <summary>付 $10 開一盒。回傳 null 表示成功，否則是原因。</summary>
        public string Open(float? roll = null)
        {
            if (!se.CanOpen) return "至少要放 2 款娃娃才能開盒";
            if (meta.coins < BlindSeries.Price) return "錢不夠開盒了";
            meta.coins -= BlindSeries.Price; se.opened++;
            var e = se.Pick(roll ?? Random.value);
            doll = e.doll; rar = e.rarity; phase = "shake"; t = 0; tear = 0; bagTear = 0; confetti.Clear();
            return null;
        }

        bool RM => meta.reduceMotion;
        bool Reveal => phase == "reveal" || phase == "card";
        float Suspense => rar == "secret" ? 1.8f : rar == "rare" ? 1.1f : .7f;

        public override string Hint => null;
        public override bool Done => phase == "card";
        public override bool ShowDoll => false;

        public override void Tick(float dt)
        {
            t += dt;
            if (phase == "shake" && t > .9f) { phase = "lid"; t = 0; }
            if (phase == "lidfly" && t > .5f) { phase = "bagrise"; t = 0; }
            if (phase == "bagrise" && t > .6f) { phase = "bag"; t = 0; }
            if (phase == "suspense" && t > Suspense)
            {
                phase = "reveal"; t = 0;
                var cols = (rar == "secret" ? new[] { "#ff7b9c", "#ffd54a", "#7ed6a0", "#7fb6ff", "#c59bff" } : rar == "rare" ? new[] { "#ffd54a", "#ffe9a8", "#f6b84a" } : new[] { "#f4b6c6", "#f6d96b", "#a9d3ee", "#a6dcc0" }).Select(Hex).ToArray();
                int n = Mathf.RoundToInt((rar == "secret" ? 160 : 90) * (RM ? .35f : 1));
                for (int i = 0; i < n; i++) confetti.Add(new Bit { p = new Vector2(SM.CX + (Random.value - .5f) * 60, 200), v = new Vector2((Random.value - .5f) * 12, -Random.value * 10 - 3), c = cols[i % cols.Length], r = Random.value * Mathf.PI, l = 1 });
            }
            if (phase == "reveal" && t > 1.8f) { phase = "card"; t = 0; se.owned[doll] = (se.owned.TryGetValue(doll, out var c) ? c : 0) + 1; }
            float k = dt * 60;
            foreach (var p in confetti) { p.v.y += .22f * k; p.p += p.v * k; p.r += .12f * k; if (p.p.y > SM.H - 30) p.l -= dt * 2; }
            confetti.RemoveAll(p => p.l <= 0);
        }

        public override void Down(Vector2 p) { if (phase == "card" && streamMode) Open(); }

        public override void Move(Vector2 p, Vector2 prev)
        {
            if (!ptr.down) return;
            if (phase == "lid") { tear += Mathf.Max(0, prev.y - p.y) / 160 + Mathf.Abs(p.x - prev.x) / 900; if (tear >= 1) { phase = "lidfly"; t = 0; } }
            if (phase == "bag") { bagTear += Mathf.Abs(p.x - prev.x) / 260; if (bagTear >= 1) { phase = "suspense"; t = 0; } }
        }

        float RevealEase => phase == "card" ? 1 : 1 - Mathf.Pow(1 - Mathf.Min(1, t / .8f), 3);

        public override IEnumerable<(DollState doll, Vector2 center, float scale, bool silhouette)> Gallery()
        {
            if (!Reveal) yield break;
            float e = RevealEase;
            yield return (doll, new Vector2(SM.CX, 190 + (1 - e) * 120), .62f * (.4f + .6f * e), false);
        }

        float Shake(float now) => RM ? 0 : phase == "shake" ? Mathf.Sin(t * 50) * 7 * (1 - t) : phase == "lid" ? Mathf.Sin(now / 30) * tear * 3 : 0;
        bool BagShown => phase == "bagrise" || phase == "bag" || phase == "suspense";
        float BagY => BY + 30 - (1 - Mathf.Pow(1 - (phase == "bagrise" ? Mathf.Min(1, t / .6f) : 1), 3)) * 200;

        static Color Rainbow(float h, float a) { var c = Color.HSVToRGB(Mathf.Repeat(h, 360) / 360f, .7f, .95f); c.a = a; return c; }

        public override void Draw(Sketch under, Sketch over)
        {
            float now = Now; var g = under.P;
            if (meta.chroma) g.Rect(-600, -400, 2000, 1400, Hex("#00b140"));
            else
            {
                g.Rect(-600, -400, 2000, 1400, Hex("#141017"));
                bool flick = phase == "suspense" && rar == "secret" && !RM && Mathf.FloorToInt(now / 90) % 2 == 1;
                g.Blob(new Vector2(SM.CX, 240), 560, Hex(flick ? "#4a3a58" : "#3a2f40"), Hex("#141017"), 64);
            }
            if (Reveal)
            {
                float alpha = Mathf.Min(1, t * 2 + (phase == "card" ? 1 : 0)), tt = RM ? 0 : now / 1000; var c0 = new Vector2(SM.CX, 190);
                var r = Rarity.Of(rar);
                for (int i = 0; i < 18; i++)
                {
                    float a = i / 18f * Mathf.PI * 2 + tt * .4f;
                    var col = r.rainbow ? Rainbow(i * 20 + tt * 160, .28f * alpha) : new Color(r.color.r, r.color.g, r.color.b, .22f * alpha);
                    g.Fill(new List<Vector2> { c0, c0 + new Vector2(Mathf.Cos(a - .06f), Mathf.Sin(a - .06f)) * 520, c0 + new Vector2(Mathf.Cos(a + .06f), Mathf.Sin(a + .06f)) * 520 }, col);
                }
                g.Blob(c0, 170, new Color(1, 1, 1, .75f * alpha), new Color(1, 1, 1, 0), 40);
            }
            float lid = phase == "shake" ? 0 : phase == "lid" ? tear * .25f : phase == "lidfly" ? .25f + t * 3 : 2;
            BoxArt.Draw(under, BX, BY, BW, BH, BD, se.box, lid, phase != "shake" && phase != "lid", Shake(now));
            if (BagShown) BoxArt.Bag(under, SM.CX - 75, BagY, 150, 190, bagTear, now, phase == "suspense" && !RM ? Mathf.Sin(now / 25) * 4 : 0, BY + 4);
            var o = over.P;
            foreach (var p in confetti)
            {
                var c = p.c; c.a = Mathf.Clamp01(p.l);
                var q = new[] { new Vector2(-4, -2.5f), new Vector2(4, -2.5f), new Vector2(4, 2.5f), new Vector2(-4, 2.5f) }.Select(v => p.p + SM.RotV(v.x, v.y, p.r)).ToArray();
                o.Quad(q[0], q[1], q[2], q[3], c);
            }
            if (phase == "card") o.RoundRect(SM.CX + 150, 330, 230, 130, 14, new Color(1, .98f, .953f, .95f));
        }

        string Prompt => phase == "lid" ? "⬆ 按住盒蓋往上拖，撕開盒子" : phase == "bag" ? "⬅➡ 按住左右拖，撕開鋁箔袋" : null;

        public override void CanvasGUI(GameScreen ui)
        {
            float now = Now;
            if (phase == "shake" || phase == "lid") BoxArt.Labels(ui, BX, BY, BW, BH, se.box, se.name, Shake(now));
            else ui.CanvasLabel(new Vector2(BX + BW / 2, BY + BH - 14), "BLIND BOX・盲盒", 11, new Color(1, 1, 1, .95f), TextAnchor.MiddleCenter, true);
            if (BagShown && BagY + 95 < BY) ui.CanvasLabel(new Vector2(SM.CX, BagY + 103), "?", 44, new Color(.47f, .5f, .55f, .55f), TextAnchor.MiddleCenter, true);
            if (Reveal)
            {
                float e = RevealEase;
                var col = rar == "secret" ? Rainbow(now / 6, e) : rar == "rare" ? new Color(1, .835f, .29f, e) : new Color(1, 1, 1, e);
                ui.CanvasLabel(new Vector2(SM.CX, 52), rar == "secret" ? "✨ 隱藏款！✨" : rar == "rare" ? "★ 稀有 ★" : "普通款", rar == "secret" ? 30 : 24, col, TextAnchor.MiddleCenter, true);
            }
            if (phase == "card")
            {
                float x = SM.CX + 150 + 16, y = 330 + 16; int n = se.owned.TryGetValue(doll, out var c) ? c : 0;
                ui.CanvasLabel(new Vector2(x, y), doll.name, 20, Ink, TextAnchor.UpperLeft, true);
                ui.CanvasLabel(new Vector2(x, y + 32), $"{Rarity.Of(rar).name}・{se.name}", 14, Hex("#8a5a3a"), TextAnchor.UpperLeft);
                ui.CanvasLabel(new Vector2(x, y + 56), $"收集 {se.Collected} / {se.dolls.Count}{(n > 1 ? "・重複了！" : "・新的！")}", 14, Hex("#8a5a3a"), TextAnchor.UpperLeft);
                ui.CanvasLabel(new Vector2(x, y + 84), streamMode ? "點畫面再開一盒" : "用右邊的按鈕再開一盒", 12, Hex("#9a8778"), TextAnchor.UpperLeft);
            }
            if (Prompt != null) ui.CanvasLabel(new Vector2(SM.CX, SM.H - 28), Prompt, 17, Color.white, TextAnchor.MiddleCenter, true);
            if (Flashing) ui.CanvasLabel(new Vector2(SM.CX, SM.H - 56), flash, 16, Bad, TextAnchor.MiddleCenter, true, true);
        }

        public override void Panel(GameScreen ui)
        {
            ui.H2("🎁 開箱");
            ui.Small(se.name);
            if (phase != "card") { ui.Body(Prompt ?? "開箱中……"); return; }
            if (ui.MainBtn($"🎁 再開一盒（${BlindSeries.Price}）", meta.coins >= BlindSeries.Price)) { var why = Open(); if (why != null) Flash(why); }
            ui.Small($"收集進度：{se.Collected} / {se.dolls.Count} 款　💰 ${meta.coins}");
            if (ui.Btn("← 回到系列")) ui.OpenSeries();
            if (ui.Btn("📺 直播模式")) ui.ToggleStream();
        }
    }
}
