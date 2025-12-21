using System.Runtime.InteropServices;

namespace guideXOS.Misc {
    /// <summary>
    /// UEFI Boot Information structure - matches C++ guideXOS::BootInfo
    /// This is the modern boot information format passed by the UEFI bootloader
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct UefiBootInfo {
        // Header (32 bytes)
        public uint Magic;              // 'GXBI' (0x49425847)
        public ushort Version;          // Version 1
        public ushort Size;             // sizeof(UefiBootInfo)
        public uint Flags;              // Bit flags for valid fields
        public uint HeaderChecksum;     // 32-bit checksum
        public uint Reserved0;
        public BootMode BootMode;       // UEFI boot mode
        public uint Reserved1;

        // Memory Map (24 bytes)
        public ulong MemoryMap;                // Pointer to EFI_MEMORY_DESCRIPTOR array
        public ulong MemoryMapEntryCount;      // Number of entries
        public ulong MemoryMapDescriptorSize;  // Size of each descriptor

        // Framebuffer (40 bytes)
        public ulong FramebufferBase;          // Physical address
        public ulong FramebufferSize;          // Size in bytes
        public uint FramebufferWidth;          // Width in pixels
        public uint FramebufferHeight;         // Height in pixels
        public uint FramebufferPitch;          // Bytes per scanline
        public FramebufferFormat FramebufferFormat; // Pixel format
        public uint Reserved2;

        // ACPI (8 bytes)
        public ulong AcpiRsdp;                 // ACPI RSDP pointer

        // Command Line (8 bytes)
        public ulong CommandLine;              // Kernel command line (optional)

        // Ramdisk (16 bytes)
        public ulong RamdiskBase;              // Ramdisk physical address
        public ulong RamdiskSize;              // Ramdisk size in bytes

        // Reserved for future use (48 bytes)
        public fixed ulong Reserved[6];

        /// <summary>
        /// Check if memory map is valid
        /// </summary>
        public bool HasMemoryMap => (Flags & 0x1) != 0;

        /// <summary>
        /// Check if framebuffer is valid
        /// </summary>
        public bool HasFramebuffer => (Flags & 0x2) != 0;

        /// <summary>
        /// Check if ramdisk is valid
        /// </summary>
        public bool HasRamdisk => (Flags & 0x4) != 0;

        /// <summary>
        /// Convert to VBEInfo for legacy compatibility
        /// </summary>
        public VBEInfo ToVBEInfo() {
            return new VBEInfo {
                ScreenWidth = (ushort)FramebufferWidth,
                ScreenHeight = (ushort)FramebufferHeight,
                PhysBase = (uint)FramebufferBase,
                Pitch = (ushort)FramebufferPitch,
                BitsPerPixel = 32, // Assume 32-bit
                MemoryModel = 6,   // Direct color
            };
        }
    }

    /// <summary>
    /// Boot mode enumeration
    /// </summary>
    public enum BootMode : uint {
        Uefi = 1,  // UEFI boot
        // Bios = 2,  // Reserved for future BIOS support
    }

    /// <summary>
    /// Framebuffer pixel format
    /// </summary>
    public enum FramebufferFormat : uint {
        Unknown = 0,
        R8G8B8A8 = 1,  // RGBA
        B8G8R8A8 = 2,  // BGRA (most common)
    }
}
