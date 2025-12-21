using System;
using System.Runtime.InteropServices;

namespace guideXOS.Misc {
    public static unsafe class SchedulerExtensions {
        [DllImport("*")]
        private static extern void iret_to_user(ulong rip, ulong cs, ulong rflags, ulong rsp, ulong ss);

        public static void EnterUserMode(ulong rip, ulong rsp) {
            // Prepare a classic iretq transition to ring 3 using our GDT user selectors
            ulong rflags = 0x202; // IF set
            ushort ucs = GDT.UserCodeSelector;
            ushort uds = GDT.UserDataSelector;
            iret_to_user(rip, ucs, rflags, rsp, uds);
        }
    }
}
