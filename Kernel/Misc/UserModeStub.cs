using System.Runtime;

namespace guideXOS.Misc {
    internal static unsafe class UserModeStub {
        // Provide a stub export to satisfy linking when real ASM iret_to_user is unavailable.
        [RuntimeExport("iret_to_user")]
        public static void iret_to_user(ulong rip, ulong cs, ulong rflags, ulong rsp, ulong ss) {
            // No-op. Future: real iretq into ring3.
            BootConsole.WriteLine("[GXM] User mode transition not available in this build.");
        }
    }
}
