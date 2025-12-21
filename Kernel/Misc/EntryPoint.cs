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
    /// <summary>
    /// NEW UEFI entry point called from UEFI bootloader
    /// This is the modern entry point that receives guideXOS::BootInfo
    /// 
    /// NOTE: The UEFI bootloader uses Microsoft x64 ABI (parameter in RCX).
    /// This should match since we're building for Windows x64.
    /// 
    /// UnmanagedCallersOnly ensures the function has a proper native entry point
    /// without any managed code preamble that might cause issues.
    /// </summary>
    /// <param name="bootInfo">UEFI boot information structure</param>
        [RuntimeExport("KMain")]
        [UnmanagedCallersOnly(EntryPoint = "KMain")]
        public static void KMain(UefiBootInfo* bootInfo) {
            // CRITICAL FIX: The bootloader's stack overlaps with kernel memory!
            // Stack top: 0x3D785000, Kernel start: 0x3D785000
            // This causes stack pushes to overwrite kernel code!
            // 
            // WORKAROUND: Switch to a safe stack region immediately
            // We'll use a region in the framebuffer's vicinity that's mapped
            // The framebuffer is at 0x80000000, we can use 0x7FF00000 for stack (16MB below)
            // But since we can't easily switch stacks in C#, let's try minimal operations
            
            // STEP 0: ABSOLUTE FIRST INSTRUCTION - Draw using hardcoded framebuffer address
            // This proves we entered KMain BEFORE touching ANY parameters
            {
                uint* fb = (uint*)0x80000000UL;
                uint pitch = 1280;
                // Draw BRIGHT PURPLE line at y=50
                for (uint x = 0; x < 800; x++) {
                    *(fb + 50 * pitch + x) = 0x00FF00FF; // PURPLE = KMain entry!
                }
            }
            
            // STEP 1: Draw a visual marker IMMEDIATELY to prove we're in KMain
            // This must happen BEFORE any managed code that might throw
            if (bootInfo != null && bootInfo->FramebufferBase != 0) {
                uint* fb = (uint*)bootInfo->FramebufferBase;
                uint pitch = bootInfo->FramebufferPitch / 4;
                // Draw RED line at y=100 (below bootloader's colored squares)
                for (uint x = 0; x < 400; x++) {
                    fb[100 * pitch + x] = 0x00FF0000; // RED = entered KMain
                }
            }
            
            // STEP 2: Validate boot info magic
            if (bootInfo == null || bootInfo->Magic != 0x49425847) { // 'GXBI'
                // Invalid boot info - draw blue error line and halt
                if (bootInfo != null && bootInfo->FramebufferBase != 0) {
                    uint* fb = (uint*)bootInfo->FramebufferBase;
                    uint pitch = bootInfo->FramebufferPitch / 4;
                    for (uint x = 0; x < 400; x++) {
                        fb[110 * pitch + x] = 0x000000FF; // BLUE = bad magic
                    }
                }
                for (; ; ) Native.Hlt();
            }
            
            // STEP 3: Initialize allocator FIRST (before any allocations)
            // Draw yellow line to show we're initializing allocator
            if (bootInfo->FramebufferBase != 0) {
                uint* fb = (uint*)bootInfo->FramebufferBase;
                uint pitch = bootInfo->FramebufferPitch / 4;
                for (uint x = 0; x < 400; x++) {
                    fb[120 * pitch + x] = 0x00FFFF00; // YELLOW = allocator init
                }
            }
            
            // CRITICAL: The allocator base MUST be in a memory region that is mapped
            // The bootloader maps the kernel at physical addresses starting around 0x3D785000
            // We'll use a region after the kernel for allocations
            // For UEFI boot, use the end of kernel region (kernel is loaded at ~0x3D785000)
            // Calculate allocator base relative to kernel's physical location
            // A safe bet is to use 0x40000000 which should be in mapped conventional memory
            // Or even better, use a region starting after where the kernel ends
            // The kernel is loaded at ~0x3D785000 with size ~0x41C600 bytes
            // So it ends around 0x3DBA1600 - we'll start allocator at 0x3DC00000 (aligned)
            // Actually, let's use a fixed address that we know the bootloader maps: the framebuffer region!
            // No wait - framebuffer is MMIO and can't be used for allocations
            
            // SAFEST approach: Use memory starting after a large offset from kernel load base
            // The kernel mapping should extend far enough if the bootloader maps sufficient pages
            // Let's try 0x4000000 (64MB from start) - this should be within the first 1GB identity mapping
            Allocator.Initialize((IntPtr)0x4000000);
            
            // STEP 4: Draw green line after allocator succeeds
            if (bootInfo->FramebufferBase != 0) {
                uint* fb = (uint*)bootInfo->FramebufferBase;
                uint pitch = bootInfo->FramebufferPitch / 4;
                for (uint x = 0; x < 400; x++) {
                    fb[130 * pitch + x] = 0x0000FF00; // GREEN = allocator OK
                }
            }
            
            // STEP 5: Initialize modules (required for NativeAOT runtime)
            StartupCodeHelpers.InitializeModules((IntPtr)0);
            
            // STEP 6: Draw cyan line after modules initialized
            if (bootInfo->FramebufferBase != 0) {
                uint* fb = (uint*)bootInfo->FramebufferBase;
                uint pitch = bootInfo->FramebufferPitch / 4;
                for (uint x = 0; x < 400; x++) {
                    fb[140 * pitch + x] = 0x0000FFFF; // CYAN = modules OK
                }
            }
            
            // STEP 7: Initialize page tables
            PageTable.Initialise();
            ASC16.Initialize();

            // STEP 8: Initialize framebuffer from UEFI GOP info
            if (bootInfo->HasFramebuffer && bootInfo->FramebufferBase != 0) {
                Framebuffer.Initialize(
                    (ushort)bootInfo->FramebufferWidth,
                    (ushort)bootInfo->FramebufferHeight,
                    (uint*)bootInfo->FramebufferBase
                );
                Framebuffer.Graphics.Clear(0x0);
            } else {
                // No framebuffer - halt
                for (; ; ) Native.Hlt();
            }

            // STEP 9: Boot splash
            BootSplash.Initialize("Team Nexgen", "guideXOS", "Version: 0.2 UEFI");
            Console.Setup();
            Console.WriteLine("[UEFI] Booting from UEFI bootloader");
            Console.WriteLine($"[UEFI] BootInfo Magic: 0x{bootInfo->Magic:X8}");
            Console.WriteLine($"[UEFI] BootInfo Version: {bootInfo->Version}");
            
            // Detect architecture
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

            // Initialize ACPI (use RSDP from boot info if available)
            if (bootInfo->AcpiRsdp != 0) {
                Console.WriteLine($"[ACPI] RSDP at 0x{bootInfo->AcpiRsdp:X16}");
                // TODO: Pass RSDP to ACPI.Initialize() when it supports it
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

            // Handle ramdisk from UEFI
            if (bootInfo->HasRamdisk && bootInfo->RamdiskBase != 0) {
                Console.WriteLine($"[Initrd] Ramdisk at 0x{bootInfo->RamdiskBase:X16}, size {bootInfo->RamdiskSize} bytes");
                new Ramdisk((IntPtr)bootInfo->RamdiskBase);
            } else {
                Console.WriteLine("[Initrd] WARNING: No ramdisk loaded!");
            }

            // Initialize filesystem
            new AutoFS();

            // Animate boot splash
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