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
            
            // Debug: Print first 4 bytes
            Native.Out8(0x3F8, (byte)'M');
            Native.Out8(0x3F8, (byte)':');
            for (int i = 0; i < 4; i++) {
                byte b = ptr[i];
                char hi = (char)(((b >> 4) & 0xF) < 10 ? '0' + ((b >> 4) & 0xF) : 'A' + (((b >> 4) & 0xF) - 10));
                char lo = (char)((b & 0xF) < 10 ? '0' + (b & 0xF) : 'A' + ((b & 0xF) - 10));
                Native.Out8(0x3F8, (byte)hi);
                Native.Out8(0x3F8, (byte)lo);
            }
            Native.Out8(0x3F8, (byte)'\n');
            
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
            BootConsole.WriteLine("[RdskFS] ReadAllBytes called");
            
            if (Name == null) {
                BootConsole.WriteLine("[RdskFS] ERROR: Name is null!");
                return null;
            }
            
            // Normalize path (remove leading slash if present)
            string searchPath = Name;
            if (Name.Length > 0 && Name[0] == '/') {
                searchPath = Name.Substring(1);
            }
            
            byte* ptr = _ramdisk.GetRawPointer();
            if (ptr == null) {
                BootConsole.WriteLine("[RdskFS] ERROR: Raw pointer is null!");
                return null;
            }
            
            // Check magic
            if (ptr[0] != (byte)'R' || ptr[1] != (byte)'D' || ptr[2] != (byte)'S' || ptr[3] != (byte)'K') {
                BootConsole.WriteLine("[RdskFS] ERROR: Invalid magic!");
                return null;
            }
            
            // Read file count
            uint fileCount = *(uint*)(ptr + 8);
            
            // Scan through files
            ulong offset = 12; // Start after header
            
            for (uint i = 0; i < fileCount; i++) {
                // Read path length (2 bytes)
                ushort pathLen = *(ushort*)(ptr + offset);
                offset += 2;
                
                if (pathLen == 0 || pathLen > 1000) {
                    break; // Invalid entry
                }
                
                // Read path
                string path = string.FromASCII((nint)(ptr + offset), pathLen);
                offset += pathLen;
                
                // Read data length (4 bytes)
                uint dataLen = *(uint*)(ptr + offset);
                offset += 4;
                
                // Check if this is the file we want
                if (path.Equals(searchPath) || path.Equals(Name)) {
                    BootConsole.WriteLine("[RdskFS] FOUND: " + path);
                    
                    // Copy file data
                    byte[] buffer = new byte[dataLen];
                    byte* src = ptr + offset;
                    
                    fixed (byte* dst = buffer) {
                        for (uint j = 0; j < dataLen; j++) {
                            dst[j] = src[j];
                        }
                    }
                    
                    path.Dispose();
                    BootConsole.WriteLine("[RdskFS] Loaded " + dataLen.ToString() + " bytes");
                    return buffer;
                }
                
                path.Dispose();
                
                // Skip past data
                offset += dataLen;
            }
            
            BootConsole.WriteLine("[RdskFS] NOT FOUND: " + searchPath);
            return null;
        }
        
        public override void WriteAllBytes(string Name, byte[] Content) { }
        
        public override void Format() { }
    }
}
