using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Windows.Forms;

namespace guideXOS.DockableWidgets {
    /// <summary>
    /// Uptime - Displays system uptime since boot
    /// </summary>
    internal class Uptime : DockableWidget {
        private const int WidgetWidth = 200;
        private const int WidgetHeight = 100;
        
        // Cache for formatted uptime string to prevent per-frame allocations
        private static string _cachedUptimeString = null;
        private static ulong _lastUpdateTick = 0;
        private const ulong UpdateIntervalMs = 1000; // Update every second
        
        public override int PreferredHeight => WidgetHeight - Padding * 2;
        
        /// <summary>
        /// Boot time in ticks - set from EntryPoint
        /// </summary>
        public static ulong BootTimeTicks { get; set; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        public Uptime(int X, int Y) : base(X, Y, WidgetWidth, WidgetHeight) {
            Title = "Uptime";
            ShowInStartMenu = false;
            ShowInTaskbar = false;
        }
        
        public override void OnInput() {
            if (!Visible) return;
            
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool leftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            
            // Close button hit test
            int closeX = X + Width - Padding - CloseBtnSize;
            int closeY = Y + Padding;
            _closeHover = mx >= closeX && mx <= closeX + CloseBtnSize && 
                          my >= closeY && my <= closeY + CloseBtnSize;
            
            if (leftDown && _closeHover) {
                Visible = false;
                return;
            }
            
            // Handle dragging
            HandleDragging();
        }
        
        /// <summary>
        /// On Draw
        /// </summary>
        public override void OnDraw() {
            if (!Visible) return;
            
            // Draw widget background with subtle glow
            UIPrimitives.AFillRoundedRect(X - 2, Y - 2, Width + 4, Height + 4, 0x331E90FF, 8);
            UIPrimitives.AFillRoundedRect(X, Y, Width, Height, 0xDD1A1A1A, 6);
            
            // Draw border
            UIPrimitives.DrawRoundedRect(X, Y, Width, Height, 0xFF3A3A3A, 1, 6);
            
            int cy = Y + Padding;
            int cx = X + Padding;
            int contentWidth = Width - Padding * 2;
            
            DrawContent(cx, cy, contentWidth);
            
            // Draw close button
            int closeX = X + Width - Padding - CloseBtnSize;
            int closeY = Y + Padding;
            DrawCloseButton(closeX, closeY);
        }
        
        public override void DrawContent(int contentX, int contentY, int contentWidth) {
            // Update uptime string only once per second to prevent allocations
            ulong currentTick = Timer.Ticks;
            if (_cachedUptimeString == null || (currentTick - _lastUpdateTick) >= UpdateIntervalMs) {
                // Dispose old cached string
                if (_cachedUptimeString != null) {
                    _cachedUptimeString.Dispose();
                }
                
                // Calculate uptime in milliseconds
                ulong uptimeMs = currentTick - BootTimeTicks;
                
                // Convert to days, hours, minutes, seconds
                ulong totalSeconds = uptimeMs / 1000;
                ulong days = totalSeconds / 86400;
                ulong hours = (totalSeconds % 86400) / 3600;
                ulong minutes = (totalSeconds % 3600) / 60;
                ulong seconds = totalSeconds % 60;
                
                // Format uptime string
                string uptimeText = "System Uptime:";
                string timeString;
                
                if (days > 0) {
                    // Show days + hours
                    string daysStr = days.ToString();
                    string hoursStr = hours.ToString();
                    timeString = daysStr + "d " + hoursStr + "h";
                    daysStr.Dispose();
                    hoursStr.Dispose();
                } else if (hours > 0) {
                    // Show hours + minutes
                    string hoursStr = hours.ToString();
                    string minutesStr = minutes < 10 ? "0" + minutes.ToString() : minutes.ToString();
                    timeString = hoursStr + ":" + minutesStr + ":" + (seconds < 10 ? "0" : "") + seconds.ToString();
                    hoursStr.Dispose();
                    minutesStr.Dispose();
                } else if (minutes > 0) {
                    // Show minutes + seconds
                    string minutesStr = minutes.ToString();
                    string secondsStr = seconds < 10 ? "0" + seconds.ToString() : seconds.ToString();
                    timeString = minutesStr + ":" + secondsStr;
                    minutesStr.Dispose();
                    secondsStr.Dispose();
                } else {
                    // Show just seconds
                    string secondsStr = seconds.ToString();
                    timeString = secondsStr + "s";
                    secondsStr.Dispose();
                }
                
                _cachedUptimeString = uptimeText + "\n" + timeString;
                uptimeText.Dispose();
                timeString.Dispose();
                
                _lastUpdateTick = currentTick;
            }
            
            // Draw title
            if (WindowManager.font != null) {
                WindowManager.font.DrawString(contentX + 4, contentY + 4, "Uptime");
            }
            
            // Draw cached uptime string (no allocations per frame)
            if (WindowManager.font != null && _cachedUptimeString != null) {
                // Split the string by newline and draw each line
                int lineHeight = 20;
                int yOffset = contentY + 28;
                
                // Find newline position
                int newlinePos = -1;
                for (int i = 0; i < _cachedUptimeString.Length; i++) {
                    if (_cachedUptimeString[i] == '\n') {
                        newlinePos = i;
                        break;
                    }
                }
                
                if (newlinePos >= 0) {
                    // Draw first line (label)
                    string line1 = _cachedUptimeString.Substring(0, newlinePos);
                    WindowManager.font.DrawString(contentX + 4, yOffset, line1);
                    line1.Dispose();
                    
                    // Draw second line (time value)
                    string line2 = _cachedUptimeString.Substring(newlinePos + 1);
                    // Center the time value
                    int textWidth = WindowManager.font.MeasureString(line2);
                    int centeredX = contentX + (contentWidth - textWidth) / 2;
                    WindowManager.font.DrawString(centeredX, yOffset + lineHeight, line2);
                    line2.Dispose();
                } else {
                    // Single line fallback
                    WindowManager.font.DrawString(contentX + 4, yOffset, _cachedUptimeString);
                }
            }
        }
    }
}
