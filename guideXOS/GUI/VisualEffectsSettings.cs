using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace guideXOS.GUI {
    /// <summary>
    /// Visual Effects Settings Window - Comprehensive settings for all GUI parameters organized in tabs
    /// </summary>
    internal class VisualEffectsSettings : Window {
        private int _padding = 20;
        private int _lineHeight = 44;
        private int _labelWidth = 300;
        private int _sliderWidth = 220;
        private int _checkboxSize = 24;
        
        // Tab system
        private int _tabH = 40;
        private int _tabGap = 10;
        private int _currentTab = 0; // 0=Animations, 1=Visual Effects, 2=Window Rendering, 3=Performance, 4=Widgets
        
        // Scroll state
        private int _scrollY = 0;
        private bool _scrollDragging = false;
        private int _scrollStartY = 0;
        private int _scrollStartScroll = 0;
        
        // Slider state
        private int _draggingSlider = -1;
        
        // Button state
        private bool _btnClickLatch = false;
        
        // Local copies of settings - Animations Tab
        private bool _enableFadeAnimations;
        private int _fadeInDuration;
        private int _fadeOutDuration;
        private bool _enableWindowSlideAnimations;
        private int _windowSlideDuration;
        private bool _enableFadeOverlay;
        private bool _enableAnimationUpdates;
        
        // Visual Effects Tab
        private bool _enableBlurredTitleBars;
        private bool _enableTransparentWindows;
        private bool _enableWindowGlow;
        private bool _enableRoundedCorners;
        private bool _enableButtonHoverEffects;
        private bool _enableResizeGrip;
        private bool _enableBlurCaching;
        private bool _enableBlurCacheInvalidation;
        private bool _enableBlurCacheDisposal;
        
        // Window Rendering Tab
        private bool _enableWindowTitles;
        private bool _enableWindowBorders;
        private bool _enableTitleBarButtons;
        private bool _enableTitleBarBackground;
        private bool _enableWindowContentBackground;
        private bool _enableButtonBackgrounds;
        private bool _enableButtonBorders;
        private bool _enableButtonIcons;
        private bool _enableTaskbarIcons;
        private int _buttonSizeOffset;
        private int _buttonSpacing;
        
        // Performance Tab
        private bool _enableDrawCallCaching;
        private bool _enableGeometryBatching;
        private bool _skipOffscreenRendering;
        private bool _skipMinimizedRendering;
        private bool _skipInvisibleRendering;
        private bool _checkFramebufferBeforeRender;
        private bool _checkFontBeforeRender;
        private bool _enableEarlyReturnOptimizations;
        
        // Widget Tab
        private bool _enableWidgetRendering;
        private bool _enableWidgetTransparency;
        private bool _enableWidgetBorders;
        private bool _enableWidgetShadows;
        private bool _enableWidgetFadeAnimations;
        private bool _enableWidgetHoverAnimations;
        private int _widgetFadeInDuration;
        private int _widgetFadeOutDuration;
        private int _widgetDefaultRefreshRate;
        
        // Background Rotation Tab (added to Visual Effects)
        private bool _enableAutoBackgroundRotation;
        private bool _enableRandomBackgroundOnStartup;
        private int _backgroundRotationInterval;
        private bool _enableBackgroundFadeTransition;
        private int _backgroundFadeDuration;
        
        // Taskbar Settings (added to Visual Effects)
        private bool _enableTaskbarAutoHide;
        private bool _enableTaskbarSlideDown;
        private bool _enableTaskbarSlideUp;
        private int _taskbarAutoHideDelay;
        private int _taskbarSlideDuration;
        
        // Special Effects Tab
        private bool _enableSpecialWindowEffects;
        private WindowEffectType _windowOpenEffect;
        private WindowEffectType _windowCloseEffect;
        private int _specialEffectDuration;
        private int _digitizeScanlineCount;
        private int _burnParticleCount;
        private int _smokeParticleCount;
        private int _glitchIntensity;
        private int _rippleWaveCount;
        private int _explodeParticleCount;
        
        // Dropdown states
        private bool _openEffectDropdownOpen = false;
        private bool _closeEffectDropdownOpen = false;
        
        public VisualEffectsSettings(int X, int Y) : base(X, Y, 900, 700) {
            IsResizable = false;
            ShowInTaskbar = true;
            ShowMaximize = false;
            ShowMinimize = true;
            ShowTombstone = true;
            Title = "Visual Effects & UI Settings";
            
            LoadSettings();
        }
        
        private void LoadSettings() {
            // Animations
            _enableFadeAnimations = UISettings.EnableFadeAnimations;
            _fadeInDuration = UISettings.FadeInDurationMs;
            _fadeOutDuration = UISettings.FadeOutDurationMs;
            _enableWindowSlideAnimations = UISettings.EnableWindowSlideAnimations;
            _windowSlideDuration = UISettings.WindowSlideDurationMs;
            _enableFadeOverlay = UISettings.EnableFadeOverlay;
            _enableAnimationUpdates = UISettings.EnableAnimationUpdates;
            
            // Visual Effects
            _enableBlurredTitleBars = UISettings.EnableBlurredTitleBars;
            _enableTransparentWindows = UISettings.EnableTransparentWindows;
            _enableWindowGlow = UISettings.EnableWindowGlow;
            _enableRoundedCorners = UISettings.EnableRoundedCorners;
            _enableButtonHoverEffects = UISettings.EnableButtonHoverEffects;
            _enableResizeGrip = UISettings.EnableResizeGrip;
            _enableBlurCaching = UISettings.EnableBlurCaching;
            _enableBlurCacheInvalidation = UISettings.EnableBlurCacheInvalidation;
            _enableBlurCacheDisposal = UISettings.EnableBlurCacheDisposal;
            
            // Background Rotation
            _enableAutoBackgroundRotation = UISettings.EnableAutoBackgroundRotation;
            _enableRandomBackgroundOnStartup = UISettings.EnableRandomBackgroundOnStartup;
            _backgroundRotationInterval = UISettings.BackgroundRotationIntervalMinutes;
            _enableBackgroundFadeTransition = UISettings.EnableBackgroundFadeTransition;
            _backgroundFadeDuration = UISettings.BackgroundFadeDurationMs;
            
            // Taskbar
            _enableTaskbarAutoHide = UISettings.EnableTaskbarAutoHide;
            _enableTaskbarSlideDown = UISettings.EnableTaskbarSlideDown;
            _enableTaskbarSlideUp = UISettings.EnableTaskbarSlideUp;
            _taskbarAutoHideDelay = UISettings.TaskbarAutoHideDelayMs;
            _taskbarSlideDuration = UISettings.TaskbarSlideDurationMs;
            
            // Window Rendering
            _enableWindowTitles = UISettings.EnableWindowTitles;
            _enableWindowBorders = UISettings.EnableWindowBorders;
            _enableTitleBarButtons = UISettings.EnableTitleBarButtons;
            _enableTitleBarBackground = UISettings.EnableTitleBarBackground;
            _enableWindowContentBackground = UISettings.EnableWindowContentBackground;
            _enableButtonBackgrounds = UISettings.EnableButtonBackgrounds;
            _enableButtonBorders = UISettings.EnableButtonBorders;
            _enableButtonIcons = UISettings.EnableButtonIcons;
            _enableTaskbarIcons = UISettings.EnableTaskbarIcons;
            _buttonSizeOffset = UISettings.ButtonSizeOffset;
            _buttonSpacing = UISettings.ButtonSpacing;
            
            // Performance
            _enableDrawCallCaching = UISettings.EnableDrawCallCaching;
            _enableGeometryBatching = UISettings.EnableGeometryBatching;
            _skipOffscreenRendering = UISettings.SkipOffscreenRendering;
            _skipMinimizedRendering = UISettings.SkipMinimizedRendering;
            _skipInvisibleRendering = UISettings.SkipInvisibleRendering;
            _checkFramebufferBeforeRender = UISettings.CheckFramebufferBeforeRender;
            _checkFontBeforeRender = UISettings.CheckFontBeforeRender;
            _enableEarlyReturnOptimizations = UISettings.EnableEarlyReturnOptimizations;
            
            // Widgets
            _enableWidgetRendering = UISettings.EnableWidgetRendering;
            _enableWidgetTransparency = UISettings.EnableWidgetTransparency;
            _enableWidgetBorders = UISettings.EnableWidgetBorders;
            _enableWidgetShadows = UISettings.EnableWidgetShadows;
            _enableWidgetFadeAnimations = UISettings.EnableWidgetFadeAnimations;
            _enableWidgetHoverAnimations = UISettings.EnableWidgetHoverAnimations;
            _widgetFadeInDuration = UISettings.WidgetFadeInDurationMs;
            _widgetFadeOutDuration = UISettings.WidgetFadeOutDurationMs;
            _widgetDefaultRefreshRate = UISettings.WidgetDefaultRefreshRateMs;
            
            // Special Effects
            _enableSpecialWindowEffects = UISettings.EnableSpecialWindowEffects;
            _windowOpenEffect = UISettings.WindowOpenEffect;
            _windowCloseEffect = UISettings.WindowCloseEffect;
            _specialEffectDuration = UISettings.SpecialEffectDurationMs;
            _digitizeScanlineCount = UISettings.DigitizeScanlineCount;
            _burnParticleCount = UISettings.BurnParticleCount;
            _smokeParticleCount = UISettings.SmokeParticleCount;
            _glitchIntensity = UISettings.GlitchIntensity;
            _rippleWaveCount = UISettings.RippleWaveCount;
            _explodeParticleCount = UISettings.ExplodeParticleCount;
        }
        
        public override void OnInput() {
            if (!Visible) return;
            if (!IsUnderMouse()) {
                base.OnInput();
                return;
            }
            
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool leftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            
            int cx = X + _padding;
            int cy = Y + _padding;
            int cw = Width - _padding * 2;
            int contentY = cy + _tabH + _tabGap;
            int contentH = Height - _padding * 2 - _tabH - _tabGap - 60;
            
            if (leftDown) {
                if (!_btnClickLatch) {
                    // Tab clicks
                    int tabW = (cw - _tabGap * 5) / 6; // 6 tabs
                    int tabY = cy;
                    for (int i = 0; i < 6; i++) {
                        int tabX = cx + i * (tabW + _tabGap);
                        if (mx >= tabX && mx <= tabX + tabW && my >= tabY && my <= tabY + _tabH) {
                            _currentTab = i;
                            _scrollY = 0; // Reset scroll when switching tabs
                            _btnClickLatch = true;
                            return;
                        }
                    }
                    
                    // Apply and Reset buttons
                    int btnW = 120;
                    int btnH = 38;
                    int btnY = Y + Height - _padding - btnH - 10;
                    int applyX = X + Width - _padding - btnW;
                    int resetX = applyX - btnW - 16;
                    
                    if (mx >= applyX && mx <= applyX + btnW && my >= btnY && my <= btnY + btnH) {
                        ApplySettings();
                        _btnClickLatch = true;
                        return;
                    }
                    
                    if (mx >= resetX && mx <= resetX + btnW && my >= btnY && my <= btnY + btnH) {
                        ResetToDefaults();
                        _btnClickLatch = true;
                        return;
                    }
                }
                
                // Scrollbar handling
                int sbW = 12;
                int sbX = X + Width - _padding - sbW;
                int sbY = contentY;
                int sbH = contentH;
                
                if (!_scrollDragging && !_btnClickLatch) {
                    if (mx >= sbX && mx <= sbX + sbW && my >= sbY && my <= sbY + sbH) {
                        _scrollDragging = true;
                        _scrollStartY = my;
                        _scrollStartScroll = _scrollY;
                    }
                }
                
                // Process tab-specific inputs
                if (!_btnClickLatch && !_scrollDragging) {
                    ProcessTabInput(mx, my, cx, contentY, contentH);
                }
                
            } else {
                _scrollDragging = false;
                _draggingSlider = -1;
                _btnClickLatch = false;
            }
            
            // Update scroll position
            if (_scrollDragging) {
                int totalHeight = GetCurrentTabHeight();
                int maxScroll = totalHeight > contentH ? totalHeight - contentH : 0;
                int dy = my - _scrollStartY;
                _scrollY = _scrollStartScroll + dy;
                if (_scrollY < 0) _scrollY = 0;
                if (_scrollY > maxScroll) _scrollY = maxScroll;
            }
            
            if (!_btnClickLatch && !_scrollDragging && _draggingSlider < 0) {
                base.OnInput();
            }
        }
        
        private void ProcessTabInput(int mx, int my, int cx, int contentY, int contentH) {
            int currentY = contentY - _scrollY;
            
            switch (_currentTab) {
                case 0: ProcessAnimationsInput(mx, my, cx, currentY, contentY, contentH); break;
                case 1: ProcessVisualEffectsInput(mx, my, cx, currentY, contentY, contentH); break;
                case 2: ProcessWindowRenderingInput(mx, my, cx, currentY, contentY, contentH); break;
                case 3: ProcessPerformanceInput(mx, my, cx, currentY, contentY, contentH); break;
                case 4: ProcessWidgetsInput(mx, my, cx, currentY, contentY, contentH); break;
                case 5: ProcessSpecialEffectsInput(mx, my, cx, currentY, contentY, contentH); break;
            }
        }
        
        private void ProcessAnimationsInput(int mx, int my, int cx, int currentY, int contentY, int contentH) {
            currentY += _lineHeight; // Skip title
            
            // Checkboxes
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableFadeAnimations = !_enableFadeAnimations; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            // Fade In slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 0; }
            currentY += _lineHeight;
            
            // Fade Out slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 1; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableWindowSlideAnimations = !_enableWindowSlideAnimations; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            // Slide Duration slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 2; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableFadeOverlay = !_enableFadeOverlay; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableAnimationUpdates = !_enableAnimationUpdates; _btnClickLatch = true; return; }
            
            // Handle slider dragging
            if (_draggingSlider >= 0) {
                float t = (float)(mx - (cx + _labelWidth)) / _sliderWidth;
                if (t < 0) t = 0;
                if (t > 1) t = 1;
                
                switch (_draggingSlider) {
                    case 0: _fadeInDuration = (int)(50 + t * 950); break; // 50-1000ms
                    case 1: _fadeOutDuration = (int)(50 + t * 950); break;
                    case 2: _windowSlideDuration = (int)(50 + t * 950); break;
                }
            }
        }
        
        private void ProcessVisualEffectsInput(int mx, int my, int cx, int currentY, int contentY, int contentH) {
            currentY += _lineHeight; // Skip title
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableBlurredTitleBars = !_enableBlurredTitleBars; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableTransparentWindows = !_enableTransparentWindows; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableWindowGlow = !_enableWindowGlow; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableRoundedCorners = !_enableRoundedCorners; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableButtonHoverEffects = !_enableButtonHoverEffects; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableResizeGrip = !_enableResizeGrip; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            currentY += _lineHeight; // Skip subtitle
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableBlurCaching = !_enableBlurCaching; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableBlurCacheInvalidation = !_enableBlurCacheInvalidation; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableBlurCacheDisposal = !_enableBlurCacheDisposal; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            currentY += _lineHeight; // Skip subtitle
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableAutoBackgroundRotation = !_enableAutoBackgroundRotation; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableRandomBackgroundOnStartup = !_enableRandomBackgroundOnStartup; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            // Background Rotation Interval slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 10; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableBackgroundFadeTransition = !_enableBackgroundFadeTransition; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            // Background Fade Duration slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 11; }
            currentY += _lineHeight;
            
            currentY += _lineHeight; // Skip subtitle
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableTaskbarAutoHide = !_enableTaskbarAutoHide; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableTaskbarSlideDown = !_enableTaskbarSlideDown; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableTaskbarSlideUp = !_enableTaskbarSlideUp; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            // Taskbar Auto-Hide Delay slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 12; }
            currentY += _lineHeight;
            
            // Taskbar Slide Duration slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 13; }
            
            // Handle slider dragging
            if (_draggingSlider >= 0) {
                float t = (float)(mx - (cx + _labelWidth)) / _sliderWidth;
                if (t < 0) t = 0;
                if (t > 1) t = 1;
                
                switch (_draggingSlider) {
                    case 10: _backgroundRotationInterval = (int)(1 + t * 59); break; // 1-60 minutes
                    case 11: _backgroundFadeDuration = (int)(200 + t * 4800); break; // 200-5000ms
                    case 12: _taskbarAutoHideDelay = (int)(500 + t * 4500); break; // 500-5000ms
                    case 13: _taskbarSlideDuration = (int)(100 + t * 900); break; // 100-1000ms
                }
            }
        }
        
        private void ProcessWindowRenderingInput(int mx, int my, int cx, int currentY, int contentY, int contentH) {
            currentY += _lineHeight; // Skip title
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableWindowTitles = !_enableWindowTitles; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableWindowBorders = !_enableWindowBorders; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableTitleBarButtons = !_enableTitleBarButtons; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableTitleBarBackground = !_enableTitleBarBackground; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableWindowContentBackground = !_enableWindowContentBackground; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            currentY += _lineHeight; // Skip subtitle
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableButtonBackgrounds = !_enableButtonBackgrounds; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableButtonBorders = !_enableButtonBorders; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableButtonIcons = !_enableButtonIcons; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableTaskbarIcons = !_enableTaskbarIcons; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            // Button Size Offset slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 20; }
            currentY += _lineHeight;
            
            // Button Spacing slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 21; }
            
            // Handle slider dragging
            if (_draggingSlider >= 0) {
                float t = (float)(mx - (cx + _labelWidth)) / _sliderWidth;
                if (t < 0) t = 0;
                if (t > 1) t = 1;
                
                switch (_draggingSlider) {
                    case 20: _buttonSizeOffset = (int)(4 + t * 20); break; // 4-24
                    case 21: _buttonSpacing = (int)(2 + t * 18); break; // 2-20
                }
            }
        }
        
        private void ProcessPerformanceInput(int mx, int my, int cx, int currentY, int contentY, int contentH) {
            currentY += _lineHeight; // Skip title
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableDrawCallCaching = !_enableDrawCallCaching; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableGeometryBatching = !_enableGeometryBatching; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _skipOffscreenRendering = !_skipOffscreenRendering; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _skipMinimizedRendering = !_skipMinimizedRendering; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _skipInvisibleRendering = !_skipInvisibleRendering; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _checkFramebufferBeforeRender = !_checkFramebufferBeforeRender; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _checkFontBeforeRender = !_checkFontBeforeRender; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableEarlyReturnOptimizations = !_enableEarlyReturnOptimizations; _btnClickLatch = true; return; }
        }
        
        private void ProcessWidgetsInput(int mx, int my, int cx, int currentY, int contentY, int contentH) {
            currentY += _lineHeight; // Skip title
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableWidgetRendering = !_enableWidgetRendering; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableWidgetTransparency = !_enableWidgetTransparency; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableWidgetBorders = !_enableWidgetBorders; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableWidgetShadows = !_enableWidgetShadows; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            currentY += _lineHeight; // Skip subtitle
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableWidgetFadeAnimations = !_enableWidgetFadeAnimations; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableWidgetHoverAnimations = !_enableWidgetHoverAnimations; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            // Widget Fade In slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 30; }
            currentY += _lineHeight;
            
            // Widget Fade Out slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 31; }
            currentY += _lineHeight;
            
            // Widget Refresh Rate slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 32; }
            
            // Handle slider dragging
            if (_draggingSlider >= 0) {
                float t = (float)(mx - (cx + _labelWidth)) / _sliderWidth;
                if (t < 0) t = 0;
                if (t > 1) t = 1;
                
                switch (_draggingSlider) {
                    case 30: _widgetFadeInDuration = (int)(50 + t * 950); break; // 50-1000ms
                    case 31: _widgetFadeOutDuration = (int)(50 + t * 950); break;
                    case 32: _widgetDefaultRefreshRate = (int)(100 + t * 4900); break; // 100-5000ms
                }
            }
        }
        
        private bool CheckCheckbox(int mx, int my, int cx, int cy, int contentY, int contentH) {
            if (cy < contentY - _lineHeight || cy > contentY + contentH) return false;
            return mx >= cx && mx <= cx + _checkboxSize && my >= cy && my <= cy + _checkboxSize;
        }
        
        private bool CheckSlider(int mx, int my, int sx, int sy, int contentY, int contentH) {
            if (sy < contentY - _lineHeight || sy > contentY + contentH) return false;
            return my >= sy + 8 && my <= sy + 24 && mx >= sx && mx <= sx + _sliderWidth;
        }
        
        public override void OnDraw() {
            base.OnDraw();
            if (WindowManager.font == null) return;
            
            var g = Framebuffer.Graphics;
            int cx = X + _padding;
            int cy = Y + _padding;
            int cw = Width - _padding * 2;
            int contentY = cy + _tabH + _tabGap;
            int contentH = Height - _padding * 2 - _tabH - _tabGap - 60;
            
            // Draw tabs
            int tabW = (cw - _tabGap * 5) / 6;
            int tabY = cy;
            string[] tabNames = { "Animations", "Visual Effects", "Rendering", "Performance", "Widgets", "Special Effects" };
            
            for (int i = 0; i < 6; i++) {
                int tabX = cx + i * (tabW + _tabGap);
                uint tabBg = (_currentTab == i) ? 0xFF3A3A3A : 0xFF252525;
                g.FillRectangle(tabX, tabY, tabW, _tabH, tabBg);
                g.DrawRectangle(tabX, tabY, tabW, _tabH, 0xFF4F4F4F, 1);
                
                int textX = tabX + (tabW / 2) - (tabNames[i].Length * WindowManager.font.FontSize / 4);
                int textY = tabY + (_tabH / 2 - WindowManager.font.FontSize / 2);
                WindowManager.font.DrawString(textX, textY, tabNames[i]);
            }
            
            // Draw content background
            g.FillRectangle(cx - 4, contentY - 4, cw + 8 - 16, contentH + 8, 0xFF1A1A1A);
            
            // Draw tab content
            switch (_currentTab) {
                case 0: DrawAnimationsTab(cx, contentY, contentH); break;
                case 1: DrawVisualEffectsTab(cx, contentY, contentH); break;
                case 2: DrawWindowRenderingTab(cx, contentY, contentH); break;
                case 3: DrawPerformanceTab(cx, contentY, contentH); break;
                case 4: DrawWidgetsTab(cx, contentY, contentH); break;
                case 5: DrawSpecialEffectsTab(cx, contentY, contentH); break;
            }
            
            // Draw scrollbar
            DrawScrollbar(cx, contentY, cw, contentH);
            
            // Draw buttons
            DrawButtons();
        }
        
        private void DrawAnimationsTab(int cx, int contentY, int contentH) {
            int currentY = contentY - _scrollY;
            
            DrawTitle(cx, currentY, "Window Animation Settings");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableFadeAnimations, "Enable Fade Animations");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "  Fade In Duration:", _fadeInDuration, 50, 1000, " ms");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "  Fade Out Duration:", _fadeOutDuration, 50, 1000, " ms");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableWindowSlideAnimations, "Enable Window Slide Animations");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "  Slide Duration:", _windowSlideDuration, 50, 1000, " ms");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableFadeOverlay, "Enable Fade Overlay");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableAnimationUpdates, "Enable Animation Position Updates");
        }
        
        private void DrawVisualEffectsTab(int cx, int contentY, int contentH) {
            int currentY = contentY - _scrollY;
            
            DrawTitle(cx, currentY, "Window Visual Effects");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableBlurredTitleBars, "Enable Blurred Title Bars (Aero Glass)");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableTransparentWindows, "Enable Transparent Windows");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableWindowGlow, "Enable Window Glow/Shadow");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableRoundedCorners, "Enable Rounded Corners (WARNING: Memory Leak)");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableButtonHoverEffects, "Enable Button Hover Effects");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableResizeGrip, "Enable Resize Grip Visual");
            currentY += _lineHeight;
            
            DrawSubtitle(cx, currentY, "Blur Cache Settings");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableBlurCaching, "Enable Blur Caching");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableBlurCacheInvalidation, "Enable Cache Invalidation on Move/Resize");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableBlurCacheDisposal, "Dispose Cache When Hiding Window");
            currentY += _lineHeight;
            
            DrawSubtitle(cx, currentY, "Background Rotation");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableAutoBackgroundRotation, "Auto-Rotate Desktop Backgrounds");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableRandomBackgroundOnStartup, "Random Background on Startup (ignored if auto-rotate on)");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "  Rotation Interval:", _backgroundRotationInterval, 1, 60, " min");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableBackgroundFadeTransition, "Enable Background Fade Transition");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "  Transition Duration:", _backgroundFadeDuration, 200, 5000, " ms");
            currentY += _lineHeight;
            
            DrawSubtitle(cx, currentY, "Taskbar Settings");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableTaskbarAutoHide, "Auto-Hide Taskbar");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableTaskbarSlideDown, "Slide Down Animation (Show)");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableTaskbarSlideUp, "Slide Up Animation (Hide)");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "  Auto-Hide Delay:", _taskbarAutoHideDelay, 500, 5000, " ms");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "  Slide Duration:", _taskbarSlideDuration, 100, 1000, " ms");
        }
        
        private void DrawWindowRenderingTab(int cx, int contentY, int contentH) {
            int currentY = contentY - _scrollY;
            
            DrawTitle(cx, currentY, "Window Rendering Settings");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableWindowTitles, "Enable Window Titles");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableWindowBorders, "Enable Window Borders");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableTitleBarButtons, "Enable Title Bar Buttons");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableTitleBarBackground, "Enable Title Bar Background");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableWindowContentBackground, "Enable Window Content Background");
            currentY += _lineHeight;
            
            currentY += _lineHeight; // Skip subtitle
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableButtonBackgrounds, "Enable Button Backgrounds");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableButtonBorders, "Enable Button Borders");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableButtonIcons, "Enable Button Icons");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableTaskbarIcons, "Enable Taskbar Icons");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "Button Size Offset:", _buttonSizeOffset, 4, 24, " px");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "Button Spacing:", _buttonSpacing, 2, 20, " px");
        }
        
        private void DrawPerformanceTab(int cx, int contentY, int contentH) {
            int currentY = contentY - _scrollY;
            
            DrawTitle(cx, currentY, "Performance Optimization Settings");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableDrawCallCaching, "Enable Draw Call Caching");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableGeometryBatching, "Enable Geometry Batching");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _skipOffscreenRendering, "Skip Off-screen Window Rendering");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _skipMinimizedRendering, "Skip Minimized Window Rendering");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _skipInvisibleRendering, "Skip Invisible Window Rendering");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _checkFramebufferBeforeRender, "Check Framebuffer Before Render");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _checkFontBeforeRender, "Check Font Before Render");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableEarlyReturnOptimizations, "Enable Early Return Optimizations");
        }
        
        private void DrawWidgetsTab(int cx, int contentY, int contentH) {
            int currentY = contentY - _scrollY;
            
            DrawTitle(cx, currentY, "Widget Settings");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableWidgetRendering, "Enable Widget Rendering");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableWidgetTransparency, "Enable Widget Transparency");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableWidgetBorders, "Enable Widget Borders");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableWidgetShadows, "Enable Widget Shadows");
            currentY += _lineHeight;
            
            DrawSubtitle(cx, currentY, "Widget Animations");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableWidgetFadeAnimations, "Enable Widget Fade Animations");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableWidgetHoverAnimations, "Enable Widget Hover Animations");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "Widget Fade In Duration:", _widgetFadeInDuration, 50, 1000, " ms");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "Widget Fade Out Duration:", _widgetFadeOutDuration, 50, 1000, " ms");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "Default Refresh Rate:", _widgetDefaultRefreshRate, 100, 5000, " ms");
        }
        
        private void DrawTitle(int cx, int cy, string text) {
            if (cy >= Y + _padding + _tabH + _tabGap - _lineHeight && cy <= Y + Height - _padding - 60) {
                WindowManager.font.DrawString(cx, cy, text);
            }
        }
        
        private void DrawSubtitle(int cx, int cy, string text) {
            if (cy >= Y + _padding + _tabH + _tabGap - _lineHeight && cy <= Y + Height - _padding - 60) {
                WindowManager.font.DrawString(cx, cy, text);
            }
        }
        
        private void DrawCheckboxWithLabel(int cx, int cy, int contentY, int contentH, bool checked_, string label) {
            if (cy < contentY - _lineHeight || cy > contentY + contentH) return;
            
            DrawCheckbox(cx, cy, checked_);
            WindowManager.font.DrawString(cx + _checkboxSize + 12, cy + (_checkboxSize - WindowManager.font.FontSize) / 2, label);
        }
        
        private void DrawSliderWithLabel(int cx, int cy, int contentY, int contentH, string label, int value, int min, int max, string suffix) {
            if (cy < contentY - _lineHeight || cy > contentY + contentH) return;
            
            WindowManager.font.DrawString(cx + 32, cy, label);
            float t = (float)(value - min) / (max - min);
            DrawSlider(cx + _labelWidth, cy, _sliderWidth, t);
            string val = value.ToString() + suffix;
            WindowManager.font.DrawString(cx + _labelWidth + _sliderWidth + 16, cy, val);
            val.Dispose();
        }
        
        private void DrawCheckbox(int x, int y, bool checked_) {
            var g = Framebuffer.Graphics;
            
            g.FillRectangle(x, y, _checkboxSize, _checkboxSize, 0xFF2A2A2A);
            g.DrawRectangle(x, y, _checkboxSize, _checkboxSize, 0xFF4F4F4F, 2);
            
            if (checked_) {
                for (int i = 0; i < 3; i++) {
                    g.FillRectangle(x + 6 + i, y + 10 + i, 2, 2, 0xFF4A8FD8);
                    g.FillRectangle(x + 8 + i, y + 12 - i, 2, 2, 0xFF4A8FD8);
                    g.FillRectangle(x + 10 + i, y + 10 - i, 2, 2, 0xFF4A8FD8);
                }
            }
        }
        
        private void DrawSlider(int x, int y, int width, float value) {
            var g = Framebuffer.Graphics;
            
            if (value < 0) value = 0;
            if (value > 1) value = 1;
            
            int trackY = y + 12;
            int trackH = 6;
            
            g.FillRectangle(x, trackY, width, trackH, 0xFF1A1A1A);
            g.DrawRectangle(x, trackY, width, trackH, 0xFF3F3F3F, 1);
            
            int fillW = (int)(width * value);
            if (fillW > 0) {
                g.FillRectangle(x, trackY, fillW, trackH, 0xFF4A8FD8);
            }
            
            int thumbX = x + (int)(width * value) - 8;
            int thumbY = y + 8;
            int thumbSize = 16;
            
            g.FillRectangle(thumbX, thumbY, thumbSize, thumbSize, 0xFF4A8FD8);
            g.DrawRectangle(thumbX, thumbY, thumbSize, thumbSize, 0xFF6AAFF8, 2);
        }
        
        private void DrawScrollbar(int cx, int contentY, int cw, int contentH) {
            var g = Framebuffer.Graphics;
            int sbW = 12;
            int sbX = X + Width - _padding - sbW;
            int sbY = contentY;
            int sbH = contentH;
            int totalHeight = GetCurrentTabHeight();
            int maxScroll = totalHeight > contentH ? totalHeight - contentH : 0;
            
            g.FillRectangle(sbX, sbY, sbW, sbH, 0xFF0F0F0F);
            if (maxScroll > 0 && totalHeight > 0) {
                int thumbH = sbH * sbH / totalHeight;
                if (thumbH < 24) thumbH = 24;
                if (thumbH > sbH) thumbH = sbH;
                int thumbY = sbH * _scrollY / totalHeight;
                if (thumbY + thumbH > sbH) thumbY = sbH - thumbH;
                g.FillRectangle(sbX + 1, sbY + thumbY, sbW - 2, thumbH, 0xFF3F3F3F);
            }
        }
        
        private void DrawButtons() {
            var g = Framebuffer.Graphics;
            int btnW = 120;
            int btnH = 38;
            int btnY = Y + Height - _padding - btnH - 10;
            int applyX = X + Width - _padding - btnW;
            int resetX = applyX - btnW - 16;
            
            g.FillRectangle(resetX, btnY, btnW, btnH, 0xFF2A2A2A);
            g.DrawRectangle(resetX, btnY, btnW, btnH, 0xFF3F3F3F, 1);
            WindowManager.font.DrawString(resetX + 30, btnY + (btnH - WindowManager.font.FontSize) / 2, "Reset");
            
            g.FillRectangle(applyX, btnY, btnW, btnH, 0xFF2E7F3F);
            g.DrawRectangle(applyX, btnY, btnW, btnH, 0xFF3F3F3F, 1);
            WindowManager.font.DrawString(applyX + 34, btnY + (btnH - WindowManager.font.FontSize) / 2, "Apply");
        }
        
        private int GetCurrentTabHeight() {
            switch (_currentTab) {
                case 0: return _lineHeight * 9; // Animations
                case 1: return _lineHeight * 23; // Visual Effects (increased for taskbar settings)
                case 2: return _lineHeight * 13; // Window Rendering
                case 3: return _lineHeight * 9; // Performance
                case 4: return _lineHeight * 11; // Widgets
                case 5: return _lineHeight * 14; // Special Effects
                default: return _lineHeight * 10;
            }
        }
        
        private void ApplySettings() {
            // Animations
            UISettings.EnableFadeAnimations = _enableFadeAnimations;
            UISettings.FadeInDurationMs = _fadeInDuration;
            UISettings.FadeOutDurationMs = _fadeOutDuration;
            UISettings.EnableWindowSlideAnimations = _enableWindowSlideAnimations;
            UISettings.WindowSlideDurationMs = _windowSlideDuration;
            UISettings.EnableFadeOverlay = _enableFadeOverlay;
            UISettings.EnableAnimationUpdates = _enableAnimationUpdates;
            
            // Visual Effects
            UISettings.EnableBlurredTitleBars = _enableBlurredTitleBars;
            UISettings.EnableTransparentWindows = _enableTransparentWindows;
            UISettings.EnableWindowGlow = _enableWindowGlow;
            UISettings.EnableRoundedCorners = _enableRoundedCorners;
            UISettings.EnableButtonHoverEffects = _enableButtonHoverEffects;
            UISettings.EnableResizeGrip = _enableResizeGrip;
            UISettings.EnableBlurCaching = _enableBlurCaching;
            UISettings.EnableBlurCacheInvalidation = _enableBlurCacheInvalidation;
            UISettings.EnableBlurCacheDisposal = _enableBlurCacheDisposal;
            
            // Background Rotation
            UISettings.EnableAutoBackgroundRotation = _enableAutoBackgroundRotation;
            UISettings.EnableRandomBackgroundOnStartup = _enableRandomBackgroundOnStartup;
            UISettings.BackgroundRotationIntervalMinutes = _backgroundRotationInterval;
            UISettings.EnableBackgroundFadeTransition = _enableBackgroundFadeTransition;
            UISettings.BackgroundFadeDurationMs = _backgroundFadeDuration;
            
            // Taskbar
            UISettings.EnableTaskbarAutoHide = _enableTaskbarAutoHide;
            UISettings.EnableTaskbarSlideDown = _enableTaskbarSlideDown;
            UISettings.EnableTaskbarSlideUp = _enableTaskbarSlideUp;
            UISettings.TaskbarAutoHideDelayMs = _taskbarAutoHideDelay;
            UISettings.TaskbarSlideDurationMs = _taskbarSlideDuration;
            
            // Window Rendering
            UISettings.EnableWindowTitles = _enableWindowTitles;
            UISettings.EnableWindowBorders = _enableWindowBorders;
            UISettings.EnableTitleBarButtons = _enableTitleBarButtons;
            UISettings.EnableTitleBarBackground = _enableTitleBarBackground;
            UISettings.EnableWindowContentBackground = _enableWindowContentBackground;
            UISettings.EnableButtonBackgrounds = _enableButtonBackgrounds;
            UISettings.EnableButtonBorders = _enableButtonBorders;
            UISettings.EnableButtonIcons = _enableButtonIcons;
            UISettings.EnableTaskbarIcons = _enableTaskbarIcons;
            UISettings.ButtonSizeOffset = _buttonSizeOffset;
            UISettings.ButtonSpacing = _buttonSpacing;
            
            // Performance
            UISettings.EnableDrawCallCaching = _enableDrawCallCaching;
            UISettings.EnableGeometryBatching = _enableGeometryBatching;
            UISettings.SkipOffscreenRendering = _skipOffscreenRendering;
            UISettings.SkipMinimizedRendering = _skipMinimizedRendering;
            UISettings.SkipInvisibleRendering = _skipInvisibleRendering;
            UISettings.CheckFramebufferBeforeRender = _checkFramebufferBeforeRender;
            UISettings.CheckFontBeforeRender = _checkFontBeforeRender;
            UISettings.EnableEarlyReturnOptimizations = _enableEarlyReturnOptimizations;
            
            // Widgets
            UISettings.EnableWidgetRendering = _enableWidgetRendering;
            UISettings.EnableWidgetTransparency = _enableWidgetTransparency;
            UISettings.EnableWidgetBorders = _enableWidgetBorders;
            UISettings.EnableWidgetShadows = _enableWidgetShadows;
            UISettings.EnableWidgetFadeAnimations = _enableWidgetFadeAnimations;
            UISettings.EnableWidgetHoverAnimations = _enableWidgetHoverAnimations;
            UISettings.WidgetFadeInDurationMs = _widgetFadeInDuration;
            UISettings.WidgetFadeOutDurationMs = _widgetFadeOutDuration;
            UISettings.WidgetDefaultRefreshRateMs = _widgetDefaultRefreshRate;
            
            // Special Effects
            UISettings.EnableSpecialWindowEffects = _enableSpecialWindowEffects;
            UISettings.WindowOpenEffect = _windowOpenEffect;
            UISettings.WindowCloseEffect = _windowCloseEffect;
            UISettings.SpecialEffectDurationMs = _specialEffectDuration;
            UISettings.DigitizeScanlineCount = _digitizeScanlineCount;
            UISettings.BurnParticleCount = _burnParticleCount;
            UISettings.SmokeParticleCount = _smokeParticleCount;
            UISettings.GlitchIntensity = _glitchIntensity;
            UISettings.RippleWaveCount = _rippleWaveCount;
            UISettings.ExplodeParticleCount = _explodeParticleCount;
            
            NotificationManager.Add(new Notify("Settings applied successfully", NotificationLevel.None));
            this.Visible = false;
        }
        
        private void ResetToDefaults() {
            // Reset to default values from UISettings
            _enableFadeAnimations = false;
            _fadeInDuration = 180;
            _fadeOutDuration = 180;
            _enableWindowSlideAnimations = false;
            _windowSlideDuration = 220;
            _enableFadeOverlay = false;
            _enableAnimationUpdates = true;
            
            _enableBlurredTitleBars = true;
            _enableTransparentWindows = true;
            _enableWindowGlow = true;
            _enableRoundedCorners = false;
            _enableButtonHoverEffects = true;
            _enableResizeGrip = true;
            _enableBlurCaching = true;
            _enableBlurCacheInvalidation = true;
            _enableBlurCacheDisposal = true;
            
            _enableAutoBackgroundRotation = false;
            _enableRandomBackgroundOnStartup = true;
            _backgroundRotationInterval = 5;
            _enableBackgroundFadeTransition = true;
            _backgroundFadeDuration = 1000;
            
            _enableTaskbarAutoHide = true;
            _enableTaskbarSlideDown = true;
            _enableTaskbarSlideUp = true;
            _taskbarAutoHideDelay = 1500;
            _taskbarSlideDuration = 200;
            
            _enableWindowTitles = true;
            _enableWindowBorders = true;
            _enableTitleBarButtons = true;
            _enableTitleBarBackground = true;
            _enableWindowContentBackground = true;
            _enableButtonBackgrounds = true;
            _enableButtonBorders = true;
            _enableButtonIcons = true;
            _enableTaskbarIcons = true;
            _buttonSizeOffset = 12;
            _buttonSpacing = 6;
            
            _enableDrawCallCaching = true;
            _enableGeometryBatching = true;
            _skipOffscreenRendering = true;
            _skipMinimizedRendering = true;
            _skipInvisibleRendering = true;
            _checkFramebufferBeforeRender = true;
            _checkFontBeforeRender = true;
            _enableEarlyReturnOptimizations = true;
            
            _enableWidgetRendering = true;
            _enableWidgetTransparency = true;
            _enableWidgetBorders = true;
            _enableWidgetShadows = true;
            _enableWidgetFadeAnimations = true;
            _enableWidgetHoverAnimations = true;
            _widgetFadeInDuration = 200;
            _widgetFadeOutDuration = 200;
            _widgetDefaultRefreshRate = 1000;
            
            _enableSpecialWindowEffects = true;
            _windowOpenEffect = WindowEffectType.Digitize;
            _windowCloseEffect = WindowEffectType.Derezz;
            _specialEffectDuration = 400;
            _digitizeScanlineCount = 25;
            _burnParticleCount = 150;
            _smokeParticleCount = 100;
            _glitchIntensity = 15;
            _rippleWaveCount = 8;
            _explodeParticleCount = 200;
            
            NotificationManager.Add(new Notify("Settings reset to defaults", NotificationLevel.None));
        }
        
        // Special Effects tab processing and drawing
        private void ProcessSpecialEffectsInput(int mx, int my, int cx, int currentY, int contentY, int contentH) {
            currentY += _lineHeight; // Skip title
            
            if (CheckCheckbox(mx, my, cx, currentY, contentY, contentH)) { _enableSpecialWindowEffects = !_enableSpecialWindowEffects; _btnClickLatch = true; return; }
            currentY += _lineHeight;
            
            // Open Effect dropdown
            if (CheckDropdown(mx, my, cx + _labelWidth, currentY, contentY, contentH)) {
                _openEffectDropdownOpen = !_openEffectDropdownOpen;
                _closeEffectDropdownOpen = false;
                _btnClickLatch = true;
                return;
            }
            if (_openEffectDropdownOpen) {
                WindowEffectType selected = ProcessDropdownOptions(mx, my, cx + _labelWidth, currentY + _lineHeight, contentY, contentH);
                if (selected != (WindowEffectType)(-1)) {
                    _windowOpenEffect = selected;
                    _openEffectDropdownOpen = false;
                    _btnClickLatch = true;
                    return;
                }
            }
            currentY += _lineHeight;
            
            // Close Effect dropdown
            if (CheckDropdown(mx, my, cx + _labelWidth, currentY, contentY, contentH)) {
                _closeEffectDropdownOpen = !_closeEffectDropdownOpen;
                _openEffectDropdownOpen = false;
                _btnClickLatch = true;
                return;
            }
            if (_closeEffectDropdownOpen) {
                WindowEffectType selected = ProcessDropdownOptions(mx, my, cx + _labelWidth, currentY + _lineHeight, contentY, contentH);
                if (selected != (WindowEffectType)(-1)) {
                    _windowCloseEffect = selected;
                    _closeEffectDropdownOpen = false;
                    _btnClickLatch = true;
                    return;
                }
            }
            currentY += _lineHeight;
            
            // Effect Duration slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 40; }
            currentY += _lineHeight;
            
            // Digitize Scanline Count slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 41; }
            currentY += _lineHeight;
            
            // Burn Particle Count slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 42; }
            currentY += _lineHeight;
            
            // Smoke Particle Count slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 43; }
            currentY += _lineHeight;
            
            // Glitch Intensity slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 44; }
            currentY += _lineHeight;
            
            // Ripple Wave Count slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 45; }
            currentY += _lineHeight;
            
            // Explode Particle Count slider
            if (CheckSlider(mx, my, cx + _labelWidth, currentY, contentY, contentH)) { _draggingSlider = 46; }
            
            // Handle slider dragging
            if (_draggingSlider >= 0) {
                float t = (float)(mx - (cx + _labelWidth)) / _sliderWidth;
                if (t < 0) t = 0;
                if (t > 1) t = 1;
                
                switch (_draggingSlider) {
                    case 40: _specialEffectDuration = (int)(100 + t * 900); break; // 100-1000ms
                    case 41: _digitizeScanlineCount = (int)(10 + t * 90); break; // 10-100
                    case 42: _burnParticleCount = (int)(50 + t * 450); break; // 50-500
                    case 43: _smokeParticleCount = (int)(30 + t * 270); break; // 30-300
                    case 44: _glitchIntensity = (int)(5 + t * 45); break; // 5-50
                    case 45: _rippleWaveCount = (int)(3 + t * 17); break; // 3-20
                    case 46: _explodeParticleCount = (int)(100 + t * 400); break; // 100-500
                }
            }
        }
        
        private void DrawSpecialEffectsTab(int cx, int contentY, int contentH) {
            int currentY = contentY - _scrollY;
            
            DrawTitle(cx, currentY, "Special Window Effects (Tron-style & More)");
            currentY += _lineHeight;
            
            DrawCheckboxWithLabel(cx, currentY, contentY, contentH, _enableSpecialWindowEffects, "Enable Special Window Effects");
            currentY += _lineHeight;
            
            DrawDropdownWithLabel(cx, currentY, contentY, contentH, "Window Open Effect:", _windowOpenEffect, _openEffectDropdownOpen);
            if (_openEffectDropdownOpen) {
                DrawDropdownOptions(cx + _labelWidth, currentY + _lineHeight, contentY, contentH);
            }
            currentY += _lineHeight;
            
            DrawDropdownWithLabel(cx, currentY, contentY, contentH, "Window Close Effect:", _windowCloseEffect, _closeEffectDropdownOpen);
            if (_closeEffectDropdownOpen) {
                DrawDropdownOptions(cx + _labelWidth, currentY + _lineHeight, contentY, contentH);
            }
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "Effect Duration:", _specialEffectDuration, 100, 1000, " ms");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "Digitize Scanlines:", _digitizeScanlineCount, 10, 100, "");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "Burn Particles:", _burnParticleCount, 50, 500, "");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "Smoke Particles:", _smokeParticleCount, 30, 300, "");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "Glitch Intensity:", _glitchIntensity, 5, 50, " px");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "Ripple Waves:", _rippleWaveCount, 3, 20, "");
            currentY += _lineHeight;
            
            DrawSliderWithLabel(cx, currentY, contentY, contentH, "Explode Particles:", _explodeParticleCount, 100, 500, "");
        }
        
        private bool CheckDropdown(int mx, int my, int dx, int dy, int contentY, int contentH) {
            if (dy < contentY - _lineHeight || dy > contentY + contentH) return false;
            return mx >= dx && mx <= dx + _sliderWidth && my >= dy + 8 && my <= dy + 30;
        }
        
        private void DrawDropdownWithLabel(int cx, int cy, int contentY, int contentH, string label, WindowEffectType value, bool isOpen) {
            if (cy < contentY - _lineHeight || cy > contentY + contentH) return;
            
            WindowManager.font.DrawString(cx + 32, cy, label);
            
            // Draw dropdown box
            int dx = cx + _labelWidth;
            int dy = cy + 8;
            int dw = _sliderWidth;
            int dh = 22;
            
            var g = Framebuffer.Graphics;
            g.FillRectangle(dx, dy, dw, dh, 0xFF2A2A2A);
            g.DrawRectangle(dx, dy, dw, dh, 0xFF4F4F4F, 1);
            
            // Draw current value
            string valueStr = GetEffectName(value);
            WindowManager.font.DrawString(dx + 6, dy + 3, valueStr);
            
            // Draw arrow
            int arrowX = dx + dw - 16;
            int arrowY = dy + 8;
            if (isOpen) {
                // Up arrow
                g.DrawLine(arrowX, arrowY + 4, arrowX + 4, arrowY, 0xFFCCCCCC);
                g.DrawLine(arrowX + 4, arrowY, arrowX + 8, arrowY + 4, 0xFFCCCCCC);
            } else {
                // Down arrow
                g.DrawLine(arrowX, arrowY, arrowX + 4, arrowY + 4, 0xFFCCCCCC);
                g.DrawLine(arrowX + 4, arrowY + 4, arrowX + 8, arrowY, 0xFFCCCCCC);
            }
        }
        
        private void DrawDropdownOptions(int dx, int dy, int contentY, int contentH) {
            var g = Framebuffer.Graphics;
            int dw = _sliderWidth;
            int optionHeight = 24;
            
            WindowEffectType[] effects = {
                WindowEffectType.None, WindowEffectType.Fade, WindowEffectType.Digitize,
                WindowEffectType.Derezz, WindowEffectType.BurnIn, WindowEffectType.BurnOut,
                WindowEffectType.SmokeIn, WindowEffectType.SmokeOut, WindowEffectType.Glitch,
                WindowEffectType.Ripple, WindowEffectType.Explode, WindowEffectType.Implode,
                WindowEffectType.Random
            };
            
            int totalHeight = effects.Length * optionHeight;
            g.FillRectangle(dx, dy, dw, totalHeight, 0xFF1A1A1A);
            g.DrawRectangle(dx, dy, dw, totalHeight, 0xFF4F4F4F, 1);
            
            for (int i = 0; i < effects.Length; i++) {
                int oy = dy + (i * optionHeight);
                if (oy >= contentY && oy <= contentY + contentH) {
                    string name = GetEffectName(effects[i]);
                    WindowManager.font.DrawString(dx + 6, oy + 4, name);
                }
            }
        }
        
        private WindowEffectType ProcessDropdownOptions(int mx, int my, int dx, int dy, int contentY, int contentH) {
            int dw = _sliderWidth;
            int optionHeight = 24;
            
            WindowEffectType[] effects = {
                WindowEffectType.None, WindowEffectType.Fade, WindowEffectType.Digitize,
                WindowEffectType.Derezz, WindowEffectType.BurnIn, WindowEffectType.BurnOut,
                WindowEffectType.SmokeIn, WindowEffectType.SmokeOut, WindowEffectType.Glitch,
                WindowEffectType.Ripple, WindowEffectType.Explode, WindowEffectType.Implode,
                WindowEffectType.Random
            };
            
            int totalHeight = effects.Length * optionHeight;
            
            if (mx >= dx && mx <= dx + dw && my >= dy && my <= dy + totalHeight) {
                int index = (my - dy) / optionHeight;
                if (index >= 0 && index < effects.Length) {
                    return effects[index];
                }
            }
            
            return (WindowEffectType)(-1);
        }
        
        private string GetEffectName(WindowEffectType effect) {
            switch (effect) {
                case WindowEffectType.None: return "None";
                case WindowEffectType.Fade: return "Fade";
                case WindowEffectType.Digitize: return "Digitize (Tron)";
                case WindowEffectType.Derezz: return "De-rezz (Tron)";
                case WindowEffectType.BurnIn: return "Burn In";
                case WindowEffectType.BurnOut: return "Burn Out";
                case WindowEffectType.SmokeIn: return "Smoke In";
                case WindowEffectType.SmokeOut: return "Smoke Out";
                case WindowEffectType.Glitch: return "Glitch";
                case WindowEffectType.Ripple: return "Ripple";
                case WindowEffectType.Explode: return "Explode";
                case WindowEffectType.Implode: return "Implode";
                case WindowEffectType.Random: return "Random";
                default: return "Unknown";
            }
        }
    }
}
