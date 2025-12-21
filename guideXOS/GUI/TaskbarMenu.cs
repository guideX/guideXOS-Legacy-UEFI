using guideXOS.DefaultApps;
using guideXOS.Kernel.Drivers;
using System.Windows.Forms;
namespace guideXOS.GUI {
    /// <summary>
    /// Right-click context menu for the taskbar with a Task Manager entry
    /// </summary>
    internal class TaskbarMenu : Window {
        private const int ItemH = 28;
        private const int MenuW = 180;
        private const int Pad = 6;
        private bool _clickLatch;

        public TaskbarMenu(int x, int y) : base(x, y, MenuW, ItemH + Pad * 2) {
            Title = ""; BarHeight = 0;
        }

        public override void OnSetVisible(bool value) {
            base.OnSetVisible(value);
            if (value) {
                // Position near cursor
                X = Control.MousePosition.X - 8;
                Y = Control.MousePosition.Y - (ItemH + Pad * 2) + 8;
                // Clamp to screen
                if (X + Width > Framebuffer.Width) X = Framebuffer.Width - Width - 2;
                if (Y + Height > Framebuffer.Height) Y = Framebuffer.Height - Height - 2;
            }
        }

        public override void OnInput() {
            if (!Visible) return;
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            if (Control.MouseButtons.HasFlag(MouseButtons.Left)) {
                if (_clickLatch) return;
                _clickLatch = true;
                // Single item: Task Manager
                int itemX = X + Pad; int itemY = Y + Pad; int itemW = Width - Pad * 2; int itemH = ItemH;
                if (mx >= itemX && mx <= itemX + itemW && my >= itemY && my <= itemY + itemH) {
                    var tm = new TaskManager(200, 160, 760, 520);
                    WindowManager.MoveToEnd(tm);
                    tm.Visible = true;
                    Visible = false; return;
                }
                // click elsewhere inside closes
                Visible = false; return;
            } else {
                _clickLatch = false;
                // Right-click outside closes
                if (!IsUnderMouse()) Visible = false;
            }
        }

        public override void OnDraw() {
            // Background
            Framebuffer.Graphics.FillRectangle(X, Y, Width, Height, 0xFF2A2A2A);
            Framebuffer.Graphics.DrawRectangle(X, Y, Width, Height, 0xFF3A3A3A, 1);
            // Item
            int itemX = X + Pad; int itemY = Y + Pad; int itemW = Width - Pad * 2; int itemH = ItemH;
            // Highlight on hover
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            bool hover = (mx >= itemX && mx <= itemX + itemW && my >= itemY && my <= itemY + itemH);
            if (hover) Framebuffer.Graphics.FillRectangle(itemX, itemY, itemW, itemH, 0xFF353535);
            WindowManager.font.DrawString(itemX + 6, itemY + (itemH / 2 - WindowManager.font.FontSize / 2), "Task Manager");
        }
    }
}
