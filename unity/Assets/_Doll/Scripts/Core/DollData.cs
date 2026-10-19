using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DollWorkshop
{
    public class ShapeChoice
    {
        public string id, name;
        public string[] tags = new string[0];
        public ShapeDef shape;
    }

    public class HairDef
    {
        public string id, name, bs, back;
        public string[] tags = new string[0];
        public float side, bang, fluff;
    }

    public class PartOpt
    {
        public string id, name, rname, layer = "back";
        public string[] tags = new string[0];
        public float a, inset, tilt;
        public bool inner, down, nostuff;
        public List<Vector2> P; // 單位：R（娃娃基準半徑）的倍數，底部在 (0,0)，往上延伸
        public bool IsNone => id == "none";
    }

    public class PartGroup
    {
        public string key, label;
        public List<PartOpt> opts;
        public PartOpt None => opts[0];
        public PartOpt Find(string id) => opts.FirstOrDefault(o => o.id == id) ?? None;
    }

    public class MatDef
    {
        public string id, name, grain, desc;
        public string[] tags = new string[0];
        public float p10, gloss, stretch;
        public bool Hard => id == "vinyl" || id == "wood";
    }

    public class ColorDef
    {
        public string id, name;
        public Color color;
        public string[] tags = new string[0];
    }

    public class SliderDef
    {
        public string key, label;
        public float min, max, def;
        public SliderDef(string key, string label, float min, float max, float def) { this.key = key; this.label = label; this.min = min; this.max = max; this.def = def; }
    }

    /// <summary>
    /// 所有內容資料。目前直接寫在程式裡，和網頁原型一一對應；
    /// 之後會改成 ScriptableObject，讓企劃在 Inspector 裡直接編輯。
    /// </summary>
    public static class DB
    {
        static ShapeChoice SC(string id, string name, string[] tags, ShapeDef s) => new ShapeChoice { id = id, name = name, tags = tags, shape = s };
        static string[] T(params string[] t) => t;

        public static readonly List<ShapeChoice> Bodies = new List<ShapeChoice>
        {
            SC("round", "圓滾滾", T("溫柔"), new ShapeDef(1f, .92f)),
            SC("mochi", "糰子", T("療癒", "好笑"), new ShapeDef(1.22f, .8f)),
            SC("pear", "西洋梨", T("療癒"), new ShapeDef(.88f, .95f, 2f, new Bump(40, .15f, .45f), new Bump(140, .15f, .45f))),
            SC("tall", "長身", T("高級"), new ShapeDef(.78f, 1.12f)),
            SC("square", "方方", T("好笑"), new ShapeDef(1f, .9f, 4f)),
            SC("ghost", "幽靈", T("酷", "好笑"), new ShapeDef(.85f, 1.02f, 2f, new Bump(52, .22f, .1f), new Bump(90, .24f, .1f), new Bump(128, .22f, .1f))),
        };

        public static readonly List<ShapeChoice> Heads = new List<ShapeChoice>
        {
            SC("round", "圓頭", T("可愛"), new ShapeDef(1f, .92f)),
            SC("bun", "包子頭", T("療癒", "好笑"), new ShapeDef(1.13f, .84f)),
            SC("egg", "蛋形頭", T("高級"), new ShapeDef(.88f, 1f)),
            SC("squircle", "方圓頭", T("好笑"), new ShapeDef(1f, .9f, 3.2f)),
        };

        public static readonly List<HairDef> Hairs = new List<HairDef>
        {
            new HairDef { id = "none", name = "不要" },
            new HairDef { id = "bob", name = "短髮", tags = T("溫柔"), side = 35, bang = -.12f, bs = "curve" },
            new HairDef { id = "hime", name = "齊瀏海長髮", tags = T("高級", "復古"), side = 55, bang = -.04f, bs = "flat", back = "long" },
            new HairDef { id = "fluffy", name = "炸毛", tags = T("活潑", "好笑"), side = 22, bang = -.18f, bs = "zig", fluff = .08f },
            new HairDef { id = "twin", name = "雙馬尾", tags = T("可愛", "活潑"), side = 25, bang = -.1f, bs = "curve", back = "twin" },
            new HairDef { id = "part", name = "中分", tags = T("酷"), side = 42, bang = -.2f, bs = "part" },
        };

        static PartOpt None() => new PartOpt { id = "none", name = "不要" };

        public static readonly Dictionary<string, PartGroup> Parts = new Dictionary<string, PartGroup>
        {
            ["ears"] = new PartGroup { key = "ears", label = "耳朵", opts = new List<PartOpt> {
                None(),
                new PartOpt { id = "round", name = "圓耳", tags = T("溫柔", "復古"), a = 50, inset = .14f, tilt = 4, inner = true, rname = "耳", P = SM.EllPoly(0, -.2f, .25f, .24f) },
                new PartOpt { id = "long", name = "長耳", tags = T("可愛", "溫柔"), a = 20, inset = .1f, tilt = -6, inner = true, rname = "耳", P = SM.EllPoly(0, -.5f, .17f, .56f) },
                new PartOpt { id = "pointy", name = "尖耳", tags = T("活潑", "可愛"), a = 42, inset = .12f, tilt = 2, inner = true, rname = "耳", P = SM.PointyPoly(.27f, .52f) },
                new PartOpt { id = "floppy", name = "垂耳", tags = T("療癒", "溫柔"), a = 58, inset = .06f, tilt = 110, layer = "front", rname = "耳", P = SM.EllPoly(0, -.38f, .17f, .44f) },
                new PartOpt { id = "horn", name = "小角", tags = T("酷"), a = 32, inset = .06f, tilt = -6, rname = "角", P = SM.HornPoly(.15f, .42f) },
            }},
            ["arms"] = new PartGroup { key = "arms", label = "手", opts = new List<PartOpt> {
                None(),
                new PartOpt { id = "nub", name = "小圓手", tags = T("可愛"), a = 102, inset = .12f, tilt = 12, rname = "手", P = SM.EllPoly(0, -.17f, .15f, .21f) },
                new PartOpt { id = "long", name = "長手", tags = T("好笑"), a = 100, inset = .08f, tilt = 28, rname = "手", P = SM.EllPoly(0, -.4f, .12f, .44f) },
                new PartOpt { id = "wing", name = "小翅膀", tags = T("活潑", "高級"), a = 58, inset = 0, tilt = 38, rname = "翅膀", nostuff = true, P = SM.WingPoly(1.5f) },
            }},
            ["feet"] = new PartGroup { key = "feet", label = "腳", opts = new List<PartOpt> {
                None(),
                new PartOpt { id = "round", name = "圓腳", tags = T("溫柔"), a = 150, inset = .16f, down = true, layer = "front", rname = "腳", P = SM.EllPoly(0, -.13f, .21f, .15f) },
                new PartOpt { id = "long", name = "長腿", tags = T("好笑"), a = 156, inset = .1f, down = true, layer = "front", rname = "腳", P = SM.EllPoly(0, -.32f, .11f, .34f) },
            }},
            ["belly"] = new PartGroup { key = "belly", label = "肚子", opts = new List<PartOpt> {
                None(),
                new PartOpt { id = "oval", name = "圓肚", tags = T("療癒"), P = SM.EllPoly(0, .3f, .42f, .36f) },
                new PartOpt { id = "heart", name = "愛心肚", tags = T("可愛"), P = SM.HeartPoly(.4f, .32f) },
            }},
        };
        public static readonly string[] PartKeys = { "ears", "arms", "feet" };

        public static readonly List<MatDef> Materials = new List<MatDef>
        {
            new MatDef { id = "felt", name = "不織布", tags = T("手作感", "復古"), p10 = 1, gloss = .25f, stretch = .8f, grain = "none", desc = "沒有布紋，怎麼排都行。硬挺，有一種古早的手工味。" },
            new MatDef { id = "cotton", name = "印花棉布", tags = T("活潑", "可愛"), p10 = 2, gloss = .5f, stretch = 1f, grain = "two", desc = "有布紋（可以上下顛倒）。花色明亮，看了心情好。" },
            new MatDef { id = "knit", name = "毛線針織", tags = T("溫柔", "復古"), p10 = 2, gloss = .35f, stretch = 1.3f, grain = "two", desc = "很會延展，摸起來暖暖的。布紋排歪會塞得凹凸不平。" },
            new MatDef { id = "minky", name = "短絨毛", tags = T("療癒", "高級"), p10 = 3, gloss = .9f, stretch = 1.1f, grain = "one", desc = "毛有順向，不能顛倒排。摸起來像雲一樣。" },
        };
        public static readonly List<MatDef> HardMats = new List<MatDef>
        {
            new MatDef { id = "vinyl", name = "搪膠", tags = T("可愛", "高級"), gloss = 1.4f, stretch = 1f, grain = "none", desc = "灌模、烤、倒膠、修毛邊，再上色。" },
            new MatDef { id = "wood", name = "木頭", tags = T("復古", "手作感", "溫柔"), gloss = .35f, stretch = 1f, grain = "none", desc = "鋸外形、雕刻、砂磨，再上色上漆。" },
        };

        static ColorDef C(string id, string name, string hex, params string[] tags)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return new ColorDef { id = id, name = name, color = c, tags = tags };
        }
        public static readonly List<ColorDef> Colors = new List<ColorDef>
        {
            C("cream", "奶油白", "#f3e7d3", "溫柔"), C("skin", "膚色", "#f7dcc6", "溫柔"), C("tan", "小麥", "#d8a77e", "活潑"),
            C("pink", "櫻花粉", "#f4b6c6", "可愛"), C("sky", "天空藍", "#a9d3ee", "療癒"), C("lemon", "檸檬黃", "#f6d96b", "活潑"),
            C("mint", "薄荷綠", "#a6dcc0", "療癒"), C("milktea", "奶茶金", "#e5c48e", "溫柔"), C("brick", "磚紅", "#b4573f", "復古"),
            C("cocoa", "可可棕", "#6b4a35", "復古"), C("grape", "葡萄紫", "#8e70b4", "高級"), C("coal", "炭黑", "#3d3c43", "酷"),
        };
        public static readonly List<ColorDef> Woods = new List<ColorDef>
        {
            C("pine", "松木", "#e6c494", "溫柔"), C("birch", "白樺", "#f0e2c4", "療癒"), C("cherry", "櫻桃木", "#b5694b", "高級"), C("walnut", "胡桃木", "#76503a", "復古"),
        };

        public static MatDef Mat(string id) => Materials.Concat(HardMats).FirstOrDefault(m => m.id == id);
        public static ColorDef Col(string id) => Colors.Concat(Woods).FirstOrDefault(c => c.id == id);

        public static readonly Dictionary<string, SliderDef[]> Sliders = new Dictionary<string, SliderDef[]>
        {
            ["body"] = new[] { new SliderDef("w", "寬度", .7f, 1.5f, 1), new SliderDef("h", "高度", .7f, 1.4f, 1), new SliderDef("sq", "方圓", 0, 1, 0), new SliderDef("pear", "上寬↔下寬", -1, 1, 0) },
            ["head"] = new[] { new SliderDef("size", "頭的大小", .75f, 1.25f, 1), new SliderDef("w", "寬度", .75f, 1.35f, 1), new SliderDef("h", "高度", .8f, 1.3f, 1), new SliderDef("sq", "方圓", 0, 1, 0) },
            ["torso"] = new[] { new SliderDef("w", "胖瘦", .75f, 1.6f, 1), new SliderDef("h", "身高", .75f, 1.4f, 1), new SliderDef("armL", "手長", .3f, 1.1f, .62f), new SliderDef("armW", "手粗", .12f, .32f, .2f),
                                new SliderDef("armA", "手的角度", -10, 45, 12), new SliderDef("legL", "腿長", .3f, 1.2f, .66f), new SliderDef("legW", "腿粗", .08f, .22f, .13f), new SliderDef("legSp", "腿距", 10, 38, 22) },
        };

        public static readonly string[] SlotKeys = { "body", "hair", "ears", "earInner", "arms", "feet", "belly" };
        public static readonly Dictionary<string, string> SlotName = new Dictionary<string, string>
        {
            ["body"] = "身體", ["hair"] = "頭髮", ["ears"] = "耳朵", ["earInner"] = "耳朵內側", ["arms"] = "手", ["feet"] = "腳", ["belly"] = "肚子",
        };
    }
}
