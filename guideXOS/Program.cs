using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using guideXOS;
using guideXOS.DefaultApps;
using guideXOS.DockableWidgets;
using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Drivers.Input;
using guideXOS.Misc;
using guideXOS.OS;
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
    #region "public variables"
    /// <summary>
    /// Wallpaper
    /// </summary>
    public static Image Wallpaper;
    /// <summary>
    /// Widgets Container
    /// </summary>
    public static WidgetContainer WidgetsContainer;
    /// <summary>
    /// Right Clicked
    /// </summary>
    public static bool RightClicked;
    /// <summary>
    /// FConsole
    /// </summary>
    public static FConsole FConsole;
    /// <summary>
    /// Right Menu
    /// </summary>
    public static RightMenu RightMenu;
    /// <summary>
    /// Perf Widget
    /// </summary>
    public static PerformanceWidget PerfWidget;
    /// <summary>
    /// Widget Context Menu
    /// </summary>
    public static WidgetContextMenu widgetContextMenu;
    #endregion
    #region "private variables"
    /// <summary>
    /// Cusor
    /// </summary>
    private static Image Cursor;
    /// <summary>
    /// Cursor Moving
    /// </summary>
    private static Image CursorMoving;
    /// <summary>
    /// Cursor Busy
    /// </summary>
    private static Image CursorBusy;
    /// <summary>
    /// Cached Document Icon
    /// </summary>
    private static Image _cachedDocumentIcon;
    /// <summary>
    /// Cached Folder Icon
    /// </summary>
    private static Image _cachedFolderIcon;
    /// <summary>
    /// Cached Image Icon
    /// </summary>
    private static Image _cachedImageIcon;
    /// <summary>
    /// Cached Audio Icon
    /// </summary>
    private static Image _cachedAudioIcon;
    /// <summary>
    /// Cached Icon Size
    /// </summary>
    private static int _cachedIconSize = 48;
    /// <summary>
    /// Last Icon Cache Refresh
    /// </summary>
    private static ulong _lastIconCacheRefresh = 0;
    #endregion
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
    /// Test function to verify function calls work
    /// </summary>
    private static void TestFunction() {
        Native.Out8(0x3F8, (byte)'T');
        Native.Out8(0x3F8, (byte)'E');
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'T');
        Native.Out8(0x3F8, (byte)'\n');
    }

    /// <summary>
    /// KMain
    /// </summary>
    public static void KMain() {
        string customCharset = null;
        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
            // In UEFI mode, disable debug lines to prevent graphical corruption
            BootConsole.DrawDebugLines = false;
            BootConsole.WriteLine("[BOOT_MODE] UEFI");
            BootConsole.WriteLine("[MOUSE_CAPABILITIES] INITIALIZE");
            // UEFI mode: mark uefi=true and disable PS/2 fallback.
            MouseCapabilityDetector.DetectAndInitialize(null, true, false);
            BootConsole.WriteLine("[MOUSE_CAPABILITIES] DEVICE CHOSEN: " + MouseCapabilityDetector.GetCapabilityName(MouseCapabilityDetector.PrimaryCapability));
            //try {
            BootConsole.WriteLine("[INPUT] INITIALIZE");
            Keyboard.Initialize();
            //BootConsole.WriteLine("[INPUT] Subscribing Kbd2Mouse");
            //Keyboard.OnKeyChanged += (sender, key) => Kbd2Mouse.OnKeyChanged(key);
            //} catch {
                //BootConsole.WriteLine("[INPUT] Keyboard Dispatcher Init Failed");
            //}

            // UEFI: do not attempt to initialize PS/2 keyboard hardware.
            // PS/2 controller/IRQ code is legacy-oriented and can stall in UEFI-only environments.
            BootConsole.WriteLine("[INPUT] Skipping PS/2 keyboard init (UEFI)");

            BootConsole.WriteLine("[USB] INIT");
            // Always try to initialize USB, even in UEFI mode

            //try {
            Hub.Initialize();
            HID.Initialize();
            EHCI.Initialize();
            USB.StartPolling();

            // After USB init, check if USB HID mouse is available
            MouseCapabilityDetector.CheckUsbHidMouse();
            BootConsole.WriteLine("[USB] Initialization sequence complete");
            //} catch {
                //BootConsole.WriteLine("[USB] Init failed");
            //}
            // Log final mouse detection result
            BootConsole.WriteLine("[INPUT] Mouse detection complete");
            BootConsole.WriteLine("[INPUT] Mouse enabled: " + MouseCapabilityDetector.MouseEnabled);
            BootConsole.WriteLine("[CURSOR] Creating cursor images");
            // Create simple white arrow cursor
            try {
                Cursor = new Image(16, 16);
                BootConsole.WriteLine("[CURSOR] Image allocated");
                
                if (Cursor != null && Cursor.RawData != null) {
                    BootConsole.WriteLine("[CURSOR] Drawing cursor pattern");
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
            } catch {
                BootConsole.WriteLine("[CURSOR] EXCEPTION during cursor creation!");
                // Create a minimal fallback cursor
                Cursor = null;
                CursorMoving = null;
                CursorBusy = null;
            }
            BootConsole.WriteLine("[WM] INIT");
            try {
                WindowManager.Initialize();
                BootConsole.WriteLine("[WM] INIT complete");
            } catch {
                BootConsole.WriteLine("[WM] INIT FAILED!");
            }
            
            BootConsole.WriteLine("[DESKTOP] INIT");
            try {
                Desktop.Initialize();
                BootConsole.WriteLine("[DESKTOP] INIT complete");
            } catch {
                BootConsole.WriteLine("[DESKTOP] INIT FAILED!");
            }
            
            BootConsole.WriteLine("[KMAIN] Calling SMain");
            
            // CRITICAL: Test if we can even call a function
            Native.Out8(0x3F8, (byte)'P');
            Native.Out8(0x3F8, (byte)'R');
            Native.Out8(0x3F8, (byte)'E');
            Native.Out8(0x3F8, (byte)'\n');
            
            // Test function call
            TestFunction();
            
            Native.Out8(0x3F8, (byte)'C');
            Native.Out8(0x3F8, (byte)'A');
            Native.Out8(0x3F8, (byte)'L');
            Native.Out8(0x3F8, (byte)'L');
            Native.Out8(0x3F8, (byte)'\n');
            
            SMain();
            
            // If we get here, SMain returned (shouldn't happen in infinite loop)
            Native.Out8(0x3F8, (byte)'R');
            Native.Out8(0x3F8, (byte)'E');
            Native.Out8(0x3F8, (byte)'T');
            Native.Out8(0x3F8, (byte)'\n');
        } else if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            BootConsole.DrawDebugLines = true;

            BootConsole.WriteLine("[BOOT_MODE] LEGACY");
            Animator.Initialize();
            // Initialize legacy PS/2 input first so VirtualBox (default PS/2 devices) works out-of-the-box.
            // This provides keyboard IRQ1 (0x21) and mouse IRQ12 (0x2C) handling even without USB HID.
            try { PS2Keyboard.Initialize(); } catch { }
            try { PS2Mouse.Initialize(); } catch { }
            // Initialize VMware absolute pointer backdoor if present (no-op on other hypervisors)
            try { VMwareTools.Initialize(); } catch { }
#if USBDebug
        Hub.Initialize();
        HID.Initialize();
        EHCI.Initialize();
        //USB.StartPolling();

        //Use qemu for USB debug
        //VMware won't connect virtual USB HIDs
        /*
        if (HID.Mouse == null)
        {
            Console.WriteLine("USB Mouse not present");
        }
        if (HID.Keyboard == null)
        {
            Console.WriteLine("USB Keyboard not present");
        }

        for(; ; )
        {
            if (HID.Mouse != null)
            {
                HID.GetMouseThings(HID.Mouse, out sbyte AxisX, out sbyte AxisY, out var Buttons);
                if (AxisX != 0 && AxisY != 0)
                {
                    Console.WriteLine($"X:{AxisX} Y:{AxisY}");
                }
            }
            if(HID.Keyboard != null) 
            {
                HID.GetKeyboard(HID.Keyboard, out var ScanCode, out var Key);
                if(ScanCode != 0)
                {
                    Console.WriteLine($"ScanCode:{ScanCode}");
                }
            }
        }
#else
            try {
                Hub.Initialize();
                HID.Initialize();
                EHCI.Initialize();
                USB.StartPolling();
            } catch { /* USB stack is optional; continue boot */ }

            try {
                /*
                if (HID.Mouse == null) {
                    Console.WriteLine("USB Mouse not present");
                }
                if (HID.Keyboard == null) {
                    Console.WriteLine("USB Keyboard not present");
                }
                */
            } catch { }
#endif
            //Sized width to 512
            try { Cursor = new PNG(File.ReadAllBytes("Images/Cursor.png")); } catch { Cursor = new Image(16, 16); }
            try { CursorMoving = new PNG(File.ReadAllBytes("Images/Grab.png")); } catch { CursorMoving = Cursor; }
            try { CursorBusy = new PNG(File.ReadAllBytes("Images/Busy.png")); } catch { CursorBusy = Cursor; }
            //try { Wallpaper = new PNG(File.ReadAllBytes("Images/tronporche.png")); } catch { Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height); }
            BitFont.Initialize();
            // FIXED: Added leading space to charset to match font image layout
            customCharset = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            BitFont.RegisterBitFont(new BitFontDescriptor("Enludo", customCharset, File.ReadAllBytes("Fonts/enludo.btf"), 16));
            //Terminal = null;
            WindowManager.Initialize();
            Desktop.Initialize();
            Firewall.Initialize();
            Audio.Initialize();
            AC97.Initialize();
            if (AC97.DeviceLocated) Console.WriteLine("Device Located: " + AC97.DeviceName);
            ES1371.Initialize();
#if NETWORK
            Console.WriteLine("[NET] Initializing network subsystem...");
            try {
                NETv4.Initialize();
                Intel825xx.Initialize();
                RTL8111.Initialize();
            } catch {
                Console.WriteLine("[NET] Network driver initialization error");
            }

            // Only try DHCP if a network driver was found
            if (NETv4.Sender != null) {
                Console.WriteLine("[NET] Network driver found");
                Console.WriteLine("[NET] Skipping automatic DHCP (use 'netinit' command in console)");
                // Skip DHCP during boot to prevent hanging
                // User can run 'netinit' command in FConsole to configure network manually
            } else {
                Console.WriteLine("[NET] No network hardware detected");
            }
#endif
            // Apply saved display mode before wallpaper resize
            DisplayManager.ApplySavedResolution();
            // Load saved configuration (UI settings, window positions, recent files, etc.)
            guideXOS.OS.Configuration.LoadConfiguration();
            SMain();
        }
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



    public static void SMain() {
        // CRITICAL: RAW serial output FIRST to prove we entered SMain
        // Use direct port I/O to bypass any potential issues with BootConsole
        Native.Out8(0x3F8, (byte)'[');
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'M');
        Native.Out8(0x3F8, (byte)'A');
        Native.Out8(0x3F8, (byte)'I');
        Native.Out8(0x3F8, (byte)'N');
        Native.Out8(0x3F8, (byte)']');
        Native.Out8(0x3F8, (byte)'\n');
        
        // CRITICAL: Test framebuffer IMMEDIATELY at function entry
        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
            BootConsole.WriteLine("[SMAIN] UEFI MODE - Testing framebuffer access");
            uint* testFb = Framebuffer.VideoMemory;
            if (testFb == null) {
                BootConsole.WriteLine("[SMAIN] ERROR: Framebuffer.VideoMemory is NULL!");
                for (;;) Native.Hlt();
            }
            
            BootConsole.WriteLine("[SMAIN] Framebuffer pointer valid - writing test pattern");
            // Write a simple test pattern: 4 colored squares at the top-left corner
            // Each square is 50x50 pixels
            int fbW = Framebuffer.Width;
            
            // Square 1: RED (top-left)
            for (int y = 0; y < 50; y++) {
                for (int x = 0; x < 50; x++) {
                    testFb[y * fbW + x] = 0xFFFF0000;
                }
            }
            
            // Square 2: GREEN (next to red)
            for (int y = 0; y < 50; y++) {
                for (int x = 50; x < 100; x++) {
                    testFb[y * fbW + x] = 0xFF00FF00;
                }
            }
            
            // Square 3: BLUE (below red)
            for (int y = 50; y < 100; y++) {
                for (int x = 0; x < 50; x++) {
                    testFb[y * fbW + x] = 0xFF0000FF;
                }
            }
            
            // Square 4: WHITE (bottom-right of the 4 squares)
            for (int y = 50; y < 100; y++) {
                for (int x = 50; x < 100; x++) {
                    testFb[y * fbW + x] = 0xFFFFFFFF;
                }
            }
            
            BootConsole.WriteLine("[SMAIN] Test pattern written - you should see 4 colored squares");
            BootConsole.WriteLine("[SMAIN] If you don't see them, the framebuffer is not displaying");
        }
        
        BootConsole.WriteLine("[SMAIN] Starting desktop rendering");
        
        // CRITICAL: Disable triple buffering for UEFI - might cause black screen
        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
            BootConsole.WriteLine("[SMAIN] UEFI mode - disabling triple buffering");
            Framebuffer.TripleBuffered = false;
        } else {
            Framebuffer.TripleBuffered = true;
        }

        BootConsole.WriteLine("[SMAIN] Creating wallpaper");

        // In UEFI mode, skip wallpaper creation - go straight to rendering loop
        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
            BootConsole.WriteLine("[SMAIN] UEFI mode - skipping wallpaper creation");
            Wallpaper = null;
        } else {
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
            if (RightMenu == null) {
                RightMenu = new RightMenu();
                RightMenu.Visible = false;
            }

            // Create widget context menu
            widgetContextMenu = new WidgetContextMenu();
            widgetContextMenu.Visible = false;
            WindowManager.MoveToEnd(widgetContextMenu);
            BootConsole.WriteLine("[SMAIN] Context menus created (Legacy)");
        } else {
            // UEFI mode: Skip context menus for now (require file access)
            BootConsole.WriteLine("[SMAIN] Context menus skipped (UEFI mode)");
            RightMenu = null;
            widgetContextMenu = null;
        }

        BootConsole.WriteLine("[SMAIN] Creating widgets");
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            // Create performance widget (initially visible)
            PerfWidget = new PerformanceWidget();
            PerfWidget.Visible = false; // Don't show standalone - will be in container
            WindowManager.MoveToEnd(PerfWidget);

            // Create clock widget positioned below performance widget
            var clockWidget = new guideXOS.DockableWidgets.Clock(
                PerfWidget.X,  // Same X position as performance widget
                PerfWidget.Y + PerfWidget.Height + 10  // Below performance widget with 10px gap
            );
            clockWidget.Visible = false; // Don't show standalone - will be in container
            WindowManager.MoveToEnd(clockWidget);

            // Create monitor widget for system charts
            var monitorWidget = new guideXOS.DockableWidgets.Monitor();
            monitorWidget.Visible = false; // Don't show standalone - will be in container
            WindowManager.MoveToEnd(monitorWidget);

            // Create uptime widget to show system uptime
            var uptimeWidget = new guideXOS.DockableWidgets.Uptime(
                PerfWidget.X,  // Same X position as other widgets
                PerfWidget.Y + PerfWidget.Height + clockWidget.PreferredHeight + 20  // Below clock widget
            );
            uptimeWidget.Visible = false; // Don't show standalone - will be in container
            WindowManager.MoveToEnd(uptimeWidget);

            // Create a container and dock all widgets together
            var widgetContainer = new WidgetContainer(
                Framebuffer.Width - 220,  // Position more to the left (was -160)
                80  // Y position from top
            );
            widgetContainer.AddWidget(PerfWidget);
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
            PerfWidget = null;
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

        for (; ; ) {
            try {
                frameCounter++;

                // In UEFI mode, use simplified direct framebuffer rendering
                if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                    // Log first few frames to debug
                    if (frameCounter <= 5) {
                        Native.Out8(0x3F8, (byte)'F');
                        Native.Out8(0x3F8, (byte)'R');
                        Native.Out8(0x3F8, (byte)('0' + (frameCounter % 10)));
                        Native.Out8(0x3F8, (byte)'\n');
                    }
                    
                    uint* fb = Framebuffer.VideoMemory;
                    int fbW = Framebuffer.Width;
                    int fbH = Framebuffer.Height;
                    
                    // CRITICAL: Check if framebuffer is actually available
                    if (fb == null || fbW <= 0 || fbH <= 0) {
                        if (frameCounter == 1) {
                            BootConsole.WriteLine("[SMAIN] ERROR: Invalid framebuffer!");
                        }
                        Thread.Sleep(10);
                        continue;
                    }
                    
                    // Standard RGB format (not BGRX - framebuffer already handles conversion)
                    uint teal = 0xFF0D7D77;    // ARGB: Teal
                    uint white = 0xFFFFFFFF;   // ARGB: White
                    uint red = 0xFFFF0000;     // ARGB: Red
                    uint blue = 0xFF0000FF;    // ARGB: Blue
                    
                    // Clear to teal background
                    int totalPixels = fbW * fbH;
                    for (int i = 0; i < totalPixels; i++) {
                        fb[i] = teal;
                    }
                    
                    // Draw white border (10px thick for visibility)
                    // Top border
                    for (int y = 0; y < 10 && y < fbH; y++) {
                        for (int x = 0; x < fbW; x++) {
                            fb[y * fbW + x] = white;
                        }
                    }
                    // Bottom border
                    for (int y = fbH - 10; y < fbH && y >= 0; y++) {
                        for (int x = 0; x < fbW; x++) {
                            fb[y * fbW + x] = white;
                        }
                    }
                    // Left border
                    for (int y = 0; y < fbH; y++) {
                        for (int x = 0; x < 10 && x < fbW; x++) {
                            fb[y * fbW + x] = white;
                        }
                    }
                    // Right border
                    for (int y = 0; y < fbH; y++) {
                        for (int x = fbW - 10; x < fbW && x >= 0; x++) {
                            fb[y * fbW + x] = white;
                        }
                    }
                    
                    // Draw large test square in center
                    int centerX = fbW / 2 - 50;
                    int centerY = fbH / 2 - 50;
                    for (int dy = 0; dy < 100 && (centerY + dy) < fbH; dy++) {
                        for (int dx = 0; dx < 100 && (centerX + dx) < fbW; dx++) {
                            fb[(centerY + dy) * fbW + (centerX + dx)] = blue;
                        }
                    }
                    
                    // Draw cursor (simple white arrow)
                    int cx = Control.MousePosition.X;
                    int cy = Control.MousePosition.Y;
                    for (int dy = 0; dy < 16; dy++) {
                        int py = cy + dy;
                        if (py < 0 || py >= fbH) continue;
                        for (int dx = 0; dx < 16; dx++) {
                            int px = cx + dx;
                            if (px < 0 || px >= fbW) continue;
                            // Draw arrow shape
                            if (dy < 12 && dx < 8 && dx <= dy && dx < (12 - dy)) {
                                fb[py * fbW + px] = white;
                            }
                        }
                    }
                    
                    // Heartbeat indicator (flashing square at top-left corner)
                    uint heartbeat = (frameCounter % 60 < 30) ? white : red;
                    for (int dy = 20; dy < 40 && dy < fbH; dy++) {
                        for (int dx = 20; dx < 40 && dx < fbW; dx++) {
                            fb[dy * fbW + dx] = heartbeat;
                        }
                    }
                    
                    // Poll mouse every frame
                    try {
                        MouseEventDispatcher.Update();
                    } catch { }
                    
                    // Use Thread.Sleep instead of busy-wait (more efficient and reliable)
                    Thread.Sleep(16); // ~60 FPS
                    continue;
                }

                // Reduce serial output in UEFI mode to improve performance
                // Only log first frame
                bool shouldLog = (frameCounter == 1);

                if (shouldLog) {
                    BootConsole.WriteLine("[SMAIN] First frame rendered");
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
                    if ((Control.MouseButtons & MouseButtons.Right) == MouseButtons.Right && !RightClicked && !WindowManager.MouseHandled) {
                        RightClicked = true;
                        if (RightMenu != null) {
                            RightMenu.X = Control.MousePosition.X;
                            RightMenu.Y = Control.MousePosition.Y;
                            WindowManager.MoveToEnd(RightMenu);
                            RightMenu.Visible = true;
                        }
                    } else if ((Control.MouseButtons & MouseButtons.Right) != MouseButtons.Right) {
                        RightClicked = false;
                    }
                } catch {
                    // Ignore context menu errors
                    RightClicked = false;
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
}