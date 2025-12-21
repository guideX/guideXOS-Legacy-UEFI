using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.OS;
using System.Windows.Forms;
namespace guideXOS.DefaultApps {
    /// <summary>
    /// Firewall Alert Window
    /// </summary>
    internal class FirewallAlert : Window {
        /// <summary>
        /// 
        /// </summary>
        private string _program;
        private string _action;
        private bool _autoAllow;
        private bool _latch;
        public FirewallAlert(string program, string action) : base((Framebuffer.Width - 420) / 2, (Framebuffer.Height - 160) / 2, 420, 160) {
            Title = "Firewall";
            _program = program;
            _action = action;
            ShowInTaskbar = false;
            ShowMinimize = false;
            ShowTombstone = false;
            ShowInStartMenu = false;
            ShowMaximize = false;
            IsResizable = false;
        }
        public override void OnInput() {
            base.OnInput();
            if (!Visible) return;
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);
            int btnY = Y + Height - 40;
            int allowX = X + Width - 200;
            int denyX = allowX - 100 - 10;
            int btnW = 100;
            int btnH = 24;
            int chkX = X + 14;
            int chkY = btnY - 28;
            if (left && !_latch) {
                if (mx >= allowX && mx <= allowX + btnW && my >= btnY && my <= btnY + btnH) {
                    if (_autoAllow) Firewall.AddException(_program);
                    Firewall.RemoveAlert(_program, _action);
                    Visible = false;
                    return;
                }
                if (mx >= denyX && mx <= denyX + btnW && my >= btnY && my <= btnY + btnH) {
                    Firewall.RemoveAlert(_program, _action);
                    Visible = false;
                    return;
                }
                if (mx >= chkX && mx <= chkX + 14 && my >= chkY && my <= chkY + 14) {
                    _autoAllow = !_autoAllow;
                    _latch = true;
                }
            } else if (!left) {
                _latch = false;
            }
        }
        public override void OnDraw() {
            base.OnDraw();
            string msg = _program + " wants to communicate (" + _action + ")";
            WindowManager.font.DrawString(X + 12, Y + BarHeight + 12, msg, Width - 24, WindowManager.font.FontSize * 3);
            int btnY = Y + Height - 40;
            int allowX = X + Width - 200;
            int denyX = allowX - 100 - 10;
            int btnW = 100;
            int btnH = 24;
            int chkX = X + 14;
            int chkY = btnY - 28;
            Framebuffer.Graphics.DrawRectangle(chkX, chkY, 14, 14, 0xFFAAAAAA, 1);
            if (_autoAllow) Framebuffer.Graphics.FillRectangle(chkX + 2, chkY + 2, 10, 10, 0xFF2E86C1);
            WindowManager.font.DrawString(chkX + 20, chkY - 2, "Auto-allow and add to exceptions");
            Framebuffer.Graphics.FillRectangle(denyX, btnY, btnW, btnH, 0xFF3A3A3A);
            WindowManager.font.DrawString(denyX + 8, btnY + (btnH / 2 - WindowManager.font.FontSize / 2), "Deny");
            Framebuffer.Graphics.FillRectangle(allowX, btnY, btnW, btnH, 0xFF2E86C1);
            WindowManager.font.DrawString(allowX + 8, btnY + (btnH / 2 - WindowManager.font.FontSize / 2), "Allow");
        }
    }
}