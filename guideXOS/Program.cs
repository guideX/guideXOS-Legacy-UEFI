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
    /// UEFI direct renderer toggle (debug-only)
    /// </summary>
    private static bool _useUefiDirectRenderer = false;
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
            // USB stack is optional in UEFI mode - EHCI may panic if BAR0 is unmapped
            try {
                Hub.Initialize();
                HID.Initialize();
                EHCI.Initialize();
                USB.StartPolling();

                // After USB init, check if USB HID mouse is available
                MouseCapabilityDetector.CheckUsbHidMouse();
                BootConsole.WriteLine("[USB] Initialization sequence complete");
            } catch {
                BootConsole.WriteLine("[USB] Init failed (non-fatal, continuing)");
            }
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

            // UEFI: Skip BitFont file loading here — File.Instance is unreliable at this
            // stage (managed reference corruption). WindowManager.Initialize() already
            // creates a dummy IFont fallback that the taskbar can use.
            // BitFont from ramdisk can be loaded later in SMainSetupUefi after
            // File.Instance is re-mounted from the surviving Disk.Instance.

            // CRITICAL: Stop BootConsole from painting over framebuffer before entering GUI
            // From this point forward, all logging goes to serial only
            BootConsole.DrawDebugLines = false;
            
            SMain();
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

        // Run setup in a separate method to keep SMain's stack frame minimal
        SMainSetup();

        BootConsole.WriteLine("[SMAIN] Setup complete - entering main loop");

        // Add global Escape key handler to close active (topmost visible) window
        // Skip in UEFI mode (Keyboard not initialized)
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            SetupEscapeKeyHandler();
        }

        BootConsole.WriteLine("[SMAIN] Entering main render loop");

        // Enter the main render loop (in a separate method to keep stack frames small)
        RenderLoop();
    }

    /// <summary>
    /// Setup phase for SMain - extracted to keep SMain's stack frame small.
    /// NativeAOT allocates stack space for ALL local variables in a function's
    /// prologue. A giant function causes a giant sub rsp,N that can overflow
    /// the 512 KB UEFI stack.
    /// </summary>
    private static void SMainSetup() {
        if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
            SMainSetupUefi();
        } else {
            SMainSetupLegacy();
        }

        FConsole = null; // Don't create console here - let it be created on-demand

        // Initialize icons
        SetupIcons();

        // UEFI: Skip BitFont file loading — File.Instance's managed vtable is
        // corrupted even after re-mount (FS Type shows garbage pointer).
        // File.ReadAllBytes will hang (infinite loop in corrupted dispatch).
        // WindowManager.Initialize() already creates a dummy IFont fallback.

        // Context menus
        SetupContextMenus();

        // Widgets
        SetupWidgets();
    }

    /// <summary>
    /// UEFI-specific setup (framebuffer test, wallpaper, triple buffering)
    /// </summary>
    private static void SMainSetupUefi() {
        BootConsole.WriteLine("[SMAIN] UEFI MODE");
        BootConsole.WriteLine("[SMAIN] UEFI mode - disabling triple buffering");
        Framebuffer.TripleBuffered = false;
        // CRITICAL: In UEFI mode, static managed object references (like Framebuffer.Graphics
        // and File.Instance) get zeroed between EntryPoint.KMain and Program.KMain.
        // Value-type fields (VideoMemory pointer, Width, Height) survive.
        // Recreate the Graphics object from the surviving fields.
        BootConsole.WriteLine("[SMAIN] Framebuffer.Graphics is " + (Framebuffer.Graphics == null ? "NULL" : "OK"));
        BootConsole.WriteLine("[SMAIN] Framebuffer.VideoMemory = " + ((ulong)Framebuffer.VideoMemory).ToString("x"));
        Framebuffer.EnsureGraphics();
        BootConsole.WriteLine("[SMAIN] After EnsureGraphics: " + (Framebuffer.Graphics == null ? "NULL" : "OK"));
        if (Framebuffer.Graphics != null) {
            BootConsole.WriteLine("[SMAIN] Graphics.VideoMemory = " + ((ulong)Framebuffer.Graphics.VideoMemory).ToString("x"));
            // Verify the framebuffer is writable by drawing a test pixel
            Framebuffer.Graphics.VideoMemory[0] = 0x00FF00FF; // Magenta test pixel
            BootConsole.WriteLine("[SMAIN] Test pixel written OK");
        } else {
            BootConsole.WriteLine("[SMAIN] ERROR: EnsureGraphics failed!");
        }
        BootConsole.WriteLine("[SMAIN] UEFI mode - skipping wallpaper creation");
        Wallpaper = null;
        BootConsole.WriteLine("[SMAIN] Wallpaper created");
    }

    /// <summary>
    /// Legacy-specific setup (wallpaper creation, triple buffering)
    /// </summary>
    private static void SMainSetupLegacy() {
        BootConsole.WriteLine("[SMAIN] Starting desktop rendering");
        Framebuffer.TripleBuffered = true;

        BootConsole.WriteLine("[SMAIN] Creating wallpaper");
        Image wall = Wallpaper;
        try {
            BootConsole.WriteLine("[SMAIN] Checking existing wallpaper");
            if (wall != null) {
                BootConsole.WriteLine("[SMAIN] Resizing existing wallpaper");
                Wallpaper = wall.ResizeImage(Framebuffer.Width, Framebuffer.Height);
                wall.Dispose();
            } else {
                BootConsole.WriteLine("[SMAIN] Creating default gradient wallpaper");
                BootConsole.WriteLine("[SMAIN] FB size: " + Framebuffer.Width.ToString() + "x" + Framebuffer.Height.ToString());
                Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height);
                if (Wallpaper == null) {
                    BootConsole.WriteLine("[SMAIN] ERROR: Wallpaper allocation returned null!");
                } else if (Wallpaper.RawData == null) {
                    BootConsole.WriteLine("[SMAIN] ERROR: Wallpaper.RawData is null!");
                } else {
                    BootConsole.WriteLine("[SMAIN] Image allocated, drawing gradient...");
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
    }

    /// <summary>
    /// Initialize desktop icons (shared by both boot modes)
    /// </summary>
    private static void SetupIcons() {
        BootConsole.WriteLine("[SMAIN] Setting up icons");
        BootConsole.WriteLine("[SMAIN] File.Instance = " + (File.Instance == null ? "NULL" : "OK"));
        BootConsole.WriteLine("[SMAIN] Disk.Instance = " + (Disk.Instance == null ? "NULL" : "OK"));
        // In UEFI mode, File.Instance can be zeroed even though it was set in EntryPoint.
        // Re-mount from Disk.Instance if available.
        if (File.Instance == null && Disk.Instance != null) {
            BootConsole.WriteLine("[SMAIN] Re-mounting filesystem from surviving Disk.Instance");
            try {
                File.Instance = new RdskFS();
                BootConsole.WriteLine("[SMAIN] Filesystem re-mounted OK");
            } catch {
                BootConsole.WriteLine("[SMAIN] Filesystem re-mount failed");
            }
        }
        if (File.Instance != null) {
            BootConsole.WriteLine("[SMAIN] Initializing background manager and icons");
            if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
                BackgroundRotationManager.Initialize();
                guideXOS.Modules.ModuleManager.InitializeBuiltins();
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
                BootConsole.WriteLine("[SMAIN] UEFI mode - using fallback icons (no PNG)");
                _cachedDocumentIcon = new Image(48, 48);
                _cachedFolderIcon = new Image(48, 48);
                _cachedImageIcon = new Image(48, 48);
                _cachedAudioIcon = new Image(48, 48);
            }
        } else {
            BootConsole.WriteLine("[SMAIN] No filesystem - using fallback icons");
            _cachedDocumentIcon = new Image(48, 48);
            _cachedFolderIcon = new Image(48, 48);
            _cachedImageIcon = new Image(48, 48);
            _cachedAudioIcon = new Image(48, 48);
        }
    }

    /// <summary>
    /// Initialize context menus
    /// </summary>
    private static void SetupContextMenus() {
        BootConsole.WriteLine("[SMAIN] Creating context menus");
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            if (RightMenu == null) {
                RightMenu = new RightMenu();
                RightMenu.Visible = false;
            }
            widgetContextMenu = new WidgetContextMenu();
            widgetContextMenu.Visible = false;
            WindowManager.MoveToEnd(widgetContextMenu);
            BootConsole.WriteLine("[SMAIN] Context menus created (Legacy)");
        } else {
            BootConsole.WriteLine("[SMAIN] Context menus skipped (UEFI mode)");
            RightMenu = null;
            widgetContextMenu = null;
        }
    }

    /// <summary>
    /// Initialize widgets (Legacy only)
    /// </summary>
    private static void SetupWidgets() {
        BootConsole.WriteLine("[SMAIN] Creating widgets");
        if (BootConsole.CurrentMode == guideXOS.BootMode.Legacy) {
            PerfWidget = new PerformanceWidget();
            PerfWidget.Visible = false;
            WindowManager.MoveToEnd(PerfWidget);

            var clockWidget = new guideXOS.DockableWidgets.Clock(
                PerfWidget.X,
                PerfWidget.Y + PerfWidget.Height + 10
            );
            clockWidget.Visible = false;
            WindowManager.MoveToEnd(clockWidget);

            var monitorWidget = new guideXOS.DockableWidgets.Monitor();
            monitorWidget.Visible = false;
            WindowManager.MoveToEnd(monitorWidget);

            var uptimeWidget = new guideXOS.DockableWidgets.Uptime(
                PerfWidget.X,
                PerfWidget.Y + PerfWidget.Height + clockWidget.PreferredHeight + 20
            );
            uptimeWidget.Visible = false;
            WindowManager.MoveToEnd(uptimeWidget);

            var widgetContainer = new WidgetContainer(
                Framebuffer.Width - 220,
                80
            );
            widgetContainer.AddWidget(PerfWidget);
            widgetContainer.AddWidget(clockWidget);
            widgetContainer.AddWidget(monitorWidget);
            widgetContainer.AddWidget(uptimeWidget);
            widgetContainer.Visible = UISettings.ShowWidgetsOnStartup;
            WindowManager.MoveToEnd(widgetContainer);

            Program.WidgetsContainer = widgetContainer;

            if (!UISettings.ShowWidgetsOnStartup) {
                var toggle = new WidgetToggleButton(Framebuffer.Width - 26, 6);
                WindowManager.MoveToEnd(toggle);
                toggle.Visible = true;
            }
            BootConsole.WriteLine("[SMAIN] Widgets created (Legacy)");
        } else {
            BootConsole.WriteLine("[SMAIN] Widgets skipped (UEFI mode)");
            PerfWidget = null;
            widgetContextMenu = null;
            Program.WidgetsContainer = null;
        }
    }

    /// <summary>
    /// Setup global Escape key handler (Legacy only)
    /// </summary>
    private static void SetupEscapeKeyHandler() {
        Keyboard.OnKeyChanged += (sender, key) => {
            try {
                if (key.Key == System.ConsoleKey.Escape && key.KeyState == System.ConsoleKeyState.Pressed) {
                    if (Desktop.Taskbar != null && Desktop.Taskbar.IsWorkspaceSwitcherVisible) {
                        Desktop.Taskbar.CloseWorkspaceSwitcher();
                        return;
                    }
                    for (int i = WindowManager.Windows.Count - 1; i >= 0; i--) {
                        var window = WindowManager.Windows[i];
                        if (window.Visible && !window.IsTombstoned) {
                            window.OnGlobalKey(key);
                            break;
                        }
                    }
                }
            } catch {
                // Ignore errors in global key handler to prevent crashes
            }
        };
    }

    /// <summary>
    /// Writes a single ASCII char to COM1 (serial port 0x3F8) for bare-metal debug.
    /// </summary>
    private static void SerialChar(char c) {
        Native.Out8(0x3F8, (byte)c);
    }

    /// <summary>
    /// Main render loop - extracted from SMain to keep stack frames small
    /// </summary>
    private static void RenderLoop() {
        int lastMouseX = Control.MousePosition.X;
        int lastMouseY = Control.MousePosition.Y;
        ulong lastMoveTick = Timer.Ticks;
        const ulong ActiveMoveMs = 100;
        int frameCounter = 0;
        bool isUefi = (BootConsole.CurrentMode == guideXOS.BootMode.UEFI);

        for (; ; ) {
            try {
                frameCounter++;

                // In UEFI mode, optionally use simplified direct framebuffer rendering (debug-only)
                if (isUefi && _useUefiDirectRenderer) {
                    RenderFrameUefiDirect(frameCounter);
                    Thread.Sleep(16);
                    continue;
                }

                // Emit per-step serial markers for first 3 frames in UEFI mode
                // Format: [F<n>:<step>] where step is a single letter
                bool debugFrame = isUefi && (frameCounter <= 3);

                if (debugFrame) { SerialChar('['); SerialChar('F'); SerialChar((char)('0' + (frameCounter % 10))); SerialChar(':'); SerialChar('A'); SerialChar(']'); } // A = start

                bool shouldLog = (frameCounter == 1);

                // CRITICAL: Ensure Graphics object exists and points at the real framebuffer.
                Framebuffer.EnsureGraphics();

                if (debugFrame) SerialChar('B'); // B = EnsureGraphics done

                if (shouldLog) {
                    // Use serial-only output to avoid BootConsole painting over the framebuffer
                    SerialChar('F'); SerialChar('1'); SerialChar('\n');
                }

                // Periodically refresh cached icons (skip in UEFI for now)
                if (!isUefi && UISettings.EnableDesktopIconCacheRefresh && File.Instance != null) {
                    ulong intervalMs = (ulong)UISettings.DesktopIconCacheRefreshIntervalMinutes * 60000UL;
                    if (Timer.Ticks - _lastIconCacheRefresh >= intervalMs) {
                        RefreshCachedIcons();
                        _lastIconCacheRefresh = Timer.Ticks;
                    }
                }

                // Update background rotation manager - Legacy only
                if (!isUefi) {
                    try {
                        BackgroundRotationManager.Update();
                    } catch { }
                }

                if (debugFrame) SerialChar('C'); // C = pre-mouse

                // Poll mouse input
                try {
                    MouseEventDispatcher.Update();
                } catch { }

                if (debugFrame) SerialChar('D'); // D = post-mouse

                // Per-frame input pass
                WindowManager.MouseHandled = false;
                try {
                    WindowManager.InputAll();
                } catch { }

                if (debugFrame) SerialChar('E'); // E = post-input

                // FlushPendingCreates - Legacy only
                if (!isUefi) {
                    try { WindowManager.FlushPendingCreates(); } catch { }
                }

                // Audio - Legacy only
                if (!isUefi) {
                    try { WAVPlayer.DoPlay(); } catch { }
                }

                if (debugFrame) SerialChar('F'); // F = pre-clear

                //clear screen
                try {
                    // CRITICAL: Use Graphics.VideoMemory, not Framebuffer.VideoMemory.
                    // In UEFI mode, Framebuffer.VideoMemory can be corrupted to a heap address
                    // while Graphics.VideoMemory retains the correct framebuffer pointer.
                    uint* vm = Framebuffer.Graphics.VideoMemory;
                    Native.Stosd(vm, 0x00000000, (ulong)(Framebuffer.Width * Framebuffer.Height));
                } catch {
                    if (debugFrame) { SerialChar('!'); SerialChar('F'); SerialChar('\n'); }
                    Thread.Sleep(1);
                    continue;
                }

                if (debugFrame) SerialChar('G'); // G = post-clear, pre-background

                //draw background
                try {
                    if (!isUefi) {
                        BackgroundRotationManager.DrawBackground();
                    } else {
                        // UEFI: Write teal directly to framebuffer using raw pointer
                        // Use 0xFF alpha for opaque (ARGB format: 0xAARRGGBB)
                        // CRITICAL: Use Graphics.VideoMemory (correct address)
                        Native.Stosd(Framebuffer.Graphics.VideoMemory, 0xFF0D7D77, (ulong)(Framebuffer.Width * Framebuffer.Height));
                    }
                } catch {
                    if (debugFrame) { SerialChar('!'); SerialChar('G'); }
                    // Fallback: try raw fill using Graphics pointer
                    try {
                        Native.Stosd(Framebuffer.Graphics.VideoMemory, 0xFF0D7D77, (ulong)(Framebuffer.Width * Framebuffer.Height));
                    } catch { }
                }

                if (debugFrame) SerialChar('H'); // H = post-background

                // Context menu - skip in UEFI (RightMenu is null)
                if (!isUefi) {
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
                        RightClicked = false;
                    }
                }

                if (debugFrame) SerialChar('I'); // I = pre-desktop

                // Draw desktop icons and taskbar
                try {
                    Desktop.Update(
                        _cachedDocumentIcon,
                        _cachedFolderIcon,
                        _cachedImageIcon,
                        _cachedAudioIcon,
                        48
                    );
                } catch {
                    if (debugFrame) { SerialChar('!'); SerialChar('I'); }
                }

                if (debugFrame) SerialChar('J'); // J = pre-windows

                // Draw windows
                try {
                    WindowManager.DrawAllExceptTaskManager();
                } catch {
                    if (debugFrame) { SerialChar('!'); SerialChar('J'); }
                }

                try {
                    if (Desktop.Taskbar != null) {
                        Desktop.Taskbar.DrawWorkspaceSwitcher();
                    }
                } catch { }

                try {
                    WindowManager.DrawTaskManager();
                } catch { }

                try {
                    WindowManager.CleanupClosedWindows();
                } catch { }

                if (debugFrame) SerialChar('K'); // K = pre-cursor

                //draw cursor
                try {
                    var img = (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left ? CursorMoving : Cursor;
                    if (img != null && img.RawData != null) {
                        Framebuffer.Graphics.DrawImage(Control.MousePosition.X, Control.MousePosition.Y, img);
                    } else {
                        if (debugFrame) { SerialChar('!'); SerialChar('K'); }
                    }
                } catch {
                    if (debugFrame) { SerialChar('x'); SerialChar('K'); }
                }

                if (debugFrame) SerialChar('L'); // L = pre-update

                //refresh screen
                try {
                    Framebuffer.Update();
                } catch {
                    if (debugFrame) { SerialChar('!'); SerialChar('L'); SerialChar('\n'); }
                    Thread.Sleep(1);
                    continue;
                }

                if (debugFrame) { SerialChar('Z'); SerialChar('\n'); } // Z = frame complete

                // Mouse responsiveness throttling
                int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
                if (mx != lastMouseX || my != lastMouseY) {
                    lastMouseX = mx; lastMouseY = my; lastMoveTick = Timer.Ticks;
                    Thread.Sleep(0);
                } else {
                    ulong age = (Timer.Ticks >= lastMoveTick) ? (Timer.Ticks - lastMoveTick) : 0UL;
                    if (age < ActiveMoveMs) Thread.Sleep(0); else Thread.Sleep(1);
                }
            } catch {
                if (isUefi && frameCounter <= 3) {
                    SerialChar('!'); SerialChar('!'); SerialChar('!'); SerialChar('\n');
                }
                Thread.Sleep(10);
            }
        }
    }

    /// <summary>
    /// Draw UEFI background (teal gradient) directly to framebuffer
    /// </summary>
    private static void DrawUefiBackground() {
        uint* fb = Framebuffer.Graphics.VideoMemory;
        int fbW = Framebuffer.Width;
        int fbH = Framebuffer.Height;
        for (int y = 0; y < fbH; y++) {
            int t = (y * 128) / fbH;
            uint color = (uint)(0xFF0D7D77 + (t << 16) + (t << 8) + t);
            for (int x = 0; x < fbW; x++) {
                fb[y * fbW + x] = color;
            }
        }
    }

    /// <summary>
    /// Debug-only UEFI direct framebuffer renderer (renders test pattern + cursor)
    /// </summary>
    private static void RenderFrameUefiDirect(int frameCounter) {
        if (frameCounter <= 5) {
            Native.Out8(0x3F8, (byte)'F');
            Native.Out8(0x3F8, (byte)'R');
            Native.Out8(0x3F8, (byte)('0' + (frameCounter % 10)));
            Native.Out8(0x3F8, (byte)'\n');
        }

        uint* fb = Framebuffer.Graphics != null ? Framebuffer.Graphics.VideoMemory : Framebuffer.VideoMemory;
        int fbW = Framebuffer.Width;
        int fbH = Framebuffer.Height;

        if (fb == null || fbW <= 0 || fbH <= 0) {
            if (frameCounter == 1) {
                BootConsole.WriteLine("[SMAIN] ERROR: Invalid framebuffer!");
            }
            Thread.Sleep(10);
            return;
        }

        uint teal = 0xFF0D7D77;
        uint white = 0xFFFFFFFF;
        uint red = 0xFFFF0000;
        uint blue = 0xFF0000FF;

        int totalPixels = fbW * fbH;
        for (int i = 0; i < totalPixels; i++) {
            fb[i] = teal;
        }

        // White border
        for (int y = 0; y < 10 && y < fbH; y++)
            for (int x = 0; x < fbW; x++)
                fb[y * fbW + x] = white;
        for (int y = fbH - 10; y < fbH && y >= 0; y++)
            for (int x = 0; x < fbW; x++)
                fb[y * fbW + x] = white;
        for (int y = 0; y < fbH; y++)
            for (int x = 0; x < 10 && x < fbW; x++)
                fb[y * fbW + x] = white;
        for (int y = 0; y < fbH; y++)
            for (int x = fbW - 10; x < fbW && x >= 0; x++)
                fb[y * fbW + x] = white;

        // Center square
        int centerX = fbW / 2 - 50;
        int centerY = fbH / 2 - 50;
        for (int dy = 0; dy < 100 && (centerY + dy) < fbH; dy++)
            for (int dx = 0; dx < 100 && (centerX + dx) < fbW; dx++)
                fb[(centerY + dy) * fbW + (centerX + dx)] = blue;

        // Cursor
        int cx = Control.MousePosition.X;
        int cy = Control.MousePosition.Y;
        for (int dy = 0; dy < 16; dy++) {
            int py = cy + dy;
            if (py < 0 || py >= fbH) continue;
            for (int dx = 0; dx < 16; dx++) {
                int px = cx + dx;
                if (px < 0 || px >= fbW) continue;
                if (dy < 12 && dx < 8 && dx <= dy && dx < (12 - dy)) {
                    fb[py * fbW + px] = white;
                }
            }
        }

        // Heartbeat
        uint heartbeat = (frameCounter % 60 < 30) ? white : red;
        for (int dy = 20; dy < 40 && dy < fbH; dy++)
            for (int dx = 20; dx < 40 && dx < fbW; dx++)
                fb[dy * fbW + dx] = heartbeat;

        try {
            MouseEventDispatcher.Update();
        } catch { }
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