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

        // 製作結果（暫代值；小遊戲移植後由小遊戲寫入）
        public float cutAcc = .8f, sewAcc = .8f;
        public bool backOk = true;
        public float bodyFill = .6f, partFill = .6f, lump;
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
            if (step == Step.Layout && order.free && fabricCost > meta.coins) { why = $"錢不夠買布（需要 ${fabricCost}）。"; return false; }
            return step != Step.Deliver;
        }

        /// <summary>前往下一步，並執行進入該步驟時要做的事。</summary>
        public void Advance()
        {
            if (!CanAdvance(out _)) return;
            var list = Steps();
            int i = list.IndexOf(step);
            step = list[Mathf.Min(i + 1, list.Count - 1)];
            OnEnter(step);
        }

        void OnEnter(Step s)
        {
            switch (s)
            {
                case Step.Layout: fabricCost = EstimateFabricCost(); break;
                case Step.Cast:
                case Step.Saw:
                    fabricCost = (doll.material == "vinyl" ? 30 : 20) + doll.ActiveParts().Count() * 4 + (doll.IsHuman ? 8 : 0);
                    if (doll.material == "wood") { bodyFill = partFill = 1f; }
                    break;
                case Step.Deliver: Deliver(); break;
            }
        }

        public int DecorCost() => decor.Sum(id => DB.DecorById(id)?.cost ?? 0);
        public int Cost() => fabricCost + DecorCost();

        /// <summary>
        /// 排版小遊戲移植前的布料估價：依每種布的布片面積（含前後片、對折份數）換算布長。
        /// 網頁版是玩家實際排版、越省布越便宜；這裡假設排得普通（利用率 60%）。
        /// </summary>
        public int EstimateFabricCost()
        {
            var area = new Dictionary<string, (MatDef mat, float a)>();
            void Add(string slot, IEnumerable<List<Vector2>> polys, int mult)
            {
                var f = doll.FabOf(slot); if (f.mat == null || f.color == null) return;
                float a = polys.Sum(PolyArea) * mult;
                string k = f.mat.id + f.color.id;
                area[k] = area.TryGetValue(k, out var v) ? (v.mat, v.a + a) : (f.mat, a);
            }
            Add("body", doll.Mains().Select(m => doll.MainOutline(m, 120)), 2);
            var hg = doll.GetHair();
            if (hg != null) { var hp = new List<List<Vector2>> { hg.cap }; if (hg.back != null) hp.Add(hg.back); if (hg.tails != null) hp.AddRange(hg.tails); Add("hair", hp, 1); }
            foreach (var k in doll.ActiveParts()) Add(k, new[] { doll.PartPoly(k, 1) }, 4);
            if (doll.parts["ears"].inner && !doll.parts["ears"].IsNone) Add("earInner", new[] { doll.InnerPoly("ears", 1) }, 2);
            if (!doll.parts["belly"].IsNone) Add("belly", new[] { doll.BellyPoly() }, 1);
            int cost = 0;
            foreach (var v in area.Values)
            {
                // 布寬 230cm（排版畫面）＝實際 460 像素；1cm = 2 像素
                float lenCm = v.a / 460f / .6f / 2f;
                cost += Mathf.CeilToInt(lenCm / 10f) * (int)v.mat.p10;
            }
            return cost;
        }

        static float PolyArea(List<Vector2> P)
        {
            float s = 0; for (int i = 0, j = P.Count - 1; i < P.Count; j = i++) s += P[j].x * P[i].y - P[i].x * P[j].y;
            return Mathf.Abs(s) / 2f;
        }

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
