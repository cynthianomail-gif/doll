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
            ss.Advance();
            var dg1 = (DecorGame)ss.craft; dg1.Add("eyes_button"); dg1.Add("mouth_smile"); dg1.Add("blush");
            Check(ss.step == Step.Decor && ss.Cost() == ss.fabricCost + 28, $"裝飾：加了鈕扣眼、微笑、腮紅（合計 ${ss.Cost()}）");
            ss.Advance();
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

            var woodOrder = RunHard(meta);

            // ---------- 裝飾 ----------
            var dec = new Session(meta, Orders.Doll[0]);
            dec.doll.fab["body"] = new Fab(DB.Mat("felt"), DB.Col("cream"));
            var dg = new DecorGame(dec);
            float before = Evaluator.Tally(dec)["可愛"];
            var eyes = dg.Add("eyes_sparkle");
            Check(dec.DecorCost() == 30 && Evaluator.Tally(dec)["可愛"] == before + 1, "閃亮大眼：花費 $30、多一分「可愛」");
            Check(eyes.Pos == dec.doll.DecorPos("eyes") && eyes.Pos.y < SM.CY, "眼睛加在臉上（身體中心上方）");
            var eyes2 = dg.Add("eyes_dot");
            Check(eyes2.x == eyes.x + 12 && eyes2.y == eyes.y + 10, "同一個位置的第二個配件會錯開");
            var bow = dg.Add("bow");
            Check(dg.HitAt(bow.Pos + new Vector2(10, 0)) == 2 && dg.HitAt(new Vector2(50, 50)) == -1, "點選範圍判定");
            dg.Down(bow.Pos + new Vector2(5, 0)); dg.Move(new Vector2(300, 250), bow.Pos); dg.Up(new Vector2(300, 250));
            Check(Mathf.Abs(bow.x - 295) < .01f && Mathf.Abs(bow.y - 250) < .01f, "拖曳移動配件");
            for (int i = 0; i < 30; i++) dg.Act("+");
            Check(Mathf.Approximately(bow.s, 2.6f), "放大有上限");
            dg.sel = dec.doll.decor.IndexOf(eyes2); dg.Act("del");
            Check(dec.doll.decor.Count == 2 && !dec.doll.decor.Contains(eyes2) && dec.DecorCost() == 55, "刪除配件，花費跟著扣掉");
            var hd = new DollState { kind = "human" }; hd.Rebuild();
            var hm = hd.GetMain("head");
            Check(Vector2.Distance(hd.DecorPos("eyes"), new Vector2(hm.cx, hm.cy)) < hm.r, "人形娃娃的眼睛在頭上");
            foreach (var d in DB.Decor) { var mb = new MeshBuilder(); DecorArt.Draw(mb, new DecorItem { type = d.id, x = 400, y = 300 }); if (mb.v.Count == 0) Check(false, $"{d.name} 沒有圖案"); }
            Check(true, $"{DB.Decor.Count} 種配件都有圖案");

            // ---------- 交件演出 ----------
            var ds = (DeliverStage)hu.craft;
            Check(ds != null && ds.DollOffset.x < 0, "交件時娃娃移到左邊的展示台");
            Check((hu.result.stars >= 4) == (ds.Mood == "happy") && (hu.result.stars >= 4) == (ds.ConfettiCount > 0), $"客人表情：{ds.Mood}，彩紙 {ds.ConfettiCount} 片");
            foreach (var (stars, mood, conf) in new[] { (1, "sad", false), (2, "sad", false), (3, "ok", false), (4, "happy", true), (5, "happy", true) })
            {
                var fake = new Session(meta, Orders.Doll[0]) { result = new Result { stars = stars, line = "…" } };
                var sd = new DeliverStage(fake);
                if (sd.Mood != mood || (sd.ConfettiCount > 0) != conf) Check(false, $"{stars}★ 的客人反應不對（{sd.Mood}）");
            }
            Check(true, "1–2★ 冒冷汗、3★ 普通、4–5★ 開心＋彩紙");

            RunGarments(meta);
            RunBlindBox(meta);
        }

        static void RunBlindBox(Meta meta)
        {
            var a = Garments.BuildDoll(new DollSpec { name = "小A", hair = "bob", decor = new[] { "eyes_dot" } });
            var b = Garments.BuildDoll(new DollSpec { name = "小B", hair = "twin", hairColor = "milktea" });
            var c = Garments.BuildDoll(new DollSpec { name = "小C", hair = "hime", torsoW = 1.4f });
            meta.dolls.AddRange(new[] { a, b, c });
            var ss = new Session(meta, Orders.Free(false));
            var sc = new SeriesScreen(ss); var se = sc.NewSeries();
            Check(meta.CurSeries == se && se.name.StartsWith("我的系列"), $"新系列：{se.name}");
            se.Toggle(a);
            Check(!se.CanOpen && new UnboxStage(ss, se).Open() != null, "只有 1 款不能開盒");
            se.Toggle(b); se.Toggle(c);
            se.SetRarity(se.dolls[1], "rare"); se.SetRarity(se.dolls[2], "secret");
            float tot = 1 + .35f + .08f;
            Check(Mathf.Abs(se.Odds(se.dolls[2]) - .08f / tot) < 1e-4f && Mathf.Abs(se.dolls.Sum(se.Odds) - 1) < 1e-4f, $"機率：普通 {se.Odds(se.dolls[0]):P0}、稀有 {se.Odds(se.dolls[1]):P0}、隱藏款 {se.Odds(se.dolls[2]):P1}");
            se.SetRarity(se.dolls[0], "secret");
            Check(se.dolls[0].rarity == "secret" && se.dolls[2].rarity == "rare", "隱藏款只能一款，原本的變成稀有");
            se.SetRarity(se.dolls[0], "common"); se.SetRarity(se.dolls[2], "secret");
            Check(se.Pick(.01f).doll == a && se.Pick(.999f).doll == c, "依機率抽到對應的娃娃");
            var rng = new System.Random(7); int secret = 0; for (int i = 0; i < 20000; i++) if (se.Pick((float)rng.NextDouble()).rarity == "secret") secret++;
            Check(Mathf.Abs(secret / 20000f - .08f / tot) < .01f, $"抽兩萬次，隱藏款出現 {secret / 200f:F1}%（應該約 {8f / tot:F1}%）");
            var big = new BlindSeries { name = "大系列" };
            for (int i = 0; i < 7; i++) big.Toggle(Garments.BuildDoll(new DollSpec { name = "x" + i }));
            Check(big.Toggle(a) != null && big.dolls.Count == BlindSeries.Max, "一個系列最多 7 款");

            meta.coins = 50;
            var u = new UnboxStage(ss, se);
            Check(u.Open(.75f) == null && meta.coins == 40 && se.opened == 1 && u.doll == b && u.rar == "rare", $"開一盒：花 $10，抽到 {u.doll.name}");
            u.Tick(1);
            Check(u.phase == "lid", "盒子晃完，輪到撕盒蓋");
            u.ptr.down = true; u.Move(new Vector2(400, 100), new Vector2(400, 300));
            Check(u.phase == "lidfly", "往上拖撕開盒蓋");
            u.Tick(.6f); u.Tick(.7f);
            Check(u.phase == "bag" && !u.Gallery().Any(), "鋁箔袋升起來，還看不到娃娃");
            u.Move(new Vector2(560, 300), new Vector2(260, 300));
            Check(u.phase == "suspense", "左右拖撕開袋子");
            u.Tick(1.2f); u.Tick(1.9f);
            Check(u.phase == "card" && se.owned[b] == 1 && se.Collected == 1 && u.Gallery().Count() == 1, "稀有款懸疑比較久，揭曉後收集卡記下進度 1/3");
            u.streamMode = true; u.Down(new Vector2(400, 300));
            Check(u.phase == "shake" && meta.coins == 30 && se.opened == 2, "直播模式點畫面再開一盒");
            meta.coins = 5;
            Check(new UnboxStage(ss, se).Open() == "錢不夠開盒了", "錢不夠不能開");
            Check(sc.Gallery().Count() == 3 && sc.Gallery().Count(g => g.silhouette) == 1, "系列一覽：3 款，隱藏款畫成剪影");

            // 繪圖：盒子五種圖案、鋁箔袋、剪影娃娃
            var go = new GameObject("test sketch"); var sk = go.AddComponent<Sketch>();
            var gr = new GameObject("test silhouette").AddComponent<DollRenderer>();
            try
            {
                sk.Begin();
                foreach (var (id, _) in BoxStyle.Patterns) { var bs = new BoxStyle { pattern = id, color = "sky", pcolor = "white" }; BoxArt.Draw(sk, 100, 200, 180, 200, 60, bs, .5f, true, 3); }
                BoxArt.Bag(sk, 300, 150, 150, 190, .4f, 0, 0, 284);
                sk.End();
                Check(go.transform.childCount > 0, $"5 種圖案的盒子和鋁箔袋畫得出來（{go.transform.childCount} 批）");
                gr.state = c; gr.silhouette = new Color(.231f, .2f, .259f); gr.Build();
                var mf = gr.GetComponentInChildren<MeshFilter>(); var cols = mf.sharedMesh.colors; var want = FabricTextures.Lin(new Color(.231f, .2f, .259f));
                Check(cols.Length > 0 && cols.All(x => Mathf.Abs(x.r - want.r) < .01f && Mathf.Abs(x.b - want.b) < .01f), "剪影：整隻娃娃同一個顏色");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(gr.gameObject); }
        }

        // ---------- 硬材質：程式代玩 ----------
        static Session NewHard(Meta meta, OrderDef order, string mat)
        {
            var ss = new Session(meta, order); ss.Advance();
            ss.doll.material = mat; ss.doll.fab["body"] = new Fab(DB.Mat(mat), DB.Col(mat == "wood" ? "pine" : "pink"));
            ss.doll.parts["ears"] = DB.Parts["ears"].Find("round"); ss.doll.ResetAdj("ears");
            ss.doll.Rebuild(); ss.Advance();
            return ss;
        }

        static void Cast(CastGame c, float pour, float bake, float drainSec)
        {
            c.ptr.down = true; for (int i = 0; i < Mathf.RoundToInt(pour / .03f); i++) c.Tick(.1f); c.ptr.down = false;
            c.PourDone();
            for (int i = 0; i < Mathf.RoundToInt(bake * 50); i++) c.Tick(.1f);
            c.BakeStop();
            c.ptr.down = true; for (int i = 0; i < Mathf.RoundToInt(drainSec * 10); i++) c.Tick(.1f); c.ptr.down = false;
            c.DrainDone();
        }

        static void TrimAll(TrimGame t) { foreach (var f in t.flashes.ToList()) t.Process(f.p + f.n * f.len * .5f); }

        static void SawAll(SawGame s) { s.ptr.speed = .1f; foreach (var p in s.pts) s.Process(p); }

        static void CarveAll(CarveGame c) { for (int k = 0; k < c.R.Length && !c.done; k++) if (c.R[k]) c.Process(c.Grid.Center(k)); }

        static void SandAll(SandGame s)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                s.coarse = pass == 0;
                for (int guard = 0; guard < 60 && !s.done; guard++)
                    for (int k = 0; k < s.r.Length && !s.done; k += 3) if (s.r[k] > 0) s.Process(s.Grid.Center(k), 10);
            }
        }

        static OrderDef RunHard(Meta meta)
        {
            // ---------- 搪膠 ----------
            var ivy = Orders.Doll.First(o => o.need.Any(n => n.needMat == "vinyl"));
            var v = NewHard(meta, ivy, "vinyl");
            var cast = (CastGame)v.craft;
            Check(v.step == Step.Cast && !cast.ShowDoll && !v.CanAdvance(out _), "搪膠從灌模開始，模具裡還看不到娃娃");
            Cast(cast, 1f, .65f, 1.5f);
            Check(cast.Done && v.hardAccs["pour"] > .9f && v.hardAccs["bake"] > .9f && v.holes == 0 && !v.sticky && v.doll.paint.tint == 0, "灌到虛線、烤剛好：沒缺洞、沒焦黃");
            Check(Mathf.Abs(v.doll.OverallSoft() - (.4f + .65f * cast.thick)) < .01f && cast.thick < .6f, $"倒膠 1.5 秒：外殼變薄（{cast.thick:F2}），手感 {Evaluator.SoftLabel(v.doll.OverallSoft())}");
            var bad = NewHard(meta, ivy, "vinyl"); var bc = (CastGame)bad.craft;
            Cast(bc, .6f, .95f, 0);
            Check(bad.holes > 0 && bad.doll.paint.tint > 0 && bc.thick == 1, $"灌太少有缺洞（{bad.holes}）、烤太久焦黃、沒倒膠很硬");
            var raw = NewHard(meta, ivy, "vinyl"); Cast((CastGame)raw.craft, 1.2f, .3f, 0);
            Check(raw.sticky && raw.waste > 0, $"烤太短黏黏的，灌太多浪費 ${raw.Cost() - raw.fabricCost}");
            v.Advance();
            var trim = (TrimGame)v.craft;
            Check(trim.flashes.Count == 14, "修毛邊：14 處毛邊");
            var m = v.doll.GetMain("body"); trim.Process(new Vector2(m.cx, m.cy));
            Check(trim.nicks.Count == 1, "刀子切進娃娃中間留下刀痕");
            TrimAll(trim);
            Check(trim.Done && v.hardAccs["trim"] < 1, $"毛邊修完，品質 {v.hardAccs["trim"]:P0}");
            v.Advance();
            var paint = (PaintGame)v.craft;
            var layer0 = PaintLayer.For(v.doll).PaintedPixels();
            Check(layer0 > 0, $"搪膠有光澤（上色層 {layer0} 像素）");
            paint.Down(new Vector2(20, 20)); paint.Move(new Vector2(60, 20), new Vector2(20, 20));
            Check(PaintLayer.For(v.doll).PaintedPixels() == layer0, "畫在娃娃外面不會留下顏色");
            paint.Undo();
            paint.color = PaintGame.Paints.First(c => c.id == "black"); paint.Down(new Vector2(m.cx - 30, m.cy)); paint.Move(new Vector2(m.cx + 30, m.cy), new Vector2(m.cx - 30, m.cy));
            paint.color = DB.Col("sky"); paint.spray = true; paint.Down(new Vector2(m.cx, m.cy + 30));
            Check(PaintLayer.For(v.doll).PaintedPixels() > layer0 && v.PaintCost() == 4, $"在娃娃身上畫兩種顏色：顏料費 ${v.PaintCost()}");
            v.Advance(); v.Advance();
            Check(v.step == Step.Deliver && v.result != null, $"{ivy.who}：搪膠交件 {v.result.stars}★，手工 {v.result.craft:P0}，評語：{string.Join(" / ", v.result.comments)}");

            // ---------- 木頭 ----------
            var woodOrder = Orders.Doll.First(o => o.need.Any(n => n.needMat == "wood"));
            var w = NewHard(meta, woodOrder, "wood");
            Check(w.Steps().SequenceEqual(new[] { Step.Order, Step.Design, Step.Saw, Step.Carve, Step.Sand, Step.Paint, Step.Decor, Step.Deliver }), "木頭步驟正確");
            var saw = (SawGame)w.craft;
            saw.ptr.speed = 2; saw.Process(saw.pts[0]);
            Check(saw.jams == 1 && !saw.cov[0], "鋸太快會卡住");
            CraftGame.Clock = () => 1e6f; SawAll(saw); CraftGame.Clock = () => clock;
            Check(saw.Done && w.hardAccs["saw"] > .9f, $"沿線鋸完，品質 {w.hardAccs["saw"]:P0}");
            w.Advance();
            var carve = (CarveGame)w.craft; var wm = w.doll.GetMain("body");
            carve.Process(new Vector2(wm.cx, wm.cy));
            Check(carve.gouges.Count == 1, "鑿進娃娃中間留下鑿痕");
            CarveAll(carve);
            Check(carve.Done && carve.removed >= carve.total * .96f, $"刮掉多餘的木頭（{carve.total} 格）");
            w.Advance();
            var sand = (SandGame)w.craft;
            sand.coarse = true; for (int i = 0; i < 40; i++) for (int k = 0; k < sand.r.Length; k += 3) if (sand.r[k] > 0) sand.Process(sand.Grid.Center(k), 10);
            Check(!sand.Done && sand.Rough > .3f, $"只用粗砂紙磨不光（粗糙度 {sand.Rough:F2}）");
            SandAll(sand);
            Check(sand.Done, $"換細砂紙磨光（品質 {w.hardAccs["sand"]:P0}）");
            w.Advance();
            var wp = (PaintGame)w.craft;
            float before = Evaluator.Tally(w)["高級"];
            w.doll.paint.varnish = true; w.doll.paint.Touch();
            Check(Evaluator.Tally(w)["高級"] == before + 1 && w.PaintCost() == 5, "上亮光漆：多一分「高級」、$5");
            w.Advance(); w.Advance();
            Check(w.result.review.Any(r => r.ok && r.text == woodOrder.need.First(n => n.needMat == "wood").label) && w.doll.OverallSoft() >= 1, $"{woodOrder.who}：木頭交件 {w.result.stars}★，手工 {w.result.craft:P0}，評語：{string.Join(" / ", w.result.comments)}");
            return woodOrder;
        }

        static Session GarmentRun(Meta meta, OrderDef o, string type, string cls, string mat, string color, params string[] trims)
        {
            var ss = new Session(meta, o);
            if (o.free) ss.Begin(Step.GDesign); else ss.Advance();
            var gd = (GarmentDesign)ss.craft;
            gd.SetType(type); if (cls != null) gd.SetCls(cls); gd.SetMat(DB.Mat(mat)); gd.SetColor(DB.Col(color));
            ss.Advance(); ss.layout.AutoPack(); ss.Advance(); CutAll(ss); ss.Advance(); SewAll((SewGame)ss.craft, true); ss.Advance();
            var dg = (DecorGame)ss.craft; foreach (var t in trims) dg.Add(t);
            ss.Advance();
            return ss;
        }

        static void RunGarments(Meta meta)
        {
            // ---------- 衣服版型 ----------
            var man = meta.Mannequin("20cm 正常體");
            foreach (var t in Garments.Types)
            {
                var g = Garments.New(null); Garments.SetType(g, t.id, true);
                var P = Garments.PolyOn(g, man);
                if (P.Count < 3 || P.Any(p => float.IsNaN(p.x) || float.IsNaN(p.y))) Check(false, $"{t.name} 版型算不出來");
            }
            Check(true, $"{Garments.Types.Count} 種衣服配件的版型都算得出來");
            var big = Garments.New("25cm 胖胖體"); var small = Garments.New("15cm 正常體");
            Check(SM.Bbox(Garments.PolyOn(big, man)).width > SM.Bbox(Garments.PolyOn(small, man)).width, "胖胖體的上衣比正常體寬");
            Check(meta.Mannequin("15cm 胖胖體").BodyClass() == "15cm 胖胖體" && meta.MannequinHead("頭圍 L").HeadClass() == "頭圍 L", "人台的身形、頭圍判定正確");

            // ---------- 衣服訂單 ----------
            var mei = Orders.Cloth.First(o => o.who == "美玲");
            var s1 = new Session(meta, mei);
            Check(s1.IsGarment && s1.model != null && s1.model.BodyClass() == "20cm 胖胖體", $"客人的娃娃：{s1.model?.BodyClass()}");
            Check(s1.Steps().SequenceEqual(new[] { Step.Order, Step.GDesign, Step.Layout, Step.Cut, Step.Sew, Step.GDecor, Step.Deliver }), "衣服訂單的步驟");
            s1.Advance();
            Check(s1.step == Step.GDesign && s1.g.cls == null && !s1.CanAdvance(out _), "沒選尺寸不能往下");
            int coins0 = meta.coins;
            var ok = GarmentRun(meta, mei, "skirt", "20cm 胖胖體", "minky", "pink", "lace", "bow");
            Check(ok.step == Step.Deliver && ok.result.review.Any(x => x.text == "尺寸合身" && x.ok), $"美玲：做對尺寸的裙子 {ok.result.stars}★，{string.Join("、", ok.result.review.Select(x => (x.ok ? "✔" : "✘") + x.text))}");
            Check(ok.g.done && ok.g.trims.Count == 2 && ok.layout.units.Count == 1 && ok.layout.units[0].mult == 2, $"衣服完成：{ok.g.name}，裝飾 {ok.g.trims.Count} 個，布片「{ok.layout.units[0].name}」×2");
            Check(ok.model.Worn("bottom") == ok.g, "客人的娃娃身上穿著做好的裙子");
            Check(meta.coins == coins0 + ok.result.profit, $"衣服訂單也進帳（{ok.result.profit}）");
            var wrong = GarmentRun(meta, mei, "skirt", "20cm 正常體", "minky", "pink");
            Check(wrong.result.stars == 1 && wrong.result.line.Contains("穿不下") && wrong.result.comments[0].Contains("20cm 胖胖體"), $"尺寸做錯：{wrong.result.stars}★、「{wrong.result.comments[0]}」");
            var kui = Orders.Cloth.First(o => o.who == "小葵");
            var sk = GarmentRun(meta, kui, "skirt", "20cm 正常體", "cotton", "lemon");
            Check(sk.result.review.Any(x => x.text == "不要裙子" && !x.ok), "小葵說不要裙子，做裙子會被扣分");
            var p = Orders.Cloth.First(o => o.who == "小P");
            var hat = GarmentRun(meta, p, "beret", "頭圍 S", "felt", "coal");
            Check(hat.result.review.Any(x => x.text == "要帽子" && x.ok) && hat.result.review.Any(x => x.text == "尺寸合身" && x.ok), $"小P：頭圍 S 的貝雷帽 {hat.result.stars}★");
            var hatL = GarmentRun(meta, p, "beanie", "頭圍 L", "knit", "cream");
            Check(hatL.result.review.Any(x => x.text == "尺寸合身" && !x.ok), "頭圍做錯，帽子戴不下");
            var band = GarmentRun(meta, p, "hairband", null, "cotton", "pink");
            Check(band.g.cls == "通用" && band.result.review.Any(x => x.text == "尺寸合身" && x.ok), "髮帶是通用尺寸");

            // ---------- 自由創作與衣櫃 ----------
            int c0 = meta.coins, n0 = meta.clothes.Count;
            var free = GarmentRun(meta, Orders.Free(true), "top", "20cm 正常體", "felt", "sky", "button1", "pocket");
            Check(free.result.free && meta.clothes.Count == n0 + 1 && meta.coins == c0 - free.result.cost, $"自由創作做衣服：收進衣櫃，材料費 ${free.result.cost}");
            var fd = new Session(meta, Orders.Free(false)); fd.Begin(Step.Design);
            fd.doll.kind = "human"; fd.doll.fab["body"] = new Fab(DB.Mat("cotton"), DB.Col("skin")); fd.doll.Rebuild();
            Layout(fd); fd.Advance(); CutAll(fd); fd.Advance(); SewAll((SewGame)fd.craft, true); fd.Advance(); StuffAll((StuffGame)fd.craft, .7f, .75f); fd.Advance(); fd.Advance();
            Check(fd.result.free && meta.dolls.Contains(fd.doll) && fd.doll.name.StartsWith("娃娃"), $"自由創作做娃娃：收進展示櫃（{fd.doll.name}，{fd.doll.BodyClass()}）");
            free.ClearPreview(); // 遊戲裡開衣櫃或換下一單時會清掉試穿
            var cl = new ClosetScreen(fd);
            cl.model = meta.Mannequin("20cm 正常體");
            Check(cl.Wear(free.g) == null && cl.model.Worn("top") == free.g, "人台穿上同身形的上衣");
            Check(cl.Wear(free.g) == null && cl.model.Worn("top") == null, "再點一次脫下");
            cl.model = meta.Mannequin("25cm 胖胖體");
            Check(cl.Wear(free.g) != null, $"不同身形穿不下：{cl.Wear(free.g)}");
            cl.model = new DollState();
            Check(cl.Wear(free.g) != null, "動物布偶不能穿衣服");
            Check(meta.ClosetModels().Count == 9 + meta.dolls.Count, $"衣櫃裡的娃娃：9 個人台＋{meta.dolls.Count} 隻");

            // 繪圖：穿著衣服的娃娃可以畫出來
            var go = new GameObject("test renderer"); var rend = go.AddComponent<DollRenderer>();
            try
            {
                var m = meta.Mannequin("20cm 正常體");
                m.wear.Clear(); rend.state = m; rend.Build();
                int bare = 0; foreach (Transform c in go.transform) if (c.gameObject.activeSelf) bare++;
                foreach (var t in Garments.Types)
                {
                    var g = Garments.New("20cm 正常體"); Garments.SetType(g, t.id, true); g.fab = new Fab(DB.Mat("cotton"), DB.Col("pink")); g.done = true;
                    g.trims.Add(new DecorItem { type = "lace", x = 0, y = 0 });
                    m.wear[t.slot] = g;
                }
                m.Touch(); rend.Build();
                int dressed = 0; foreach (Transform c in go.transform) if (c.gameObject.activeSelf) dressed++;
                Check(dressed >= bare + 7, $"穿上 7 個位置的衣服配件後多畫了 {dressed - bare} 個網格");
                m.wear.Clear(); m.Touch();
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
