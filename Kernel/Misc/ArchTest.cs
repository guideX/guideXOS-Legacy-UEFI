using guideXOS.Misc;
using System;

namespace guideXOS.Kernel {
    /// <summary>
    /// Architecture testing and validation utility
    /// </summary>
    public static unsafe class ArchTest {
        /// <summary>
        /// Run comprehensive architecture tests
        /// </summary>
        public static void Run() {
            Console.WriteLine("================================================");
            Console.WriteLine("   guideXOS Architecture Test Suite            ");
            Console.WriteLine("================================================");
            Console.WriteLine();
            
            TestPointerSize();
            TestCPUFeatures();
            TestPointerArithmetic();
            TestMemoryAddressing();
            TestStructSizes();
            
            Console.WriteLine();
            Console.WriteLine("================================================");
            Console.WriteLine("   All Architecture Tests Complete             ");
            Console.WriteLine("================================================");
        }
        
        /// <summary>
        /// Test 1: Pointer Size
        /// </summary>
        private static void TestPointerSize() {
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("Test 1: Pointer Size");
            Console.WriteLine("------------------------------------------------");
            
            int ptrSize = sizeof(nint);
            int nativeIntSize = sizeof(nuint);
            
            Console.WriteLine($"  sizeof(nint):     {ptrSize} bytes");
            Console.WriteLine($"  sizeof(nuint):    {nativeIntSize} bytes");
            Console.WriteLine($"  sizeof(void*):    {sizeof(void*)} bytes");
            Console.WriteLine($"  Architecture:     {(ptrSize == 8 ? "x64 (AMD64)" : ptrSize == 4 ? "x86 (32-bit)" : "Unknown")}");
            
            if (ptrSize == nativeIntSize && ptrSize == sizeof(void*)) {
                Console.WriteLine("  Status:           PASS - Consistent pointer sizes");
            } else {
                Console.WriteLine("  Status:           FAIL - Inconsistent pointer sizes!");
            }
            Console.WriteLine();
        }
        
        /// <summary>
        /// Test 2: CPU Features
        /// </summary>
        private static void TestCPUFeatures() {
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("Test 2: CPU Features");
            Console.WriteLine("------------------------------------------------");
            
            // COMMENTED OUT: CPUID native functions not yet implemented
            // To enable this test, implement cpuid.cpp and link NativeLib.lib
            Console.WriteLine("  CPUID Support:    SKIPPED (native functions not linked)");
            Console.WriteLine("  Note: Implement cpuid.cpp to enable CPU feature detection");
            
            /*
            bool cpuidSupport = CPUIDHelper.IsCPUIDSupported();
            Console.WriteLine($"  CPUID Support:    {(cpuidSupport ? "YES" : "NO")}");
            
            if (cpuidSupport) {
                // Get CPU vendor
                var vendor = CPUIDHelper.GetVendorString();
                Console.WriteLine($"  CPU Vendor:       {vendor}");
                
                // Check Long Mode (64-bit) - inferred from pointer size
                bool longMode = (sizeof(nint) == 8);
                Console.WriteLine($"  Long Mode (x64):  {(longMode ? "YES" : "NO")}");
                
                // SIMD features
                Console.WriteLine($"  SSE Support:      {(CPUIDHelper.HasSSE() ? "YES" : "NO")}");
                Console.WriteLine($"  SSE2 Support:     {(CPUIDHelper.HasSSE2() ? "YES" : "NO")}");
                Console.WriteLine($"  SSE3 Support:     {(CPUIDHelper.HasSSE3() ? "YES" : "NO")}");
                Console.WriteLine($"  AVX Support:      {(CPUIDHelper.HasAVX() ? "YES" : "NO")}");
                Console.WriteLine($"  APIC Support:     {(CPUIDHelper.HasAPIC() ? "YES" : "NO")}");
                Console.WriteLine($"  PAE Support:      {(CPUIDHelper.HasPAE() ? "YES" : "NO")}");
            }
            */
            
            Console.WriteLine();
        }
        
        /// <summary>
        /// Test 3: Pointer Arithmetic
        /// </summary>
        private static void TestPointerArithmetic() {
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("Test 3: Pointer Arithmetic & Truncation");
            Console.WriteLine("------------------------------------------------");
            
            // Test small address (fits in 32-bit)
            {
                ulong smallAddr = 0x12345678UL;
                void* ptr = (void*)smallAddr;
                ulong recovered = (ulong)ptr;
                bool pass = (recovered == smallAddr);
                Console.WriteLine($"  32-bit address:   0x{smallAddr:X8} -> 0x{recovered:X16} {(pass ? "OK" : "FAIL")}");
            }
            
            // Test large address (requires 64-bit) - only if in 64-bit mode
            if (sizeof(nint) == 8) {
                ulong largeAddr = 0x123456789ABCDEF0UL;
                void* ptr = (void*)largeAddr;
                ulong recovered = (ulong)ptr;
                bool pass = (recovered == largeAddr);
                Console.WriteLine($"  64-bit address:   0x{largeAddr:X16} -> 0x{recovered:X16} {(pass ? "OK" : "FAIL")}");
                
                if (!pass) {
                    Console.WriteLine("  Status:           FAIL - Pointer truncation detected!");
                    Console.WriteLine($"  Lost bits:        0x{(largeAddr & ~recovered):X16}");
                } else {
                    Console.WriteLine("  Status:           PASS - No pointer truncation");
                }
            } else {
                Console.WriteLine("  64-bit address:   SKIPPED (running in 32-bit mode)");
                Console.WriteLine("  Status:           PASS (32-bit mode expected)");
            }
            Console.WriteLine();
        }
        
        /// <summary>
        /// Test 4: Memory Addressing
        /// </summary>
        private static void TestMemoryAddressing() {
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("Test 4: Memory Addressing Capability");
            Console.WriteLine("------------------------------------------------");
            
            ulong allocBase = (ulong)Allocator._Info.Start;
            ulong totalMem = Allocator.MemorySize;
            ulong highestAddr = allocBase + totalMem;
            
            Console.WriteLine($"  Alloc Base:       0x{allocBase:X16}");
            Console.WriteLine($"  Total Memory:     {totalMem / (1024*1024)} MB ({totalMem / (1024*1024*1024)} GB)");
            Console.WriteLine($"  Highest Address:  0x{highestAddr:X16}");
            
            bool canAccess4GB = highestAddr > 0x100000000UL;
            Console.WriteLine($"  Accessing >4GB:   {(canAccess4GB ? "YES" : "NO")}");
            
            if (sizeof(nint) == 8 && !canAccess4GB) {
                Console.WriteLine("  Status:           WARNING - 64-bit mode but not using high memory");
            } else if (sizeof(nint) == 4 && canAccess4GB) {
                Console.WriteLine("  Status:           FAIL - Trying to access >4GB in 32-bit mode!");
            } else {
                Console.WriteLine("  Status:           PASS - Memory addressing consistent");
            }
            Console.WriteLine();
        }
        
        /// <summary>
        /// Test 5: Critical Structure Sizes
        /// </summary>
        private static void TestStructSizes() {
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("Test 5: Critical Structure Sizes");
            Console.WriteLine("------------------------------------------------");
            
            Console.WriteLine($"  sizeof(nint):     {sizeof(nint)} bytes");
            Console.WriteLine($"  sizeof(nuint):    {sizeof(nuint)} bytes");
            Console.WriteLine($"  sizeof(void*):    {sizeof(void*)} bytes");
            Console.WriteLine($"  sizeof(ulong):    {sizeof(ulong)} bytes");
            
            // Note: EHCI struct sizes require Kernel.Drivers namespace
            // These would be tested at runtime when USB subsystem initializes
            
            Console.WriteLine("  Status:           PASS - Structure sizes logged");
            Console.WriteLine();
        }
    }
}
