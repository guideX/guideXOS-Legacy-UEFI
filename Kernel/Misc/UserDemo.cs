using System;

namespace guideXOS.Misc {
    internal static unsafe class UserDemo {
        // A stub address to jump into for demo; in real systems this would be a mapped user program image
        public static void LaunchSimpleUserTask() {
            // Allocate user stack and a tiny user code page with an infinite int 0x80 loop (placeholder)
            const ulong Page = 4096;
            ulong userStackBottom = (ulong)Allocator.Allocate(Page * 2);
            // Mark these as user-accessible in the current page table
            PageTable.MapUser(userStackBottom, userStackBottom);
            PageTable.MapUser(userStackBottom + Page, userStackBottom + Page);

            // For demo purposes, reuse an existing kernel function address would be unsafe.
            // Here we just enter user mode at a dummy address; real mapping of user code is required.
            ulong userRip = userStackBottom; // in a real case, point to actual user code mapped with MapUser
            ulong userRsp = userStackBottom + Page * 2 - 16;

            SchedulerExtensions.EnterUserMode(userRip, userRsp);
        }
    }
}
