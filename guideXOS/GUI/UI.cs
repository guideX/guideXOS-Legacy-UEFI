using System.Windows.Forms;

namespace guideXOS.GUI {
    internal static class UI {
        public static bool IsMouseOver(int x, int y, int w, int h) {
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            return mx >= x && mx <= x + w && my >= y && my <= y + h;
        }

        public static uint ButtonFillColor(int x, int y, int w, int h, uint normal, uint hover, uint pressed, bool enabled = true, uint disabled = 0xFF2A2A2A) {
            if (!enabled) return disabled;
            bool over = IsMouseOver(x, y, w, h);
            bool leftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            if (leftDown && over) return pressed;
            if (over) return hover;
            return normal;
        }
    }
}
