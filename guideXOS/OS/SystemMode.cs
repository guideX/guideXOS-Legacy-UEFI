using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using System;

namespace guideXOS.OS {
    /// <summary>
    /// System Mode Detection - determines if OS is running in LiveMode or Installed Mode
    /// LiveMode: Running from USB/CD/Read-only media - settings cannot be saved
    /// Installed: Running from hard drive - settings are persisted
    /// </summary>
    internal static class SystemMode {
        private static bool _isLiveMode = true;
        private static bool _detected = false;
        private static string _bootDevice = "ramdisk";
        
        /// <summary>
        /// True if running from USB/CD/read-only media (no persistent storage)
        /// False if installed to hard drive (can save settings)
        /// </summary>
        public static bool IsLiveMode {
            get {
                if (!_detected) {
                    DetectMode();
                }
                return _isLiveMode;
            }
        }
        
        /// <summary>
        /// Boot device identifier (ramdisk, /dev/sda2, etc.)
        /// </summary>
        public static string BootDevice => _bootDevice;
        
        /// <summary>
        /// Detect if system is running in LiveMode or Installed mode
        /// Called automatically on first access to IsLiveMode
        /// </summary>
        public static void DetectMode() {
            if (_detected) return;
            
            try {
                // Check for boot configuration file
                if (File.Exists("/boot/config.txt")) {
                    // Boot config exists - system is installed
                    _isLiveMode = false;
                    _bootDevice = "/dev/sda2"; // default system partition
                    BootConsole.WriteLine($"[BOOT] System Mode: Installed ({_bootDevice})");
                } else {
                    // No boot config - running from ramdisk (LiveMode)
                    _isLiveMode = true;
                    _bootDevice = "ramdisk";
                    BootConsole.WriteLine("[BOOT] System Mode: LiveMode (USB/CD)");
                }
            } catch {
                // On any error, assume LiveMode (safer default)
                _isLiveMode = true;
                _bootDevice = "ramdisk";
                BootConsole.WriteLine("[BOOT] System Mode: LiveMode (detection failed)");
            }
            
            _detected = true;
        }
        
        /// <summary>
        /// Force LiveMode setting (for testing)
        /// </summary>
        public static void ForceLiveMode(bool liveMode) {
            _isLiveMode = liveMode;
            _detected = true;
            if (liveMode) {
                _bootDevice = "ramdisk";
                BootConsole.WriteLine("[BOOT] System Mode: Forced LiveMode");
            } else {
                BootConsole.WriteLine("[BOOT] System Mode: Forced Installed");
            }
        }
        
        /// <summary>
        /// Check if writable storage is available
        /// </summary>
        public static bool CanWriteSettings() {
            if (IsLiveMode) return false;
            
            try {
                // Try to write a test file
                string testPath = "/etc/.write_test";
                byte[] testData = new byte[] { 0x54, 0x45, 0x53, 0x54 }; // "TEST"
                File.WriteAllBytes(testPath, testData);
                testData.Dispose();
                
                // Try to read it back
                if (File.Exists(testPath)) {
                    byte[] readBack = File.ReadAllBytes(testPath);
                    bool success = readBack != null && readBack.Length == 4;
                    readBack?.Dispose();
                    return success;
                }
            } catch {
                // Write failed - no writable storage
            }
            
            return false;
        }
    }
}
