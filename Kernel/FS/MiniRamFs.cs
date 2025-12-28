using System.Collections.Generic;
namespace guideXOS.FS {
    /// <summary>
    /// Minimal in-place filesystem implementation for an in-memory disk.
    /// Intended for a kernel-provided RAM disk on UEFI boot.
    ///
    /// Constraints:
    /// - Sector size: 512
    /// - Fixed directory table: 128 entries
    /// - Filename max: 32 ASCII bytes
    /// - Simple contiguous allocator (append-only); overwrites allocate new space
    ///
    /// On-disk format:
    /// Sector 0: Superblock
    ///   0x00 u32 Magic 'MRFS' (0x4D524653)
    ///   0x04 u32 SectorSize (512)
    ///   0x08 u32 DirEntries (128)
    ///   0x0C u32 DataStartSector
    ///   0x10 u32 NextFreeSector
    /// Directory table: DirEntries * 64 bytes (rounded up to whole sectors)
    /// DirEntry (64 bytes):
    ///   0x00 u8  Used
    ///   0x01 char[32] Name (null-terminated ASCII)
    ///   0x21 u32 StartSector
    ///   0x25 i32 LengthBytes
    ///   rest reserved
    /// </summary>
    internal sealed unsafe class MiniRamFs : FileSystem {
        /// <summary>
        /// Sector Size in bytes.
        /// </summary>
        public const int SectorSz = 512;
        /// <summary>
        /// Magic
        /// </summary>
        private const uint Magic = 0x4D524653; // 'MRFS'
        /// <summary>
        /// Max Name
        /// </summary>
        private const int MaxName = 32;
        /// <summary>
        /// Directory Entries
        /// </summary>
        private const int DirEntries = 128;
        /// <summary>
        /// Directory Entry Size
        /// </summary>
        private const int DirEntrySize = 64;
        /// <summary>
        /// Superblock Sectors
        /// </summary>
        private const int SuperblockSectors = 1;
        /// <summary>
        /// Disk
        /// </summary>
        private readonly Disk _disk;
        /// <summary>
        /// Data Start Sector
        /// </summary>
        private int _dataStartSector;
        /// <summary>
        /// Next Free Sector
        /// </summary>
        private int _nextFreeSector;
        /// <summary>
        /// MiniRamFs Constructor
        /// </summary>
        /// <param name="disk"></param>
        public MiniRamFs(Disk disk) {
            _disk = disk;
        }
        /// <summary>
        /// Format
        /// </summary>
        public override void Format() {
            // Compute layout
            int dirBytes = DirEntries * DirEntrySize;
            int dirSectors = (dirBytes + SectorSz - 1) / SectorSz;
            _dataStartSector = SuperblockSectors + dirSectors;
            _nextFreeSector = _dataStartSector;

            // Zero sector 0 + directory sectors
            var zero = new byte[SectorSz];
            Zero(zero);
            fixed (byte* pz = zero) {
                for (ulong s = 0; s < (ulong)_dataStartSector; s++) {
                    _disk.Write(s, 1, pz);
                }
            }

            WriteSuperblock();
        }
        /// <summary>
        /// Mount
        /// </summary>
        public void Mount() {
            if (!ReadAndValidateSuperblock(out _dataStartSector, out _nextFreeSector)) {
                // If invalid, leave unmounted.
                _dataStartSector = 0;
                _nextFreeSector = 0;
            }
        }

        public override List<FileInfo> GetFiles(string directory) {
            if (directory == null) directory = "";
            if (directory.Length != 0 && directory != "/") return new List<FileInfo>();

            var list = new List<FileInfo>();
            for (int i = 0; i < DirEntries; i++) {
                var e = ReadDirEntry(i);
                if (e.Used == 0) continue;
                list.Add(new FileInfo { Name = e.Name, Attribute = FileAttribute.Archive, Param0 = (ulong)e.LengthBytes, Param1 = (ulong)e.StartSector });
            }
            return list;
        }

        public override void Delete(string name) {
            string just = NormalizeName(name);
            if (just.Length == 0) return;
            if (!FindEntryByName(just, out int idx)) return;

            var e = ReadDirEntry(idx);
            e.Used = 0;
            e.Name = "";
            e.StartSector = 0;
            e.LengthBytes = 0;
            WriteDirEntry(idx, e);
        }

        public override byte[] ReadAllBytes(string name) {
            string just = NormalizeName(name);
            if (just.Length == 0) return new byte[0];
            if (!FindEntryByName(just, out int idx)) return new byte[0];

            var e = ReadDirEntry(idx);
            if (e.LengthBytes <= 0) return new byte[0];

            byte[] result = new byte[e.LengthBytes];
            int remaining = e.LengthBytes;
            int dstOff = 0;
            ulong sector = (ulong)e.StartSector;

            var buf = new byte[SectorSz];
            fixed (byte* p = buf) {
                while (remaining > 0) {
                    _disk.Read(sector, 1, p);
                    int toCopy = remaining > SectorSz ? SectorSz : remaining;
                    CopyBytes(buf, 0, result, dstOff, toCopy);
                    remaining -= toCopy;
                    dstOff += toCopy;
                    sector++;
                }
            }

            return result;
        }

        public override void WriteAllBytes(string name, byte[] content) {
            if (content == null) content = new byte[0];
            string just = NormalizeName(name);
            if (just.Length == 0) return;

            int idx;
            if (!FindEntryByName(just, out idx)) {
                idx = FindFreeDirEntry();
                if (idx < 0) return; // directory full
            }

            int sectorsNeeded = (content.Length + SectorSz - 1) / SectorSz;
            if (sectorsNeeded == 0) sectorsNeeded = 1;

            int start = AllocateSectors(sectorsNeeded);

            var buf = new byte[SectorSz];
            fixed (byte* p = buf) {
                int srcOff = 0;
                for (int s = 0; s < sectorsNeeded; s++) {
                    Zero(buf);
                    int toCopy = content.Length - srcOff;
                    if (toCopy > SectorSz) toCopy = SectorSz;
                    if (toCopy > 0) CopyBytes(content, srcOff, buf, 0, toCopy);
                    _disk.Write((ulong)(start + s), 1, p);
                    srcOff += toCopy;
                }
            }

            WriteDirEntry(idx, new DirEntry { Used = 1, Name = just, StartSector = start, LengthBytes = content.Length });
            PersistAllocatorState();
        }

        public void WriteAt(string name, int fileOffset, byte[] data) {
            if (fileOffset < 0) return;
            if (data == null) data = new byte[0];

            var current = ReadAllBytes(name);
            int newLen = current.Length;
            if (fileOffset + data.Length > newLen) newLen = fileOffset + data.Length;

            var merged = new byte[newLen];
            if (current.Length > 0) CopyBytes(current, 0, merged, 0, current.Length);
            if (data.Length > 0) CopyBytes(data, 0, merged, fileOffset, data.Length);
            WriteAllBytes(name, merged);
        }

        // ---- on-disk IO helpers ----

        private void WriteSuperblock() {
            var sb = new byte[SectorSz];
            Zero(sb);
            WriteU32(sb, 0, Magic);
            WriteU32(sb, 4, SectorSz);
            WriteU32(sb, 8, DirEntries);
            WriteU32(sb, 12, (uint)_dataStartSector);
            WriteU32(sb, 16, (uint)_nextFreeSector);
            sb[510] = 0x55;
            sb[511] = 0xAA;
            fixed (byte* p = sb) _disk.Write(0, 1, p);
        }

        private bool ReadAndValidateSuperblock(out int dataStartSector, out int nextFreeSector) {
            dataStartSector = 0;
            nextFreeSector = 0;

            var sb = new byte[SectorSz];
            fixed (byte* p = sb) _disk.Read(0, 1, p);

            if (ReadU32(sb, 0) != Magic) return false;
            if (ReadU32(sb, 4) != SectorSz) return false;
            if (ReadU32(sb, 8) != DirEntries) return false;

            dataStartSector = (int)ReadU32(sb, 12);
            nextFreeSector = (int)ReadU32(sb, 16);
            if (dataStartSector <= 0 || nextFreeSector < dataStartSector) return false;
            return true;
        }

        private void PersistAllocatorState() {
            var sb = new byte[SectorSz];
            fixed (byte* p = sb) _disk.Read(0, 1, p);
            WriteU32(sb, 16, (uint)_nextFreeSector);
            fixed (byte* p2 = sb) _disk.Write(0, 1, p2);
        }

        private int AllocateSectors(int count) {
            if (count <= 0) count = 1;
            int start = _nextFreeSector;
            _nextFreeSector += count;
            return start;
        }

        private int FindFreeDirEntry() {
            for (int i = 0; i < DirEntries; i++) {
                var e = ReadDirEntry(i);
                if (e.Used == 0) return i;
            }
            return -1;
        }

        private bool FindEntryByName(string name, out int index) {
            for (int i = 0; i < DirEntries; i++) {
                var e = ReadDirEntry(i);
                if (e.Used == 0) continue;
                if (e.Name == name) { index = i; return true; }
            }
            index = -1;
            return false;
        }

        private int DirTableStartSector => SuperblockSectors;

        private DirEntry ReadDirEntry(int index) {
            if ((uint)index >= DirEntries) return default;

            int byteIndex = index * DirEntrySize;
            int sector = DirTableStartSector + (byteIndex / SectorSz);
            int off = byteIndex % SectorSz;

            var buf = new byte[SectorSz];
            fixed (byte* p = buf) _disk.Read((ulong)sector, 1, p);

            byte used = buf[off + 0];
            string name = ReadFixedString(buf, off + 1, MaxName);
            uint startSector = ReadU32(buf, off + 33);
            int len = ReadI32(buf, off + 37);

            return new DirEntry { Used = used, Name = name, StartSector = (int)startSector, LengthBytes = len };
        }

        private void WriteDirEntry(int index, DirEntry entry) {
            if ((uint)index >= DirEntries) return;

            int byteIndex = index * DirEntrySize;
            int sector = DirTableStartSector + (byteIndex / SectorSz);
            int off = byteIndex % SectorSz;

            var buf = new byte[SectorSz];
            fixed (byte* p = buf) _disk.Read((ulong)sector, 1, p);

            buf[off + 0] = entry.Used;
            WriteFixedString(buf, off + 1, MaxName, entry.Name);
            WriteU32(buf, off + 33, (uint)entry.StartSector);
            WriteI32(buf, off + 37, entry.LengthBytes);
            for (int i = off + 41; i < off + DirEntrySize; i++) buf[i] = 0;

            fixed (byte* p2 = buf) _disk.Write((ulong)sector, 1, p2);
        }

        private static string NormalizeName(string name) {
            if (name == null) return "";
            // Keep last segment.
            int slash = name.LastIndexOf('/');
            if (slash >= 0) name = name.Substring(slash + 1);
            if (name.Length == 0) return "";
            if (name.Length > MaxName) return "";
            // Disallow directories/backslashes without string.Contains
            for (int i = 0; i < name.Length; i++) {
                char c = name[i];
                if (c == '/' || c == '\\' || c == '\0') return "";
            }
            return name;
        }

        // Helper functions must be declared before use in this codebase's compiler settings.
        private static void Zero(byte[] b) { for (int i = 0; i < b.Length; i++) b[i] = 0; }
        private static void CopyBytes(byte[] src, int srcOff, byte[] dst, int dstOff, int len) {
            for (int i = 0; i < len; i++) dst[dstOff + i] = src[srcOff + i];
        }

        private static void WriteU32(byte[] buf, int off, uint v) {
            buf[off + 0] = (byte)(v);
            buf[off + 1] = (byte)(v >> 8);
            buf[off + 2] = (byte)(v >> 16);
            buf[off + 3] = (byte)(v >> 24);
        }

        private static uint ReadU32(byte[] buf, int off) {
            return (uint)(buf[off] | (buf[off + 1] << 8) | (buf[off + 2] << 16) | (buf[off + 3] << 24));
        }
        /// <summary>
        /// Write I32
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="off"></param>
        /// <param name="v"></param>
        private static void WriteI32(byte[] buf, int off, int v) => WriteU32(buf, off, unchecked((uint)v));
        /// <summary>
        /// Read I32
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="off"></param>
        /// <returns></returns>
        private static int ReadI32(byte[] buf, int off) => unchecked((int)ReadU32(buf, off));
        /// <summary>
        /// Read Fixed String
        /// </summary>
        /// <param name="arr"></param>
        /// <param name="off"></param>
        /// <param name="maxLen"></param>
        /// <returns></returns>
        private static string ReadFixedString(byte[] arr, int off, int maxLen) {
            int len = 0;
            for (; len < maxLen; len++) {
                if (arr[off + len] == 0) break;
            }
            // ASCII only to keep kernel-friendly.
            char[] chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = (char)(arr[off + i] & 0x7F);
            return new string(chars);
        }
        /// <summary>
        /// Write Fixed String
        /// </summary>
        /// <param name="arr"></param>
        /// <param name="off"></param>
        /// <param name="maxLen"></param>
        /// <param name="s"></param>
        private static void WriteFixedString(byte[] arr, int off, int maxLen, string s) {
            if (s == null) s = "";
            if (s.Length > maxLen) return;
            for (int i = 0; i < maxLen; i++) arr[off + i] = 0;
            for (int i = 0; i < s.Length; i++) arr[off + i] = (byte)(s[i] & 0x7F);
        }
        /// <summary>
        /// Directory entry structure.
        /// </summary>
        private struct DirEntry {
            /// <summary>
            /// Used
            /// </summary>
            public byte Used;
            /// <summary>
            /// Name
            /// </summary>
            public string Name;
            /// <summary>
            /// Start Sector
            /// </summary>
            public int StartSector;
            /// <summary>
            /// Length Bytes
            /// </summary>
            public int LengthBytes;
        }
    }
}