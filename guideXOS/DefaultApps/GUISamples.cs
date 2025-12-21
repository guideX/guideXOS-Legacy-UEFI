using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System.Windows.Forms;

namespace guideXOS.DefaultApps {
    /// <summary>
    /// Comprehensive demonstration window for GXM GUI controls and callbacks
    /// Shows all available features: buttons, labels, lists, dropdowns, and event handling
    /// </summary>
    internal class GUISamples : Window {
        private GXMScriptWindow _demo1;
        private GXMScriptWindow _demo2;
        private GXMScriptWindow _demo3;
        private GXMScriptWindow _demo4;
        
        private bool _clickLatch;
        private int _btnW = 180;
        private int _btnH = 32;
        private bool _samplesBuilt = false;
        private int _framesSinceVisible = 0;
        private int _framesAfterBuild = 0; // Track frames after building to gradually show windows
        
        public GUISamples(int x, int y) : base(x, y, 640, 480) {
            Title = "GXM GUI Samples";
            ShowMinimize = true;
            ShowMaximize = true;
            ShowInTaskbar = false;
            ShowTombstone = true;
            IsResizable = true;
            // Don't build samples in constructor - wait for a few frames after becoming visible
        }
        
        private void BuildSamples() {
            if (_samplesBuilt) return;
            
            // Safety: Don't build if we're not visible yet
            if (!Visible) return;
            
            _samplesBuilt = true;
            
            // Safety: Use try-catch to prevent crashes during window creation
            try {
                // Sample 1: Basic buttons + click callbacks
                _demo1 = new GXMScriptWindow("Button Demo", 280, 220);
                if (_demo1 == null) return; // Safety check
                
                _demo1.AddLabel("Click the buttons below:", 12, 12);
                _demo1.AddButton(1, "Show Message", 12, 50, 120, 28);
                _demo1.AddButton(4, "Close Window", 12, 88, 120, 28);
                _demo1.AddOnClick(1, "MSG", "Hello from GXM scripting!");
                _demo1.AddOnClick(4, "CLOSE", "");
                _demo1.X = this.X + 12;
                _demo1.Y = this.Y + 60;
                
                // Register with window manager first, BEFORE setting visible
                WindowManager.MoveToEnd(_demo1);
                
                // Don't set visible immediately - will be done later in OnDraw
                // This ensures proper registration and initialization
            } catch {
                // If window creation fails, reset the flag so we can try again
                _samplesBuilt = false;
                _demo1 = null;
            }
            
            // COMMENT OUT OTHER WINDOWS FOR NOW - testing with just one
            /*
            // Sample 2: List view + change events
            _demo2 = new GXMScriptWindow("ListView Demo", 300, 280);
            // ... rest of demo2 setup ...
            */
        }
        
        public override void OnInput() {
            base.OnInput();
            
            if (!Visible || IsMinimized)
                return;
            
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);
            
            int pad = 12;
            int btnX = X + pad;
            int btnY = Y + 80;
            int btnGap = 8;
            
            if (left) {
                if (!_clickLatch) {
                    // Create new demo button
                    if (mx >= btnX && mx <= btnX + _btnW && 
                        my >= btnY && my <= btnY + _btnH) {
                        CreateNewDemo();
                        _clickLatch = true;
                    }
                    
                    // Reload demos button
                    btnY += _btnH + btnGap;
                    if (mx >= btnX && mx <= btnX + _btnW && 
                        my >= btnY && my <= btnY + _btnH) {
                        ReloadDemos();
                        _clickLatch = true;
                    }
                    
                    // Hide all button
                    btnY += _btnH + btnGap;
                    if (mx >= btnX && mx <= btnX + _btnW && 
                        my >= btnY && my <= btnY + _btnH) {
                        HideAllDemos();
                        _clickLatch = true;
                    }
                    
                    // Show all button
                    btnY += _btnH + btnGap;
                    if (mx >= btnX && mx <= btnX + _btnW && 
                        my >= btnY && my <= btnY + _btnH) {
                        ShowAllDemos();
                        _clickLatch = true;
                    }
                }
            } else {
                _clickLatch = false;
            }
        }
        
        public override void OnDraw() {
            base.OnDraw();
            
            // Build samples after MORE frames when window is visible
            // Increased from 3 to 10 to give more time for system to stabilize
            if (!_samplesBuilt && Visible) {
                _framesSinceVisible++;
                if (_framesSinceVisible >= 10) {
                    BuildSamples();
                }
            }
            
            // Show demo windows after they've been registered for MORE frames
            // Increased delay to ensure system is ready
            if (_samplesBuilt && _framesAfterBuild < 30) {
                _framesAfterBuild++;
                
                // Show demo1 after 10 frames from building (was 3)
                if (_framesAfterBuild == 10 && _demo1 != null && !_demo1.Visible) {
                    _demo1.Visible = true;
                }
                
                // Future: show demo2, demo3, demo4 at frames 15, 20, 25
            }
            
            int pad = 12;
            int y = Y + pad;
            
            // Title
            WindowManager.font.DrawString(X + pad, y, "GXM GUI Scripting Samples");
            y += WindowManager.font.FontSize + 8;
            
            // Description
            string desc = "This window demonstrates GXM GUI scripting capabilities.";
            WindowManager.font.DrawString(X + pad, y, desc, Width - pad * 2, WindowManager.font.FontSize * 2);
            y += WindowManager.font.FontSize * 2 + 12;
            
            // Add test message
            y += 20;
            WindowManager.font.DrawString(X + pad, y, "[TEST MODE: Only showing 1 demo window]");
            y += WindowManager.font.FontSize + 4;
            WindowManager.font.DrawString(X + pad, y, "Demo window will appear ~1 second after launch.");
            y += WindowManager.font.FontSize + 4;
            if (_samplesBuilt) {
                WindowManager.font.DrawString(X + pad, y, $"Frames since build: {_framesAfterBuild}/30");
            } else if (Visible) {
                WindowManager.font.DrawString(X + pad, y, $"Waiting to build: {_framesSinceVisible}/10");
            }
            
            // Control buttons
            int btnX = X + pad;
            int btnY = Y + 80;
            int btnGap = 8;
            
            // Button: Create New Demo
            uint col1 = UI.ButtonFillColor(btnX, btnY, _btnW, _btnH, 0xFF3A3A3A, 0xFF444444, 0xFF4A4A4A);
            Framebuffer.Graphics.FillRectangle(btnX, btnY, _btnW, _btnH, col1);
            WindowManager.font.DrawString(btnX + 12, btnY + (_btnH / 2 - WindowManager.font.FontSize / 2), 
                "Create Test Window");
            btnY += _btnH + btnGap;
            
            // Button: Reload Demos
            uint col2 = UI.ButtonFillColor(btnX, btnY, _btnW, _btnH, 0xFF3A3A3A, 0xFF444444, 0xFF4A4A4A);
            Framebuffer.Graphics.FillRectangle(btnX, btnY, _btnW, _btnH, col2);
            WindowManager.font.DrawString(btnX + 12, btnY + (_btnH / 2 - WindowManager.font.FontSize / 2), 
                "Reload All Demos");
            btnY += _btnH + btnGap;
            
            // Button: Hide All
            uint col3 = UI.ButtonFillColor(btnX, btnY, _btnW, _btnH, 0xFF3A3A3A, 0xFF444444, 0xFF4A4A4A);
            Framebuffer.Graphics.FillRectangle(btnX, btnY, _btnW, _btnH, col3);
            WindowManager.font.DrawString(btnX + 12, btnY + (_btnH / 2 - WindowManager.font.FontSize / 2), 
                "Hide All Demos");
            btnY += _btnH + btnGap;
            
            // Button: Show All
            uint col4 = UI.ButtonFillColor(btnX, btnY, _btnW, _btnH, 0xFF3A3A3A, 0xFF444444, 0xFF4A4A4A);
            Framebuffer.Graphics.FillRectangle(btnX, btnY, _btnW, _btnH, col4);
            WindowManager.font.DrawString(btnX + 12, btnY + (_btnH / 2 - WindowManager.font.FontSize / 2), 
                "Show All Demos");
            
            // Info text
            y = btnY + _btnH + 20;
            WindowManager.font.DrawString(X + pad, y, 
                "Each demo window shows different GXM features.", 
                Width - pad * 2, WindowManager.font.FontSize * 3);
            y += WindowManager.font.FontSize + 8;
            WindowManager.font.DrawString(X + pad, y, 
                "Interact with controls to test callbacks and events.", 
                Width - pad * 2, WindowManager.font.FontSize * 3);
        }
        
        private void CreateNewDemo() {
            // Safety: Use try-catch to prevent crashes
            try {
                // Create window with explicit size
                var testWin = new GXMScriptWindow("Test Window", 360, 260);
                if (testWin == null) return; // Safety check
                
                // Add controls one by one with error handling
                testWin.AddLabel("Quick Test Window", 12, 12);
                
                testWin.AddButton(1, "Show Alert", 12, 50, 120, 28);
                testWin.AddOnClick(1, "MSG", "Test alert from new window!");
                
                // Add list with limited items (3 items, well within capacity)
                testWin.AddList(2, 12, 90, 200, 100, "Test Item 1;Test Item 2;Test Item 3");
                testWin.AddOnChange(2, "MSG", "Selected: $VALUE");
                
                testWin.AddButton(99, "Close", 12, 200, 100, 28);
                testWin.AddOnClick(99, "CLOSE", "");
                
                // Position relative to parent window
                testWin.X = this.X + 220;
                testWin.Y = this.Y + 100;
                
                // Register and show window
                WindowManager.MoveToEnd(testWin);
                testWin.Visible = true;
            } catch {
                // Window creation failed - silently ignore
                // This prevents the entire OS from crashing if something goes wrong
            }
        }
        
        private void ReloadDemos() {
            // Safety: Add null checks
            try {
                // Close existing demos
                if (_demo1 != null) _demo1.Visible = false;
                if (_demo2 != null) _demo2.Visible = false;
                if (_demo3 != null) _demo3.Visible = false;
                if (_demo4 != null) _demo4.Visible = false;
                
                // Reset flags and rebuild
                _samplesBuilt = false;
                _framesSinceVisible = 0;
                _framesAfterBuild = 0;
            } catch {
                // Ignore errors during reload
            }
        }
        
        private void HideAllDemos() {
            // Safety: Add null checks
            if (_demo1 != null) _demo1.Visible = false;
            if (_demo2 != null) _demo2.Visible = false;
            if (_demo3 != null) _demo3.Visible = false;
            if (_demo4 != null) _demo4.Visible = false;
        }
        
        private void ShowAllDemos() {
            // Safety: Add null checks and ensure windows exist
            if (_demo1 != null) {
                _demo1.Visible = true;
                WindowManager.MoveToEnd(_demo1);
            }
            if (_demo2 != null) {
                _demo2.Visible = true;
                WindowManager.MoveToEnd(_demo2);
            }
            if (_demo3 != null) {
                _demo3.Visible = true;
                WindowManager.MoveToEnd(_demo3);
            }
            if (_demo4 != null) {
                _demo4.Visible = true;
                WindowManager.MoveToEnd(_demo4);
            }
        }
    }
}