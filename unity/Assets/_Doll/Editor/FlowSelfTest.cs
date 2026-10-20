using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DollWorkshop.EditorTools
{
    /// <summary>不開畫面，直接用程式把幾張訂單從接單跑到交件，檢查狀態機、帳本和評分。</summary>
    public static class FlowSelfTest
    {
        static int fails;
        static void Check(bool ok, string what) { if (!ok) fails++; Debug.Log($"[FlowTest] {(ok ? "PASS" : "FAIL")} {what}"); }

        static Session RunAll(Meta meta, OrderDef order, System.Action<DollState> design)
        {
            var ss = new Session(meta, order);
            ss.Advance(); // 接單 → 設計
            design(ss.doll); ss.doll.Rebuild();
            int guard = 0;
            while (ss.step != Step.Deliver && guard++ < 20) ss.Advance();
            return ss;
        }

        [MenuItem("Doll/流程自我測試")]
        public static void Run()
        {
            fails = 0;
            var meta = new Meta();
            Check(meta.NextOrder() == Orders.Doll[0], "新手教學固定第一位是林太太");

            // 設計還沒選布不能前進
            var s0 = new Session(meta, Orders.Doll[0]); s0.Advance();
            Check(s0.step == Step.Design && !s0.CanAdvance(out _), "沒選身體布料不能離開設計");

            // 布偶：完整走完
            var cloth = RunAll(meta, Orders.Doll[0], d => d.fab["body"] = new Fab(DB.Mat("minky"), DB.Col("cream")));
            Check(cloth.step == Step.Deliver && cloth.result != null, "布偶流程走到交件並產生評分");
            Check(cloth.fabricCost > 0, $"布偶有布料成本（${cloth.fabricCost}）");
            Check(cloth.Steps().Contains(Step.Stuff) && !cloth.Steps().Contains(Step.Cast), "布偶步驟含塞棉花、不含灌模");
            Check(meta.done == 1 && meta.tutDone, "交件後完成件數 +1、新手教學結束");
            Check(meta.coins == 100 + cloth.result.profit, $"錢包加上利潤（{cloth.result.profit}）");
            Check(cloth.result.stars >= 1 && cloth.result.stars <= 5, $"星等在 1–5（{cloth.result.stars}★）");

            // 木頭：王老師指定木頭
            var woodOrder = Orders.Doll.First(o => o.need.Any(n => n.needMat == "wood"));
            var wood = RunAll(meta, woodOrder, d => { d.material = "wood"; d.fab["body"] = new Fab(DB.Mat("wood"), DB.Col("pine")); });
            Check(wood.Steps().SequenceEqual(new[] { Step.Order, Step.Design, Step.Saw, Step.Carve, Step.Sand, Step.Paint, Step.Decor, Step.Deliver }), "木頭步驟正確");
            Check(wood.result.review.Any(r => r.ok && r.text == woodOrder.need.First(n => n.needMat == "wood").label), $"{woodOrder.who}：木頭需求判定符合");

            // 同一張單用布偶交 → 材質不符
            var wrong = RunAll(meta, woodOrder, d => d.fab["body"] = new Fab(DB.Mat("felt"), DB.Col("cream")));
            Check(wrong.result.review.Any(r => !r.ok) && wrong.result.total < wood.result.total + .01f, "材質不符會被扣分");

            // 搪膠
            var vinyl = RunAll(meta, Orders.Doll[1], d => { d.material = "vinyl"; d.fab["body"] = new Fab(DB.Mat("vinyl"), DB.Col("cream")); });
            Check(vinyl.Steps().Contains(Step.Cast) && vinyl.fabricCost >= 30, $"搪膠走灌模、材料費（${vinyl.fabricCost}）");

            // 配件計入花費與標籤
            var dec = new Session(meta, Orders.Doll[0]);
            dec.doll.fab["body"] = new Fab(DB.Mat("felt"), DB.Col("cream"));
            float before = Evaluator.Tally(dec).Values.Sum();
            dec.decor.Add(DB.Decor[0].id);
            Check(dec.DecorCost() == DB.Decor[0].cost && Evaluator.Tally(dec).Values.Sum() >= before, "配件計入花費與標籤");

            // 超出預算
            var over = RunAll(meta, Orders.Doll[0], d => d.fab["body"] = new Fab(DB.Mat("minky"), DB.Col("cream")));
            Check(over.result.review.Any(r => r.text == "在預算內"), "評語含預算項目");

            Debug.Log(fails == 0 ? "[FlowTest] 全部通過" : $"[FlowTest] 有 {fails} 項失敗");
        }
    }
}
