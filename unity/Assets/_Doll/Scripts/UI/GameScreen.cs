using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 一張訂單的遊戲流程畫面：接單 → 設計 → 製作各步驟 → 裝飾 → 交件。
    /// 設計步驟交給 DesignScreen；還沒移植的製作小遊戲先顯示「暫代」面板，用滑桿設定結果，讓整個流程能走完。
    /// 介面暫用 IMGUI，之後換成 UI Toolkit。
    /// </summary>
    public class GameScreen : MonoBehaviour
    {
        public DollRenderer dollRenderer;
        public DesignScreen design;
        public Camera cam;

        public readonly Meta meta = new Meta();
        public Session ss;

        Vector2 scroll;
        string flash; float flashUntil;
        GUIStyle h2, h4, body, small, btn, btnMain, bubble;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            NewOrder();
        }

        public void NewOrder()
        {
            ss = new Session(meta, meta.NextOrder());
            dollRenderer.state = ss.doll;
            design.ResetUI();
            design.onDone = Advance;
            design.orderBrief = $"{ss.order.who}：「{ss.order.text}」　預算 ${ss.order.budget}";
            SyncDesign();
            scroll = Vector2.zero;
        }

        public void Advance()
        {
            if (!ss.CanAdvance(out var why)) { Flash(why); return; }
            ss.Advance();
            scroll = Vector2.zero;
            SyncDesign();
        }

        void SyncDesign()
        {
            design.enabled = ss.step == Step.Design;
            if (!design.enabled) dollRenderer.highlight = null;
            dollRenderer.gameObject.SetActive(ss.step != Step.Order); // 接單時畫面留給客人
        }

        void Flash(string t) { flash = t; flashUntil = Time.time + 2.5f; }

        void Update()
        {
            if (!design.enabled) cam.rect = new Rect(0, 0, 1 - DesignScreen.PanelWidth / Screen.width, 1);
        }

        // ---------- 介面 ----------
        void Styles()
        {
            if (h2 != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Microsoft JhengHei UI", "PingFang TC", "Noto Sans CJK TC", "Microsoft YaHei", "Arial" }, 15);
            GUI.skin.font = font;
            Color ink = new Color(.29f, .23f, .2f), muted = new Color(.55f, .48f, .42f);
            h2 = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true }; h2.normal.textColor = ink;
            h4 = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, margin = new RectOffset(4, 4, 10, 2) }; h4.normal.textColor = muted;
            body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true }; body.normal.textColor = ink;
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true }; small.normal.textColor = muted;
            btn = new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(10, 10, 7, 7), wordWrap = true };
            btnMain = new GUIStyle(btn) { fontStyle = FontStyle.Bold, fontSize = 15 };
            bubble = new GUIStyle(GUI.skin.box) { fontSize = 19, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(18, 18, 14, 14) }; bubble.normal.textColor = ink;
        }

        bool Main(string label) { var o = GUI.backgroundColor; GUI.backgroundColor = new Color(1f, .7f, .6f); bool r = GUILayout.Button(label, btnMain, GUILayout.Height(40)); GUI.backgroundColor = o; return r; }

        void OnGUI()
        {
            Styles();
            float pw = DesignScreen.PanelWidth, gx = Screen.width - pw;
            StepBar(gx);
            if (Time.time < flashUntil) GUI.Label(new Rect(16, Screen.height - 64, gx - 32, 30), flash, body);
            if (ss.step == Step.Order) DrawCustomer(gx);
            if (ss.step == Step.Deliver) DrawDeliverStage(gx);
            if (design.enabled) return; // 設計步驟的右側面板由 DesignScreen 負責

            GUILayout.BeginArea(new Rect(gx, 0, pw, Screen.height), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            OrderCard();
            GUILayout.Space(8);
            switch (ss.step)
            {
                case Step.Order: OrderPanel(); break;
                case Step.Layout: LayoutPanel(); break;
                case Step.Cut: QualityPanel("裁布", "沿著虛線剪下每一塊布片。", () => ss.cutAcc, v => ss.cutAcc = v); break;
                case Step.Sew: SewPanel(); break;
                case Step.Stuff: StuffPanel(); break;
                case Step.Cast: CastPanel(); break;
                case Step.Trim: HardPanel("trim", "修毛邊", "沿著邊緣削掉毛邊，刀子不要切進娃娃。"); break;
                case Step.Saw: HardPanel("saw", "鋸外形", "沿著鉛筆線慢慢鋸，太快鋸子會卡住。"); break;
                case Step.Carve: HardPanel("carve", "雕刻", "刮掉外圍多餘的木頭，不要鑿進本體。"); break;
                case Step.Sand: HardPanel("sand", "砂磨", "先粗砂紙磨平，再細砂紙磨光。"); break;
                case Step.Paint: PaintPanel(); break;
                case Step.Decor: DecorPanel(); break;
                case Step.Deliver: DeliverPanel(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void StepBar(float gx)
        {
            var steps = ss.Steps(); int cur = steps.IndexOf(ss.step);
            string bar = string.Join("　", steps.Select((s, i) => i < cur ? "✓" + Session.StepName(s) : i == cur ? $"【{Session.StepName(s)}】" : Session.StepName(s)));
            GUI.Label(new Rect(16, 8, gx - 32, 26), bar, small);
        }

        void OrderCard()
        {
            var o = ss.order;
            GUILayout.Label($"{o.who}", h2);
            GUILayout.Label($"「{o.text}」", small);
            GUILayout.Label($"預算 ${o.budget}", body);
            GUILayout.Label("帳本", h4);
            int dc = ss.DecorCost(), c = ss.Cost();
            GUILayout.Label($"{(ss.doll.IsHard ? "材料" : "布料")}：{(ss.fabricCost > 0 ? "$" + ss.fabricCost : "（排版後計價）")}　配件：${dc}", small);
            var old = GUI.contentColor; if (c > o.budget) GUI.contentColor = new Color(.82f, .33f, .29f);
            GUILayout.Label($"合計 ${c} ／ 預算 ${o.budget}", small);
            GUI.contentColor = old;
            GUILayout.Label($"💰 ${meta.coins}　⭐ 聲望 {meta.rep}　🧸 {meta.done} 件", small);
        }

        void DrawCustomer(float gx)
        {
            var o = ss.order;
            float w = Mathf.Min(560, gx - 80);
            GUI.Box(new Rect(40, 70, 120, 120), o.who.Substring(0, 1), new GUIStyle(GUI.skin.box) { fontSize = 54, alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(40, 196, 120, 26), o.who, new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            float h = bubble.CalcHeight(new GUIContent(o.text), w);
            GUI.Box(new Rect(180, 80, w, h), o.text, bubble);
            GUI.Label(new Rect(196, 90 + h, w, 26), $"預算：${o.budget}", new GUIStyle(body) { fontStyle = FontStyle.Bold });
        }

        void OrderPanel()
        {
            GUILayout.Label(meta.tutDone ? "有客人上門了。仔細讀客人說的話——你要自己判斷對方想要什麼樣的娃娃。" : "第一位客人！讀完需求後接下委託，開始設計第一隻娃娃。", body);
            if (Main("接下委託 →")) Advance();
            if (meta.tutDone && GUILayout.Button("換一位客人", btn)) NewOrder();
        }

        void Placeholder(string what) => GUILayout.Label($"（「{what}」的小遊戲還在移植，先用滑桿設定結果，讓流程可以走完。）", small);

        float Slider(string label, float v, float min, float max, string valueText = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, body, GUILayout.Width(110));
            v = GUILayout.HorizontalSlider(v, min, max);
            GUILayout.Label(valueText ?? $"{Mathf.RoundToInt(v * 100)}%", small, GUILayout.Width(70));
            GUILayout.EndHorizontal();
            return v;
        }

        void LayoutPanel()
        {
            GUILayout.Label("排版", h2);
            Placeholder("排版");
            GUILayout.Label($"依布片面積估算（排得普通）：需要 ${ss.fabricCost} 的布。", body);
            GUILayout.Label("移植後會由玩家自己排：越省布越便宜，還要注意布紋方向和污漬。", small);
            if (Main($"確定買布 ${ss.fabricCost} →")) Advance();
        }

        void QualityPanel(string name, string desc, System.Func<float> get, System.Action<float> set)
        {
            GUILayout.Label(name, h2); GUILayout.Label(desc, body); Placeholder(name);
            set(Slider("精準度", get(), 0, 1));
            if (Main("完成這一步 →")) Advance();
        }

        void SewPanel()
        {
            GUILayout.Label("車縫", h2); GUILayout.Label("放珠針固定，沿著虛線車，開頭和結尾回針。", body); Placeholder("車縫");
            ss.sewAcc = Slider("車縫品質", ss.sewAcc, 0, 1);
            ss.backOk = GUILayout.Toggle(ss.backOk, " 開頭和結尾都有回針", body);
            if (Main("完成這一步 →")) Advance();
        }

        void StuffPanel()
        {
            GUILayout.Label("塞棉花", h2); GUILayout.Label("末端要先塞小團，身體再塞；來回揉可以揉平凹凸。", body); Placeholder("塞棉花");
            ss.bodyFill = Slider("身體", ss.bodyFill, 0, 1.35f, Evaluator.SoftLabel(ss.bodyFill));
            if (Evaluator.HasExtremities(ss.doll)) ss.partFill = Slider("手腳／耳朵", ss.partFill, 0, 1.35f, Evaluator.SoftLabel(ss.partFill));
            ss.lump = Slider("凹凸程度", ss.lump, 0, 1);
            GUILayout.Label($"整體手感：{Evaluator.SoftLabel(Evaluator.OverallSoft(ss))}", body);
            if (Main("塞好了，縫合開口 →")) Advance();
        }

        void CastPanel()
        {
            GUILayout.Label("灌模", h2); GUILayout.Label("灌膠到虛線、看準時機出爐、倒出多餘的膠決定外殼厚度。", body); Placeholder("灌模");
            ss.vinylThickness = Slider("外殼厚度", ss.vinylThickness, 0, 1, ss.vinylThickness > .66f ? "厚（硬）" : ss.vinylThickness > .33f ? "中等" : "薄（軟）");
            ss.bodyFill = ss.partFill = .4f + .65f * ss.vinylThickness;
            ss.hardAccs["cast"] = Slider("灌模品質", ss.hardAccs.TryGetValue("cast", out var a) ? a : .8f, 0, 1);
            if (Main("脫模，下一步 →")) Advance();
        }

        void HardPanel(string key, string name, string desc)
        {
            GUILayout.Label(name, h2); GUILayout.Label(desc, body); Placeholder(name);
            ss.hardAccs[key] = Slider("品質", ss.hardAccs.TryGetValue(key, out var a) ? a : .8f, 0, 1);
            if (Main("完成這一步 →")) Advance();
        }

        void PaintPanel()
        {
            GUILayout.Label("上色", h2); GUILayout.Label("在娃娃身上自由手繪、噴漆，木頭可以上亮光漆。", body); Placeholder("上色");
            if (Main("先跳過上色 →")) Advance();
        }

        void DecorPanel()
        {
            GUILayout.Label("裝飾", h2);
            GUILayout.Label("（配件擺放與刺繡五官的繪製在 M4 移植；目前先選要加哪些配件，會計入標籤和花費。）", small);
            GUILayout.Label("點一下加入：", h4);
            var list = DB.Decor;
            for (int i = 0; i < list.Count; i += 3)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(list.Count, i + 3); j++)
                    if (GUILayout.Button($"{list[j].name}  ${list[j].cost}", btn)) ss.decor.Add(list[j].id);
                GUILayout.EndHorizontal();
            }
            if (ss.decor.Count > 0)
            {
                GUILayout.Label("已加入（點一下移除）：", h4);
                for (int i = 0; i < ss.decor.Count; i++)
                    if (GUILayout.Button("✕ " + DB.DecorById(ss.decor[i]).name, btn)) { ss.decor.RemoveAt(i); break; }
            }
            if (Main($"完成，交給{ss.order.who} →")) Advance();
        }

        void DrawDeliverStage(float gx)
        {
            var r = ss.result; if (r == null) return;
            GUI.Label(new Rect(gx - 300, 60, 280, 40), new string('★', r.stars) + new string('☆', 5 - r.stars), new GUIStyle(h2) { fontSize = 30, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(.88f, .63f, .13f) } });
            float h = bubble.CalcHeight(new GUIContent(r.line), 280);
            GUI.Box(new Rect(gx - 300, 110, 280, h), r.line, bubble);
        }

        void DeliverPanel()
        {
            var r = ss.result;
            GUILayout.Label("交件", h2);
            GUILayout.Label($"交件後才知道：{ss.order.who}在意的是——", small);
            foreach (var (text, ok) in r.review) GUILayout.Label($"{(ok ? "✔" : "✘")} {text}", body);
            GUILayout.Label($"符合期待 {Mathf.RoundToInt(r.tagS * 100)}%　柔軟度 {Mathf.RoundToInt(r.softS * 100)}%　手工 {Mathf.RoundToInt(r.craft * 100)}%", small);
            GUILayout.Label($"成本 ${r.cost} ／ 預算 ${ss.order.budget}　收到 ${r.pay}　利潤 {(r.profit >= 0 ? "+" : "")}${r.profit}　聲望 {(r.stars - 3 >= 0 ? "+" : "")}{r.stars - 3}", body);
            foreach (var c in r.comments) GUILayout.Label("・" + c, small);
            if (Main("下一位客人 →")) NewOrder();
        }
    }
}
