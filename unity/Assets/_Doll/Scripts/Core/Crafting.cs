using System.Collections.Generic;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>可以分開塞棉花的部位：主體、主體上有名字的凸起（人形的手腳）、左右的部件。</summary>
    public class StuffRegion
    {
        public string name, main, part, host;
        public Bump? bump;
        public int side;
        public float fill, lump;
        /// <summary>early：要在主體塞滿之前先塞（不然棉花推不過去）；small：只塞得進小團。</summary>
        public bool early, small;
        public bool IsMainBody => main != null && bump == null;
    }

    public class SeamPoint { public Vector2 p; public float t, lim = .8f; public bool part; }
    public class Stitch { public float dx, dy, q; public bool bad; }
    public class Pin { public int i; public bool pulled; }

    /// <summary>一個主體的縫線（開口處不車）。車好的針腳存成相對主體中心的位移，塞棉花後會跟著鼓起來。</summary>
    public class Seam
    {
        public string mid, name;
        public float cx, cy;
        public bool sewing;               // false = 放珠針階段
        public readonly List<Pin> pins = new List<Pin>();
        public List<SeamPoint> path = new List<SeamPoint>();
        public int idx;
        public readonly List<Stitch> stitches = new List<Stitch>();
        public int jams, pinHits;
        public float drift, jamUntil;
        public bool done, backStart, backEnd;
        public float acc;
        public bool BackOk => backStart && backEnd;
    }

    /// <summary>排版時布片壓到的污漬，會留在娃娃身上。</summary>
    public class Stain { public string mid; public float dx, dy; }

    /// <summary>裝飾步驟加到娃娃上的配件或刺繡五官（位置是畫布像素，rot 是弧度）。</summary>
    public class DecorItem { public string type; public float x, y, s = 1, rot; public Vector2 Pos => new Vector2(x, y); }

    /// <summary>上色的一筆（筆刷或噴漆）。colorId 用來算顏料費（每種顏色 $2）。</summary>
    public class PaintStroke
    {
        public string colorId; public Color color; public float size; public bool spray;
        public readonly List<Vector2> pts = new List<Vector2>();
    }

    /// <summary>硬材質娃娃表面的顏色：手繪、噴漆、亮光漆，以及烤太久的焦黃。</summary>
    public class PaintData
    {
        public readonly List<PaintStroke> strokes = new List<PaintStroke>();
        public bool varnish;
        public float tint;
        /// <summary>每改一次 +1，畫面用它判斷要不要重畫上色層。</summary>
        public int version;
        public void Touch() => version++;
    }
}
