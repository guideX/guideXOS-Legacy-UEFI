/*
   guideXOS Development Constraints:

   - Presume a C# Kernel, and C# OS
   - Presume a C++ bootloader
   - The code must be compatible with UEFI mode.
   - Please make use of BootConsole.WriteLine, BootConsole.Write, and other helper functions there, do not
     always use while ((Native.In8(0x3FD) & 0x20) == 0) { } Native.Out8(0x3F8, 'a'); for example when debugging
 */
using guideXOS;
using guideXOS.DefaultApps;
using guideXOS.DockableWidgets;
using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Drivers.Input;
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

        // ============================================================================
        // CAPABILITY-BASED MOUSE DETECTION
        // ============================================================================
        // 
        // Priority order:
        // 1. UEFI Simple Pointer (UEFI boot, before ExitBootServices)
        // 2. VMware Backdoor (VMware VMs - absolute positioning)
        // 3. USB HID Mouse (after USB init)
        // 4. Legacy PS/2 Mouse (Legacy boot only, explicitly enabled)
        // 5. Keyboard Emulation (fallback)
        //
        // NO unconditional PS/2 initialization - only if explicitly needed
        // ============================================================================
        
        BootConsole.WriteLine("[INPUT] Starting capability-based mouse detection");
        
        bool isLegacyBoot = (BootConsole.CurrentMode == guideXOS.BootMode.Legacy);
        bool enablePS2Fallback = isLegacyBoot; // Only enable PS/2 fallback in Legacy mode
        
        // Get boot info for UEFI mode (null for Legacy)
        // Note: bootInfo is passed through a different path, so we use null here
        // The MouseInputManager was already initialized in EntryPoint with bootInfo
        BootConsole.WriteLine("[INPUT] About to call DetectAndInitialize");
        MouseCapabilityDetector.DetectAndInitialize(null, isLegacyBoot, enablePS2Fallback);
        BootConsole.WriteLine("[INPUT] DetectAndInitialize returned");
        
        // Log which mouse path was chosen
        BootConsole.WriteLine("[INPUT] Primary mouse: " + 
            MouseCapabilityDetector.GetCapabilityName(MouseCapabilityDetector.PrimaryCapability));
        
        BootConsole.WriteLine("[INPUT] After primary mouse log");
        
        // Initialize PS/2 keyboard (Legacy mode only)
        // In UEFI mode, PS/2 keyboard uses IRQ1 which conflicts with APIC mode
        // Use keyboard-to-mouse emulation as fallback input in UEFI mode
        if (isLegacyBoot) {
            BootConsole.WriteLine("[INPUT] Initializing PS/2 keyboard");
            try {
                // Initialize keyboard event dispatcher first
                Keyboard.Initialize();
                
                // Initialize PS/2 keyboard hardware interface
                PS2Keyboard.Initialize();
                
                // Enable full keyboard processing (was disabled during early boot)
                PS2Keyboard.EnableFullProcessing();
                
                BootConsole.WriteLine("[INPUT] PS/2 keyboard initialized");
            } catch { 
                BootConsole.WriteLine("[INPUT] PS/2 keyboard initialization failed");
            }
        } else {
            BootConsole.WriteLine("[INPUT] PS/2 keyboard skipped (UEFI mode - use keyboard emulation)");
            BootConsole.WriteLine("[INPUT] Keyboard-to-mouse emulation active");
            BootConsole.WriteLine("[INPUT] Use Arrow keys to move cursor, F1=Left click, F2=Right click");
        }

        BootConsole.WriteLine("[USB] INIT");
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            try {
                Hub.Initialize();
                HID.Initialize();
                EHCI.Initialize();
                USB.StartPolling();
                
                // After USB init, check if USB HID mouse is available
                MouseCapabilityDetector.CheckUsbHidMouse();
            } catch { }
            BootConsole.WriteLine("[USB] Complete (Legacy)");
        } else {
            BootConsole.WriteLine("[USB] Skipped (UEFI mode)");
        }
        
        // Log final mouse detection result
        BootConsole.WriteLine("[INPUT] Mouse detection complete");
        BootConsole.WriteLine("[INPUT] Mouse enabled: " + MouseCapabilityDetector.MouseEnabled);

        //Sized width to 512
        BootConsole.WriteLine("[CURSOR] Creating cursor images");
        
        // UEFI mode: Skip PNG decoding (hangs) - use simple fallback cursors
        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
            BootConsole.WriteLine("[CURSOR] UEFI mode - using simple fallback cursors");
            
            // Create simple white arrow cursor
            Cursor = new Image(16, 16);
            if (Cursor != null && Cursor.RawData != null) {
                // Clear to transparent first
                for (int i = 0; i < 16 * 16; i++) {
                    Cursor.RawData[i] = 0; // Transparent
                }
                
                // Draw a larger, more visible white arrow
                for (int y = 0; y < 16; y++) {
                    for (int x = 0; x < 16; x++) {
                        // Larger arrow shape: classic pointer
                        if (y < 12 && x < 8 && x <= y && x < (12 - y)) {
                            Cursor.RawData[y * 16 + x] = unchecked((int)0xFFFFFFFF); // White
                        }
                        // Add black outline for visibility
                        else if (y < 13 && x < 9 && (x == y + 1 || x == (11 - y) || (y == 11 && x <= 7))) {
                            Cursor.RawData[y * 16 + x] = unchecked((int)0xFF000000); // Black outline
                        }
                    }
                }
                
                BootConsole.WriteLine("[CURSOR] Fallback cursor drawn successfully");
            } else {
                BootConsole.WriteLine("[CURSOR] ERROR: Cursor or RawData is null!");
            }
            
            CursorMoving = Cursor;
            CursorBusy = Cursor;
            
            BootConsole.WriteLine("[CURSOR] Fallback cursors created (UEFI)");
        }
        // Check if File.Instance is available (Legacy mode)
        else if (File.Instance == null) {
            BootConsole.WriteLine("[CURSOR] File.Instance is NULL - using fallback cursors");
            
            // Create simple fallback cursors
            Cursor = new Image(16, 16);
            for (int y = 0; y < 16; y++) {
                for (int x = 0; x < 16; x++) {
                    if (x + y < 16) {
                        Cursor.RawData[y * 16 + x] = unchecked((int)0xFFFFFFFF);
                    }
                }
            }
            
            CursorMoving = Cursor;
            CursorBusy = Cursor;
            
            BootConsole.WriteLine("[CURSOR] Fallback cursors created");
        } else {
            // Load PNG cursors from filesystem (works in both Legacy and UEFI with managed decoder)
            BootConsole.WriteLine("[CURSOR] Loading PNG cursors from filesystem");
            
            // Load cursor
            bool cursorLoaded = false;
            try { 
                byte[] cursorData = File.ReadAllBytes("Images/Cursor.png");
                if (cursorData != null) {
                    BootConsole.WriteLine("[CURSOR] Creating PNG object");
                    Cursor = new PNG(cursorData);
                    cursorData.Dispose();
                    BootConsole.WriteLine("[CURSOR] Loaded Cursor.png");
                    cursorLoaded = true;
                }
            } catch { 
                BootConsole.WriteLine("[CURSOR] Exception loading Cursor.png");
            }
            
            if (!cursorLoaded) {
                BootConsole.WriteLine("[CURSOR] Failed to load Cursor.png, using fallback");
                Cursor = new Image(16, 16);
                for (int y = 0; y < 16; y++) {
                    for (int x = 0; x < 16; x++) {
                        if (x + y < 16) {
                            Cursor.RawData[y * 16 + x] = unchecked((int)0xFFFFFFFF);
                        }
                    }
                }
            }
            
            try { 
                byte[] grabData = File.ReadAllBytes("Images/Grab.png");
                CursorMoving = new PNG(grabData);
                grabData.Dispose();
                BootConsole.WriteLine("[CURSOR] Loaded Grab.png");
            } catch { 
                CursorMoving = Cursor;
            }
            
            try { 
                byte[] busyData = File.ReadAllBytes("Images/Busy.png");
                CursorBusy = new PNG(busyData);
                busyData.Dispose();
                BootConsole.WriteLine("[CURSOR] Loaded Busy.png");
            } catch { 
                CursorBusy = Cursor;
            }
        }
        
        BootConsole.WriteLine("[CURSOR] All cursors created");
        
        // Initialize BitFont - skip in UEFI mode (file access issues)
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy && File.Instance != null) {
            try {
                BitFont.Initialize();
                string CustomCharset = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
                byte[] fontData = File.ReadAllBytes("Fonts/enludo.btf");
                if (fontData != null) {
                    BitFont.RegisterBitFont(new BitFontDescriptor("Enludo", CustomCharset, fontData, 16));
                    BootConsole.WriteLine("[FONT] BitFont initialized");
                }
            } catch {
                BootConsole.WriteLine("[FONT] BitFont initialization failed");
            }
        } else if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
            BootConsole.WriteLine("[FONT] Skipped (UEFI mode)");
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
        
        // Debug: about to call SMain
        BootConsole.WriteLine("[KMAIN] About to call SMain");
        while ((Native.In8(0x3FD) & 0x20) == 0) { }
        Native.Out8(0x3F8, (byte)'G');
        Native.Out8(0x3F8, (byte)'O');
        Native.Out8(0x3F8, (byte)'\n');
        
        // UEFI mode: Run a minimal but functional desktop render loop
        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
            BootConsole.WriteLine("[KMAIN] UEFI desktop render mode");
            
            uint* fb = Framebuffer.VideoMemory;
            int fbW = Framebuffer.Width;
            int fbH = Framebuffer.Height;
            
            BootConsole.WriteLine("[KMAIN] FB: " + fbW.ToString() + "x" + fbH.ToString());
            
            // Colors
            uint teal = 0x000D7D77;         // Desktop background
            uint darkTeal = 0x00095550;     // Taskbar background
            uint white = 0x00FFFFFF;        // Cursor and text
            uint lightGray = 0x00CCCCCC;    // Start button
            
            int taskbarHeight = 40;
            int mouseX = fbW / 2;
            int mouseY = fbH / 2;
            
            int frame = 0;
            for (;;) {
                frame++;
                
                // Poll mouse input (skip first few frames to avoid init issues)
                if (frame > 2) {
                    try {
                        MouseEventDispatcher.Update();
                        mouseX = Control.MousePosition.X;
                        mouseY = Control.MousePosition.Y;
                        
                        // Clamp to screen bounds
                        if (mouseX < 0) mouseX = 0;
                        if (mouseY < 0) mouseY = 0;
                        if (mouseX >= fbW) mouseX = fbW - 1;
                        if (mouseY >= fbH) mouseY = fbH - 1;
                    } catch { }
                }
                
                // FAST fill: Use Native.Stosd for the entire screen with teal
                Native.Stosd(fb, teal, (ulong)(fbW * fbH));
                
                // Draw taskbar (overwrite bottom portion with dark teal)
                uint* taskbarStart = fb + (fbW * (fbH - taskbarHeight));
                Native.Stosd(taskbarStart, darkTeal, (ulong)(fbW * taskbarHeight));
                
                // Draw "Start" button (light gray rectangle) - small area, use loop
                int startBtnX = 5;
                int startBtnY = fbH - taskbarHeight + 5;
                int startBtnW = 60;
                int startBtnH = 30;
                for (int y = startBtnY; y < startBtnY + startBtnH && y < fbH; y++) {
                    for (int x = startBtnX; x < startBtnX + startBtnW && x < fbW; x++) {
                        fb[y * fbW + x] = lightGray;
                    }
                }
                
                // Draw cursor (white triangle) - small area, use loop
                for (int dy = 0; dy < 16 && (mouseY + dy) < fbH; dy++) {
                    for (int dx = 0; dx < 16 && (mouseX + dx) < fbW; dx++) {
                        if (dx <= dy && dx < (12 - dy)) {
                            fb[(mouseY + dy) * fbW + (mouseX + dx)] = white;
                        }
                    }
                }
                
                // Log first frame
                if (frame == 1) {
                    BootConsole.WriteLine("[KMAIN] Desktop rendered!");
                }
                
                // ~60fps
                Thread.Sleep(16);
            }
        }
        
        // Legacy mode: use the full SMain
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
        // IMMEDIATE debug output - before ANYTHING else
        // Use raw serial to bypass any potential BootConsole issues
        while ((Native.In8(0x3FD) & 0x20) == 0) { }
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'M');
        Native.Out8(0x3F8, (byte)'1');
        Native.Out8(0x3F8, (byte)'\n');
        
        // UEFI and Legacy both use full desktop rendering now
        BootConsole.WriteLine("[SMAIN] Starting desktop rendering");

        // CRITICAL: Test framebuffer IMMEDIATELY before any complex operations
        BootConsole.WriteLine("[SMAIN] Testing framebuffer access");
        
        // Draw directly to framebuffer to prove it works
        while ((Native.In8(0x3FD) & 0x20) == 0) { }
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'M');
        Native.Out8(0x3F8, (byte)'2');
        Native.Out8(0x3F8, (byte)'\n');
        
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
            BootConsole.WriteLine("[SMAIN] Checking existing wallpaper");
            if (wall != null) {
                BootConsole.WriteLine("[SMAIN] Resizing existing wallpaper");
                Wallpaper = wall.ResizeImage(Framebuffer.Width, Framebuffer.Height);
                wall.Dispose(); // FIXED: Dispose original wallpaper
            } else {
                BootConsole.WriteLine("[SMAIN] Creating default gradient wallpaper");
                BootConsole.WriteLine("[SMAIN] FB size: " + Framebuffer.Width.ToString() + "x" + Framebuffer.Height.ToString());
                
                // Create default wallpaper with teal gradient (top to bottom)
                Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height);
                
                if (Wallpaper == null) {
                    BootConsole.WriteLine("[SMAIN] ERROR: Wallpaper allocation returned null!");
                } else if (Wallpaper.RawData == null) {
                    BootConsole.WriteLine("[SMAIN] ERROR: Wallpaper.RawData is null!");
                } else {
                    BootConsole.WriteLine("[SMAIN] Image allocated, drawing gradient...");
                    
                    // Simplified gradient - just fill with solid teal color for now
                    int totalPixels = Framebuffer.Width * Framebuffer.Height;
                    int tealColor = unchecked((int)0xFF0D7D77);
                    
                    for (int i = 0; i < totalPixels; i++) {
                        Wallpaper.RawData[i] = tealColor;
                    }
                    
                    BootConsole.WriteLine("[SMAIN] Solid color fill complete");
                }
            }
        } catch {
            BootConsole.WriteLine("[SMAIN] Wallpaper creation exception - using solid color");
            // Fallback: create wallpaper with solid teal color
            try {
                Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height);
                if (Wallpaper != null && Wallpaper.RawData != null) {
                    int tealColor = unchecked((int)0xFF0D7D77);
                    for (int i = 0; i < Framebuffer.Width * Framebuffer.Height; i++) {
                        Wallpaper.RawData[i] = tealColor;
                    }
                }
            } catch {
                BootConsole.WriteLine("[SMAIN] Fallback wallpaper also failed!");
            }
        }

        BootConsole.WriteLine("[SMAIN] Wallpaper created");
        //Lockscreen.Run();
        FConsole = null; // Don't create console here - let it be created on-demand

        // Initialize background rotation manager and icons
        BootConsole.WriteLine("[SMAIN] Setting up icons");
        if (File.Instance != null) {
            BootConsole.WriteLine("[SMAIN] Initializing background manager and icons");
            
            if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                BackgroundRotationManager.Initialize();
                // Initialize module system (built-in modules)
                guideXOS.Modules.ModuleManager.InitializeBuiltins();
                
                // Initialize cached desktop icons (Legacy only - PNG decoding works)
                try {
                    RefreshCachedIcons();
                    _lastIconCacheRefresh = Timer.Ticks;
                    BootConsole.WriteLine("[SMAIN] Icons initialized");
                } catch {
                    BootConsole.WriteLine("[SMAIN] Icon initialization failed - using fallback");
                    _cachedDocumentIcon = new Image(48, 48);
                    _cachedFolderIcon = new Image(48, 48);
                    _cachedImageIcon = new Image(48, 48);
                    _cachedAudioIcon = new Image(48, 48);
                }
            } else {
                // UEFI mode: Skip PNG icons - use simple fallback
                BootConsole.WriteLine("[SMAIN] UEFI mode - using fallback icons (no PNG)");
                _cachedDocumentIcon = new Image(48, 48);
                _cachedFolderIcon = new Image(48, 48);
                _cachedImageIcon = new Image(48, 48);
                _cachedAudioIcon = new Image(48, 48);
            }
        } else {
            BootConsole.WriteLine("[SMAIN] No filesystem - using fallback icons");
            // Set up dummy icons
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
        BootConsole.WriteLine("[SMAIN] Drawing BLUE test marker before loop");
        try {
            uint* testFb = Framebuffer.VideoMemory;
            int testW = Framebuffer.Width;
            uint blue = 0x000000FF;
            for (int y = 0; y < 50; y++) {
                for (int x = 50; x < 100; x++) {
                    testFb[y * testW + x] = blue;
                }
            }
            BootConsole.WriteLine("[SMAIN] BLUE marker drawn - about to enter loop");
        } catch {
            BootConsole.WriteLine("[SMAIN] Failed to draw blue marker!");
        }

        for (; ; ) {
            try {
                frameCounter++;

                // In UEFI mode, draw directly to framebuffer every frame to ensure screen updates
                if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                    uint* fb = Framebuffer.VideoMemory;
                    int fbW = Framebuffer.Width;
                    int fbH = Framebuffer.Height;
                    
                    // Draw teal background directly to framebuffer
                    uint teal = 0x000D7D77;
                    for (int i = 0; i < fbW * fbH; i++) {
                        fb[i] = teal;
                    }
                    
                    // Draw cursor directly  
                    int cx = Control.MousePosition.X;
                    int cy = Control.MousePosition.Y;
                    uint white = 0x00FFFFFF;
                    for (int dy = 0; dy < 16 && (cy + dy) < fbH; dy++) {
                        for (int dx = 0; dx < 16 && (cx + dx) < fbW; dx++) {
                            if (dx <= dy && dx < (12 - dy)) {
                                fb[(cy + dy) * fbW + (cx + dx)] = white;
                            }
                        }
                    }
                    
                    // Log first frame only
                    if (frameCounter == 1) {
                        BootConsole.WriteLine("[SMAIN] First UEFI frame rendered");
                    }
                    
                    // Skip the rest of the complex rendering for now
                    Thread.Sleep(16); // ~60fps
                    continue;
                }

                // Reduce serial output in UEFI mode to improve performance
                // Only log first frame and every 600 frames (~10 seconds)
                bool shouldLog = (frameCounter == 1) || (frameCounter % 600 == 0);

                if (shouldLog) {
                    // RAW serial output for debugging
                    BootConsole.WriteLine("FRM");
                }

                // FIXED: Periodically refresh cached icons to prevent memory buildup (optional)
                if (UISettings.EnableDesktopIconCacheRefresh && File.Instance != null) {
                    ulong intervalMs = (ulong)UISettings.DesktopIconCacheRefreshIntervalMinutes * 60000UL;
                    if (Timer.Ticks - _lastIconCacheRefresh >= intervalMs) {
                        RefreshCachedIcons();
                        _lastIconCacheRefresh = Timer.Ticks;
                    }
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

                // Poll mouse input from all registered providers (UEFI, USB HID, etc.)
                // Uses unified MouseEventDispatcher for source-agnostic event handling
                // This updates Control.MousePosition and Control.MouseButtons
                try {
                    MouseEventDispatcher.Update();
                } catch {
                    // Ignore mouse polling errors
                }

                // Per-frame input pass for all windows
                WindowManager.MouseHandled = false;
                try {
                    WindowManager.InputAll();
                } catch {
                    // Ignore input errors
                }

                // Skip FlushPendingCreates in UEFI mode - might hang
                if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                    try {
                        WindowManager.FlushPendingCreates();
                    } catch {
                        // Ignore window creation errors
                    }
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

                //clear screen
                try {
                    // In UEFI mode, skip clear - we'll draw full screen anyway
                    if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                        // Use Stosd for fast clear - much faster than per-pixel loop
                        Native.Stosd(Framebuffer.VideoMemory, 0x00000000, (ulong)(Framebuffer.Width * Framebuffer.Height));
                    }
                } catch {
                    // Critical: if we can't clear screen, skip frame
                    Thread.Sleep(1);
                    continue;
                }

                //draw carpet or wallpaper - use BackgroundRotationManager for fade effects
                try {
                    if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                        BackgroundRotationManager.DrawBackground();
                    } else {
                        // UEFI mode: Draw directly to framebuffer for testing
                        // First, draw a solid color to prove we can write to framebuffer
                        uint* fb = Framebuffer.VideoMemory;
                        int fbW = Framebuffer.Width;
                        int fbH = Framebuffer.Height;
                        
                        // Draw teal background directly to framebuffer
                        for (int y = 0; y < fbH; y++) {
                            // Calculate gradient color (teal gradient)
                            int t = (y * 128) / fbH; // 0-128 range
                            uint color = (uint)(0x000D7D77 + (t << 16) + (t << 8) + t); // Lighter at bottom
                            for (int x = 0; x < fbW; x++) {
                                fb[y * fbW + x] = color;
                            }
                        }
                    }
                } catch {
                    // Draw solid color fallback
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

                // Draw desktop icons and taskbar
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

                // Draw windows in layers to control z-order:
                // 1. Regular windows (except Task Manager)
                try {
                    WindowManager.DrawAllExceptTaskManager();
                } catch {
                    // Ignore window drawing errors
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
                    if (img != null && img.RawData != null) {
                        Framebuffer.Graphics.DrawImage(Control.MousePosition.X, Control.MousePosition.Y, img);
                    } else {
                        // Debug: log if cursor is null (only once per 600 frames)
                        if (shouldLog) {
                            BootConsole.WriteLine("CNULL");
                        }
                    }
                } catch {
                    // Ignore cursor drawing errors
                }

                //refresh screen
                try {
                    Framebuffer.Update();
                } catch {
                    // Critical: if we can't update screen, skip frame
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
        // Works in both Legacy and UEFI modes now (with managed PNG decoder)
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
    /// <summary>
    /// Widgets Container
    /// </summary>
    public static WidgetContainer WidgetsContainer;
}