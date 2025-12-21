using guideXOS.Misc;

namespace guideXOS {
    public static unsafe class PageTable {
        public enum PageSize {
            Typical = 4096,
            //Huge = 0x200000
        }

        public static ulong* PML4;

        internal static void Initialise() {
            PML4 = (ulong*)SMP.SharedPageTable;

            Native.Stosb(PML4, 0, 0x1000);

            ulong i = 0;
            //Map the first 4KiB-4GiB
            //Reserve 4KiB for null reference exception
            for (i = (ulong)PageSize.Typical; i < 1024 * 1024 * 1024 * 4UL; i += (ulong)PageSize.Typical) {
                Map(i, i, PageSize.Typical);
            }

            Native.WriteCR3((ulong)PML4);
        }

        // Original API: kernel supervisor mappings
        public static ulong* GetPage(ulong VirtualAddress, PageSize pageSize = PageSize.Typical) {
            return GetPageInternal(PML4, VirtualAddress, user: false, pageSize);
        }

        // New: user-accessible page path
        public static ulong* GetPageUser(ulong VirtualAddress, PageSize pageSize = PageSize.Typical) {
            return GetPageInternal(PML4, VirtualAddress, user: true, pageSize);
        }

        // Root-aware variants
        public static ulong* GetPageOnRoot(ulong* rootPml4, ulong VirtualAddress, bool user, PageSize pageSize = PageSize.Typical) {
            return GetPageInternal(rootPml4, VirtualAddress, user, pageSize);
        }

        private static ulong* GetPageInternal(ulong* rootPml4, ulong VirtualAddress, bool user, PageSize pageSize) {
            if ((VirtualAddress % (ulong)PageSize.Typical) != 0) Panic.Error("Invalid address");

            ulong pml4_entry = (VirtualAddress & ((ulong)0x1ff << 39)) >> 39;
            ulong pml3_entry = (VirtualAddress & ((ulong)0x1ff << 30)) >> 30;
            ulong pml2_entry = (VirtualAddress & ((ulong)0x1ff << 21)) >> 21;
            ulong pml1_entry = (VirtualAddress & ((ulong)0x1ff << 12)) >> 12;

            ulong* pml3 = Next(rootPml4, pml4_entry, user);
            ulong* pml2 = Next(pml3, pml3_entry, user);

            /*
            if (pageSize == PageSize.Huge)
            {
                return &pml2[pml2_entry];
            }
            else 
            */
            if (pageSize == PageSize.Typical) {
                ulong* pml1 = Next(pml2, pml2_entry, user);
                return &pml1[pml1_entry];
            }
            return null;
        }

        /// <summary>
        /// Map Physical Address At Virtual Address Specificed (kernel root)
        /// </summary>
        public static void Map(ulong VirtualAddress, ulong PhysicalAddress, PageSize pageSize = PageSize.Typical) {
            MapOnRoot(PML4, VirtualAddress, PhysicalAddress, user: false, pageSize);
        }

        public static void MapUser(ulong VirtualAddress, ulong PhysicalAddress, PageSize pageSize = PageSize.Typical) {
            MapOnRoot(PML4, VirtualAddress, PhysicalAddress, user: true, pageSize);
        }

        // Root-aware mapping
        public static void MapOnRoot(ulong* rootPml4, ulong VirtualAddress, ulong PhysicalAddress, bool user, PageSize pageSize = PageSize.Typical) {
            /*
            if (pageSize == PageSize.Huge)
            {
                *GetPage(VirtualAddress, pageSize) = PhysicalAddress | 0b10000011;
            }
            else 
            */
            if (pageSize == PageSize.Typical) {
                ulong* pte = GetPageInternal(rootPml4, VirtualAddress, user, pageSize);
                // present | rw | user(optional)
                ulong flags = 0b11UL | (user ? 0b100UL : 0);
                *pte = (PhysicalAddress & 0x000F_FFFF_FFFF_F000UL) | flags;
            }

            Native.Invlpg(PhysicalAddress);
        }

        public static ulong* Next(ulong* Directory, ulong Entry) {
            return Next(Directory, Entry, user: false);
        }

        private static ulong* Next(ulong* Directory, ulong Entry, bool user) {
            ulong* p = null;

            if (((Directory[Entry]) & 0x01) != 0) {
                p = (ulong*)(Directory[Entry] & 0x000F_FFFF_FFFF_F000);
                if (user) Directory[Entry] |= 0b100UL; // Ensure user bit if requested
            } else {
                p = (ulong*)Allocator.Allocate(0x1000);
                Native.Stosb(p, 0, 0x1000);

                // present | rw | user(optional)
                Directory[Entry] = (((ulong)p) & 0x000F_FFFF_FFFF_F000) | 0b11UL | (user ? 0b100UL : 0);
            }

            return p;
        }
    }
}