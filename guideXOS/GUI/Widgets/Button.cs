using guideXOS.GUI.Widgets.Base;
using guideXOS.Kernel.Drivers;
namespace guideXOS.GUI.Widgets {
    /// <summary>
    /// Button
    /// </summary>
    internal class Button : Widget {
        /// <summary>
        /// UInt Param
        /// </summary>
        public uint UIntParam;
        /// <summary>
        /// Name
        /// </summary>
        public string Name;
        /// <summary>
        /// Optional: enabled state
        /// </summary>
        public bool Enabled = true;
        /// <summary>
        /// Draw a simple rectangular button with mouse-over and pressed feedback.
        /// </summary>
        public void Draw(int originX, int originY, int w, int h, string label) {
            int bx = originX + X; int by = originY + Y;
            uint bg = UI.ButtonFillColor(bx, by, w, h, 0xFF2A2A2A, 0xFF343434, 0xFF3F3F3F, Enabled);
            Framebuffer.Graphics.FillRectangle(bx, by, w, h, bg);
            Framebuffer.Graphics.DrawRectangle(bx, by, w, h, 0xFF3F3F3F, 1);
            int tx = bx + (w / 2) - (WindowManager.font.MeasureString(label) / 2);
            int ty = by + (h / 2) - (WindowManager.font.FontSize / 2);
            WindowManager.font.DrawString(tx, ty, label);
        }
        /// <summary>
        /// Returns true on click release inside rect.
        /// </summary>
        public bool HandleClick(int originX, int originY, int w, int h, ref bool latch) {
            int bx = originX + X; int by = originY + Y;
            bool over = UI.IsMouseOver(bx, by, w, h);
            bool left = System.Windows.Forms.Control.MouseButtons.HasFlag(System.Windows.Forms.MouseButtons.Left);
            if (!Enabled) { latch = false; return false; }
            if (left) {
                if (!latch && over) { latch = true; }
                return false;
            }
            if (latch && over) { latch = false; return true; }
            latch = false; return false;
        }
    }
}