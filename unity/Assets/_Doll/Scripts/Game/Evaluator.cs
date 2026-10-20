using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>
    /// 交件評分（從網頁原型移植）。系統看不懂美感，只比對「標籤」：
    /// 造型、部件、比例、布料、顏色、配件都帶有隱藏標籤，客人的需求就是一組標籤條件。
    /// </summary>
    public static class Evaluator
    {
        public static readonly string[] Tags = { "可愛", "溫柔", "活潑", "酷", "復古", "療癒", "高級", "好笑", "手作感" };

        public static Dictionary<string, float> Tally(Session ss)
        {
            var d = ss.doll;
            var t = Tags.ToDictionary(x => x, x => 0f);
            void Add(IEnumerable<string> a, float w = 1) { foreach (var x in a) if (t.ContainsKey(x)) t[x] += w; }

            if (d.IsHuman) { Add(d.head.tags); Add(d.hair.tags); } else Add(d.body.tags);
            foreach (var o in d.parts.Values) Add(o.tags);

            var s = d.shp;
            if (d.IsHuman)
            {
                if (s["head"]["size"] > 1.1f) Add(new[] { "可愛" });
                if (s["torso"]["w"] > 1.3f) Add(new[] { "療癒" });
                if (s["torso"]["legL"] > 1f) Add(new[] { "好笑" });
                if (s["torso"]["h"] > 1.2f && s["head"]["size"] < .9f) Add(new[] { "高級" });
            }
            else
            {
                if (s["body"]["w"] > 1.25f) Add(new[] { "療癒" });
                if (s["body"]["h"] > 1.25f) Add(new[] { "好笑" });
                if (s["body"]["sq"] > .6f) Add(new[] { "酷" });
            }

            if (d.DesignReady)
            {
                var fabs = d.UsedSlots().Select(d.FabOf).Where(f => f.mat != null && f.color != null).ToList();
                foreach (var m in fabs.Select(f => f.mat).Distinct()) Add(m.tags);
                var bc = d.FabOf("body").color;
                Add(bc.tags);
                foreach (var c in fabs.Select(f => f.color).Distinct().Where(c => c != bc)) Add(c.tags, .5f);
                if (fabs.Select(f => f.mat.id + f.color.id).Distinct().Count() >= 3) Add(new[] { "手作感", "活潑" });
            }
            foreach (var it in d.decor) { var dd = DB.DecorById(it.type); if (dd != null) Add(dd.tags); }
            return t;
        }

        static bool NeedOk(Need n, float v) => (n.min == null || v >= n.min) && (n.max == null || v <= n.max);

        static string NeedText(Need n) =>
            n.max != null && n.min == null ? $"不要太「{n.tag}」" : n.max != null ? $"「{n.tag}」但不要太過" : $"「{n.tag}」";

        public static string SoftLabel(float p) => p < .3f ? "扁塌塌" : p < .7f ? "軟綿綿" : p < .95f ? "飽滿" : p <= 1.05f ? "硬挺" : "快撐破了";

        public static Result Evaluate(Session ss)
        {
            var d = ss.doll; var o = ss.order; var r = new Result();
            var t = Tally(ss);
            var reqS = new List<float>();
            foreach (var n in o.need)
            {
                if (n.needMat != null) { bool ok = d.material == n.needMat; r.review.Add((n.label, ok)); if (!ok) r.comments.Add($"我想要的是{n.label}的耶……"); reqS.Add(ok ? 1 : 0); continue; }
                if (n.needKind != null) { bool ok = d.kind == n.needKind; r.review.Add((n.label, ok)); if (!ok) r.comments.Add($"我想要的是{n.label}……"); reqS.Add(ok ? 1 : 0); continue; }
                float v = t[n.tag], s = 1;
                if (n.min != null && v < n.min) { s = Mathf.Min(s, v / n.min.Value); r.comments.Add($"感覺不夠「{n.tag}」……"); }
                if (n.max != null && v > n.max) { s = Mathf.Min(s, Mathf.Max(0, 1 - (v - n.max.Value) * .4f)); r.comments.Add($"太「{n.tag}」了啦。"); }
                r.review.Add((NeedText(n), NeedOk(n, v)));
                reqS.Add(s);
            }
            r.tagS = reqS.Count > 0 ? reqS.Average() : 1;

            var (label, lo, hi) = Orders.Soft[o.soft];
            var regs = d.regs ?? new List<StuffRegion>();
            float p = d.OverallSoft();
            r.softS = p >= lo && p <= hi ? 1 : Mathf.Max(0, 1 - Mathf.Min(Mathf.Abs(p - lo), Mathf.Abs(p - hi)) * 3);
            r.review.Add(($"柔軟度：{label}", p >= lo && p <= hi));
            if (p < lo) r.comments.Add("有點扁，抱起來沒什麼感覺。");
            else if (p > hi) r.comments.Add(p > 1.05f ? "縫線好像快被撐開了……" : "比我想的還要硬耶。");
            var small = regs.Where(x => x.small).ToList();
            foreach (var x in small) if (x.fill < .3f) { r.softS = Mathf.Max(0, r.softS - .12f); r.comments.Add($"{x.name}扁扁的，好像沒塞到棉花。"); }
            foreach (var g in small.GroupBy(x => x.name.Substring(1)))
            {
                var f = g.Select(x => x.fill).ToList();
                if (f.Count == 2 && Mathf.Abs(f[0] - f[1]) > .35f) r.comments.Add($"兩邊的{g.Key}一大一小耶。");
            }
            var mainR = regs.Where(x => x.IsMainBody).ToList();
            float lumpAvg = mainR.Count > 0 ? mainR.Average(x => x.lump) : 0;
            if (lumpAvg > .3f) r.comments.Add("摸起來凹凸不平的。");

            float craft;
            if (d.IsHard) craft = ss.hardAccs.Count > 0 ? ss.hardAccs.Values.Average() : .8f;
            else craft = (ss.cutAcc + ss.SewAcc) / 2f * (1 - .35f * lumpAvg);
            if (ss.offGrain > 0) { craft -= Mathf.Min(.3f, ss.offGrain * .06f); r.comments.Add("有些布片的布紋方向不對，摸起來怪怪的。"); }
            if (d.stains.Count > 0) { craft -= d.stains.Count * .12f; r.comments.Add("這裡怎麼有一塊污漬？"); }
            else if (ss.otherFlaws > 0) { craft -= .05f; r.comments.Add("有個地方好像有點髒。"); }
            if (!ss.BackOk) { craft -= .08f; r.comments.Add(ss.leaked ? "縫線開頭鬆了，棉花一直跑出來。" : "線頭鬆鬆的，會不會散開啊？"); }
            if (regs.Any(x => x.fill > 1.05f)) craft *= .8f;
            r.craft = Mathf.Clamp01(craft);
            if (r.craft < .55f) r.comments.Add(d.IsHard ? "做得有點粗糙。" : "縫得有點粗糙。");

            r.cost = ss.Cost();
            bool over = r.cost > o.budget;
            r.review.Add(("在預算內", !over));
            if (over) r.comments.Add($"超出預算了（${r.cost}），我只能付原本說好的錢。");

            r.total = Mathf.Clamp01(.5f * r.tagS + .2f * r.softS + .3f * r.craft - (over ? .15f : 0));
            r.stars = Mathf.Max(1, Mathf.RoundToInt(r.total * 5));
            r.pay = Mathf.RoundToInt(o.budget * (r.stars >= 2 ? .5f + .5f * r.total : .2f));
            r.profit = r.pay - r.cost;
            r.line = r.stars >= 4 ? o.good : r.stars == 3 ? o.mid : o.bad;
            return r;
        }
    }
}
