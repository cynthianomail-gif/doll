using UnityEngine;

namespace DollWorkshop
{
    /// <summary>指標（滑鼠或手指）的狀態，座標是畫布像素。speed 的單位和網頁版一樣：像素／毫秒。</summary>
    public class Ptr
    {
        public Vector2 p = new Vector2(-99, -99);
        public bool down, inside, right;
        public float speed;
    }

    /// <summary>
    /// 製作小遊戲的共同介面（排版、裁布、車縫、塞棉花……）。
    /// 遊戲邏輯都在這裡，畫面由 GameScreen 每格呼叫 Draw／Panel；不依賴場景，所以可以在編輯器裡直接用程式測試。
    /// </summary>
    public abstract class CraftGame
    {
        /// <summary>毫秒時鐘。測試時可以換成自己的時鐘。</summary>
        public static System.Func<float> Clock = () => Time.time * 1000f;
        protected static float Now => Clock();

        public readonly Session ss;
        public DollState D => ss.doll;
        public Ptr ptr = new Ptr();

        protected CraftGame(Session ss) { this.ss = ss; }

        public string flash; float flashT = -1e9f;
        public void Flash(string m) { flash = m; flashT = Now; }
        public bool Flashing => Now - flashT < 1600;

        /// <summary>畫面上方的提示。</summary>
        public abstract string Hint { get; }
        /// <summary>這一步完成，可以前往下一步。</summary>
        public abstract bool Done { get; }
        /// <summary>是否顯示娃娃本體（排版、裁布時只看布片）。</summary>
        public virtual bool ShowDoll => true;
        /// <summary>裁布在綠色切割墊上做，其他步驟在工作桌上。</summary>
        public virtual bool CuttingMat => false;
        /// <summary>隱藏系統游標，改畫剪刀、縫紉機等工具。</summary>
        public virtual bool HideCursor => false;

        public virtual void Down(Vector2 p) { }
        /// <summary>按住拖曳時，沿路每隔約 3 像素呼叫一次（和網頁版一樣，快速移動也不會漏掉）。</summary>
        public virtual void Drag(Vector2 q) { }
        public virtual void Move(Vector2 p, Vector2 prev) { }
        public virtual void Up(Vector2 p) { }
        public virtual void Tick(float dt) { }
        public virtual void Draw(Sketch under, Sketch over) { }
        public virtual void CanvasGUI(GameScreen ui) { }
        public abstract void Panel(GameScreen ui);

        protected static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        protected static readonly Color Accent = Hex("#e07a5f"), Bad = Hex("#d0534a"), Good = Hex("#3d9a66"), Ink = Hex("#4a3b32");
    }
}
