using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DollWorkshop
{
    /// <summary>
    /// 設計畫面（從網頁原型的「設計」步驟移植）。
    /// 介面暫時用 IMGUI 快速搭出來，之後美術與介面整理時會換成 UI Toolkit；
    /// 邏輯都在 DollState，換介面時不需要改。
    /// </summary>
    public class DesignScreen : MonoBehaviour
    {
        public DollRenderer dollRenderer;
        public Camera cam;

        DollState S => dollRenderer.state;
        string tab = "body";
        Vector2 scroll;
        int adjSide = 1;
        string flash; float flashUntil;

        // 拖曳中的部件
        string dragKey; int dragSide; Vector2 dragOffset, dragStart; float dragDx0, dragDy0;

        GUIStyle chip, chipOn, h4, small, title, status;
        Font font;
        const float PanelFrac = .4f;

        static readonly (string id, string label)[] TabsAnimal = { ("body", "身體"), ("ears", "耳朵"), ("earInner", "耳朵內側"), ("arms", "手"), ("feet", "腳"), ("belly", "肚子") };
        static readonly (string id, string label)[] TabsHuman = { ("head", "頭"), ("body", "身體"), ("hair", "頭髮"), ("ears", "屬性耳"), ("earInner", "耳朵內側") };

        void Start()
        {
            if (cam == null) cam = Camera.main;
            dollRenderer.highlight = tab;
        }

        float PanelW => Mathf.Max(380, Screen.width * PanelFrac);

        void Update()
        {
            cam.rect = new Rect(0, 0, 1 - PanelW / Screen.width, 1);
            dollRenderer.highlight = tab;
            HandlePointer();
        }

        // ---------- 拖曳與點選 ----------
        void HandlePointer()
        {
            var mouse = Mouse.current; if (mouse == null || !S.DesignReady) return;
            Vector2 sp = mouse.position.ReadValue();
            bool inGame = sp.x < Screen.width - PanelW;
            Vector2 p = DollRenderer.Px(cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, 10)));

            if (mouse.leftButton.wasPressedThisFrame && inGame)
            {
                var hit = S.PickAt(p, tab);
                if (hit != null)
                {
                    tab = hit.Value.tab;
                    if (hit.Value.key == "belly") { dragKey = "belly"; dragStart = p; dragDx0 = S.belly.dx; dragDy0 = S.belly.dy; }
                    else if (hit.Value.key != null)
                    {
                        dragKey = hit.Value.key; dragSide = hit.Value.side; adjSide = dragSide;
                        dragOffset = p - S.PartXform(dragKey, dragSide).pos;
                    }
                }
            }
            if (mouse.leftButton.isPressed && dragKey != null) MoveDrag(p);
            if (mouse.leftButton.wasReleasedThisFrame) dragKey = null;

            float wheel = mouse.scroll.ReadValue().y;
            if (inGame && wheel != 0 && (DB.PartKeys.Contains(tab) || tab == "belly")) AdjOp(wheel > 0 ? "+" : "-");
        }

        void MoveDrag(Vector2 p)
        {
            if (dragKey == "belly")
            {
                var m = S.GetMain("body");
                S.belly.dx = Mathf.Clamp(dragDx0 + (p.x - dragStart.x) / m.r, -.5f, .5f);
                S.belly.dy = Mathf.Clamp(dragDy0 + (p.y - dragStart.y) / m.r, -.65f, .45f);
                S.Touch(); return;
            }
            var host = S.GetMain(S.HostOf(dragKey));
            var t = p - dragOffset;
            float phi = Mathf.Atan2(t.y - host.cy, t.x - host.cx);
            float a = Mathf.Clamp(SM.AngDiff(phi, -Mathf.PI / 2) / SM.D * dragSide, 0, 180);
            float rEdge = host.r * SM.ShapeR(host.sh, phi) * DollState.PreviewK;
            float inset = Mathf.Clamp(1 - Vector2.Distance(t, new Vector2(host.cx, host.cy)) / rEdge, -.3f, .45f);
            S.SetAdj(dragKey, dragSide, q => { q.a = a; q.inset = inset; });
        }

        void AdjOp(string op)
        {
            if (tab == "belly")
            {
                if (op == "+") S.belly.scale = Mathf.Clamp(S.belly.scale * 1.1f, .5f, 1.6f);
                if (op == "-") S.belly.scale = Mathf.Clamp(S.belly.scale / 1.1f, .5f, 1.6f);
                if (op == "reset") S.ResetAdj("belly");
                S.Touch(); return;
            }
            if (!S.adj.TryGetValue(tab, out var A)) return;
            switch (op)
            {
                case "sym": A.sym = !A.sym; if (A.sym) { var c = A.Get(adjSide).Clone(); if (adjSide < 0) A.right = c; else A.left = c; } S.Touch(); break;
                case "+": S.SetAdj(tab, adjSide, q => q.scale = Mathf.Clamp(q.scale * 1.1f, .5f, 1.8f)); break;
                case "-": S.SetAdj(tab, adjSide, q => q.scale = Mathf.Clamp(q.scale / 1.1f, .5f, 1.8f)); break;
                case "l": S.SetAdj(tab, adjSide, q => q.tilt -= 8); break;
                case "r": S.SetAdj(tab, adjSide, q => q.tilt += 8); break;
                case "side": adjSide = -adjSide; break;
                case "reset": S.ResetAdj(tab); break;
            }
        }

        // ---------- 介面 ----------
        void Styles()
        {
            if (chip != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Microsoft JhengHei UI", "PingFang TC", "Noto Sans CJK TC", "Microsoft YaHei", "Arial" }, 15);
            GUI.skin.font = font;
            chip = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 6, 6), wordWrap = true };
            chipOn = new GUIStyle(chip) { fontStyle = FontStyle.Bold };
            chipOn.normal.textColor = chipOn.hover.textColor = new Color(.79f, .38f, .25f);
            h4 = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, margin = new RectOffset(4, 4, 10, 2) };
            h4.normal.textColor = new Color(.6f, .53f, .47f);
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            small.normal.textColor = new Color(.55f, .48f, .42f);
            title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(.29f, .23f, .2f);
            status = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            status.normal.textColor = new Color(.29f, .23f, .2f);
        }

        bool Chip(string label, bool on, Color? tint = null)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = tint ?? (on ? new Color(1f, .86f, .78f) : new Color(.95f, .91f, .85f));
            bool r = GUILayout.Button((on ? "✔ " : "") + label, on ? chipOn : chip);
            GUI.backgroundColor = old;
            return r;
        }

        void ChipRow<T>(IList<T> items, System.Func<T, string> label, System.Func<T, bool> isOn, System.Action<T> pick, int perRow = 3, System.Func<T, Color?> tint = null)
        {
            for (int i = 0; i < items.Count; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(items.Count, i + perRow); j++)
                    if (Chip(label(items[j]), isOn(items[j]), tint?.Invoke(items[j]))) { pick(items[j]); S.Touch(); }
                GUILayout.EndHorizontal();
            }
        }

        void Slider(string group, SliderDef d)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(d.label, GUILayout.Width(80));
            float v = S.shp[group][d.key];
            float nv = GUILayout.HorizontalSlider(v, d.min, d.max);
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(v, nv)) { S.shp[group][d.key] = nv; S.Rebuild(); }
        }

        void Flash(string t) { flash = t; flashUntil = Time.time + 2.5f; }

        void OnGUI()
        {
            Styles();
            float pw = PanelW, gx = Screen.width - pw;
            // 左側畫面上方的提示
            GUI.Label(new Rect(16, 12, gx - 32, 60), S.DesignReady
                ? "點畫面上的部位可以切換分頁；耳朵、手、腳、肚子可以直接拖曳移動（滾輪縮放）。" + (Time.time < flashUntil ? "  " + flash : "")
                : $"先選{SlotLabel("body")}的布料和顏色。", status);
            if (S.DesignReady) GUI.Label(new Rect(16, Screen.height - 34, gx - 32, 24), $"用到 {S.FabricCount()} 種布・" + (S.IsHuman ? $"{S.BodyClass()}・{S.HeadClass()}" : S.body.name), small);

            GUILayout.BeginArea(new Rect(gx, 0, pw, Screen.height), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("🧸 設計娃娃", title);

            GUILayout.Label("材質", h4);
            GUILayout.BeginHorizontal();
            if (Chip("🧵 布偶", S.material == "cloth")) SetMaterial("cloth");
            if (Chip("🫧 搪膠", S.material == "vinyl")) SetMaterial("vinyl");
            if (Chip("🪵 木頭", S.material == "wood")) SetMaterial("wood");
            GUILayout.EndHorizontal();

            GUILayout.Label("娃娃類型", h4);
            GUILayout.BeginHorizontal();
            if (Chip("🐻 動物布偶", !S.IsHuman)) SetKind("animal");
            if (Chip("🧒 人形娃娃", S.IsHuman)) SetKind("human");
            GUILayout.EndHorizontal();

            var tabs = (S.IsHuman ? TabsHuman : TabsAnimal).Where(t => t.id != "earInner" || S.parts["ears"].inner).ToList();
            GUILayout.Label("部位", h4);
            ChipRow(tabs, t => t.label + PartSuffix(t.id), t => t.id == tab, t => tab = t.id, 3);

            TabContent();

            GUILayout.Space(14);
            GUI.enabled = S.DesignReady;
            if (GUILayout.Button(S.IsHard ? "設計好了，開始製作 →（下一個移植項目）" : "設計好了，去排版 →（下一個移植項目）", chipOn, GUILayout.Height(40)))
                Flash("製作流程還在移植中，目前先做到設計畫面。");
            GUI.enabled = true;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        string PartSuffix(string id)
        {
            if (DB.PartKeys.Contains(id) || id == "belly") return S.parts[id].IsNone ? "" : "・" + S.parts[id].name;
            if (id == "hair" && S.hair.id != "none") return "・" + S.hair.name;
            return "";
        }

        string SlotLabel(string s) => s == "body" && S.IsHuman ? "皮膚" : DB.SlotName[s];
        string SlotOfTab() => tab == "head" ? "body" : tab;

        void TabContent()
        {
            if (tab == "body" && !S.IsHuman)
            {
                GUILayout.Label("身體造型（先選基本形，再用滑桿調整）", h4);
                ChipRow(DB.Bodies, b => b.name, b => S.body == b, b => { S.body = b; S.Rebuild(); });
                foreach (var d in DB.Sliders["body"]) Slider("body", d);
                if (GUILayout.Button("重設比例", chip)) { ResetShp("body"); }
            }
            else if (tab == "body")
            {
                GUILayout.Label("身體素體：用滑桿自由調整", h4);
                foreach (var d in DB.Sliders["torso"]) Slider("torso", d);
                GUILayout.Label($"目前身形：{S.BodyClass()}（之後做的衣服，只有同身形的娃娃能互穿）", small);
                if (GUILayout.Button("重設比例", chip)) ResetShp("torso");
            }
            else if (tab == "head")
            {
                GUILayout.Label("頭型（先選基本形，再用滑桿調整）", h4);
                ChipRow(DB.Heads, b => b.name, b => S.head == b, b => { S.head = b; S.Rebuild(); });
                foreach (var d in DB.Sliders["head"]) Slider("head", d);
                GUILayout.Label($"目前頭圍：{S.HeadClass()}（帽子要同頭圍才戴得下）", small);
                if (GUILayout.Button("重設比例", chip)) ResetShp("head");
            }
            else if (tab == "hair")
            {
                GUILayout.Label("髮型", h4);
                ChipRow(DB.Hairs, h => h.name, h => S.hair == h, h => S.hair = h);
            }
            else if (tab != "earInner")
            {
                var g = DB.Parts[tab];
                GUILayout.Label(g.label + "造型", h4);
                ChipRow(g.opts, o => o.name, o => S.parts[tab] == o, o => { S.parts[tab] = o; S.ResetAdj(tab); });
                AdjPanel();
            }

            string s = SlotOfTab();
            bool hasFab = s == "body" || s == "earInner" || (s == "hair" ? S.hair.id != "none" : !S.parts[s].IsNone);
            if (!hasFab) return;
            S.fab.TryGetValue(s, out var own);
            var cur = S.DesignReady ? S.FabOf(s) : (own ?? new Fab(null, null));
            bool inherit = s != "body" && own == null;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{SlotLabel(s)}的{(S.material == "wood" ? "木材" : S.IsHard ? "顏色" : "布料")}", h4);
            if (s != "body" && Chip($"跟{SlotLabel("body")}一樣", inherit)) { S.fab.Remove(s); S.Touch(); }
            GUILayout.EndHorizontal();
            if (!S.IsHard)
                ChipRow(DB.Materials, m => $"{m.name}  每 10cm ${m.p10}", m => cur.mat == m, m => SetFab(s, m, null), 2);
            var colors = S.material == "wood" ? DB.Woods : DB.Colors;
            if (!S.IsHard) GUILayout.Label($"{SlotLabel(s)}的顏色", h4);
            ChipRow(colors, c => c.name, c => cur.color == c, c => SetFab(s, null, c), 4, c => c.color);
        }

        void AdjPanel()
        {
            if (S.parts[tab].IsNone) return;
            GUILayout.Label($"位置：直接在畫面上拖曳{DB.Parts[tab].label}，它會沿著{(S.IsHuman ? "頭" : "身體")}的邊緣滑動；拖進去一點會陷得比較深。", small);
            if (!S.adj.TryGetValue(tab, out var A)) S.ResetAdj(tab);
            A = S.adj[tab];
            GUILayout.BeginHorizontal();
            if (Chip($"對稱：{(A.sym ? "開" : "關")}", A.sym)) AdjOp("sym");
            if (!A.sym && Chip($"正在調：{(adjSide < 0 ? "左邊" : "右邊")}", false)) AdjOp("side");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Chip("縮小", false)) AdjOp("-");
            if (Chip("放大", false)) AdjOp("+");
            if (Chip("↺ 角度", false)) AdjOp("l");
            if (Chip("↻ 角度", false)) AdjOp("r");
            if (Chip("重設", false)) AdjOp("reset");
            GUILayout.EndHorizontal();
        }

        void SetFab(string s, MatDef m, ColorDef c)
        {
            var hm = S.IsHard ? DB.Mat(S.material) : null;
            if (s == "body")
            {
                S.fab.TryGetValue("body", out var b); b ??= new Fab(null, null);
                if (m != null) b.mat = m; if (c != null) b.color = c; if (hm != null) b.mat = hm;
                S.fab["body"] = b;
            }
            else
            {
                S.fab.TryGetValue(s, out var f); f ??= new Fab(null, null);
                if (m != null) f.mat = m; if (c != null) f.color = c; if (hm != null) f.mat = hm;
                S.fab[s] = f;
            }
            S.Touch();
        }

        void SetMaterial(string id)
        {
            if (S.material == id) return;
            S.material = id;
            foreach (var k in new[] { "ears", "earInner", "arms", "feet", "belly" }) S.fab.Remove(k);
            if (id != "cloth")
            {
                var hm = DB.Mat(id);
                S.fab["body"] = new Fab(hm, id == "wood" ? DB.Col("pine") : DB.Col("cream"));
                S.fab["hair"] = new Fab(hm, id == "wood" ? DB.Col("walnut") : DB.Col("cocoa"));
            }
            else
            {
                S.fab.Remove("body");
                S.fab["hair"] = new Fab(DB.Mat("minky"), DB.Col("cocoa"));
            }
            S.Touch();
        }

        void SetKind(string k)
        {
            if (S.kind == k) return;
            S.kind = k;
            foreach (var p in new[] { "ears", "arms", "feet", "belly" }) { S.parts[p] = DB.Parts[p].None; S.adj.Remove(p); }
            tab = k == "human" ? "head" : "body";
            if (k == "human" && S.material == "cloth")
            {
                S.fab.TryGetValue("body", out var b);
                if (b == null || b.color == null) S.fab["body"] = new Fab(b?.mat, DB.Col("skin"));
            }
            S.Rebuild();
        }

        void ResetShp(string g)
        {
            foreach (var d in DB.Sliders[g]) S.shp[g][d.key] = d.def;
            S.Rebuild();
        }
    }
}
