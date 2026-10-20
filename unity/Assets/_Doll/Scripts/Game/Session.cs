using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    public enum Step { Order, Design, Layout, Cut, Sew, Stuff, Cast, Trim, Saw, Carve, Sand, Paint, Decor, Deliver }

    /// <summary>跨訂單保存的資料：金錢、聲望、完成件數、客人順序。存檔在 M8 加入。</summary>
    public class Meta
    {
        public int coins = 100, rep, done;
        public bool tutDone;
        /// <summary>看過（按了「知道了」）的步驟提示。</summary>
        public readonly HashSet<Step> tipsSeen = new HashSet<Step>();
        readonly Queue<int> queue = new Queue<int>();

        public OrderDef NextOrder()
        {
            if (!tutDone) return Orders.Doll[0]; // 第一張訂單固定是林太太（新手教學）
            if (queue.Count == 0)
                foreach (var i in Enumerable.Range(0, Orders.Doll.Count).OrderBy(_ => Random.value)) queue.Enqueue(i);
            return Orders.Doll[queue.Dequeue()];
        }
    }

    public class Result
    {
        public float tagS, softS, craft, total;
        public int stars, pay, cost, profit;
        public List<string> comments = new List<string>();
        public List<(string text, bool ok)> review = new List<(string, bool)>();
        public string line;
    }

    /// <summary>
    /// 一張訂單從接單到交件的狀態機。
    /// 製作小遊戲還沒移植的步驟，先用「暫代」數值（品質、棉花量等）讓流程能走完；
    /// 之後每移植一個小遊戲，就由小遊戲把結果寫回這些欄位。
    /// </summary>
    public class Session
    {
        public readonly Meta meta;
        public readonly OrderDef order;
        public readonly DollState doll = new DollState();
        public Step step = Step.Order;

        // 帳本
        public int fabricCost;
        public readonly List<string> decor = new List<string>();

        // 目前步驟的小遊戲（排版、裁布、車縫、塞棉花；其他步驟還是暫代面板時為 null）
        public CraftGame craft;
        public LayoutGame layout;

        // 布偶製作結果（由小遊戲寫入）
        public float cutAcc;
        public int offGrain, otherFlaws;
        public bool leaked;
        public float SewAcc => doll.seams.Count > 0 ? doll.seams.Average(s => s.acc) : 0;
        public bool BackOk => doll.seams.All(s => s.BackOk);

        // 硬材質（M6 移植前的暫代值）
        public readonly Dictionary<string, float> hardAccs = new Dictionary<string, float>();
        public float vinylThickness = .5f;

        public Result result;

        public Session(Meta meta, OrderDef order) { this.meta = meta; this.order = order; }

        public List<Step> Steps()
        {
            if (doll.material == "vinyl") return new List<Step> { Step.Order, Step.Design, Step.Cast, Step.Trim, Step.Paint, Step.Decor, Step.Deliver };
            if (doll.material == "wood") return new List<Step> { Step.Order, Step.Design, Step.Saw, Step.Carve, Step.Sand, Step.Paint, Step.Decor, Step.Deliver };
            return new List<Step> { Step.Order, Step.Design, Step.Layout, Step.Cut, Step.Sew, Step.Stuff, Step.Decor, Step.Deliver };
        }

        public static string StepName(Step s) => s switch
        {
            Step.Order => "接單", Step.Design => "設計", Step.Layout => "排版", Step.Cut => "裁布", Step.Sew => "車縫", Step.Stuff => "塞棉花",
            Step.Cast => "灌模", Step.Trim => "修毛邊", Step.Saw => "鋸外形", Step.Carve => "雕刻", Step.Sand => "砂磨", Step.Paint => "上色",
            Step.Decor => "裝飾", Step.Deliver => "交件", _ => s.ToString(),
        };

        public bool CanAdvance(out string why)
        {
            why = null;
            if (step == Step.Design && !doll.DesignReady) { why = "先選身體的布料和顏色。"; return false; }
            if (step == Step.Layout && !layout.Valid) { why = "布片還沒排好（要全部放在布上、不能重疊）。"; return false; }
            if (step == Step.Layout && order.free && layout.Cost > meta.coins) { why = $"錢不夠買布（需要 ${layout.Cost}）。"; return false; }
            if (craft != null && !craft.Done) { why = "這一步還沒做完。"; return false; }
            return step != Step.Deliver;
        }

        /// <summary>前往下一步，並執行進入該步驟時要做的事。</summary>
        public void Advance()
        {
            if (!CanAdvance(out _)) return;
            if (step == Step.Layout) layout.Commit();
            var list = Steps();
            int i = list.IndexOf(step);
            step = list[Mathf.Min(i + 1, list.Count - 1)];
            OnEnter(step);
        }

        void OnEnter(Step s)
        {
            craft = null;
            switch (s)
            {
                case Step.Layout: doll.previewFill = 0; doll.Touch(); craft = layout = new LayoutGame(this); break;
                case Step.Cut: craft = new CutGame(this, layout); break;
                case Step.Sew: craft = new SewGame(this); break;
                case Step.Stuff: craft = new StuffGame(this); break;
                case Step.Cast:
                case Step.Saw:
                    fabricCost = (doll.material == "vinyl" ? 30 : 20) + doll.ActiveParts().Count() * 4 + (doll.IsHuman ? 8 : 0);
                    doll.regs = doll.RegionsFor();
                    SetHardFill(doll.material == "wood" ? 1f : .4f + .65f * vinylThickness);
                    break;
                case Step.Deliver: Deliver(); break;
            }
        }

        public int DecorCost() => decor.Sum(id => DB.DecorById(id)?.cost ?? 0);
        public int Cost() => fabricCost + DecorCost();

        /// <summary>硬材質的軟硬由外殼厚度決定（木頭是實心的）。</summary>
        public void SetHardFill(float f) { if (doll.regs == null) return; foreach (var r in doll.regs) r.fill = f; doll.Touch(); }

        void Deliver()
        {
            result = Evaluator.Evaluate(this);
            meta.coins += result.profit;
            meta.rep += result.stars - 3;
            meta.done++;
            meta.tutDone = true;
        }
    }
}
