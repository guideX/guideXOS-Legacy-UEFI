using guideXOS.Kernel.Helpers;
using guideXOS.Misc;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace guideXOS.FS {
    /// <summary>
    /// Tar FS with directory caching for fast file access
    /// </summary>
    internal unsafe class TarFS : FileSystem {
        private Disk _disk;
        
        // OPTIMIZATION: Use simple array instead of List to avoid allocation issues
        private TarFileEntry[] _fileCache;
        private int _fileCacheCount;
        private bool _cacheInitialized;

        // Cached file entry structure
        private class TarFileEntry {
            public string FullPath;
            public ulong DataSector;  // Sector where file data starts
            public ulong FileSize;
            public bool IsDirectory;
        }

        public TarFS(Disk disk)
        {
            BootConsole.WriteLine("[TarFS] Constructor (with disk) called");
            _fileCache = new TarFileEntry[1000];  // Preallocate array for up to 1000 files
            _fileCacheCount = 0;
            _cacheInitialized = false;
            _disk = disk;
            BootConsole.WriteLine("[TarFS] Disk assigned");
            InitializeFileCache();
        }

        public TarFS()
        {
            BootConsole.WriteLine("[TarFS] Constructor (parameterless) called");
            
            BootConsole.WriteLine("[TarFS] About to allocate file cache array");
            _fileCache = new TarFileEntry[1000];  // Preallocate array
            _fileCacheCount = 0;
            _cacheInitialized = false;
            BootConsole.WriteLine("[TarFS] File cache array allocated");
            
            BootConsole.WriteLine("[TarFS] About to get Disk.Instance");
            _disk = Disk.Instance;
            BootConsole.WriteLine("[TarFS] Disk.Instance retrieved");
            
            // WORKAROUND: Don't call InitializeFileCache - just mark as initialized
            // We'll scan the TAR on-demand in ReadAllBytes instead
            BootConsole.WriteLine("[TarFS] Skipping cache initialization (on-demand mode)");
            _cacheInitialized = false; // Will scan on first file access
            _fileCacheCount = 0;
            
            BootConsole.WriteLine("[TarFS] Constructor completed");
        }

        /// <summary>
        /// Build directory cache on mount - scan TAR archive once and cache all entries
        /// </summary>
        private void InitializeFileCache() {
            BootConsole.WriteLine("[TarFS] InitializeFileCache entered");
            
            if (_cacheInitialized) {
                BootConsole.WriteLine("[TarFS] Already initialized, returning");
                return;
            }
            
            // CRITICAL: Verify disk is ready before trying to read
            if (_disk == null) {
                BootConsole.WriteLine("[TarFS] ERROR: _disk is null!");
                _cacheInitialized = true;
                return;
            }
            
            BootConsole.WriteLine("[TarFS] Building file cache...");
            
            // Note: _fileCache is already allocated in constructor
            if (_fileCache == null) {
                BootConsole.WriteLine("[TarFS] ERROR: _fileCache is null!");
                _cacheInitialized = true;
                return;
            }
            
            BootConsole.WriteLine("[TarFS] Array ready, starting scan");
            BootConsole.WriteLine("[TarFS] Allocating TAR header buffer");
            
            // CRITICAL FIX: Allocate header on heap instead of stack
            // Stack allocation of 512-byte structure might cause overflow
            byte* headerBuffer = (byte*)Allocator.Allocate(512);
            if (headerBuffer == null) {
                BootConsole.WriteLine("[TarFS] ERROR: Failed to allocate header buffer!");
                _cacheInitialized = true;
                return;
            }
            
            BootConsole.WriteLine("[TarFS] Header buffer allocated");
            
            ulong sec = 0;
            posix_tar_header* ptr = (posix_tar_header*)headerBuffer;
            int fileCount = 0;
            int maxFiles = _fileCache.Length; // Use array length
            
            BootConsole.WriteLine("[TarFS] Starting TAR scan");
            BootConsole.WriteLine("[TarFS] About to read first header at sector 0");
            
            // Scan entire TAR archive once and cache all file entries
            while (fileCount < maxFiles) {
                // Read header
                BootConsole.WriteLine("[TarFS] Reading sector " + sec.ToString());
                
                bool readResult = false;
                try {
                    readResult = _disk.Read(sec, 1, (byte*)ptr);
                } catch {
                    BootConsole.WriteLine("[TarFS] EXCEPTION during disk read at sector " + sec.ToString());
                    break;
                }
                
                BootConsole.WriteLine("[TarFS] Read completed, result: " + (readResult ? "true" : "false"));
                
                if (!readResult) {
                    BootConsole.WriteLine("[TarFS] Disk read failed at sector " + sec.ToString());
                    break;
                }
                
                // Check for end of archive (null header)
                if (ptr->name[0] == 0) {
                    BootConsole.WriteLine("[TarFS] End of archive at sector " + sec.ToString());
                    break;
                }
                
                sec++; // Move past header
                
                // Parse file size
                ulong size = 0;
                try {
                    size = mystrtoul(ptr->size, null, 8);
                } catch {
                    BootConsole.WriteLine("[TarFS] ERROR: Failed to parse size");
                    break;
                }
                
                // Parse filename - SIMPLIFIED to avoid string allocation issues
                int nameLen = StringHelper.StringLength(ptr->name);
                if (nameLen > 0 && ptr->name[nameLen - 1] == (byte)'/') {
                    nameLen--; // Remove trailing slash
                }
                
                if (nameLen <= 0 || nameLen > 100) {
                    BootConsole.WriteLine("[TarFS] Invalid name length: " + nameLen.ToString());
                    sec += SizeToSec(size);
                    continue;
                }
                
                string fullPath = null;
                try {
                    fullPath = string.FromASCII((nint)ptr->name, nameLen);
                } catch {
                    BootConsole.WriteLine("[TarFS] ERROR: Failed to create string from name");
                    sec += SizeToSec(size);
                    continue;
                }
                
                // Cache this entry
                try {
                    TarFileEntry entry = new TarFileEntry();
                    entry.FullPath = fullPath;
                    entry.DataSector = sec;
                    entry.FileSize = size;
                    entry.IsDirectory = (ptr->typeflag == (byte)'5');
                    _fileCache[fileCount] = entry; // Store in array
                    fileCount++;
                    
                    // Log every 10 files to show progress
                    if (fileCount % 10 == 0) {
                        BootConsole.WriteLine("[TarFS] Cached " + fileCount.ToString() + " files");
                    }
                } catch {
                    BootConsole.WriteLine("[TarFS] ERROR: Failed to add entry to cache");
                    if (fullPath != null) fullPath.Dispose();
                    break;
                }
                
                // Move past data sectors
                sec += SizeToSec(size);
            }
            
            _fileCacheCount = fileCount;
            _cacheInitialized = true;
            BootConsole.WriteLine("[TarFS] Cache built: " + fileCount.ToString() + " files");
        }

        /// <summary>
        /// Posix Tar Header
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct posix_tar_header {
            /// <summary>
            /// Name
            /// </summary>
            public fixed byte name[100];
            /// <summary>
            /// Mode
            /// </summary>
            public fixed byte mode[8];
            /// <summary>
            /// UID
            /// </summary>
            public fixed byte uid[8];
            /// <summary>
            /// GID
            /// </summary>
            public fixed byte gid[8];
            /// <summary>
            /// SIZE
            /// </summary>
            public fixed byte size[12];
            /// <summary>
            /// MTime
            /// </summary>
            public fixed byte mtime[12];
            /// <summary>
            /// Check Sum
            /// </summary>
            public fixed byte chksum[8];
            /// <summary>
            /// Type Flag
            /// </summary>
            public byte typeflag;
            /// <summary>
            /// Link name
            /// </summary>
            public fixed byte linkname[100];
            /// <summary>
            /// Magic
            /// </summary>
            public fixed byte magic[6];
            /// <summary>
            /// Version
            /// </summary>
            public fixed byte version[2];
            /// <summary>
            /// UName
            /// </summary>
            public fixed byte uname[32];
            /// <summary>
            /// GName
            /// </summary>
            public fixed byte gname[32];
            /// <summary>
            /// Dev Major
            /// </summary>
            public fixed byte devmajor[8];
            /// <summary>
            /// Dev Minor
            /// </summary>
            public fixed byte devminor[8];
            /// <summary>
            /// Prefix
            /// </summary>
            public fixed byte prefix[155];
        };
        /// <summary>
        /// Mystrtoul
        /// </summary>
        /// <param name="nptr"></param>
        /// <param name="endptr"></param>
        /// <param name="base"></param>
        /// <returns></returns>
        [DllImport("*")]
        static extern ulong mystrtoul(byte* nptr, byte** endptr, int @base);
        
        /// <summary>
        /// Get Files - use cached directory instead of scanning TAR
        /// </summary>
        /// <param name="Directory"></param>
        /// <returns></returns>
        public override List<FileInfo> GetFiles(string Directory) {
            List<FileInfo> list = new();
            
            // Use cached file entries instead of scanning disk
            for (int i = 0; i < _fileCacheCount; i++) {
                var entry = _fileCache[i];
                
                if (IsInDirectory(entry.FullPath, Directory)) {
                    FileInfo info = new();
                    info.Param0 = entry.DataSector;
                    info.Param1 = entry.FileSize;
                    info.Name = entry.FullPath.Substring(entry.FullPath.LastIndexOf('/') + 1);
                    if (entry.IsDirectory) info.Attribute |= FileAttribute.Directory;
                    list.Add(info);
                }
            }
            
            return list;
        }
        
        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="Name"></param>
        public override void Delete(string Name) { }
        
        /// <summary>
        /// Read All Bytes - SIMPLIFIED: Scan TAR on-demand without cache
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        public override byte[] ReadAllBytes(string Name) {
            BootConsole.WriteLine("[TarFS] ReadAllBytes called for: " + Name);
            
            // Normalize path (remove leading slash if present)
            string searchPath = Name;
            if (Name.Length > 0 && Name[0] == '/') {
                searchPath = Name.Substring(1);
            }
            
            BootConsole.WriteLine("[TarFS] Searching for: " + searchPath);
            
            // Allocate header buffer on heap
            byte* headerBuffer = (byte*)Allocator.Allocate(512);
            if (headerBuffer == null) {
                BootConsole.WriteLine("[TarFS] ERROR: Failed to allocate header buffer!");
                return null;
            }
            
            posix_tar_header* hdr = (posix_tar_header*)headerBuffer;
            ulong sec = 0;
            int entryCount = 0;
            
            BootConsole.WriteLine("[TarFS] Starting TAR scan");
            
            // Scan TAR archive to find file
            while (true) {
                // Read header
                if (!_disk.Read(sec, 1, (byte*)hdr)) {
                    BootConsole.WriteLine("[TarFS] Disk read failed at sector " + sec.ToString());
                    break;
                }
                
                // DEBUG: Show first few bytes of header
                if (sec == 0) {
                    BootConsole.WriteLine("[TarFS] First header bytes (ASCII):");
                    BootConsole.Write("  ");
                    for (int i = 0; i < 16; i++) {
                        byte b = ((byte*)hdr)[i];
                        if (b >= 32 && b < 127) {
                            // Can't use Write(char) directly, so write the byte as a simple marker
                            BootConsole.Write(b >= 65 && b <= 90 ? "A" : b >= 97 && b <= 122 ? "a" : "x");
                        } else {
                            BootConsole.Write(".");
                        }
                    }
                    BootConsole.WriteLine("");
                }
                
                // Check for end of archive
                if (hdr->name[0] == 0) {
                    BootConsole.WriteLine("[TarFS] End of archive at sector " + sec.ToString() + " after " + entryCount.ToString() + " entries");
                    break;
                }
                
                entryCount++;
                
                
                sec++; // Move past header
                
                // Parse file size
                ulong size = 0;
                try {
                    size = mystrtoul(hdr->size, null, 8);
                } catch {
                    BootConsole.WriteLine("[TarFS] ERROR: Failed to parse size");
                    break;
                }
                
                // Parse filename
                int nameLen = StringHelper.StringLength(hdr->name);
                if (nameLen > 0 && hdr->name[nameLen - 1] == (byte)'/') {
                    nameLen--; // Remove trailing slash
                }
                
                if (nameLen <= 0 || nameLen > 100) {
                    BootConsole.WriteLine("[TarFS] Invalid name length: " + nameLen.ToString());
                    sec += SizeToSec(size);
                    continue;
                }
                
                string fullPath = null;
                try {
                    fullPath = string.FromASCII((nint)hdr->name, nameLen);
                    
                    // DEBUG: Log every file we find
                    BootConsole.WriteLine("[TarFS] Found entry: " + fullPath);
                } catch {
                    BootConsole.WriteLine("[TarFS] ERROR: Failed to create string from name");
                    sec += SizeToSec(size);
                    continue;
                }
                
                // Check if this is the file we want
                if (fullPath.Equals(searchPath) || fullPath.Equals(Name)) {
                    BootConsole.WriteLine("[TarFS] Found file: " + fullPath);
                    
                    // Read file data
                    ulong sectorCount = SizeToSec(size);
                    byte[] buffer = new byte[(uint)sectorCount * 512];
                    
                    fixed (byte* ptr = buffer) {
                        if (!_disk.Read(sec, (uint)sectorCount, ptr)) {
                            BootConsole.WriteLine("[TarFS] ERROR: Failed to read file data");
                            if (fullPath != null) fullPath.Dispose();
                            return null;
                        }
                    }
                    
                    // Trim to actual file size
                    buffer.Length = (int)size;
                    
                    if (fullPath != null) fullPath.Dispose();
                    return buffer;
                }
                
                // Not the file we want - skip to next entry
                if (fullPath != null) fullPath.Dispose();
                sec += SizeToSec(size);
            }
            
            BootConsole.WriteLine("[TarFS] File not found: " + Name);
            Panic.Error($"{Name} is not found!");
            return null;
        }
        
        /// <summary>
        /// Write All Bytes
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="Content"></param>
        public override void WriteAllBytes(string Name, byte[] Content) { }
        
        /// <summary>
        /// Format
        /// </summary>
        public override void Format() { }
    }
}