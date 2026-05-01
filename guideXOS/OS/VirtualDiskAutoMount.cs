using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Collections.Generic;

namespace guideXOS.OS {
    /// <summary>
    /// Virtual Disk Auto-Mount Manager
    /// Automatically mounts configured disk images at boot time
    /// </summary>
    public static class VirtualDiskAutoMount {
        private const string AutoMountConfigFile = "/etc/guidexos/automount.conf";
        
        /// <summary>
        /// Mounted virtual disks registry
        /// Key: mount point (e.g., "/mnt/fat32", "/mnt/ext4")
        /// Value: FileDisk instance
        /// </summary>
        private static Dictionary<string, MountedVirtualDisk> _mountedDisks = new Dictionary<string, MountedVirtualDisk>();
        
        private class MountedVirtualDisk {
            public string ImagePath;
            public string MountPoint;
            public FileDisk Disk;
            public FileSystem FileSystem;
            public string FsType; // "FAT32" or "EXT4"
        }
        
        /// <summary>
        /// Initialize and auto-mount configured disk images
        /// Call this during OS boot after the ramdisk filesystem is initialized
        /// </summary>
        public static void Initialize() {
            Console.WriteLine("[AutoMount] Initializing virtual disk auto-mount...");
            
            // Try to load configuration
            LoadAutoMountConfiguration();
            
            Console.WriteLine($"[AutoMount] Auto-mount initialization complete ({_mountedDisks.Count} disks mounted)");
        }
        
        /// <summary>
        /// Load and process auto-mount configuration file
        /// </summary>
        private static void LoadAutoMountConfiguration() {
            // Check if configuration file exists
            if (!File.Exists(AutoMountConfigFile)) {
                Console.WriteLine("[AutoMount] No configuration file found, using defaults");
                // Mount default images if they exist
                MountDefaultImages();
                return;
            }
            
            try {
                byte[] data = File.ReadAllBytes(AutoMountConfigFile);
                string content = GetStringFromBytes(data);
                
                // Parse configuration line by line
                // Format: <image_path>:<mount_point>:<fs_type>
                // Example: /disks/test-fat32.img:/mnt/fat32:FAT32
                
                string[] lines = content.Split('\n');
                for (int i = 0; i < lines.Length; i++) {
                    string line = TrimString(lines[i]);
                    
                    // Skip comments and empty lines
                    if (line.Length == 0 || line[0] == '#') continue;
                    
                    string[] parts = line.Split(':');
                    if (parts.Length >= 3) {
                        string imagePath = TrimString(parts[0]);
                        string mountPoint = TrimString(parts[1]);
                        string fsType = TrimString(parts[2]);
                        
                        MountVirtualDisk(imagePath, mountPoint, fsType);
                    }
                }
                
                content.Dispose();
            } catch {
                Console.WriteLine("[AutoMount] Error loading configuration file");
                // Fall back to defaults
                MountDefaultImages();
            }
        }
        
        /// <summary>
        /// Mount default disk images if they exist
        /// </summary>
        private static void MountDefaultImages() {
            // The ramdisk directory is "Disks" with capital D (not "disks")
            // Try both possibilities for robustness
            
            string[] possiblePathsFat32 = new string[] {
                "/Disks/test-fat32.img",  // Correct case - capital D
                "/disks/test-fat32.img",  // lowercase (just in case)
                "/ddDisks/test-fat32.img" // with dd prefix (some filesystems add this)
            };
            
            bool fat32Mounted = false;
            Console.WriteLine("[AutoMount] Checking for FAT32 test image...");
            
            for (int i = 0; i < possiblePathsFat32.Length; i++) {
                if (File.Exists(possiblePathsFat32[i])) {
                    Console.WriteLine("[AutoMount] Found " + possiblePathsFat32[i] + ", auto-mounting...");
                    if (MountVirtualDisk(possiblePathsFat32[i], "/mnt/fat32", "FAT32")) {
                        fat32Mounted = true;
                        break;
                    }
                }
            }
            
            if (!fat32Mounted) {
                Console.WriteLine("[AutoMount] test-fat32.img not found (tried /Disks/, /disks/, /ddDisks/)");
            }
            
            string[] possiblePathsExt4 = new string[] {
                "/Disks/test-ext4.img",   // Correct case - capital D
                "/disks/test-ext4.img",   // lowercase (just in case)
                "/ddDisks/test-ext4.img"  // with dd prefix (some filesystems add this)
            };
            
            bool ext4Mounted = false;
            Console.WriteLine("[AutoMount] Checking for EXT4 test image...");
            
            for (int i = 0; i < possiblePathsExt4.Length; i++) {
                if (File.Exists(possiblePathsExt4[i])) {
                    Console.WriteLine("[AutoMount] Found " + possiblePathsExt4[i] + ", auto-mounting...");
                    if (MountVirtualDisk(possiblePathsExt4[i], "/mnt/ext4", "EXT4")) {
                        ext4Mounted = true;
                        break;
                    }
                }
            }
            
            if (!ext4Mounted) {
                Console.WriteLine("[AutoMount] test-ext4.img not found (tried /Disks/, /disks/, /ddDisks/)");
            }
            
            if (!fat32Mounted && !ext4Mounted) {
                Console.WriteLine("[AutoMount] Tip: Check 'ls /' to see available directories");
                Console.WriteLine("[AutoMount] Or use 'vfsmount' command to manually mount images");
            }
        }
        
        /// <summary>
        /// Mount a virtual disk image
        /// </summary>
        /// <param name="imagePath">Path to the .img file</param>
        /// <param name="mountPoint">Virtual mount point identifier</param>
        /// <param name="fsType">Filesystem type: "FAT32" or "EXT4"</param>
        /// <returns>True if mounted successfully</returns>
        public static bool MountVirtualDisk(string imagePath, string mountPoint, string fsType) {
            try {
                // Check if already mounted
                if (_mountedDisks.ContainsKey(mountPoint)) {
                    Console.WriteLine($"[AutoMount] {mountPoint} already mounted");
                    return false;
                }
                
                // Create virtual disk
                var fileDisk = new FileDisk(imagePath);
                
                // Create filesystem based on type
                FileSystem fs = null;
                if (fsType == "FAT32" || fsType == "FAT") {
                    fs = new FAT(fileDisk);
                } else if (fsType == "EXT4" || fsType == "EXT2" || fsType == "EXT3") {
                    fs = new EXT2(fileDisk);
                } else {
                    Console.WriteLine($"[AutoMount] Unknown filesystem type: {fsType}");
                    return false;
                }
                
                // Register mounted disk
                var mountedDisk = new MountedVirtualDisk {
                    ImagePath = imagePath,
                    MountPoint = mountPoint,
                    Disk = fileDisk,
                    FileSystem = fs,
                    FsType = fsType
                };
                
                _mountedDisks.Add(mountPoint, mountedDisk);
                
                Console.WriteLine($"[AutoMount] Mounted {imagePath} at {mountPoint} as {fsType}");
                return true;
                
            } catch {
                Console.WriteLine($"[AutoMount] Failed to mount {imagePath}");
                return false;
            }
        }
        
        /// <summary>
        /// Unmount a virtual disk
        /// </summary>
        public static bool UnmountVirtualDisk(string mountPoint) {
            if (!_mountedDisks.ContainsKey(mountPoint)) {
                Console.WriteLine($"[AutoMount] {mountPoint} is not mounted");
                return false;
            }
            
            try {
                var mounted = _mountedDisks[mountPoint];
                
                // Sync changes before unmounting
                mounted.Disk.Sync();
                
                _mountedDisks.Remove(mountPoint);
                
                Console.WriteLine($"[AutoMount] Unmounted {mountPoint}");
                return true;
                
            } catch {
                Console.WriteLine($"[AutoMount] Error unmounting {mountPoint}");
                return false;
            }
        }
        
        /// <summary>
        /// Switch to a mounted virtual disk
        /// </summary>
        public static bool SwitchToVirtualDisk(string mountPoint) {
            if (!_mountedDisks.ContainsKey(mountPoint)) {
                Console.WriteLine($"[AutoMount] {mountPoint} is not mounted");
                return false;
            }
            
            var mounted = _mountedDisks[mountPoint];
            
            Disk.Instance = mounted.Disk;
            File.Instance = mounted.FileSystem;
            
            Console.WriteLine($"[AutoMount] Switched to {mountPoint} ({mounted.FsType})");
            return true;
        }
        
        /// <summary>
        /// Get information about a mounted virtual disk
        /// </summary>
        public static bool GetMountInfo(string mountPoint, out string imagePath, out string fsType, out ulong sizeBytes) {
            imagePath = null;
            fsType = null;
            sizeBytes = 0;
            
            if (!_mountedDisks.ContainsKey(mountPoint)) {
                return false;
            }
            
            var mounted = _mountedDisks[mountPoint];
            imagePath = mounted.ImagePath;
            fsType = mounted.FsType;
            sizeBytes = mounted.Disk.Size;
            
            return true;
        }
        
        /// <summary>
        /// Get list of all mounted virtual disks
        /// </summary>
        public static List<string> GetMountedDisks() {
            var list = new List<string>(_mountedDisks.Count);
            
            var keys = _mountedDisks.Keys;
            for (int i = 0; i < keys.Count; i++) {
                list.Add(keys[i]);
            }
            
            return list;
        }
        
        /// <summary>
        /// Sync all mounted virtual disks
        /// Call this before shutdown to ensure all changes are saved
        /// </summary>
        public static void SyncAll() {
            Console.WriteLine($"[AutoMount] Syncing {_mountedDisks.Count} virtual disks...");
            
            var keys = _mountedDisks.Keys;
            for (int i = 0; i < keys.Count; i++) {
                string key = keys[i];
                try {
                    _mountedDisks[key].Disk.Sync();
                    Console.WriteLine($"[AutoMount] Synced {key}");
                } catch {
                    Console.WriteLine($"[AutoMount] Error syncing {key}");
                }
            }
            
            Console.WriteLine("[AutoMount] Sync complete");
        }
        
        /// <summary>
        /// Create a default auto-mount configuration file
        /// </summary>
        public static void CreateDefaultConfig() {
            string config = "# Virtual Disk Auto-Mount Configuration\n";
            config += "# Format: <image_path>:<mount_point>:<fs_type>\n";
            config += "# \n";
            config += "# Example entries (note: use capital D in /Disks/):\n";
            config += "# /Disks/test-fat32.img:/mnt/fat32:FAT32\n";
            config += "# /Disks/test-ext4.img:/mnt/ext4:EXT4\n";
            config += "# /Disks/data.img:/mnt/data:FAT32\n";
            config += "\n";
            config += "# Default auto-mount entries:\n";
            config += "/Disks/test-fat32.img:/mnt/fat32:FAT32\n";
            config += "/Disks/test-ext4.img:/mnt/ext4:EXT4\n";
            
            try {
                byte[] data = GetBytesFromString(config);
                File.WriteAllBytes(AutoMountConfigFile, data);
                Console.WriteLine("[AutoMount] Created default configuration file");
            } catch {
                Console.WriteLine("[AutoMount] Failed to create configuration file");
            }
        }
        
        // Helper methods for string conversion (no System.Text.Encoding available)
        private static string GetStringFromBytes(byte[] bytes) {
            string result = "";
            for (int i = 0; i < bytes.Length; i++) {
                result += (char)bytes[i];
            }
            return result;
        }
        
        private static byte[] GetBytesFromString(string str) {
            byte[] bytes = new byte[str.Length];
            for (int i = 0; i < str.Length; i++) {
                bytes[i] = (byte)str[i];
            }
            return bytes;
        }
        
        // Helper to trim whitespace from string
        private static string TrimString(string str) {
            if (str.Length == 0) return str;
            
            int start = 0;
            int end = str.Length - 1;
            
            // Trim from start
            while (start < str.Length && (str[start] == ' ' || str[start] == '\t' || str[start] == '\r' || str[start] == '\n')) {
                start++;
            }
            
            // Trim from end
            while (end >= start && (str[end] == ' ' || str[end] == '\t' || str[end] == '\r' || str[end] == '\n')) {
                end--;
            }
            
            if (start > end) return "";
            return str.Substring(start, end - start + 1);
        }
    }
}
