using System.Runtime.InteropServices;

namespace System.Reflection.PortableExecutable {
    // IMAGE_IMPORT_DESCRIPTOR (32-bit RVAs even for PE32+)
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct ImportDescriptor {
        public uint OriginalFirstThunk; // RVA to IMAGE_THUNK_DATA (INT)
        public uint TimeDateStamp;
        public uint ForwarderChain;
        public uint Name;              // RVA to ASCII string
        public uint FirstThunk;        // RVA to IAT (array of IMAGE_THUNK_DATA)
    }

    // IMAGE_THUNK_DATA64
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct ThunkData64 {
        public ulong Value; // Can be ordinal flag or RVA to IMAGE_IMPORT_BY_NAME
    }

    // IMAGE_IMPORT_BY_NAME
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct ImportByName {
        public ushort Hint;
        public fixed byte Name[1]; // null-terminated ANSI string
    }
}
