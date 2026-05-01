using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;

namespace guideXOS.Examples {
    /// <summary>
    /// Example code showing how to programmatically mount and use virtual disk images
    /// </summary>
    public static class VirtualDiskExample {
        /// <summary>
        /// Example: Mount FAT32 image, read files, and unmount
        /// </summary>
        public static void MountFAT32ImageExample() {
            Console.WriteLine("[Example] Mounting FAT32 disk image...");
            
            // Save the current disk and filesystem
            var originalDisk = Disk.Instance;
            var originalFS = File.Instance;
            
            try {
                // Create a file-backed virtual disk from the .img file
                var virtualDisk = new FileDisk("/disks/test-fat32.img");
                
                // Set it as the active disk
                Disk.Instance = virtualDisk;
                
                // Mount the FAT filesystem on this disk
                File.Instance = new FAT(virtualDisk);
                
                Console.WriteLine("[Example] Successfully mounted FAT32 image");
                
                // Now you can use the File APIs to work with this virtual disk
                var files = File.GetFiles("/");
                Console.WriteLine($"[Example] Found {files.Count} files in root directory:");
                
                for (int i = 0; i < files.Count && i < 10; i++) {
                    var file = files[i];
                    string type = (file.Attribute & FileAttribute.Directory) != 0 ? "[DIR]" : "[FILE]";
                    Console.WriteLine($"  {type} {file.Name}");
                }
                
                // Example: Read a file (if it exists)
                if (File.Exists("/readme.txt")) {
                    var data = File.ReadAllBytes("/readme.txt");
                    Console.WriteLine($"[Example] Read readme.txt: {data.Length} bytes");
                }
                
                
                // Example: Write a file (if you have byte data)
                // Note: Encoding is not available, so you'd need to manually convert strings to bytes
                // byte[] testBytes = new byte[] { 72, 101, 108, 108, 111 }; // "Hello"
                // File.WriteAllBytes("/test.txt", testBytes);
                // Console.WriteLine("[Example] Wrote test.txt to virtual disk");
                
                // Sync changes back to the image file (optional)
                virtualDisk.Sync();
                Console.WriteLine("[Example] Synced changes to disk image");
                
            } catch {
                Console.WriteLine("[Example] Error occurred during FAT32 mount");
            } finally {
                // Always restore the original disk and filesystem
                Disk.Instance = originalDisk;
                File.Instance = originalFS;
                Console.WriteLine("[Example] Restored original disk");
            }
        }
        
        /// <summary>
        /// Example: Mount EXT4 image and read files
        /// </summary>
        public static void MountEXT4ImageExample() {
            Console.WriteLine("[Example] Mounting EXT4 disk image...");
            
            var originalDisk = Disk.Instance;
            var originalFS = File.Instance;
            
            try {
                var virtualDisk = new FileDisk("/disks/test-ext4.img");
                Disk.Instance = virtualDisk;
                
                // EXT2 driver supports EXT2/EXT3/EXT4 (read-mostly)
                File.Instance = new EXT2(virtualDisk);
                
                Console.WriteLine("[Example] Successfully mounted EXT4 image");
                
                // List files
                var files = File.GetFiles("/");
                Console.WriteLine($"[Example] Found {files.Count} files in root directory:");
                
                for (int i = 0; i < files.Count && i < 10; i++) {
                    var file = files[i];
                    string type = (file.Attribute & FileAttribute.Directory) != 0 ? "[DIR]" : "[FILE]";
                    Console.WriteLine($"  {type} {file.Name}");
                }
                
                // Note: EXT2 driver currently only supports overwriting existing files,
                // not creating new ones or growing files beyond allocated blocks
                
            } catch {
                Console.WriteLine("[Example] Error occurred during EXT4 mount");
            } finally {
                Disk.Instance = originalDisk;
                File.Instance = originalFS;
                Console.WriteLine("[Example] Restored original disk");
            }
        }
        
        /// <summary>
        /// Example: Compare files between two virtual disks
        /// </summary>
        public static void CompareDiskImagesExample() {
            Console.WriteLine("[Example] Comparing two disk images...");
            
            var originalDisk = Disk.Instance;
            var originalFS = File.Instance;
            
            try {
                // Mount first image (FAT32)
                var disk1 = new FileDisk("/disks/test-fat32.img");
                Disk.Instance = disk1;
                File.Instance = new FAT(disk1);
                
                var files1 = File.GetFiles("/");
                Console.WriteLine($"[Example] FAT32 image has {files1.Count} files");
                
                // Mount second image (EXT4)
                var disk2 = new FileDisk("/disks/test-ext4.img");
                Disk.Instance = disk2;
                File.Instance = new EXT2(disk2);
                
                var files2 = File.GetFiles("/");
                Console.WriteLine($"[Example] EXT4 image has {files2.Count} files");
                
                // Compare file lists
                Console.WriteLine("[Example] Files in FAT32 but not in EXT4:");
                for (int i = 0; i < files1.Count; i++) {
                    bool found = false;
                    for (int j = 0; j < files2.Count; j++) {
                        if (files1[i].Name == files2[j].Name) {
                            found = true;
                            break;
                        }
                    }
                    if (!found) {
                        Console.WriteLine($"  - {files1[i].Name}");
                    }
                }
                
            } catch {
                Console.WriteLine("[Example] Error occurred during disk comparison");
            } finally {
                Disk.Instance = originalDisk;
                File.Instance = originalFS;
                Console.WriteLine("[Example] Restored original disk");
            }
        }
    }
}
