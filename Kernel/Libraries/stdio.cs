using guideXOS.FS;
using guideXOS.Kernel.Helpers;
using guideXOS.Misc;
using System.Runtime;
using System.Runtime.InteropServices;
namespace guideXOS.Kernel.Libraries {
    /// <summary>
    /// The stdio.h header defines three variable types, several macros, and various functions for performing input and output.
    /// </summary>
#pragma warning disable CS8981
    internal unsafe class stdio {
#pragma warning restore CS8981
        /// <summary>
        /// Put Char
        /// </summary>
        /// <param name="chr"></param>
        [RuntimeExport("_putchar")]
        public static void _putchar(byte chr) {
            if (chr == '\n') {
                Console.WriteLine();
            } else {
                Console.Write((char)chr);
            }
        }
        /// <summary>
        /// File
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct FILE {
            /// <summary>
            /// Data
            /// </summary>
            public byte* DATA;
            /// <summary>
            /// Offset
            /// </summary>
            public long OFFSET;
            /// <summary>
            /// Length
            /// </summary>
            public long LENGTH;
        }
        /// <summary>
        /// Seek
        /// </summary>
        public enum SEEK {
            SET,
            CUR,
            END
        }

        [RuntimeExport("fopen")]
        public static FILE* fopen(byte* name, byte* mode) {
            string sname = string.FromASCII((System.IntPtr)name, StringHelper.StringLength(name));
            FILE file = new FILE();
            byte[] buffer = File.ReadAllBytes(sname);
            if (buffer == null) {
                Panic.Error("fopen: file not found!");
            }
            file.DATA = (byte*)Allocator.Allocate((ulong)buffer.Length);
            fixed (byte* p = buffer)
                Native.Movsb(file.DATA, p, (ulong)buffer.Length);
            file.LENGTH = buffer.Length;
            buffer.Dispose();
            sname.Dispose();
            return &file;
        }

        [RuntimeExport("fseek")]
        public static void fseek(FILE* handle, long offset, SEEK seek) {
            if (seek == SEEK.SET) {
                handle->OFFSET = offset;
            } else {
                Panic.Error("Unknown seek");
            }
        }

        [RuntimeExport("fread")]
        public static void fread(byte* buffer, long elementSize, long elementCount, FILE* handle) {
            Native.Movsb(buffer, handle->DATA + handle->OFFSET, (ulong)elementSize);
        }
    }
}