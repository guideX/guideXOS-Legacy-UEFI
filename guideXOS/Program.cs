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
    /// Load a PNG image using PngLoader (UEFI-safe).
    /// Returns null on any failure (no exceptions, no hangs).
    /// </summary>
    private static Image LoadPngSafe(string path) {
        byte[] data = null;
        try { data = File.ReadAllBytes(path); } catch { return null; }
        if (data == null || data.Length < 33) return null;
        Image result;
        if (PngLoader.Load(data, out result) && result != null)
            return result;
        return null;
    }

    /// <summary>
    /// Create a procedural arrow cursor (16x16).
    /// Used in UEFI mode where PNG decoding is unsafe.
    /// </summary>
    private static Image CreateFallbackCursor() {
        var img = new Image(16, 16);
        if (img == null || img.RawData == null) return img;
        for (int i = 0; i < 16 * 16; i++) {
            img.RawData[i] = 0;
        }
        for (int y = 0; y < 16; y++) {
            for (int x = 0; x < 16; x++) {
                if (y < 12 && x < 8 && x <= y && x < (12 - y)) {
                    img.RawData[y * 16 + x] = unchecked((int)0xFFFFFFFF);
                } else if (y < 13 && x < 9 && (x == y + 1 || x == (11 - y) || (y == 11 && x <= 7))) {
                    img.RawData[y * 16 + x] = unchecked((int)0xFF000000);
                }
            }
        }
        return img;
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
            // Raw serial marker to confirm we returned from DetectAndInitialize
            Native.Out8(0x3F8, (byte)'M');
            Native.Out8(0x3F8, (byte)'C');
            Native.Out8(0x3F8, (byte)'D');
            Native.Out8(0x3F8, (byte)'O');
            Native.Out8(0x3F8, (byte)'K');
            Native.Out8(0x3F8, (byte)'\n');
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
            // Skip EHCI entirely in UEFI mode to avoid accessing unmapped MMIO addresses
            // that would cause page faults (uncatchable as C# exceptions)
            try {
                Hub.Initialize();
                HID.Initialize();
                // EHCI accesses PCI BAR0 MMIO which may not be identity-mapped in UEFI mode.
                // A page fault here is fatal (not catchable). Skip EHCI in UEFI.
                BootConsole.WriteLine("[USB] Skipping EHCI in UEFI mode (unmapped MMIO risk)");
                // USB.StartPolling(); // Skip polling without EHCI

                // After USB init, check if USB HID mouse is available
                MouseCapabilityDetector.CheckUsbHidMouse();
                BootConsole.WriteLine("[USB] Initialization sequence complete");
            } catch {
                BootConsole.WriteLine("[USB] Init failed (non-fatal, continuing)");
            }
            // Log final mouse detection result
            BootConsole.WriteLine("[INPUT] Mouse detection complete");
            BootConsole.WriteLine("[INPUT] Mouse enabled: " + MouseCapabilityDetector.MouseEnabled);

            // CRITICAL: Re-mount filesystem early.
            // In UEFI mode, ALL managed static references (File.Instance, Disk.Instance,
            // Ramdisk.Instance) get zeroed between EntryPoint.KMain and Program.KMain.
            // However, Ramdisk.RawBasePointer (a static byte*) survives because it's a
            // value type, not a managed reference. Use it to reconstruct everything.
            BootConsole.WriteLine("[FS] Re-mounting filesystem for UEFI");
            BootConsole.WriteLine("[FS] RawBasePointer = " + ((ulong)Ramdisk.RawBasePointer).ToString("x"));
            if (Ramdisk.RawBasePointer != null) {
                try {
                    // Reconstruct Ramdisk from surviving raw pointer
                    if (Disk.Instance == null) {
                        BootConsole.WriteLine("[FS] Reconstructing Ramdisk from RawBasePointer");
                        new Ramdisk((System.IntPtr)Ramdisk.RawBasePointer);
                        BootConsole.WriteLine("[FS] Ramdisk reconstructed");
                    }
                    // Now mount RdskFS (uses RawBasePointer directly)
                    File.Instance = new RdskFS();
                    BootConsole.WriteLine("[FS] Filesystem re-mounted OK");
                } catch {
                    BootConsole.WriteLine("[FS] Filesystem re-mount FAILED");
                }
            } else {
                BootConsole.WriteLine("[FS] FATAL: RawBasePointer is NULL - no ramdisk!");
            }

            BootConsole.WriteLine("[CURSOR] Creating cursor images");
            // Debug: Verify we're still in UEFI mode
            Native.Out8(0x3F8, (byte)'[');
            Native.Out8(0x3F8, (byte)'M');
            Native.Out8(0x3F8, (byte)'O');
            Native.Out8(0x3F8, (byte)'D');
            Native.Out8(0x3F8, (byte)'E');
            Native.Out8(0x3F8, (byte)'=');
            byte modeVal = (byte)(BootConsole.CurrentMode == guideXOS.BootMode.UEFI ? 'U' : 'L');
            Native.Out8(0x3F8, modeVal);
            Native.Out8(0x3F8, (byte)']');
            Native.Out8(0x3F8, (byte)'\n');
            // UEFI: Skip all PNG decoding for cursors.
            // Both LodePNG (managed port) and PngLoader hang during DEFLATE
            // decompression in the post-ExitBootServices environment, blocking
            // the entire GUI from initialising. Use procedural cursors instead.
            BootConsole.WriteLine("[CURSOR] About to call CreateFallbackCursor()");
            Cursor = CreateFallbackCursor();
            BootConsole.WriteLine("[CURSOR] CreateFallbackCursor() returned, assigning to CursorMoving");
            CursorMoving = Cursor;
            BootConsole.WriteLine("[CURSOR] CursorMoving assigned, assigning to CursorBusy");
            CursorBusy = Cursor;
            BootConsole.WriteLine("[CURSOR] Cursors created (procedural fallback)");
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
            // RAW serial marker to prove we entered Legacy path
            Native.Out8(0x3F8, (byte)'[');
            Native.Out8(0x3F8, (byte)'L');
            Native.Out8(0x3F8, (byte)'E');
            Native.Out8(0x3F8, (byte)'G');
            Native.Out8(0x3F8, (byte)'A');
            Native.Out8(0x3F8, (byte)'C');
            Native.Out8(0x3F8, (byte)'Y');
            Native.Out8(0x3F8, (byte)']');
            Native.Out8(0x3F8, (byte)'\n');
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
        // OriginalVideoMemory is the authoritative framebuffer address.
        BootConsole.WriteLine("[SMAIN] Framebuffer.Graphics is " + (Framebuffer.Graphics == null ? "NULL" : "OK"));
        BootConsole.WriteLine("[SMAIN] Framebuffer.VideoMemory = " + ((ulong)Framebuffer.VideoMemory).ToString("x"));
        BootConsole.WriteLine("[SMAIN] Framebuffer.OriginalVideoMemory = " + ((ulong)Framebuffer.OriginalVideoMemory).ToString("x"));
        // If VideoMemory got corrupted, restore from OriginalVideoMemory before EnsureGraphics
        if ((ulong)Framebuffer.OriginalVideoMemory != 0 && (ulong)Framebuffer.VideoMemory != (ulong)Framebuffer.OriginalVideoMemory) {
            BootConsole.WriteLine("[SMAIN] WARNING: VideoMemory corrupted! Restoring from OriginalVideoMemory");
            Framebuffer.VideoMemory = Framebuffer.OriginalVideoMemory;
        }
        Framebuffer.EnsureGraphics();
        BootConsole.WriteLine("[SMAIN] After EnsureGraphics: " + (Framebuffer.Graphics == null ? "NULL" : "OK"));
        if (Framebuffer.Graphics != null) {
            BootConsole.WriteLine("[SMAIN] Graphics.VideoMemory = " + ((ulong)Framebuffer.Graphics.VideoMemory).ToString("x"));
            BootConsole.WriteLine("[SMAIN] EnsureGraphics OK");
        } else {
            BootConsole.WriteLine("[SMAIN] ERROR: EnsureGraphics failed!");
        }
        // Create teal gradient wallpaper for UEFI desktop
        BootConsole.WriteLine("[SMAIN] Creating UEFI wallpaper");
        try {
            Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height);
            if (Wallpaper != null && Wallpaper.RawData != null) {
                uint topColor = 0xFF5FD4C4;
                uint bottomColor = 0xFF0D7D77;
                int topR = (int)((topColor >> 16) & 0xFF);
                int topG = (int)((topColor >> 8) & 0xFF);
                int topB = (int)(topColor & 0xFF);
                int bottomR = (int)((bottomColor >> 16) & 0xFF);
                int bottomG = (int)((bottomColor >> 8) & 0xFF);
                int bottomB = (int)(bottomColor & 0xFF);
                for (int y = 0; y < Framebuffer.Height; y++) {
                    int t256 = (y * 256) / Framebuffer.Height;
                    int r = topR + ((bottomR - topR) * t256) / 256;
                    int g = topG + ((bottomG - topG) * t256) / 256;
                    int b = topB + ((bottomB - topB) * t256) / 256;
                    int color = unchecked((int)(0xFF000000 | (uint)(r << 16) | (uint)(g << 8) | (uint)b));
                    int rowBase = y * Framebuffer.Width;
                    for (int x = 0; x < Framebuffer.Width; x++) {
                        Wallpaper.RawData[rowBase + x] = color;
                    }
                }
                BootConsole.WriteLine("[SMAIN] Wallpaper gradient created");
            }
        } catch {
            BootConsole.WriteLine("[SMAIN] Wallpaper creation failed - using null");
            Wallpaper = null;
        }
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
        // Re-mount if needed (should already be done in KMain, but safety check)
        if (File.Instance == null && Ramdisk.RawBasePointer != null) {
            BootConsole.WriteLine("[SMAIN] Re-mounting filesystem from RawBasePointer");
            try {
                if (Disk.Instance == null) {
                    new Ramdisk((System.IntPtr)Ramdisk.RawBasePointer);
                }
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
                // In UEFI mode, this is only safe during the first GUI frame; repeated
                // managed-reference repair can trip over post-ExitBootServices state.
                if (!isUefi || frameCounter == 1) {
                    Framebuffer.EnsureGraphics();

                    // UEFI safety: validate Graphics.VideoMemory points at the real framebuffer
                    // If OriginalVideoMemory is set, verify Graphics uses it (not a heap address)
                    if (isUefi && (ulong)Framebuffer.OriginalVideoMemory != 0) {
                        if (Framebuffer.Graphics != null && (ulong)Framebuffer.Graphics.VideoMemory != (ulong)Framebuffer.OriginalVideoMemory) {
                            // Graphics pointer was corrupted - fix it
                            Framebuffer.Graphics.VideoMemory = Framebuffer.OriginalVideoMemory;
                            Framebuffer.VideoMemory = Framebuffer.OriginalVideoMemory;
                        }
                    }
                }

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

                // UEFI Frame 1 diagnostics: Print framebuffer state
                if (isUefi && frameCounter == 1) {
                    SerialChar('['); SerialChar('F'); SerialChar('B'); SerialChar(':');
                    // Print Width
                    int w = Framebuffer.Width;
                    SerialChar((char)('0' + (w / 1000) % 10));
                    SerialChar((char)('0' + (w / 100) % 10));
                    SerialChar((char)('0' + (w / 10) % 10));
                    SerialChar((char)('0' + w % 10));
                    SerialChar('x');
                    // Print Height
                    int h = Framebuffer.Height;
                    SerialChar((char)('0' + (h / 1000) % 10));
                    SerialChar((char)('0' + (h / 100) % 10));
                    SerialChar((char)('0' + (h / 10) % 10));
                    SerialChar((char)('0' + h % 10));
                    SerialChar(']');
                }

                // Clear screen. In UEFI mode the GOP framebuffer is uncached MMIO;
                // full-frame rep stosd is prohibitively slow and can stall boot.
                if (!isUefi) {
                    try {
                        uint* vm = Framebuffer.Graphics.VideoMemory;
                        Native.Stosd(vm, 0x00000000, (ulong)(Framebuffer.Width * Framebuffer.Height));
                    } catch {
                        if (debugFrame) { SerialChar('!'); SerialChar('F'); SerialChar('\n'); }
                        Thread.Sleep(1);
                        continue;
                    }
                }

                if (debugFrame) SerialChar('G'); // G = post-clear, pre-background

                //draw background
                try {
                    if (!isUefi) {
                        BackgroundRotationManager.DrawBackground();
                    } else {
                        // UEFI: avoid full-screen MMIO blits during bring-up.
                        // The boot splash remains as the background while UI elements render.
                    }
                } catch {
                    if (debugFrame) { SerialChar('!'); SerialChar('G'); }
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

                // Draw cursor. UEFI has no active post-ExitBootServices pointer provider yet,
                // so avoid managed cursor image blits during bring-up.
                if (!isUefi) {
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

                if (isUefi) {
                    DrawUefiHeartbeat(frameCounter);
                    if ((frameCounter & 0xFFF) == 0) {
                        SerialChar('H'); SerialChar('B'); SerialChar('\n');
                    }
                }

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
    /// Tiny UEFI proof-of-life marker drawn directly to GOP memory.
    /// </summary>
    private static void DrawUefiHeartbeat(int frameCounter) {
        uint* fb = Framebuffer.OriginalVideoMemory != null
            ? Framebuffer.OriginalVideoMemory
            : (Framebuffer.VideoMemory != null
                ? Framebuffer.VideoMemory
                : (Framebuffer.Graphics != null ? Framebuffer.Graphics.VideoMemory : null));
        int fbW = Framebuffer.Width;
        int fbH = Framebuffer.Height;

        if (fb == null || fbW <= 0 || fbH <= 0) {
            return;
        }

        uint color = ((frameCounter / 30) & 1) == 0 ? 0xFFFFFFFFu : 0xFFFF3040u;
        int maxY = fbH < 24 ? fbH : 24;
        int maxX = fbW < 24 ? fbW : 24;

        for (int y = 8; y < maxY; y++) {
            int row = y * fbW;
            for (int x = 8; x < maxX; x++) {
                fb[row + x] = color;
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
