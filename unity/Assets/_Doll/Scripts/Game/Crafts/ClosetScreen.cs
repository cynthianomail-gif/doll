using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 衣櫃：選一隻娃娃（人台或自由創作的娃娃），點衣服穿上或脫下。同身形（帽子看頭圍）的衣服才穿得下；布偶不能穿。
    /// （網頁版 closetPanel / ACT.wear）
    /// </summary>
    public class ClosetScreen : CraftGame
    {
        readonly Meta meta;
        public DollState model;

        public ClosetScreen(Session ss) : base(ss)
        {
            meta = ss.meta;
            model = meta.dolls.FirstOrDefault(d => d.IsHuman) ?? meta.Mannequin("20cm 正常體");
        }

        /// <summary>穿上或脫下。回傳 null 表示成功，否則是穿不上的原因。</summary>
        public string Wear(Garment g)
        {
            if (!model.IsHuman) return "布偶不能穿衣服（之後的版本再加）";
            string mc = Garments.FitClassOf(g, model);
            if (g.cls != mc) return $"穿不下：這件是 {g.cls}，{(model.name != "" ? model.name : "娃娃")}是 {mc}";
            var slot = g.T.slot;
            if (model.wear.TryGetValue(slot, out var cur) && cur == g) model.wear.Remove(slot); else model.wear[slot] = g;
            model.Touch();
            return null;
        }

        public override string Hint => "衣櫃：同身形的衣服可以互穿";
        public override DollState MainDoll => model;
        public override bool Done => true;

        public override void CanvasGUI(GameScreen ui)
        {
            ui.CanvasLabel(new Vector2(24, 24), model.name != "" ? model.name : "娃娃", 15, Hex("#8a5a3a"), TextAnchor.UpperLeft, true);
            ui.CanvasLabel(new Vector2(24, 46), model.IsHuman ? $"身形：{model.BodyClass()}・{model.HeadClass()}" : "動物布偶", 13, Hex("#9a8778"), TextAnchor.UpperLeft);
            if (Flashing) ui.CanvasLabel(new Vector2(SM.W / 2, SM.H - 28), flash, 16, Bad, TextAnchor.MiddleCenter, true, true);
        }

        public override void Panel(GameScreen ui)
        {
            ui.H2("🧺 衣櫃");
            ui.Small("穿衣服的娃娃（人台＝空白的身形模特兒）");
            foreach (var d in meta.ClosetModels())
                if (ui.Chip($"{d.name}　{(d.IsHuman ? d.BodyClass() + "・" + d.HeadClass() : "動物布偶")}", d == model)) { model = d; }
            ui.Small($"衣櫃（{meta.clothes.Count} 件）——點一下穿上／脫下");
            if (meta.clothes.Count == 0) ui.Body("還沒有衣服。回到工作室，用「自由創作：做衣服」做幾件吧。");
            foreach (var g in meta.clothes)
            {
                bool on = model.wear.TryGetValue(g.T.slot, out var w) && w == g;
                bool fit = model.IsHuman && Garments.FitClassOf(g, model) == g.cls;
                if (ui.Chip($"{g.name}{(on ? " ✔" : "")}　{g.cls}{(fit ? "" : "・尺寸不合")}", on, g.fab.color.color))
                {
                    var why = Wear(g); if (why != null) Flash(why);
                }
            }
            if (ui.MainBtn("← 回到工作室")) ui.CloseCloset();
        }
    }
}
