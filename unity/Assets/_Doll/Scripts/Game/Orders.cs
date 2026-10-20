using System.Collections.Generic;

namespace DollWorkshop
{
    /// <summary>客人的一項需求。可以是標籤（數量下限／上限），也可以指定材質或娃娃類型。</summary>
    public class Need
    {
        public string tag, needMat, needKind, label;
        public float? min, max;
        public static Need Tag(string tag, float? min = null, float? max = null) => new Need { tag = tag, min = min, max = max };
        public static Need Mat(string mat, string label) => new Need { needMat = mat, label = label };
        public static Need Kind(string kind, string label) => new Need { needKind = kind, label = label };
    }

    public class OrderDef
    {
        public string who, emoji, text, soft;
        public List<Need> need = new List<Need>();
        public int budget;
        public string good, mid, bad;
        public bool free;
    }

    /// <summary>娃娃訂單（從網頁原型移植）。衣服訂單在 M5 加入。</summary>
    public static class Orders
    {
        public static readonly Dictionary<string, (string label, float lo, float hi)> Soft = new Dictionary<string, (string, float, float)>
        {
            ["soft"] = ("軟綿綿", .35f, .7f),
            ["full"] = ("飽滿", .7f, .95f),
            ["firm"] = ("硬挺", .85f, 1.05f),
            ["any"] = ("都可以", .3f, 1.05f),
        };

        static OrderDef O(string who, string emoji, string text, string soft, int budget, string good, string mid, string bad, params Need[] need) =>
            new OrderDef { who = who, emoji = emoji, text = text, soft = soft, budget = budget, good = good, mid = mid, bad = bad, need = new List<Need>(need) };

        public static readonly List<OrderDef> Doll = new List<OrderDef>
        {
            O("林太太", "👩", "我女兒最近住院，晚上一個人會怕。想要一隻可以抱著睡、摸起來很安心的娃娃。", "soft", 220,
              "她一定會抱著它睡著的……真的謝謝你。", "嗯，她應該會喜歡吧。", "這個……好像不太適合放在病床上。", Need.Tag("療癒", 2), Need.Tag("溫柔", 2)),
            O("阿哲", "🧑‍🎤", "我們樂團要一隻吊在貝斯上的吉祥物。要酷！絕對不要可愛的東西，很遜。要硬挺一點，甩起來才不會軟趴趴。", "firm", 180,
              "靠，超酷！今晚就帶上台！", "還行啦，勉強可以掛。", "這掛上去我會被團員笑死。", Need.Tag("酷", 3), Need.Tag("可愛", null, 1)),
            O("陳奶奶", "👵", "我小時候有一隻手縫的娃娃，搬家時弄丟了。可以幫我做一隻有「以前那種味道」的嗎？軟硬都沒關係。", "any", 150,
              "哎呀……就是這個感覺，跟我那隻好像。", "做得很用心，謝謝你啊。", "現在的娃娃都長這樣啊……", Need.Tag("復古", 3), Need.Tag("手作感", 1)),
            O("小薇", "👩‍💼", "工作壓力好大，想要一隻放在辦公桌上、可以一直捏的娃娃。最好看了會想笑。", "soft", 200,
              "哈哈哈哈它的臉！我今天不會想辭職了。", "還不錯，捏起來滿舒服的。", "看了……更累了。", Need.Tag("療癒", 2), Need.Tag("好笑", 2)),
            O("甜點店老闆", "👨‍🍳", "我要一隻放在櫃檯的吉祥物，要活潑、讓客人心情好。可愛……但不要太可愛，我們不是兒童用品店。要飽滿一點，看起來才有精神。", "full", 260,
              "完美！客人一定會跟它拍照。", "嗯，放櫃檯應該可以。", "這放出去，客人會以為我們走錯路線了。", Need.Tag("活潑", 2), Need.Tag("可愛", 1, 3)),
            O("Leo", "🕶️", "我在收藏設計師玩具。想要一隻硬挺、有質感、放在展示櫃裡會很酷的公仔。", "firm", 240,
              "這質感可以進我的展示櫃最上層。", "嗯，還算有收藏價值。", "這……比較像小孩的玩具。", Need.Tag("酷", 2), Need.Tag("高級", 2)),
            O("王老師", "🧑‍🏫", "我在教木工課，想要一隻「木頭做的」娃娃擺在教室，讓學生看看木頭也可以很溫柔。", "any", 200,
              "學生一定會搶著摸它。", "嗯，木紋還不錯。", "這……不太像木頭的作品耶。", Need.Mat("wood", "木頭做的"), Need.Tag("溫柔", 2)),
            O("阿傑", "👨‍🍼", "這是寶寶的第一隻娃娃。要軟軟的、抱起來安心，看起來可愛一點。", "soft", 180,
              "寶寶一抓到就不放手了！", "應該可以陪寶寶睡覺。", "這個寶寶可能會怕……", Need.Tag("療癒", 2), Need.Tag("可愛", 1)),
            O("小晴", "🙋‍♀️", "我要做應援娃娃！要「人形」的，最好閃閃亮亮、很可愛，演唱會舉起來大家都看得到。", "full", 230,
              "這隻絕對是全場最可愛的應援娃娃！", "嗯，可以帶去應援。", "這樣大家認不出來是應援娃娃啦。", Need.Kind("human", "人形娃娃"), Need.Tag("可愛", 2), Need.Tag("高級", 1)),
            O("老K", "🎬", "我在拍恐怖喜劇片，需要一隻有點詭異、但又讓人忍不住笑出來的娃娃當道具。", "any", 210,
              "完美！這隻一出場觀眾就會又叫又笑。", "可以用，再靠燈光補一下。", "太正常了，嚇不到人。", Need.Tag("酷", 2), Need.Tag("好笑", 2)),
            O("林伯伯", "👴", "孫子下個月生日，想送他一隻像我小時候那種復古的熊娃娃，飽飽的那種。", "full", 160,
              "就是這個樣子！孫子一定會喜歡。", "跟我記憶中的有點像。", "現在的熊都長這樣喔？", Need.Tag("復古", 2), Need.Tag("溫柔", 1)),
            O("Ivy", "👩‍🎓", "我是設計系學生，要拍作品集。想要一隻「搪膠」做的公仔，顏色和質感都要很高級。", "firm", 260,
              "這張照片可以當作品集封面了！", "質感還可以，拍起來應該不錯。", "這樣放進作品集會被教授念……", Need.Mat("vinyl", "搪膠做的"), Need.Tag("高級", 2)),
            O("美雲", "👩‍🦳", "我開寵物店，想要一隻放在店門口的吉祥物，要活潑、讓人經過就想笑。", "full", 220,
              "客人一定會停下來跟它拍照！", "放門口應該還行。", "好像有點嚇到路過的狗了……", Need.Tag("活潑", 2), Need.Tag("好笑", 1)),
            O("阿明", "🥱", "我最近一直失眠。想要一隻超級軟、抱著就能睡著的娃娃，越療癒越好。", "soft", 240,
              "抱著它……我已經想睡了……", "嗯，抱起來還算舒服。", "抱著它反而更清醒了。", Need.Tag("療癒", 3)),
            O("小安", "🧒", "我想要一隻跟我一樣綁雙馬尾的娃娃！要可以帶去學校、塞在書包裡也不會壞掉。", "full", 200,
              "哇！她跟我一模一樣！我要叫她小小安！", "嗯……還可以啦。", "這不像我啦……", Need.Tag("活潑", 2), Need.Tag("可愛", 2)),
        };
    }
}
