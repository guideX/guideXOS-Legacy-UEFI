using guideXOS.FS;
using System;
namespace guideXOS.Kernel.Drivers {
    public unsafe class Ramdisk : Disk {
        byte* ptr;

        public Ramdisk(IntPtr _ptr) {
            BootConsole.WriteLine("[Ramdisk] Constructor called");
            
            ptr = (byte*)_ptr;
            
            BootConsole.WriteLine("[Ramdisk] ptr field set");
            
            // CRITICAL: Only set Instance AFTER ptr is initialized
            // This prevents TarFS from reading before ptr is ready
            Disk.Instance = this;
            
            BootConsole.WriteLine("[Ramdisk] Disk.Instance set");
        }

        public override bool Read(ulong sector, uint count, byte* p) {
            BootConsole.WriteLine("[Ramdisk] Read called");
            
            Native.Movsb(p, ptr + (sector * 512), 512 * count);
            
            BootConsole.WriteLine("[Ramdisk] Movsb completed");
            return true;
        }

        public override bool Write(ulong sector, uint count, byte* p) {
            Native.Movsb(ptr + (sector * 512), p, 512 * count);
            return true;
        }
    }
}