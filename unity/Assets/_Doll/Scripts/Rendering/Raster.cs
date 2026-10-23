using System.Collections.Generic;
using UnityEngine;

namespace DollWorkshop
{
    /// <summary>把多邊形塗成畫布大小（800×560）的遮罩：在任何一個多邊形裡面就是 true。用掃描線，很快。</summary>
    public static class Raster
    {
        public const int W = 800, H = 560;

        public static bool[] Mask(IEnumerable<List<Vector2>> polys)
        {
            var m = new bool[W * H]; var xs = new List<float>();
            foreach (var P in polys)
            {
                if (P == null || P.Count < 3) continue;
                var bb = SM.Bbox(P);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(bb.yMin)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(bb.yMax));
                for (int y = y0; y <= y1; y++)
                {
                    float sy = y + .5f; xs.Clear();
                    for (int i = 0, j = P.Count - 1; i < P.Count; j = i++)
                    {
                        Vector2 a = P[i], b = P[j];
                        if ((a.y > sy) != (b.y > sy)) xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
                    }
                    xs.Sort();
                    for (int k = 0; k + 1 < xs.Count; k += 2)
                    {
                        int xa = Mathf.Max(0, Mathf.CeilToInt(xs[k] - .5f)), xb = Mathf.Min(W - 1, Mathf.FloorToInt(xs[k + 1] - .5f));
                        for (int x = xa; x <= xb; x++) m[y * W + x] = true;
                    }
                }
            }
            return m;
        }

        public static bool At(bool[] m, float x, float y)
        {
            int i = Mathf.FloorToInt(x), j = Mathf.FloorToInt(y);
            return i >= 0 && j >= 0 && i < W && j < H && m[j * W + i];
        }
    }

    /// <summary>
    /// 娃娃周圍的格子（每格 cs 像素）：T = 這格在娃娃裡面。修毛邊、雕刻、砂磨都用它判斷刀子有沒有切進娃娃。
    /// （網頁版 buildGrid / deepInside / cellsNear）
    /// </summary>
    public class HardGrid
    {
        public int x0, y0, cs, nx, ny;
        public bool[] T;

        public HardGrid(DollState d, int margin = 20, int cs = 4)
        {
            var polys = d.SilhouettePolys();
            var all = new List<Vector2>(); foreach (var P in polys) all.AddRange(P);
            var bb = SM.Bbox(all); var mask = Raster.Mask(polys);
            this.cs = cs;
            x0 = Mathf.FloorToInt(bb.xMin - margin); y0 = Mathf.FloorToInt(bb.yMin - margin);
            nx = Mathf.CeilToInt((bb.width + 2 * margin) / cs); ny = Mathf.CeilToInt((bb.height + 2 * margin) / cs);
            T = new bool[nx * ny];
            for (int j = 0; j < ny; j++) for (int i = 0; i < nx; i++) T[j * nx + i] = Raster.At(mask, x0 + (i + .5f) * cs, y0 + (j + .5f) * cs);
        }

        public Vector2 Center(int k) { int i = k % nx, j = k / nx; return new Vector2(x0 + (i + .5f) * cs, y0 + (j + .5f) * cs); }
        public Rect CanvasRect => new Rect(x0, y0, nx * cs, ny * cs);

        /// <summary>這一點周圍 rc 格都在娃娃裡面（刀子切進娃娃了）。</summary>
        public bool DeepInside(float x, float y, int rc)
        {
            int i0 = Mathf.FloorToInt((x - x0) / cs), j0 = Mathf.FloorToInt((y - y0) / cs);
            for (int dj = -rc; dj <= rc; dj++) for (int di = -rc; di <= rc; di++)
            {
                int i = i0 + di, j = j0 + dj;
                if (i < 0 || j < 0 || i >= nx || j >= ny || !T[j * nx + i]) return false;
            }
            return true;
        }

        /// <summary>半徑 r 以內的每一格。</summary>
        public void CellsNear(float x, float y, float r, System.Action<int> fn)
        {
            int rc = Mathf.CeilToInt(r / cs), i0 = Mathf.FloorToInt((x - x0) / cs), j0 = Mathf.FloorToInt((y - y0) / cs);
            for (int dj = -rc; dj <= rc; dj++) for (int di = -rc; di <= rc; di++)
            {
                int i = i0 + di, j = j0 + dj; if (i < 0 || j < 0 || i >= nx || j >= ny) continue;
                float cx = x0 + (i + .5f) * cs, cy = y0 + (j + .5f) * cs;
                if ((cx - x) * (cx - x) + (cy - y) * (cy - y) <= r * r) fn(j * nx + i);
            }
        }
    }
}
