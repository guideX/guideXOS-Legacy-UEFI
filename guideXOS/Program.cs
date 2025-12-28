using guideXOS;
using guideXOS.DefaultApps;
using guideXOS.DockableWidgets;
using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using guideXOS.OS;
using guideXOS.Modules;
using System.Drawing;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System;
/// <summary>
/// Program
/// </summary>
unsafe class Program {
    /// <summary>
    /// Main
    /// </summary>
    static void Main() {
    }
    /// <summary>
    /// DLL Import
    /// </summary>
    [DllImport("*")]
    public static extern void test();
    /// <summary>
    /// Cusor
    /// </summary>
    private static Image Cursor;
    private static Image CursorMoving;
    private static Image CursorBusy;
    /// <summary>
    /// Wallpaper
    /// </summary>
    public static Image Wallpaper;
    /// <summary>
    /// USB Mouse Test
    /// </summary>
    /// <returns></returns>
    private static bool USBMouseTest() {
        HID.GetMouse(HID.Mouse, out _, out _, out var Buttons);
        return Buttons != MouseButtons.None;
    }
    /// <summary>
    /// USB Keyboard Test
    /// </summary>
    /// <returns></returns>
    private static bool USBKeyboardTest() {
        HID.GetKeyboard(HID.Keyboard, out var ScanCode, out _);
        return ScanCode != 0;
    }
    /// <summary>
    /// KMain
    /// </summary>
    public static void KMain() {
        BootConsole.WriteLine("[ANIMATOR] INITIALIZE");

        // UEFI mode: Skip animator for now (requires complex initialization)
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            Animator.Initialize();
            BootConsole.WriteLine("[ANIMATOR] Complete (Legacy)");
        } else {
            BootConsole.WriteLine("[ANIMATOR] Skipped (UEFI mode)");
        }

        BootConsole.WriteLine("[INPUT] PS/2 INIT");
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            // Initialize legacy PS/2 input first so VirtualBox (default PS/2 devices) works out-of-the-box.
            // This provides keyboard IRQ1 (0x21) and mouse IRQ12 (0x2C) handling even without USB HID.
            try { PS2Keyboard.Initialize(); } catch { }
            try { PS2Mouse.Initialize(); } catch { }
            // Initialize VMware absolute pointer backdoor if present (no-op on other hypervisors)
            try { VMwareTools.Initialize(); } catch { }
            BootConsole.WriteLine("[INPUT] PS/2 complete (Legacy)");
        } else {
            BootConsole.WriteLine("[INPUT] PS/2 skipped (UEFI mode)");
        }

        BootConsole.WriteLine("[USB] INIT");
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            try {
                Hub.Initialize();
                HID.Initialize();
                EHCI.Initialize();
                USB.StartPolling();
            } catch { /* USB stack is optional; continue boot */ }
            BootConsole.WriteLine("[USB] Complete (Legacy)");
        } else {
            BootConsole.WriteLine("[USB] Skipped (UEFI mode)");
        }

        //Sized width to 512
        BootConsole.WriteLine("[CURSOR] Creating cursor images");
        
        // Try to load PNG cursors from filesystem (works in both Legacy and UEFI with cached TarFS)
        BootConsole.WriteLine("[CURSOR] Loading PNG cursors from filesystem");
        
        try { 
            BootConsole.WriteLine("[CURSOR] Loading Images/Cursor.png");
            byte[] cursorData = File.ReadAllBytes("Images/Cursor.png");
            Cursor = new PNG(cursorData);
            cursorData.Dispose();
            BootConsole.WriteLine("[CURSOR] Loaded Cursor.png successfully");
        } catch { 
            BootConsole.WriteLine("[CURSOR] Failed to load Cursor.png, using fallback");
            Cursor = new Image(16, 16);
            // Fill with white arrow
            for (int y = 0; y < 16; y++) {
                for (int x = 0; x < 16; x++) {
                    if (x + y < 16) {
                        Cursor.RawData[y * 16 + x] = unchecked((int)0xFFFFFFFF);
                    }
                }
            }
        }
        
        try { 
            BootConsole.WriteLine("[CURSOR] Loading Images/Grab.png");
            byte[] grabData = File.ReadAllBytes("Images/Grab.png");
            CursorMoving = new PNG(grabData);
            grabData.Dispose();
            BootConsole.WriteLine("[CURSOR] Loaded Grab.png successfully");
        } catch { 
            CursorMoving = Cursor;
            BootConsole.WriteLine("[CURSOR] Failed to load Grab.png, using fallback");
        }
        
        try { 
            BootConsole.WriteLine("[CURSOR] Loading Images/Busy.png");
            byte[] busyData = File.ReadAllBytes("Images/Busy.png");
            CursorBusy = new PNG(busyData);
            busyData.Dispose();
            BootConsole.WriteLine("[CURSOR] Loaded Busy.png successfully");
        } catch { 
            CursorBusy = Cursor;
            BootConsole.WriteLine("[CURSOR] Failed to load Busy.png, using fallback");
        }
        
        BootConsole.WriteLine("[CURSOR] All cursors created");
        
        // Only initialize BitFont in Legacy mode (fonts require more complex initialization)
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            BitFont.Initialize();
            string CustomCharset = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            BitFont.RegisterBitFont(new BitFontDescriptor("Enludo", CustomCharset, File.ReadAllBytes("Fonts/enludo.btf"), 16));
        }

        //Terminal = null;
        BootConsole.WriteLine("[WM] INIT");
        WindowManager.Initialize();
        BootConsole.WriteLine("[WM] INIT complete");

        BootConsole.WriteLine("[DESKTOP] INIT");
        Desktop.Initialize();
        BootConsole.WriteLine("[DESKTOP] INIT complete");

        // Skip these subsystems in UEFI mode for now
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            Firewall.Initialize();
            Audio.Initialize();
            AC97.Initialize();
            if (AC97.DeviceLocated) BootConsole.WriteLine("Device Located: " + AC97.DeviceName);
            ES1371.Initialize();
        } else {
            BootConsole.WriteLine("[UEFI] Skipping audio/firewall (legacy only)");
        }
#if NETWORK
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            BootConsole.WriteLine("[NET] Initializing network subsystem...");
            try {
                NETv4.Initialize();
                Intel825xx.Initialize();
                RTL8111.Initialize();
            } catch {
                BootConsole.WriteLine("[NET] Network driver initialization error");
            }

            // Only try DHCP if a network driver was found
            if (NETv4.Sender != null) {
                BootConsole.WriteLine("[NET] Network driver found");
                BootConsole.WriteLine("[NET] Skipping automatic DHCP (use 'netinit' command in console)");
                // Skip DHCP during boot to prevent hanging
                // User can run 'netinit' command in FConsole to configure network manually
            } else {
                BootConsole.WriteLine("[NET] No network hardware detected");
            }
        }
#endif
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            // Apply saved display mode before wallpaper resize
            DisplayManager.ApplySavedResolution();

            // Load saved configuration (UI settings, window positions, recent files, etc.)
            guideXOS.OS.Configuration.LoadConfiguration();
        }
        SMain();
    }

#if NETWORK
    private static void Client_OnData(byte[] data) {
        for (int i = 0; i < data.Length; i++) {
            Console.Write((char)data[i]);
        }
        BootConsole.WriteLine(" ");
    }

    public static byte[] ToASCII(string s) {
        byte[] buffer = new byte[s.Length];
        for (int i = 0; i < buffer.Length; i++) buffer[i] = (byte)s[i];
        return buffer;
    }
#endif
    public static bool rightClicked;
    public static FConsole FConsole;
    public static RightMenu rightmenu;
    public static PerformanceWidget perfWidget;
    public static WidgetContextMenu widgetContextMenu;

    // Cached desktop icons to prevent per-frame allocations
    // FIXED: Added periodic refresh to prevent memory buildup
    private static Image _cachedDocumentIcon;
    private static Image _cachedFolderIcon;
    private static Image _cachedImageIcon;
    private static Image _cachedAudioIcon;
    private static int _cachedIconSize = 48;
    private static ulong _lastIconCacheRefresh = 0;

    public static void SMain() {
        // UEFI and Legacy both use full desktop rendering now
        BootConsole.WriteLine("[SMAIN] Starting desktop rendering");

        // CRITICAL: Test framebuffer IMMEDIATELY before any complex operations
        BootConsole.WriteLine("[SMAIN] Testing framebuffer access");
        try {
            uint* testFb = Framebuffer.VideoMemory;
            int testW = Framebuffer.Width;
            int testH = Framebuffer.Height;

            // Draw a RED test pattern to prove framebuffer is accessible
            uint red = 0x00FF0000;
            for (int y = 0; y < 100 && y < testH; y++) {
                for (int x = 0; x < 100 && x < testW; x++) {
                    testFb[y * testW + x] = red;
                }
            }
            BootConsole.WriteLine("[SMAIN] Framebuffer test PASSED - red square drawn");
        } catch {
            BootConsole.WriteLine("[SMAIN] Framebuffer test FAILED!");
            for (; ; ) { Native.Hlt(); }
        }

        // CRITICAL: Disable triple buffering for UEFI - might cause black screen
        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
            BootConsole.WriteLine("[SMAIN] UEFI mode - disabling triple buffering");
            Framebuffer.TripleBuffered = false;
        } else {
            Framebuffer.TripleBuffered = true;
        }

        BootConsole.WriteLine("[SMAIN] Creating wallpaper");

        Image wall = Wallpaper;
        try {
            if (wall != null) {
                Wallpaper = wall.ResizeImage(Framebuffer.Width, Framebuffer.Height);
                wall.Dispose(); // FIXED: Dispose original wallpaper
            } else {
                // Create default wallpaper with teal gradient (top to bottom)
                Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height);

                // Teal gradient colors - lighter at top, darker at bottom
                uint topColor = 0xFF5FD4C4;      // Light teal/cyan
                uint bottomColor = 0xFF0D7D77;   // Darker teal

                int topR = (int)((topColor >> 16) & 0xFF);
                int topG = (int)((topColor >> 8) & 0xFF);
                int topB = (int)(topColor & 0xFF);

                int bottomR = (int)((bottomColor >> 16) & 0xFF);
                int bottomG = (int)((bottomColor >> 8) & 0xFF);
                int bottomB = (int)(bottomColor & 0xFF);

                // Create vertical gradient
                for (int y = 0; y < Wallpaper.Height; y++) {
                    float t = (float)y / Wallpaper.Height;
                    int r = (int)(topR + (bottomR - topR) * t);
                    int g = (int)(topG + (bottomG - topG) * t);
                    int b = (int)(topB + (bottomB - topB) * t);
                    uint color = (uint)(0xFF000000 | (r << 16) | (g << 8) | b);

                    for (int x = 0; x < Wallpaper.Width; x++) {
                        Wallpaper.RawData[y * Wallpaper.Width + x] = (int)color;
                    }
                }
            }
        } catch {
            // Fallback: create wallpaper with teal gradient
            Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height);

            uint topColor = 0xFF5FD4C4;      // Light teal/cyan
            uint bottomColor = 0xFF0D7D77;   // Darker teal

            int topR = (int)((topColor >> 16) & 0xFF);
            int topG = (int)((topColor >> 8) & 0xFF);
            int topB = (int)(topColor & 0xFF);

            int bottomR = (int)((bottomColor >> 16) & 0xFF);
            int bottomG = (int)((bottomColor >> 8) & 0xFF);
            int bottomB = (int)(bottomColor & 0xFF);

            for (int y = 0; y < Wallpaper.Height; y++) {
                float t = (float)y / Wallpaper.Height;
                int r = (int)(topR + (bottomR - topR) * t);
                int g = (int)(topG + (bottomG - topG) * t);
                int b = (int)(topB + (bottomB - topB) * t);
                uint color = (uint)(0xFF000000 | (r << 16) | (g << 8) | b);

                for (int x = 0; x < Wallpaper.Width; x++) {
                    Wallpaper.RawData[y * Wallpaper.Width + x] = (int)color;
                }
            }
        }

        BootConsole.WriteLine("[SMAIN] Wallpaper created");
        //Lockscreen.Run();
        FConsole = null; // Don't create console here - let it be created on-demand

        // Initialize background rotation manager (Legacy only - requires file system)
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            BootConsole.WriteLine("[SMAIN] Initializing background manager");
            BackgroundRotationManager.Initialize();
            // Initialize module system (built-in modules)
            guideXOS.Modules.ModuleManager.InitializeBuiltins();
            // Initialize cached desktop icons once to prevent per-frame allocations
            RefreshCachedIcons();
            _lastIconCacheRefresh = Timer.Ticks;
        } else {
            BootConsole.WriteLine("[UEFI] Skipping file-based features");
            // Set up dummy icons for UEFI mode
            _cachedDocumentIcon = new Image(48, 48);
            _cachedFolderIcon = new Image(48, 48);
            _cachedImageIcon = new Image(48, 48);
            _cachedAudioIcon = new Image(48, 48);
        }

        // Ensure context menu exists
        BootConsole.WriteLine("[SMAIN] Creating context menus");
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            if (rightmenu == null) {
                rightmenu = new RightMenu();
                rightmenu.Visible = false;
            }

            // Create widget context menu
            widgetContextMenu = new WidgetContextMenu();
            widgetContextMenu.Visible = false;
            WindowManager.MoveToEnd(widgetContextMenu);
            BootConsole.WriteLine("[SMAIN] Context menus created (Legacy)");
        } else {
            // UEFI mode: Skip context menus for now (require file access)
            BootConsole.WriteLine("[SMAIN] Context menus skipped (UEFI mode)");
            rightmenu = null;
            widgetContextMenu = null;
        }

        BootConsole.WriteLine("[SMAIN] Creating widgets");
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            // Create performance widget (initially visible)
            perfWidget = new PerformanceWidget();
            perfWidget.Visible = false; // Don't show standalone - will be in container
            WindowManager.MoveToEnd(perfWidget);

            // Create clock widget positioned below performance widget
            var clockWidget = new guideXOS.DockableWidgets.Clock(
                perfWidget.X,  // Same X position as performance widget
                perfWidget.Y + perfWidget.Height + 10  // Below performance widget with 10px gap
            );
            clockWidget.Visible = false; // Don't show standalone - will be in container
            WindowManager.MoveToEnd(clockWidget);

            // Create monitor widget for system charts
            var monitorWidget = new guideXOS.DockableWidgets.Monitor();
            monitorWidget.Visible = false; // Don't show standalone - will be in container
            WindowManager.MoveToEnd(monitorWidget);

            // Create uptime widget to show system uptime
            var uptimeWidget = new guideXOS.DockableWidgets.Uptime(
                perfWidget.X,  // Same X position as other widgets
                perfWidget.Y + perfWidget.Height + clockWidget.PreferredHeight + 20  // Below clock widget
            );
            uptimeWidget.Visible = false; // Don't show standalone - will be in container
            WindowManager.MoveToEnd(uptimeWidget);

            // Create a container and dock all widgets together
            var widgetContainer = new WidgetContainer(
                Framebuffer.Width - 220,  // Position more to the left (was -160)
                80  // Y position from top
            );
            widgetContainer.AddWidget(perfWidget);
            widgetContainer.AddWidget(clockWidget);
            widgetContainer.AddWidget(monitorWidget);
            widgetContainer.AddWidget(uptimeWidget);
            widgetContainer.Visible = UISettings.ShowWidgetsOnStartup; // Respect ShowWidgetsOnStartup setting
            WindowManager.MoveToEnd(widgetContainer);

            // Store reference for toggle button
            Program.WidgetsContainer = widgetContainer;

            // Show small toggle button on the far right if widgets are hidden
            if (!UISettings.ShowWidgetsOnStartup) {
                var toggle = new WidgetToggleButton(Framebuffer.Width - 26, 6);
                WindowManager.MoveToEnd(toggle);
                toggle.Visible = true;
            }
            BootConsole.WriteLine("[SMAIN] Widgets created (Legacy)");
        } else {
            // UEFI mode: Skip widgets (require complex initialization)
            BootConsole.WriteLine("[SMAIN] Widgets skipped (UEFI mode)");
            perfWidget = null;
            widgetContextMenu = null;
            Program.WidgetsContainer = null;
        }

        // Console will be created on-demand when user opens it from Start Menu
        // No longer auto-created at startup

        BootConsole.WriteLine("[SMAIN] Setup complete - entering main loop");
        // Show login screen immediately after unlocking
        // var login = new guideXOS.GUI.LoginDialog();
        // WindowManager.MoveToEnd(login);
        // login.Visible = true;

        //var welcome = new Welcome(500, 250);

        //It freezes here too
        //WindowManager.EnablePerfTracking();

        // FIXED: Removed debug line "Console.WriteLine("Draw Start");" that was polluting FConsole output

        // Add global Escape key handler to close active (topmost visible) window
        // Skip in UEFI mode (Keyboard not initialized)
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            Keyboard.OnKeyChanged += (sender, key) => {
                try {
                    // Only handle Escape key press events
                    if (key.Key == System.ConsoleKey.Escape && key.KeyState == System.ConsoleKeyState.Pressed) {
                        // Block Escape key when workspace switcher is visible
                        if (Desktop.Taskbar != null && Desktop.Taskbar.IsWorkspaceSwitcherVisible) {
                            Desktop.Taskbar.CloseWorkspaceSwitcher();
                            return;
                        }

                        // Find the topmost visible window and call OnGlobalKey on it
                        for (int i = WindowManager.Windows.Count - 1; i >= 0; i--) {
                            var window = WindowManager.Windows[i];
                            if (window.Visible && !window.IsTombstoned) {
                                // Call OnGlobalKey which will close the window if allowed
                                window.OnGlobalKey(key);
                                break; // Only affect the topmost visible window
                            }
                        }
                    }
                } catch {
                    // Ignore errors in global key handler to prevent crashes
                }
            };
        }

        int lastMouseX = Control.MousePosition.X;
        int lastMouseY = Control.MousePosition.Y;
        ulong lastMoveTick = Timer.Ticks;
        const ulong ActiveMoveMs = 100; // stay responsive for 100ms after a move

        // Add frame counter to verify loop is running
        int frameCounter = 0;
        BootConsole.WriteLine("[SMAIN] Entering main render loop");

        // IMMEDIATE test: Draw directly to framebuffer to prove loop is executing
        BootConsole.WriteLine("[SMAIN] Drawing GREEN test marker");
        try {
            uint* testFb = Framebuffer.VideoMemory;
            int testW = Framebuffer.Width;
            uint green = 0x0000FF00;
            for (int y = 0; y < 50; y++) {
                for (int x = 0; x < 50; x++) {
                    testFb[y * testW + x] = green;
                }
            }
            BootConsole.WriteLine("[SMAIN] GREEN marker drawn - loop started");
        } catch {
            BootConsole.WriteLine("[SMAIN] Failed to draw green marker!");
        }

        for (; ; ) {
            try {
                frameCounter++;

                // Log EVERY frame for first 10 frames to see if loop is working
                if (frameCounter <= 10) {
                    BootConsole.WriteLine($"[RENDER] Frame {frameCounter}");
                }

                // Every 60 frames (~1 second), output a marker
                if (frameCounter % 60 == 0) {
                    BootConsole.WriteLine($"[RENDER] Frame {frameCounter}");
                }

                // Log each step for first frame
                if (frameCounter == 1) {
                    BootConsole.WriteLine("[RENDER] Step 1: Icon cache check");
                }

                // FIXED: Periodically refresh cached icons to prevent memory buildup (optional)
                // Skip in UEFI mode
                if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy && UISettings.EnableDesktopIconCacheRefresh) {
                    ulong intervalMs = (ulong)UISettings.DesktopIconCacheRefreshIntervalMinutes * 60000UL;
                    if (Timer.Ticks - _lastIconCacheRefresh >= intervalMs) {
                        RefreshCachedIcons();
                        _lastIconCacheRefresh = Timer.Ticks;
                    }
                }

                if (frameCounter == 1) {
                    BootConsole.WriteLine("[RENDER] Step 2: Background update (skip in UEFI)");
                }

                // Update background rotation manager (handles automatic rotation and fade transitions)
                // Skip in UEFI mode
                if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                    try {
                        BackgroundRotationManager.Update();
                    } catch {
                        // Ignore background rotation errors
                    }
                }

                if (frameCounter == 1) {
                    BootConsole.WriteLine("[RENDER] Step 3: Window input");
                }

                // Per-frame input pass for all windows
                WindowManager.MouseHandled = false;
                try {
                    WindowManager.InputAll();
                } catch {
                    // Log input error but continue
                    BootConsole.WriteLine("Input handling error");
                }

                if (frameCounter == 1) {
                    BootConsole.WriteLine("[RENDER] Step 4: Flush pending");
                }

                // Skip FlushPendingCreates in UEFI mode - might hang
                if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                    try {
                        WindowManager.FlushPendingCreates();
                    } catch {
                        // Ignore window creation errors
                    }
                }

                if (frameCounter == 1) {
                    BootConsole.WriteLine("[RENDER] Step 5: Audio (skip in UEFI)");
                }

                // Service audio playback from main loop
                // Skip in UEFI mode - WAVPlayer might hang
                if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                    try {
                        WAVPlayer.DoPlay();
                    } catch {
                        // Ignore audio errors
                    }
                }

                if (frameCounter == 1) {
                    BootConsole.WriteLine("[RENDER] Step 6: Clear screen");
                }

                //clear screen
                try {
                    // UEFI mode: Use fast direct framebuffer clear instead of Graphics.Clear()
                    if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                        // Direct memset-style clear - MUCH faster than Graphics.Clear()
                        uint* fb = Framebuffer.VideoMemory;
                        int totalPixels = Framebuffer.Width * Framebuffer.Height;
                        for (int i = 0; i < totalPixels; i++) {
                            fb[i] = 0x00000000; // Black
                        }
                    } else {
                        // Legacy mode: Use Graphics.Clear() which might have optimizations
                        Framebuffer.Graphics.Clear(0x0);
                    }
                    
                    if (frameCounter == 1) {
                        BootConsole.WriteLine("[RENDER] Step 7: Screen cleared");
                    }
                } catch {
                    // Critical: if we can't clear screen, skip frame
                    Thread.Sleep(1);
                    continue;
                }

                if (frameCounter == 1) {
                    BootConsole.WriteLine("[RENDER] Step 8: Draw wallpaper");
                }

                //draw carpet or wallpaper - use BackgroundRotationManager for fade effects
                try {
                    if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                        BackgroundRotationManager.DrawBackground();
                    } else {
                        // UEFI mode: Just draw the wallpaper directly
                        if (frameCounter == 1) {
                            BootConsole.WriteLine("[RENDER] Step 8a: Check wallpaper");
                        }
                        
                        if (Wallpaper != null) {
                            if (frameCounter == 1) {
                                BootConsole.WriteLine("[RENDER] Step 8b: About to draw wallpaper");
                            }
                            Framebuffer.Graphics.DrawImage(0, 0, Wallpaper);
                            if (frameCounter == 1) {
                                BootConsole.WriteLine("[RENDER] Step 8c: Wallpaper drawn");
                            }
                        } else {
                            if (frameCounter == 1) {
                                BootConsole.WriteLine("[RENDER] Step 8d: Using solid color");
                            }
                            Framebuffer.Graphics.Clear(0xFF0D7D77); // Teal background
                        }
                    }
                } catch {
                    // Draw solid color fallback
                    if (frameCounter == 1) {
                        BootConsole.WriteLine("[RENDER] Step 8e: Wallpaper error, using fallback");
                    }
                    try {
                        Framebuffer.Graphics.Clear(0xFF0D7D77);
                    } catch { }
                }

                //Inspects the system to see if the user has right clicked there is a small difference between these two functions
                // Show desktop context menu only when right-click happened and no other window consumed the mouse.
                // FIXED: Use bitwise AND instead of HasFlag() to avoid enum boxing (saves ~200 KB/minute)
                try {
                    if ((Control.MouseButtons & MouseButtons.Right) == MouseButtons.Right && !rightClicked && !WindowManager.MouseHandled) {
                        rightClicked = true;
                        if (rightmenu != null) {
                            rightmenu.X = Control.MousePosition.X;
                            rightmenu.Y = Control.MousePosition.Y;
                            WindowManager.MoveToEnd(rightmenu);
                            rightmenu.Visible = true;
                        }
                    } else if ((Control.MouseButtons & MouseButtons.Right) != MouseButtons.Right) {
                        rightClicked = false;
                    }
                } catch {
                    // Ignore context menu errors
                    rightClicked = false;
                }

                try {
                    int iconSize = 48;
                    Desktop.Update(
                        _cachedDocumentIcon,
                        _cachedFolderIcon,
                        _cachedImageIcon,
                        _cachedAudioIcon,
                        iconSize
                    );
                } catch {
                    // Ignore desktop update errors
                }

                //Desktop.Draw();

                // Draw windows in layers to control z-order:
                // 1. Regular windows (except Task Manager)
                try {
                    WindowManager.DrawAllExceptTaskManager();
                } catch {
                    // Log but continue if window drawing fails
                    BootConsole.WriteLine("Window drawing error");
                }

                // 2. Workspace switcher (if visible) - appears on top of regular windows
                try {
                    if (Desktop.Taskbar != null) {
                        Desktop.Taskbar.DrawWorkspaceSwitcher();
                    }
                } catch {
                    // Ignore workspace switcher errors
                }

                // 3. Task Manager (always on top)
                try {
                    WindowManager.DrawTaskManager();
                } catch {
                    // Ignore task manager drawing errors
                }

                // 4. Clean up closed windows to prevent memory leaks
                try {
                    WindowManager.CleanupClosedWindows();
                } catch {
                    // Ignore cleanup errors
                }

                //draw cursor
                // FIXED: Use bitwise AND instead of HasFlag() to avoid enum boxing
                try {
                    var img = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left ? CursorMoving : Cursor;
                    if (img != null) Framebuffer.Graphics.DrawImage(Control.MousePosition.X, Control.MousePosition.Y, img);
                } catch {
                    // Ignore cursor drawing errors
                }

                //refresh screen
                try {
                    // Log every 60th update to verify we're calling it
                    if (frameCounter % 60 == 0) {
                        BootConsole.WriteLine("[RENDER] Calling Framebuffer.Update()");
                    }
                    Framebuffer.Update();

                    // Success marker
                    if (frameCounter % 60 == 0) {
                        BootConsole.WriteLine("[RENDER] Update() succeeded");
                    }
                } catch {
                    // Critical: if we can't update screen, skip frame
                    BootConsole.WriteLine("[RENDER] Update() FAILED!");
                    Thread.Sleep(1);
                    continue;
                }

                // Mouse responsiveness throttling: if mouse moved recently, keep minimal sleep (0) for max responsiveness.
                // When idle, yield a bit to lower CPU usage.
                int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
                if (mx != lastMouseX || my != lastMouseY) {
                    lastMouseX = mx; lastMouseY = my; lastMoveTick = Timer.Ticks;
                    Thread.Sleep(0); // No sleep when mouse moving for maximum responsiveness
                } else {
                    ulong age = (Timer.Ticks >= lastMoveTick) ? (Timer.Ticks - lastMoveTick) : 0UL;
                    if (age < ActiveMoveMs) Thread.Sleep(0); else Thread.Sleep(1); // Reduced from 2ms to 1ms for better responsiveness
                }
            } catch {
                // Catch any unhandled exception in main loop
                BootConsole.WriteLine("Critical error in main loop");
                Thread.Sleep(10); // Prevent tight error loop
            }
        }
        // End of SMain - both Legacy and UEFI use the same rendering loop above
    }

    /// <summary>
    /// FIXED: Helper method to refresh cached icons and dispose old ones
    /// CRITICAL: Create new icons BEFORE disposing old ones to prevent Desktop.Update from receiving null/disposed icons
    /// </summary>
    private static void RefreshCachedIcons() {
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            try {
                // STEP 1: Create new icons first
                Image newDocumentIcon = Icons.DocumentIcon(_cachedIconSize);
                Image newFolderIcon = Icons.FolderIcon(_cachedIconSize);
                Image newImageIcon = Icons.ImageIcon(_cachedIconSize);
                Image newAudioIcon = Icons.AudioIcon(_cachedIconSize);

                // STEP 2: Save old icons for disposal
                Image oldDocumentIcon = _cachedDocumentIcon;
                Image oldFolderIcon = _cachedFolderIcon;
                Image oldImageIcon = _cachedImageIcon;
                Image oldAudioIcon = _cachedAudioIcon;

                // STEP 3: Atomically swap to new icons (prevents Desktop.Update from seeing null)
                _cachedDocumentIcon = newDocumentIcon;
                _cachedFolderIcon = newFolderIcon;
                _cachedImageIcon = newImageIcon;
                _cachedAudioIcon = newAudioIcon;

                // STEP 4: Now safely dispose old icons (after swap is complete)
                if (oldDocumentIcon != null) oldDocumentIcon.Dispose();
                if (oldFolderIcon != null) oldFolderIcon.Dispose();
                if (oldImageIcon != null) oldImageIcon.Dispose();
                if (oldAudioIcon != null) oldAudioIcon.Dispose();
            } catch {
                // If icon creation fails, keep using old icons rather than having null icons
                BootConsole.WriteLine("Icon cache refresh failed - keeping old icons");
            }
        }
    }
    /// <summary>
    /// Widgets Container
    /// </summary>
    public static WidgetContainer WidgetsContainer;
}