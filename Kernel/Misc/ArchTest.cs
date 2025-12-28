namespace guideXOS.Kernel {
    /// <summary>
    /// Architecture testing and validation utility
    /// </summary>
    public static unsafe class ArchTest {
        /// <summary>
        /// Run comprehensive architecture tests
        /// </summary>
        public static void Run() {
           
            BootConsole.WriteLine("================================================");
            BootConsole.WriteLine("   guideXOS Architecture Test Suite            ");
            BootConsole.WriteLine("================================================");
            BootConsole.WriteLine(" ");
            
            TestPointerSize();
            TestCPUFeatures();
            TestPointerArithmetic();
            TestMemoryAddressing();
            TestStructSizes();
            
            BootConsole.WriteLine(" ");
            BootConsole.WriteLine("================================================");
            BootConsole.WriteLine("   All Architecture Tests Complete             ");
            BootConsole.WriteLine("================================================");
        }
        
        /// <summary>
        /// Test 1: Pointer Size
        /// </summary>
        private static void TestPointerSize() {
            BootConsole.WriteLine("------------------------------------------------");
            BootConsole.WriteLine("Test 1: Pointer Size");
            BootConsole.WriteLine("------------------------------------------------");
            
            int ptrSize = sizeof(nint);
            int nativeIntSize = sizeof(nuint);
            
            BootConsole.WriteLine($"  sizeof(nint):     {ptrSize} bytes");
            BootConsole.WriteLine($"  sizeof(nuint):    {nativeIntSize} bytes");
            BootConsole.WriteLine($"  sizeof(void*):    {sizeof(void*)} bytes");
            BootConsole.WriteLine($"  Architecture:     {(ptrSize == 8 ? "x64 (AMD64)" : ptrSize == 4 ? "x86 (32-bit)" : "Unknown")}");
            
            if (ptrSize == nativeIntSize && ptrSize == sizeof(void*)) {
                BootConsole.WriteLine("  Status:           PASS - Consistent pointer sizes");
            } else {
                BootConsole.WriteLine("  Status:           FAIL - Inconsistent pointer sizes!");
            }
            BootConsole.WriteLine(" ");
        }
        
        /// <summary>
        /// Test 2: CPU Features
        /// </summary>
        private static void TestCPUFeatures() {
            BootConsole.WriteLine("------------------------------------------------");
            BootConsole.WriteLine("Test 2: CPU Features");
            BootConsole.WriteLine("------------------------------------------------");
            
            // COMMENTED OUT: CPUID native functions not yet implemented
            // To enable this test, implement cpuid.cpp and link NativeLib.lib
            BootConsole.WriteLine("  CPUID Support:    SKIPPED (native functions not linked)");
            BootConsole.WriteLine("  Note: Implement cpuid.cpp to enable CPU feature detection");
            
            /*
            bool cpuidSupport = CPUIDHelper.IsCPUIDSupported();
            BootConsole.WriteLine($"  CPUID Support:    {(cpuidSupport ? "YES" : "NO")}");
            
            if (cpuidSupport) {
                // Get CPU vendor
                var vendor = CPUIDHelper.GetVendorString();
                BootConsole.WriteLine($"  CPU Vendor:       {vendor}");
                
                // Check Long Mode (64-bit) - inferred from pointer size
                bool longMode = (sizeof(nint) == 8);
                BootConsole.WriteLine($"  Long Mode (x64):  {(longMode ? "YES" : "NO")}");
                
                // SIMD features
                BootConsole.WriteLine($"  SSE Support:      {(CPUIDHelper.HasSSE() ? "YES" : "NO")}");
                BootConsole.WriteLine($"  SSE2 Support:     {(CPUIDHelper.HasSSE2() ? "YES" : "NO")}");
                BootConsole.WriteLine($"  SSE3 Support:     {(CPUIDHelper.HasSSE3() ? "YES" : "NO")}");
                BootConsole.WriteLine($"  AVX Support:      {(CPUIDHelper.HasAVX() ? "YES" : "NO")}");
                BootConsole.WriteLine($"  APIC Support:     {(CPUIDHelper.HasAPIC() ? "YES" : "NO")}");
                BootConsole.WriteLine($"  PAE Support:      {(CPUIDHelper.HasPAE() ? "YES" : "NO")}");
            }
            */
            
            BootConsole.WriteLine(" ");
        }
        
        /// <summary>
        /// Test 3: Pointer Arithmetic
        /// </summary>
        private static void TestPointerArithmetic() {
            BootConsole.WriteLine("------------------------------------------------");
            BootConsole.WriteLine("Test 3: Pointer Arithmetic & Truncation");
            BootConsole.WriteLine("------------------------------------------------");
            
            // Test small address (fits in 32-bit)
            {
                ulong smallAddr = 0x12345678UL;
                void* ptr = (void*)smallAddr;
                ulong recovered = (ulong)ptr;
                bool pass = (recovered == smallAddr);
                BootConsole.WriteLine($"  32-bit address:   0x{smallAddr:X8} -> 0x{recovered:X16} {(pass ? "OK" : "FAIL")}");
            }
            
            // Test large address (requires 64-bit) - only if in 64-bit mode
            if (sizeof(nint) == 8) {
                ulong largeAddr = 0x123456789ABCDEF0UL;
                void* ptr = (void*)largeAddr;
                ulong recovered = (ulong)ptr;
                bool pass = (recovered == largeAddr);
                BootConsole.WriteLine($"  64-bit address:   0x{largeAddr:X16} -> 0x{recovered:X16} {(pass ? "OK" : "FAIL")}");
                
                if (!pass) {
                    BootConsole.WriteLine("  Status:           FAIL - Pointer truncation detected!");
                    BootConsole.WriteLine($"  Lost bits:        0x{(largeAddr & ~recovered):X16}");
                } else {
                    BootConsole.WriteLine("  Status:           PASS - No pointer truncation");
                }
            } else {
                BootConsole.WriteLine("  64-bit address:   SKIPPED (running in 32-bit mode)");
                BootConsole.WriteLine("  Status:           PASS (32-bit mode expected)");
            }
            BootConsole.WriteLine(" ");
        }
        
        /// <summary>
        /// Test 4: Memory Addressing
        /// </summary>
        private static void TestMemoryAddressing() {
            BootConsole.WriteLine("------------------------------------------------");
            BootConsole.WriteLine("Test 4: Memory Addressing Capability");
            BootConsole.WriteLine("------------------------------------------------");
            
            ulong allocBase = (ulong)Allocator._Info.Start;
            ulong totalMem = Allocator.MemorySize;
            ulong highestAddr = allocBase + totalMem;
            
            BootConsole.WriteLine($"  Alloc Base:       0x{allocBase:X16}");
            BootConsole.WriteLine($"  Total Memory:     {totalMem / (1024*1024)} MB ({totalMem / (1024*1024*1024)} GB)");
            BootConsole.WriteLine($"  Highest Address:  0x{highestAddr:X16}");
            
            bool canAccess4GB = highestAddr > 0x100000000UL;
            BootConsole.WriteLine($"  Accessing >4GB:   {(canAccess4GB ? "YES" : "NO")}");
            
            if (sizeof(nint) == 8 && !canAccess4GB) {
                BootConsole.WriteLine("  Status:           WARNING - 64-bit mode but not using high memory");
            } else if (sizeof(nint) == 4 && canAccess4GB) {
                BootConsole.WriteLine("  Status:           FAIL - Trying to access >4GB in 32-bit mode!");
            } else {
                BootConsole.WriteLine("  Status:           PASS - Memory addressing consistent");
            }
            BootConsole.WriteLine(" ");
        }
        
        /// <summary>
        /// Test 5: Critical Structure Sizes
        /// </summary>
        private static void TestStructSizes() {
            BootConsole.WriteLine("------------------------------------------------");
            BootConsole.WriteLine("Test 5: Critical Structure Sizes");
            BootConsole.WriteLine("------------------------------------------------");
            
            BootConsole.WriteLine($"  sizeof(nint):     {sizeof(nint)} bytes");
            BootConsole.WriteLine($"  sizeof(nuint):    {sizeof(nuint)} bytes");
            BootConsole.WriteLine($"  sizeof(void*):    {sizeof(void*)} bytes");
            BootConsole.WriteLine($"  sizeof(ulong):    {sizeof(ulong)} bytes");
            
            // Note: EHCI struct sizes require Kernel.Drivers namespace
            // These would be tested at runtime when USB subsystem initializes
            
            BootConsole.WriteLine("  Status:           PASS - Structure sizes logged");
            BootConsole.WriteLine(" ");
        }
    }
}
