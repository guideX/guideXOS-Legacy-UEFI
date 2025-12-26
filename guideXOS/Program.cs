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
        HID.GetMouse(HID.Mouse, out _ /*sbyte AxisX*/, out _ /*sbyte AxisY*/, out var Buttons);
        return Buttons != MouseButtons.None;
    }
    /// <summary>
    /// USB Keyboard Test
    /// </summary>
    /// <returns></returns>
    private static bool USBKeyboardTest() {
        HID.GetKeyboard(HID.Keyboard, out var ScanCode, out _/*var Key*/);
        return ScanCode != 0;
    }
    /// <summary>
    /// KMain - Called by EntryPoint after initialization
    /// Note: This is NOT the UEFI entry point. The actual UEFI entry is in EntryPoint.cs
    /// </summary>
    public static void KMain() {
        // CRITICAL DEBUG: Write directly to serial port to prove we're here
        Native.Out8(0x3F8, (byte)'P');
        Native.Out8(0x3F8, (byte)'R');
        Native.Out8(0x3F8, (byte)'O');
        Native.Out8(0x3F8, (byte)'G');
        Native.Out8(0x3F8, (byte)'\r');
        Native.Out8(0x3F8, (byte)'\n');
        
        // Serial debug: Entering KMain
        // SIMPLIFIED: Removed excessive serial debug markers that were overwhelming the UART
        // The serial busy-wait loops were causing hangs after USB init
        
        // SKIP Console.WriteLine for now - it might be broken
        //Console.WriteLine("[BOOT] KMain started");
        
        Native.Out8(0x3F8, (byte)'A');
        Native.Out8(0x3F8, (byte)'N');
        Native.Out8(0x3F8, (byte)'I');
        Native.Out8(0x3F8, (byte)'M');
        
        Animator.Initialize();
        
        Native.Out8(0x3F8, (byte)'a');
        Native.Out8(0x3F8, (byte)'n');
        Native.Out8(0x3F8, (byte)'i');
        Native.Out8(0x3F8, (byte)'m');
        
        //Console.WriteLine("[BOOT] Animator initialized");

        // Initialize legacy PS/2 input first so VirtualBox (default PS/2 devices) works out-of-the-box.
        // This provides keyboard IRQ1 (0x21) and mouse IRQ12 (0x2C) handling even without USB HID.
        
        Native.Out8(0x3F8, (byte)'K');
        Native.Out8(0x3F8, (byte)'B');
        Native.Out8(0x3F8, (byte)'D');
        
        // CRITICAL: PS2Keyboard.Initialize() hangs - we already skipped PS/2 controller init in EntryPoint
        // GUI will work without keyboard/mouse for first render test
        //Console.WriteLine("[BOOT] Initializing PS/2 keyboard...");
        //try { PS2Keyboard.Initialize(); } catch { }
        
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'K');
        Native.Out8(0x3F8, (byte)'k');
        Native.Out8(0x3F8, (byte)'b');
        Native.Out8(0x3F8, (byte)'d');
        
        //Console.WriteLine("[BOOT] PS/2 keyboard done");
        //Console.WriteLine("[BOOT] Initializing PS/2 mouse...");
        
        Native.Out8(0x3F8, (byte)'M');
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'E');
        
        // CRITICAL: PS2Mouse.Initialise() hangs - we already skipped PS/2 controller init in EntryPoint
        // GUI will work without keyboard/mouse for first render test
        //try { PS2Mouse.Initialise(); } catch { }
        
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'K');
        Native.Out8(0x3F8, (byte)'m');
        Native.Out8(0x3F8, (byte)'s');
        Native.Out8(0x3F8, (byte)'e');
        
        //Console.WriteLine("[BOOT] PS/2 mouse done");
        
        // Initialize VMware absolute pointer backdoor if present (no-op on other hypervisors)
        // CRITICAL: VMwareTools.Initialize() seems to hang at the end - skip it entirely
        // try { VMwareTools.Initialize(); } catch { }
        
        // CRITICAL: Console.WriteLine() HANGS! Skip it
        //Console.WriteLine("[BOOT] VMware init skipped");
        
        Native.Out8(0x3F8, (byte)'V');
        Native.Out8(0x3F8, (byte)'M');
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'K');
        Native.Out8(0x3F8, (byte)'I');
        Native.Out8(0x3F8, (byte)'P');

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
        */

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
// SKIP USB initialization - causes hang without timer interrupts
// USB is optional; PS/2 keyboard/mouse already initialized
        
        Native.Out8(0x3F8, (byte)'U');
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'B');
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'K');
#endif

        //Sized width to 512
        Native.Out8(0x3F8, (byte)'I');
        Native.Out8(0x3F8, (byte)'M');
        Native.Out8(0x3F8, (byte)'G');
        
        // CRITICAL: PNG loading hangs! Use dummy images instead
        Native.Out8(0x3F8, (byte)'1');
        //try { Cursor = new PNG(File.ReadAllBytes("Images/Cursor.png")); } catch { Cursor = new Image(16,16); }
        Cursor = new Image(16,16);  // Skip PNG, use dummy image
        Native.Out8(0x3F8, (byte)'2');
        //try { CursorMoving = new PNG(File.ReadAllBytes("Images/Grab.png")); } catch { CursorMoving = Cursor; }
        CursorMoving = Cursor;  // Skip PNG, reuse cursor
        Native.Out8(0x3F8, (byte)'3');
        //try { CursorBusy = new PNG(File.ReadAllBytes("Images/Busy.png")); } catch { CursorBusy = Cursor; }
        CursorBusy = Cursor;  // Skip PNG, reuse cursor
        Native.Out8(0x3F8, (byte)'i');
        Native.Out8(0x3F8, (byte)'m');
        Native.Out8(0x3F8, (byte)'g');
        //try { Wallpaper = new PNG(File.ReadAllBytes("Images/tronporche.png")); } catch { Wallpaper = new Image(Framebuffer.Width, Framebuffer.Height); }
        
        Native.Out8(0x3F8, (byte)'F');
        Native.Out8(0x3F8, (byte)'N');
        Native.Out8(0x3F8, (byte)'T');
        
        // CRITICAL: Font file loading hangs! Skip BitFont initialization
        //BitFont.Initialize();
        // FIXED: Added leading space to charset to match font image layout
        //string CustomCharset = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
        
        //BitFont.RegisterBitFont(new BitFontDescriptor("Enludo", CustomCharset, File.ReadAllBytes("Fonts/enludo.btf"), 16));
        // Skip font loading - system will use fallback rendering
        
        Native.Out8(0x3F8, (byte)'f');
        Native.Out8(0x3F8, (byte)'n');
        Native.Out8(0x3F8, (byte)'t');
        //Terminal = null;
        
        Native.Out8(0x3F8, (byte)'W');
        Native.Out8(0x3F8, (byte)'M');
        Native.Out8(0x3F8, (byte)'[');
        WindowManager.Initialize();
        Native.Out8(0x3F8, (byte)']');
        Native.Out8(0x3F8, (byte)'w');
        Native.Out8(0x3F8, (byte)'m');
        
        Native.Out8(0x3F8, (byte)'D');
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'[');
        Desktop.Initialize();
        Native.Out8(0x3F8, (byte)']');
        Native.Out8(0x3F8, (byte)'d');
        Native.Out8(0x3F8, (byte)'s');
        
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'U');
        Native.Out8(0x3F8, (byte)'B');
        Native.Out8(0x3F8, (byte)'[');
        Firewall.Initialize();
        Native.Out8(0x3F8, (byte)'F');
        Audio.Initialize();
        Native.Out8(0x3F8, (byte)'A');
        AC97.Initialize();
        Native.Out8(0x3F8, (byte)'C');
        ES1371.Initialize();
        Native.Out8(0x3F8, (byte)'E');
        Native.Out8(0x3F8, (byte)']');
        Native.Out8(0x3F8, (byte)'s');
        Native.Out8(0x3F8, (byte)'u');
        Native.Out8(0x3F8, (byte)'b');
        Native.Out8(0x3F8, (byte)'\r');
        Native.Out8(0x3F8, (byte)'\n');
        
        // DEBUG: Add marker after subsystems
        Native.Out8(0x3F8, (byte)'P');
        Native.Out8(0x3F8, (byte)'O');
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'T');
        Native.Out8(0x3F8, (byte)'-');
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'U');
        Native.Out8(0x3F8, (byte)'B');
        Native.Out8(0x3F8, (byte)'\r');
        Native.Out8(0x3F8, (byte)'\n');
        
// MINIMAL BOOT: Skip network initialization - it uses Console.WriteLine which may crash
#if false // NETWORK - DISABLED FOR MINIMAL BOOT
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

        Native.Out8(0x3F8, (byte)'N');
        Native.Out8(0x3F8, (byte)'E');
        Native.Out8(0x3F8, (byte)'T');
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'K');
        Native.Out8(0x3F8, (byte)'\r');
        Native.Out8(0x3F8, (byte)'\n');

// Apply saved display mode before wallpaper resize
Native.Out8(0x3F8, (byte)'C');
Native.Out8(0x3F8, (byte)'F');
Native.Out8(0x3F8, (byte)'G');
Native.Out8(0x3F8, (byte)'[');

// CRITICAL: DisplayManager.ApplySavedResolution() tries to read config file which hangs!
// Skip it - we'll use the current resolution from UEFI GOP
//DisplayManager.ApplySavedResolution();

Native.Out8(0x3F8, (byte)'D');
Native.Out8(0x3F8, (byte)'S');
Native.Out8(0x3F8, (byte)'K');

// Load saved configuration (UI settings, window positions, recent files, etc.)
// CRITICAL: Configuration.LoadConfiguration() reads files which hangs!
// Skip it - we'll use default settings
//guideXOS.OS.Configuration.LoadConfiguration();

Native.Out8(0x3F8, (byte)'L');
Native.Out8(0x3F8, (byte)'S');
Native.Out8(0x3F8, (byte)'K');
Native.Out8(0x3F8, (byte)']');
Native.Out8(0x3F8, (byte)'c');
Native.Out8(0x3F8, (byte)'f');
Native.Out8(0x3F8, (byte)'g');

Native.Out8(0x3F8, (byte)'\r');
Native.Out8(0x3F8, (byte)'\n');
Native.Out8(0x3F8, (byte)'=');
Native.Out8(0x3F8, (byte)'L');
Native.Out8(0x3F8, (byte)'O');
Native.Out8(0x3F8, (byte)'O');
Native.Out8(0x3F8, (byte)'P');
Native.Out8(0x3F8, (byte)'=');
Native.Out8(0x3F8, (byte)'\r');
Native.Out8(0x3F8, (byte)'\n');
SMain();
    }

#if NETWORK
    private static void Client_OnData(byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            Console.Write((char)data[i]);
        }
        Console.WriteLine();
    }

    public static byte[] ToASCII(string s) 
    {
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
        // CRITICAL: Add markers to prove we entered SMain
        Native.Out8(0x3F8, (byte)'S');
        Native.Out8(0x3F8, (byte)'M');
        Native.Out8(0x3F8, (byte)'A');
        Native.Out8(0x3F8, (byte)'I');
        Native.Out8(0x3F8, (byte)'N');
        Native.Out8(0x3F8, (byte)'\r');
        Native.Out8(0x3F8, (byte)'\n');
        
        // ============================================================
        // ULTRA MINIMAL BOOT MODE - Absolute minimum to show anything
        // ============================================================
        
        Native.Out8(0x3F8, (byte)'0');
        
        // SKIP TripleBuffered - it calls Graphics.Clear which might crash
        // Framebuffer.TripleBuffered = true;
        
        Native.Out8(0x3F8, (byte)'1');
        
        // Skip wallpaper Image creation - might be crashing
        Wallpaper = null;
        
        Native.Out8(0x3F8, (byte)'2');
        
        // MINIMAL: Skip all widget/menu/icon initialization
        FConsole = null;
        rightmenu = null;
        widgetContextMenu = null;
        perfWidget = null;
        WidgetsContainer = null;
        
        Native.Out8(0x3F8, (byte)'3');
        Native.Out8(0x3F8, (byte)'\r');
        Native.Out8(0x3F8, (byte)'\n');
        
        Native.Out8(0x3F8, (byte)'L');
        Native.Out8(0x3F8, (byte)'O');
        Native.Out8(0x3F8, (byte)'O');
        Native.Out8(0x3F8, (byte)'P');
        Native.Out8(0x3F8, (byte)'\r');
        Native.Out8(0x3F8, (byte)'\n');
        
        // ============================================================
        // ULTRA MINIMAL RENDER LOOP - Just solid color + cursor
        // Direct framebuffer writes, no Graphics calls that might crash
        // ============================================================
        int frameCount = 0;
        for (; ; ) {
            try {
                // Output a dot every 100 frames to show we're alive
                frameCount++;
                if (frameCount % 100 == 0) {
                    Native.Out8(0x3F8, (byte)'.');
                }
                
                // DIRECT framebuffer access - bypass Graphics class entirely
                // This tests if the framebuffer address is valid
                uint* fb = Framebuffer.VideoMemory;
                int w = Framebuffer.Width;
                int h = Framebuffer.Height;
                
                // Clear screen with solid teal color
                uint tealColor = 0xFF0D7D77;
                for (int i = 0; i < w * h; i++) {
                    fb[i] = tealColor;
                }
                
                // Draw cursor as simple white rectangle
                int cursorX = Control.MousePosition.X;
                int cursorY = Control.MousePosition.Y;
                
                // Clamp cursor position
                if (cursorX < 0) cursorX = 0;
                if (cursorY < 0) cursorY = 0;
                if (cursorX >= w - 4) cursorX = w - 5;
                if (cursorY >= h - 4) cursorY = h - 5;
                
                // Draw 4x4 white square cursor directly to framebuffer
                uint white = 0xFFFFFFFF;
                for (int cy = 0; cy < 4; cy++) {
                    for (int cx = 0; cx < 4; cx++) {
                        int idx = (cursorY + cy) * w + (cursorX + cx);
                        if (idx >= 0 && idx < w * h) {
                            fb[idx] = white;
                        }
                    }
                }
                
                // No Update() call - we write directly to VideoMemory
                // Sleep is optional
                // Thread.Sleep(1);
                
            } catch {
                // Error in render loop
                Native.Out8(0x3F8, (byte)'!');
            }
        }
     }
     
     // Keep RefreshCachedIcons but don't call it in minimal boot
     #if false // MINIMAL_BOOT_DISABLED
     /// <summary>
     /// FIXED: Helper method to refresh cached icons and dispose old ones
     /// CRITICAL: Create new icons BEFORE disposing old ones to prevent Desktop.Update from receiving null/disposed icons
     /// </summary>
     private static void RefreshCachedIcons() {
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
             Console.WriteLine("Icon cache refresh failed - keeping old icons");
         }
     }
     #endif // MINIMAL_BOOT_DISABLED
     
     public static WidgetContainer WidgetsContainer;
}
