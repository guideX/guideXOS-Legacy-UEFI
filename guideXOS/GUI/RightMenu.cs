using guideXOS.Kernel.Drivers;
using System.Windows.Forms;
namespace guideXOS.GUI {
    /// <summary>
    /// Right Menu
    /// </summary>
    internal class RightMenu : Window {
        private bool _showIconSizeSubmenu = false;
        private int _iconSizeItemIndex = -1;

        /// <summary>
        /// Right Menu
        /// </summary>
        public RightMenu() : base(Control.MousePosition.X, Control.MousePosition.Y, 220, 200) {
            Visible = false;
        }
        /// <summary>
        /// On Set Visible
        /// </summary>
        /// <param name="value"></param>
        public override void OnSetVisible(bool value) {
            base.OnSetVisible(value);
            if (value) {
                X = Control.MousePosition.X - 8;
                Y = Control.MousePosition.Y - 8;
            }
        }
        /// <summary>
        /// On Input
        /// </summary>
        public override void OnInput() {
            if (!Visible) return;

            int itemH = 28;
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;

            bool leftClick = Control.MouseButtons.HasFlag(MouseButtons.Left);

            // Calculate Icon Size item index dynamically
            int iconSizeIdx = 2; // Start after Display Options and Performance Widget
            if (!guideXOS.OS.SystemMode.IsLiveMode) iconSizeIdx++; // Add Save Settings
            if (Desktop.Dir.Length > 0) iconSizeIdx++; // Add Up One Level
            _iconSizeItemIndex = iconSizeIdx;
            
            // Check if hovering over Icon Size item
            bool hoverIconSize = Hit(iconSizeIdx, mx, my, itemH);
            
            // Check if hovering over submenu
            bool hoverSubmenu = false;
            if (_showIconSizeSubmenu) {
                int subX = X + Width;
                int subY = Y + iconSizeIdx * itemH;
                int subW = 160;
                int subH = itemH * 5; // 5 icon sizes
                hoverSubmenu = (mx >= subX && mx <= subX + subW && my >= subY && my <= subY + subH);
            }
            
            // Show submenu on hover, hide when not hovering over main item or submenu
            if (hoverIconSize) {
                _showIconSizeSubmenu = true;
            } else if (!hoverSubmenu) {
                _showIconSizeSubmenu = false;
            }

            if (leftClick) {
                int currentItem = 0;
                
                // Item 0: Display Options
                if (Hit(currentItem, mx, my, itemH)) {
                    WindowManager.EnqueueDisplayOptions(Control.MousePosition.X, Control.MousePosition.Y, 800, 600);
                    this.Visible = false;
                    return;
                }
                currentItem++;
                
                // Item 1: Performance Widget toggle
                if (Hit(currentItem, mx, my, itemH)) {
                    if (Program.PerfWidget != null) {
                        Program.PerfWidget.Visible = !Program.PerfWidget.Visible;
                        if (Program.PerfWidget.Visible) {
                            WindowManager.MoveToEnd(Program.PerfWidget);
                        }
                    }
                    this.Visible = false;
                    return;
                }
                currentItem++;
                
                // Save Settings (only when not in LiveMode)
                if (!guideXOS.OS.SystemMode.IsLiveMode) {
                    if (Hit(currentItem, mx, my, itemH)) {
                        guideXOS.OS.Configuration.SaveConfiguration();
                        
                        // Show a confirmation message
                        if (Desktop.msgbox != null) {
                            Desktop.msgbox.X = Control.MousePosition.X + 20;
                            Desktop.msgbox.Y = Control.MousePosition.Y + 20;
                            Desktop.msgbox.SetText("Settings saved successfully!");
                            WindowManager.MoveToEnd(Desktop.msgbox);
                            Desktop.msgbox.Visible = true;
                        }
                        
                        this.Visible = false;
                        return;
                    }
                    currentItem++;
                }
                
                // Up One Level (only when not root)
                if (Desktop.Dir.Length > 0) {
                    if (Hit(currentItem, mx, my, itemH)) {
                        Desktop.Dir.Length--;

                        if (Desktop.Dir.IndexOf('/') != -1) {
                            string ndir = $"{Desktop.Dir.Substring(0, Desktop.Dir.LastIndexOf('/'))}/";
                            Desktop.Dir.Dispose();
                            Desktop.Dir = ndir;
                        } else {
                            Desktop.Dir = "";
                        }
                        this.Visible = false;
                        return;
                    }
                    currentItem++;
                }
                
                // Icon Size is the next item
                _iconSizeItemIndex = currentItem;
                
                // Handle submenu clicks
                if (_showIconSizeSubmenu && hoverSubmenu) {
                    int[] sizes = new[] { 16, 24, 32, 48, 128 };
                    int subX = X + Width;
                    int subY = Y + iconSizeIdx * itemH;
                    
                    for (int i = 0; i < sizes.Length; i++) {
                        int subItemY = subY + i * itemH;
                        if (my >= subItemY && my <= subItemY + itemH) {
                            Desktop.SetIconSize(sizes[i]);
                            this.Visible = false;
                            return;
                        }
                    }
                }

                // Click anywhere else -> close (but not on Icon Size itself, that just shows submenu)
                if (!hoverIconSize) {
                    this.Visible = false;
                }
            }
        }
        private bool Hit(int index, int mx, int my, int itemH) {
            int y = Y + index * itemH;
            return (mx >= X && mx <= X + Width && my >= y && my <= y + itemH);
        }
        /// <summary>
        /// On Draw
        /// </summary>
        public override void OnDraw() {
            int itemH = 28;
            int extra = 2 + (Desktop.Dir.Length > 0 ? 1 : 0); // +1 for Display Options, +1 for Performance Widget
            
            // Add Save Settings option if not in LiveMode
            if (!guideXOS.OS.SystemMode.IsLiveMode) {
                extra++;
            }
            
            Height = itemH * (extra + 1); // +1 for Icon Size submenu parent
            
            // Background
            Framebuffer.Graphics.AFillRectangle(X, Y, Width, Height, 0xCC222222);

            int y = Y;
            WindowManager.font.DrawString(X + 8, y + (itemH / 2) - (WindowManager.font.FontSize / 2), "Display Options"); y += itemH;
            
            // Performance Widget toggle
            string perfLabel = "Performance Widget";
            if (Program.PerfWidget != null && Program.PerfWidget.Visible) {
                perfLabel += " ?";
            }
            WindowManager.font.DrawString(X + 8, y + (itemH / 2) - (WindowManager.font.FontSize / 2), perfLabel); 
            perfLabel.Dispose();
            y += itemH;
            
            // Save Settings (only when not in LiveMode)
            if (!guideXOS.OS.SystemMode.IsLiveMode) {
                WindowManager.font.DrawString(X + 8, y + (itemH / 2) - (WindowManager.font.FontSize / 2), "Save Settings");
                y += itemH;
            }
            
            if (Desktop.Dir.Length > 0) { 
                WindowManager.font.DrawString(X + 8, y + (itemH / 2) - (WindowManager.font.FontSize / 2), "Up one level"); 
                y += itemH; 
            }
            
            // Icon Size with submenu indicator
            WindowManager.font.DrawString(X + 8, y + (itemH / 2) - (WindowManager.font.FontSize / 2), "Icon Size");
            WindowManager.font.DrawString(X + Width - 20, y + (itemH / 2) - (WindowManager.font.FontSize / 2), ">");
            
            // Draw submenu if visible
            if (_showIconSizeSubmenu) {
                int subX = X + Width;
                int subY = y;
                int subW = 160;
                int subH = itemH * 5;
                
                // Submenu background
                Framebuffer.Graphics.AFillRectangle(subX, subY, subW, subH, 0xCC222222);
                Framebuffer.Graphics.DrawRectangle(subX, subY, subW, subH, 0xFF3F3F3F, 1);
                
                int[] sizes = new[] { 16, 24, 32, 48, 128 };
                int mx = Control.MousePosition.X;
                int my = Control.MousePosition.Y;
                
                for (int i = 0; i < sizes.Length; i++) {
                    int subItemY = subY + i * itemH;
                    
                    // Hover highlight
                    if (mx >= subX && mx <= subX + subW && my >= subItemY && my <= subItemY + itemH) {
                        Framebuffer.Graphics.FillRectangle(subX + 1, subItemY, subW - 2, itemH, 0xFF313131);
                    }
                    
                    string label = sizes[i].ToString();
                    if (sizes[i] == Desktop.IconSize) {
                        label += " ?";
                    }
                    WindowManager.font.DrawString(subX + 8, subItemY + (itemH / 2) - (WindowManager.font.FontSize / 2), label);
                    label.Dispose();
                }
            }
            
            y += itemH;
            DrawBorder(false);
        }
    }
}