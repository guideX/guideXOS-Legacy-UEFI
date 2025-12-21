using guideXOS.FS;
using guideXOS.Misc;

namespace guideXOS.Kernel.Drivers {
    // Minimal USB Mass Storage handling. We register devices and expose a safe way to create a temporary Disk.
    public static class USBMSC {
        public static void Initialize(USBDevice device) {
            // Class 0x08 = Mass Storage; SubClass 0x06 = SCSI transparent; Protocol 0x50 = Bulk-Only
            USBStorage.Register(device);
        }

        // Try creating a temporary Disk instance for browsing. Returns null on any failure.
        public static USBMSCBot.USBDisk TryOpenDisk(USBDevice dev) {
            return USBMSCBot.TryCreateDisk(dev);
        }
    }
}
