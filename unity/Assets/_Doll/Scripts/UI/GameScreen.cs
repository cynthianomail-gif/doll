using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DollWorkshop
{
    /// <summary>
    /// 一張訂單的遊戲流程畫面：接單 → 設計 → 製作各步驟 → 裝飾 → 交件。
    /// 設計步驟交給 DesignScreen；布偶的排版、裁布、車縫、塞棉花由小遊戲（CraftGame）處理，
    /// 這裡負責把滑鼠／手指的位置轉成畫布座標交給小遊戲、每格請它畫圖，以及右側面板。
    /// 還沒移植的步驟（硬材質、上色）先用「暫代」面板。介面暫用 IMGUI，之後換成 UI Toolkit。
    /// </summary>
    public class GameScreen : MonoBehaviour
    {
        public DollRenderer dollRenderer;
        public DesignScreen design;
        public Camera cam;

        public readonly Meta meta = new Meta();
        public Session ss;

        Sketch under, over;
        readonly Ptr ptr = new Ptr();
        float lastMove;

        Vector2 scroll;
        string flash; float flashUntil;
        GUIStyle h2, h4, body, small, btn, btnMain, bubble, lab, labBold, pill, tip;
        Texture2D white;

        static readonly Dictionary<Step, string> Tips = new Dictionary<Step, string>
        {
            { Step.Order, "客人會用自己的話說需求。仔細讀，自己判斷對方想要什麼樣的娃娃——交件後才會知道客人真正在意的是什麼。下面的帳本會記錄花費。" },
            { Step.Layout, "把布片拖到布上排好，越省布越便宜。布片上的箭頭要和布上的箭頭同方向，也要避開污漬。選取布片後按 R 或右鍵可以旋轉。" },
            { Step.Cut, "按住滑鼠，沿著虛線剪。剪歪的地方會留下紅線，影響手工分數。" },
            { Step.Sew, "先在虛線上點幾下放珠針（橘色段落代表還沒固定）。開始車縫後：按住滑鼠沿虛線走，車速不要超過右邊的紅線；珠針靠近時按空白鍵拔掉；開頭和結尾按住 B 回針。覺得太難可以打開「車縫輔助」。" },
            { Step.Stuff, "在棉花袋上按住，拉出棉花（按越久越大團），拖到要塞的部位放開。耳朵、手腳只能塞小團，而且要在身體塞滿之前先塞。在娃娃上來回揉，可以把凹凸揉平。" },
            { Step.Deliver, "交件了！看看客人在意的是什麼，下一位客人的話可以讀得更仔細。" },
        };

        void Awake()
        {
            under = Sketch.Create("SketchUnder", -2000);
            over = Sketch.Create("SketchOver", 2000);
        }

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
            ptr.down = false;
            SyncDesign();
        }

        void SyncDesign()
        {
            design.enabled = ss.step == Step.Design;
            if (!design.enabled) dollRenderer.highlight = null;
        }

        void Flash(string t) { flash = t; flashUntil = Time.time + 2.5f; }

        // ---------- 每格：輸入、小遊戲、繪圖 ----------
        void Update()
        {
            cam.orthographicSize = Mathf.Max(2.8f, 4.0f / Mathf.Max(.1f, cam.aspect)); // 整張 800×560 的畫布都要看得到
            if (!design.enabled) cam.rect = new Rect(0, 0, 1 - DesignScreen.PanelWidth / Screen.width, 1);
            var craft = ss.craft;
            dollRenderer.gameObject.SetActive(ss.step != Step.Order && (craft == null || craft.ShowDoll));
            if (craft != null)
            {
                craft.ptr = ptr;
                HandlePointer(craft);
                craft.Tick(Time.deltaTime);
            }
            Cursor.visible = !(craft != null && craft.HideCursor && ptr.inside);
            under.Begin(); over.Begin();
            craft?.Draw(under, over);
            under.End(); over.End();
        }

        void HandlePointer(CraftGame craft)
        {
            var pointer = Pointer.current; if (pointer == null) return;
            var mouse = Mouse.current;
            Vector2 sp = pointer.position.ReadValue();
            bool inGame = sp.x >= 0 && sp.x < Screen.width - DesignScreen.PanelWidth && sp.y >= 0 && sp.y < Screen.height;
            Vector2 p = DollRenderer.Px(cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, 10)));
            var prev = ptr.p; float d = Vector2.Distance(p, prev);
            // 速度（像素／毫秒）：和網頁版一樣平滑；停下來後逐漸歸零
            if (d > .01f) { ptr.speed = ptr.speed * .55f + d / Mathf.Max(1, Time.unscaledDeltaTime * 1000) * .45f; lastMove = Time.unscaledTime; }
            else if (Time.unscaledTime - lastMove > .06f) ptr.speed *= .8f;
            ptr.inside = inGame; ptr.p = p;

            bool leftPress = pointer.press.wasPressedThisFrame, rightPress = mouse != null && mouse.rightButton.wasPressedThisFrame;
            if ((leftPress || rightPress) && inGame)
            {
                ptr.down = true; ptr.right = rightPress && !leftPress; ptr.speed = 0;
                craft.Down(p);
            }
            else if (ptr.down)
            {
                if (d > 0)
                {
                    int steps = Mathf.Max(1, Mathf.CeilToInt(d / 3));
                    for (int i = 1; i <= steps; i++) craft.Drag(Vector2.Lerp(prev, p, i / (float)steps));
                    craft.Move(p, prev);
                }
                bool held = pointer.press.isPressed || (mouse != null && mouse.rightButton.isPressed);
                if (!held) { ptr.down = false; craft.Up(p); }
            }
        }

        void OnDisable() { Cursor.visible = true; }

        // ---------- 介面元件（小遊戲的面板也用這些） ----------
        void Styles()
        {
            if (h2 != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Microsoft JhengHei UI", "PingFang TC", "Noto Sans CJK TC", "Microsoft YaHei", "Segoe UI Symbol", "Segoe UI Emoji", "Arial" }, 15);
            GUI.skin.font = font;
            Color ink = new Color(.29f, .23f, .2f), muted = new Color(.55f, .48f, .42f);
            white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply();
            h2 = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true }; h2.normal.textColor = ink;
            h4 = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, margin = new RectOffset(4, 4, 10, 2) }; h4.normal.textColor = muted;
            body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true }; body.normal.textColor = ink;
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true }; small.normal.textColor = muted;
            btn = new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(10, 10, 7, 7), wordWrap = true };
            btnMain = new GUIStyle(btn) { fontStyle = FontStyle.Bold, fontSize = 15 };
            bubble = new GUIStyle(GUI.skin.box) { fontSize = 19, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(18, 18, 14, 14) }; bubble.normal.textColor = ink;
            lab = new GUIStyle(GUI.skin.label) { wordWrap = false, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            labBold = new GUIStyle(lab) { fontStyle = FontStyle.Bold };
            var pillTex = new Texture2D(1, 1); pillTex.SetPixel(0, 0, new Color(1, 1, 1, .9f)); pillTex.Apply();
            pill = new GUIStyle(GUI.skin.label); pill.normal.background = pillTex;
            var tipTex = new Texture2D(1, 1); tipTex.SetPixel(0, 0, new Color(1f, .95f, .82f, 1)); tipTex.Apply();
            tip = new GUIStyle(GUI.skin.box) { fontSize = 13, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) }; tip.normal.background = tipTex; tip.normal.textColor = ink;
        }

        public void H2(string t) => GUILayout.Label(t, h2);
        public void Body(string t) => GUILayout.Label(t, body);
        public void Small(string t) => GUILayout.Label(t, small);

        public bool MainBtn(string label, bool enabled = true)
        {
            bool e = GUI.enabled; GUI.enabled = e && enabled;
            var o = GUI.backgroundColor; GUI.backgroundColor = new Color(1f, .7f, .6f);
            bool r = GUILayout.Button(label, btnMain, GUILayout.Height(40));
            GUI.backgroundColor = o; GUI.enabled = e; return r && enabled;
        }

        public bool Btn(string label, bool enabled = true)
        {
            bool e = GUI.enabled; GUI.enabled = e && enabled;
            bool r = GUILayout.Button(label, btn);
            GUI.enabled = e; return r && enabled;
        }

        /// <summary>按住期間每格都回傳 true（回針用）。</summary>
        public bool HoldBtn(string label) => GUILayout.RepeatButton(label, btn);

        public bool Chip(string label, bool on, Color? swatch = null)
        {
            var o = GUI.backgroundColor; if (on) GUI.backgroundColor = new Color(1f, .78f, .7f);
            bool r = GUILayout.Button((swatch != null ? "　 " : "") + label, new GUIStyle(btn) { alignment = TextAnchor.MiddleLeft });
            GUI.backgroundColor = o;
            if (swatch != null && Event.current.type == EventType.Repaint)
            {
                var rr = GUILayoutUtility.GetLastRect(); var c = GUI.color; GUI.color = swatch.Value;
                GUI.DrawTexture(new Rect(rr.x + 9, rr.y + (rr.height - 14) / 2, 14, 14), white); GUI.color = c;
            }
            return r;
        }

        public void Bar(string name, float v, Color col, string right)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, body, GUILayout.Width(96));
            var r = GUILayoutUtility.GetRect(60, 22, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                var c = GUI.color; var bar = new Rect(r.x, r.y + 8, r.width, 8);
                GUI.color = new Color(.94f, .9f, .85f); GUI.DrawTexture(bar, white);
                GUI.color = col; GUI.DrawTexture(new Rect(bar.x, bar.y, Mathf.Max(1, bar.width * Mathf.Clamp01(v)), bar.height), white);
                GUI.color = c;
            }
            GUILayout.Label(right, small, GUILayout.Width(110));
            GUILayout.EndHorizontal();
        }

        /// <summary>在畫布座標 p 寫字（會跟著畫面縮放）。anchor 決定 p 是文字的哪一點。</summary>
        public void CanvasLabel(Vector2 p, string text, int size, Color col, TextAnchor anchor, bool bold = false, bool bg = false, float rotDeg = 0)
        {
            var sc = cam.WorldToScreenPoint(DollRenderer.W(p));
            float k = cam.pixelHeight / (cam.orthographicSize * 200f);
            var st = bold ? labBold : lab; st.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * k)); st.normal.textColor = col;
            var gc = new GUIContent(text); var sz = st.CalcSize(gc);
            float x = sc.x, y = Screen.height - sc.y;
            int cx = (int)anchor % 3, cy = (int)anchor / 3;
            var r = new Rect(x - (cx == 0 ? 0 : cx == 1 ? sz.x / 2 : sz.x), y - (cy == 0 ? 0 : cy == 1 ? sz.y / 2 : sz.y), sz.x, sz.y);
            if (bg) GUI.Box(new Rect(r.x - 10 * k, r.y - 4 * k, r.width + 20 * k, r.height + 8 * k), GUIContent.none, pill);
            if (rotDeg != 0) { var m = GUI.matrix; GUIUtility.RotateAroundPivot(rotDeg, new Vector2(x, y)); GUI.Label(r, gc, st); GUI.matrix = m; }
            else GUI.Label(r, gc, st);
        }

        void Hint(string text) => CanvasLabel(new Vector2(SM.W / 2, 30), text, 16, new Color(.29f, .23f, .2f), TextAnchor.MiddleCenter, true, true);

        // ---------- 畫面 ----------
        void OnGUI()
        {
            Styles();
            float pw = DesignScreen.PanelWidth, gx = Screen.width - pw;
            StepBar(gx);
            var craft = ss.craft;
            if (craft != null) { craft.CanvasGUI(this); Hint(craft.Hint); }
            if (Time.time < flashUntil) GUI.Label(new Rect(16, Screen.height - 64, gx - 32, 30), flash, body);
            if (ss.step == Step.Order) DrawCustomer(gx);
            if (ss.step == Step.Deliver) DrawDeliverStage(gx);
            if (design.enabled) return; // 設計步驟的右側面板由 DesignScreen 負責

            GUILayout.BeginArea(new Rect(gx, 0, pw, Screen.height), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            TipCard();
            if (craft != null) craft.Panel(this);
            else switch (ss.step)
            {
                case Step.Order: OrderPanel(); break;
                case Step.Cast: CastPanel(); break;
                case Step.Trim: HardPanel("trim", "修毛邊", "沿著邊緣削掉毛邊，刀子不要切進娃娃。"); break;
                case Step.Saw: HardPanel("saw", "鋸外形", "沿著鉛筆線慢慢鋸，太快鋸子會卡住。"); break;
                case Step.Carve: HardPanel("carve", "雕刻", "刮掉外圍多餘的木頭，不要鑿進本體。"); break;
                case Step.Sand: HardPanel("sand", "砂磨", "先粗砂紙磨平，再細砂紙磨光。"); break;
                case Step.Paint: PaintPanel(); break;
                case Step.Decor: DecorPanel(); break;
                case Step.Deliver: DeliverPanel(); break;
            }
            GUILayout.Space(12);
            OrderCard();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void TipCard()
        {
            if (!Tips.TryGetValue(ss.step, out var t) || meta.tipsSeen.Contains(ss.step)) return;
            GUILayout.Label($"💡 {(meta.tutDone ? "小提示" : "新手教學")}　{t}", tip);
            if (GUILayout.Button("知道了", btn, GUILayout.Width(90))) meta.tipsSeen.Add(ss.step);
            GUILayout.Space(6);
        }

        void StepBar(float gx)
        {
            var steps = ss.Steps(); int cur = steps.IndexOf(ss.step);
            string bar = string.Join("　", steps.Select((s, i) => i < cur ? "✓" + Session.StepName(s) : i == cur ? $"【{Session.StepName(s)}】" : Session.StepName(s)));
            GUI.Label(new Rect(16, Screen.height - 30, gx - 32, 26), bar, small);
        }

        void OrderCard()
        {
            var o = ss.order;
            GUILayout.Label("委託", h4);
            GUILayout.Label($"{o.who}：「{o.text}」", small);
            GUILayout.Label("帳本", h4);
            int dc = ss.DecorCost(), c = ss.Cost();
            GUILayout.Label($"{(ss.doll.IsHard ? "材料" : "布料")}：{(ss.fabricCost > 0 ? "$" + ss.fabricCost : ss.step == Step.Layout ? $"（排版中：${ss.layout.Cost}）" : "（排版後計價）")}　配件：${dc}", small);
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
            H2(ss.order.who);
            Body(meta.tutDone ? "有客人上門了。仔細讀客人說的話——你要自己判斷對方想要什麼樣的娃娃。" : "第一位客人！讀完需求後接下委託，開始設計第一隻娃娃。");
            if (MainBtn("接下委託 →")) Advance();
            if (meta.tutDone && Btn("換一位客人")) NewOrder();
        }

        void Placeholder(string what) => Small($"（「{what}」的小遊戲在 M6 移植，先用滑桿設定結果，讓流程可以走完。）");

        float Slider(string label, float v, float min, float max, string valueText = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, body, GUILayout.Width(110));
            v = GUILayout.HorizontalSlider(v, min, max);
            GUILayout.Label(valueText ?? $"{Mathf.RoundToInt(v * 100)}%", small, GUILayout.Width(70));
            GUILayout.EndHorizontal();
            return v;
        }

        void CastPanel()
        {
            H2("灌模"); Body("灌膠到虛線、看準時機出爐、倒出多餘的膠決定外殼厚度。"); Placeholder("灌模");
            float t = Slider("外殼厚度", ss.vinylThickness, 0, 1, ss.vinylThickness > .66f ? "厚（硬）" : ss.vinylThickness > .33f ? "中等" : "薄（軟）");
            if (!Mathf.Approximately(t, ss.vinylThickness)) { ss.vinylThickness = t; ss.SetHardFill(.4f + .65f * t); }
            ss.hardAccs["cast"] = Slider("灌模品質", ss.hardAccs.TryGetValue("cast", out var a) ? a : .8f, 0, 1);
            if (MainBtn("脫模，下一步 →")) Advance();
        }

        void HardPanel(string key, string name, string desc)
        {
            H2(name); Body(desc); Placeholder(name);
            ss.hardAccs[key] = Slider("品質", ss.hardAccs.TryGetValue(key, out var a) ? a : .8f, 0, 1);
            if (MainBtn("完成這一步 →")) Advance();
        }

        void PaintPanel()
        {
            H2("上色"); Body("在娃娃身上自由手繪、噴漆，木頭可以上亮光漆。"); Placeholder("上色");
            if (MainBtn("先跳過上色 →")) Advance();
        }

        void DecorPanel()
        {
            H2("裝飾");
            Small("（配件擺放與刺繡五官的繪製在 M4 移植；目前先選要加哪些配件，會計入標籤和花費。）");
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
            if (MainBtn($"完成，交給{ss.order.who} →")) Advance();
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
            var r = ss.result; var green = new Color(.42f, .66f, .54f);
            H2("交件");
            Small($"交件後才知道：{ss.order.who}在意的是——");
            foreach (var (text, ok) in r.review) Body($"{(ok ? "✔" : "✘")} {text}");
            Bar("符合期待", r.tagS, green, $"{Mathf.RoundToInt(r.tagS * 100)}%");
            Bar("柔軟度", r.softS, green, $"{Mathf.RoundToInt(r.softS * 100)}%");
            Bar("手工品質", r.craft, green, $"{Mathf.RoundToInt(r.craft * 100)}%");
            Body($"成本 ${r.cost} ／ 預算 ${ss.order.budget}　收到 ${r.pay}　利潤 {(r.profit >= 0 ? "+" : "")}${r.profit}　聲望 {(r.stars - 3 >= 0 ? "+" : "")}{r.stars - 3}");
            foreach (var c in r.comments) Small("・" + c);
            if (MainBtn("下一位客人 →")) NewOrder();
        }
    }
}
