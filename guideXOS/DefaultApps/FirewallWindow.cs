using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.OS;
using System.Windows.Forms;
namespace guideXOS.DefaultApps {
    /// <summary>
    /// Firewall configuration window
    /// </summary>
    internal class FirewallWindow : Window {
        private int _pad = 10; private int _rowH = 22; private int _scroll; private bool _clickLatch;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public FirewallWindow(int x, int y) : base(x, y, 520, 360) { 
            Title = "guideXOS Firewall"; 
            ShowInTaskbar = true;
            ShowMinimize = true;
            ShowMaximize = true;
            IsResizable = true;
            ShowMaximize = true;
            ShowTombstone = true;
        }
        /// <summary>
        /// On Input
        /// </summary>
        public override void OnInput() {
            base.OnInput(); if (!Visible) return; int mx = Control.MousePosition.X; int my = Control.MousePosition.Y; bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);
            int cx = X + _pad; int cy = Y + _pad; int cw = Width - 2 * _pad; int ch = Height - 2 * _pad;
            // Mode selector buttons
            int by = cy; int bw = 110; int bh = 26; int bx = cx; FirewallMode[] modes = new[] { FirewallMode.Normal, FirewallMode.BlockAll, FirewallMode.Disabled, FirewallMode.Autolearn };
            for (int i = 0; i < modes.Length; i++) {
                bool over = mx >= bx && mx <= bx + bw && my >= by && my <= by + bh; if (left && over && !_clickLatch) { Firewall.Mode = modes[i]; _clickLatch = true; }
                bx += bw + 8;
            }
            if (!left) _clickLatch = false;
        }
        public override void OnDraw() {
            base.OnDraw(); int cx = X + _pad; int cy = Y + _pad; int cw = Width - 2 * _pad; int ch = Height - 2 * _pad;
            // Header
            WindowManager.font.DrawString(cx, cy, "Mode: " + Firewall.Mode.ToString());
            // Buttons visual
            int by = cy + WindowManager.font.FontSize + 6; int bw = 110; int bh = 26; int bx = cx; string[] names = new[] { "Normal Mode", "Block All", "Disabled", "Autolearn" }; FirewallMode[] modes = new[] { FirewallMode.Normal, FirewallMode.BlockAll, FirewallMode.Disabled, FirewallMode.Autolearn };
            for (int i = 0; i < names.Length; i++) { uint c = Firewall.Mode == modes[i] ? 0xFF2E86C1u : 0xFF2E2E2E; Framebuffer.Graphics.FillRectangle(bx, by, bw, bh, c); WindowManager.font.DrawString(bx + 8, by + (bh / 2 - WindowManager.font.FontSize / 2), names[i]); bx += bw + 8; }
            // Exception list
            int listY = by + bh + 10; int listH = ch - (listY - cy) - 10; Framebuffer.Graphics.AFillRectangle(cx, listY, cw, listH, 0x80282828);
            int y = listY + 6; var ex = Firewall.Exceptions; for (int i = 0; i < ex.Length; i++) { WindowManager.font.DrawString(cx + 8, y, ex[i]); y += _rowH; }
            WindowManager.font.DrawString(cx, Y + Height - _pad - WindowManager.font.FontSize - 2, "Press Esc to close. Double-click entries to remove (todo)");
        }
    }
}