using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DollWorkshop
{
    /// <summary>
    /// 裝飾：點配件加到娃娃上（眼睛、鼻子、嘴巴是刺繡的五官，會加在臉上），
    /// 拖曳移動、滾輪或按鈕縮放、旋轉，Delete 刪除。同一種位置的配件會錯開一點放。（網頁版 addDecor / hitDecor）
    /// </summary>
    public class DecorGame : CraftGame
    {
        public int sel = -1;
        Vector2? dragOff;

        public DecorGame(Session ss) : base(ss) { }

        public DecorItem Sel => sel >= 0 && sel < D.decor.Count ? D.decor[sel] : null;

        public DecorItem Add(string id)
        {
            var d = DB.DecorById(id); var p = D.DecorPos(d.pos);
            int same = D.decor.Count(x => DB.DecorById(x.type)?.pos == d.pos);
            var it = new DecorItem { type = id, x = p.x + same * 12, y = p.y + same * 10, s = D.IsHuman ? .85f : 1 };
            D.decor.Add(it); sel = D.decor.Count - 1; D.Touch();
            return it;
        }

        /// <summary>點到哪個配件（離中心最近、而且在點選範圍內的那個）。</summary>
        public int HitAt(Vector2 p)
        {
            int best = -1; float bs = 1;
            for (int i = 0; i < D.decor.Count; i++)
            {
                var it = D.decor[i]; float s = Vector2.Distance(p, it.Pos) / ((DB.DecorById(it.type)?.hit ?? 20) * it.s);
                if (s <= bs) { bs = s; best = i; }
            }
            return best;
        }

        /// <summary>對選取的配件：+ 放大、- 縮小、l／r 旋轉、del 刪除。</summary>
        public void Act(string a)
        {
            var it = Sel; if (it == null) return;
            if (a == "+") it.s = Mathf.Clamp(it.s * 1.12f, .4f, 2.6f);
            if (a == "-") it.s = Mathf.Clamp(it.s / 1.12f, .4f, 2.6f);
            if (a == "l") it.rot -= .2f;
            if (a == "r") it.rot += .2f;
            if (a == "del") { D.decor.RemoveAt(sel); sel = -1; }
            D.Touch();
        }

        // ---------- 操作 ----------
        public override string Hint => "點右邊的配件加上去；拖曳移動、滾輪縮放";
        public override bool Done => true;

        public override void Down(Vector2 p)
        {
            int i = HitAt(p); sel = i; dragOff = null;
            if (i < 0) return;
            var it = D.decor[i]; dragOff = p - it.Pos;
            D.decor.RemoveAt(i); D.decor.Add(it); sel = D.decor.Count - 1; D.Touch(); // 拿起來的放到最上層
        }

        public override void Move(Vector2 p, Vector2 prev)
        {
            var it = Sel; if (it == null || dragOff == null) return;
            it.x = Mathf.Clamp(p.x - dragOff.Value.x, 0, SM.W); it.y = Mathf.Clamp(p.y - dragOff.Value.y, 0, SM.H); D.Touch();
        }

        public override void Up(Vector2 p) => dragOff = null;

        public override void Tick(float dt)
        {
            var it = Sel; if (it == null) return;
            var mouse = Mouse.current;
            if (mouse != null && ptr.inside)
            {
                float y = mouse.scroll.ReadValue().y;
                if (y != 0) { it.s = Mathf.Clamp(it.s * (y > 0 ? 1.08f : 1 / 1.08f), .4f, 2.6f); D.Touch(); }
            }
            var kb = Keyboard.current;
            if (kb != null && (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) Act("del");
        }

        // ---------- 畫面 ----------
        public override void Draw(Sketch under, Sketch over)
        {
            var it = Sel; if (it == null) return;
            float r = (DB.DecorById(it.type)?.hit ?? 20) * it.s + 4;
            var ring = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < 48; i++) { float a = i / 48f * Mathf.PI * 2; ring.Add(it.Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
            over.P.Line(ring, 2, Accent, true, 5, 4);
        }

        public override void Panel(GameScreen ui)
        {
            ui.H2("裝飾");
            ui.Small(D.IsHuman ? "點一下加到娃娃上（可以加很多個）——眼睛、嘴巴會加在臉上，是刺繡的五官。" : "點一下加到娃娃上（可以加很多個）。眼睛、鼻子、嘴巴是刺繡的五官。");
            var list = DB.Decor;
            for (int i = 0; i < list.Count; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(list.Count, i + 2); j++)
                    if (ui.Btn($"{list[j].name}  ${list[j].cost}")) Add(list[j].id);
                GUILayout.EndHorizontal();
            }
            bool on = Sel != null;
            ui.Small(on ? $"選取的配件：{DB.DecorById(Sel.type).name}" : "點娃娃身上的配件可以選取。");
            GUILayout.BeginHorizontal();
            if (ui.Btn("縮小", on)) Act("-");
            if (ui.Btn("放大", on)) Act("+");
            if (ui.Btn("↺", on)) Act("l");
            if (ui.Btn("↻", on)) Act("r");
            if (ui.Btn("刪除", on)) Act("del");
            GUILayout.EndHorizontal();
            if (ui.MainBtn($"完成，交給{ss.order.who} →")) ui.Advance();
        }
    }
}
