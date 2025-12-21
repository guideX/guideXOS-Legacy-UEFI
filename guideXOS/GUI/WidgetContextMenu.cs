using guideXOS.Kernel.Drivers;
using System.Windows.Forms;

namespace guideXOS.GUI {
    /// <summary>
    /// Context menu for dockable widgets
    /// </summary>
    internal class WidgetContextMenu : Window {
        private DockableWidget _targetWidget;
        private WidgetContainer _targetContainer;
        private bool _isDocked;
        
        public WidgetContextMenu() : base(0, 0, 120, 28) {
            Visible = false;
            BarHeight = 0;
            ShowInTaskbar = false;
            ShowMaximize = false;
            ShowMinimize = false;
            ShowTombstone = false;
            IsResizable = false;
        }
        
        /// <summary>
        /// Show menu for a docked widget
        /// </summary>
        public void ShowForDockedWidget(DockableWidget widget, WidgetContainer container, int x, int y) {
            _targetWidget = widget;
            _targetContainer = container;
            _isDocked = true;
            X = x;
            Y = y;
            Visible = true;
            WindowManager.MoveToEnd(this);
        }
        
        /// <summary>
        /// Show menu for a standalone widget
        /// </summary>
        public void ShowForStandaloneWidget(DockableWidget widget, int x, int y) {
            _targetWidget = widget;
            _targetContainer = null;
            _isDocked = false;
            X = x;
            Y = y;
            Visible = true;
            WindowManager.MoveToEnd(this);
        }
        
        public override void OnInput() {
            if (!Visible) return;
            
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool leftClick = Control.MouseButtons.HasFlag(MouseButtons.Left);
            
            if (leftClick) {
                // Check if clicked on menu item
                if (mx >= X && mx <= X + Width && my >= Y && my <= Y + Height) {
                    if (_isDocked) {
                        // Undock action
                        if (_targetWidget != null && _targetContainer != null) {
                            _targetContainer.UndockWidgetToPosition(_targetWidget, mx, my);
                        }
                    } else {
                        // Dock action - try to find nearby container or widget
                        if (_targetWidget != null) {
                            _targetWidget.TryDockToNearby();
                        }
                    }
                }
                
                // Close menu on any click
                Visible = false;
                _targetWidget = null;
                _targetContainer = null;
            }
        }
        
        public override void OnDraw() {
            if (!Visible) return;
            
            // Background
            Framebuffer.Graphics.AFillRectangle(X, Y, Width, Height, 0xCC222222);
            
            // Menu text
            string menuText = _isDocked ? "Undock" : "Dock";
            int textY = Y + (Height / 2) - (WindowManager.font.FontSize / 2);
            WindowManager.font.DrawString(X + 8, textY, menuText);
            menuText.Dispose();
            
            // Border
            Framebuffer.Graphics.DrawRectangle(X, Y, Width, Height, 0xFF444444, 1);
        }
    }
}
