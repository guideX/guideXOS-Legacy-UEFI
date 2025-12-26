using guideXOS.Kernel.Drivers;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using guideXOS.DefaultApps;
namespace guideXOS.GUI {
    internal class Taskbar {
        public StartMenu StartMenu;
        private int _barHeight;
        private Image _startIcon;
        private bool _clockUse12Hour = false;
        private bool _clockClickLatch = false;
        private bool _startClickLatch = false;
        
        // Taskbar auto-hide and slide animation
        private bool _taskbarHidden = false;
        private int _taskbarOffset = 0; // pixels hidden (0 = fully visible, _barHeight = fully hidden)
        private ulong _lastMouseActivity = 0;
        private bool _isAnimating = false;
        private int _animationStartOffset = 0;
        private int _animationTargetOffset = 0;
        private ulong _animationStartTick = 0;
        
        // Right-click context menu
        private TaskbarMenu _menu;
        private bool _rightClickLatch = false;

        // Network indicator animation
        private int _netAnimPhase = 0;
        private ulong _lastTick = 0;
        private bool _netConnectedShown = false;

        // Network animation scheduling
        private readonly ulong _bootTicks;
        private ulong _animWindowStart;
        private ulong _animWindowEnd;
        private ulong _nextCycleStart;
        private const ulong TenSeconds = 10_000;       // ms
        private const ulong FiveMinutes = 300_000;     // ms
        
        // Track actual network activity for animation
        private ulong _lastNetActivity = 0;
        private const ulong NetActivityWindow = 3_000; // show animation for 3 seconds after activity

        // New: latches and references for Workspace Switcher and Show Desktop
        private bool _taskViewLatch = false;
        private bool _showDesktopLatch = false;
        
        // DON'T use singleton - let it be created fresh but DELAY the creation
        private bool _needsWorkspaceSwitcher = false;
        private WorkspaceSwitcher _workspaceSwitcher; // Add field for the switcher instance

        // Public property to check if workspace switcher is visible (blocks input to windows)
        public bool IsWorkspaceSwitcherVisible => _workspaceSwitcher != null && _workspaceSwitcher.Visible;

        // Track windows minimized by Show Desktop to restore them on toggle
        private bool _desktopShown = false;
        private readonly List<Window> _minimizedByShowDesktop = new List<Window>(32);

        // Latch for pinned quicklaunch
        private bool _pinnedClickLatch = false;
        
        // On-Screen Keyboard button latch
        private bool _oskClickLatch = false;

        // FIXED: Cache time/date strings to prevent per-frame allocations
        private string _cachedTime = null;
        private string _cachedDate = null;
        private int _lastHour = -1;
        private int _lastMinute = -1;
        private int _lastDay = -1;

        public Taskbar(int barHeight, Image startIcon) { 
            _barHeight = barHeight; 
            _startIcon = startIcon; 
            // schedule: show animation for first 10 seconds after boot
            _bootTicks = Timer.Ticks;
            _animWindowStart = _bootTicks;
            _animWindowEnd = _bootTicks + TenSeconds;
            _nextCycleStart = _bootTicks + FiveMinutes;

            // Initialize last mouse activity to current time so auto-hide respects initial delay after startup
            // Without this, timeSinceActivity would be large on first draw and cause immediate hide.
            _lastMouseActivity = _bootTicks;
        }

        public void CloseWorkspaceSwitcher() {
            if (_workspaceSwitcher != null) {
                _workspaceSwitcher.Visible = false;
            }
        }

        /// <summary>
        /// Show the workspace switcher overlay
        /// </summary>
        public void ShowWorkspaceSwitcher() {
            _needsWorkspaceSwitcher = true;
        }

        /// <summary>
        /// Draw the workspace switcher if visible (called separately to control z-order)
        /// </summary>
        public void DrawWorkspaceSwitcher() {
            // Handle workspace switcher input FIRST (if visible)
            if (_workspaceSwitcher != null && _workspaceSwitcher.Visible) {
                _workspaceSwitcher.OnInput();
                // Draw the workspace switcher
                _workspaceSwitcher.OnDraw();
            }
        }

        public void Draw() {
            // ============================================================
            // MINIMAL BOOT MODE - Just draw a simple taskbar bar
            // Skip everything that might load files (Icons, StartMenu, etc)
            // ============================================================
            
            // Calculate taskbar position
            int yTop = Framebuffer.Height - _barHeight;
            
            // Draw simple dark taskbar background
            Framebuffer.Graphics.FillRectangle(0, yTop, Framebuffer.Width, _barHeight, 0xFF1A1A1A);
            
            // Draw a simple line at top of taskbar
            Framebuffer.Graphics.FillRectangle(0, yTop, Framebuffer.Width, 1, 0xFF333333);
            
            // Draw clock using cached strings - this should be safe
            // FIXED: Only regenerate time/date strings when the time actually changes
            bool timeChanged = (RTC.Hour != _lastHour || RTC.Minute != _lastMinute);
            bool dateChanged = (RTC.Day != _lastDay);
            
            if (_cachedTime == null || timeChanged) {
                if (_cachedTime != null) {
                    _cachedTime.Dispose();
                }
                
                if (_clockUse12Hour) {
                    bool isPM = RTC.Hour >= 12;
                    int hour12 = (RTC.Hour % 12 == 0) ? 12 : (RTC.Hour % 12);
                    string sfx = isPM ? " PM" : " AM";
                    string min = RTC.Minute < 10 ? ("0" + RTC.Minute.ToString()) : RTC.Minute.ToString();
                    string h12 = hour12.ToString();
                    string temp = h12 + ":" + min;
                    _cachedTime = temp + sfx;
                    h12.Dispose();
                    min.Dispose();
                    temp.Dispose();
                } else {
                    string h = RTC.Hour < 10 ? ("0" + RTC.Hour.ToString()) : RTC.Hour.ToString();
                    string m = RTC.Minute < 10 ? ("0" + RTC.Minute.ToString()) : RTC.Minute.ToString();
                    _cachedTime = h + ":" + m;
                    h.Dispose();
                    m.Dispose();
                }
                
                _lastHour = RTC.Hour;
                _lastMinute = RTC.Minute;
            }
            
            if (_cachedDate == null || dateChanged) {
                if (_cachedDate != null) {
                    _cachedDate.Dispose();
                }
                
                string monthStr = RTC.Month.ToString();
                string dayStr = RTC.Day.ToString();
                string yearStr = RTC.Year.ToString();
                string temp1 = monthStr + "/";
                string temp2 = temp1 + dayStr;
                string temp3 = temp2 + "/";
                _cachedDate = temp3 + yearStr;
                monthStr.Dispose();
                dayStr.Dispose();
                yearStr.Dispose();
                temp1.Dispose();
                temp2.Dispose();
                temp3.Dispose();
                
                _lastDay = RTC.Day;
            }

            // Draw time in upper right
            if (WindowManager.font != null && _cachedTime != null) {
                int timeW = WindowManager.font.MeasureString(_cachedTime);
                int timeX = Framebuffer.Width - 12 - timeW;
                int timeY = yTop + ((_barHeight - WindowManager.font.FontSize) / 2) - (WindowManager.font.FontSize/2);
                WindowManager.font.DrawString(timeX, timeY, _cachedTime);
                
                // Date below time
                if (_cachedDate != null) {
                    int dateY = timeY + WindowManager.font.FontSize;
                    WindowManager.font.DrawString(timeX, dateY, _cachedDate);
                }
            }
            
            // Draw simple start button placeholder (square)
            int startX = 12; 
            int startY = yTop + 4;
            int startSize = _barHeight - 8;
            Framebuffer.Graphics.FillRectangle(startX, startY, startSize, startSize, 0xFF2E2E2E);
            Framebuffer.Graphics.DrawRectangle(startX, startY, startSize, startSize, 0xFF3E3E3E, 1);
            
            // Skip all the complex icon loading, Start Menu, workspace switcher, etc.
            // This is MINIMAL BOOT MODE
        }

        private void ToggleShowDesktop() {
            if (!_desktopShown) {
                _minimizedByShowDesktop.Clear();
                // minimize all visible taskbar windows
                for (int i = 0; i < WindowManager.Windows.Count; i++) {
                    var w = WindowManager.Windows[i];
                    if (!w.ShowInTaskbar) continue; // skip non-taskbar elements like Start Menu
                    if (w.Visible && !w.IsMinimized) {
                        _minimizedByShowDesktop.Add(w);
                        w.Minimize();
                    }
                }
                // Hide Start menu if open
                if (StartMenu != null && StartMenu.Visible) StartMenu.Visible = false;
                _desktopShown = true;
            } else {
                // restore only those we minimized
                for (int i = 0; i < _minimizedByShowDesktop.Count; i++) {
                    var w = _minimizedByShowDesktop[i];
                    if (w != null && w.IsMinimized) w.Restore();
                }
                _minimizedByShowDesktop.Clear();
                _desktopShown = false;
            }
        }

        public void OpenOnScreenKeyboard() {
            // Check if OSK is already open
            for (int i = 0; i < WindowManager.Windows.Count; i++) {
                if (WindowManager.Windows[i] is OnScreenKeyboard osk) {
                    if (!osk.Visible) {
                        osk.Visible = true;
                        WindowManager.MoveToEnd(osk);
                    }
                    return;
                }
            }
            
            // Create new OSK window at bottom center of screen
            int oskW = 800;
            int oskH = 280;
            int oskX = (Framebuffer.Width - oskW) / 2;
            int oskY = Framebuffer.Height - _barHeight - oskH - 10;
            
            var keyboard = new OnScreenKeyboard(oskX, oskY);
            WindowManager.MoveToEnd(keyboard);
            keyboard.Visible = true;
        }
        
        private void StartAnimation(int fromOffset, int toOffset) {
            // Check if animations are disabled
            bool canSlide = (toOffset == 0 && UISettings.EnableTaskbarSlideDown) || 
                           (toOffset > 0 && UISettings.EnableTaskbarSlideUp);
            
            if (!canSlide) {
                // Instant change without animation
                _taskbarOffset = toOffset;
                return;
            }
            
            _isAnimating = true;
            _animationStartOffset = fromOffset;
            _animationTargetOffset = toOffset;
            _animationStartTick = Timer.Ticks;
        }
        
        private void UpdateAnimation() {
            if (!_isAnimating) return;
            
            ulong elapsed = Timer.Ticks >= _animationStartTick ? Timer.Ticks - _animationStartTick : 0;
            
            if (elapsed >= (ulong)UISettings.TaskbarSlideDurationMs) {
                // Animation complete
                _taskbarOffset = _animationTargetOffset;
                _isAnimating = false;
            } else {
                // Calculate interpolated position
                float t = (float)elapsed / UISettings.TaskbarSlideDurationMs;
                // Ease out cubic for smooth deceleration
                t = 1.0f - (1.0f - t) * (1.0f - t) * (1.0f - t);
                
                int delta = _animationTargetOffset - _animationStartOffset;
                _taskbarOffset = _animationStartOffset + (int)(delta * t);
            }
        }
    }
}