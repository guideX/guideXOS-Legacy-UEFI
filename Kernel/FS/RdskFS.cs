using guideXOS.Kernel.Drivers;
using guideXOS.Kernel.Helpers;
using guideXOS.Misc;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace guideXOS.FS {
    /// <summary>
    /// RDSK filesystem - custom ramdisk format used by guideXOS
    /// Format:
    ///   Magic: "RDSK" (4 bytes)
    ///   Version: 1 (4 bytes, little-endian)
    ///   FileCount: N (4 bytes, little-endian)
    ///   For each file:
    ///     PathLength: 2 bytes (little-endian)
    ///     Path: UTF-8 string
    ///     DataLength: 4 bytes (little-endian)
    ///     Data: raw bytes
    /// </summary>
    internal unsafe class RdskFS : FileSystem {
        private Ramdisk _ramdisk;

        public RdskFS()
        {
            BootConsole.WriteLine("[RdskFS] Constructor called");
            
            // Get direct ramdisk reference
            _ramdisk = Ramdisk.Instance;
            if (_ramdisk == null) {
                BootConsole.WriteLine("[RdskFS] ERROR: Ramdisk.Instance is null!");
                return;
            }
            
            BootConsole.WriteLine("[RdskFS] Got Ramdisk reference");
            
            // Validate RDSK header
            byte* ptr = _ramdisk.GetRawPointer();
            if (ptr == null) {
                BootConsole.WriteLine("[RdskFS] ERROR: Raw pointer is null!");
                return;
            }
            
            // Check magic
            if (ptr[0] == (byte)'R' && ptr[1] == (byte)'D' && ptr[2] == (byte)'S' && ptr[3] == (byte)'K') {
                BootConsole.WriteLine("[RdskFS] Magic OK");
            } else {
                BootConsole.WriteLine("[RdskFS] WARNING: Invalid magic, may not work");
            }
            
            BootConsole.WriteLine("[RdskFS] Constructor completed");
        }

        public override List<FileInfo> GetFiles(string Directory) {
            return new List<FileInfo>();
        }
        
        public override void Delete(string Name) { }
        
        /// <summary>
        /// Read file by scanning RDSK on-demand (no pre-caching)
        /// </summary>
        public override byte[] ReadAllBytes(string Name) {
            if (Name == null) {
                return null;
            }
            
            // Normalize path (remove leading slash if present)
            string searchPath = Name;
            if (Name.Length > 0 && Name[0] == '/') {
                searchPath = Name.Substring(1);
            }
            
            byte* ptr = _ramdisk.GetRawPointer();
            if (ptr == null) {
                return null;
            }
            
            // Check magic
            if (ptr[0] != (byte)'R' || ptr[1] != (byte)'D' || ptr[2] != (byte)'S' || ptr[3] != (byte)'K') {
                return null;
            }
            
            // Read file count carefully
            uint fileCount = *(uint*)(ptr + 8);
            
            // Safety: limit file count to prevent DoS
            if (fileCount == 0 || fileCount > 10000) {
                return null;
            }
            
            // Scan through files
            ulong offset = 12; // Start after header
            const ulong MAX_RDSK_SIZE = 512 * 1024 * 1024; // 512MB max size safety limit
            
            for (uint i = 0; i < fileCount; i++) {
                // Safety check: don't read past reasonable bounds
                if (offset > MAX_RDSK_SIZE || offset + 2 > MAX_RDSK_SIZE) {
                    break;
                }
                
                // Read path length (2 bytes)
                ushort pathLen = *(ushort*)(ptr + offset);
                offset += 2;
                
                // Validate path length
                if (pathLen == 0 || pathLen > 512) {
                    break;
                }
                
                // Safety: ensure we can read the path
                if (offset + pathLen > MAX_RDSK_SIZE) {
                    break;
                }
                
                // Validate path data before trying to create string
                bool pathValid = true;
                for (ushort j = 0; j < pathLen; j++) {
                    byte b = ptr[offset + j];
                    if (b == 0 || b > 127) {
                        pathValid = false;
                        break;
                    }
                }
                
                if (!pathValid) {
                    break;
                }
                
                // Read path
                string path = null;
                try {
                    path = string.FromASCII((nint)(ptr + offset), pathLen);
                } catch {
                    break;
                }
                offset += pathLen;
                
                // Safety: ensure we can read dataLen
                if (offset + 4 > MAX_RDSK_SIZE) {
                    if (path != null) path.Dispose();
                    break;
                }
                
                // Read data length (4 bytes)
                uint dataLen = *(uint*)(ptr + offset);
                offset += 4;
                
                // Validate data length
                if (dataLen > 100 * 1024 * 1024) {
                    if (path != null) path.Dispose();
                    break;
                }
                
                // Safety: ensure data exists within bounds
                if (offset + dataLen > MAX_RDSK_SIZE) {
                    if (path != null) path.Dispose();
                    break;
                }
                
                // Check if this is the file we want
                if (path != null && (path.Equals(searchPath) || path.Equals(Name))) {
                    // Copy file data
                    byte[] buffer = new byte[dataLen];
                    byte* src = ptr + offset;
                    
                    fixed (byte* dst = buffer) {
                        for (uint j = 0; j < dataLen; j++) {
                            dst[j] = src[j];
                        }
                    }
                    
                    path.Dispose();
                    return buffer;
                }
                
                if (path != null) path.Dispose();
                
                // Skip past data
                offset += dataLen;
            }
            
            return null;
        }
        
        public override void WriteAllBytes(string Name, byte[] Content) { }
        
        public override void Format() { }
    }
}
