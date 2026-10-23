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
            { Step.Cast, "搪膠灌模：按住倒膠到虛線；烤的時候指針走到綠色區間就出爐；倒膠時按住越久，外殼越薄越軟。" },
            { Step.Trim, "沿著邊緣削掉凸出來的毛邊，刀子不要切進娃娃。" },
            { Step.Saw, "沿著鉛筆線慢慢鋸，太快鋸子會卡住。" },
            { Step.Carve, "刮掉外圍多餘的木頭，不要鑿進娃娃本體。" },
            { Step.Sand, "按住來回磨：先用粗砂紙磨平，再換細砂紙磨光。" },
            { Step.Paint, "自由上色：選顏料和筆刷大小，按住拖曳就能畫。噴漆適合畫腮紅這類漸層。" },
            { Step.Decor, "點右邊的配件加到娃娃上，拖曳移動、滾輪縮放，Delete 刪除。眼睛、鼻子、嘴巴都是刺繡的五官。" },
            { Step.GDesign, "做衣服：選種類和尺寸。衣服要跟娃娃的身形一樣才穿得下（帽子看頭圍）——看清楚客人說的娃娃身形。" },
            { Step.GDecor, "在衣服上加蕾絲、鈕扣、口袋等裝飾。" },
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

        public void NewOrder() => StartSession(new Session(meta, meta.NextOrder()), Step.Order);

        /// <summary>自由創作：不接單，直接從設計開始。</summary>
        public void NewFree(bool garment) => StartSession(new Session(meta, Orders.Free(garment)), garment ? Step.GDesign : Step.Design);

        void StartSession(Session s, Step start)
        {
            ss?.ClearPreview();
            closet = null;
            ss = s;
            if (start != Step.Order) ss.Begin(start);
            dollRenderer.state = ss.ViewDoll;
            design.ResetUI();
            design.onDone = Advance;
            design.orderBrief = ss.order.free ? ss.order.text : $"{ss.order.who}：「{ss.order.text}」　預算 ${ss.order.budget}";
            SyncDesign();
            scroll = Vector2.zero;
        }

        // ---------- 衣櫃 ----------
        ClosetScreen closet;
        public void OpenCloset() { ss.ClearPreview(); closet = new ClosetScreen(ss); design.enabled = false; scroll = Vector2.zero; }
        public void CloseCloset()
        {
            closet = null; scroll = Vector2.zero;
            if (ss.step == Step.Deliver) NewOrder(); else SyncDesign();
        }
        CraftGame Active => closet ?? ss.craft;

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
            design.enabled = ss.step == Step.Design && closet == null;
            if (!design.enabled) dollRenderer.highlight = null;
        }

        void Flash(string t) { flash = t; flashUntil = Time.time + 2.5f; }

        // ---------- 每格：輸入、小遊戲、繪圖 ----------
        void Update()
        {
            cam.orthographicSize = Mathf.Max(2.8f, 4.0f / Mathf.Max(.1f, cam.aspect)); // 整張 800×560 的畫布都要看得到
            if (!design.enabled) cam.rect = new Rect(0, 0, 1 - DesignScreen.PanelWidth / Screen.width, 1);
            var craft = Active;
            ss.SyncPreview();
            dollRenderer.state = closet != null ? closet.model : ss.ViewDoll;
            // 衣服訂單：接單時在旁邊小小地顯示客人的娃娃
            bool customerDoll = closet == null && ss.step == Step.Order && ss.model != null;
            dollRenderer.gameObject.SetActive(customerDoll || (closet != null || ss.step != Step.Order) && (craft == null || craft.ShowDoll));
            if (customerDoll)
            {
                const float k = .42f; float b = ss.model.Bounds().yMax;
                dollRenderer.transform.localScale = new Vector3(k, k, 1);
                dollRenderer.transform.position = DollRenderer.W(new Vector2(560, 440)) - k * DollRenderer.W(new Vector2(SM.CX, b));
            }
            else
            {
                var off = craft?.DollOffset ?? Vector2.zero;
                dollRenderer.transform.localScale = Vector3.one;
                dollRenderer.transform.position = new Vector3(off.x / 100f, -off.y / 100f, 0);
            }
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

        /// <summary>對話框：左上角在畫布座標 p、寬 w（畫布像素），高度依文字自動決定，下方有尖角。</summary>
        public void CanvasBubble(Vector2 p, float w, string text, int size)
        {
            var sc = cam.WorldToScreenPoint(DollRenderer.W(p));
            float k = cam.pixelHeight / (cam.orthographicSize * 200f);
            var st = new GUIStyle(bubble) { fontSize = Mathf.Max(8, Mathf.RoundToInt(size * k)), padding = new RectOffset((int)(18 * k), (int)(18 * k), (int)(14 * k), (int)(14 * k)) };
            st.normal.background = pill.normal.background;
            float wpx = w * k, h = st.CalcHeight(new GUIContent(text), wpx);
            var r = new Rect(sc.x, Screen.height - sc.y, wpx, h);
            GUI.Box(r, text, st);
            var tail = new GUIStyle(lab) { fontSize = Mathf.RoundToInt(22 * k), alignment = TextAnchor.UpperCenter }; tail.normal.textColor = new Color(1, 1, 1, .9f);
            GUI.Label(new Rect(r.x + r.width * .55f, r.yMax - 6 * k, 30 * k, 30 * k), "▼", tail);
        }

        public void NextCustomer() => NewOrder();

        void Hint(string text) => CanvasLabel(new Vector2(SM.W / 2, 30), text, 16, new Color(.29f, .23f, .2f), TextAnchor.MiddleCenter, true, true);

        // ---------- 畫面 ----------
        void OnGUI()
        {
            Styles();
            float pw = DesignScreen.PanelWidth, gx = Screen.width - pw;
            if (closet == null) StepBar(gx);
            var craft = Active;
            if (closet == null && ss.step == Step.Order && ss.model != null) CanvasLabel(new Vector2(560, 448), "客人的娃娃", 13, new Color(.54f, .35f, .23f), TextAnchor.UpperCenter, true);
            if (craft != null) { craft.CanvasGUI(this); if (craft.Hint != null) Hint(craft.Hint); }
            if (Time.time < flashUntil) GUI.Label(new Rect(16, Screen.height - 64, gx - 32, 30), flash, body);
            if (ss.step == Step.Order && closet == null) DrawCustomer(gx);
            if (design.enabled) return; // 設計步驟的右側面板由 DesignScreen 負責

            GUILayout.BeginArea(new Rect(gx, 0, pw, Screen.height), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            TipCard();
            if (craft != null) craft.Panel(this);
            else switch (ss.step)
            {
                case Step.Order: OrderPanel(); break;
            }
            GUILayout.Space(12);
            if (closet == null) OrderCard();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void TipCard()
        {
            if (closet != null || !Tips.TryGetValue(ss.step, out var t) || meta.tipsSeen.Contains(ss.step)) return;
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
            if (o.free) { GUILayout.Label($"材料費 ${c}（自由創作用自己的錢）", small); GUILayout.Label($"💰 ${meta.coins}　⭐ 聲望 {meta.rep}　🧸 {meta.done} 件", small); return; }
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
            Body(!meta.tutDone ? "第一位客人！讀完需求後接下委託，開始設計第一隻娃娃。"
                : ss.IsGarment ? "有客人帶著自己的娃娃來訂做衣服。仔細讀客人說的話，也要看清楚娃娃的身形。"
                : "有客人上門了。仔細讀客人說的話——你要自己判斷對方想要什麼樣的娃娃。");
            if (MainBtn("接下委託 →")) Advance();
            if (!meta.tutDone) return;
            if (Btn("換一位客人")) NewOrder();
            GUILayout.Space(8);
            if (Btn("🧸 自由創作：做娃娃")) NewFree(false);
            if (Btn("👗 自由創作：做衣服")) NewFree(true);
            if (Btn($"🧺 衣櫃（娃娃 {meta.dolls.Count}・衣服 {meta.clothes.Count}）")) OpenCloset();
        }

        public float Slider(string label, float v, float min, float max, string valueText = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, body, GUILayout.Width(110));
            v = GUILayout.HorizontalSlider(v, min, max);
            GUILayout.Label(valueText ?? $"{Mathf.RoundToInt(v * 100)}%", small, GUILayout.Width(70));
            GUILayout.EndHorizontal();
            return v;
        }
    }
}
