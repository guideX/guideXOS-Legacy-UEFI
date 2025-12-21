using guideXOS.Kernel.Helpers;
using guideXOS.Misc;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace guideXOS.FS {
    /// <summary>
    /// Tar FS
    /// </summary>
    internal unsafe class TarFS : FileSystem {
        private Disk _disk;

        public TarFS(Disk disk)
        {
            _disk = disk;
        }

        public TarFS()
        {
            _disk = Disk.Instance;
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
        /// Get Files
        /// </summary>
        /// <param name="Directory"></param>
        /// <returns></returns>
        public override List<FileInfo> GetFiles(string Directory) {
            ulong sec = 0;
            posix_tar_header hdr;
            posix_tar_header* ptr = &hdr;
            List<FileInfo> list = new();
            while (_disk.Read(sec, 1, (byte*)ptr) && hdr.name[0]) {
                sec++;
                ulong size = mystrtoul(hdr.size, null, 8);
                string name = string.FromASCII((nint)hdr.name, StringHelper.StringLength(hdr.name) - (hdr.name[StringHelper.StringLength(hdr.name) - 1] == '/' ? 1 : 0));
                if (IsInDirectory(name, Directory)) {
                    FileInfo info = new();
                    info.Param0 = sec;
                    info.Param1 = size;
                    info.Name = name.Substring(name.LastIndexOf('/') + 1);
                    if (hdr.typeflag == '5') info.Attribute |= FileAttribute.Directory;
                    list.Add(info);
                }
                name.Dispose();
                sec += SizeToSec(size);
            }
            return list;
        }
        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="Name"></param>
        public override void Delete(string Name) { }
        /// <summary>
        /// Read All Bytes
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        public override byte[] ReadAllBytes(string Name) {
            string dir = null;
            if (Name.IndexOf('/') == -1) {
                dir = "";
            } else {
                dir = $"{Name.Substring(0, Name.LastIndexOf('/'))}/";
            }
            string fname = Name.Substring(dir.Length);
            byte[] buffer = null;
            List<FileInfo> list = GetFiles(dir);
            for (int i = 0; i < list.Count; i++) {
                if (list[i].Name.Equals(fname)) {
                    buffer = new byte[(uint)SizeToSec(list[i].Param1) * 512];
                    fixed (byte* ptr = buffer) {
                        _disk.Read(list[i].Param0, (uint)SizeToSec(list[i].Param1), ptr);
                    }
                    buffer.Length = (int)list[i].Param1;
                    //Disposing
                    for (i = 0; i < list.Count; i++) {
                        list[i].Dispose();
                    }
                    list.Dispose();
                    fname.Dispose();
                    return buffer;
                }
            }
            Panic.Error($"{Name} is not found!");
            return buffer;
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