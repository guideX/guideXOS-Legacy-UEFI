using guideXOS.FS;
using System;
namespace guideXOS.Kernel.Drivers {
    public unsafe class Ramdisk : Disk {
        byte* ptr;
        
        // WORKAROUND: Static instance for direct access since RTTI is broken in UEFI mode
        public static Ramdisk Instance;
        
        // CRITICAL: Store the raw pointer in a static field to avoid GC/object relocation issues
        public static byte* RawBasePointer;

        // Size of ramdisk in bytes (for bounds checking)
        private readonly ulong _sizeBytes;

        // Some bootloaders place a header before the TAR. We auto-detect and skip it.
        private ulong _baseOffsetBytes;
        private bool _offsetDetected;

        public Ramdisk(IntPtr _ptr) {
            BootConsole.WriteLine("[Ramdisk] Constructor called");
            
            ptr = (byte*)_ptr;
            
            // CRITICAL: Store in static field for reliable access
            RawBasePointer = ptr;
            
            // Debug: Print the pointer value we received
            ulong ptrVal = (ulong)ptr;
            Native.Out8(0x3F8, (byte)'R');
            Native.Out8(0x3F8, (byte)'P');
            Native.Out8(0x3F8, (byte)':');
            for (int shift = 60; shift >= 0; shift -= 4) {
                int nibble = (int)((ptrVal >> shift) & 0xF);
                char hexChar = (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
                Native.Out8(0x3F8, (byte)hexChar);
            }
            Native.Out8(0x3F8, (byte)'\n');
            
            BootConsole.WriteLine("[Ramdisk] ptr field set");

            _baseOffsetBytes = 0;
            _offsetDetected = false;

            // Try to find the ramdisk size from boot info if available; keep 0 when unknown.
            try {
                // Many environments store this in BootInfo; if not present it stays 0 (no bounds checks).
                // This is intentionally best-effort to avoid breaking boot if fields change.
                _sizeBytes = 0;
            } catch {
                _sizeBytes = 0;
            }
            
            // Test: Try to read first byte directly to verify memory is accessible
            if (ptr != null) {
                byte testByte = *ptr;
                if (testByte == 0) {
                    BootConsole.WriteLine("[Ramdisk] WARNING: First byte at ramdisk is ZERO");
                } else {
                    BootConsole.WriteLine("[Ramdisk] First byte OK (non-zero)");
                }
            } else {
                BootConsole.WriteLine("[Ramdisk] ERROR: ptr is null!");
            }
            
            // CRITICAL: Set both Instance pointers
            Disk.Instance = this;
            Instance = this; // Direct reference for TarFS
            
            BootConsole.WriteLine("[Ramdisk] Disk.Instance set");
        }

        private static void CopyBytes(byte* dest, byte* src, ulong len) {
            for (ulong i = 0; i < len; i++) {
                dest[i] = src[i];
            }
        }

        private static bool HasUstarMagic(byte* block512) {
            // ustar is at offset 257..261 in the TAR header
            return block512[257] == (byte)'u' &&
                   block512[258] == (byte)'s' &&
                   block512[259] == (byte)'t' &&
                   block512[260] == (byte)'a' &&
                   block512[261] == (byte)'r';
        }

        private void EnsureTarOffsetDetected() {
            if (_offsetDetected || ptr == null) return;

            _offsetDetected = true;
            _baseOffsetBytes = 0;

            // Scan first few KB for a TAR header. Keep it tiny to avoid slowing boot.
            // We assume the TAR starts on a 512-byte boundary.
            const int maxSectorsToScan = 256; // 128KB
            byte* tmp = stackalloc byte[512];

            for (int s = 0; s < maxSectorsToScan; s++) {
                byte* src = ptr + ((ulong)s * 512ul);
                CopyBytes(tmp, src, 512);
                if (HasUstarMagic(tmp)) {
                    _baseOffsetBytes = (ulong)s * 512ul;
                    BootConsole.WriteLine("[Ramdisk] TAR header found at byte offset: " + _baseOffsetBytes.ToString());
                    return;
                }
            }

            BootConsole.WriteLine("[Ramdisk] WARNING: TAR magic not found in initial scan; using offset 0");
        }

        public override bool Read(ulong sector, uint count, byte* p) {
            // CRITICAL: Log IMMEDIATELY - proves method is called
            Native.Out8(0x3F8, (byte)'R'); // Write 'R' to serial port
            Native.Out8(0x3F8, (byte)'\n');
            
            BootConsole.WriteLine("[Ramdisk] Read called");
            
            if (ptr == null) {
                BootConsole.WriteLine("[Ramdisk] ERROR: ptr is null!");
                return false;
            }

            if (p == null) {
                BootConsole.WriteLine("[Ramdisk] ERROR: destination is null!");
                return false;
            }

            EnsureTarOffsetDetected();

            ulong byteOffset = _baseOffsetBytes + (sector * 512ul);
            ulong byteCount = (ulong)count * 512ul;

            if (byteCount == 0) return true;

            // Bounds check only when size is known (>0)
            if (_sizeBytes != 0 && (byteOffset + byteCount) > _sizeBytes) {
                BootConsole.WriteLine("[Ramdisk] ERROR: Out-of-bounds read");
                return false;
            }
            
            BootConsole.WriteLine("[Ramdisk] About to copy bytes");
            
            // Calculate source address
            byte* src = ptr + byteOffset;

            CopyBytes(p, src, byteCount);

            // Debug: on first sector read, print first few bytes as hex nibbles to serial
            if (sector == 0 && count > 0) {
                BootConsole.WriteLine("[Ramdisk] Sector0 first bytes:");
                for (int i = 0; i < 8; i++) {
                    byte b = p[i];
                    char hi = (char)(((b >> 4) & 0xF) < 10 ? '0' + ((b >> 4) & 0xF) : 'A' + (((b >> 4) & 0xF) - 10));
                    char lo = (char)((b & 0xF) < 10 ? '0' + (b & 0xF) : 'A' + ((b & 0xF) - 10));
                    Native.Out8(0x3F8, (byte)hi);
                    Native.Out8(0x3F8, (byte)lo);
                    Native.Out8(0x3F8, (byte)' ');
                }
                Native.Out8(0x3F8, (byte)'\n');
            }
            
            // CRITICAL: Check first byte of EVERY read to detect zero-filled sectors
            if (count > 0) {
                byte firstByte = *p;
                if (firstByte == 0) {
                    BootConsole.WriteLine("[Ramdisk] WARNING: Read returned ZERO byte!");
                } else {
                    BootConsole.WriteLine("[Ramdisk] Read OK (non-zero byte)");
                }
            }
            
            BootConsole.WriteLine("[Ramdisk] Copy completed");
            return true;
        }

        public override bool Write(ulong sector, uint count, byte* p) {
            if (ptr == null || p == null) return false;

            EnsureTarOffsetDetected();

            ulong byteOffset = _baseOffsetBytes + (sector * 512ul);
            ulong byteCount = (ulong)count * 512ul;

            if (byteCount == 0) return true;
            if (_sizeBytes != 0 && (byteOffset + byteCount) > _sizeBytes) return false;

            CopyBytes(ptr + byteOffset, p, byteCount);
            return true;
        }

        /// <summary>
        /// Get raw pointer to ramdisk memory for direct access (used by RdskFS)
        /// </summary>
        public byte* GetRawPointer() {
            // Use static field to avoid GC/object relocation issues
            byte* result = RawBasePointer;
            
            // Debug: Print the pointer value
            ulong ptrVal = (ulong)result;
            Native.Out8(0x3F8, (byte)'P');
            Native.Out8(0x3F8, (byte)'T');
            Native.Out8(0x3F8, (byte)'R');
            Native.Out8(0x3F8, (byte)':');
            for (int shift = 60; shift >= 0; shift -= 4) {
                int nibble = (int)((ptrVal >> shift) & 0xF);
                char hexChar = (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
                Native.Out8(0x3F8, (byte)hexChar);
            }
            Native.Out8(0x3F8, (byte)'\n');
            
            return result;
        }
    }
}