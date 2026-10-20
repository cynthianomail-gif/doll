using System.Linq;
using System.Collections.Generic;

namespace DollWorkshop
{
    /// <summary>客人的一項需求。可以是標籤（數量下限／上限），也可以指定材質或娃娃類型。</summary>
    public class Need
    {
        public string tag, needMat, needKind, label;
        /// <summary>衣服訂單：不要某種衣服、指定某種衣服、指定穿的位置（帽子、外套……）。</summary>
        public string notType, isType, needSlot;
        public float? min, max;
        public static Need Tag(string tag, float? min = null, float? max = null) => new Need { tag = tag, min = min, max = max };
        public static Need Mat(string mat, string label) => new Need { needMat = mat, label = label };
        public static Need Kind(string kind, string label) => new Need { needKind = kind, label = label };
        public static Need NotType(string t) => new Need { notType = t };
        public static Need IsType(string t, string label) => new Need { isType = t, label = label };
        public static Need Slot(string slot, string label) => new Need { needSlot = slot, label = label };
    }

    public class OrderDef
    {
        public string who, emoji, text, soft;
        public List<Need> need = new List<Need>();
        public int budget;
        public string good, mid, bad;
        public bool free;
        /// <summary>doll = 做娃娃；cloth = 幫客人的娃娃做衣服（doll 是客人的娃娃）。</summary>
        public string type = "doll";
        public DollSpec doll;
        public bool IsCloth => type == "cloth";
    }

    /// <summary>娃娃訂單與衣服訂單（從網頁原型移植）。</summary>
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

        static OrderDef C(string who, string emoji, string text, DollSpec doll, int budget, string good, string mid, string bad, params Need[] need) =>
            new OrderDef { type = "cloth", who = who, emoji = emoji, text = text, soft = "any", doll = doll, budget = budget, good = good, mid = mid, bad = bad, need = new List<Need>(need) };

        /// <summary>衣服訂單：客人帶自己的娃娃來，要看清楚娃娃的身形和頭圍。</summary>
        public static readonly List<OrderDef> Cloth = new List<OrderDef>
        {
            C("美玲", "👩‍🦰", "這是我的娃娃「圓圓」，20cm 胖胖體。下個月她要當伴娘，幫她做一件正式又有點可愛的衣服，但不要太花俏。",
              new DollSpec { torsoW = 1.4f, hair = "hime", hairColor = "cocoa", name = "圓圓", decor = new[] { "eyes_sparkle", "mouth_smile", "blush" } }, 120,
              "圓圓好美！婚禮上一定最搶眼。", "嗯，可以穿去啦。", "這……不太像伴娘耶。", Need.Tag("高級", 2), Need.Tag("可愛", 1, 3)),
            C("阿凱", "🧔", "我家「小黑」是 15cm 正常體。天氣變冷了，想幫他做一件暖暖的外套，有點復古感更好。",
              new DollSpec { torsoH = .82f, headId = "bun", hair = "fluffy", hairColor = "coal", skin = "tan", name = "小黑", decor = new[] { "eyes_dot", "mouth_w" } }, 90,
              "小黑穿起來好暖，跟我一樣有型。", "還可以，至少不會冷。", "他看起來還是好冷……", Need.Tag("溫柔", 2), Need.Tag("復古", 1)),
            C("小葵", "👧", "我的娃娃要參加娃娃運動會！她是 20cm 正常體。幫她做一件好活動、很有精神的衣服，不要裙子！",
              new DollSpec { hair = "twin", hairColor = "milktea", decor = new[] { "eyes_sparkle", "mouth_w", "blush" } }, 100,
              "她一定會拿第一名！", "嗯……應該跑得動。", "這樣怎麼跑步啦！", Need.Tag("活潑", 2), Need.NotType("skirt")),
            C("雅婷", "🎤", "我家娃娃「阿喵」是 25cm 正常體，週末要跟我去看搖滾演唱會！幫她做一件酷一點、又有活力的衣服。",
              new DollSpec { torsoH = 1.27f, hair = "part", hairColor = "coal", name = "阿喵", decor = new[] { "eyes_dot", "mouth_fang" } }, 110,
              "阿喵穿這樣一定是全場最酷的！", "嗯，可以去演唱會了。", "這穿去演唱會會被笑啦。", Need.Tag("酷", 1), Need.Tag("活潑", 1)),
            C("Ann", "👩‍🔬", "我的娃娃「糖糖」是 15cm 胖胖體，很怕冷。想幫她做一件可以披著的「斗篷」，摸起來溫暖一點。",
              new DollSpec { torsoH = .82f, torsoW = 1.4f, hair = "bob", hairColor = "milktea", name = "糖糖", decor = new[] { "eyes_stitch", "blush" } }, 100,
              "糖糖披著好像一顆暖暖的湯圓！", "這樣應該不會冷了。", "她還是在發抖……", Need.Slot("outer", "要斗篷"), Need.Tag("溫柔", 1)),
            C("小P", "📷", "我要幫娃娃拍一組文青照片，想要一頂有質感的「帽子」。她的頭比較小，是頭圍 S。",
              new DollSpec { headSize = .84f, hair = "hime", hairColor = "cocoa", decor = new[] { "eyes_sparkle", "mouth_smile" } }, 80,
              "這頂帽子一戴，整組照片都有氣質了。", "可以拍，效果還行。", "這頂帽子跟照片風格不太搭。", Need.Slot("hat", "要帽子"), Need.Tag("高級", 1)),
            C("Grace", "👰", "我開婚紗店，櫥窗娃娃是 20cm 正常體，要當花童。幫她做一件「裙子」，長長的、看起來很高級。",
              new DollSpec { hair = "twin", hairColor = "milktea", decor = new[] { "eyes_sparkle", "mouth_smile", "blush" } }, 130,
              "櫥窗一下子變得好夢幻！", "嗯，放櫥窗應該可以。", "這不太像婚禮上的花童……", Need.IsType("skirt", "要裙子"), Need.Tag("高級", 2)),
            C("小雨", "🧑‍🦱", "我的娃娃「大福」頭比較大，是頭圍 L。冬天到了，想幫她做一頂暖暖的帽子，戴起來軟軟的那種。",
              new DollSpec { headSize = 1.22f, hair = "bob", hairColor = "cocoa", name = "大福", decor = new[] { "eyes_stitch", "mouth_smile", "blush" } }, 70,
              "大福戴起來好可愛，一點都不冷了！", "還不錯，冬天可以戴。", "這個……大福戴不太下。", Need.Slot("hat", "要帽子"), Need.Tag("溫柔", 2)),
        };

        public static readonly List<OrderDef> All = Doll.Concat(Cloth).ToList();

        /// <summary>自由創作：不接單，做自己想做的娃娃或衣服，用自己的錢買材料。</summary>
        public static OrderDef Free(bool garment) => new OrderDef
        {
            type = garment ? "cloth" : "doll", free = true, who = "自由創作", emoji = "🧑‍🎨", soft = "any", budget = 0,
            text = garment ? "不接單，做一件自己想做的衣服，完成後收進衣櫃。" : "不接單，做一隻自己想做的娃娃，完成後收進展示櫃。",
            good = "完成了！", mid = "完成了！", bad = "完成了！",
        };
    }
}
