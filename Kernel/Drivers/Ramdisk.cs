using guideXOS.FS;
using System;
namespace guideXOS.Kernel.Drivers {
    public unsafe class Ramdisk : Disk {
        byte* ptr;
        
        // WORKAROUND: Static instance for direct access since RTTI is broken in UEFI mode
        public static Ramdisk Instance;

        public Ramdisk(IntPtr _ptr) {
            BootConsole.WriteLine("[Ramdisk] Constructor called");
            
            ptr = (byte*)_ptr;
            
            BootConsole.WriteLine("[Ramdisk] ptr field set");
            
            // Test: Try to read first byte directly to verify memory is accessible
            if (ptr != null) {
                byte testByte = *ptr;
                if (testByte == 0) {
                    BootConsole.WriteLine("[Ramdisk] WARNING: First byte at ramdisk is ZERO");
                } else {
                    BootConsole.WriteLine("[Ramdisk] First byte OK (non-zero)");
                }
            } else {
                BootConsole.WriteLine("[Ramdisk] ERROR: ptr is null!");
            }
            
            // CRITICAL: Set both Instance pointers
            Disk.Instance = this;
            Instance = this; // Direct reference for TarFS
            
            BootConsole.WriteLine("[Ramdisk] Disk.Instance set");
        }

        public override bool Read(ulong sector, uint count, byte* p) {
            // CRITICAL: Log IMMEDIATELY - proves method is called
            Native.Out8(0x3F8, (byte)'R'); // Write 'R' to serial port
            Native.Out8(0x3F8, (byte)'\n');
            
            BootConsole.WriteLine("[Ramdisk] Read called");
            
            if (ptr == null) {
                BootConsole.WriteLine("[Ramdisk] ERROR: ptr is null!");
                return false;
            }
            
            BootConsole.WriteLine("[Ramdisk] About to call Movsb");
            
            // Calculate source address
            byte* src = ptr + (sector * 512);
            ulong srcAddr = (ulong)src;
            
            // Log source address for first sector only
            if (sector == 0) {
                BootConsole.WriteLine("[Ramdisk] Reading from ramdisk base");
            }
            
            Native.Movsb(p, src, 512 * count);
            
            // Verify data was actually copied (check first byte)
            if (sector == 0 && p != null) {
                byte firstByte = *p;
                if (firstByte == 0) {
                    BootConsole.WriteLine("[Ramdisk] WARNING: First byte is zero!");
                } else {
                    BootConsole.WriteLine("[Ramdisk] First byte OK (non-zero)");
                }
            }
            
            BootConsole.WriteLine("[Ramdisk] Movsb completed");
            return true;
        }

        public override bool Write(ulong sector, uint count, byte* p) {
            Native.Movsb(ptr + (sector * 512), p, 512 * count);
            return true;
        }
    }
}