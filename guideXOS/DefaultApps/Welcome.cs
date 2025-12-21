using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System.Drawing;
namespace guideXOS.DefaultApps {
    /// <summary>
    /// Welcome
    /// </summary>
    internal class Welcome : Window {
        /// <summary>
        /// Image
        /// </summary>
        public Image img;
        /// <summary>
        /// Link
        /// </summary>
        public Image link;
        /// <summary>
        /// Welcome
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        public Welcome(int X, int Y) : base(X, Y, 280, 225) {
            Title = "guideXOS";
            img = new PNG(File.ReadAllBytes("Images/Banner.png"));
            link = new PNG(File.ReadAllBytes("Images/teamnexgenlink.png"));
            ShowInStartMenu = false;
            ShowInTaskbar = true;
            ShowMaximize = false;
            IsResizable = false;
            ShowMinimize = false;
            ShowTombstone = false;
        }
        /// <summary>
        /// On Draw
        /// </summary>
        public override void OnDraw() {
            base.OnDraw();
            Framebuffer.Graphics.DrawImage(X, Y, img);
            WindowManager.font.DrawString(X, Y + img.Height, "Welcome to guideXOS, this is a work in progress. Please direct all questions to guide_X@live.com.", Width);
            WindowManager.font.DrawString(X, Y + img.Height + 80, "http://team-nexgen.com", Width);
        }
    }
}