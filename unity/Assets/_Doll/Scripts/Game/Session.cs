using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    public enum Step { Order, Design, GDesign, Layout, Cut, Sew, Stuff, Cast, Trim, Saw, Carve, Sand, Paint, Decor, GDecor, Deliver }

    /// <summary>跨訂單保存的資料：金錢、聲望、完成件數、客人順序。存檔在 M8 加入。</summary>
    public class Meta
    {
        public int coins = 100, rep, done;
        public bool tutDone;
        /// <summary>看過（按了「知道了」）的步驟提示。</summary>
        public readonly HashSet<Step> tipsSeen = new HashSet<Step>();
        /// <summary>自由創作做好的娃娃（展示櫃）與衣服（衣櫃）。</summary>
        public readonly List<DollState> dolls = new List<DollState>();
        public readonly List<Garment> clothes = new List<Garment>();
        readonly Dictionary<string, DollState> mannequins = new Dictionary<string, DollState>();

        /// <summary>某個身形的人台（空白的人形模特兒，衣服試穿用）。</summary>
        public DollState Mannequin(string cls)
        {
            if (!mannequins.TryGetValue(cls, out var d))
            {
                var c = Garments.ClassDims(cls);
                mannequins[cls] = d = Garments.BuildDoll(new DollSpec { torsoW = c.w, torsoH = c.h, skin = "cream", skinMat = "felt", name = $"人台・{cls}" });
            }
            return d;
        }

        /// <summary>某個頭圍的人台（做帽子用）。</summary>
        public DollState MannequinHead(string hcls)
        {
            if (!mannequins.TryGetValue("H" + hcls, out var d))
                mannequins["H" + hcls] = d = Garments.BuildDoll(new DollSpec { headSize = Garments.SizeOf(hcls), skin = "cream", skinMat = "felt", name = $"人台・{hcls}" });
            return d;
        }

        /// <summary>做這件衣服時用的人台。</summary>
        public DollState CraftModel(Garment g) => g.T.sizeBy == "head" ? MannequinHead(g.cls ?? "頭圍 M") : g.T.sizeBy == "body" ? Mannequin(g.cls ?? "20cm 正常體") : Mannequin("20cm 正常體");

        /// <summary>衣櫃裡可以換衣服的娃娃：六種身形人台、三種頭圍人台、自己做的娃娃。</summary>
        public List<DollState> ClosetModels() =>
            Garments.Classes.Select(c => Mannequin(c.name)).Concat(Garments.HClasses.Select(h => MannequinHead(h.name))).Concat(dolls).ToList();
        readonly Queue<int> queue = new Queue<int>();

        public OrderDef NextOrder()
        {
            if (!tutDone) return Orders.Doll[0]; // 第一張訂單固定是林太太（新手教學）
            if (queue.Count == 0)
                foreach (var i in Enumerable.Range(0, Orders.All.Count).OrderBy(_ => Random.value)) queue.Enqueue(i);
            return Orders.All[queue.Dequeue()];
        }
    }

    public class Result
    {
        public float tagS, softS, craft, total;
        public int stars, pay, cost, profit;
        public List<string> comments = new List<string>();
        public List<(string text, bool ok)> review = new List<(string, bool)>();
        public string line;
        /// <summary>free：自由創作（不評分，只看手工）；fit：衣服訂單的柔軟度欄位改成「合身」。</summary>
        public bool free, fitLabel;
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

        // 衣服訂單：做的衣服、客人的娃娃、裝飾中的衣服配件（畫布座標）
        public readonly Garment g;
        public readonly DollState model;
        public readonly List<DecorItem> gItems = new List<DecorItem>();
        public bool IsGarment => order.IsCloth;
        /// <summary>畫面上要顯示的娃娃：做娃娃時是這隻娃娃；做衣服時是客人的娃娃（自由創作時是人台）。</summary>
        public DollState ViewDoll => IsGarment ? (order.free || model == null ? meta.CraftModel(g) : model) : doll;
        DollState previewOn;

        // 帳本
        public int fabricCost;

        // 目前步驟的小遊戲（排版、裁布、車縫、塞棉花；其他步驟還是暫代面板時為 null）
        public CraftGame craft;
        public LayoutGame layout;

        // 布偶製作結果（由小遊戲寫入）
        public float cutAcc;
        public int offGrain, otherFlaws;
        public bool leaked;
        public List<Seam> Seams => IsGarment ? g.seams : doll.seams;
        public float SewAcc => Seams.Count > 0 ? Seams.Average(s => s.acc) : 0;
        public bool BackOk => Seams.All(s => s.BackOk);

        // 硬材質（M6 移植前的暫代值）
        public readonly Dictionary<string, float> hardAccs = new Dictionary<string, float>();
        public float vinylThickness = .5f;

        public Result result;

        public Session(Meta meta, OrderDef order)
        {
            this.meta = meta; this.order = order;
            if (IsGarment)
            {
                g = Garments.New(order.free ? "20cm 正常體" : null);
                if (!order.free && order.doll != null) model = Garments.BuildDoll(order.doll);
            }
        }

        /// <summary>直接從某一步開始（自由創作跳過接單）。</summary>
        public void Begin(Step s) { step = s; OnEnter(s); }

        /// <summary>讓畫面上的娃娃試穿正在做的衣服（換人台時，舊人台上的試穿拿掉）。</summary>
        public void SyncPreview()
        {
            var v = IsGarment && step != Step.Order ? ViewDoll : null;
            if (previewOn != null && previewOn != v) { previewOn.preview = null; previewOn.Touch(); }
            if (v != null && v.preview != g) { v.preview = g; v.Touch(); }
            previewOn = v;
        }

        public void ClearPreview() { if (previewOn != null) { previewOn.preview = null; previewOn.Touch(); previewOn = null; } }

        public Fab FabOfSlot(string slot) => slot == "garment" ? g.fab : doll.FabOf(slot);

        public List<Step> Steps()
        {
            if (IsGarment) return new List<Step> { Step.Order, Step.GDesign, Step.Layout, Step.Cut, Step.Sew, Step.GDecor, Step.Deliver };
            if (doll.material == "vinyl") return new List<Step> { Step.Order, Step.Design, Step.Cast, Step.Trim, Step.Paint, Step.Decor, Step.Deliver };
            if (doll.material == "wood") return new List<Step> { Step.Order, Step.Design, Step.Saw, Step.Carve, Step.Sand, Step.Paint, Step.Decor, Step.Deliver };
            return new List<Step> { Step.Order, Step.Design, Step.Layout, Step.Cut, Step.Sew, Step.Stuff, Step.Decor, Step.Deliver };
        }

        public static string StepName(Step s) => s switch
        {
            Step.Order => "接單", Step.Design => "設計", Step.GDesign => "設計衣服", Step.GDecor => "裝飾", Step.Layout => "排版", Step.Cut => "裁布", Step.Sew => "車縫", Step.Stuff => "塞棉花",
            Step.Cast => "灌模", Step.Trim => "修毛邊", Step.Saw => "鋸外形", Step.Carve => "雕刻", Step.Sand => "砂磨", Step.Paint => "上色",
            Step.Decor => "裝飾", Step.Deliver => "交件", _ => s.ToString(),
        };

        public bool CanAdvance(out string why)
        {
            why = null;
            if (step == Step.Design && !doll.DesignReady) { why = "先選身體的布料和顏色。"; return false; }
            if (step == Step.GDesign && (g.cls == null || g.fab.mat == null || g.fab.color == null)) { why = "先選尺寸、布料和顏色。"; return false; }
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
            if (step == Step.GDecor)
            {
                // 衣服上的裝飾改存成相對位置，換給別的娃娃穿也會跟著走
                var a = Garments.AnchorOn(g, ViewDoll); g.trims.Clear();
                foreach (var it in gItems) g.trims.Add(new DecorItem { type = it.type, x = it.x - a.cx, y = it.y - a.cy, s = it.s, rot = it.rot });
            }
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
                case Step.Decor: case Step.GDecor: craft = new DecorGame(this); break;
                case Step.GDesign: craft = new GarmentDesign(this); break;
                case Step.Deliver: Deliver(); craft = new DeliverStage(this); break;
            }
        }

        public int DecorCost() => (IsGarment ? gItems : doll.decor).Sum(it => DB.DecorById(it.type)?.cost ?? 0);
        public int Cost() => fabricCost + DecorCost();

        /// <summary>硬材質的軟硬由外殼厚度決定（木頭是實心的）。</summary>
        public void SetHardFill(float f) { if (doll.regs == null) return; foreach (var r in doll.regs) r.fill = f; doll.Touch(); }

        void Deliver()
        {
            if (IsGarment)
            {
                g.done = true; g.id = g.id ?? System.Guid.NewGuid().ToString("N");
                g.name = $"{g.T.name}・{g.fab.color.name}";
            }
            if (order.free)
            {
                // 自由創作：用自己的錢買材料，做好收進衣櫃或展示櫃
                result = Evaluator.EvaluateFree(this);
                meta.coins -= result.cost;
                if (IsGarment) meta.clothes.Add(g);
                else { doll.name = $"娃娃 {meta.dolls.Count + 1}"; meta.dolls.Add(doll); }
                return;
            }
            result = IsGarment ? Evaluator.EvaluateGarment(this) : Evaluator.Evaluate(this);
            meta.coins += result.profit;
            meta.rep += result.stars - 3;
            meta.done++;
            meta.tutDone = true;
        }
    }
}
