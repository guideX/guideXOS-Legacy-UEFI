// Alternative CPUID implementation using explicit pointers
// Use this if the struct-return version causes crashes

using System.Runtime.InteropServices;

namespace guideXOS.Misc {
    // Note: CPUID struct is already defined in CPUID.cs - we reuse it here
    
    internal static unsafe class CPUIDHelperSafe {
        // Safe version - explicitly pass pointer
        [DllImport("*")]
        private static extern void cpuid_safe(CPUID* result, uint leaf, uint subleaf);
        
        [DllImport("*")]
        private static extern bool cpuid_supported();
        
        private static CPUID CallCPUID(uint leaf, uint subleaf) {
            CPUID result;
            cpuid_safe(&result, leaf, subleaf);
            return result;
        }
        
        public static bool IsCPUIDSupported() {
            return cpuid_supported();
        }
        
        public static bool HasSSE() {
            if (!IsCPUIDSupported()) return false;
            var info = CallCPUID(1, 0);
            return (info.EDX & (1u << 25)) != 0;
        }
        
        public static bool HasSSE2() {
            if (!IsCPUIDSupported()) return false;
            var info = CallCPUID(1, 0);
            return (info.EDX & (1u << 26)) != 0;
        }
        
        public static bool HasSSE3() {
            if (!IsCPUIDSupported()) return false;
            var info = CallCPUID(1, 0);
            return (info.ECX & (1u << 0)) != 0;
        }
        
        public static bool HasAVX() {
            if (!IsCPUIDSupported()) return false;
            var info = CallCPUID(1, 0);
            return (info.ECX & (1u << 28)) != 0;
        }
        
        public static bool HasAPIC() {
            if (!IsCPUIDSupported()) return false;
            var info = CallCPUID(1, 0);
            return (info.EDX & (1u << 9)) != 0;
        }
        
        public static bool HasPAE() {
            if (!IsCPUIDSupported()) return false;
            var info = CallCPUID(1, 0);
            return (info.EDX & (1u << 6)) != 0;
        }
        
        public static bool HasFXSR() {
            if (!IsCPUIDSupported()) return false;
            var info = CallCPUID(1, 0);
            return (info.EDX & (1u << 24)) != 0;
        }
        
        public static string GetVendorString() {
            if (!IsCPUIDSupported()) return "Unknown";
            
            var info = CallCPUID(0, 0);
            
            char[] vendor = new char[12];
            
            // EBX
            vendor[0] = (char)(info.EBX & 0xFF);
            vendor[1] = (char)((info.EBX >> 8) & 0xFF);
            vendor[2] = (char)((info.EBX >> 16) & 0xFF);
            vendor[3] = (char)((info.EBX >> 24) & 0xFF);
            
            // EDX
            vendor[4] = (char)(info.EDX & 0xFF);
            vendor[5] = (char)((info.EDX >> 8) & 0xFF);
            vendor[6] = (char)((info.EDX >> 16) & 0xFF);
            vendor[7] = (char)((info.EDX >> 24) & 0xFF);
            
            // ECX
            vendor[8] = (char)(info.ECX & 0xFF);
            vendor[9] = (char)((info.ECX >> 8) & 0xFF);
            vendor[10] = (char)((info.ECX >> 16) & 0xFF);
            vendor[11] = (char)((info.ECX >> 24) & 0xFF);
            
            return new string(vendor);
        }
        
        public static uint GetMaxLeaf() {
            if (!IsCPUIDSupported()) return 0;
            var info = CallCPUID(0, 0);
            return info.EAX;
        }
        
        public static void PrintCPUInfo() {
            if (!IsCPUIDSupported()) {
                BootConsole.WriteLine("[CPU] CPUID not supported");
                return;
            }
            
            BootConsole.WriteLine($"[CPU] Vendor: {GetVendorString()}");
            BootConsole.WriteLine($"[CPU] Max CPUID Leaf: 0x{GetMaxLeaf():X}");
            BootConsole.Write("[CPU] Features: ");
            
            if (HasSSE()) BootConsole.Write("SSE ");
            if (HasSSE2()) BootConsole.Write("SSE2 ");
            if (HasSSE3()) BootConsole.Write("SSE3 ");
            if (HasAVX()) BootConsole.Write("AVX ");
            if (HasFXSR()) BootConsole.Write("FXSR ");
            if (HasPAE()) BootConsole.Write("PAE ");
            if (HasAPIC()) BootConsole.Write("APIC ");
            
            BootConsole.WriteLine("");
        }
    }
}
