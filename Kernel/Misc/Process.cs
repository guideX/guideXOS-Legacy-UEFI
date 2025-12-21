using System;
using guideXOS.Misc;

namespace guideXOS.Misc {
    // Minimal process/address space scaffolding to support ring-3 switching
    public unsafe class AddressSpace {
        public ulong* Pml4;
        public ulong EntryRip;
        public ulong UserRsp;

        public AddressSpace() {
            // Create a fresh PML4 and clone kernel space from current kernel PML4 (upper half or identity range)
            Pml4 = (ulong*)Allocator.Allocate(0x1000);
            Native.Stosb(Pml4, 0, 0x1000);

            // Simple approach: mirror the current kernel mappings by copying entire PML4
            // In a production kernel, only copy kernel slots; this is a placeholder for simplicity
            Native.Movsb(Pml4, PageTable.PML4, 0x1000);
        }
    }
}
