using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using Internal.Runtime.CompilerHelpers;
using System;
using System.Runtime;
using System.Runtime.InteropServices;
namespace guideXOS.Misc {
/// <summary>
/// Entry Point
/// </summary>
internal static unsafe class EntryPoint {
    // Import the module table accessor from native_stubs.asm
    // This returns the address of the __Module symbol which contains the NativeAOT module table
    [DllImport("*", EntryPoint = "__modules_a")]
    private static extern IntPtr GetModulesPointer();
    
    // Boot debug marker - writes 'K!' to serial and magenta pixel to framebuffer
    // This is pure assembly with NO managed code overhead
    [DllImport("*")]
    private static extern void SerialDebugMarker();
    
    // IMPORTANT: This forces KMainWrapper to be included in the final executable
    // KMainWrapper is the REAL entry point that should be called by the bootloader
    // It writes debug markers before calling KMain
    [DllImport("*")]
    private static extern void KMainWrapper(UefiBootInfo* bootInfo);
    
    // Static field to hold KMainWrapper address - this forces linker to include it
    private static IntPtr _kMainWrapperAddr;
    
    // Helper to force KMainWrapper inclusion - MUST be called from reachable code
    private static void ForceIncludeKMainWrapper() {
        // Take address of KMainWrapper to force linker to include it
        _kMainWrapperAddr = (IntPtr)(delegate*<UefiBootInfo*, void>)&KMainWrapper;
    }
    
    /// <summary>
    /// NEW UEFI entry point called from UEFI bootloader
    /// This is the modern entry point that receives guideXOS::BootInfo
    /// 
    /// NOTE: The UEFI bootloader uses Microsoft x64 ABI (parameter in RCX).
    /// This should match since we're building for Windows x64.
    /// </summary>
    /// <param name="bootInfo">UEFI boot information structure</param>
        [RuntimeExport("KMain")]
        public static void KMain(UefiBootInfo* bootInfo) {
            // Force linker to include KMainWrapper (even though we don't use it here)
            // ForceIncludeKMainWrapper(); // DISABLED - not needed anymore
            
            // === PHASE 0: IMMEDIATE SERIAL DEBUG ===
            // Write serial markers to track execution progress
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'M');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'A');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'I');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'N');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'!');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\r');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\n');
            
            // === PHASE 0.1: FRAMEBUFFER TEST ===
            // Write '[FB]' to serial before framebuffer access
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'[');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'F');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'B');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)']');
            
            // Write directly to framebuffer using raw pointer
            // Framebuffer is identity-mapped at 0x80000000
            uint* fb0 = (uint*)0x80000000;
            
            // Write WHITE pixels at (0,0)
            fb0[0] = 0x00FFFFFF;
            fb0[1] = 0x00FFFFFF;
            fb0[2] = 0x00FFFFFF;
            fb0[3] = 0x00FFFFFF;
            fb0[4] = 0x00FFFFFF;
            
            // Confirm framebuffer write
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'O');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\r');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\n');
            
            // === PHASE 1: VALIDATE BOOT INFO ===
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'[');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'B');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'I');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)']');
            
            if (bootInfo == null || bootInfo->FramebufferBase == 0) {
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'E');
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'R');
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'R');
                for (; ; ) { Native.Hlt(); }
            }
            
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'O');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\r');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\n');
            
            // === PHASE 2: DRAW BOOT MARKER ===
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'[');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'D');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'R');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)']');
            
            uint* fb = (uint*)bootInfo->FramebufferBase;
            uint pitch = bootInfo->FramebufferPitch / 4;
            
            // Draw magenta line at y=100
            for (uint x = 0; x < 200; x++) {
                fb[100 * pitch + x] = 0x00FF00FF;
            }
            
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'O');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\r');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\n');
            
            // === PHASE 3: INITIALIZE ALLOCATOR ===
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'[');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'A');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'L');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)']');
            
            Allocator.Initialize((IntPtr)0x4000000);
            
            // Draw green line
            for (uint x = 0; x < 200; x++) {
                fb[110 * pitch + x] = 0x0000FF00;
            }
            
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'O');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\r');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\n');
            
            // === PHASE 4: INITIALIZE MODULES ===
            // TEMPORARILY DISABLED: Module initialization may access unmapped memory
            // The NativeAOT runtime's InitializeModules walks internal data structures
            // that may have pointers to regions not mapped by the bootloader.
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'[');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'M');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'O');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'D');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)']');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'S');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'I');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'P');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\r');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\n');
            
            // SKIP module init for now - kernel basic features should still work
            // IntPtr modulesPtr = GetModulesPointer();
            // StartupCodeHelpers.InitializeModules(modulesPtr);
            
            // Draw cyan line
            for (uint x = 0; x < 200; x++) {
                fb[120 * pitch + x] = 0x0000FFFF;
            }
            
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'O');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\r');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\n');
            
            // === PHASE 5: CONTINUE INITIALIZATION ===
            // NOTE: For UEFI boot, the bootloader already set up page tables.
            // We skip PageTable.Initialise() to avoid overwriting them and
            // avoid the extremely slow 4GB mapping loop.
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'[');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'P');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'T');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)']');
            
            // SKIP PageTable.Initialise() - bootloader already set up identity mapping
            // PageTable.Initialise();
            
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'S');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'I');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'P');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\r');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\n');
            
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'[');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'A');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'S');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'C');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)']');
            
            ASC16.Initialize();
            
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'O');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\r');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\n');
            
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'[');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'F');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'B');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'I');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)']');

            // Initialize framebuffer wrapper
            if (bootInfo->HasFramebuffer && bootInfo->FramebufferBase != 0) {
                Framebuffer.Initialize(
                    (ushort)bootInfo->FramebufferWidth,
                    (ushort)bootInfo->FramebufferHeight,
                    (uint*)bootInfo->FramebufferBase
                );
                
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'C');
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'L');
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'R');
                
                Framebuffer.Graphics.Clear(0x0); // Clear screen
            }
            
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'O');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\r');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\n');
            
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'[');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'B');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'S');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'P');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)']');

            // Boot splash
            BootSplash.Initialize("Team Nexgen", "guideXOS", "Version: 0.2 UEFI");
            
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'O');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\r');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'\n');
            
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'[');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'C');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'O');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'N');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)']');
            
            Console.Setup();
            Console.WriteLine("[UEFI] Booting from UEFI bootloader");
            Console.WriteLine($"[UEFI] BootInfo Magic: 0x{bootInfo->Magic:X8}");
            Console.WriteLine($"[UEFI] BootInfo Version: {bootInfo->Version}");
            
            DetectArchitecture();

            // Initialize GDT/IDT
            IDT.Disable();
            GDT.Initialise();
            {
                const ulong kStackSize = 64 * 1024;
                ulong rsp0 = (ulong)Allocator.Allocate(kStackSize) + kStackSize;
                GDT.SetKernelStack(rsp0);
            }
            IDT.Initialize();
            IDT.AllowUserSoftwareInterrupt(0x80);
            Interrupts.Initialize();
            IDT.Enable();
            SSE.enable_sse();

            // Initialize ACPI
            if (bootInfo->AcpiRsdp != 0) {
                Console.WriteLine($"[ACPI] RSDP at 0x{bootInfo->AcpiRsdp:X16}");
            }
            ACPI.Initialize();

#if UseAPIC
            PIC.Disable();
            LocalAPIC.Initialize();
            IOAPIC.Initialize();
#else
            PIC.Enable();
#endif

            // Initialize drivers
            Timer.Initialize();
            Keyboard.Initialize();
            Serial.Initialise();
            PS2Controller.Initialize();
            VMwareTools.Initialize();
            SMBIOS.Initialise();
            PCI.Initialise();
            IDE.Initialize();
            SATA.Initialize();
            ThreadPool.Initialize();

            // Handle ramdisk
            if (bootInfo->HasRamdisk && bootInfo->RamdiskBase != 0) {
                Console.WriteLine($"[Initrd] Ramdisk at 0x{bootInfo->RamdiskBase:X16}, size {bootInfo->RamdiskSize} bytes");
                new Ramdisk((IntPtr)bootInfo->RamdiskBase);
            } else {
                Console.WriteLine("[Initrd] WARNING: No ramdisk loaded!");
            }

            new AutoFS();

            for (int i = 0; i < 120; i++) {
                BootSplash.Tick();
            }

            BootSplash.Cleanup();
            guideXOS.DockableWidgets.Uptime.BootTimeTicks = Timer.Ticks;
            guideXOS.OS.SystemMode.DetectMode();

            if (File.Exists("/boot/config.txt")) {
                Disk.Instance = IDE.Ports[0];
                File.Instance = new FAT();
                Console.WriteLine("[BOOT] Mounted /dev/sda2 as root");
            } else {
                Console.WriteLine("[BOOT] Using default filesystem");
            }

            guideXOS.OS.Configuration.Initialize();
            
            // Call main kernel entry
            KernelMain();
        }

        /// <summary>
        /// LEGACY Multiboot entry point (for GRUB2 compatibility)
        /// This is the old entry point that receives MultibootInfo
        /// </summary>
        /// <param name="Info"></param>
        /// <param name="Modules"></param>
        /// <param name="Trampoline"></param>
        [RuntimeExport("Entry")]
        public static void Entry(MultibootInfo* Info, IntPtr Modules, IntPtr Trampoline) {
            Allocator.Initialize((IntPtr)0x20000000);
            StartupCodeHelpers.InitializeModules(Modules);
            PageTable.Initialise();
            ASC16.Initialize();
            VBEInfo* info = (VBEInfo*)Info->VBEInfo;
            if (info->PhysBase != 0) {
                Framebuffer.Initialize(info->ScreenWidth, info->ScreenHeight, (uint*)info->PhysBase);
                Framebuffer.Graphics.Clear(0x0);
            } else {
                for (; ; ) Native.Hlt();
            }
            // Boot splash init
            BootSplash.Initialize("Team Nexgen", "guideXOS", "Version: 0.2");
            Console.Setup();
            
            // Detect and log architecture
            DetectArchitecture();
            
            IDT.Disable();
            GDT.Initialise();
            {
                const ulong kStackSize = 64 * 1024;
                ulong rsp0 = (ulong)Allocator.Allocate(kStackSize) + kStackSize;
                GDT.SetKernelStack(rsp0);
            }
            IDT.Initialize();
            IDT.AllowUserSoftwareInterrupt(0x80);
            Interrupts.Initialize();
            IDT.Enable();
            SSE.enable_sse();
            
            // Enable AVX if supported - COMMENTED OUT: CPUID native functions not yet implemented
            // if (CPUIDHelper.IsCPUIDSupported() && CPUIDHelper.HasAVX()) {
            //     Console.WriteLine("[CPU] AVX supported - enabling");
            //     // AVX.init_avx();  // Uncomment when AVX initialization is implemented
            // }
            
            ACPI.Initialize();
#if UseAPIC
            PIC.Disable();
            LocalAPIC.Initialize();
            IOAPIC.Initialize();
#else
        	PIC.Enable();
#endif
            Timer.Initialize();
            Keyboard.Initialize();
            Serial.Initialise();
            PS2Controller.Initialize();
            VMwareTools.Initialize();
            SMBIOS.Initialise();
            PCI.Initialise();
            IDE.Initialize();
            SATA.Initialize();
            ThreadPool.Initialize();
            Console.WriteLine($"[SMP] Trampoline: 0x{((ulong)Trampoline).ToString("x2")}");
            Native.Movsb((byte*)SMP.Trampoline, (byte*)Trampoline, 512);
            SMP.Initialize((uint)SMP.Trampoline);
            Console.Write("[Initrd] Initrd: 0x");
            Console.WriteLine((Info->Mods[0]).ToString("x2"));
            Console.WriteLine("[Initrd] Initializing Ramdisk");
            new Ramdisk((IntPtr)(Info->Mods[0]));
            // Initialize filesystem: Auto-detect FAT (12/16/32) or TAR
            new AutoFS();
            // While we are still here (single core boot), animate splash a bit
            for (int i = 0; i < 120; i++) { // ~2 seconds at 60Hz
                BootSplash.Tick();
            }

            // Cleanup boot splash resources before transitioning to desktop
            BootSplash.Cleanup();

            // Record boot time for uptime tracking
            guideXOS.DockableWidgets.Uptime.BootTimeTicks = Timer.Ticks;

            // Detect system mode (LiveMode vs Installed)
            guideXOS.OS.SystemMode.DetectMode();

            if (File.Exists("/boot/config.txt")) {
                // Booting from HDD - switch to system partition
                Disk.Instance = IDE.Ports[0]; // Or SATA.Drives[0]
                File.Instance = new FAT();
                Console.WriteLine("[BOOT] Mounted /dev/sda2 as root");
            } else {
                // Booting from USB/CD - use ramdisk
                Console.WriteLine("[BOOT] Using ramdisk");
            }

            // Initialize configuration system (only works when not in LiveMode)
            guideXOS.OS.Configuration.Initialize();

            // Call main kernel entry
            KernelMain();
        }

        /// <summary>
        /// Main kernel initialization (shared by both entry points)
        /// </summary>
        private static void KernelMain() {
            // Call the main OS initialization - this sets up GUI, drivers, etc.
            Program.KMain();
        }

        /// <summary>
        /// Detect and validate system architecture
        /// </summary>
        private static void DetectArchitecture() {
            Console.WriteLine("=== Architecture Detection ===");
            
            // 1. Pointer size check
            int ptrSize = sizeof(nint);
            Console.WriteLine($"[ARCH] Pointer Size: {ptrSize} bytes ({(ptrSize == 8 ? "64-bit" : ptrSize == 4 ? "32-bit" : "Unknown")})");

            // 2. CPUID support check - COMMENTED OUT: Native cpuid functions not yet implemented
            // When you implement cpuid.cpp and link it, uncomment this section:
            /*
            if (CPUIDHelper.IsCPUIDSupported()) {
                Console.WriteLine("[ARCH] CPUID: Supported");
                
                // Print CPU info
                CPUIDHelper.PrintCPUInfo();
                
                // Check for Long Mode (AMD64/x86-64)
                if (CPUIDHelper.GetMaxLeaf() >= 0x80000001) {
                    bool probablyLongMode = (ptrSize == 8);
                    Console.WriteLine($"[ARCH] Long Mode (64-bit): {(probablyLongMode ? "YES" : "NO")}");
                }
            } else {
                Console.WriteLine("[ARCH] CPUID: Not supported");
            }
            */

            // Simplified detection based on pointer size
            if (ptrSize == 8) {
                Console.WriteLine("[ARCH] Running in 64-bit mode (AMD64)");
            } else if (ptrSize == 4) {
                Console.WriteLine("[ARCH] Running in 32-bit mode");
            }

            // 3. Test pointer arithmetic integrity
            TestPointerIntegrity();

            Console.WriteLine("=== Architecture Detection Complete ===");
        }

        /// <summary>
        /// Test that pointer arithmetic works correctly (no truncation)
        /// </summary>
        private static void TestPointerIntegrity() {
            if (sizeof(nint) == 8) {
                // Test 64-bit pointer handling
                ulong testValue = 0x123456789ABCDEF0UL;
                void* testPtr = (void*)testValue;
                ulong recovered = (ulong)testPtr;
                
                if (recovered == testValue) {
                    Console.WriteLine("[ARCH] Pointer integrity: PASS (64-bit pointers working)");
                } else {
                    Panic.Error($"Pointer truncation detected! Expected 0x{testValue:X16}, got 0x{recovered:X16}");
                }
            } else {
                Console.WriteLine("[ARCH] Pointer integrity: SKIP (32-bit mode)");
            }
        }
    }
}