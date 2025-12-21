using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Collections.Generic;

namespace guideXOS.FS {
    // Minimal EXT2/EXT3/EXT4 (no extents) reader. Read-mostly with basic overwrite support.
    // Supported writes:
    // - Overwrite existing regular files without growing beyond already allocated blocks (direct + single-indirect)
    // Not supported yet:
    // - Creating new files/directories
    // - Growing files that require new block/inode allocations
    // - Deleting files
    internal unsafe class EXT2 : FileSystem {
        private const ushort EXT_MAGIC = 0xEF53;
        private const ushort S_IFDIR = 0x4000;
        private const ushort S_IFREG = 0x8000;

        private Disk _disk;
        private Disk disk => _disk ?? Disk.Instance;

        private uint _blockSize;
        private uint _inodesPerGroup;
        private uint _inodeSize;
        private uint _blocksPerGroup;
        private uint _firstDataBlock;
        private uint _totalInodes;
        private uint _totalBlocks;
        private uint _groupCount;
        private uint[] _inodeTableBlock; // per group

        public EXT2(Disk disk)
        {
            _disk = disk;
            // Read superblock at byte offset 1024 (LBA +2 for 512B sectors)
            var sb = ReadBytes(2048, 1024); // read 1024 bytes from 1024 offset
            ushort magic = ReadU16(sb, 0x38);
            if (magic != EXT_MAGIC) Panic.Error("EXT2: Invalid superblock magic");

            _totalInodes = ReadU32(sb, 0x00);
            _totalBlocks = ReadU32(sb, 0x04);
            _firstDataBlock = ReadU32(sb, 0x14);
            uint logBlockSize = ReadU32(sb, 0x18);
            _blockSize = 1024u << (int)logBlockSize;
            _blocksPerGroup = ReadU32(sb, 0x20);
            _inodesPerGroup = ReadU32(sb, 0x28);
            _inodeSize = ReadU16(sb, 0x58);
            if (_inodeSize == 0) _inodeSize = 128; // old rev

            _groupCount = DivRoundUp(_totalBlocks - _firstDataBlock, _blocksPerGroup);

            // Read group descriptor table. For 1KiB blocks it starts at block 2, otherwise at block 1
            uint gdtBlock = _blockSize == 1024 ? 2u : 1u;
            int gdtBytes = (int)(_groupCount * 32); // ext2/3 32B descriptors
            var gdt = ReadBlockRange(gdtBlock, (uint)DivRoundUp((uint)gdtBytes, _blockSize));
            _inodeTableBlock = new uint[_groupCount];
            for (uint i = 0; i < _groupCount; i++)
            {
                int off = (int)(i * 32);
                _inodeTableBlock[i] = ReadU32(gdt, off + 8); // bg_inode_table
            }
        }

        public EXT2() {
            // Read superblock at byte offset 1024 (LBA +2 for 512B sectors)
            var sb = ReadBytes(2048, 1024); // read 1024 bytes from 1024 offset
            ushort magic = ReadU16(sb, 0x38);
            if (magic != EXT_MAGIC) Panic.Error("EXT2: Invalid superblock magic");

            _totalInodes = ReadU32(sb, 0x00);
            _totalBlocks = ReadU32(sb, 0x04);
            _firstDataBlock = ReadU32(sb, 0x14);
            uint logBlockSize = ReadU32(sb, 0x18);
            _blockSize = 1024u << (int)logBlockSize;
            _blocksPerGroup = ReadU32(sb, 0x20);
            _inodesPerGroup = ReadU32(sb, 0x28);
            _inodeSize = ReadU16(sb, 0x58);
            if (_inodeSize == 0) _inodeSize = 128; // old rev

            _groupCount = DivRoundUp(_totalBlocks - _firstDataBlock, _blocksPerGroup);

            // Read group descriptor table. For 1KiB blocks it starts at block 2, otherwise at block 1
            uint gdtBlock = _blockSize == 1024 ? 2u : 1u;
            int gdtBytes = (int)(_groupCount * 32); // ext2/3 32B descriptors
            var gdt = ReadBlockRange(gdtBlock, (uint)DivRoundUp((uint)gdtBytes, _blockSize));
            _inodeTableBlock = new uint[_groupCount];
            for (uint i = 0; i < _groupCount; i++) {
                int off = (int)(i * 32);
                _inodeTableBlock[i] = ReadU32(gdt, off + 8); // bg_inode_table
            }
        }

        private static uint DivRoundUp(uint a, uint b) => (a + b - 1) / b;
        private static uint DivRoundUp(ulong a, uint b) => (uint)((a + b - 1) / b);

        private byte[] ReadBytes(ulong byteOffset, uint count) {
            // Disk sector is 512. Align to sector.
            ulong startSector = byteOffset / 512u;
            uint sectorOffset = (uint)(byteOffset % 512u);
            uint total = sectorOffset + count;
            uint sectorsToRead = DivRoundUp(total, 512u);
            var buf = new byte[sectorsToRead * 512u];
            fixed (byte* p = buf) disk.Read(startSector, sectorsToRead, p);
            // slice
            var outb = new byte[count];
            for (uint i = 0; i < count; i++) outb[i] = buf[sectorOffset + i];
            return outb;
        }

        private void WriteBytes(ulong byteOffset, byte[] data) {
            // Read-modify-write covering sectors touching the region
            ulong startSector = byteOffset / 512u;
            uint sectorOffset = (uint)(byteOffset % 512u);
            uint total = sectorOffset + (uint)data.Length;
            uint sectors = DivRoundUp(total, 512u);
            var buf = new byte[sectors * 512u];
            fixed (byte* p = buf) disk.Read(startSector, sectors, p);
            // patch
            for (int i = 0; i < data.Length; i++) buf[sectorOffset + i] = data[i];
            fixed (byte* p2 = buf) disk.Write(startSector, sectors, p2);
        }

        private byte[] ReadBlock(uint block) {
            ulong byteOffset = (ulong)block * _blockSize;
            uint sectors = _blockSize / 512u;
            var buf = new byte[_blockSize];
            fixed (byte* p = buf) disk.Read(byteOffset / 512u, sectors, p);
            return buf;
        }

        private void WriteBlock(uint block, byte[] data) {
            var buf = new byte[_blockSize];
            int toCopy = data.Length < buf.Length ? data.Length : buf.Length;
            for (int i = 0; i < toCopy; i++) buf[i] = data[i];
            for (int i = toCopy; i < buf.Length; i++) buf[i] = 0;
            ulong byteOffset = (ulong)block * _blockSize;
            uint sectors = _blockSize / 512u;
            fixed (byte* p = buf) disk.Write(byteOffset / 512u, sectors, p);
        }

        private byte[] ReadBlockRange(uint startBlock, uint countBlocks) {
            ulong byteOffset = (ulong)startBlock * _blockSize;
            uint sectors = (uint)((ulong)_blockSize * countBlocks / 512u);
            var buf = new byte[_blockSize * countBlocks];
            fixed (byte* p = buf) disk.Read(byteOffset / 512u, sectors, p);
            return buf;
        }

        private static ushort ReadU16(byte[] b, int off) { return (ushort)(b[off] | (b[off + 1] << 8)); }
        private static uint ReadU32(byte[] b, int off) { return (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24)); }
        private static void WriteU16(byte[] b, int off, ushort v) { b[off] = (byte)(v & 0xFF); b[off + 1] = (byte)((v >> 8) & 0xFF); }
        private static void WriteU32(byte[] b, int off, uint v) { b[off] = (byte)(v & 0xFF); b[off + 1] = (byte)((v >> 8) & 0xFF); b[off + 2] = (byte)((v >> 16) & 0xFF); b[off + 3] = (byte)((v >> 24) & 0xFF); }

        private struct Inode {
            public ushort Mode;
            public uint SizeLo;
            public uint[] Blocks; // 15 entries
        }

        private Inode ReadInode(uint inodeNum) {
            if (inodeNum == 0) return default;
            uint idx = inodeNum - 1;
            uint group = idx / _inodesPerGroup;
            uint indexInGroup = idx % _inodesPerGroup;
            uint tableBlock = _inodeTableBlock[group];
            ulong byteOffset = ((ulong)tableBlock * _blockSize) + (ulong)indexInGroup * _inodeSize;
            var raw = ReadBytes(byteOffset, _inodeSize);
            Inode ino = new Inode();
            ino.Mode = ReadU16(raw, 0x00);
            ino.SizeLo = ReadU32(raw, 0x04);
            ino.Blocks = new uint[15];
            int bOff = 0x28; // i_block offset
            for (int i = 0; i < 15; i++) ino.Blocks[i] = ReadU32(raw, bOff + i * 4);
            return ino;
        }

        private void WriteInode(uint inodeNum, Inode ino) {
            uint idx = inodeNum - 1;
            uint group = idx / _inodesPerGroup;
            uint indexInGroup = idx % _inodesPerGroup;
            uint tableBlock = _inodeTableBlock[group];
            ulong byteOffset = ((ulong)tableBlock * _blockSize) + (ulong)indexInGroup * _inodeSize;
            var raw = ReadBytes(byteOffset, _inodeSize);
            WriteU16(raw, 0x00, ino.Mode);
            WriteU32(raw, 0x04, ino.SizeLo);
            int bOff = 0x28;
            for (int i = 0; i < 15; i++) WriteU32(raw, bOff + i * 4, ino.Blocks[i]);
            WriteBytes(byteOffset, raw);
        }

        private bool IsDir(Inode ino) => (ino.Mode & S_IFDIR) == S_IFDIR;
        private bool IsReg(Inode ino) => (ino.Mode & S_IFREG) == S_IFREG;

        private struct DirEnt { public string Name; public uint Inode; public byte Type; }

        private List<DirEnt> EnumDirectory(Inode dirIno) {
            List<DirEnt> list = new List<DirEnt>();
            // iterate direct and single-indirect only for directories
            int maxPtrs = 12; // directories typically small; avoid indirect unless needed
            for (int i = 0; i < maxPtrs; i++) {
                uint blk = dirIno.Blocks[i]; if (blk == 0) continue;
                var buf = ReadBlock(blk);
                int off = 0;
                while (off + 8 <= buf.Length) {
                    uint ino = ReadU32(buf, off + 0);
                    ushort rec_len = ReadU16(buf, off + 4);
                    byte name_len = buf[off + 6];
                    byte file_type = buf[off + 7];
                    if (rec_len == 0) break;
                    if (ino != 0 && name_len > 0 && off + 8 + name_len <= buf.Length) {
                        string name = "";
                        for (int c = 0; c < name_len; c++) name += (char)buf[off + 8 + c];
                        if (name != "." && name != "..") list.Add(new DirEnt { Name = name, Inode = ino, Type = file_type });
                    }
                    off += rec_len;
                }
            }
            // Single-indirect if present
            uint sib = dirIno.Blocks[12];
            if (sib != 0) {
                var ind = ReadBlock(sib);
                for (int i = 0; i + 4 <= ind.Length; i += 4) {
                    uint blk = ReadU32(ind, i); if (blk == 0) continue;
                    var buf = ReadBlock(blk);
                    int off = 0;
                    while (off + 8 <= buf.Length) {
                        uint ino = ReadU32(buf, off + 0);
                        ushort rec_len = ReadU16(buf, off + 4);
                        byte name_len = buf[off + 6];
                        byte file_type = buf[off + 7];
                        if (rec_len == 0) break;
                        if (ino != 0 && name_len > 0 && off + 8 + name_len <= buf.Length) {
                            string name = "";
                            for (int c = 0; c < name_len; c++) name += (char)buf[off + 8 + c];
                            if (name != "." && name != "..") list.Add(new DirEnt { Name = name, Inode = ino, Type = file_type });
                        }
                        off += rec_len;
                    }
                }
            }
            return list;
        }

        private uint ResolvePathToInode(string path) {
            // normalize
            string p = path;
            while (p.Length > 0 && p[0] == '/') p = p.Substring(1);
            if (p.Length == 0) return 2; // root inode
            var parts = p.Split('/');
            uint curIno = 2;
            for (int i = 0; i < parts.Length; i++) {
                string part = parts[i];
                var cur = ReadInode(curIno);
                if (!IsDir(cur)) return 0;
                bool found = false;
                var ents = EnumDirectory(cur);
                for (int e = 0; e < ents.Count; e++) {
                    if (StrEq(ents[e].Name, part)) { curIno = ents[e].Inode; found = true; break; }
                }
                if (!found) return 0;
            }
            return curIno;
        }

        private static bool StrEq(string a, string b) {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public override List<FileInfo> GetFiles(string Directory) {
            var list = new List<FileInfo>();
            uint inoNum = ResolvePathToInode(Directory);
            if (inoNum == 0) return list;
            var dir = ReadInode(inoNum);
            if (!IsDir(dir)) return list;
            var entries = EnumDirectory(dir);
            for (int i = 0; i < entries.Count; i++) {
                var e = entries[i];
                var child = ReadInode(e.Inode);
                var fi = new FileInfo();
                fi.Name = e.Name;
                if (IsDir(child)) fi.Attribute |= FileAttribute.Directory;
                fi.Param0 = e.Inode;
                fi.Param1 = child.SizeLo;
                list.Add(fi);
            }
            return list;
        }

        public override void Delete(string Name) { Panic.Error("EXT2: Delete not supported"); }

        public override byte[] ReadAllBytes(string Name) {
            uint inoNum = ResolvePathToInode(Name);
            if (inoNum == 0) { Panic.Error($"{Name} is not found!"); return null; }
            var ino = ReadInode(inoNum);
            if (!IsReg(ino)) { Panic.Error("EXT2: Not a regular file"); return null; }
            uint size = ino.SizeLo;
            var data = new byte[size];
            int written = 0;
            // Direct blocks
            for (int i = 0; i < 12 && written < size; i++) {
                uint blk = ino.Blocks[i]; if (blk == 0) break;
                var buf = ReadBlock(blk);
                int toCopy = (int)((uint)buf.Length < (size - (uint)written) ? (uint)buf.Length : (size - (uint)written));
                Copy(buf, 0, data, written, toCopy);
                written += toCopy;
            }
            if (written >= size) return data;
            // Single indirect
            uint ind = ino.Blocks[12];
            if (ind != 0 && written < size) {
                var ib = ReadBlock(ind);
                for (int i = 0; i + 4 <= ib.Length && written < size; i += 4) {
                    uint blk = ReadU32(ib, i); if (blk == 0) continue;
                    var buf = ReadBlock(blk);
                    int toCopy = (int)((uint)buf.Length < (size - (uint)written) ? (uint)buf.Length : (size - (uint)written));
                    Copy(buf, 0, data, written, toCopy);
                    written += toCopy;
                }
            }
            // Double/Triple indirect not supported
            return data;
        }

        public override void WriteAllBytes(string Name, byte[] Content) {
            // Only overwrite existing files without growing beyond allocated capacity
            uint inoNum = ResolvePathToInode(Name);
            if (inoNum == 0) { Panic.Error("EXT2: Creating new files is not supported yet"); return; }
            var ino = ReadInode(inoNum);
            if (!IsReg(ino)) { Panic.Error("EXT2: Not a regular file"); return; }

            // Collect data blocks (direct + single-indirect)
            List<uint> blocks = new List<uint>();
            for (int i = 0; i < 12; i++) if (ino.Blocks[i] != 0) blocks.Add(ino.Blocks[i]);
            if (ino.Blocks[12] != 0) {
                var ib = ReadBlock(ino.Blocks[12]);
                for (int i = 0; i + 4 <= ib.Length; i += 4) { uint blk = ReadU32(ib, i); if (blk == 0) break; blocks.Add(blk); }
            }
            ulong capacity = (ulong)blocks.Count * _blockSize;
            if ((ulong)Content.Length > capacity) { Panic.Error("EXT2: Not enough allocated space to grow file"); return; }

            // Write data into blocks
            int offset = 0;
            for (int i = 0; i < blocks.Count && offset < Content.Length; i++) {
                int toCopy = Content.Length - offset; if (toCopy > (int)_blockSize) toCopy = (int)_blockSize;
                var tmp = new byte[toCopy];
                for (int j = 0; j < toCopy; j++) tmp[j] = Content[offset + j];
                WriteBlock(blocks[i], tmp);
                offset += toCopy;
            }
            // Zero any remaining tail of the last block if file shrank
            if (offset < ino.SizeLo) {
                // file shrink - zero the remaining part of the current block to avoid stale data exposure
                int tail = (int)(ino.SizeLo - (uint)offset);
                if (tail > 0 && blocks.Count > 0) {
                    int usedInLast = offset % (int)_blockSize;
                    if (usedInLast > 0) {
                        var lastBuf = new byte[_blockSize];
                        // we only need to zero, so leave prefix as written; already wrote exact bytes for that block
                        for (int j = usedInLast; j < lastBuf.Length; j++) lastBuf[j] = 0;
                        WriteBlock(blocks[(offset - 1) / (int)_blockSize], lastBuf);
                    }
                }
            }

            // Update inode size
            ino.SizeLo = (uint)Content.Length;
            WriteInode(inoNum, ino);
        }

        private static void Copy(byte[] src, int sOff, byte[] dst, int dOff, int len) {
            for (int i = 0; i < len; i++) dst[dOff + i] = src[sOff + i];
        }

        public override void Format() { Panic.Error("EXT2: Format not supported"); }
    }
}
