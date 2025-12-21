using guideXOS.Misc;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace guideXOS.FS {
    /// <summary>
    /// Complete FAT12/16/32 filesystem with LFN support and sector caching.
    /// Supports read, write, create, delete, and format operations.
    /// </summary>
    internal unsafe class FAT : FileSystem {
        /// <summary>
        /// Fat Type
        /// </summary>
        enum FatType {
            /// <summary>
            /// FAT12
            /// </summary>
            FAT12, 
            /// <summary>
            /// FAT16
            /// </summary>
            FAT16, 
            /// <summary>
            /// FAT32
            /// </summary>
            FAT32
        }
        /// <summary>
        /// BPB Common
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BPB_Common {
            public fixed byte jmpBoot[3];
            public fixed byte OEMName[8];
            public ushort BytsPerSec; // 0x0B
            public byte SecPerClus;   // 0x0D
            public ushort RsvdSecCnt; // 0x0E
            public byte NumFATs;      // 0x10
            public ushort RootEntCnt; // 0x11 (FAT12/16)
            public ushort TotSec16;   // 0x13
            public byte Media;        // 0x15
            public ushort FATSz16;    // 0x16
            public ushort SecPerTrk;  // 0x18
            public ushort NumHeads;   // 0x1A
            public uint HiddSec;      // 0x1C
            public uint TotSec32;     // 0x20
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BPB_FAT32 {
            public uint FATSz32;      // 0x24
            public ushort ExtFlags;   // 0x28
            public ushort FSVer;      // 0x2A
            public uint RootClus;     // 0x2C
            public ushort FSInfo;     // 0x30
            public ushort BkBootSec;  // 0x32
            public fixed byte Reserved[12];
            public byte DrvNum;       // 0x40
            public byte Reserved1;    // 0x41
            public byte BootSig;      // 0x42
            public uint VolID;        // 0x43
            public fixed byte VolLab[11]; // 0x47
            public fixed byte FilSysType[8]; // 0x52 e.g. "FAT32   "
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct DirEntry {
            public fixed byte Name83[11];
            public byte Attr;
            public byte NTRes;
            public byte CrtTimeTenth;
            public ushort CrtTime;
            public ushort CrtDate;
            public ushort LstAccDate;
            public ushort FstClusHI;
            public ushort WrtTime;
            public ushort WrtDate;
            public ushort FstClusLO;
            public uint FileSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct LfnEntry {
            public byte Ord; // sequence number
            public fixed ushort Name1[5];
            public byte Attr; // 0x0F
            public byte Type;
            public byte Chksum;
            public fixed ushort Name2[6];
            public ushort FstClusLO;
            public fixed ushort Name3[2];
        }

        // Runtime fields
        private Disk _disk;
        private Disk disk => _disk ?? Disk.Instance;
        private FatType _type;
        private ushort _bytesPerSec;
        private byte _secPerClus;
        private ushort _rsvdSecCnt;
        private byte _numFATs;
        private uint _FATSz;
        private uint _rootDirSectors;
        private uint _firstDataSector;
        private uint _fatStart;
        private uint _firstRootDirSector; // FAT12/16 only
        private uint _rootCluster; // FAT32 root cluster
        private uint _clusterCount;

        // Simple sector cache - replaced Dictionary with parallel arrays
        private const int CacheCapacity = 1024; // sectors
        private ulong[] _cacheKeys;
        private byte[][] _cacheValues;
        private int _cacheCount;
        private int _lruHead; // simple circular buffer for LRU

        public FAT(Disk disk) {
            this._disk = disk;
            // Initialize cache arrays
            _cacheKeys = new ulong[CacheCapacity];
            _cacheValues = new byte[CacheCapacity][];
            _cacheCount = 0;
            _lruHead = 0;

            // Read boot sector
            var sec0 = ReadSectorsCached(0, 1);
            fixed (byte* p = sec0) {
                BPB_Common* bpb = (BPB_Common*)p;
                _bytesPerSec = bpb->BytsPerSec;
                _secPerClus = bpb->SecPerClus;
                _rsvdSecCnt = bpb->RsvdSecCnt;
                _numFATs = bpb->NumFATs;
                uint totSec = bpb->TotSec16 != 0 ? bpb->TotSec16 : bpb->TotSec32;
                uint fatsz = bpb->FATSz16;
                if (fatsz == 0) {
                    BPB_FAT32* bpb32 = (BPB_FAT32*)(p + 0x24);
                    fatsz = bpb32->FATSz32;
                    _rootCluster = bpb32->RootClus;
                }
                _FATSz = fatsz;
                uint rootEntCnt = bpb->RootEntCnt;
                _rootDirSectors = (uint)((rootEntCnt * 32 + (_bytesPerSec - 1)) / _bytesPerSec);
                uint dataSec = totSec - (uint)(_rsvdSecCnt + (_numFATs * fatsz) + _rootDirSectors);
                uint countOfClusters = dataSec / _secPerClus;
                _clusterCount = countOfClusters;
                _type = countOfClusters < 4085 ? FatType.FAT12 : (countOfClusters < 65525 ? FatType.FAT16 : FatType.FAT32);
                _fatStart = _rsvdSecCnt;
                _firstDataSector = (uint)(_rsvdSecCnt + (_numFATs * fatsz) + _rootDirSectors);
                _firstRootDirSector = (uint)(_rsvdSecCnt + (_numFATs * fatsz));
                if (_type != FatType.FAT32) _rootCluster = 0; // not used
            }
        }

        public FAT() {
            // Initialize cache arrays
            _cacheKeys = new ulong[CacheCapacity];
            _cacheValues = new byte[CacheCapacity][];
            _cacheCount = 0;
            _lruHead = 0;

            // Read boot sector
            var sec0 = ReadSectorsCached(0, 1);
            fixed (byte* p = sec0) {
                BPB_Common* bpb = (BPB_Common*)p;
                _bytesPerSec = bpb->BytsPerSec;
                _secPerClus = bpb->SecPerClus;
                _rsvdSecCnt = bpb->RsvdSecCnt;
                _numFATs = bpb->NumFATs;
                uint totSec = bpb->TotSec16 != 0 ? bpb->TotSec16 : bpb->TotSec32;
                uint fatsz = bpb->FATSz16;
                if (fatsz == 0) {
                    BPB_FAT32* bpb32 = (BPB_FAT32*)(p + 0x24);
                    fatsz = bpb32->FATSz32;
                    _rootCluster = bpb32->RootClus;
                }
                _FATSz = fatsz;
                uint rootEntCnt = bpb->RootEntCnt;
                _rootDirSectors = (uint)((rootEntCnt * 32 + (_bytesPerSec - 1)) / _bytesPerSec);
                uint dataSec = totSec - (uint)(_rsvdSecCnt + (_numFATs * fatsz) + _rootDirSectors);
                uint countOfClusters = dataSec / _secPerClus;
                _clusterCount = countOfClusters;
                _type = countOfClusters < 4085 ? FatType.FAT12 : (countOfClusters < 65525 ? FatType.FAT16 : FatType.FAT32);
                _fatStart = _rsvdSecCnt;
                _firstDataSector = (uint)(_rsvdSecCnt + (_numFATs * fatsz) + _rootDirSectors);
                _firstRootDirSector = (uint)(_rsvdSecCnt + (_numFATs * fatsz));
                if (_type != FatType.FAT32) _rootCluster = 0; // not used
            }
        }

        private void InvalidateSector(ulong lba) {
            // Linear search and invalidate
            for (int i = 0; i < _cacheCount; i++) {
                if (_cacheKeys[i] == lba) {
                    _cacheValues[i] = null;
                    // Don't reduce count - keep slot for reuse
                    return;
                }
            }
        }

        private byte[] ReadSectorsCached(ulong lba, uint count) {
            if (count == 1) {
                // Check cache first
                for (int i = 0; i < _cacheCount; i++) {
                    if (_cacheKeys[i] == lba && _cacheValues[i] != null) {
                        return _cacheValues[i];
                    }
                }

                // Not in cache - read from disk
                var buf = new byte[SectorSize];
                fixed (byte* p = buf) disk.Read(lba, 1, p);

                // Add to cache
                if (_cacheCount < CacheCapacity) {
                    // Add new entry
                    _cacheKeys[_cacheCount] = lba;
                    _cacheValues[_cacheCount] = buf;
                    _cacheCount++;
                } else {
                    // Replace oldest (circular LRU)
                    _cacheKeys[_lruHead] = lba;
                    _cacheValues[_lruHead] = buf;
                    _lruHead = (_lruHead + 1) % CacheCapacity;
                }

                return buf;
            } else {
                // Multi-sector read - don't cache
                var buf = new byte[count * SectorSize];
                fixed (byte* p = buf) disk.Read(lba, count, p);
                return buf;
            }
        }

        private void WriteSector(ulong lba, byte[] data) {
            fixed (byte* p = data) disk.Write(lba, 1, p);
            InvalidateSector(lba);
        }

        private uint FirstSectorOfCluster(uint n) { return (uint)((n - 2) * _secPerClus) + _firstDataSector; }

        private uint ReadFatEntry(uint cluster) {
            switch (_type) {
                case FatType.FAT12: {
                        uint fatOffset = cluster + (cluster / 2);
                        uint fatSec = (uint)(_fatStart + (fatOffset / _bytesPerSec));
                        int off = (int)(fatOffset % _bytesPerSec);
                        var sec = ReadSectorsCached(fatSec, 1);
                        var nextSec = off == _bytesPerSec - 1 ? ReadSectorsCached(fatSec + 1, 1) : null;
                        uint val = sec[off];
                        if (off == _bytesPerSec - 1) val |= (uint)(nextSec[0] << 8);
                        else val |= (uint)(sec[off + 1] << 8);
                        if ((cluster & 1) == 1) val >>= 4; else val &= 0x0FFF;
                        return val;
                    }
                case FatType.FAT16: {
                        uint fatOffset = cluster * 2u;
                        uint fatSec = (uint)(_fatStart + (fatOffset / _bytesPerSec));
                        int off = (int)(fatOffset % _bytesPerSec);
                        var sec = ReadSectorsCached(fatSec, 1);
                        return (uint)(sec[off] | (sec[off + 1] << 8));
                    }
                default: {
                        uint fatOffset = cluster * 4u;
                        uint fatSec = (uint)(_fatStart + (fatOffset / _bytesPerSec));
                        int off = (int)(fatOffset % _bytesPerSec);
                        var sec = ReadSectorsCached(fatSec, 1);
                        uint val = (uint)(sec[off] | (sec[off + 1] << 8) | (sec[off + 2] << 16) | (sec[off + 3] << 24));
                        return val & 0x0FFFFFFF;
                    }
            }
        }

        private void WriteFatEntry(uint cluster, uint value) {
            // Write to all FAT copies
            for (int fatCopy = 0; fatCopy < _numFATs; fatCopy++) {
                uint fatBase = (uint)(_fatStart + (fatCopy * _FATSz));
                switch (_type) {
                    case FatType.FAT12: {
                            uint fatOffset = cluster + (cluster / 2);
                            uint fatSec = (uint)(fatBase + (fatOffset / _bytesPerSec));
                            int off = (int)(fatOffset % _bytesPerSec);
                            var sec = ReadSectorsCached(fatSec, 1);
                            // get the two bytes
                            byte b0 = sec[off];
                            byte b1 = (off == _bytesPerSec - 1) ? ReadSectorsCached(fatSec + 1, 1)[0] : sec[off + 1];
                            uint cur = (uint)(b0 | (b1 << 8));
                            if ((cluster & 1) == 1) {
                                // odd cluster: high 12 bits
                                cur &= 0x000F;
                                cur |= (value & 0x0FFF) << 4;
                            } else {
                                // even cluster: low 12 bits
                                cur &= 0xF000;
                                cur |= (value & 0x0FFF);
                            }
                            // write back
                            sec[off] = (byte)(cur & 0xFF);
                            if (off == _bytesPerSec - 1) {
                                var sec2 = ReadSectorsCached(fatSec + 1, 1);
                                sec2[0] = (byte)((cur >> 8) & 0xFF);
                                WriteSector(fatSec + 1, sec2);
                            } else {
                                sec[off + 1] = (byte)((cur >> 8) & 0xFF);
                            }
                            WriteSector(fatSec, sec);
                            break;
                        }
                    case FatType.FAT16: {
                            uint fatOffset = cluster * 2u;
                            uint fatSec = (uint)(fatBase + (fatOffset / _bytesPerSec));
                            int off = (int)(fatOffset % _bytesPerSec);
                            var sec = ReadSectorsCached(fatSec, 1);
                            sec[off] = (byte)(value & 0xFF);
                            sec[off + 1] = (byte)((value >> 8) & 0xFF);
                            WriteSector(fatSec, sec);
                            break;
                        }
                    default: {
                            uint fatOffset = cluster * 4u;
                            uint fatSec = (uint)(fatBase + (fatOffset / _bytesPerSec));
                            int off = (int)(fatOffset % _bytesPerSec);
                            var sec = ReadSectorsCached(fatSec, 1);
                            uint cur = (uint)(sec[off] | (sec[off + 1] << 8) | (sec[off + 2] << 16) | (sec[off + 3] << 24));
                            cur &= 0xF0000000; // upper 4 bits preserved
                            cur |= (value & 0x0FFFFFFF);
                            sec[off] = (byte)(cur & 0xFF);
                            sec[off + 1] = (byte)((cur >> 8) & 0xFF);
                            sec[off + 2] = (byte)((cur >> 16) & 0xFF);
                            sec[off + 3] = (byte)((cur >> 24) & 0xFF);
                            WriteSector(fatSec, sec);
                            break;
                        }
                }
            }
        }

        private bool IsEOC(uint clus) {
            switch (_type) {
                case FatType.FAT12: return clus >= 0x0FF8;
                case FatType.FAT16: return clus >= 0xFFF8;
                default: return clus >= 0x0FFFFFF8;
            }
        }

        private uint EOC() {
            switch (_type) {
                case FatType.FAT12: return 0x0FFF;
                case FatType.FAT16: return 0xFFFF;
                default: return 0x0FFFFFFF;
            }
        }

        private List<uint> GetClusterChain(uint start) {
            List<uint> chain = new List<uint>();
            uint c = start;
            while (c >= 2 && !IsEOC(c)) {
                chain.Add(c);
                uint next = ReadFatEntry(c);
                if (next == 0 || next == c) break;
                c = next;
            }
            if (c >= 2) chain.Add(c);
            return chain;
        }

        private void ReadCluster(uint cluster, byte[] dest, int destOffset) {
            uint firstSec = FirstSectorOfCluster(cluster);
            for (int i = 0; i < _secPerClus; i++) {
                var sec = ReadSectorsCached(firstSec + (uint)i, 1);
                fixed (byte* pSec = sec)
                fixed (byte* pDest = dest) {
                    Native.Movsb(pDest + destOffset + i * _bytesPerSec, pSec, (ulong)_bytesPerSec);
                }
            }
        }

        private void ZeroCluster(uint cluster) {
            uint firstSec = FirstSectorOfCluster(cluster);
            var zero = new byte[_bytesPerSec];
            for (int i = 0; i < _secPerClus; i++) {
                WriteSector(firstSec + (uint)i, zero);
            }
        }

        private byte[] ReadAllClusterChain(uint startCluster, uint fileSize) {
            int totalBytes = (int)(fileSize == 0 ? (_secPerClus * _bytesPerSec) : fileSize);
            var buf = new byte[AlignUp(totalBytes, _secPerClus * _bytesPerSec)];
            int off = 0;
            var chain = GetClusterChain(startCluster);
            for (int i = 0; i < chain.Count; i++) {
                ReadCluster(chain[i], buf, off);
                off += _secPerClus * _bytesPerSec;
            }
            if (fileSize == 0) return new byte[0];
            if ((uint)buf.Length == fileSize) return buf;
            var trimmed = new byte[fileSize];
            fixed (byte* pSrc = buf)
            fixed (byte* pDst = trimmed) {
                Native.Movsb(pDst, pSrc, fileSize);
            }
            return trimmed;
        }

        private static int AlignUp(int val, int align) { int m = val % align; return m == 0 ? val : val + (align - m); }

        private struct DirResult { public bool Found; public uint FirstCluster; public uint Size; }

        private static char ToChar(byte b) { return (char)b; }

        private string ComposeShortName(byte* name83) {
            int nameLen = 8; while (nameLen > 0 && name83[nameLen - 1] == (byte)' ') nameLen--;
            int extLen = 3; while (extLen > 0 && name83[8 + extLen - 1] == (byte)' ') extLen--;
            string name = string.Empty;
            for (int i = 0; i < nameLen; i++) name += ToChar(name83[i]);
            if (extLen > 0) { name += "."; for (int i = 0; i < extLen; i++) name += ToChar(name83[8 + i]); }
            return name;
        }

        private static string AppendUtf16(string s, ushort ch) { if (ch == 0xFFFF || ch == 0x0000) return s; return s + (char)ch; }

        private string AssembleLfn(LfnEntry* lfnParts, int count) {
            string name = string.Empty;
            for (int i = count - 1; i >= 0; i--) {
                var p = lfnParts + i;
                for (int j = 0; j < 5; j++) name = AppendUtf16(name, p->Name1[j]);
                for (int j = 0; j < 6; j++) name = AppendUtf16(name, p->Name2[j]);
                for (int j = 0; j < 2; j++) name = AppendUtf16(name, p->Name3[j]);
            }
            return name;
        }

        private uint GetDirStartCluster(uint dirCluster) { if (_type == FatType.FAT32) return dirCluster == 0 ? _rootCluster : dirCluster; return dirCluster; }

        private bool IterateDirectory(uint dirCluster, Func<string, bool, uint, uint, bool> onEntry) {
            if (_type == FatType.FAT32 || dirCluster >= 2) {
                var chain = GetClusterChain(GetDirStartCluster(dirCluster));
                for (int idx = 0; idx < chain.Count; idx++) { if (!IterateDirSectorRangeCluster(chain[idx], onEntry)) return false; }
                return true;
            } else {
                for (uint s = 0; s < _rootDirSectors; s++) { var sec = ReadSectorsCached(_firstRootDirSector + s, 1); fixed (byte* p = sec) { if (!IterateDirEntries(p, _bytesPerSec, onEntry)) return false; } }
                return true;
            }
        }

        private bool IterateDirSectorRangeCluster(uint cluster, Func<string, bool, uint, uint, bool> onEntry) {
            uint firstSec = FirstSectorOfCluster(cluster);
            for (int i = 0; i < _secPerClus; i++) { var sec = ReadSectorsCached(firstSec + (uint)i, 1); fixed (byte* p = sec) { if (!IterateDirEntries(p, _bytesPerSec, onEntry)) return false; } }
            return true;
        }

        private bool IterateDirEntries(byte* p, int bytes, Func<string, bool, uint, uint, bool> onEntry) {
            int count = bytes / 32; LfnEntry* lfnBuf = stackalloc LfnEntry[20]; int lfnCount = 0;
            for (int i = 0; i < count; i++) {
                byte first = p[i * 32]; if (first == 0x00) break; if (first == 0xE5) { lfnCount = 0; continue; }
                byte attr = p[i * 32 + 11]; if (attr == 0x0F) { var lfn = (LfnEntry*)(p + i * 32); lfnBuf[lfnCount++] = *lfn; continue; }
                var de = (DirEntry*)(p + i * 32); bool isDir = (de->Attr & 0x10) != 0;
                string name = lfnCount > 0 ? AssembleLfn(lfnBuf, lfnCount) : ComposeShortName(de->Name83);
                uint clus = ((uint)de->FstClusHI << 16) | de->FstClusLO; uint size = de->FileSize; lfnCount = 0;
                if (!onEntry(name, isDir, clus, size)) return false;
            }
            return true;
        }

        private static bool EqualsIgnoreCase(string a, string b) {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) { char ca = a[i]; char cb = b[i]; if (ca >= 'a' && ca <= 'z') ca = (char)(ca - 32); if (cb >= 'a' && cb <= 'z') cb = (char)(cb - 32); if (ca != cb) return false; }
            return true;
        }

        private DirResult FindPath(string path) {
            while (path.Length > 0 && path[0] == '/') path = path.Substring(1);
            if (path.Length == 0) return new DirResult { Found = true, FirstCluster = _type == FatType.FAT32 ? _rootCluster : 0, Size = 0 };
            var parts = path.Split('/');
            uint current = _type == FatType.FAT32 ? _rootCluster : 0;
            for (int i = 0; i < parts.Length; i++) {
                string part = parts[i]; bool last = i == parts.Length - 1; bool found = false;
                IterateDirectory(current, (name, isDir, clus, size) => { if (EqualsIgnoreCase(name, part)) { if (!last && !isDir) return true; found = true; current = clus; return false; } return true; });
                if (!found) return new DirResult { Found = false }; if (!last && current < 2 && _type == FatType.FAT32) return new DirResult { Found = false }; part.Dispose();
            }
            uint sizeFinal = 0; string parent = path.LastIndexOf('/') >= 0 ? path.Substring(0, path.LastIndexOf('/')) : ""; string lastName = path.Substring(path.LastIndexOf('/') + 1);
            uint parentCluster = _type == FatType.FAT32 ? _rootCluster : 0; if (parent.Length > 0) { var parentRes = FindPath(parent); if (!parentRes.Found) return new DirResult { Found = false }; parentCluster = parentRes.FirstCluster; }
            uint firstClusFinal = 0; IterateDirectory(parentCluster, (name, isDir, clus, size) => { if (EqualsIgnoreCase(name, lastName)) { firstClusFinal = clus; sizeFinal = size; return false; } return true; }); parent.Dispose(); lastName.Dispose();
            return new DirResult { Found = true, FirstCluster = firstClusFinal, Size = sizeFinal };
        }

        private struct EntryLoc { public bool Found; public ulong LBA; public int Index; public uint Cluster; public bool RootFixed; public DirEntry Entry; }

        private EntryLoc FindEntryLoc(uint dirCluster, string name, bool findFreeSlot, out bool exists) {
            exists = false; EntryLoc firstFree = default; firstFree.Found = false;
            if (_type == FatType.FAT32 || dirCluster >= 2) {
                var chain = GetClusterChain(GetDirStartCluster(dirCluster));
                for (int cidx = 0; cidx < chain.Count; cidx++) {
                    uint c = chain[cidx]; uint firstSec = FirstSectorOfCluster(c);
                    for (int i = 0; i < _secPerClus; i++) {
                        ulong lba = firstSec + (uint)i; var sec = ReadSectorsCached(lba, 1); fixed (byte* p = sec) {
                            int count = _bytesPerSec / 32; int lfnCount = 0; LfnEntry* lfnBuf = stackalloc LfnEntry[20];
                            for (int e = 0; e < count; e++) {
                                byte first = p[e * 32]; if (first == 0x00) { if (!firstFree.Found) { firstFree.Found = true; firstFree.LBA = lba; firstFree.Index = e; firstFree.Cluster = c; firstFree.RootFixed = false; } goto Done; }
                                if (first == 0xE5) { if (findFreeSlot && !firstFree.Found) { firstFree.Found = true; firstFree.LBA = lba; firstFree.Index = e; firstFree.Cluster = c; firstFree.RootFixed = false; } lfnCount = 0; continue; }
                                byte attr = p[e * 32 + 11]; if (attr == 0x0F) { var lfn = (LfnEntry*)(p + e * 32); lfnBuf[lfnCount++] = *lfn; continue; }
                                var de = (DirEntry*)(p + e * 32); string ename = lfnCount > 0 ? AssembleLfn(lfnBuf, lfnCount) : ComposeShortName(de->Name83); lfnCount = 0;
                                if (EqualsIgnoreCase(ename, name)) { exists = true; EntryLoc loc; loc.Found = true; loc.LBA = lba; loc.Index = e; loc.Cluster = c; loc.RootFixed = false; loc.Entry = *de; return loc; }
                            }
                        }
                    }
                }
            } else {
                for (uint s = 0; s < _rootDirSectors; s++) {
                    ulong lba = _firstRootDirSector + s; var sec = ReadSectorsCached(lba, 1); fixed (byte* p = sec) {
                        int count = _bytesPerSec / 32; int lfnCount = 0; LfnEntry* lfnBuf = stackalloc LfnEntry[20];
                        for (int e = 0; e < count; e++) {
                            byte first = p[e * 32]; if (first == 0x00) { if (!firstFree.Found) { firstFree.Found = true; firstFree.LBA = lba; firstFree.Index = e; firstFree.Cluster = 0; firstFree.RootFixed = true; } goto Done; }
                            if (first == 0xE5) { if (findFreeSlot && !firstFree.Found) { firstFree.Found = true; firstFree.LBA = lba; firstFree.Index = e; firstFree.Cluster = 0; firstFree.RootFixed = true; } lfnCount = 0; continue; }
                            byte attr = p[e * 32 + 11]; if (attr == 0x0F) { var lfn = (LfnEntry*)(p + e * 32); lfnBuf[lfnCount++] = *lfn; continue; }
                            var de = (DirEntry*)(p + e * 32); string ename = lfnCount > 0 ? AssembleLfn(lfnBuf, lfnCount) : ComposeShortName(de->Name83); lfnCount = 0;
                            if (EqualsIgnoreCase(ename, name)) { exists = true; EntryLoc loc; loc.Found = true; loc.LBA = lba; loc.Index = e; loc.Cluster = 0; loc.RootFixed = true; loc.Entry = *de; return loc; }
                        }
                    }
                }
            }
        Done:
            return firstFree;
        }

        private uint NextFreeCluster(uint start) {
            uint lastClusterNum = (uint)(_clusterCount + 1); // clusters numbered up to count+1
            if (start < 2) start = 2;
            for (uint c = start; c <= lastClusterNum + 2; c++) {
                uint idx = 2 + ((c - 2) % lastClusterNum);
                if (ReadFatEntry(idx) == 0) return idx;
            }
            // No free clusters available
            return 0;
        }

        private List<uint> AllocateClustersForSize(uint size) {
            List<uint> chain = new List<uint>();
            if (size == 0) return chain;
            int clustersNeeded = AlignUp((int)size, _secPerClus * _bytesPerSec) / (_secPerClus * _bytesPerSec);
            uint prev = 0; uint search = 2;
            for (int i = 0; i < clustersNeeded; i++) {
                uint c = NextFreeCluster(search); 
                if (c == 0) {
                    // Out of space - free what we allocated and return empty chain
                    if (chain.Count > 0) {
                        FreeClusterChain(chain[0]);
                    }
                    return new List<uint>();
                }
                // mark allocated
                WriteFatEntry(c, EOC());
                if (prev != 0) WriteFatEntry(prev, c);
                chain.Add(c);
                prev = c; search = c + 1;
            }
            return chain;
        }

        private void FreeClusterChain(uint start) {
            if (start < 2) return;
            uint c = start;
            while (c >= 2 && !IsEOC(c)) {
                uint next = ReadFatEntry(c);
                WriteFatEntry(c, 0);
                if (next == 0 || next == c) break; c = next;
            }
            // free last one too
            WriteFatEntry(c, 0);
        }

        private void WriteDataToChain(List<uint> chain, byte[] content) {
            int clusterBytes = _secPerClus * _bytesPerSec; int offset = 0; var scratch = new byte[clusterBytes];
            for (int i = 0; i < chain.Count; i++) {
                // prepare buffer
                int remaining = content.Length - offset; if (remaining < 0) remaining = 0; int toCopy = remaining > clusterBytes ? clusterBytes : remaining;
                // zero scratch
                for (int k = 0; k < clusterBytes; k++) scratch[k] = 0;
                if (toCopy > 0) {
                    fixed (byte* pSrc = content)
                    fixed (byte* pDst = scratch) {
                        Native.Movsb(pDst, pSrc + offset, (ulong)toCopy);
                    }
                }
                // write sectors
                uint firstSec = FirstSectorOfCluster(chain[i]);
                for (int s = 0; s < _secPerClus; s++) {
                    var slice = new byte[_bytesPerSec];
                    for (int b = 0; b < _bytesPerSec; b++) slice[b] = scratch[s * _bytesPerSec + b];
                    WriteSector(firstSec + (uint)s, slice);
                }
                offset += toCopy;
                if (offset >= content.Length) break;
            }
        }

        private void WriteDirEntry(EntryLoc slot, string shortName, uint firstCluster, uint fileSize, byte attr) {
            var sec = ReadSectorsCached(slot.LBA, 1);
            fixed (byte* p = sec) {
                DirEntry* de = (DirEntry*)(p + slot.Index * 32);
                // name
                for (int i = 0; i < 11; i++) de->Name83[i] = (byte)' ';
                int dot = shortName.LastIndexOf('.'); string name = shortName; string ext = "";
                if (dot >= 0) { name = shortName.Substring(0, dot); ext = shortName.Substring(dot + 1); }
                name = name.ToUpper(); ext = ext.ToUpper();
                for (int i = 0; i < name.Length && i < 8; i++) de->Name83[i] = (byte)name[i];
                for (int i = 0; i < ext.Length && i < 3; i++) de->Name83[8 + i] = (byte)ext[i];
                de->Attr = attr;
                de->FstClusHI = (ushort)((firstCluster >> 16) & 0xFFFF);
                de->FstClusLO = (ushort)(firstCluster & 0xFFFF);
                de->FileSize = fileSize;
            }
            WriteSector(slot.LBA, sec);
        }

        private string GenerateShortName(string name) {
            // Simple 8.3 upper-case generator; strip invalid chars
            string n = name; int lastSlash = n.LastIndexOf('/'); if (lastSlash >= 0) n = n.Substring(lastSlash + 1);
            int dot = n.LastIndexOf('.'); string baseN = dot >= 0 ? n.Substring(0, dot) : n; string ext = dot >= 0 ? n.Substring(dot + 1) : "";
            string filtered = ""; for (int i = 0; i < baseN.Length; i++) { char c = baseN[i]; if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_') filtered += c; }
            string filteredExt = ""; for (int i = 0; i < ext.Length; i++) { char c = ext[i]; if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_') filteredExt += c; }
            if (filtered.Length > 8) filtered = filtered.Substring(0, 8);
            if (filteredExt.Length > 3) filteredExt = filteredExt.Substring(0, 3);
            // upper
            string up = ""; for (int i = 0; i < filtered.Length; i++) { char c = filtered[i]; if (c >= 'a' && c <= 'z') c = (char)(c - 32); up += c; }
            string e2 = ""; for (int i = 0; i < filteredExt.Length; i++) { char c = filteredExt[i]; if (c >= 'a' && c <= 'z') c = (char)(c - 32); e2 += c; }
            return e2.Length > 0 ? (up + "." + e2) : up;
        }

        private void EnsureDirHasFreeSlot(uint dirCluster, ref EntryLoc slot) {
            if (slot.Found) return;
            // need to expand directory if possible
            if (_type == FatType.FAT12 || _type == FatType.FAT16) {
                if (dirCluster == 0) {
                    // Root directory is full on FAT12/16 - cannot expand
                    return;
                }
            }
            // FAT32 or subdir: append a new cluster
            uint lastCluster = 0; var chain = GetClusterChain(GetDirStartCluster(dirCluster)); if (chain.Count > 0) lastCluster = chain[chain.Count - 1];
            uint newc = NextFreeCluster(2); 
            if (newc == 0) {
                // No free clusters available
                return;
            }
            WriteFatEntry(newc, EOC()); if (lastCluster != 0) WriteFatEntry(lastCluster, newc);
            ZeroCluster(newc);
            // first entry of new cluster
            slot.Found = true; slot.Cluster = newc; slot.RootFixed = false; slot.LBA = FirstSectorOfCluster(newc); slot.Index = 0;
        } 

        public override List<FileInfo> GetFiles(string Directory) {
            // Normalize Directory to have trailing '/'
            string dir = Directory; if (dir.Length > 0 && dir[dir.Length - 1] == '/') dir = dir.Substring(0, dir.Length - 1);
            // Find directory cluster
            uint dirCluster = _type == FatType.FAT32 ? _rootCluster : 0;
            if (!string.IsNullOrEmpty(dir)) { var rr = FindPath(dir); if (!rr.Found) return new List<FileInfo>(); dirCluster = rr.FirstCluster; }
            List<FileInfo> list = new List<FileInfo>();
            IterateDirectory(dirCluster, (name, isDir, clus, size) => {
                // Skip '.' and '..'
                if (name == "." || name == "..") return true;
                var fi = new FileInfo(); fi.Name = name; if (isDir) fi.Attribute |= FileAttribute.Directory; fi.Param0 = clus; fi.Param1 = size; list.Add(fi); return true;
            });
            return list;
        }

        public override void Delete(string Name) {
            // Only files for now
            string parent = Name.LastIndexOf('/') >= 0 ? Name.Substring(0, Name.LastIndexOf('/')) : "";
            string just = Name.Substring(Name.LastIndexOf('/') + 1);
            uint parentCluster = _type == FatType.FAT32 ? _rootCluster : 0;
            if (parent.Length > 0) { 
                var pr = FindPath(parent); 
                if (!pr.Found) { 
                    // Parent directory not found - cannot delete
                    return; 
                } 
                parentCluster = pr.FirstCluster; 
            }
            bool exists; 
            var loc = FindEntryLoc(parentCluster, just, false, out exists); 
            if (!exists || !loc.Found) { 
                // File not found - nothing to delete
                return; 
            }
            uint firstClus = ((uint)loc.Entry.FstClusHI << 16) | loc.Entry.FstClusLO; 
            if (firstClus >= 2) FreeClusterChain(firstClus);
            // mark deleted
            var sec = ReadSectorsCached(loc.LBA, 1); 
            sec[loc.Index * 32] = 0xE5; 
            WriteSector(loc.LBA, sec);
        }

        public override byte[] ReadAllBytes(string Name) {
            var res = FindPath(Name); if (!res.Found) { Panic.Error($"{Name} is not found!"); return null; }
            if (res.FirstCluster < 2 && _type == FatType.FAT32) { Panic.Error("Path refers to a directory"); return null; }
            return res.FirstCluster >= 2 ? ReadAllClusterChain(res.FirstCluster, res.Size) : new byte[0];
        }

        public override void WriteAllBytes(string Name, byte[] Content) {
            // resolve parent
            string parent = Name.LastIndexOf('/') >= 0 ? Name.Substring(0, Name.LastIndexOf('/')) : "";
            string just = Name.Substring(Name.LastIndexOf('/') + 1);
            uint parentCluster = _type == FatType.FAT32 ? _rootCluster : 0;
            if (parent.Length > 0) { 
                var pr = FindPath(parent); 
                if (!pr.Found) {
                    // Parent directory not found - cannot write file
                    return;
                }
                parentCluster = pr.FirstCluster; 
            }
            // locate entry or free slot
            bool exists; var loc = FindEntryLoc(parentCluster, just, true, out exists);
            if (!loc.Found && !exists) { 
                EnsureDirHasFreeSlot(parentCluster, ref loc); 
                if (!loc.Found) {
                    // No space available in directory
                    return;
                }
            }
            uint firstClus = 0;
            if (exists) {
                // free old chain
                uint old = ((uint)loc.Entry.FstClusHI << 16) | loc.Entry.FstClusLO; if (old >= 2) FreeClusterChain(old);
            }
            // allocate if needed
            List<uint> chain = AllocateClustersForSize((uint)Content.Length);
            if (Content.Length > 0 && chain.Count == 0) {
                // Allocation failed - out of space
                return;
            }
            if (chain.Count > 0) firstClus = chain[0];
            // write data
            if (Content.Length > 0) WriteDataToChain(chain, Content);
            // write dir entry
            string shortName = GenerateShortName(just);
            WriteDirEntry(loc, shortName, firstClus, (uint)Content.Length, 0x20);
            // cleanup
            parent.Dispose(); just.Dispose(); shortName.Dispose();
        }

        public override void Format() {
            // Format the disk as FAT32
            // Get total sectors from disk
            ulong totalSectors = 0;
            
            // Try to read boot sector to get disk size, or use a default
            byte[] testSec = new byte[512];
            try {
                testSec = ReadSectorsCached(0, 1);
                // If we can read it, try to get total sectors from existing BPB
                fixed (byte* p = testSec) {
                    BPB_Common* bpb = (BPB_Common*)p;
                    totalSectors = bpb->TotSec16 != 0 ? bpb->TotSec16 : bpb->TotSec32;
                }
            } catch {
                // If we can't determine size, default to 100MB
                totalSectors = (100 * 1024 * 1024) / 512;
            }
            
            if (totalSectors == 0 || totalSectors < 4096) {
                totalSectors = (100 * 1024 * 1024) / 512; // Default to 100MB
            }
            
            // Choose FAT type based on size
            bool useFAT32 = totalSectors > 65525 * 8; // > ~256MB
            ushort bytesPerSec = 512;
            byte secPerClus = 8; // 4KB clusters
            ushort rsvdSecCnt = (ushort)(useFAT32 ? 32 : 1);
            byte numFATs = 2;
            ushort rootEntCnt = (ushort)(useFAT32 ? 0 : 512);
            uint rootDirSectors = (uint)((rootEntCnt * 32 + (bytesPerSec - 1)) / bytesPerSec);
            
            // Calculate FAT size
            uint tmpVal1 = (uint)(totalSectors - (rsvdSecCnt + rootDirSectors));
            uint tmpVal2 = (uint)((256 * secPerClus) + numFATs);
            if (useFAT32) tmpVal2 = (uint)((128 * secPerClus) + numFATs);
            uint FATSz = (tmpVal1 + tmpVal2 - 1) / tmpVal2;
            
            // Create boot sector
            byte[] bootSector = new byte[512];
            for (int i = 0; i < 512; i++) bootSector[i] = 0;
            
            fixed (byte* p = bootSector) {
                BPB_Common* bpb = (BPB_Common*)p;
                
                // Jump instruction
                bpb->jmpBoot[0] = 0xEB;
                bpb->jmpBoot[1] = 0x58;
                bpb->jmpBoot[2] = 0x90;
                
                // OEM name
                string oem = "GUIDEXOS";
                for (int i = 0; i < 8; i++) bpb->OEMName[i] = (byte)(i < oem.Length ? oem[i] : ' ');
                
                bpb->BytsPerSec = bytesPerSec;
                bpb->SecPerClus = secPerClus;
                bpb->RsvdSecCnt = rsvdSecCnt;
                bpb->NumFATs = numFATs;
                bpb->RootEntCnt = rootEntCnt;
                bpb->TotSec16 = (ushort)(totalSectors < 65536 && !useFAT32 ? totalSectors : 0);
                bpb->Media = 0xF8;
                bpb->FATSz16 = (ushort)(useFAT32 ? 0 : FATSz);
                bpb->SecPerTrk = 63;
                bpb->NumHeads = 255;
                bpb->HiddSec = 0;
                bpb->TotSec32 = (uint)(totalSectors >= 65536 || useFAT32 ? totalSectors : 0);
                
                if (useFAT32) {
                    BPB_FAT32* bpb32 = (BPB_FAT32*)(p + 0x24);
                    bpb32->FATSz32 = FATSz;
                    bpb32->ExtFlags = 0;
                    bpb32->FSVer = 0;
                    bpb32->RootClus = 2;
                    bpb32->FSInfo = 1;
                    bpb32->BkBootSec = 6;
                    bpb32->DrvNum = 0x80;
                    bpb32->BootSig = 0x29;
                    bpb32->VolID = 0x12345678;
                    
                    string label = "GUIDEXOS   ";
                    for (int i = 0; i < 11; i++) bpb32->VolLab[i] = (byte)(i < label.Length ? label[i] : ' ');
                    
                    string fstype = "FAT32   ";
                    for (int i = 0; i < 8; i++) bpb32->FilSysType[i] = (byte)(i < fstype.Length ? fstype[i] : ' ');
                }
                
                // Boot signature
                bootSector[510] = 0x55;
                bootSector[511] = 0xAA;
            }
            
            // Write boot sector
            WriteSector(0, bootSector);
            
            // Clear reserved sectors
            byte[] zeroSec = new byte[512];
            for (int i = 0; i < 512; i++) zeroSec[i] = 0;
            
            for (ulong s = 1; s < rsvdSecCnt; s++) {
                WriteSector(s, zeroSec);
            }
            
            // Write FSInfo for FAT32
            if (useFAT32) {
                byte[] fsInfo = new byte[512];
                for (int i = 0; i < 512; i++) fsInfo[i] = 0;
                fsInfo[0] = (byte)'R';
                fsInfo[1] = (byte)'R';
                fsInfo[2] = (byte)'a';
                fsInfo[3] = (byte)'A';
                fsInfo[484] = 0xFF; 
                fsInfo[485] = 0xFF; 
                fsInfo[486] = 0xFF; 
                fsInfo[487] = 0xFF;
                fsInfo[488] = 0xFF; 
                fsInfo[489] = 0xFF; 
                fsInfo[490] = 0xFF; 
                fsInfo[491] = 0xFF;
                fsInfo[510] = 0x55;
                fsInfo[511] = 0xAA;
                WriteSector(1, fsInfo);
            }
            
            // Initialize FAT tables
            ulong fatStart = rsvdSecCnt;
            for (int fatNum = 0; fatNum < numFATs; fatNum++) {
                for (uint i = 0; i < FATSz; i++) {
                    WriteSector(fatStart + (ulong)(fatNum * FATSz) + i, zeroSec);
                }
            }
            
            // Write initial FAT entries
            byte[] fatFirst = new byte[512];
            for (int i = 0; i < 512; i++) fatFirst[i] = 0;
            
            if (useFAT32) {
                // Media descriptor
                fatFirst[0] = 0xF8; fatFirst[1] = 0xFF; fatFirst[2] = 0xFF; fatFirst[3] = 0x0F;
                // EOC for cluster 1
                fatFirst[4] = 0xFF; fatFirst[5] = 0xFF; fatFirst[6] = 0xFF; fatFirst[7] = 0x0F;
                // Root directory cluster (2)
                fatFirst[8] = 0xFF; fatFirst[9] = 0xFF; fatFirst[10] = 0xFF; fatFirst[11] = 0x0F;
            } else {
                // FAT12/16
                fatFirst[0] = 0xF8;
                fatFirst[1] = 0xFF;
                fatFirst[2] = 0xFF;
                if (!useFAT32 && totalSectors >= 4085) {
                    // FAT16
                    fatFirst[3] = 0xFF;
                }
            }
            
            // Write to all FAT copies
            for (int fatNum = 0; fatNum < numFATs; fatNum++) {
                WriteSector(fatStart + (ulong)(fatNum * FATSz), fatFirst);
            }
            
            // Clear root directory area
            ulong firstRootSec = fatStart + (ulong)(numFATs * FATSz);
            if (useFAT32) {
                // Root is in data area - clear cluster 2
                ulong firstDataSector = firstRootSec;
                for (int i = 0; i < secPerClus; i++) {
                    WriteSector(firstDataSector + (ulong)i, zeroSec);
                }
            } else {
                // Fixed root directory for FAT12/16
                for (uint i = 0; i < rootDirSectors; i++) {
                    WriteSector(firstRootSec + i, zeroSec);
                }
            }
            
            // Re-initialize this FAT instance with the new filesystem
            _cacheKeys = new ulong[CacheCapacity];
            _cacheValues = new byte[CacheCapacity][];
            _cacheCount = 0;
            _lruHead = 0;
            
            var sec0 = ReadSectorsCached(0, 1);
            fixed (byte* p = sec0) {
                BPB_Common* bpb = (BPB_Common*)p;
                _bytesPerSec = bpb->BytsPerSec;
                _secPerClus = bpb->SecPerClus;
                _rsvdSecCnt = bpb->RsvdSecCnt;
                _numFATs = bpb->NumFATs;
                uint totSec = bpb->TotSec16 != 0 ? bpb->TotSec16 : bpb->TotSec32;
                uint fatsz = bpb->FATSz16;
                if (fatsz == 0) {
                    BPB_FAT32* bpb32 = (BPB_FAT32*)(p + 0x24);
                    fatsz = bpb32->FATSz32;
                    _rootCluster = bpb32->RootClus;
                }
                _FATSz = fatsz;
                uint rootEntCnt2 = bpb->RootEntCnt;
                _rootDirSectors = (uint)((rootEntCnt2 * 32 + (_bytesPerSec - 1)) / _bytesPerSec);
                uint dataSec = totSec - (uint)(_rsvdSecCnt + (_numFATs * fatsz) + _rootDirSectors);
                uint countOfClusters = dataSec / _secPerClus;
                _clusterCount = countOfClusters;
                _type = countOfClusters < 4085 ? FatType.FAT12 : (countOfClusters < 65525 ? FatType.FAT16 : FatType.FAT32);
                _fatStart = _rsvdSecCnt;
                _firstDataSector = (uint)(_rsvdSecCnt + (_numFATs * fatsz) + _rootDirSectors);
                _firstRootDirSector = (uint)(_rsvdSecCnt + (_numFATs * fatsz));
                if (_type != FatType.FAT32) _rootCluster = 0;
            }
        }
    }
}