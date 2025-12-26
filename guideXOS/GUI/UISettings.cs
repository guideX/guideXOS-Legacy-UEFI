namespace guideXOS.GUI {
    // Global UI settings for animations
    internal static class UISettings {
        // ===== ANIMATION SETTINGS =====
        // Fading animations (open/close) 
        public static bool EnableFadeAnimations = false;
        public static int FadeInDurationMs = 180;
        public static int FadeOutDurationMs = 180;
        public static bool UpdateAnimations = true; // enable animation updates during draw calls
        
        // Special window effects (Tron-style and more)
        public static bool EnableSpecialWindowEffects = false; // master toggle for special effects
        public static WindowEffectType WindowOpenEffect = WindowEffectType.None; // effect when window opens
        public static WindowEffectType WindowCloseEffect = WindowEffectType.None; // effect when window closes
        public static int SpecialEffectDurationMs = 400; // duration for special effects
        
        // Effect-specific settings
        public static int DigitizeScanlineCount = 25; // number of scanlines for digitize effect
        public static int BurnParticleCount = 150; // number of particles for burn effect
        public static int SmokeParticleCount = 100; // number of particles for smoke effect
        public static int GlitchIntensity = 15; // intensity of glitch effect (pixel shift)
        public static int RippleWaveCount = 8; // number of ripple waves
        public static int ExplodeParticleCount = 200; // number of particles for explode effect

        // Window slide animations (minimize/restore)
        public static bool EnableWindowSlideAnimations = false;
        public static int WindowSlideDurationMs = 220;
        
        // Animation overlay effects
        public static bool EnableFadeOverlay = false; // enable fade overlay during animations
        public static bool EnableAnimationUpdates = true; // enable animation position/alpha updates
        
        // ===== VISUAL EFFECTS SETTINGS =====
        // Major visual effects
        public static bool EnableBlurredTitleBars = false; // enable blur effect on window title bars
        public static bool EnableTransparentWindows = false; // enable transparency effects on windows
        public static bool EnableWindowGlow = false; // enable glow/shadow behind windows
        public static bool EnableRoundedCorners = true; // enable rounded corners on windows (causes massive memory leak)
        public static bool EnableButtonHoverEffects = true; // enable hover glow on title bar buttons
        public static bool EnableResizeGrip = true; // enable resize grip visual in corner

        // Auto-hide widgets (visuals)
        public static bool EnableAutoHideWidgetsVisuals = true; // enable reveal/hide visual effects for auto-hidden widgets
        public static bool EnableAutoHideWidgetsSlideAnimation = true; // slide widgets in/out when auto-hide triggers
        public static int AutoHideWidgetsSlideDurationMs = 180; // animation duration for reveal/hide
        
        // Blur cache settings
        public static bool EnableBlurCaching = true; // enable blur cache system
        public static bool EnableBlurCacheInvalidation = true; // invalidate blur cache on move/resize
        public static bool EnableBlurCacheDisposal = true; // dispose blur cache when hiding window
        
        // ===== WINDOW RENDERING SETTINGS =====
        // Title bar and window chrome
        public static bool EnableWindowTitles = true; // enable window title text rendering
        public static bool EnableWindowBorders = true; // enable window border drawing
        public static bool EnableTitleBarButtons = true; // enable title bar button rendering
        public static bool EnableTitleBarBackground = true; // enable title bar background fill
        public static bool EnableWindowContentBackground = true; // enable window content background
        
        // Additional rendering features
        public static bool EnableButtonComputation = true; // enable button rectangle computation
        public static bool EnableButtonHitTesting = true; // enable button hit detection
        public static bool EnableButtonRendering = true; // enable actual button drawing (separate from icons)
        
        // ===== BUTTON AND ICON SETTINGS =====
        // Title bar button visuals
        public static bool EnableButtonBackgrounds = true; // enable button background fills
        public static bool EnableButtonBorders = true; // enable button border outlines
        public static bool EnableButtonIcons = true; // enable button icon glyphs
        public static bool EnableTaskbarIcons = true; // enable window icons in taskbar
        
        // Button sizing and layout
        public static int ButtonSizeOffset = 12; // BarHeight - ButtonSizeOffset = button size
        public static int MinimumButtonSize = 16; // minimum button size in pixels
        public static int ButtonSpacing = 6; // spacing between buttons
        
        // ===== TEXT RENDERING SETTINGS =====
        // Font and text controls
        public static bool EnableTextRendering = true; // master toggle for all text rendering
        public static bool EnableTextShadows = true; // enable drop shadows on text (if implemented)
        public static bool EnableTextAntialiasing = true; // enable antialiased text (if implemented)
        public static bool EnableTitleTextMeasurement = true; // enable measuring title text width
        public static bool EnableEmptyTitleHandling = true; // enable special handling for empty titles
        
        // ===== WINDOW INTERACTION SETTINGS =====
        // Input and interaction
        public static bool EnableWindowDragging = true; // enable dragging windows by title bar
        public static bool EnableWindowResizing = true; // enable window resize functionality
        public static bool EnableButtonClicking = true; // enable clicking title bar buttons
        public static bool EnableWindowFocusing = true; // enable window focus changes
        public static bool EnableMouseHitTesting = true; // enable IsUnderMouse checks
        
        // Mouse responsiveness settings
        public static bool EnableMouseAcceleration = false; // enable mouse acceleration (off for better raw input)
        public static bool EnableRawMouseInput = true; // use raw PS/2 mouse input without smoothing
        public static int MouseSensitivity = 100; // mouse sensitivity percentage (100 = default)
        
        // Touchpad-specific settings (for laptop touchpads on real hardware)
        public static bool EnableTouchpadFiltering = true; // enable touchpad noise filtering and debouncing
        public static float TouchpadSensitivity = 0.08f; // touchpad sensitivity multiplier (0.05-1.0, lower = slower, reduced for better control)
        public static int TouchpadNoiseThreshold = 4; // ignore movements smaller than this (reduces jitter, increased to filter more noise)
        public static int TouchpadMaxDelta = 20; // maximum movement per packet (prevents cursor jumping, reduced to prevent corner jumps)
        public static int TouchpadButtonDebounce = 3; // packets required before registering click (prevents phantom clicks, increased for stability)
        
        // Resize settings
        public static int ResizeGripSize = 16; // size of resize grip in pixels
        public static int MinimumWindowWidth = 160; // minimum window width
        public static int MinimumWindowHeight = 120; // minimum window height
        
        // ===== SCREEN CLAMPING SETTINGS =====
        public static bool EnableScreenClamping = true; // keep windows on screen
        public static bool ClampHorizontally = true; // clamp window X position
        public static bool ClampVertically = true; // clamp window Y position
        public static bool ClampByTitleBar = true; // ensure title bar is always visible
        
        // ===== WINDOW LIFECYCLE SETTINGS =====
        // Construction and destruction
        public static bool EnableAutoRegistration = true; // auto-add windows to WindowManager.Windows
        public static bool EnableAutoFadeIn = true; // auto-start fade-in animation on create
        public static bool EnableOwnerIdTracking = true; // track memory owner IDs
        public static bool EnableMemoryFreeing = true; // free memory on dispose
        
        // Visibility management
        public static bool EnableVisibilityCallbacks = true; // call OnSetVisible
        public static bool EnableBlurCacheCleanup = true; // cleanup blur cache on hide
        
        // ===== TOMBSTONE SETTINGS =====
        public static bool EnableTombstoneOverlay = true; // enable visual overlay when window is tombstoned
        public static bool EnableTombstoneText = true; // enable "Tombstoned" text display
        public static bool EnableTombstoneInputBlocking = true; // block input when tombstoned
        
        // ===== PERFORMANCE SETTINGS =====
        // Optimization flags
        public static bool EnableDrawCallCaching = true; // cache repeated draw operations
        public static bool EnableGeometryBatching = true; // batch similar geometry calls
        public static bool SkipOffscreenRendering = true; // don't render windows that are off-screen
        public static bool SkipMinimizedRendering = true; // don't render minimized windows
        public static bool SkipInvisibleRendering = true; // don't render invisible windows
        
        // Frame optimization
        public static bool CheckFramebufferBeforeRender = true; // verify Framebuffer.Graphics exists
        public static bool CheckFontBeforeRender = true; // verify WindowManager.font exists
        public static bool EnableEarlyReturnOptimizations = true; // use early returns to skip work
        
        // ===== Z-ORDER AND FOCUS SETTINGS =====
        public static bool EnableWindowOrdering = true; // enable MoveToEnd functionality
        public static bool EnableFocusOnClick = true; // focus window when clicked
        public static bool EnableFocusOnDrag = true; // focus window when drag starts
        
        // ===== WINDOW STATE SETTINGS =====
        // Minimize/Maximize/Restore
        public static bool EnableMinimize = true; // allow windows to minimize
        public static bool EnableMaximize = true; // allow windows to maximize
        public static bool EnableRestore = true; // allow windows to restore from min/max
        public static bool EnableStateMemory = true; // remember normal bounds for restore
        
        // ===== KEYBOARD SETTINGS =====
        public static bool EnableKeyboardInput = true; // enable OnGlobalKey handling
        public static bool EnableEscapeToClose = true; // allow ESC key to close windows
        
        // ===== BUTTON PRESS STATE SETTINGS =====
        public static bool EnableButtonPressStates = true; // track pressed button state
        public static bool EnableButtonHoverStates = true; // track hover button state
        public static bool EnableButtonCaptureMode = true; // enable button mouse capture
        
        // ===== LINE DRAWING SETTINGS (for button icons) =====
        public static bool EnableLineDrawing = true; // enable DrawLine calls for icons
        public static bool EnableRectangleDrawing = true; // enable DrawRectangle calls for icons
        public static bool EnableRectangleFilling = true; // enable FillRectangle calls for icons
        
        // ===== DISPOSE AND CLEANUP SETTINGS =====
        public static bool EnableWindowDisposal = true; // enable Dispose() functionality
        public static bool EnableResourceCleanup = true; // cleanup resources on dispose
        public static bool EnableAllocatorFreeMemory = true; // call allocator to free memory
        public static bool EnableUnsafeMemoryOps = true; // allow unsafe memory operations
        
        // ===== WIDGET UI SETTINGS =====
        // Widget rendering and visual effects
        public static bool EnableWidgetRendering = true; // master toggle for all widget rendering
        public static bool EnableWidgetTransparency = true; // enable transparency effects on widgets
        public static bool EnableWidgetBlurBackground = false; // enable blur effect on widget backgrounds
        public static bool EnableWidgetBorders = true; // enable widget border drawing
        public static bool EnableWidgetShadows = true; // enable drop shadows behind widgets
        public static bool EnableWidgetRoundedCorners = true; // enable rounded corners on widgets
        public static bool EnableWidgetGlow = true; // enable glow effect on widgets

        // Auto-hide widgets behavior
        public static bool EnableAutoHideWidgets = false; // when enabled, widget button and widgets are hidden until mouse reaches far right edge
        public static bool AutoHideWidgetsHideResources = true; // when hidden, widgets do not consume system resources (skip updates/renders)
        public static int AutoHideWidgetsRevealThresholdPx = 4; // distance from the far right screen edge to trigger reveal
        public static int AutoHideWidgetsHideDelayMs = 150; // delay before hiding again after mouse leaves the reveal area
        public static bool AutoHideWidgetsHideWidgetButton = true; // also hide the main widget toggle button when auto-hide is enabled
        
        // Widget animations
        public static bool EnableWidgetFadeAnimations = true; // enable fade in/out animations for widgets
        public static bool EnableWidgetSlideAnimations = false; // enable slide animations for widgets
        public static bool EnableWidgetHoverAnimations = true; // enable hover state animations
        public static bool EnableWidgetUpdateAnimations = true; // enable smooth value update animations
        public static int WidgetFadeInDurationMs = 200; // widget fade in duration
        public static int WidgetFadeOutDurationMs = 200; // widget fade out duration
        public static int WidgetSlideDurationMs = 150; // widget slide animation duration
        public static int WidgetHoverDurationMs = 100; // widget hover animation duration
        
        // Widget interaction
        public static bool EnableWidgetMouseInput = true; // enable mouse interaction with widgets
        public static bool EnableWidgetClickDetection = true; // enable click detection on widgets
        public static bool EnableWidgetHoverDetection = true; // enable hover detection on widgets
        public static bool EnableWidgetDragging = true; // enable dragging widgets (if supported)
        public static bool EnableWidgetTooltips = true; // enable tooltips on widgets
        public static bool EnableWidgetContextMenu = true; // enable right-click context menu
        public static int WidgetTooltipDelayMs = 500; // delay before showing tooltip
        
        // Widget layout and sizing
        public static bool EnableWidgetAutoLayout = true; // enable automatic widget layout
        public static bool EnableWidgetDynamicSizing = true; // allow widgets to resize dynamically
        public static bool EnableWidgetMinimumSize = true; // enforce minimum widget sizes
        public static bool EnableWidgetMaximumSize = true; // enforce maximum widget sizes
        public static int WidgetDefaultWidth = 200; // default widget width
        public static int WidgetDefaultHeight = 120; // default widget height
        public static int WidgetMinimumWidth = 100; // minimum widget width
        public static int WidgetMinimumHeight = 60; // minimum widget height
        public static int WidgetMaximumWidth = 400; // maximum widget width
        public static int WidgetMaximumHeight = 300; // maximum widget height
        public static int WidgetPadding = 8; // internal padding for widget content
        public static int WidgetSpacing = 12; // spacing between widgets
        public static int WidgetMargin = 6; // external margin around widgets
        
        // Widget text and icons
        public static bool EnableWidgetTitles = true; // enable title text on widgets
        public static bool EnableWidgetIcons = true; // enable icons on widgets
        public static bool EnableWidgetValueDisplay = true; // enable value/data display
        public static bool EnableWidgetLabels = true; // enable labels for widget data
        public static bool EnableWidgetTextShadows = false; // enable text shadows in widgets
        public static bool EnableWidgetTextAntialiasing = true; // enable antialiased text
        
        // Widget data and updates
        public static bool EnableWidgetAutoUpdate = true; // enable automatic data updates
        public static bool EnableWidgetRefreshRate = true; // respect widget refresh rates
        public static bool EnableWidgetDataCaching = true; // cache widget data between updates
        public static bool EnableWidgetLazyLoading = true; // lazy load widget data
        public static int WidgetDefaultRefreshRateMs = 1000; // default refresh rate (1 second)
        public static int WidgetFastRefreshRateMs = 100; // fast refresh rate (100ms)
        public static int WidgetSlowRefreshRateMs = 5000; // slow refresh rate (5 seconds)
        
        // Widget performance optimization
        public static bool EnableWidgetBatchRendering = true; // batch multiple widget renders
        public static bool EnableWidgetCulling = true; // cull widgets outside visible area
        public static bool SkipOffscreenWidgetRendering = true; // skip rendering off-screen widgets
        public static bool SkipInvisibleWidgetRendering = true; // skip rendering invisible widgets
        public static bool EnableWidgetDrawCaching = true; // cache widget draw operations
        public static bool EnableWidgetGeometryBatching = true; // batch widget geometry
        public static bool EnableWidgetEarlyReturn = true; // early return optimizations
        
        // Widget lifecycle
        public static bool EnableWidgetAutoRegistration = true; // auto-register widgets to manager
        public static bool EnableWidgetDisposal = true; // enable widget disposal
        public static bool EnableWidgetResourceCleanup = true; // cleanup widget resources
        public static bool EnableWidgetMemoryFreeing = true; // free widget memory on dispose
        public static bool EnableWidgetInitialization = true; // enable widget initialization
        
        // Widget visibility and state
        public static bool ShowWidgetsOnStartup = false; // show widget container on startup (disable to start with widgets hidden)
        public static bool EnableWidgetVisibilityToggle = true; // allow toggling widget visibility
        public static bool EnableWidgetCollapseExpand = true; // allow collapsing/expanding widgets
        public static bool EnableWidgetMinimize = true; // allow minimizing widgets
        public static bool EnableWidgetStateMemory = true; // remember widget states
        public static bool EnableWidgetPersistence = true; // persist widget settings
        
        // Widget docking and positioning
        public static bool EnableWidgetDocking = true; // enable widget docking to edges
        public static bool EnableWidgetSnapping = true; // enable snapping to grid/other widgets
        public static bool EnableWidgetScreenClamping = true; // keep widgets on screen
        public static bool EnableWidgetOverlapPrevention = false; // prevent widgets from overlapping
        public static int WidgetSnapDistance = 10; // distance for snapping to work
        public static int WidgetDockMargin = 4; // margin when docked to edge
        
        // Widget visual customization
        public static uint WidgetBackgroundColor = 0xDD1A1A1A; // widget background color
        public static uint WidgetBackgroundTransparentColor = 0x991A1A1A; // transparent background
        public static uint WidgetBorderColor = 0xFF3A3A3A; // widget border color
        public static uint WidgetHeaderColor = 0xFF252525; // widget header/title bar color
        public static uint WidgetShadowColor = 0x44000000; // widget shadow color
        public static uint WidgetGlowColor = 0x332E89FF; // widget glow color
        public static uint WidgetTextColor = 0xFFE0E0E0; // widget text color
        public static uint WidgetLabelColor = 0xFFB0B0B0; // widget label color
        public static uint WidgetValueColor = 0xFFFFFFFF; // widget value text color
        public static uint WidgetHoverColor = 0xFF2E2E2E; // widget hover overlay color
        public static uint WidgetActiveColor = 0xFF3E3E3E; // widget active state color
        public static int WidgetBorderThickness = 1; // widget border thickness
        public static int WidgetShadowOffset = 4; // shadow offset in pixels
        public static int WidgetCornerRadius = 6; // corner radius for rounded widgets
        
        // Widget specific features
        public static bool EnableGraphWidgetSmoothing = true; // enable graph smoothing/interpolation
        public static bool EnableGraphWidgetGridlines = true; // show gridlines on graph widgets
        public static bool EnableGraphWidgetLegends = true; // show legends on graph widgets
        public static bool EnableMeterWidgetGradients = true; // use gradients in meter widgets
        public static bool EnableMeterWidgetAnimations = true; // animate meter value changes
        public static bool EnableClockWidgetSeconds = true; // show seconds on clock widgets
        public static bool EnableClockWidgetAnalog = false; // use analog clock display
        
        // Widget debugging
        public static bool EnableWidgetDebugBounds = false; // show widget debug boundaries
        public static bool EnableWidgetDebugText = false; // show widget debug information
        public static bool EnableWidgetPerformanceMetrics = false; // show performance metrics
        
        // ===== DESKTOP ICON SETTINGS =====
        // Desktop icon cache settings
        public static bool EnableDesktopIconCacheRefresh = false; // periodically refresh desktop icon cache (disable if icons disappear after 5 minutes)
        public static int DesktopIconCacheRefreshIntervalMinutes = 5; // how often to refresh icon cache (default 5 minutes)
        
        // ===== BACKGROUND SETTINGS =====
        // Background rotation settings
        public static bool EnableAutoBackgroundRotation = false;
        public static bool EnableRandomBackgroundOnStartup = true; // randomly choose a single background on startup (ignored if EnableAutoBackgroundRotation is true)
        public static int BackgroundRotationIntervalMinutes = 5; // 5 minutes default
        public static bool EnableBackgroundFadeTransition = true;
        public static int BackgroundFadeDurationMs = 1000; // 1 second fade
        
        // ===== TASKBAR SETTINGS =====
        // Taskbar auto-hide and animations
        public static bool EnableTaskbarAutoHide = true; // automatically hide taskbar when not in use
        public static bool EnableTaskbarSlideDown = true; // animate taskbar sliding down when showing
        public static bool EnableTaskbarSlideUp = true; // animate taskbar sliding up when hiding
        public static int TaskbarAutoHideDelayMs = 1500; // delay before auto-hiding (1.5 seconds)
        public static int TaskbarSlideDurationMs = 200; // duration of slide animation
        public static int TaskbarRevealThreshold = 5; // pixels from bottom to reveal taskbar
        
        // ===== DEFAULT VALUES AND CONSTANTS =====
        // Window defaults
        public static int DefaultBarHeight = 40; // default title bar height
        public static int DefaultTaskbarHeight = 40; // default taskbar height
        public static string DefaultWindowTitle = "Window1"; // default title for new windows
        
        // ===== COLOR CUSTOMIZATION =====
        // Title bar colors
        public static uint TitleBarColor = 0xFF111111; // solid title bar color
        public static uint TitleBarTransparentColor = 0x66111111; // transparent overlay
        public static uint TitleBarOpaqueColor = 0xAA111111; // semi-opaque overlay
        
        // Window content colors
        public static uint WindowContentColor = 0xFF222222; // solid content background
        public static uint WindowContentTransparentColor = 0xCC222222; // transparent content
        
        // Button colors
        public static uint ButtonNormalColor = 0xFF2E2E2E;
        public static uint ButtonHoverColor = 0xFF343434;
        public static uint ButtonPressedColor = 0xFF2A2A2A;
        public static uint ButtonBorderColor = 0xFF505050;
        public static uint ButtonHoverGlowColor = 0x332E89FF;
        
        // Border and grip colors
        public static uint WindowBorderColor = 0xFF333333;
        public static uint ResizeGripColor = 0x332F2F2F;
        public static uint ResizeGripOpaqueColor = 0xFF2F2F2F;
        public static uint ResizeGripLineColor = 0xFF777777;
        public static uint ResizeGripBorderColor = 0xFF444444;
        
        // Glow colors
        public static uint WindowGlowColor = 0x221E90FF;
        
        // Tombstone colors
        public static uint TombstoneTransparentColor = 0x88111111;
        public static uint TombstoneOpaqueColor = 0xDD111111;
        
        // Special effect colors
        public static uint DigitizeColor = 0xFF00FFFF; // cyan for Tron-style digitize
        public static uint DerezzColor = 0xFFFF6600; // orange for de-rezz
        public static uint BurnColor = 0xFFFF4400; // orange-red for burn
        public static uint SmokeColor = 0x88888888; // gray for smoke
        public static uint GlitchColor = 0xFF00FF00; // green for glitch
        public static uint RippleColor = 0xFF0088FF; // blue for ripple
        public static uint ExplodeColor = 0xFFFFFF00; // yellow for explode
    }
    
    /// <summary>
    /// Window effect types for open/close animations
    /// </summary>
    public enum WindowEffectType {
        None,           // No effect (instant)
        Fade,           // Simple fade (default)
        Digitize,       // Tron-style scanline materialization
        Derezz,         // Tron-style de-resolution
        BurnIn,         // Fire particles coalescing
        BurnOut,        // Fire particles dispersing
        SmokeIn,        // Smoke particles forming
        SmokeOut,       // Smoke particles dissipating
        Glitch,         // Digital corruption
        Ripple,         // Ripple wave effect
        Explode,        // Particle explosion
        Implode,        // Particle implosion
        Random          // Random effect each time
    }
}
