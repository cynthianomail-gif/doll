using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DollWorkshop.EditorTools
{
    /// <summary>
    /// 不開畫面，直接用程式把訂單從接單跑到交件：布偶的排版、裁布、車縫、塞棉花都由程式「代玩」，
    /// 檢查狀態機、帳本、小遊戲規則和評分。
    /// </summary>
    public static class FlowSelfTest
    {
        static int fails;
        static readonly List<string> log = new List<string>();
        static void Check(bool ok, string what) { if (!ok) fails++; log.Add($"{(ok ? "PASS" : "FAIL")} {what}"); }

        static float clock;

        static Session NewCloth(Meta meta, OrderDef order, string mat = "minky", bool parts = true)
        {
            var ss = new Session(meta, order);
            ss.Advance(); // 接單 → 設計
            var d = ss.doll;
            d.fab["body"] = new Fab(DB.Mat(mat), DB.Col("cream"));
            if (parts)
            {
                d.parts["ears"] = DB.Parts["ears"].Find("round"); d.ResetAdj("ears");
                d.parts["arms"] = DB.Parts["arms"].Find("nub"); d.ResetAdj("arms");
                d.fab["ears"] = new Fab(DB.Mat("felt"), DB.Col("pink"));
            }
            d.Rebuild();
            return ss;
        }

        static void Layout(Session ss) { ss.Advance(); ss.layout.AutoPack(); }

        static void CutAll(Session ss)
        {
            var cut = (CutGame)ss.craft;
            while (true)
            {
                var P = cut.pts; cut.Down(P[0]);
                for (int k = 1; k <= P.Count && !cut.done; k++) cut.Drag(P[k % P.Count]);
                if (cut.Last) break;
                cut.NextPiece();
            }
        }

        static void SewAll(SewGame sew, bool backstitch)
        {
            for (sew.cur = 0; ; sew.cur++)
            {
                var sw = sew.SW;
                for (int i = 20; i < sw.path.Count - 10; i += 40) sew.PlacePin(sw.path[i].p);
                sew.StartSewing();
                sew.ptr.speed = .1f;
                for (int k = 0; k < sw.path.Count && !sw.done; k++)
                {
                    sew.PullPin();
                    sew.Process(sw.path[k].p);
                    if (backstitch && sw.idx == 5 && !sw.backStart) { sew.HoldBack(); sew.Tick(.7f); }
                }
                if (backstitch) { sew.HoldBack(); sew.Tick(.7f); }
                if (sew.cur >= sew.List.Count - 1) break;
            }
        }

        /// <summary>某個塞棉花部位畫面上的一個點（用來把棉花丟進去）。</summary>
        static Vector2 RegionPoint(DollState d, StuffRegion r)
        {
            if (r.part != null) return SM.Centroid(d.PartPoly(r.part, r.side));
            var m = d.GetMain(r.main);
            if (r.bump != null) { float t = r.bump.Value.deg * SM.D; return d.ToDoll(m, Mathf.Cos(t) * m.r * SM.ShapeR(m.sh, t) * .85f, Mathf.Sin(t) * m.r * SM.ShapeR(m.sh, t) * .85f); }
            return new Vector2(m.cx, m.cy);
        }

        static void Fill(StuffGame st, StuffRegion r, float target, float size)
        {
            var d = st.D; int guard = 0;
            while (r.fill < target && guard++ < 60) if (!st.DropClump(RegionPoint(d, r), size)) break;
        }

        static void StuffAll(StuffGame st, float smallTo, float mainTo)
        {
            foreach (var r in st.Regs.Where(r => r.small)) Fill(st, r, smallTo, .8f);
            foreach (var r in st.Regs.Where(r => r.IsMainBody)) Fill(st, r, mainTo, 1f);
            var c = RegionPoint(st.D, st.Regs.First(r => r.IsMainBody));
            st.Down(c); for (int i = 0; i < 400; i++) st.Move(c + new Vector2((i % 2) * 30, 0), c + new Vector2(((i + 1) % 2) * 30, 0)); st.Up(c); // 揉平
            st.StartClosing();
            foreach (var p in st.ClosePoints()) st.ClickClose(p);
        }

        [MenuItem("Doll/流程自我測試")]
        public static void Run()
        {
            fails = 0; log.Clear();
            var keepClock = CraftGame.Clock; clock = 0; CraftGame.Clock = () => clock;
            try { RunAll(); }
            catch (System.Exception e) { fails++; log.Add("FAIL 例外：" + e); }
            finally { CraftGame.Clock = keepClock; }
            foreach (var l in log) Debug.Log("[FlowTest] " + l);
            Debug.Log(fails == 0 ? "[FlowTest] 全部通過" : $"[FlowTest] 有 {fails} 項失敗");
        }

        public static string RunAndReport() { Run(); return string.Join("\n", log) + "\n" + (fails == 0 ? "全部通過" : $"有 {fails} 項失敗"); }

        static void RunAll()
        {
            var meta = new Meta();
            Check(meta.NextOrder() == Orders.Doll[0], "新手教學固定第一位是林太太");

            var s0 = new Session(meta, Orders.Doll[0]); s0.Advance();
            Check(s0.step == Step.Design && !s0.CanAdvance(out _), "沒選身體布料不能離開設計");

            // ---------- 排版 ----------
            var ss = NewCloth(meta, Orders.Doll[0]);
            ss.Advance();
            var L = ss.layout;
            Check(ss.step == Step.Layout && ss.doll.previewFill == 0, "進入排版，娃娃變回沒塞棉花的布");
            Check(L.units.Count == 4 && L.units[0].mult == 2 && L.units[1].mult == 4 && L.units[2].name == "耳朵內側", $"布片（圓耳有內側）：{string.Join("、", L.units.Select(u => u.name + "×" + u.mult))}");
            Check(L.bolts.Count == 2, $"兩種布分成兩捲（{L.bolts.Count}）");
            Check(!L.Valid && !ss.CanAdvance(out _), "布片還在布外面時不能往下");
            L.AutoPack();
            Check(L.Valid, $"自動排好後合格，布料 ${L.Cost}");
            var u0 = L.units[1]; var keep = (u0.x, u0.y);
            u0.x = L.units[0].x; u0.y = L.units[0].y; L.Check();
            Check(L.Valid, "不同布捲的布片放同一個位置不算重疊");
            (u0.x, u0.y) = keep; L.Check();
            var b0 = L.bolts[0]; var minkyUnit = L.units[b0.units[0]];
            minkyUnit.rot = 1; L.Check();
            Check(!L.uInfo[b0.units[0]].grain, "短絨毛（單向布紋）轉 90° 布紋不對");
            minkyUnit.rot = 0; L.Check();
            b0.flaws[0] = new Vector2(minkyUnit.x, minkyUnit.y); L.Check();
            ss.Advance();
            Check(ss.step == Step.Cut && ss.fabricCost == L.Cost && ss.fabricCost > 0, $"確定排版：買布 ${ss.fabricCost}");
            Check(ss.doll.stains.Count == 1, "身體布片壓到污漬，娃娃身上留下污漬");

            // ---------- 裁布 ----------
            var cut = (CutGame)ss.craft;
            Check(!ss.CanAdvance(out _), "還沒剪完不能往下");
            CutAll(ss);
            Check(cut.Done && ss.cutAcc > .95f, $"沿線剪完全部 {cut.order.Count} 塊，精準度 {ss.cutAcc:P0}");
            var cut2 = new CutGame(ss, L); var P = cut2.pts; cut2.Down(P[0] + new Vector2(10, 0));
            for (int k = 1; k <= P.Count && !cut2.done; k++) cut2.Drag(P[k % P.Count] + new Vector2(10, 0));
            Check(cut2.done && cut2.accs[0] < .6f, $"剪歪 10 像素精準度變低（{cut2.accs[0]:P0}）");
            ss.cutAcc = cut.acc;
            ss.Advance();

            // ---------- 車縫 ----------
            var sew = (SewGame)ss.craft;
            Check(ss.step == Step.Sew && sew.List.Count == 1 && sew.SW.path.Count > 250, $"一條縫線，{sew.SW.path.Count} 點（開口不車）");
            Check(sew.SW.path.Any(p => p.part), "夾著耳朵、手的地方要放慢");
            var t = new SewGame(ss); var tw = t.SW; t.PlacePin(tw.path[40].p); t.StartSewing(); t.ptr.speed = .1f;
            for (int k = 0; k <= 45; k++) t.Process(tw.path[k].p);
            Check(tw.pinHits == 1, "沒拔珠針就車過去會斷針");
            clock += 2000; t.ptr.speed = 5; t.Process(tw.path[tw.idx + 2].p);
            Check(tw.jams == 1, "車太快會跳針");
            sew = new SewGame(ss); ss.craft = sew;
            SewAll(sew, true);
            Check(sew.Done && ss.BackOk && ss.SewAcc > .85f, $"車完全部縫線、有回針，品質 {ss.SewAcc:P0}");
            ss.Advance();

            // ---------- 塞棉花 ----------
            var st = (StuffGame)ss.craft;
            Check(ss.step == Step.Stuff && st.Regs.Count == 5, $"塞棉花部位：{string.Join("、", st.Regs.Select(r => r.name))}");
            var ear = st.Regs.First(r => r.part == "ears");
            Check(!st.DropClump(RegionPoint(ss.doll, ear), 2f), "大團塞不進耳朵");
            Check(!st.CanClose, "身體太扁不能縫合");
            StuffAll(st, .75f, .8f);
            Check(st.Done, $"塞好並縫合，整體手感：{Evaluator.SoftLabel(ss.doll.OverallSoft())}（{ss.doll.OverallSoft():F2}）");
            ss.Advance(); ss.Advance();
            var r1 = ss.result;
            Check(ss.step == Step.Deliver && r1 != null, $"交件：{r1.stars}★，手工 {r1.craft:P0}，柔軟 {r1.softS:P0}");
            Check(r1.comments.Contains("這裡怎麼有一塊污漬？"), "評語提到污漬");
            Check(meta.done == 1 && meta.tutDone && meta.coins == 100 + r1.profit, $"錢包加上利潤（{r1.profit}）");

            // 太晚塞手腳、沒回針、塞爆
            var bad = NewCloth(meta, Orders.Doll[0]); Layout(bad); bad.Advance(); CutAll(bad); bad.Advance();
            SewAll((SewGame)bad.craft, false); bad.Advance();
            var bst = (StuffGame)bad.craft;
            var body = bst.Regs.First(r => r.IsMainBody);
            Fill(bst, body, .6f, 1f);
            Check(!bst.DropClump(RegionPoint(bad.doll, bst.Regs.First(r => r.part == "arms")), .8f), "身體塞太滿後，手塞不進去");
            Fill(bst, body, 1.3f, 2.5f);
            bst.Tick(1f);
            Check(bad.leaked, "沒回針又塞很滿，棉花從線頭跑出來");
            bst.StartClosing(); foreach (var p in bst.ClosePoints()) bst.ClickClose(p);
            bad.Advance(); bad.Advance();
            var r2 = bad.result;
            Check(r2.craft < r1.craft && r2.comments.Any(c => c.Contains("棉花一直跑出來")) && r2.comments.Any(c => c.Contains("扁扁的")), $"做壞的娃娃手工較低（{r2.craft:P0}），評語：{string.Join(" / ", r2.comments)}");

            // 人形娃娃：素體的手腳是可以分開塞的部位、兩條縫線
            var hu = new Session(meta, Orders.Doll[8]); hu.Advance();
            hu.doll.kind = "human"; hu.doll.fab["body"] = new Fab(DB.Mat("cotton"), DB.Col("skin")); hu.doll.Rebuild();
            Layout(hu); Check(hu.layout.Valid, $"人形排版：{string.Join("、", hu.layout.units.Select(u => u.name))}");
            hu.Advance(); CutAll(hu); hu.Advance();
            Check(((SewGame)hu.craft).List.Count == 2, "人形有頭和身體兩條縫線");
            SewAll((SewGame)hu.craft, true); hu.Advance();
            var hst = (StuffGame)hu.craft;
            Check(hst.Regs.Count(r => r.bump != null) == 4, $"人形塞棉花部位：{string.Join("、", hst.Regs.Select(r => r.name))}");
            StuffAll(hst, .7f, .75f); hu.Advance(); hu.Advance();
            Check(hu.result != null, $"人形交件 {hu.result?.stars}★：{string.Join("、", hu.result.review.Select(x => (x.ok ? "✔" : "✘") + x.text))}");

            // 硬材質（M6 前仍是暫代面板）
            var woodOrder = Orders.Doll.First(o => o.need.Any(n => n.needMat == "wood"));
            var wood = new Session(meta, woodOrder); wood.Advance();
            wood.doll.material = "wood"; wood.doll.fab["body"] = new Fab(DB.Mat("wood"), DB.Col("pine"));
            while (wood.step != Step.Deliver) wood.Advance();
            Check(wood.Steps().SequenceEqual(new[] { Step.Order, Step.Design, Step.Saw, Step.Carve, Step.Sand, Step.Paint, Step.Decor, Step.Deliver }), "木頭步驟正確");
            Check(wood.result.review.Any(r => r.ok && r.text == woodOrder.need.First(n => n.needMat == "wood").label), $"{woodOrder.who}：木頭需求判定符合，{wood.result.stars}★");
            Check(wood.doll.OverallSoft() >= 1, "木頭是硬的");

            var dec = new Session(meta, Orders.Doll[0]);
            dec.doll.fab["body"] = new Fab(DB.Mat("felt"), DB.Col("cream"));
            float before = Evaluator.Tally(dec).Values.Sum();
            dec.decor.Add(DB.Decor[0].id);
            Check(dec.DecorCost() == DB.Decor[0].cost && Evaluator.Tally(dec).Values.Sum() >= before, "配件計入花費與標籤");
        }
    }
}
